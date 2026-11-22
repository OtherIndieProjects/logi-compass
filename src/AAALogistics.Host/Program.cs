using System.Diagnostics;
using System.Threading.RateLimiting;
using System.Text.Json;
using AAALogistics.Api.Hosting;
using AAALogistics.Application;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Host.Hosting;
using AAALogistics.Domain.Security;
using AAALogistics.Infrastructure;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var hostCommand = HostCommand.Parse(args, out var commandError);
if (commandError is not null)
{
    Console.Error.WriteLine(commandError);
    Console.Error.WriteLine(HostCommand.Usage);
    Environment.ExitCode = 2;
    return;
}

if (hostCommand is { Kind: HostCommandKind.InstallService })
{
    Console.WriteLine(LinuxServiceCommands.Install);
    return;
}

if (hostCommand is { Kind: HostCommandKind.UninstallService })
{
    Console.WriteLine(LinuxServiceCommands.Uninstall);
    return;
}

SingleInstanceLock? instanceLock = null;
if (!SingleInstanceLock.IsDisabledByEnvironment())
{
    try
    {
        instanceLock = SingleInstanceLock.Acquire(
            Path.Combine(Directory.GetCurrentDirectory(), "Data"));
    }
    catch (InvalidOperationException exception)
    {
        Console.Error.WriteLine(exception.Message);
        Environment.ExitCode = 1;
        return;
    }
}

using var heldInstanceLock = instanceLock;
var builder = WebApplication.CreateBuilder(
    hostCommand is null ? args : []);

var listenerOptions = builder.Configuration
    .GetSection(ListenerOptions.SectionName)
    .Get<ListenerOptions>() ?? new ListenerOptions();
if (string.IsNullOrWhiteSpace(listenerOptions.InternalUrl))
{
    listenerOptions.InternalUrl = "http://127.0.0.1:5088";
}

if (string.IsNullOrWhiteSpace(listenerOptions.PortalUrl))
{
    listenerOptions.PortalUrl = "http://127.0.0.1:5089";
}

var internalEndpoint = ListenerAddress.Parse(listenerOptions.InternalUrl);
var portalEndpoint = ListenerAddress.Parse(listenerOptions.PortalUrl);
builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, string.Empty);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(internalEndpoint.Address, internalEndpoint.Port);
    options.Listen(portalEndpoint.Address, portalEndpoint.Port);
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddSingleton<AAALogistics.Application.Logistics.IOperationsRealtime, AAALogistics.Api.Hosting.SignalROperationsRealtime>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddHealthChecks()
    .AddCheck<AAALogistics.Host.Hosting.DatabaseHealthCheck>("database");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        "auth",
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

if (instanceLock is not null)
{
    app.Lifetime.ApplicationStopping.Register(instanceLock.Dispose);
    app.Logger.LogInformation(
        "Bloqueo de instancia único en {InstanceLockPath}.",
        instanceLock.LockPath);
}

app.UseExceptionHandler();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    var (title, code) = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized =>
            ("Autenticación requerida.", "AAA-AUTH-0401"),
        StatusCodes.Status403Forbidden =>
            ("Acceso denegado.", "AAA-AUTH-0403"),
        StatusCodes.Status404NotFound =>
            ("Recurso no encontrado.", "AAA-API-0404"),
        _ => ("La solicitud no se pudo completar.", "AAA-API-ERROR"),
    };

    response.ContentType = "application/problem+json";
    await JsonSerializer.SerializeAsync(
        response.Body,
        new ProblemDetails
        {
            Status = response.StatusCode,
            Title = title,
            Type = $"https://errors.aaalogistics.local/{code}",
            Extensions =
            {
                ["code"] = code,
                ["traceId"] =
                    Activity.Current?.Id
                    ?? statusCodeContext.HttpContext.TraceIdentifier,
            },
        },
        cancellationToken: statusCodeContext.HttpContext.RequestAborted);
});

