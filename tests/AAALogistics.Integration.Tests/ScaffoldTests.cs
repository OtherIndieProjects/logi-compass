using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AAALogistics.Contracts;
using AAALogistics.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AAALogistics.Integration.Tests;

public sealed class AuthenticationEndpointTests : IAsyncLifetime
{
    private const string AdminUserName = "phase1-admin";
    private const string AdminPassword = "phase1-test-password";

    private readonly PhaseOneApiFactory _factory = new();
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
        catch (NpgsqlException)
        {
            _databaseAvailable = false;
        }
        catch (TimeoutException)
        {
            _databaseAvailable = false;
        }
    }

    [Fact]
    public async Task Authentication_Enforces_Token_And_Seeded_Admin_Can_Log_In()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        using var anonymousResponse =
            await _client.GetAsync(ApiRoutes.InternalMe);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(
            "application/problem+json",
            anonymousResponse.Content.Headers.ContentType?.MediaType);

        using var response = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(AdminUserName, AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Equal("Bearer", payload.TokenType);
        Assert.True(payload.ExpiresAtUtc > DateTimeOffset.UtcNow);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        var currentUser = await _client.GetFromJsonAsync<CurrentUserResponse>(
            ApiRoutes.InternalMe);

        Assert.NotNull(currentUser);
        Assert.Equal(AdminUserName, currentUser.UserName);
        Assert.Contains("Admin", currentUser.Roles);
        Assert.Contains("User.ManagePermissions", currentUser.Permissions);
    }

    [Fact]
    public async Task Admin_Can_Create_Worker_And_Restrict_Permissions()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        using var login = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(AdminUserName, AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var create = await _client.PostAsJsonAsync(
            ApiRoutes.InternalUsers,
            new CreateInternalUserRequest(
                "worker-perm-a",
                "WorkerLocalDev2026!",
                ["Almacen"]));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var worker = await create.Content.ReadFromJsonAsync<CreateInternalUserResponse>();
        Assert.NotNull(worker);

        using var setPermissions = await _client.PutAsJsonAsync(
            ApiRoutes.InternalUserPermissions(worker.Id),
            new SetUserPermissionsRequest(
            [
                "Stock.Read",
                "Material.Manage",
                "Report.Read",
            ]));
        Assert.Equal(HttpStatusCode.OK, setPermissions.StatusCode);

        using var workerClient = _factory.CreateClient();
        using var workerLogin = await workerClient.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(worker.UserName, "WorkerLocalDev2026!"));
        Assert.Equal(HttpStatusCode.OK, workerLogin.StatusCode);
        var workerSession =
            await workerLogin.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(workerSession);
        workerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", workerSession.AccessToken);

        var me = await workerClient.GetFromJsonAsync<CurrentUserResponse>(
            ApiRoutes.InternalMe);
        Assert.NotNull(me);
        Assert.Contains("Stock.Read", me.Permissions);
        Assert.DoesNotContain("Stock.Adjust", me.Permissions);

        using var adjust = await workerClient.PostAsJsonAsync(
            ApiRoutes.InternalStockAdjustments,
            new
            {
                materialId = Guid.CreateVersion7(),
                warehouseId = Guid.CreateVersion7(),
                signedQuantity = 1m,
                type = "Adjustment",
                reason = "test",
            });
        Assert.Equal(HttpStatusCode.Forbidden, adjust.StatusCode);
    }

    [Fact]
    public async Task Critical_Post_Does_Not_Require_Totp_StepUp()
    {
        if (!_databaseAvailable)
        {
            return;
        }

        using var login = await _client.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(AdminUserName, AdminPassword));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.AccessToken);

        using var stepGone = await _client.PostAsJsonAsync(
            ApiRoutes.InternalStepUp,
            new StepUpRequest("000000"));
        Assert.Equal(HttpStatusCode.Gone, stepGone.StatusCode);

        using var response = await _client.PostAsJsonAsync(
            ApiRoutes.InternalGoodsReceipts,
            new
            {
                purchaseOrderId = Guid.CreateVersion7(),
                documentDate = DateOnly.FromDateTime(DateTime.UtcNow),
                lines = Array.Empty<object>(),
            });
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        if (body.RootElement.TryGetProperty("code", out var code))
        {
            Assert.NotEqual("AAA-AUTH-STEPUP-REQUIRED", code.GetString());
            Assert.NotEqual("AAA-AUTH-2FA-REQUIRED", code.GetString());
        }
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

    private sealed class PhaseOneApiFactory : WebApplicationFactory<Program>
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

        public PhaseOneApiFactory()
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
}
