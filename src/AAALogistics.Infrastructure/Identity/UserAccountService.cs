using System.Security.Claims;
using System.Security.Cryptography;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Identity;

public sealed class UserAccountService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IdentityAuthenticationService authenticationService,
    IRequestContext requestContext)
    : IUserAccountService
{
    private readonly UserManager<ApplicationUser> _userManager =
        userManager ?? throw new ArgumentNullException(nameof(userManager));

    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IdentityAuthenticationService _authenticationService =
        authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    public async Task<IReadOnlyCollection<InternalUserView>> ListInternalUsersAsync(
        string? role,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var users = await _userManager.Users
            .AsNoTracking()
            .Where(user => user.CustomerId == null)
            .OrderBy(user => user.UserName)
            .ToListAsync(cancellationToken);

        var result = new List<InternalUserView>(users.Count);
        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                continue;
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (!string.IsNullOrWhiteSpace(role)
                && !roles.Contains(role.Trim(), StringComparer.Ordinal))
            {
                continue;
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var hasCustom = claims.Any(claim =>
                string.Equals(
                    claim.Type,
                    JwtClaimNames.Permission,
                    StringComparison.Ordinal));
            var suspended = user.SuspendedUtc is not null
                || (user.LockoutEnd is { } end && end > DateTimeOffset.UtcNow);
            result.Add(
                new InternalUserView(
                    user.Id,
                    user.UserName,
                    roles.Order(StringComparer.Ordinal).ToArray(),
                    suspended,
                    user.SuspensionReason,
                    hasCustom,
                    user.LockoutEnd));
        }

        return result;
    }

    public async Task<CreateInternalUserResult> CreateWorkerAsync(
        string userName,
        string? temporaryPassword,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? initialRanges = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedName = RequireUserName(userName);
        var password = string.IsNullOrWhiteSpace(temporaryPassword)
            ? GenerateTemporaryPassword()
            : temporaryPassword.Trim();

        var ranges = (initialRanges ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (ranges.Length == 0)
        {
            throw new ArgumentException(
                "Debe indicar al menos un rango (Admin, Gerente, Compras, Ventas, Almacen, Finanzas, Calidad o Cliente).");
        }

        if (ranges.Any(AppRoles.IsCustomer) && ranges.Any(AppRoles.IsInternal))
        {
            throw new InvalidOperationException(
                "El rango Cliente no se puede combinar con rangos internos.");
        }

        foreach (var range in ranges)
        {
            if (!AppRoles.All.Contains(range, StringComparer.Ordinal))
            {
                throw new ArgumentException("Rango desconocido: " + range);
            }
        }

        var existing = await _userManager.FindByNameAsync(normalizedName);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                "Ya existe un usuario con ese nombre.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = normalizedName,
            Email = normalizedName + "@internal.local",
            EmailConfirmed = true,
        };

        var created = await _userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        var roleResult = await _userManager.AddToRolesAsync(user, ranges);
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudieron asignar los rangos iniciales.");
        }

        return new CreateInternalUserResult(
            user.Id,
            user.UserName!,
            password,
            ranges);
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString("D"))
            ?? throw new KeyNotFoundException("El usuario no existe.");

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin, StringComparer.Ordinal))
        {
            return AppPermissions.All;
        }

        var userClaims = await _userManager.GetClaimsAsync(user);
        var custom = userClaims
            .Where(claim => string.Equals(
                claim.Type,
                JwtClaimNames.Permission,
                StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .Where(value => AppPermissions.Known.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (custom.Length > 0)
        {
            return custom;
        }

        var fromRanges = RangoPermissionCatalog.UnionForRoles(roles);
        if (fromRanges.Count > 0)
        {
            return fromRanges;
        }

        return [];
    }

    public async Task SetCompanyAsync(
        Guid userId,
        Guid? companyId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString("D"))
            ?? throw new KeyNotFoundException("El usuario no existe.");
        if (companyId is Guid id)
        {
            var exists = await _dbContext.Companies.AnyAsync(
                value => value.Id == id,
                cancellationToken);
            if (!exists)
            {
                throw new ArgumentException("La empresa no existe.");
            }
        }

        user.CompanyId = companyId;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    public async Task SetRangesAsync(
        Guid userId,
        IReadOnlyCollection<string> ranges,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString("D"))
            ?? throw new KeyNotFoundException("El usuario no existe.");

        var normalized = (ranges ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Debe indicar al menos un rango.");
        }

        var hasCustomer = normalized.Any(AppRoles.IsCustomer);
        var hasInternal = normalized.Any(AppRoles.IsInternal);
        if (hasCustomer && hasInternal)
        {
            throw new InvalidOperationException(
                "El rango Cliente no se puede combinar con rangos internos.");
        }

        foreach (var range in normalized)
        {
            if (!AppRoles.All.Contains(range, StringComparer.Ordinal))
            {
                throw new ArgumentException("Rango desconocido: " + range);
            }
        }

        var current = await _userManager.GetRolesAsync(user);
        var hadAdmin = current.Contains(AppRoles.Admin, StringComparer.Ordinal);
        var keepsAdmin = normalized.Contains(AppRoles.Admin, StringComparer.Ordinal);
        if (hadAdmin != keepsAdmin
            && !await AccountGuards.CallerIsAdminAsync(_userManager, _requestContext.UserId))
        {
            throw new InvalidOperationException(
                "Solo un administrador puede asignar o retirar el rango Admin.");
        }

        if (hadAdmin && !keepsAdmin)
        {
            await AccountGuards.EnsureNotLastActiveAdminAsync(_userManager, user);
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var remove = await _userManager.RemoveFromRolesAsync(user, current);
        if (!remove.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudieron limpiar los rangos previos.");
        }

        var add = await _userManager.AddToRolesAsync(user, normalized);
        if (!add.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudieron asignar los rangos.");
        }

        if (hasCustomer)
        {
            // Cliente portal requiere CustomerId; no se inventa aquí.
        }

        var stamp = await _userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo invalidar la sesión del usuario.");
        }

        await _authenticationService.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetPermissionsAsync(
        Guid userId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await AccountGuards.CallerIsAdminAsync(_userManager, _requestContext.UserId))
        {
            throw new InvalidOperationException(
                "Solo un administrador puede asignar permisos manuales.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString("D"))
            ?? throw new KeyNotFoundException("El usuario no existe.");

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(AppRoles.Admin, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Los permisos de un administrador no son editables.");
        }

        if (roles.Contains(AppRoles.Customer, StringComparer.Ordinal)
            && user.CustomerId is not null)
        {
            throw new InvalidOperationException(
                "Los permisos de usuarios portal no se gestionan aquí.");
        }

        var normalized = (permissions ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var unknown = normalized
            .Where(value => !AppPermissions.Known.Contains(value))
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                "Permisos desconocidos: " + string.Join(", ", unknown));
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await _userManager.GetClaimsAsync(user);
        foreach (var claim in existing.Where(value =>
                     string.Equals(
                         value.Type,
                         JwtClaimNames.Permission,
                         StringComparison.Ordinal)))
        {
            var removed = await _userManager.RemoveClaimAsync(user, claim);
            if (!removed.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudieron limpiar los permisos previos.");
            }
        }

        foreach (var permission in normalized)
        {
            var added = await _userManager.AddClaimAsync(
                user,
                new Claim(JwtClaimNames.Permission, permission));
            if (!added.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo asignar el permiso " + permission + ".");
            }
        }

        var stamp = await _userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo invalidar la sesión del usuario.");
        }

        await _authenticationService.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> SuspendAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var normalized = RequireReason(reason);
        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null)
        {
            return false;
        }

        if (_requestContext.UserId == user.Id)
        {
            throw new InvalidOperationException(
                "No puede suspender ni dar de baja su propia cuenta.");
        }

        await AccountGuards.EnsureCallerCanManageAdminAsync(
            _userManager,
            _requestContext.UserId,
            user,
            "Solo un administrador puede suspender o dar de baja a otro administrador.");
        await AccountGuards.EnsureNotLastActiveAdminAsync(_userManager, user);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockoutEnabled = await _userManager.SetLockoutEnabledAsync(user, true);
        if (!lockoutEnabled.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo activar el bloqueo del usuario.");
        }

        var lockoutEnd = await _userManager.SetLockoutEndDateAsync(
            user,
            DateTimeOffset.UtcNow.AddYears(100));
        if (!lockoutEnd.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo suspender el usuario.");
        }

        user.SuspensionReason = normalized;
        user.SuspendedUtc = DateTimeOffset.UtcNow;

        var stamp = await _userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo invalidar la sesión del usuario.");
        }

        await _authenticationService.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReactivateAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null)
        {
            return false;
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var clear = await _userManager.SetLockoutEndDateAsync(user, null);
        if (!clear.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo reactivar el usuario.");
        }

        user.SuspensionReason = null;
        user.SuspendedUtc = null;
        var stamp = await _userManager.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo renovar la sesión del usuario.");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TerminateAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var normalized = RequireReason(reason);
        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null)
        {
            return false;
        }

        return await SuspendAsync(
            userId,
            "TERMINATE: " + normalized,
            cancellationToken);
    }

    private static string RequireUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName.Trim().Length < 3)
        {
            throw new ArgumentException(
                "El nombre de usuario es obligatorio (mínimo 3 caracteres).",
                nameof(userName));
        }

        return userName.Trim();
    }

    private static string RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
        {
            throw new ArgumentException(
                "El motivo de suspensión es obligatorio (mínimo 3 caracteres).",
                nameof(reason));
        }

        var normalized = reason.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    internal static string GenerateTemporaryPassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return "Tmp!" + Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', 'x')
            .Replace('/', 'y');
    }
}
