using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using AAALogistics.Infrastructure.Auditing;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AAALogistics.Infrastructure.Identity;

public sealed class IdentityAuthenticationService(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    ApplicationDbContext dbContext,
    IOptions<JwtOptions> jwtOptions,
    IClock clock,
    AuditChainService auditChain,
    IRequestContext requestContext)
    : IUserAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager =
        userManager ?? throw new ArgumentNullException(nameof(userManager));

    private readonly IPasswordHasher<ApplicationUser> _passwordHasher =
        passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));

    private readonly RoleManager<IdentityRole<Guid>> _roleManager =
        roleManager ?? throw new ArgumentNullException(nameof(roleManager));

    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly JwtOptions _jwtOptions =
        jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    private readonly AuditChainService _auditChain =
        auditChain ?? throw new ArgumentNullException(nameof(auditChain));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    public async Task<AuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        string? totpCode,
        TokenAudience audience,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
        {
            PerformDummyPasswordHash(password);
            await AppendAuthAuditAsync(
                null,
                userName,
                "LOGIN_FAILED",
                audience.ToString(),
                cancellationToken,
                reason: "unknown_user");
            return AuthenticationResult.Failed;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            PerformDummyPasswordHash(password);
            await AppendAuthAuditAsync(
                user.Id,
                user.UserName ?? userName,
                "LOGIN_FAILED",
                audience.ToString(),
                cancellationToken,
                reason: "locked_out");
            return AuthenticationResult.Failed;
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            if (_userManager.SupportsUserLockout)
            {
                await _userManager.AccessFailedAsync(user);
            }

            await AppendAuthAuditAsync(
                user.Id,
                user.UserName ?? userName,
                "LOGIN_FAILED",
                audience.ToString(),
                cancellationToken,
                reason: "bad_password");
            return AuthenticationResult.Failed;
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!CanUseAudience(roles, audience)
            || string.IsNullOrWhiteSpace(user.UserName))
        {
            await AppendAuthAuditAsync(
                user.Id,
                user.UserName ?? userName,
                "LOGIN_FAILED",
                audience.ToString(),
                cancellationToken,
                reason: "audience_or_name");
            return AuthenticationResult.Failed;
        }

        if (audience == TokenAudience.Portal && user.CustomerId is null)
        {
            await AppendAuthAuditAsync(
                user.Id,
                user.UserName!,
                "LOGIN_FAILED",
                audience.ToString(),
                cancellationToken,
                reason: "portal_without_customer");
            return AuthenticationResult.Failed;
        }

        // TOTP/2FA removed (SAP-like: user + password + permissions only).
        _ = totpCode;

        if (_userManager.SupportsUserLockout)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        var authenticated = await BuildUserAsync(user, roles);
        var refresh = await IssueRefreshTokenAsync(
            user.Id,
            Guid.CreateVersion7(),
            cancellationToken);
        await AppendAuthAuditAsync(
            user.Id,
            user.UserName!,
            "LOGIN",
            audience.ToString(),
            cancellationToken);
        return AuthenticationResult.Success(
            new IssuedSession(
                authenticated,
                refresh.RawToken,
                refresh.ExpiresUtc));
    }

    public async Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        TokenAudience audience,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthenticationResult.Failed;
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var hash = HashRefreshToken(refreshToken.Trim());
        // Serialize concurrent refresh of the same token (and its family).
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"refresh:" + hash}, 0))",
            cancellationToken);
        var existing = await _dbContext.RefreshTokens
            .FromSqlInterpolated(
                $"SELECT * FROM refresh_tokens WHERE token_hash = {hash} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return AuthenticationResult.Failed;
        }

        if (existing.RevokedUtc is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AuthenticationResult.Failed;
        }

        if (existing.ExpiresUtc <= _clock.UtcNow)
        {
            return AuthenticationResult.Failed;
        }

        var user = await _userManager.FindByIdAsync(existing.UserId.ToString("D"));
        if (user is null
            || string.IsNullOrWhiteSpace(user.UserName)
            || await _userManager.IsLockedOutAsync(user))
        {
            await RevokeAllForUserAsync(existing.UserId, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AuthenticationResult.Failed;
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!CanUseAudience(roles, audience)
            || (audience == TokenAudience.Portal && user.CustomerId is null))
        {
            return AuthenticationResult.Failed;
        }

        var replacement = await IssueRefreshTokenAsync(
            user.Id,
            existing.FamilyId,
            cancellationToken);
        existing.Revoke(_clock.UtcNow, replacement.Id);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var authenticated = await BuildUserAsync(user, roles);
        return AuthenticationResult.Success(
            new IssuedSession(
                authenticated,
                replacement.RawToken,
                replacement.ExpiresUtc));
    }

    public async Task<bool> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var hash = HashRefreshToken(refreshToken.Trim());
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"refresh:" + hash}, 0))",
            cancellationToken);
        var existing = await _dbContext.RefreshTokens
            .FromSqlInterpolated(
                $"SELECT * FROM refresh_tokens WHERE token_hash = {hash} FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return false;
        }

        await RevokeFamilyAsync(existing.FamilyId, cancellationToken);
        var user = await _userManager.FindByIdAsync(existing.UserId.ToString("D"));
        var userName = user?.UserName ?? existing.UserId.ToString("D");
        if (user is not null)
        {
            // Rotate stamp so existing access JWTs fail OnTokenValidated immediately.
            await _userManager.UpdateSecurityStampAsync(user);
        }

        await _auditChain.AppendExplicitAsync(
            _dbContext,
            "auth_sessions",
            existing.UserId.ToString("D"),
            "LOGOUT",
            null,
            $"{{\"userName\":\"{EscapeJson(userName)}\"}}",
            cancellationToken,
            existing.UserId,
            _requestContext.IpAddress);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task AppendAuthAuditAsync(
        Guid? userId,
        string userName,
        string action,
        string audience,
        CancellationToken cancellationToken,
        string? reason = null)
    {
        var recordId = userId?.ToString("D")
            ?? ("name:" + userName.Trim());
        var payload = reason is null
            ? $"{{\"userName\":\"{EscapeJson(userName)}\",\"audience\":\"{EscapeJson(audience)}\"}}"
            : $"{{\"userName\":\"{EscapeJson(userName)}\",\"audience\":\"{EscapeJson(audience)}\",\"reason\":\"{EscapeJson(reason)}\"}}";
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _auditChain.AppendExplicitAsync(
            _dbContext,
            "auth_sessions",
            recordId,
            action,
            null,
            payload,
            cancellationToken,
            userId,
            _requestContext.IpAddress);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string EscapeJson(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    internal async Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(value => value.UserId == userId && value.RevokedUtc == null)
            .ToListAsync(cancellationToken);
        var now = _clock.UtcNow;
        foreach (var token in tokens)
        {
            token.Revoke(now, null);
        }
    }

    private static bool CanUseAudience(
        IEnumerable<string> roles,
        TokenAudience audience) =>
        audience switch
        {
            TokenAudience.Internal =>
                roles.Any(AppRoles.IsInternal),
            TokenAudience.Portal =>
                roles.Any(AppRoles.IsCustomer),
            _ => false,
        };

    private void PerformDummyPasswordHash(string password)
    {
        _ = _passwordHasher.HashPassword(
            new ApplicationUser { Id = Guid.CreateVersion7() },
            password);
    }

    private async Task<AuthenticatedUser> BuildUserAsync(
        ApplicationUser user,
        IList<string> roles)
    {
        IReadOnlyCollection<string> permissions;
        if (roles.Contains(AppRoles.Admin, StringComparer.Ordinal)
            || roles.Contains(AppRoles.Gerente, StringComparer.Ordinal)
            || roles.Contains(AppRoles.GerenteLogistica, StringComparer.Ordinal))
        {
            // Gerente ≈ amplio interno; Admin = All. Catálogo excluye CreateOwn/Terminate del Gerente.
            permissions = RangoPermissionCatalog.UnionForRoles(roles);
        }
        else
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            var userPermissions = userClaims
                .Where(value => string.Equals(
                    value.Type,
                    JwtClaimNames.Permission,
                    StringComparison.Ordinal))
                .Select(value => value.Value)
                .Where(value => AppPermissions.Known.Contains(value))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            permissions = userPermissions.Length > 0
                ? userPermissions
                : RangoPermissionCatalog.UnionForRoles(roles);
        }

        return new AuthenticatedUser(
            user.Id,
            user.UserName!,
            user.CustomerId,
            roles.Order(StringComparer.Ordinal).ToArray(),
            permissions,
            user.SecurityStamp ?? string.Empty,
            user.CompanyId);
    }

    private async Task<IssuedRefreshToken> IssueRefreshTokenAsync(
        Guid userId,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var now = _clock.UtcNow;
        var expires = now.AddDays(_jwtOptions.RefreshTokenDays);
        var entity = new RefreshToken(
            userId,
            HashRefreshToken(raw),
            familyId,
            now,
            expires);
        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new IssuedRefreshToken(entity.Id, raw, expires);
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(value => value.FamilyId == familyId && value.RevokedUtc == null)
            .ToListAsync(cancellationToken);
        var now = _clock.UtcNow;
        foreach (var token in tokens)
        {
            token.Revoke(now, null);
        }
    }

    private static string HashRefreshToken(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private readonly record struct IssuedRefreshToken(
        Guid Id,
        string RawToken,
        DateTimeOffset ExpiresUtc);
}

