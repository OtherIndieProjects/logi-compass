using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace AAALogistics.Infrastructure.Identity;

public sealed class PasswordManagementService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext dbContext,
    IdentityAuthenticationService authenticationService,
    IRequestContext requestContext) : IPasswordManagementService
{
    // Passwords that are or were published with the product or its scripts.
    private static readonly string[] KnownPublishedPasswords =
    [
        "AdminLocalDev2026!",
        "aaa_dev_only_change_me",
    ];

    private readonly UserManager<ApplicationUser> _userManager =
        userManager ?? throw new ArgumentNullException(nameof(userManager));

    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IdentityAuthenticationService _authenticationService =
        authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    public async Task<PasswordChangeResult> ChangeOwnPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_requestContext.UserId is not Guid userId)
        {
            return new PasswordChangeResult(PasswordChangeStatus.Unauthorized);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null)
        {
            return new PasswordChangeResult(PasswordChangeStatus.Unauthorized);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return new PasswordChangeResult(
                PasswordChangeStatus.LockedOut,
                "La cuenta está bloqueada. Espere unos minutos o contacte con un administrador.");
        }

        if (string.IsNullOrEmpty(currentPassword)
            || !await _userManager.CheckPasswordAsync(user, currentPassword))
        {
            if (_userManager.SupportsUserLockout)
            {
                await _userManager.AccessFailedAsync(user);
            }

            return new PasswordChangeResult(
                PasswordChangeStatus.InvalidCurrentPassword,
                "La contraseña actual no es correcta.");
        }

        var rejection = Validate(user, newPassword);
        if (rejection is not null)
        {
            return new PasswordChangeResult(PasswordChangeStatus.Rejected, rejection);
        }

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            return new PasswordChangeResult(
                PasswordChangeStatus.Rejected,
                "La nueva contraseña debe ser distinta de la actual.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var changed = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changed.Succeeded)
        {
            return new PasswordChangeResult(PasswordChangeStatus.Rejected, Describe(changed));
        }

        // ChangePasswordAsync rotates the security stamp, so live access tokens stop validating.
        await _authenticationService.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordChangeResult(PasswordChangeStatus.Changed);
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        Guid userId,
        bool resetTwoFactor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_requestContext.UserId == userId)
        {
            throw new InvalidOperationException(
                "Para su propia cuenta use «Cambiar contraseña».");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString("D"))
            ?? throw new KeyNotFoundException("El usuario no existe.");
        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Any(AppRoles.IsInternal))
        {
            throw new InvalidOperationException(
                "Solo se restablecen contraseñas de cuentas internas.");
        }

        await AccountGuards.EnsureCallerCanManageAdminAsync(
            _userManager,
            _requestContext.UserId,
            user,
            "Solo un administrador puede restablecer la contraseña de otro administrador.");

        var temporaryPassword = UserAccountService.GenerateTemporaryPassword();
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await _userManager.HasPasswordAsync(user))
        {
            Ensure(await _userManager.RemovePasswordAsync(user), "No se pudo retirar la contraseña anterior.");
        }

        Ensure(
            await _userManager.AddPasswordAsync(user, temporaryPassword),
            "No se pudo asignar la contraseña temporal.");
        if (user.SuspendedUtc is null)
        {
            Ensure(await _userManager.SetLockoutEndDateAsync(user, null), "No se pudo desbloquear la cuenta.");
        }

        Ensure(await _userManager.ResetAccessFailedCountAsync(user), "No se pudo reiniciar el contador de intentos.");
        if (resetTwoFactor)
        {
            Ensure(await _userManager.SetTwoFactorEnabledAsync(user, false), "No se pudo desactivar el autenticador.");
            Ensure(await _userManager.ResetAuthenticatorKeyAsync(user), "No se pudo invalidar la clave del autenticador.");
        }

        Ensure(await _userManager.UpdateSecurityStampAsync(user), "No se pudo invalidar la sesión del usuario.");
        await _authenticationService.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PasswordResetResult(user.Id, temporaryPassword, resetTwoFactor);
    }

    private static string? Validate(ApplicationUser user, string? newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return "La nueva contraseña es obligatoria.";
        }

        if (newPassword.Length < 12)
        {
            return "La nueva contraseña debe tener al menos 12 caracteres.";
        }

        if (!string.IsNullOrEmpty(user.UserName)
            && newPassword.Contains(user.UserName, StringComparison.OrdinalIgnoreCase))
        {
            return "La contraseña no puede contener el nombre de usuario.";
        }

        if (KnownPublishedPasswords.Contains(newPassword, StringComparer.Ordinal))
        {
            return "Esa contraseña es pública (aparece en documentación o scripts). Elija otra.";
        }

        return null;
    }

    private static string Describe(IdentityResult result) =>
        result.Errors.Select(error => error.Code).FirstOrDefault() switch
        {
            nameof(IdentityErrorDescriber.PasswordTooShort) =>
                "La nueva contraseña debe tener al menos 12 caracteres.",
            nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) =>
                "La nueva contraseña necesita más caracteres distintos.",
            nameof(IdentityErrorDescriber.PasswordMismatch) =>
                "La contraseña actual no es correcta.",
            "PasswordTooCommon" =>
                "La contraseña está en la lista de contraseñas comunes y no se admite.",
            _ => "La nueva contraseña no cumple la política de seguridad.",
        };

    private static void Ensure(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(message);
        }
    }
}