app.UseMiddleware<SurfaceIsolationMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer =
        scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeDatabaseAsync();

    if (hostCommand is not null)
    {
        Environment.ExitCode = await ExecuteCommandAsync(
            hostCommand,
            scope.ServiceProvider,
            initializer);
        return;
    }

    var firstBootOptions = scope.ServiceProvider
        .GetRequiredService<IOptions<FirstBootOptions>>()
        .Value;
    var firstBootResult = await initializer.BootstrapAsync(firstBootOptions);

    if (firstBootResult.Status == FirstBootStatus.AdminConfigurationMissing)
    {
        app.Logger.LogWarning(
            "{FirstBootMessage} Ejecute: create-admin --user <nombre> --password <contraseña>.",
            firstBootResult.Message);
    }
    else if (firstBootResult.Status == FirstBootStatus.AdminCreated)
    {
        app.Logger.LogInformation(
            "Inicialización de identidad: {FirstBootMessage}",
            firstBootResult.Message);
        WriteAdminSetup(
            new AdminCreationResult(
                true,
                firstBootResult.Message,
                firstBootResult.AuthenticatorKey,
                firstBootResult.RecoveryCodes));
    }
    else
    {
        app.Logger.LogInformation(
            "Inicialización de identidad: {FirstBootMessage}",
            firstBootResult.Message);
    }
}

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks(
        "/health/ready",
        new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Name == "database",
        })
    .AllowAnonymous();
app.MapGet(
        "/openapi/v1.json",
        (IWebHostEnvironment env) =>
        {
            var path = Path.Combine(env.ContentRootPath, "openapi", "v1.json");
            return File.Exists(path)
                ? Results.File(path, "application/json")
                : Results.NotFound();
        })
    .AllowAnonymous();