public enum FirstBootStatus
{
    NotRequired,
    AdminCreated,
    AdminConfigurationMissing,
}

public sealed record FirstBootResult(
    FirstBootStatus Status,
    string Message,
    string? AuthenticatorKey = null,
    IReadOnlyCollection<string>? RecoveryCodes = null);

public sealed record AdminCreationResult(
    bool Succeeded,
    string Message,
    string? AuthenticatorKey = null,
    IReadOnlyCollection<string>? RecoveryCodes = null);

public sealed class DatabaseInitializer(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager)
{
    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly RoleManager<IdentityRole<Guid>> _roleManager =
        roleManager ?? throw new ArgumentNullException(nameof(roleManager));

    private readonly UserManager<ApplicationUser> _userManager =
        userManager ?? throw new ArgumentNullException(nameof(userManager));

    public async Task InitializeDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        // RLS policy DDL requires table ownership (aaa_owner). When the login
        // role is a member of aaa_owner (lab/tests), assume it for Migrate only.
        // Keep the same physical connection open so SET ROLE applies to Migrate.
        await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        var connection = _dbContext.Database.GetDbConnection();
        var shouldResetRole = false;
        try
        {
            await using (var probe = connection.CreateCommand())
            {
                probe.CommandText =
                    """
                    SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aaa_owner')
                       AND pg_has_role(current_user, 'aaa_owner', 'MEMBER')
                    """;
                var canAssumeOwner = await probe.ExecuteScalarAsync(cancellationToken) is true;
                if (canAssumeOwner)
                {
                    var searchPath = new NpgsqlConnectionStringBuilder(
                            connection.ConnectionString)
                        .SearchPath;
                    if (string.IsNullOrWhiteSpace(searchPath))
                    {
                        searchPath = "public";
                    }

                    await using var setRole = connection.CreateCommand();
                    setRole.CommandText = "SET ROLE aaa_owner";
                    await setRole.ExecuteNonQueryAsync(cancellationToken);
                    // SET ROLE may install the role default search_path ($user); restore.
                    var firstSchema = searchPath
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .FirstOrDefault()
                        ?? "public";
                    await using var restorePath = connection.CreateCommand();
                    restorePath.CommandText =
                        "SELECT set_config('search_path', $1, false)";
                    var parameter = restorePath.CreateParameter();
                    parameter.Value = firstSchema;
                    restorePath.Parameters.Add(parameter);
                    await restorePath.ExecuteNonQueryAsync(cancellationToken);
                    shouldResetRole = true;
                }
            }

            await _dbContext.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            if (shouldResetRole && connection.State == ConnectionState.Open)
            {
                try
                {
                    await using var resetRole = connection.CreateCommand();
                    resetRole.CommandText = "RESET ROLE";
                    await resetRole.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception)
                {
                    // Connection may already be broken; continue cleanup.
                }
            }

            await _dbContext.Database.CloseConnectionAsync();
        }

