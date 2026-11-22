using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Documents;
using AAALogistics.Application.Exports;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using AAALogistics.Application.Branding;
using AAALogistics.Infrastructure.Auditing;
using AAALogistics.Infrastructure.Billing;
using AAALogistics.Infrastructure.Branding;
using AAALogistics.Infrastructure.Documents;
using AAALogistics.Infrastructure.Exports;
using AAALogistics.Infrastructure.Backups;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Email;
using AAALogistics.Infrastructure.Identity;
using AAALogistics.Infrastructure.Logistics;
using AAALogistics.Infrastructure.Persistence;
using AAALogistics.Infrastructure.Security;
using AAALogistics.Integrations.Sap;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AAALogistics.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default es obligatorio.");
        }

        var jwtOptions = GetOptions<JwtOptions>(
            configuration,
            JwtOptions.SectionName);
        var emailOptions = GetOptions<EmailOptions>(
            configuration,
            EmailOptions.SectionName);
        var firstBootOptions = GetOptions<FirstBootOptions>(
            configuration,
            FirstBootOptions.SectionName);
        var passwordHashingOptions = GetOptions<PasswordHashingOptions>(
            configuration,
            PasswordHashingOptions.SectionName);
        var auditOptions = GetOptions<AuditOptions>(
            configuration,
            AuditOptions.SectionName);
        var backupOptions = GetOptions<BackupOptions>(
            configuration,
            BackupOptions.SectionName);
        var demoOptions = GetOptions<DemoOptions>(
            configuration,
            DemoOptions.SectionName);
        var legalOptions = GetOptions<LegalOptions>(
            configuration,
            LegalOptions.SectionName);
        var listenerOptions = GetOptions<ListenerOptions>(
            configuration,
            ListenerOptions.SectionName);

        Validate(jwtOptions);
        Validate(emailOptions);
        Validate(firstBootOptions);
        Validate(passwordHashingOptions);
        Validate(auditOptions);
        Validate(backupOptions);
        Validate(legalOptions);
        Validate(listenerOptions);

        services.AddSingleton(Options.Create(jwtOptions));
        services.AddSingleton(Options.Create(emailOptions));
        services.AddSingleton(Options.Create(firstBootOptions));
        services.AddSingleton(Options.Create(passwordHashingOptions));
        services.AddSingleton(Options.Create(auditOptions));
        services.AddSingleton(Options.Create(backupOptions));
        services.AddSingleton(Options.Create(demoOptions));
        services.AddSingleton(Options.Create(legalOptions));
        services.AddSingleton(Options.Create(listenerOptions));

        var protectionDirectory = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(jwtOptions.SigningKeyPath))
            ?? Path.Combine("Data", "keys"),
            "dataprotection");
        Directory.CreateDirectory(protectionDirectory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                protectionDirectory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
        }

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(protectionDirectory))
            .SetApplicationName("AAA Logistics");

        services.AddHttpContextAccessor();
        services.AddScoped<IRequestContext, HttpRequestContext>();
        services.AddSingleton<PiiMasterKeyProvider>();
        services.AddScoped<PersonDataEncryptionService>();
        services.AddScoped<AuditChainService>();

        services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseNpgsql(
                    connectionString,
                    npgsql => npgsql.MigrationsHistoryTable(
                        "__ef_migrations_history"))
                .UseSnakeCaseNamingConvention());

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;

                options.User.RequireUniqueEmail = false;
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddPasswordValidator<CommonPasswordValidator<ApplicationUser>>();

        services.AddSingleton<
            IPasswordHasher<ApplicationUser>,
            Argon2PasswordHasher>();
        services.AddScoped<IdentityAuthenticationService>();
        services.AddScoped<IUserAuthenticationService>(
            provider => provider.GetRequiredService<IdentityAuthenticationService>());
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IPasswordManagementService, PasswordManagementService>();
        services.AddScoped<IExtendedLogisticsService, ExtendedLogisticsService>();
        services.AddScoped<IAppendixLogisticsService, AppendixLogisticsService>();
        services.AddScoped<IIndustrialLogisticsService, IndustrialLogisticsService>();
        services.AddScoped<IStudioService, StudioService>();
        services.AddScoped<ISapIntegrationService, SapIntegrationService>();
        services.AddHostedService<SapOutboxWorker>();
        services.AddHostedService<AuditRetentionWorker>();
        RegisterSapClient(services, configuration);
        services.AddScoped<ITwoFactorSetupService, AdminTwoFactorService>();
        services.AddSingleton<IStepUpJtiStore, MemoryStepUpJtiStore>();
        services.AddScoped<IStepUpAuthenticationService, StepUpAuthenticationService>();
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ILogisticsService, LogisticsService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();
        services.AddScoped<IDocumentPdfService, DocumentPdfService>();
        services.AddSingleton<IAppBrandingStore, AppBrandingStore>();
        services.AddScoped<ICustomerRlsSession, CustomerRlsSession>();

        services.AddSingleton<IClock, SystemClock>();

        if (emailOptions.Mode == EmailMode.File)
        {
            services.AddSingleton<IEmailSender, FileEmailSender>();
        }
        else if (emailOptions.Mode == EmailMode.Smtp)
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, NullEmailSender>();
        }

        var verifactuOptions = configuration
            .GetSection(VerifactuOptions.SectionName)
            .Get<VerifactuOptions>() ?? new VerifactuOptions();
        services.Configure<VerifactuOptions>(
            configuration.GetSection(VerifactuOptions.SectionName));
        services.Configure<CaptchaOptions>(
            configuration.GetSection(CaptchaOptions.SectionName));
        services.AddSingleton<ICaptchaVerifier, CaptchaVerifier>();
        if (string.Equals(
                verifactuOptions.Mode,
                "AeatHttp",
                StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IVerifactuSubmitter, AeatHttpVerifactuSubmitter>();
        }
        else
        {
            services.AddScoped<IVerifactuSubmitter, LocalChainVerifactuSubmitter>();
        }

        var signingKeyProvider = new JwtSigningKeyProvider(
            Options.Create(jwtOptions));
        services.AddSingleton(signingKeyProvider);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ClockSkew = TimeSpan.FromSeconds(30),
                    IssuerSigningKey = signingKeyProvider.SigningKey,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
                    NameClaimType = JwtClaimNames.UserName,
                    RoleClaimType = ClaimTypes.Role,
                    ValidAudiences =
                    [
                        jwtOptions.InternalAudience,
                        jwtOptions.PortalAudience,
                    ],
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        if (principal is null)
                        {
                            context.Fail("AAA-AUTH-0401");
                            return;
                        }

                        var subject = principal.FindFirst(JwtClaimNames.Subject)?.Value;
                        var stamp = principal.FindFirst(JwtClaimNames.SecurityStamp)?.Value;
                        if (!Guid.TryParse(subject, out var userId)
                            || string.IsNullOrEmpty(stamp))
                        {
                            context.Fail("AAA-AUTH-0401");
                            return;
                        }

                        var userManager = context.HttpContext.RequestServices
                            .GetRequiredService<UserManager<ApplicationUser>>();
                        var user = await userManager.FindByIdAsync(
                            userId.ToString("D"));
                        if (user is null
                            || await userManager.IsLockedOutAsync(user)
                            || !string.Equals(
                                user.SecurityStamp,
                                stamp,
                                StringComparison.Ordinal))
                        {
                            context.Fail("AAA-AUTH-0401");
                        }
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(
                AuthorizationPolicyNames.InternalApi,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(
                        JwtClaimNames.Audience,
                        jwtOptions.InternalAudience));

            options.AddPolicy(
                AuthorizationPolicyNames.PortalApi,
                policy => policy
                    .RequireAuthenticatedUser()
                    .RequireClaim(
                        JwtClaimNames.Audience,
                        jwtOptions.PortalAudience));

            foreach (var permission in AppPermissions.All)
            {
                options.AddPolicy(
                    permission,
                    policy => policy
                        .RequireAuthenticatedUser()
                        .RequireClaim(
                            JwtClaimNames.Permission,
                            permission));
            }
        });

        return services;
    }

    private static void RegisterSapClient(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var sapOptions = configuration.GetSection(SapOptions.SectionName)
            .Get<SapOptions>() ?? new SapOptions();
        sapOptions.ApplyEnvironmentOverrides();
        sapOptions.Validate();
        services.AddSingleton(Options.Create(sapOptions));

        if (!sapOptions.Enabled)
        {
            services.AddSingleton<ISapIntegrationClient, MockSapODataClient>();
            return;
        }

        services.AddHttpClient(
            "sap-odata",
            client =>
            {
                SapODataClient.ConfigureAuth(
                    client,
                    new Uri(sapOptions.BaseUrl.TrimEnd('/') + "/"),
                    sapOptions.BearerToken,
                    sapOptions.UserName,
                    sapOptions.Password,
                    TimeSpan.FromSeconds(sapOptions.TimeoutSeconds));
            });

        services.AddSingleton<ISapIntegrationClient>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var client = factory.CreateClient("sap-odata");
            return new SapODataClient(
                client,
                sapOptions.MaterialsPath,
                sapOptions.DeliveryPushPath);
        });
    }

    private static TOptions GetOptions<TOptions>(
        IConfiguration configuration,
        string sectionName)
        where TOptions : new() =>
        configuration.GetSection(sectionName).Get<TOptions>() ?? new TOptions();

    private static void Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer)
            || string.IsNullOrWhiteSpace(options.InternalAudience)
            || string.IsNullOrWhiteSpace(options.PortalAudience)
            || string.IsNullOrWhiteSpace(options.SigningKeyPath))
        {
            throw new InvalidOperationException(
                "Jwt requiere Issuer, ambas audiencias y SigningKeyPath.");
        }

        if (string.Equals(
            options.InternalAudience,
            options.PortalAudience,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Las audiencias JWT interna y de portal deben ser distintas.");
        }

        if (options.AccessTokenMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "Jwt:AccessTokenMinutes debe estar entre 1 y 60.");
        }

        if (options.RefreshTokenDays is < 1 or > 90)
        {
            throw new InvalidOperationException(
                "Jwt:RefreshTokenDays debe estar entre 1 y 90.");
        }
    }

    private static void Validate(LegalOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CurrentVersion)
            && options.CurrentVersion.Trim().Length > 64)
        {
            throw new InvalidOperationException(
                "Legal:CurrentVersion admite como máximo 64 caracteres.");
        }
    }

    private static void Validate(ListenerOptions options)
    {
        var internalEndpoint = ListenerAddress.Parse(options.InternalUrl);
        var portalEndpoint = ListenerAddress.Parse(options.PortalUrl);
        if (internalEndpoint.Port == portalEndpoint.Port
            && internalEndpoint.Address.Equals(portalEndpoint.Address))
        {
            throw new InvalidOperationException(
                "Listeners:InternalUrl y Listeners:PortalUrl deben ser distintos.");
        }
    }

    private static void Validate(EmailOptions options)
    {
        if (options.Mode == EmailMode.Smtp
            && (string.IsNullOrWhiteSpace(options.SmtpHost)
                || string.IsNullOrWhiteSpace(options.SmtpFrom)))
        {
            throw new InvalidOperationException(
                "Email:Mode=Smtp requiere Email:SmtpHost y Email:SmtpFrom.");
        }

        if (options.Mode == EmailMode.File
            && string.IsNullOrWhiteSpace(options.FileDirectory))
        {
            throw new InvalidOperationException(
                "Email:FileDirectory es obligatorio en modo File.");
        }
    }

    private static void Validate(FirstBootOptions options)
    {
        var hasUserName = !string.IsNullOrWhiteSpace(options.AdminUserName);
        var hasPassword = !string.IsNullOrEmpty(options.AdminPassword);

        if (hasUserName != hasPassword)
        {
            throw new InvalidOperationException(
                "FirstBoot requiere AdminUserName y AdminPassword juntos.");
        }

        if (hasPassword && options.AdminPassword.Length < 12)
        {
            throw new InvalidOperationException(
                "FirstBoot:AdminPassword debe tener al menos 12 caracteres.");
        }
    }

    private static void Validate(PasswordHashingOptions options)
    {
        if (options.MemorySizeKb is < (19 * 1024) or > (256 * 1024)
            || options.Iterations is < 1 or > 10
            || options.DegreeOfParallelism is < 1 or > 16
            || options.MaxConcurrentOperations is < 1 or > 16)
        {
            throw new InvalidOperationException(
                "PasswordHashing está fuera de los límites seguros admitidos.");
        }
    }

    private static void Validate(AuditOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PiiMasterKeyPath))
        {
            throw new InvalidOperationException(
                "Audit:PiiMasterKeyPath es obligatorio.");
        }

        if (options.RetentionDays is < 1 or > 3_650)
        {
            throw new InvalidOperationException(
                "Audit:RetentionDays debe estar entre 1 y 3650.");
        }

        if (string.IsNullOrWhiteSpace(options.CheckpointDirectory))
        {
            throw new InvalidOperationException(
                "Audit:CheckpointDirectory es obligatorio.");
        }

        if (options.RetentionPollHours is < 1 or > 168)
        {
            throw new InvalidOperationException(
                "Audit:RetentionPollHours debe estar entre 1 y 168.");
        }
    }

    private static void Validate(BackupOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Directory)
            || string.IsNullOrWhiteSpace(options.PgDumpPath)
            || string.IsNullOrWhiteSpace(options.EncryptionKeyPath)
            || options.TimeoutMinutes is < 1 or > 1_440)
        {
            throw new InvalidOperationException(
                "Backups requiere Directory, PgDumpPath, EncryptionKeyPath y un timeout válido.");
        }
    }
}