app.MapScalarApiReference(options =>
{
    options
        .WithTitle("Logi Compass API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
}).AllowAnonymous();
app.MapAaaLogisticsApi();
app.MapHub<AAALogistics.Api.Hosting.OperationsHub>(
    AAALogistics.Contracts.ApiRoutes.InternalHubOperations);

Console.WriteLine(
    $"AAA Logistics internal API listening on {listenerOptions.InternalUrl}");
Console.WriteLine(
    $"AAA Logistics portal API listening on {listenerOptions.PortalUrl}");
Console.WriteLine(
    "OpenAPI: /openapi/v1.json · Scalar UI: /scalar (puerto interno).");
Console.WriteLine(
    "Health is public on both listeners. "
    + "The internal port only serves /api/v1/internal (+ OpenAPI/Scalar); "
    + "the portal port only serves /api/v1/portal.");

await app.RunAsync();

static async Task<int> ExecuteCommandAsync(
    HostCommand command,
    IServiceProvider services,
    DatabaseInitializer initializer)
{
    switch (command.Kind)
    {
        case HostCommandKind.CreateAdmin:
        {
            var result = await initializer.CreateAdminAsync(
                command.UserName!,
                command.Password!);
            if (result.Succeeded)
            {
                WriteAdminSetup(result);
                return 0;
            }

            Console.Error.WriteLine(result.Message);
            return 1;
        }
        case HostCommandKind.Backup:
        {
            var backupService =
                services.GetRequiredService<IBackupService>();
            if (command.List)
            {
                WriteJson(await backupService.ListAsync());
                return 0;
            }

            WriteJson(await backupService.CreateAsync(command.Gzip));
            return 0;
        }
        case HostCommandKind.Restore:
        {
            var backupService =
                services.GetRequiredService<IBackupService>();
            if (string.IsNullOrWhiteSpace(command.FilePath))
            {
                Console.Error.WriteLine("restore exige --file <ruta.dump>.");
                return 2;
            }

            if (string.IsNullOrWhiteSpace(command.UserName))
            {
                Console.Error.WriteLine(
                    "restore exige --user <nombre> (admin) y --confirm YES.");
                return 2;
            }

            var users = services
                .GetRequiredService<UserManager<ApplicationUser>>();
            var actor = await users.FindByNameAsync(command.UserName);
            if (actor is null)
            {
                Console.Error.WriteLine("Usuario no encontrado.");
                return 1;
            }

            var roles = await users.GetRolesAsync(actor);
            if (!roles.Contains(AppRoles.Admin, StringComparer.Ordinal))
            {
                Console.Error.WriteLine(
                    "Solo un administrador puede restaurar copias de seguridad.");
                return 1;
            }

            WriteJson(
                await backupService.RestoreAsync(
                    command.FilePath,
                    command.Confirm ?? string.Empty));
            return 0;
        }
        case HostCommandKind.VerifyAudit:
        {
            var auditService =
                services.GetRequiredService<IAuditService>();
            var result = await auditService.VerifyAsync();
            WriteJson(result);
            return result.IsValid ? 0 : 1;
        }
        case HostCommandKind.CreateAuditCheckpoint:
        {
            var auditService =
                services.GetRequiredService<IAuditService>();
            var created = await auditService.CreateCheckpointAsync("host-cli");
            WriteJson(created);
            return 0;
        }
        case HostCommandKind.AuditRetention:
        {
            var auditService =
                services.GetRequiredService<IAuditService>();
            WriteJson(await auditService.GetRetentionStatusAsync());
            return 0;
        }
        case HostCommandKind.SeedDemo:
        {
            var logisticsService =
                services.GetRequiredService<ILogisticsService>();
            WriteJson(await logisticsService.SeedDemoAsync());
            return 0;
        }
        case HostCommandKind.WipeCatalog:
        {
            var logisticsService =
                services.GetRequiredService<ILogisticsService>();
            WriteJson(await logisticsService.WipeCatalogAsync(command.Confirm ?? string.Empty));
            return 0;
        }
        case HostCommandKind.ReconcileStock:
        {
            var logisticsService =
                services.GetRequiredService<ILogisticsService>();
            var result = await logisticsService.ReconcileStockAsync();
            WriteJson(result);
            return result.IsConsistent ? 0 : 1;
        }
        case HostCommandKind.Status:
        {
            var db = services.GetRequiredService<
                AAALogistics.Infrastructure.Persistence.ApplicationDbContext>();
            var canConnect = await db.Database.CanConnectAsync();
            WriteJson(new
            {
                database = canConnect ? "ok" : "unreachable",
                utc = DateTimeOffset.UtcNow,
            });
            return canConnect ? 0 : 1;
        }
        case HostCommandKind.Users:
        {
            var users = services.GetRequiredService<
                Microsoft.AspNetCore.Identity.UserManager<
                    AAALogistics.Infrastructure.Identity.ApplicationUser>>();
            var list = users.Users
                .OrderBy(value => value.UserName)
                .Select(value => new
                {
                    value.Id,
                    value.UserName,
                    value.CustomerId,
                    value.TwoFactorEnabled,
                    value.LockoutEnd,
                    value.SuspensionReason,
                })
                .ToList();
            WriteJson(list);
            return 0;
        }
        case HostCommandKind.ResetTwoFactor:
        {
            var users = services.GetRequiredService<
                Microsoft.AspNetCore.Identity.UserManager<
                    AAALogistics.Infrastructure.Identity.ApplicationUser>>();
            var user = await users.FindByNameAsync(command.UserName!);
            if (user is null)
            {
                Console.Error.WriteLine("Usuario no encontrado.");
                return 1;
            }

            await users.SetTwoFactorEnabledAsync(user, false);
            await users.ResetAuthenticatorKeyAsync(user);
            // Clear residual Identity authenticator/recovery tokens if any.
            await users.ResetAuthenticatorKeyAsync(user);
            Console.WriteLine(
                $"2FA/TOTP desactivado para '{user.UserName}' (producto sin autenticador).");
            return 0;
        }
        case HostCommandKind.UnlockUser:
        {
            var users = services.GetRequiredService<
                Microsoft.AspNetCore.Identity.UserManager<
                    AAALogistics.Infrastructure.Identity.ApplicationUser>>();
            var user = await users.FindByNameAsync(command.UserName!);
            if (user is null)
            {
                Console.Error.WriteLine("Usuario no encontrado.");
                return 1;
            }

            await users.SetLockoutEndDateAsync(user, null);
            user.SuspensionReason = null;
            user.SuspendedUtc = null;
            await users.UpdateAsync(user);
            Console.WriteLine($"Usuario '{user.UserName}' desbloqueado.");
            return 0;
        }
        case HostCommandKind.InstallService:
        case HostCommandKind.UninstallService:
            throw new InvalidOperationException(
                "El comando de servicio se resuelve antes de arrancar el host.");
        default:
            throw new ArgumentOutOfRangeException(
                nameof(command),
                command.Kind,
                "Comando local desconocido.");
    }
}

static void WriteAdminSetup(AdminCreationResult result)
{
    Console.WriteLine(result.Message);
    if (string.IsNullOrWhiteSpace(result.AuthenticatorKey))
    {
        return;
    }

    var directory = Path.GetFullPath(Path.Combine("Data", "keys"));
    Directory.CreateDirectory(directory);
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            directory,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute);
    }

    var oncePath = Path.Combine(directory, "admin-2fa-once.txt");
    var lines = new List<string>
    {
        "# AAA Logistics — secreto TOTP de administrador (una sola vez)",
        $"# Generado UTC: {DateTimeOffset.UtcNow:O}",
        "AuthenticatorKeyBase32:",
        result.AuthenticatorKey,
        "RecoveryCodes:",
    };
    lines.AddRange(result.RecoveryCodes ?? []);
    var options = new FileStreamOptions
    {
        Access = FileAccess.Write,
        Mode = FileMode.Create,
        Share = FileShare.None,
    };
    if (!OperatingSystem.IsWindows())
    {
        options.UnixCreateMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite;
    }

    using (var stream = new FileStream(oncePath, options))
    using (var writer = new StreamWriter(stream))
    {
        foreach (var line in lines)
        {
            writer.WriteLine(line);
        }
    }

    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(
            oncePath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    Console.WriteLine(
        "2FA del administrador escrito en {0} (modo 0600). "
        + "Léalo, guárdelo fuera del servidor y elimine el fichero. "
        + "La semilla no se imprime en stdout del servicio.",
        oncePath);
}

