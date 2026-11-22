using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AAALogistics.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace AAALogistics.Integration.Tests;

public sealed class AppendixLabTests : IAsyncLifetime
{
    private const string AdminUserName = "phase1-admin";
    private const string AdminPassword = "phase1-test-password";

    private readonly AppendixApiFactory _factory = new();
    private HttpClient _client = null!;
    private bool _databaseAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            await _factory.PrepareDatabaseAsync();
            _client = _factory.CreateClient();
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
        _client?.Dispose();
        if (_databaseAvailable)
        {
            await _factory.CleanupAsync();
        }

        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RateCard_And_Uom_And_Tms_Smoke()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        using var login = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(AdminUserName, AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var payload = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);

        var units = await _client.GetFromJsonAsync<List<UnitDto>>(ApiRoutes.InternalUnits);
        Assert.NotNull(units);
        Assert.True(units.Count >= 2);

        using var upsertUom = await _client.PostAsJsonAsync(
            ApiRoutes.InternalUnitConversions,
            new UpsertUnitConversionRequestDto(units[0].Id, units[1].Id, 12m));
        Assert.Equal(HttpStatusCode.Created, upsertUom.StatusCode);

        using var convert = await _client.PostAsJsonAsync(
            ApiRoutes.InternalUnitConvert,
            new ConvertQuantityRequestDto(units[0].Id, units[1].Id, 2m));
        Assert.Equal(HttpStatusCode.OK, convert.StatusCode);
        var converted = await convert.Content.ReadFromJsonAsync<ConvertQuantityResponse>();
        Assert.NotNull(converted);
        Assert.Equal(24m, converted.OutputQuantity);

