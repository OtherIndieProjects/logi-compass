using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace AAALogistics.Infrastructure.Identity;

internal static class AdminTwoFactorEnabler
{
    public static async Task<TwoFactorSetupAttempt> EnableAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(user);

        var reset = await userManager.ResetAuthenticatorKeyAsync(user);
        if (!reset.Succeeded)
        {
            throw Failure(reset, "No se pudo generar la clave TOTP.");
        }

        var sharedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            throw new InvalidOperationException(
                "Identity no devolvió la clave del autenticador.");
        }

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(
            user,
            10);
        if (recoveryCodes is null)
        {
            throw new InvalidOperationException(
                "Identity no devolvió códigos de recuperación.");
        }

        var enabled = await userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enabled.Succeeded)
        {
            throw Failure(enabled, "No se pudo activar el segundo factor.");
        }

        return new TwoFactorSetupAttempt(
            TwoFactorSetupStatus.Enabled,
            sharedKey,
            recoveryCodes.ToArray());
    }

    private static InvalidOperationException Failure(
        IdentityResult result,
        string prefix) =>
        new(
            prefix
            + " "
            + string.Join("; ", result.Errors.Select(error => error.Description)));
}

public sealed class AdminTwoFactorService(
    UserManager<ApplicationUser> userManager,
    IRequestContext requestContext) : ITwoFactorSetupService
{
    private readonly UserManager<ApplicationUser> _userManager =
        userManager ?? throw new ArgumentNullException(nameof(userManager));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    public Task<TwoFactorSetupAttempt> EnableForCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = _userManager;
        _ = _requestContext;
        // Product no longer supports TOTP enrollment (SAP-like auth).
        return Task.FromResult(
            new TwoFactorSetupAttempt(
                TwoFactorSetupStatus.Forbidden,
                null,
                null));
    }
}