static void WriteJson<T>(T value) =>
    Console.WriteLine(
        JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
            }));

public partial class Program;

internal enum HostCommandKind
{
    CreateAdmin,
    Backup,
    Restore,
    VerifyAudit,
    CreateAuditCheckpoint,
    AuditRetention,
    SeedDemo,
    WipeCatalog,
    ReconcileStock,
    InstallService,
    UninstallService,
    Status,
    Users,
    ResetTwoFactor,
    UnlockUser,
}

internal sealed record HostCommand(
    HostCommandKind Kind,
    string? UserName = null,
    string? Password = null,
    bool List = false,
    bool Gzip = false,
    string? FilePath = null,
    string? Confirm = null,
    string? TotpCode = null)
{
    public const string Usage =
        """
        Uso:
          create-admin --user <nombre> --password <contraseña-de-12-o-más>
          backup [--gzip]
          backup --list
          restore --file <ruta.dump> --confirm YES --user <admin>
          verify-audit
          create-audit-checkpoint
          audit-retention
          seed-demo
          wipe-catalog --confirm YES
          reconcile-stock
          status
          users
          disable-2fa --user <nombre>
          unlock-user --user <nombre>
          install-service
          uninstall-service
        """;

    public static HostCommand? Parse(string[] args, out string? error)
    {
        error = null;
        if (args.Length == 0)
        {
            return null;
        }

        switch (args[0])
        {
            case "create-admin":
            {
                var userName = ReadOption(args, "--user");
                var password = ReadOption(args, "--password");
                if (string.IsNullOrWhiteSpace(userName)
                    || string.IsNullOrEmpty(password))
                {
                    error =
                        "create-admin requiere --user y --password.";
                    return null;
                }

                return new HostCommand(
                    HostCommandKind.CreateAdmin,
                    userName,
                    password);
            }
            case "backup":
            {
                var list = args.Contains("--list", StringComparer.Ordinal)
                    || args.Contains("list", StringComparer.Ordinal);
                var gzip = args.Contains("--gzip", StringComparer.Ordinal);
                if (list && gzip)
                {
                    error = "backup --list no admite --gzip.";
                    return null;
                }

                return new HostCommand(
                    HostCommandKind.Backup,
                    List: list,
                    Gzip: gzip);
            }
            case "restore":
            {
                var file = ReadOption(args, "--file");
                var confirm = ReadOption(args, "--confirm");
                if (!string.Equals(confirm, "YES", StringComparison.Ordinal))
                {
                    error = "restore exige --confirm YES.";
                    return null;
                }

                if (string.IsNullOrWhiteSpace(file))
                {
                    error = "restore exige --file <ruta.dump>.";
                    return null;
                }

                var restoreUser = ReadOption(args, "--user");
                if (string.IsNullOrWhiteSpace(restoreUser))
                {
                    error =
                        "restore exige --user <nombre> (admin).";
                    return null;
                }

                return new HostCommand(
                    HostCommandKind.Restore,
                    UserName: restoreUser,
                    FilePath: file,
                    Confirm: confirm);
            }
            case "verify-audit":
                return new HostCommand(HostCommandKind.VerifyAudit);
            case "create-audit-checkpoint":
                return new HostCommand(HostCommandKind.CreateAuditCheckpoint);
            case "audit-retention":
                return new HostCommand(HostCommandKind.AuditRetention);
            case "seed-demo":
                return new HostCommand(HostCommandKind.SeedDemo);
            case "wipe-catalog":
            {
                var confirm = ReadOption(args, "--confirm");
                if (!string.Equals(confirm, "YES", StringComparison.Ordinal))
                {
                    error = "wipe-catalog exige --confirm YES.";
                    return null;
                }

                return new HostCommand(
                    HostCommandKind.WipeCatalog,
                    Confirm: confirm);
            }
            case "reconcile-stock":
                return new HostCommand(HostCommandKind.ReconcileStock);
            case "status":
                return new HostCommand(HostCommandKind.Status);
            case "users":
                return new HostCommand(HostCommandKind.Users);
            case "reset-2fa":
            case "disable-2fa":
            {
                var userName = ReadOption(args, "--user");
                if (string.IsNullOrWhiteSpace(userName))
                {
                    error = "disable-2fa/reset-2fa requiere --user.";
                    return null;
                }

                return new HostCommand(
                    HostCommandKind.ResetTwoFactor,
                    userName);
            }
            case "unlock-user":
            {
                var userName = ReadOption(args, "--user");
                if (string.IsNullOrWhiteSpace(userName))
                {
                    error = "unlock-user requiere --user.";
                    return null;
                }

                return new HostCommand(
                    HostCommandKind.UnlockUser,
                    userName);
            }
            case "install-service":
                return new HostCommand(HostCommandKind.InstallService);
            case "uninstall-service":
                return new HostCommand(HostCommandKind.UninstallService);
            default:
                return null;
        }
    }

    private static string? ReadOption(string[] args, string optionName)
    {
        for (var index = 1; index < args.Length - 1; index++)
        {
            if (string.Equals(
                    args[index],
                    optionName,
                    StringComparison.Ordinal)
                && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}