        using var rate = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRateCards,
            new UpsertRateCardRequestDto(
                "LAB",
                "Lab card",
                "EUR",
                [new UpsertRateCardLineRequestDto("PICK", 1.5m, "linea")]));
        Assert.Equal(HttpStatusCode.Created, rate.StatusCode);
        var card = await rate.Content.ReadFromJsonAsync<RateCardResponse>();
        Assert.NotNull(card);

        using var compute = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRateCardCompute(card.Id),
            new ComputeRateChargesRequestDto(
                new Dictionary<string, decimal> { ["PICK"] = 10m }));
        Assert.Equal(HttpStatusCode.OK, compute.StatusCode);
        var report = await compute.Content.ReadFromJsonAsync<RateChargeReportResponse>();
        Assert.NotNull(report);
        Assert.Equal(15m, report.Total);

        using var route = await _client.PostAsJsonAsync(
            ApiRoutes.InternalTransportRoutes,
            new CreateTransportRouteRequestDto("LABR", "Lab route", null, 10m, 5m, 1m));
        Assert.Equal(HttpStatusCode.Created, route.StatusCode);

        using var mrp = await _client.PostAsJsonAsync(
            ApiRoutes.InternalPurchaseRequisitionsGenerate,
            new GenerateMrpRequestDto(null));
        Assert.True(
            mrp.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            mrp.StatusCode.ToString());

        using var waves = await _client.GetAsync(ApiRoutes.InternalWaves);
        Assert.Equal(HttpStatusCode.OK, waves.StatusCode);
    }

    [Fact]
    public async Task Wave_Create_And_Release_With_Confirmed_SalesOrder()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await AuthenticateAdminAsync();

        using var unitResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalUnits,
            new CreateUnitOfMeasureRequest("WVU", "Wave unit", 0));
        Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
        var unit = await unitResponse.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(unit);

        using var warehouseResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalWarehouses,
            new CreateWarehouseRequest("WVW", "Wave WH"));
        Assert.Equal(HttpStatusCode.Created, warehouseResponse.StatusCode);
        var warehouse = await warehouseResponse.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(warehouse);

        using var materialResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalMaterials,
            new CreateMaterialRequest(
                "WVMAT",
                "Wave material",
                unit.Id,
                1m,
                "EUR",
                null));
        Assert.Equal(HttpStatusCode.Created, materialResponse.StatusCode);
        var material = await materialResponse.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(material);

        using var customerResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalCustomers,
            new CreatePartyRequest(
                "Wave Customer",
                "B000WAVE01",
                "Calle 1",
                null,
                "Madrid",
                null,
                "28001",
                "ES",
                null));
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customer = await customerResponse.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(customer);

        using var soResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalSalesOrders,
            new CreateSalesOrderRequest(
                customer.Id,
                warehouse.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
                "EUR",
                null,
                [new CreateSalesOrderLineRequest(material.Id, 1m)]));
        Assert.Equal(HttpStatusCode.Created, soResponse.StatusCode);
        var salesOrder = await soResponse.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(salesOrder);

        using var confirm = await _client.PostAsync(
            ApiRoutes.InternalSalesOrderConfirm(salesOrder.Id),
            null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        using var waveCreate = await _client.PostAsJsonAsync(
            ApiRoutes.InternalWaves,
            new CreateWaveRequestDto([salesOrder.Id], Priority: 1));
        Assert.Equal(HttpStatusCode.Created, waveCreate.StatusCode);
        var wave = await waveCreate.Content.ReadFromJsonAsync<WaveResponse>();
        Assert.NotNull(wave);
        Assert.Contains(salesOrder.Id, wave.SalesOrderIds);

        using var release = await _client.PostAsync(
            ApiRoutes.InternalWaveRelease(wave.Id),
            null);
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);
        var released = await release.Content.ReadFromJsonAsync<WaveResponse>();
        Assert.NotNull(released);
        Assert.Equal("Released", released.Status);
    }

    [Fact]
    public async Task Industrial_Company_Fx_Sap_And_RfScan_Smoke()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await AuthenticateAdminAsync();

        using var companyResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalCompanies,
            new { code = "LABCO", legalName = "Lab Co", taxId = "A000LAB01", currency = "EUR" });
        Assert.True(
            companyResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            companyResponse.StatusCode.ToString());

        using var fxResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalExchangeRates,
            new UpsertExchangeRateRequestDto(
                "USD",
                "EUR",
                0.92m,
                DateOnly.FromDateTime(DateTime.UtcNow)));
        Assert.True(
            fxResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            fxResponse.StatusCode.ToString());

        using var convert = await _client.PostAsJsonAsync(
            ApiRoutes.InternalFxConvert,
            new ConvertFxRequestDto("USD", "EUR", 100m, null));
        Assert.Equal(HttpStatusCode.OK, convert.StatusCode);
        var fx = await convert.Content.ReadFromJsonAsync<FxConvertResponse>();
        Assert.NotNull(fx);
        Assert.Equal(92m, fx.OutputAmount);

        using var sap = await _client.GetAsync(ApiRoutes.InternalSapMaterials);
        Assert.Equal(HttpStatusCode.OK, sap.StatusCode);

        using var unitResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalUnits,
            new CreateUnitOfMeasureRequest("RFU", "RF unit", 0));
        Assert.Equal(HttpStatusCode.Created, unitResponse.StatusCode);
        var unit = await unitResponse.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(unit);

        using var materialResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalMaterials,
            new CreateMaterialRequest(
                "RFMAT-OK",
                "RF material",
                unit.Id,
                1m,
                "EUR",
                null));
        Assert.Equal(HttpStatusCode.Created, materialResponse.StatusCode);
        var material = await materialResponse.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(material);

        using var rfHit = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRfScan,
            new RfScanRequestDto("RFMAT-OK"));
        Assert.Equal(HttpStatusCode.OK, rfHit.StatusCode);
        var scanned = await rfHit.Content.ReadFromJsonAsync<RfScanResponseDto>();
        Assert.NotNull(scanned);
        Assert.Equal("Barcode", scanned.Kind);
        Assert.Equal(material.Id, scanned.ResolvedId);

        using var rfMiss = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRfScan,
            new RfScanRequestDto("NO-SUCH-CODE-XYZ"));
        Assert.Equal(HttpStatusCode.NotFound, rfMiss.StatusCode);
    }

    private async Task AuthenticateAdminAsync()
    {
        using var login = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(AdminUserName, AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var payload = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);
    }

    private sealed class AppendixApiFactory : WebApplicationFactory<Program>
    {
        private const string BaseConnectionString =
            "Host=127.0.0.1;Port=5432;Database=aaalogistics;Username=aaa_app;Password=aaa_dev_only_change_me";

        private readonly string _schemaName = string.Create(
            CultureInfo.InvariantCulture,
            $"appendix_{Guid.CreateVersion7():N}");

        private readonly string _runtimeDirectory = Path.Combine(
            Path.GetTempPath(),
            "aaa-logistics-tests",
            Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture));

        private readonly Dictionary<string, string?> _originalEnvironment =
            new(StringComparer.Ordinal);

        public AppendixApiFactory()
        {
            SetEnvironment(
                "ConnectionStrings__Default",
                $"{BaseConnectionString};Search Path={_schemaName};Pooling=false");
            SetEnvironment("FirstBoot__AdminUserName", AdminUserName);
            SetEnvironment("FirstBoot__AdminPassword", AdminPassword);
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

    private sealed class UnitDto
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;
    }

    private sealed class IdDto
    {
        public Guid Id { get; set; }
    }
}