        await DatabaseRoleGuard.EnsureSafeRoleAsync(_dbContext, cancellationToken);
        await EnsureRolesAsync(cancellationToken);
        await EnsureRolePermissionsAsync(cancellationToken);
        await MigrateLegacyRangesAsync(cancellationToken);
    }

    public async Task<FirstBootResult> BootstrapAsync(
        FirstBootOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (await _dbContext.Users.AnyAsync(cancellationToken))
        {
            return new FirstBootResult(
                FirstBootStatus.NotRequired,
                "La base de datos ya contiene usuarios.");
        }

        if (string.IsNullOrWhiteSpace(options.AdminUserName)
            || string.IsNullOrEmpty(options.AdminPassword))
        {
            return new FirstBootResult(
                FirstBootStatus.AdminConfigurationMissing,
                "No hay usuarios. Configure FirstBoot o ejecute create-admin localmente.");
        }

        var creation = await CreateAdminAsync(
            options.AdminUserName,
            options.AdminPassword,
            cancellationToken);

        if (!creation.Succeeded)
        {
            throw new InvalidOperationException(creation.Message);
        }

        return new FirstBootResult(
            FirstBootStatus.AdminCreated,
            creation.Message,
            creation.AuthenticatorKey,
            creation.RecoveryCodes);
    }

    public async Task<AdminCreationResult> CreateAdminAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(userName))
        {
            return new AdminCreationResult(
                false,
                "El nombre de usuario no puede estar vacío.");
        }

        if (password.Length < 12)
        {
            return new AdminCreationResult(
                false,
                "La contraseña debe tener al menos 12 caracteres.");
        }

        if (await _userManager.FindByNameAsync(userName) is not null)
        {
            return new AdminCreationResult(
                false,
                "Ya existe un usuario con ese nombre.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = userName.Trim(),
            LockoutEnabled = true,
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return Failure(createResult);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Admin);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return Failure(roleResult);
        }

        return new AdminCreationResult(
            true,
            $"Usuario administrador '{user.UserName}' creado. "
            + "Acceso con usuario y contraseña; las operaciones críticas usan roles/permisos.",
            AuthenticatorKey: null,
            RecoveryCodes: null);
    }

    private async Task EnsureRolesAsync(CancellationToken cancellationToken)
    {
        foreach (var roleName in AppRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(
                new IdentityRole<Guid>
                {
                    Id = Guid.CreateVersion7(),
                    Name = roleName,
                });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el rol '{roleName}': {FormatErrors(result)}");
            }
        }
    }

    private async Task EnsureRolePermissionsAsync(
        CancellationToken cancellationToken)
    {
        foreach (var roleName in AppRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var role = await _roleManager.FindByNameAsync(roleName)
                ?? throw new InvalidOperationException(
                    $"No existe el rol '{roleName}'.");
            var desired = RangoPermissionCatalog.ForRole(roleName);
            var existing = await _roleManager.GetClaimsAsync(role);
            var existingPermissions = existing
                .Where(value => string.Equals(
                    value.Type,
                    JwtClaimNames.Permission,
                    StringComparison.Ordinal))
                .Select(value => value.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permission in desired)
            {
                if (existingPermissions.Contains(permission))
                {
                    continue;
                }

                var result = await _roleManager.AddClaimAsync(
                    role,
                    new Claim(JwtClaimNames.Permission, permission));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"No se pudo asignar '{permission}' al rol '{roleName}': "
                        + FormatErrors(result));
                }
            }
        }
    }

    private async Task MigrateLegacyRangesAsync(CancellationToken cancellationToken)
    {
        var users = await _userManager.Users.ToListAsync(cancellationToken);
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = await _userManager.GetRolesAsync(user);
            if (current.Count == 0 || !current.Any(AppRoles.IsLegacy))
            {
                continue;
            }

            var mapped = AppRoles.MapLegacyRolesToSap(current);
            if (mapped.Count == 0)
            {
                continue;
            }

            // Conservar rangos SAP ya presentes; quitar legado y extras no mapeados.
            var toRemove = current
                .Where(role => !mapped.Contains(role, StringComparer.Ordinal))
                .ToArray();
            var toAdd = mapped
                .Where(role => !current.Contains(role, StringComparer.Ordinal))
                .ToArray();

            if (toRemove.Length > 0)
            {
                var removed = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"No se pudieron migrar rangos de '{user.UserName}': {FormatErrors(removed)}");
                }
            }

            if (toAdd.Length > 0)
            {
                var added = await _userManager.AddToRolesAsync(user, toAdd);
                if (!added.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"No se pudieron asignar rangos SAP a '{user.UserName}': {FormatErrors(added)}");
                }
            }
        }
    }

    private static AdminCreationResult Failure(IdentityResult result) =>
        new(false, $"No se pudo crear el administrador: {FormatErrors(result)}");

    private static string FormatErrors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(error => error.Description));
}
