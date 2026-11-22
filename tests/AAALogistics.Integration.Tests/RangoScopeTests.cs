using System.Globalization;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Billing;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Warehouse;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AAALogistics.Integration.Tests;

public sealed class RangoScopeTests : IAsyncLifetime
{
    private readonly LogisticsApiFactory _factory = new();
    private bool _started;
    private bool _databaseAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            await _factory.PrepareDatabaseAsync();
            _ = _factory.CreateClient();
            _started = true;
            _databaseAvailable = true;
        }
        catch (NpgsqlException)
        {
            _databaseAvailable = false;
        }
        catch (TimeoutException)
        {
            _databaseAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            await _factory.DisposeAsync();
        }

        if (_databaseAvailable)
        {
            await _factory.CleanupAsync();
        }
    }

    [Fact]
    public async Task Prep_Locations_Invoice_Cmr_And_Deletion_Flow()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var logistics = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var extended = scope.ServiceProvider.GetRequiredService<IExtendedLogisticsService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var unit = await logistics.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("U3PL", "Unidad 3PL", 0));
        var warehouse = await logistics.CreateWarehouseAsync(
            new CreateWarehouseCommand("W3PL", "Campa 3PL"));
        var location = await extended.CreateLocationAsync(
            new CreateLocationNodeCommand(
                warehouse.Id,
                "Z1",
                "Zona 1",
                LocationNodeKind.Zone,
                null));
        var child = await extended.CreateLocationAsync(
            new CreateLocationNodeCommand(
                warehouse.Id,
                "C1",
                "Columna 1",
                LocationNodeKind.Column,
                location.Id));
        Assert.Equal(location.Id, child.ParentId);

        var material = await logistics.CreateMaterialAsync(
            new CreateMaterialCommand(
                "MAT-3PL",
                "Material 3PL",
                unit.Id,
                10m,
                "EUR",
                null));
        var customer = await logistics.CreateCustomerAsync(
            new CreatePartyCommand(
                "Cliente 3PL",
                "B12345678",
                "Calle 1",
                null,
                "Madrid",
                null,
                "28001",
                "ES",
                null));

        var order = await logistics.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 2m)]));
        await logistics.ConfirmSalesOrderAsync(order.Id);

        var userA = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();
        var sessions = await extended.JoinPrepAsync(
            new JoinPrepCommand(order.Id, PrepLockMode.Exclusive),
            userA,
            "operario-a");
        Assert.Single(sessions);

        var conflict = await Assert.ThrowsAsync<LogisticsException>(() =>
            extended.JoinPrepAsync(
                new JoinPrepCommand(order.Id, PrepLockMode.Open),
                userB,
                "operario-b"));
        Assert.Equal(LogisticsErrorKind.Conflict, conflict.Kind);

        var picks = await extended.ListPrepPicksAsync(order.Id);
        foreach (var pick in picks)
        {
            await extended.ConfirmPrepPickAsync(
                new ConfirmPrepPickCommand(
                    order.Id,
                    pick.Id,
                    pick.Quantity,
                    userA));
        }

        await extended.MarkPreparedAsync(order.Id, userA);
        var prepared = await db.SalesOrders.AsNoTracking()
            .SingleAsync(value => value.Id == order.Id);
        Assert.Equal(SalesOrderStatus.Prepared, prepared.Status);

        // Cierre administrativo: cancela abierta y cierra (pedido preparado sin DN).
        await logistics.CloseSalesOrderAsync(order.Id);

        var carrier = await extended.CreateCarrierAsync(
            new CreateCarrierCommand("TR1", "Transportes SA", null, null));
        var cmr = await extended.CreateCmrAsync(
            new CreateCmrCommand(order.Id, null, carrier.Id, "1234-ABC", 2, null));
        var cmrPdf = await extended.RenderCmrPdfAsync(cmr.Id);
        Assert.True(cmrPdf.Length > 20);

        await extended.UpsertIssuerAsync(
            new UpsertIssuerCommand(
                "Triple AAA Logistics SL",
                "B00000001",
                "Poligono 1",
                "Madrid",
                "28001",
                "ES",
                SignerName: "Administrador Demo",
                SignerTitle: "Administrador"));
        db.InvoiceSeries.Add(new InvoiceSeries("A", "Serie A"));
        db.InvoiceSeries.Add(new InvoiceSeries("RA", "Rectificativas A"));
        await db.SaveChangesAsync();
        var invoice = await extended.IssueInvoiceAsync(
            new IssueInvoiceCommand(
                customer.Id,
                order.Id,
                "A",
                21m,
                [new IssueInvoiceLineCommand("Servicio 3PL", 1m, 100m)],
                "inv-rango-" + Guid.CreateVersion7().ToString("N")));
        Assert.False(string.IsNullOrWhiteSpace(invoice.SifHash));
        Assert.StartsWith("A-", invoice.DocumentNumber, StringComparison.Ordinal);
        var invoicePdf = await extended.RenderInvoicePdfAsync(invoice.Id);
        Assert.True(invoicePdf.Length > 20);

        var rect = await extended.RectifyInvoiceAsync(
            invoice.Id,
            "Error cantidad",
            "rect-rango-" + Guid.CreateVersion7().ToString("N"));
        Assert.Equal("Rectifying", rect.Kind);
        Assert.Null(rect.PreviousSifHash); // first hash in rectificativa series RA
        Assert.Contains(
            await db.InvoiceLines.AsNoTracking()
                .Where(value => value.InvoiceId == rect.Id)
                .Select(value => value.Description)
                .ToListAsync(),
            description => description.Contains("Error cantidad", StringComparison.Ordinal));

        var doubleRectify = await Assert.ThrowsAsync<LogisticsException>(() =>
            extended.RectifyInvoiceAsync(
                invoice.Id,
                "Segundo intento",
                "rect-rango-2-" + Guid.CreateVersion7().ToString("N")));
        Assert.Equal("AAA-INV-0004", doubleRectify.Code);

        var invoiceA2 = await extended.IssueInvoiceAsync(
            new IssueInvoiceCommand(
                customer.Id,
                order.Id,
                "A",
                21m,
                [new IssueInvoiceLineCommand("Segunda A", 1m, 10m)],
                "inv-series-a2-" + Guid.CreateVersion7().ToString("N")));
        Assert.Equal(invoice.SifHash, invoiceA2.PreviousSifHash);

        db.InvoiceSeries.Add(new InvoiceSeries("B", "Serie B"));
        await db.SaveChangesAsync();
        var invoiceB = await extended.IssueInvoiceAsync(
            new IssueInvoiceCommand(
                customer.Id,
                order.Id,
                "B",
                21m,
                [new IssueInvoiceLineCommand("Serie B", 1m, 50m)],
                "inv-series-b-" + Guid.CreateVersion7().ToString("N")));
        Assert.Null(invoiceB.PreviousSifHash);

        var missingSeries = await Assert.ThrowsAsync<LogisticsException>(() =>
            extended.IssueInvoiceAsync(
                new IssueInvoiceCommand(
                    customer.Id,
                    null,
                    "Z",
                    21m,
                    [new IssueInvoiceLineCommand("Sin serie", 1m, 1m)],
                    "inv-missing-" + Guid.CreateVersion7().ToString("N"))));
        Assert.Equal("AAA-INV-SERIES", missingSeries.Code);

        // Seed stock on warehouse Root via opening, then transfer to child location.
        await logistics.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "open-3pl-" + Guid.CreateVersion7().ToString("N"),
                material.Id,
                warehouse.Id,
                5m,
                Domain.Inventory.StockMovementType.Opening,
                "Apertura WMS"));
        var defaultLocation = (await extended.ListLocationsAsync(warehouse.Id))
            .Single(value => value.Kind == "Root");
        await logistics.PostLocationTransferAsync(
            new PostLocationTransferCommand(
                "loc-xfer-" + Guid.CreateVersion7().ToString("N"),
                material.Id,
                warehouse.Id,
                defaultLocation.Id,
                child.Id,
                2m));
        var stock = await logistics.GetStockAsync();
        Assert.Contains(
            stock,
            row => row.MaterialId == material.Id
                && row.LocationId == child.Id
                && row.Quantity == 2m);

        var hu = await extended.CreateHandlingUnitAsync(
            new CreateHandlingUnitCommand(order.Id, null, 2));
        Assert.False(string.IsNullOrWhiteSpace(hu.Sscc));
        Assert.Equal(2, hu.PackageCount);
        var packingUnits = await extended.ListHandlingUnitsByOrderAsync(order.Id);
        Assert.Contains(packingUnits, value => value.Id == hu.Id);
        var deliveryIds = await db.Deliveries.AsNoTracking()
            .Where(value => value.SalesOrderId == order.Id)
            .Select(value => value.Id)
            .ToListAsync();
        Assert.NotEmpty(deliveryIds);
        var packingPdf = await extended.RenderPackingListPdfAsync(deliveryIds[0]);
        Assert.True(packingPdf.Length > 4);
        Assert.Equal((byte)'%', packingPdf[0]);
        Assert.Equal((byte)'P', packingPdf[1]);
        Assert.Equal((byte)'D', packingPdf[2]);
        Assert.Equal((byte)'F', packingPdf[3]);

        var returnResult = await logistics.PostSalesReturnAsync(
            new PostSalesReturnCommand(
                "ret-" + Guid.CreateVersion7().ToString("N"),
                order.Id,
                null,
                Domain.Inventory.StockMovementType.ReturnIn,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Devolución aula",
                [
                    new PostSalesReturnLineCommand(
                        material.Id,
                        warehouse.Id,
                        child.Id,
                        1m),
                ]));
        Assert.NotEmpty(returnResult.MovementIds);

        var otif = await logistics.ReportOtifAsync();
        Assert.True(otif.DeliveredOrders >= 0);

        var deletion = await extended.RequestMaterialDeletionAsync(
            material.Id,
            userA,
            "Obsoleto sin movimiento");
        // Has stock after opening — remains pending for 4-eyes.
        Assert.True(deletion.HasStock);
        Assert.Equal("Pending", deletion.Status);
        await extended.ApproveMaterialDeletionAsync(deletion.Id, userB);
    }

    private sealed class LogisticsApiFactory : WebApplicationFactory<Program>
    {
        private const string BaseConnectionString =
            "Host=127.0.0.1;Port=5432;Database=aaalogistics;Username=aaa_app;Password=aaa_dev_only_change_me";

        private readonly string _schemaName = string.Create(
            CultureInfo.InvariantCulture,
            $"rango_{Guid.CreateVersion7():N}");

        private readonly string _runtimeDirectory = Path.Combine(
            Path.GetTempPath(),
            "aaa-logistics-tests",
            Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture));

        private readonly Dictionary<string, string?> _originalEnvironment =
            new(StringComparer.Ordinal);

        public LogisticsApiFactory()
        {
            SetEnvironment(
                "ConnectionStrings__Default",
                $"{BaseConnectionString};Search Path={_schemaName};Pooling=true;Maximum Pool Size=20");
            SetEnvironment("FirstBoot__AdminUserName", "rango-admin");
            SetEnvironment(
                "FirstBoot__AdminPassword",
                "rango-integration-password");
            SetEnvironment(
                "Jwt__SigningKeyPath",
                Path.Combine(_runtimeDirectory, "jwt.key"));
            SetEnvironment(
                "Audit__PiiMasterKeyPath",
                Path.Combine(_runtimeDirectory, "pii-master.key"));
            SetEnvironment(
                "Backups__Directory",
                Path.Combine(_runtimeDirectory, "backups"));
            SetEnvironment(
                "Backups__EncryptionKeyPath",
                Path.Combine(_runtimeDirectory, "backup.key"));
            SetEnvironment("Demo__AllowSeed", "true");
            SetEnvironment("Email__Mode", "Disabled");
            SetEnvironment(
                AAALogistics.Host.Hosting.SingleInstanceLock.DisableEnvironmentVariable,
                "true");
        }

        public async Task PrepareDatabaseAsync()
        {
            await using var connection =
                new NpgsqlConnection($"{BaseConnectionString};Pooling=false");
            await connection.OpenAsync();
            using var commandBuilder = new NpgsqlCommandBuilder();
            var quotedSchema = commandBuilder.QuoteIdentifier(_schemaName);
            await using var command = new NpgsqlCommand(
                $"CREATE SCHEMA {quotedSchema}",
                connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task CleanupAsync()
        {
            try
            {
                NpgsqlConnection.ClearAllPools();
                await using var connection =
                    new NpgsqlConnection($"{BaseConnectionString};Pooling=false");
                await connection.OpenAsync();
                using var commandBuilder = new NpgsqlCommandBuilder();
                var quotedSchema = commandBuilder.QuoteIdentifier(_schemaName);
                await using var command = new NpgsqlCommand(
                    $"DROP SCHEMA IF EXISTS {quotedSchema} CASCADE",
                    connection);
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                foreach (var (name, value) in _originalEnvironment)
                {
                    Environment.SetEnvironmentVariable(name, value);
                }

                if (Directory.Exists(_runtimeDirectory))
                {
                    Directory.Delete(_runtimeDirectory, recursive: true);
                }
            }
        }

        private void SetEnvironment(string name, string value)
        {
            _originalEnvironment[name] =
                Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}
