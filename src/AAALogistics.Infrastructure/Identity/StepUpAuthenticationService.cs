using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Identity;

namespace AAALogistics.Infrastructure.Identity;

public sealed class StepUpAuthenticationService(
    UserManager<ApplicationUser> userManager,
    IRequestContext requestContext,
    IJwtTokenService jwtTokenService,
    IStepUpJtiStore jtiStore) : IStepUpAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager =
        userManager ?? throw new ArgumentNullException(nameof(userManager));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    private readonly IJwtTokenService _jwtTokenService =
        jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));

    private readonly IStepUpJtiStore _jtiStore =
        jtiStore ?? throw new ArgumentNullException(nameof(jtiStore));

    public async Task<StepUpResult> CreateAsync(
        string totpCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_requestContext.UserId is not Guid userId)
        {
            return StepUpResult.Unauthorized;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null)
        {
            return StepUpResult.Unauthorized;
        }

        if (!await IsInternalUserAsync(user))
        {
            return StepUpResult.Unauthorized;
        }

        if (!await IsEnrolledAsync(user))
        {
            return StepUpResult.NotEnrolled;
        }

        if (!await VerifyCodeAsync(user, totpCode))
        {
            if (_userManager.SupportsUserLockout)
            {
                await _userManager.AccessFailedAsync(user);
            }

            return StepUpResult.InvalidCode;
        }

        if (_userManager.SupportsUserLockout)
        {
            await _userManager.ResetAccessFailedCountAsync(user);
        }

        var stamp = await _userManager.GetSecurityStampAsync(user);
        if (string.IsNullOrWhiteSpace(stamp))
        {
            return StepUpResult.Unauthorized;
        }

        var token = _jwtTokenService.CreateStepUpToken(user.Id, stamp);
        return StepUpResult.Success(new StepUpToken(token.Value, token.ExpiresAtUtc));
    }

    public async Task<bool> ValidateAsync(
        string stepUpToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_requestContext.UserId is not Guid currentUserId)
        {
            return false;
        }

        if (!_jwtTokenService.TryValidateStepUpToken(
                stepUpToken,
                out var tokenUserId,
                out var stamp,
                out var jti,
                out var expiresAtUtc)
            || tokenUserId != currentUserId)
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(currentUserId.ToString("D"));
        if (user is null || await _userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        var currentStamp = await _userManager.GetSecurityStampAsync(user);
        if (!string.Equals(currentStamp, stamp, StringComparison.Ordinal))
        {
            return false;
        }

        // Single-use: consume jti; reuse until expiry → 401 via filter.
        return _jtiStore.TryConsume(jti, expiresAtUtc);
    }

    public async Task<bool> VerifyTotpForUserAsync(
        Guid userId,
        string totpCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(userId.ToString("D"));
        if (user is null || !await IsEnrolledAsync(user))
        {
            return false;
        }

        return await VerifyCodeAsync(user, totpCode);
    }

    private async Task<bool> IsInternalUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.Any(AppRoles.IsInternal);
    }

    private async Task<bool> IsEnrolledAsync(ApplicationUser user) =>
        user.TwoFactorEnabled
        && !string.IsNullOrWhiteSpace(
            await _userManager.GetAuthenticatorKeyAsync(user));

    private async Task<bool> VerifyCodeAsync(ApplicationUser user, string? totpCode)
    {
        if (string.IsNullOrWhiteSpace(totpCode))
        {
            return false;
        }

        var normalized = totpCode.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (await _userManager.VerifyTwoFactorTokenAsync(
                user,
                TokenOptions.DefaultAuthenticatorProvider,
                normalized))
        {
            return true;
        }

        var recovery = await _userManager.RedeemTwoFactorRecoveryCodeAsync(
            user,
            normalized);
        return recovery.Succeeded;
    }
}
