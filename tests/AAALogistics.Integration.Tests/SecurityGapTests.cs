using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Security;
using AAALogistics.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AAALogistics.Integration.Tests;

public sealed class SecurityGapTests : IAsyncLifetime
{
    private const string AdminUserName = "gap-admin";
    private const string AdminPassword = "gap-admin-password";

    private readonly GapApiFactory _factory = new();
    private HttpClient _client = null!;
    private bool _started;
    private bool _databaseAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            await _factory.PrepareDatabaseAsync();
            _client = _factory.CreateClient();
            _started = true;
            _databaseAvailable = true;
        }
        catch (NpgsqlException exception)
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("AAA_REQUIRE_DB"),
                    "1",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "AAA_REQUIRE_DB=1 y PostgreSQL no está disponible.",
                    exception);
            }

            _databaseAvailable = false;
        }
        catch (TimeoutException exception)
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("AAA_REQUIRE_DB"),
                    "1",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "AAA_REQUIRE_DB=1 y PostgreSQL no está disponible.",
                    exception);
            }

            _databaseAvailable = false;
        }
    }

    [Fact]
    public async Task Refresh_Rotates_And_Reuse_Revokes_The_Family()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        var first = await LoginAdminAsync();
        using var rotatedResponse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        var rotated = await rotatedResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);

        using var reuse = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(first.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        using var descendant = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, descendant.StatusCode);
    }

    [Fact]
    public async Task Portal_Customers_Cannot_Read_Each_Others_Sales_Orders()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        var admin = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        var unit = await PostCreatedAsync<IdResponse>(
            "/api/v1/internal/units",
            new CreateUnitOfMeasureRequest("EA", "Unidad", 0));
        var warehouse = await PostCreatedAsync<IdResponse>(
            "/api/v1/internal/warehouses",
            new CreateWarehouseRequest("ISO", "Aislado"));
        var material = await PostCreatedAsync<IdResponse>(
            "/api/v1/internal/materials",
            new CreateMaterialRequest(
                "ISO-1",
                "Material aislado",
                unit.Id,
                2m,
                "EUR",
                null));
        var customerA = await PostCreatedAsync<IdResponse>(
            "/api/v1/internal/customers",
            Party("Cliente A", "TAX-A"));
        var customerB = await PostCreatedAsync<IdResponse>(
            "/api/v1/internal/customers",
            Party("Cliente B", "TAX-B"));
        var orderA = await PostCreatedAsync<IdResponse>(
            "/api/v1/internal/sales-orders",
            Order(customerA.Id, warehouse.Id, material.Id));
        var orderB = await PostCreatedAsync<IdResponse>(
            "/api/v1/internal/sales-orders",
            Order(customerB.Id, warehouse.Id, material.Id));

        await CreatePortalUserAsync("portal-a", "portal-a-password", customerA.Id);
        await CreatePortalUserAsync("portal-b", "portal-b-password", customerB.Id);
        await CreatePortalUserAsync("portal-orphan", "portal-orphan-password", null);

        using var orphan = await _client.PostAsJsonAsync(
            ApiRoutes.PortalLogin,
            new LoginRequest("portal-orphan", "portal-orphan-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, orphan.StatusCode);

        var tokenA = await PortalTokenAsync("portal-a", "portal-a-password");
        var tokenB = await PortalTokenAsync("portal-b", "portal-b-password");
        var seenByA = await PortalOrdersAsync(tokenA);
        var seenByB = await PortalOrdersAsync(tokenB);

        Assert.Contains(seenByA, value => value.Id == orderA.Id);
        Assert.DoesNotContain(seenByA, value => value.Id == orderB.Id);
        Assert.Contains(seenByB, value => value.Id == orderB.Id);
        Assert.DoesNotContain(seenByB, value => value.Id == orderA.Id);
        Assert.All(seenByA, value => Assert.Equal(customerA.Id, value.CustomerId));

        await AssertDatabaseIsolationAsync(customerA.Id, orderA.Id, orderB.Id);
    }

    [Fact]
    public async Task Portal_Customers_Cannot_Read_Each_Others_Invoices()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        var admin = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var extended = scope.ServiceProvider
            .GetRequiredService<IExtendedLogisticsService>();
        var logistics = scope.ServiceProvider
            .GetRequiredService<ILogisticsService>();

        var unit = await logistics.CreateUnitOfMeasureAsync(
            new CreateUnitOfMeasureCommand("INV", "Unidad inv", 0));
        var warehouse = await logistics.CreateWarehouseAsync(
            new CreateWarehouseCommand("INVW", "Almacén inv"));
        var material = await logistics.CreateMaterialAsync(
            new CreateMaterialCommand(
                "INV-M",
                "Material factura",
                unit.Id,
                10m,
                "EUR",
                null));
        var customerA = await logistics.CreateCustomerAsync(
            new CreatePartyCommand(
                "Factura A",
                "TAX-FA",
                "Calle 1",
                null,
                "Madrid",
                null,
                "28001",
                "ES",
                null));
        var customerB = await logistics.CreateCustomerAsync(
            new CreatePartyCommand(
                "Factura B",
                "TAX-FB",
                "Calle 1",
                null,
                "Madrid",
                null,
                "28001",
                "ES",
                null));

        var invoiceA = await extended.IssueInvoiceAsync(
            new IssueInvoiceCommand(
                customerA.Id,
                null,
                "A",
                21m,
                [new IssueInvoiceLineCommand("Servicio A", 1m, 100m)],
                "inv-rls-a"));
        var invoiceB = await extended.IssueInvoiceAsync(
            new IssueInvoiceCommand(
                customerB.Id,
                null,
                "A",
                21m,
                [new IssueInvoiceLineCommand("Servicio B", 1m, 50m)],
                "inv-rls-b"));

        await CreatePortalUserAsync("portal-inv-a", "portal-inv-a-password", customerA.Id);
        await CreatePortalUserAsync("portal-inv-b", "portal-inv-b-password", customerB.Id);
        var tokenA = await PortalTokenAsync("portal-inv-a", "portal-inv-a-password");
        var tokenB = await PortalTokenAsync("portal-inv-b", "portal-inv-b-password");

        var seenByA = await PortalInvoicesAsync(tokenA);
        var seenByB = await PortalInvoicesAsync(tokenB);
        Assert.Contains(seenByA, value => value.Id == invoiceA.Id);
        Assert.DoesNotContain(seenByA, value => value.Id == invoiceB.Id);
        Assert.Contains(seenByB, value => value.Id == invoiceB.Id);
        Assert.DoesNotContain(seenByB, value => value.Id == invoiceA.Id);

        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var set = new NpgsqlCommand(
            "SELECT set_config('app.customer_id', @id, true)",
            connection,
            transaction))
        {
            set.Parameters.AddWithValue("id", customerA.Id.ToString("D"));
            await set.ExecuteScalarAsync();
        }

        await using var lines = new NpgsqlCommand(
            "SELECT COUNT(*) FROM invoice_lines",
            connection,
            transaction);
        // PG RLS on invoice_lines must hide B's lines when customer_id=A.
        var count = (long)(await lines.ExecuteScalarAsync() ?? 0L);
        Assert.Equal(1L, count);
    }

    [Fact]
    public async Task Registration_Honeypot_Terms_And_Approval_Create_A_Portal_User()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        using var honeypot = await _client.PostAsJsonAsync(
            ApiRoutes.PortalRegistration,
            Registration("HONEY", website: "https://spam.example"));
        Assert.Equal(HttpStatusCode.NoContent, honeypot.StatusCode);

        using var missingTerms = await _client.PostAsJsonAsync(
            ApiRoutes.PortalRegistration,
            Registration("TERMS", acceptTerms: false));
        Assert.Equal(HttpStatusCode.BadRequest, missingTerms.StatusCode);

        using var created = await _client.PostAsJsonAsync(
            ApiRoutes.PortalRegistration,
            Registration("READY"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var submitted = await created.Content.ReadFromJsonAsync<RegistrationSubmittedResponse>();
        Assert.NotNull(submitted);
        Assert.Equal("Pending", submitted.Status);

        var admin = await LoginAdminAsync();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        using var pending = await _client.GetAsync(
            ApiRoutes.InternalRegistrationRequests);
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);

        using var approved = await _client.PostAsync(
            $"{ApiRoutes.InternalRegistrationRequests}/{submitted.Id}/approve",
            content: null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var decision = await approved.Content.ReadFromJsonAsync<RegistrationDecision>();
        Assert.NotNull(decision);
        Assert.Equal("Approved", decision.Status);
        Assert.NotNull(decision.CustomerId);
        Assert.False(string.IsNullOrWhiteSpace(decision.TemporaryPassword));

        _client.DefaultRequestHeaders.Authorization = null;
        using var portalLogin = await _client.PostAsJsonAsync(
            ApiRoutes.PortalLogin,
            new LoginRequest("ready-user", decision.TemporaryPassword));
        Assert.Equal(HttpStatusCode.OK, portalLogin.StatusCode);
    }

    [Fact]
    public async Task Backup_Fails_Closed_When_PgDump_Is_Missing()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var backups = scope.ServiceProvider.GetRequiredService<IBackupService>();
        var error = await Assert.ThrowsAsync<LogisticsException>(
            () => backups.CreateAsync(gzip: false));
        Assert.Equal("AAA-BACKUP-0001", error.Code);
        Assert.Contains("pg_dump", error.Message, StringComparison.OrdinalIgnoreCase);

        var refused = await Assert.ThrowsAsync<LogisticsException>(
            () => backups.RestoreAsync("/tmp/no-existe.dump", "no"));
        Assert.Equal("AAA-BACKUP-0002", refused.Code);
    }

    [Fact]
    public async Task Concurrent_Refresh_Of_Same_Token_Leaves_One_Valid_Family()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        var session = await LoginAdminAsync();
        var first = _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(session.RefreshToken));
        var second = _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(session.RefreshToken));
        var responses = await Task.WhenAll(first, second);
        var statuses = responses.Select(value => value.StatusCode).Order().ToArray();
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Unauthorized, statuses);

        LoginResponse? survivor = null;
        foreach (var response in responses)
        {
            if (response.StatusCode == HttpStatusCode.OK)
            {
                survivor = await response.Content.ReadFromJsonAsync<LoginResponse>();
            }

            response.Dispose();
        }

        Assert.NotNull(survivor);
        using var reuseOriginal = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseOriginal.StatusCode);

        using var next = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(survivor.RefreshToken));
        Assert.True(
            next.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized,
            "La familia puede quedar revocada si el segundo hilo detectó reuso.");
    }

    [Fact]
    public async Task Logout_And_Suspend_Invalidate_Session()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        var session = await LoginAdminAsync();
        using (var logout = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogout,
            new LogoutRequest(session.RefreshToken)))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        using (var refresh = await _client.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(session.RefreshToken)))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }

        const string victimName = "gap-suspend-target";
        const string victimPassword = "gap-suspend-password";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var victim = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = victimName,
                LockoutEnabled = true,
            };
            Assert.True((await users.CreateAsync(victim, victimPassword)).Succeeded);
            Assert.True((await users.AddToRoleAsync(victim, AppRoles.Almacen)).Succeeded);
        }

        using var victimLogin = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(victimName, victimPassword));
        Assert.Equal(HttpStatusCode.OK, victimLogin.StatusCode);
        var victimSession =
            await victimLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(victimSession);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var victim = await users.FindByNameAsync(victimName);
            Assert.NotNull(victim);
            var accounts = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
            Assert.True(
                await accounts.SuspendAsync(victim.Id, "prueba de seguridad"));
        }

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", victimSession.AccessToken);
        using var me = await _client.GetAsync(ApiRoutes.InternalMe);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task Registration_Submit_Persists_Legal_Acceptance_With_Requester_Context()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        using var submit = await _client.PostAsJsonAsync(
            ApiRoutes.PortalRegistration,
            Registration("LEGALOK"));
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var created = await submit.Content.ReadFromJsonAsync<RegistrationRequestView>();
        Assert.NotNull(created);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AAALogistics.Infrastructure.Persistence.ApplicationDbContext>();
        var rls = scope.ServiceProvider
            .GetRequiredService<AAALogistics.Application.Logistics.ICustomerRlsSession>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await rls.ApplyAsync();
        var acceptance = await db.LegalAcceptances
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.RegistrationRequestId == created.Id);
        await tx.CommitAsync();
        Assert.NotNull(acceptance);
        Assert.Equal("2026-10", acceptance.Version);
        Assert.Null(acceptance.CustomerId);
    }

    public async Task DisposeAsync()
    {
        if (_started)
        {
            _client.Dispose();
        }

        await _factory.DisposeAsync();
        if (_databaseAvailable)
        {
            await _factory.CleanupAsync();
        }
    }

    private async Task<LoginResponse> LoginAdminAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(AdminUserName, AdminPassword));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        return payload;
    }

    private async Task<string> PortalTokenAsync(string userName, string password)
    {
        using var response = await _client.PostAsJsonAsync(
            ApiRoutes.PortalLogin,
            new LoginRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload.AccessToken;
    }

    private async Task<List<SalesOrderView>> PortalOrdersAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            ApiRoutes.PortalSalesOrders);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<SalesOrderView>>();
        return orders ?? [];
    }

    private async Task<List<InvoiceResponse>> PortalInvoicesAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            ApiRoutes.PortalInvoices);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoices = await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>();
        return invoices ?? [];
    }

    private async Task<T> PostCreatedAsync<T>(string path, object body)
    {
        using var response = await _client.PostAsJsonAsync(path, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            payload);
        var value = JsonSerializer.Deserialize<T>(
            payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(value);
        return value;
    }

    private async Task CreatePortalUserAsync(
        string userName,
        string password,
        Guid? customerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = userName,
            CustomerId = customerId,
            LockoutEnabled = true,
        };
        var created = await userManager.CreateAsync(user, password);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors.Select(error => error.Description)));
        var role = await userManager.AddToRoleAsync(user, AppRoles.Customer);
        Assert.True(role.Succeeded, string.Join("; ", role.Errors.Select(error => error.Description)));
    }

    private async Task AssertDatabaseIsolationAsync(
        Guid customerId,
        Guid ownOrderId,
        Guid otherOrderId)
    {
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var clear = new NpgsqlCommand(
            "SELECT set_config('app.customer_id', '', true)",
            connection,
            transaction))
        {
            await clear.ExecuteScalarAsync();
        }

        Assert.Equal(2, await CountOrdersAsync(connection, transaction));

        await using (var set = new NpgsqlCommand(
            "SELECT set_config('app.customer_id', @id, true)",
            connection,
            transaction))
        {
            set.Parameters.AddWithValue("id", customerId.ToString("D"));
            await set.ExecuteScalarAsync();
        }

        Assert.Equal(1, await CountOrdersAsync(connection, transaction));
        await using var own = new NpgsqlCommand(
            "SELECT COUNT(*) FROM sales_orders WHERE id = @id",
            connection,
            transaction);
        own.Parameters.AddWithValue("id", ownOrderId);
        Assert.Equal(1L, (long)(await own.ExecuteScalarAsync() ?? 0L));
        await using var other = new NpgsqlCommand(
            "SELECT COUNT(*) FROM sales_orders WHERE id = @id",
            connection,
            transaction);
        other.Parameters.AddWithValue("id", otherOrderId);
        Assert.Equal(0L, (long)(await other.ExecuteScalarAsync() ?? 0L));
    }

    private static async Task<long> CountOrdersAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM sales_orders",
            connection,
            transaction);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static CreatePartyRequest Party(string name, string taxId) =>
        new(name, taxId, "Calle 1", null, "Madrid", null, "28001", "ES", null);

    private static CreateSalesOrderRequest Order(
        Guid customerId,
        Guid warehouseId,
        Guid materialId) =>
        new(
            customerId,
            warehouseId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            "EUR",
            null,
            [new CreateSalesOrderLineRequest(materialId, 1m)]);

    private static SubmitRegistrationRequest Registration(
        string suffix,
        bool acceptTerms = true,
        string? website = null) =>
        new(
            $"Empresa {suffix}",
            $"TAX-{suffix}",
            "Contacto",
            $"{suffix.ToLowerInvariant()}@example.com",
            suffix.Equals("READY", StringComparison.Ordinal) ? "ready-user" : $"user-{suffix.ToLowerInvariant()}",
            "Calle 1",
            "Madrid",
            "28001",
            "ES",
            acceptTerms,
            website);

    private sealed class IdResponse
    {
        public Guid Id { get; set; }
    }

    private sealed class GapApiFactory : WebApplicationFactory<Program>
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

        public GapApiFactory()
        {
            ConnectionString =
                $"{BaseConnectionString};Search Path={_schemaName};Pooling=false";
            SetEnvironment("ConnectionStrings__Default", ConnectionString);
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
            SetEnvironment("Backups__PgDumpPath", "/nonexistent/pg_dump");
            SetEnvironment("Backups__AllowSqlFallback", "false");
            SetEnvironment("Legal__RequireLegalTexts", "true");
            SetEnvironment("Legal__CurrentVersion", "2026-10");
            SetEnvironment("Email__Mode", "Disabled");
            SetEnvironment(
                AAALogistics.Host.Hosting.SingleInstanceLock.DisableEnvironmentVariable,
                "true");
        }

        public string ConnectionString { get; }

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
