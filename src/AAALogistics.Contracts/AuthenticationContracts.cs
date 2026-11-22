namespace AAALogistics.Contracts;

public sealed record LoginRequest(
    string UserName,
    string Password,
    string? TotpCode = null);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc,
    Guid? CustomerId = null);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record TwoFactorSetupResponse(
    string SharedKey,
    IReadOnlyCollection<string> RecoveryCodes);

public sealed record StepUpRequest(string TotpCode);

public sealed record StepUpResponse(
    string StepUpToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record CurrentUserResponse(
    Guid Id,
    string UserName,
    Guid? CustomerId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    Guid? CompanyId = null);

public sealed record SetUserCompanyRequest(Guid? CompanyId);

public sealed record SuspendUserRequest(string Reason);

public sealed record CancelReasonRequest(string Reason);

public sealed record CreateInternalUserRequest(
    string UserName,
    string? TemporaryPassword = null,
    IReadOnlyCollection<string>? InitialRanges = null);

public sealed record CreateInternalUserResponse(
    Guid Id,
    string UserName,
    string TemporaryPassword,
    IReadOnlyCollection<string> Roles);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record ResetUserPasswordRequest(bool ResetTwoFactor = false);

public sealed record ResetUserPasswordResponse(
    Guid UserId,
    string TemporaryPassword,
    bool TwoFactorReset);

public sealed record InternalUserResponse(
    Guid Id,
    string UserName,
    IReadOnlyCollection<string> Roles,
    bool IsSuspended,
    string? SuspensionReason,
    bool HasCustomPermissions,
    DateTimeOffset? LockoutEndUtc);

public sealed record UserPermissionsResponse(
    Guid UserId,
    IReadOnlyCollection<string> Permissions,
    bool IsCustom,
    bool IsAdmin);

public sealed record SetUserPermissionsRequest(
    IReadOnlyCollection<string> Permissions);

public sealed record SetUserRangesRequest(
    IReadOnlyCollection<string> Ranges);
