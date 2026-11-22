using System.Globalization;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Warehouse;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AAALogistics.Integration.Tests;

public sealed class InventoryWorkflowTests : IAsyncLifetime
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
        catch (Exception exception) when (
            exception is NpgsqlException
            || exception is TimeoutException
            || exception.GetBaseException() is NpgsqlException)
        {
            _databaseAvailable = false;
            if (string.Equals(
                    Environment.GetEnvironmentVariable("AAA_REQUIRE_DB"),
                    "1",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "AAA_REQUIRE_DB=1 y PostgreSQL no está disponible: "
                    + exception.GetBaseException().Message,
                    exception);
            }
        }
    }

    [Fact]
    public async Task CreateWarehouse_SeedsRootWithWarehouseCode_And_DeleteWarehouse_RemovesEmpty()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var extended = scope.ServiceProvider.GetRequiredService<IExtendedLogisticsService>();
        var suffix = Guid.CreateVersion7().ToString("N")[..6].ToUpperInvariant();
        var code = $"WH{suffix}";
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand(code, $"Almacén {code}"));
        var locations = await extended.ListLocationsAsync(warehouse.Id);
        var root = Assert.Single(locations);
        Assert.Equal("Root", root.Kind);
        Assert.Equal(code, root.Code);
        Assert.DoesNotContain(
            locations,
            row => string.Equals(row.Code, "DEFAULT", StringComparison.OrdinalIgnoreCase));

        var zone = await extended.CreateLocationAsync(
            new CreateLocationNodeCommand(
                warehouse.Id,
                "Z1",
                "Zona 1",
                LocationNodeKind.Zone,
                null));
        Assert.Equal("Zone", zone.Kind);

        // Cascada: almacén vacío (con zonas sin stock) se elimina de una vez.
        await service.DeleteWarehouseAsync(warehouse.Id);
        var remaining = await service.ListWarehousesAsync();
        Assert.DoesNotContain(remaining, row => row.Id == warehouse.Id);
    }

    [Fact]
    public async Task DeleteRootLocation_DeletesWarehouse_WhenEmpty()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var extended = scope.ServiceProvider.GetRequiredService<IExtendedLogisticsService>();
        var suffix = Guid.CreateVersion7().ToString("N")[..6].ToUpperInvariant();
        var code = $"WR{suffix}";
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand(code, $"Almacén {code}"));
        var root = Assert.Single(await extended.ListLocationsAsync(warehouse.Id));

        await extended.DeleteLocationAsync(warehouse.Id, root.Id);
        var remaining = await service.ListWarehousesAsync();
        Assert.DoesNotContain(remaining, row => row.Id == warehouse.Id);
    }

    [Fact]
    public async Task Opening_Receipt_Delivery_Concurrency_And_Audit_Are_Consistent()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider
            .GetRequiredService<ILogisticsService>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("EA", "Unidad", 0));
        var source = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("MAIN", "Principal"));
        _ = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("SECOND", "Secundario"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "MAT-001",
                "Material de integración",
                unit.Id,
                4.50m,
                "EUR",
                null));
        var supplier = await service.CreateSupplierAsync(
            Party("Proveedor integración", "INT-SUPPLIER"));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente integración", "INT-CUSTOMER"));

        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "opening-1",
                material.Id,
                source.Id,
                10m,
                StockMovementType.Opening,
                "Apertura de prueba"));

        var purchaseOrder = await service.CreatePurchaseOrderAsync(
            new CreatePurchaseOrderCommand(
                supplier.Id,
                source.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                "EUR",
                null,
                [new CreatePurchaseOrderLineCommand(material.Id, 5m, 3m)]));
        var receiptCommand = new PostGoodsReceiptCommand(
            "receipt-1",
            purchaseOrder.Id,
            DateOnly.FromDateTime(DateTime.UtcNow),
            [
                new PostGoodsReceiptLineCommand(
                    purchaseOrder.Lines.Single().Id,
                    null,
                    5m),
            ]);
        var receipt = await service.PostGoodsReceiptAsync(receiptCommand);
        var receiptRetry = await service.PostGoodsReceiptAsync(receiptCommand);
        Assert.Equal(receipt.DocumentId, receiptRetry.DocumentId);
        Assert.Equal(receipt.DocumentNumber, receiptRetry.DocumentNumber);
        Assert.Equal(receipt.MovementIds, receiptRetry.MovementIds);

        var conflict = await Assert.ThrowsAsync<LogisticsException>(() =>
            service.PostGoodsReceiptAsync(
                receiptCommand with
                {
                    DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                }));
        Assert.Equal(LogisticsErrorKind.Conflict, conflict.Kind);
        Assert.StartsWith("AAA-IDEMPOTENCY", conflict.Code, StringComparison.Ordinal);

        var salesOrder = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                source.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 4m)]));
        var confirmation =
            await service.ConfirmSalesOrderAsync(salesOrder.Id);
        Assert.Empty(confirmation.Warnings);
        var extended = scope.ServiceProvider
            .GetRequiredService<IExtendedLogisticsService>();
        var prepUserId = Guid.CreateVersion7();
        await extended.JoinPrepAsync(
            new JoinPrepCommand(salesOrder.Id, PrepLockMode.Open),
            prepUserId,
            "prep-integration");
        await ConfirmAllPrepPicksAsync(extended, salesOrder.Id, prepUserId);
        await extended.MarkPreparedAsync(salesOrder.Id, prepUserId);
        await service.PostDeliveryAsync(
            new PostDeliveryCommand(
                "delivery-1",
                salesOrder.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [
                    new PostDeliveryLineCommand(
                        salesOrder.Lines.Single().Id,
                        null,
                        4m),
                ]));

        await Task.WhenAll(
            PostConcurrentAdjustmentAsync("concurrent-a", material.Id, source.Id),
            PostConcurrentAdjustmentAsync("concurrent-b", material.Id, source.Id));

        var stock = await service.GetStockAsync();
        Assert.Equal(
            13m,
            stock.Single(value =>
                value.MaterialId == material.Id
                && value.WarehouseId == source.Id).Quantity);
        var reconciliation = await service.ReconcileStockAsync();
        Assert.True(reconciliation.IsConsistent);

        await Task.WhenAll(
            Enumerable.Range(1, 12).Select(
                CreateConcurrentAuditedWarehouseAsync));

        var customerAudit = await db.AuditLogEntries
            .AsNoTracking()
            .Where(value => value.TableName == "customers")
            .OrderBy(value => value.Sequence)
            .FirstAsync();
        Assert.NotNull(customerAudit.EncryptedNewJson);
        var plaintext = await audit.TryDecryptPersonPayloadAsync(
            customer.Id,
            customerAudit.EncryptedNewJson);
        Assert.Contains("Cliente integración", plaintext, StringComparison.Ordinal);

        var beforeShred = await audit.VerifyAsync();
        Assert.True(beforeShred.IsValid, beforeShred.Message);
        await audit.CryptoShredPersonAsync(customer.Id);
        var afterShred = await audit.VerifyAsync();
        Assert.True(afterShred.IsValid, afterShred.Message);
        Assert.Null(
            await audit.TryDecryptPersonPayloadAsync(
                customer.Id,
                customerAudit.EncryptedNewJson));
    }

    [Fact]
    public async Task Transfer_Reverse_Covers_Both_Legs_And_Opening_Rejects_Ordinary_Stock()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("BX", "Caja", 0));
        var source = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("REV-A", "Origen"));
        var destination = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("REV-B", "Destino"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "MAT-REV",
                "Material de reverso",
                unit.Id,
                1m,
                "EUR",
                null));
        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "opening-reverse",
                material.Id,
                source.Id,
                10m,
                StockMovementType.Opening,
                "Apertura previa al traslado"));
        var transfer = await service.PostTransferAsync(
            new PostTransferCommand(
                "transfer-reverse",
                material.Id,
                source.Id,
                destination.Id,
                4m,
                "Traslado de prueba"));
        Assert.Equal(2, transfer.MovementIds.Count);

        var reversal = await service.ReverseMovementAsync(
            new ReverseMovementCommand(
                "reverse-transfer",
                transfer.MovementIds.First(),
                "Reverso de ambas patas"));
        Assert.Equal(2, reversal.MovementIds.Count);

        var stock = await service.GetStockAsync();
        Assert.Equal(
            10m,
            stock.Single(value =>
                value.MaterialId == material.Id
                && value.WarehouseId == source.Id).Quantity);
        Assert.Equal(
            0m,
            stock.Single(value =>
                value.MaterialId == material.Id
                && value.WarehouseId == destination.Id).Quantity);

        var blocked = await Assert.ThrowsAsync<LogisticsException>(
            () => service.PostAdjustmentAsync(
                new PostAdjustmentCommand(
                    "opening-too-late",
                    material.Id,
                    source.Id,
                    1m,
                    StockMovementType.Opening,
                    "Apertura tardía")));
        Assert.Equal("AAA-ADJUSTMENT-0003", blocked.Code);
    }

    [Fact]
    public async Task Confirm_Creates_Reservation_And_Cancel_Releases()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("EA-ATP", "Unidad ATP", 0));
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("ATP-WH", "Almacén ATP"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "MAT-ATP",
                "Material ATP",
                unit.Id,
                1m,
                "EUR",
                null));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente ATP", "ATP-CUST-1"));

        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "atp-opening-1",
                material.Id,
                warehouse.Id,
                8m,
                StockMovementType.Opening,
                "Apertura ATP"));

        var order = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 10m)]));

        var confirmation = await service.ConfirmSalesOrderAsync(order.Id);
        Assert.Contains(
            confirmation.Warnings,
            warning => warning.Contains("sin cubrir", StringComparison.Ordinal));

        var reservations = await db.StockReservations
            .AsNoTracking()
            .Where(value => value.SalesOrderId == order.Id)
            .ToListAsync();
        Assert.Single(reservations);
        Assert.Equal(8m, reservations[0].Quantity);
        Assert.True(reservations[0].IsActive);

        var atp = await service.GetAtpAsync(material.Id, warehouse.Id);
        Assert.Equal(0m, atp.AvailableQuantity);

        await service.CancelSalesOrderAsync(order.Id, "cancelación prueba QA");

        var afterCancel = await db.StockReservations
            .AsNoTracking()
            .SingleAsync(value => value.Id == reservations[0].Id);
        Assert.False(afterCancel.IsActive);
        Assert.NotNull(afterCancel.ReleasedUtc);

        var atpAfter = await service.GetAtpAsync(material.Id, warehouse.Id);
        Assert.Equal(8m, atpAfter.AvailableQuantity);
    }

    [Fact]
    public async Task Fefo_Suggests_Earliest_Lot_And_Delivery_Consumes_It()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var extended = scope.ServiceProvider.GetRequiredService<IExtendedLogisticsService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("EA-FEFO", "Unidad FEFO", 0));
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("FEFO-WH", "Almacén FEFO"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "MAT-FEFO",
                "Material FEFO",
                unit.Id,
                2m,
                "EUR",
                null,
                RequiresLot: true));
        Assert.True(material.RequiresLot);
        var supplier = await service.CreateSupplierAsync(
            Party("Proveedor FEFO", "FEFO-SUP"));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente FEFO", "FEFO-CUST"));

        var purchaseOrder = await service.CreatePurchaseOrderAsync(
            new CreatePurchaseOrderCommand(
                supplier.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                null,
                "EUR",
                null,
                [new CreatePurchaseOrderLineCommand(material.Id, 10m, 1m)]));
        var poLineId = purchaseOrder.Lines.Single().Id;

        await service.PostGoodsReceiptAsync(
            new PostGoodsReceiptCommand(
                "fefo-gr-early",
                purchaseOrder.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [
                    new PostGoodsReceiptLineCommand(
                        poLineId,
                        null,
                        6m,
                        "LOT-EARLY",
                        new DateOnly(2026, 6, 1)),
                ]));
        await service.PostGoodsReceiptAsync(
            new PostGoodsReceiptCommand(
                "fefo-gr-late",
                purchaseOrder.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [
                    new PostGoodsReceiptLineCommand(
                        poLineId,
                        null,
                        4m,
                        "LOT-LATE",
                        new DateOnly(2027, 12, 1)),
                ]));

        var lots = await service.ListLotsAsync(material.Id);
        Assert.Equal(2, lots.Count);
        var earlyLot = lots.Single(value => value.Code == "LOT-EARLY");
        var lateLot = lots.Single(value => value.Code == "LOT-LATE");

        var suggestions = await service.SuggestPickAsync(
            material.Id,
            warehouse.Id,
            5m);
        Assert.NotEmpty(suggestions);
        Assert.Equal(earlyLot.Id, suggestions[0].LotId);
        Assert.Equal(5m, suggestions.Sum(value => value.Quantity));

        var salesOrder = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 5m)]));
        await service.ConfirmSalesOrderAsync(salesOrder.Id);
        var prepUserId = Guid.CreateVersion7();
        await extended.JoinPrepAsync(
            new JoinPrepCommand(salesOrder.Id, PrepLockMode.Open),
            prepUserId,
            "fefo-prep");
        var picks = await extended.ListPrepPicksAsync(salesOrder.Id);
        Assert.NotEmpty(picks);
        Assert.Equal(earlyLot.Id, picks.First().SuggestedLotId);
        await ConfirmAllPrepPicksAsync(extended, salesOrder.Id, prepUserId);

        await extended.MarkPreparedAsync(salesOrder.Id, prepUserId);
        await service.PostDeliveryAsync(
            new PostDeliveryCommand(
                "fefo-delivery-1",
                salesOrder.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [
                    new PostDeliveryLineCommand(
                        salesOrder.Lines.Single().Id,
                        null,
                        5m),
                ]));

        var earlyBalance = await db.StockBalances
            .AsNoTracking()
            .SingleAsync(value =>
                value.MaterialId == material.Id
                && value.LotId == earlyLot.Id);
        var lateBalance = await db.StockBalances
            .AsNoTracking()
            .SingleAsync(value =>
                value.MaterialId == material.Id
                && value.LotId == lateLot.Id);
        Assert.Equal(1m, earlyBalance.Quantity);
        Assert.Equal(4m, lateBalance.Quantity);
    }

    [Fact]
    public async Task Credit_Without_Company_Claim_Blocks_Confirm_When_Any_Terms_Exceed()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var industrial = scope.ServiceProvider
            .GetRequiredService<IIndustrialLogisticsService>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("CRU", "Unidad crédito", 0));
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("CRW", "Almacén crédito"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "CR-MAT",
                "Material crédito",
                unit.Id,
                100m,
                "EUR",
                null));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente crédito", "CR-CUST"));
        var company = await industrial.CreateCompanyAsync(
            new CreateCompanyCommand("CRCO", "Crédito Co", "A000CR01", "EUR"));
        await industrial.UpsertCommercialTermsAsync(
            new UpsertCustomerCommercialTermsCommand(
                customer.Id,
                company.Id,
                30,
                50m,
                "EUR",
                0m));

        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "credit-open-1",
                material.Id,
                warehouse.Id,
                10m,
                StockMovementType.Opening,
                "Apertura crédito"));

        var order = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 1m)]));

        // Sin claim company: términos de cualquier company bloquean (qty*100 > 50).
        var blocked = await Assert.ThrowsAsync<LogisticsException>(
            () => service.ConfirmSalesOrderAsync(order.Id));
        Assert.Equal("AAA-CREDIT-0002", blocked.Code);
    }

    [Fact]
    public async Task IssueInvoice_Applies_Fx_When_SalesOrder_Currency_Differs_From_Default_Eur()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var extended = scope.ServiceProvider
            .GetRequiredService<IExtendedLogisticsService>();
        var industrial = scope.ServiceProvider
            .GetRequiredService<IIndustrialLogisticsService>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("FXU", "Unidad FX", 0));
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("FXW", "Almacén FX"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "FX-MAT",
                "Material FX",
                unit.Id,
                10m,
                "USD",
                null));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente FX", "FX-CUST"));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await extended.UpsertIssuerAsync(
            new UpsertIssuerCommand(
                "Triple AAA Logistics SL",
                "B00000001",
                "Poligono 1",
                "Madrid",
                "28001",
                "ES"));
        db.InvoiceSeries.Add(new AAALogistics.Domain.Billing.InvoiceSeries("A", "Serie A"));
        await db.SaveChangesAsync();

        await industrial.UpsertExchangeRateAsync(
            new UpsertExchangeRateCommand(
                "USD",
                "EUR",
                0.5m,
                DateOnly.FromDateTime(DateTime.UtcNow)));

        var order = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "USD",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 1m)]));

        var invoice = await extended.IssueInvoiceAsync(
            new IssueInvoiceCommand(
                customer.Id,
                order.Id,
                "A",
                0m,
                [new IssueInvoiceLineCommand("Línea USD", 1m, 100m)],
                "fx-invoice-1"));

        // Sin company claim la factura usa EUR por defecto y convierte desde SO USD.
        Assert.Equal(50m, invoice.NetAmount);
    }

    [Fact]
    public async Task Delivery_Reverse_Correlates_MultiPick_And_Restores_Prep_By_Location()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var extended = scope.ServiceProvider.GetRequiredService<IExtendedLogisticsService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("MPU", "Unidad multi-pick", 0));
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("MPW", "Almacén multi-pick"));
        var zone = await extended.CreateLocationAsync(
            new CreateLocationNodeCommand(
                warehouse.Id,
                "Z-MP",
                "Zona MP",
                LocationNodeKind.Zone,
                null));
        var binA = await extended.CreateLocationAsync(
            new CreateLocationNodeCommand(
                warehouse.Id,
                "BIN-A",
                "Bin A",
                LocationNodeKind.Bin,
                zone.Id));
        var binB = await extended.CreateLocationAsync(
            new CreateLocationNodeCommand(
                warehouse.Id,
                "BIN-B",
                "Bin B",
                LocationNodeKind.Bin,
                zone.Id));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "MP-MAT",
                "Material multi-pick",
                unit.Id,
                1m,
                "EUR",
                null));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente multi-pick", "MP-CUST"));

        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "mp-open",
                material.Id,
                warehouse.Id,
                10m,
                StockMovementType.Opening,
                "Apertura multi-pick"));
        var defaultLocation = (await extended.ListLocationsAsync(warehouse.Id))
            .Single(value => value.Kind == "Root");
        await service.PostLocationTransferAsync(
            new PostLocationTransferCommand(
                "mp-xfer-a",
                material.Id,
                warehouse.Id,
                defaultLocation.Id,
                binA.Id,
                5m));
        await service.PostLocationTransferAsync(
            new PostLocationTransferCommand(
                "mp-xfer-b",
                material.Id,
                warehouse.Id,
                defaultLocation.Id,
                binB.Id,
                5m));

        var salesOrder = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 10m)]));
        await service.ConfirmSalesOrderAsync(salesOrder.Id);
        var prepUserId = Guid.CreateVersion7();
        await extended.JoinPrepAsync(
            new JoinPrepCommand(salesOrder.Id, PrepLockMode.Open),
            prepUserId,
            "mp-prep");
        await ConfirmAllPrepPicksAsync(extended, salesOrder.Id, prepUserId);
        await extended.MarkPreparedAsync(salesOrder.Id, prepUserId);

        var delivery = await service.PostDeliveryAsync(
            new PostDeliveryCommand(
                "mp-delivery",
                salesOrder.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [
                    new PostDeliveryLineCommand(
                        salesOrder.Lines.Single().Id,
                        null,
                        10m),
                ]));
        Assert.True(delivery.MovementIds.Count >= 2);

        var deliveryMovements = await db.StockMovements
            .AsNoTracking()
            .Where(value => delivery.MovementIds.Contains(value.Id))
            .ToListAsync();
        Assert.All(
            deliveryMovements,
            value => Assert.Equal(delivery.DocumentId, value.CorrelatedOperationId));

        var reversal = await service.ReverseMovementAsync(
            new ReverseMovementCommand(
                "mp-reverse",
                delivery.MovementIds.First(),
                "Reverso multi-pick correlacionado"));
        Assert.Equal(delivery.MovementIds.Count, reversal.MovementIds.Count);

        var prepAfter = await db.PrepPickLines
            .AsNoTracking()
            .Where(value => value.SalesOrderId == salesOrder.Id)
            .ToListAsync();
        Assert.All(prepAfter, value => Assert.Equal(0m, value.ConsumedQuantity));

        var reservationAfter = await db.StockReservations
            .AsNoTracking()
            .Where(value => value.SalesOrderId == salesOrder.Id)
            .ToListAsync();
        Assert.All(reservationAfter, value => Assert.Equal(0m, value.ConsumedQuantity));

        await using (var statusTx = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.customer_id', '', true)");
            var orderAfterReverse = await db.SalesOrders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(value => value.Id == salesOrder.Id);
            Assert.Equal(SalesOrderStatus.Prepared, orderAfterReverse.Status);
            await statusTx.CommitAsync();
        }

        // Re-entrega: RemainingConfirmed restaurado por ubicación permite re-bind sin re-confirm.
        await service.PostDeliveryAsync(
            new PostDeliveryCommand(
                "mp-redelivery",
                salesOrder.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [
                    new PostDeliveryLineCommand(
                        salesOrder.Lines.Single().Id,
                        null,
                        10m),
                ]));
        var stock = await service.GetStockAsync();
        Assert.Equal(
            0m,
            stock.Where(value => value.MaterialId == material.Id).Sum(value => value.Quantity));
    }

    [Fact]
    public async Task AllowFefoOverride_CrossWarehouse_Does_Not_Consume_Home_Reservation()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("OVU", "Unidad override", 0));
        var home = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("OV-HOME", "Casa override"));
        var other = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("OV-OTHER", "Otro override"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "OV-MAT",
                "Material override",
                unit.Id,
                1m,
                "EUR",
                null));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente override", "OV-CUST"));

        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "ov-open-home",
                material.Id,
                home.Id,
                10m,
                StockMovementType.Opening,
                "Apertura casa"));
        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "ov-open-other",
                material.Id,
                other.Id,
                10m,
                StockMovementType.Opening,
                "Apertura otro"));

        var salesOrder = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                home.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 4m)]));
        await service.ConfirmSalesOrderAsync(salesOrder.Id);
        var extended = scope.ServiceProvider.GetRequiredService<IExtendedLogisticsService>();
        var prepUserId = Guid.CreateVersion7();
        await extended.JoinPrepAsync(
            new JoinPrepCommand(salesOrder.Id, PrepLockMode.Open),
            prepUserId,
            "ov-prep");
        await ConfirmAllPrepPicksAsync(extended, salesOrder.Id, prepUserId);
        await extended.MarkPreparedAsync(salesOrder.Id, prepUserId);

        await service.PostDeliveryAsync(
            new PostDeliveryCommand(
                "ov-delivery",
                salesOrder.Id,
                DateOnly.FromDateTime(DateTime.UtcNow),
                [
                    new PostDeliveryLineCommand(
                        salesOrder.Lines.Single().Id,
                        other.Id,
                        4m),
                ],
                AllowFefoOverride: true));

        var reservation = await db.StockReservations
            .AsNoTracking()
            .SingleAsync(value => value.SalesOrderId == salesOrder.Id);
        Assert.Equal(home.Id, reservation.WarehouseId);
        Assert.Equal(0m, reservation.ConsumedQuantity);
        Assert.Equal(4m, reservation.RemainingQuantity);

        var stock = await service.GetStockAsync();
        Assert.Equal(
            10m,
            stock.Single(value =>
                value.MaterialId == material.Id
                && value.WarehouseId == home.Id).Quantity);
        Assert.Equal(
            6m,
            stock.Single(value =>
                value.MaterialId == material.Id
                && value.WarehouseId == other.Id).Quantity);
    }

    [Fact]
    public async Task SetStockStatus_Blocks_Quality_From_Fefo_And_ReturnOut_Posts()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ILogisticsService>();
        var extended = scope.ServiceProvider.GetRequiredService<IExtendedLogisticsService>();

        var unit = await service.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("STU", "Unidad status", 0));
        var warehouse = await service.CreateWarehouseAsync(
            new CreateWarehouseCommand("STW", "Almacén status"));
        var material = await service.CreateMaterialAsync(
            new CreateMaterialCommand(
                "ST-MAT",
                "Material status",
                unit.Id,
                1m,
                "EUR",
                null));
        var customer = await service.CreateCustomerAsync(
            Party("Cliente status", "ST-CUST"));

        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                "st-open",
                material.Id,
                warehouse.Id,
                5m,
                StockMovementType.Opening,
                "Apertura status"));
        var locationId = (await extended.ListLocationsAsync(warehouse.Id))
            .Single(value => value.Kind == "Root").Id;

        await service.SetStockStatusAsync(
            new SetStockStatusCommand(
                material.Id,
                warehouse.Id,
                locationId,
                Guid.Empty,
                StockStatus.Quality));

        // FEFO / SuggestPick solo considera Available; Quality queda fuera.
        var blockedSuggestions = await service.SuggestPickAsync(
            material.Id,
            warehouse.Id,
            1m);
        Assert.Empty(blockedSuggestions);

        await service.SetStockStatusAsync(
            new SetStockStatusCommand(
                material.Id,
                warehouse.Id,
                locationId,
                Guid.Empty,
                StockStatus.Available));

        var order = await service.CreateSalesOrderAsync(
            new CreateSalesOrderCommand(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "EUR",
                null,
                [new CreateSalesOrderLineCommand(material.Id, 1m)]));
        await service.ConfirmSalesOrderAsync(order.Id);

        var returnOut = await service.PostSalesReturnAsync(
            new PostSalesReturnCommand(
                "st-return-out",
                order.Id,
                null,
                StockMovementType.ReturnOut,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Salida devolución lab",
                [
                    new PostSalesReturnLineCommand(
                        material.Id,
                        warehouse.Id,
                        locationId,
                        1m),
                ]));
        Assert.NotEmpty(returnOut.MovementIds);
    }

    [Fact]
    public void OperationsHub_WarehouseGroup_Uses_Guid_D_Format()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal(
            "wh:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            AAALogistics.Api.Hosting.OperationsHub.WarehouseGroup(id));
        Assert.Equal(
            "wh:aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            AAALogistics.Api.Hosting.OperationsHub.WarehouseGroup(id.ToString("N")));
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

    private async Task PostConcurrentAdjustmentAsync(
        string key,
        Guid materialId,
        Guid warehouseId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider
            .GetRequiredService<ILogisticsService>();
        await service.PostAdjustmentAsync(
            new PostAdjustmentCommand(
                key,
                materialId,
                warehouseId,
                1m,
                StockMovementType.Adjustment,
                "Prueba concurrente"));
    }

    private async Task CreateConcurrentAuditedWarehouseAsync(int index)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider
            .GetRequiredService<ILogisticsService>();
        await service.CreateWarehouseAsync(
            new CreateWarehouseCommand(
                $"AUD-{index:D3}",
                $"Almacén auditado {index:D3}"));
    }

    private static async Task ConfirmAllPrepPicksAsync(
        IExtendedLogisticsService extended,
        Guid salesOrderId,
        Guid userId)
    {
        var picks = await extended.ListPrepPicksAsync(salesOrderId);
        foreach (var pick in picks)
        {
            await extended.ConfirmPrepPickAsync(
                new ConfirmPrepPickCommand(
                    salesOrderId,
                    pick.Id,
                    pick.Quantity,
                    userId));
        }
    }

    private static CreatePartyCommand Party(string name, string taxId) =>
        new(
            name,
            taxId,
            "Calle de prueba 1",
            null,
            "Madrid",
            null,
            "28001",
            "ES",
            null);

    private sealed class LogisticsApiFactory : WebApplicationFactory<Program>
    {
        private const string BaseConnectionString =
            "Host=127.0.0.1;Port=5432;Database=aaalogistics;Username=aaa_app;Password=aaa_dev_only_change_me";

        private readonly string _schemaName = string.Create(
            CultureInfo.InvariantCulture,
            $"integration_{Guid.CreateVersion7():N}");

        private readonly string _runtimeDirectory = Path.Combine(
            Path.GetTempPath(),
            "aaa-logistics-tests",
            Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture));

        private readonly Dictionary<string, string?> _originalEnvironment =
            new(StringComparer.Ordinal);

        public LogisticsApiFactory()
        {
            // Pooling=false keeps SET ROLE aaa_owner on the same physical connection
            // during Migrate (RLS policy DDL requires table ownership).
            SetEnvironment(
                "ConnectionStrings__Default",
                $"{BaseConnectionString};Search Path={_schemaName};Pooling=false");
            SetEnvironment("FirstBoot__AdminUserName", "phase4-admin");
            SetEnvironment(
                "FirstBoot__AdminPassword",
                "phase4-integration-password");
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
            // Migrate assumes aaa_owner (SET ROLE) for RLS policy DDL.
            await using var grant = new NpgsqlCommand(
                $"""
                DO $grant$
                BEGIN
                  IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aaa_owner') THEN
                    EXECUTE 'GRANT USAGE, CREATE ON SCHEMA {quotedSchema} TO aaa_owner';
                  END IF;
                END
                $grant$;
                """,
                connection);
            await grant.ExecuteNonQueryAsync();
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
