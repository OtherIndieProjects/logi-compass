namespace AAALogistics.Application.Authentication;

public enum TokenAudience
{
    Internal,
    Portal,
}

public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    Guid? CustomerId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    string SecurityStamp,
    Guid? CompanyId = null);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);

public enum AuthenticationStatus
{
    Failed,
    TwoFactorRequired,
    Succeeded,
}

public sealed record IssuedSession(
    AuthenticatedUser User,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAtUtc);

public sealed record AuthenticationResult(
    AuthenticationStatus Status,
    IssuedSession? Session)
{
    public static AuthenticationResult Failed { get; } =
        new(AuthenticationStatus.Failed, null);

    public static AuthenticationResult TwoFactorChallenge { get; } =
        new(AuthenticationStatus.TwoFactorRequired, null);

    public static AuthenticationResult Success(IssuedSession session) =>
        new(AuthenticationStatus.Succeeded, session);
}

public interface IUserAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(
        string userName,
        string password,
        string? totpCode,
        TokenAudience audience,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        TokenAudience audience,
        CancellationToken cancellationToken = default);

    Task<bool> LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);
}

public sealed record InternalUserView(
    Guid Id,
    string UserName,
    IReadOnlyCollection<string> Roles,
    bool IsSuspended,
    string? SuspensionReason,
    bool HasCustomPermissions,
    DateTimeOffset? LockoutEndUtc);

public sealed record CreateInternalUserResult(
    Guid Id,
    string UserName,
    string TemporaryPassword,
    IReadOnlyCollection<string> Roles);

public interface IUserAccountService
{
    Task<IReadOnlyCollection<InternalUserView>> ListInternalUsersAsync(
        string? role,
        CancellationToken cancellationToken = default);

    Task<CreateInternalUserResult> CreateWorkerAsync(
        string userName,
        string? temporaryPassword,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<string>? initialRanges = null);

    Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SetPermissionsAsync(
        Guid userId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default);

    Task SetRangesAsync(
        Guid userId,
        IReadOnlyCollection<string> ranges,
        CancellationToken cancellationToken = default);

    Task SetCompanyAsync(
        Guid userId,
        Guid? companyId,
        CancellationToken cancellationToken = default);

    Task<bool> SuspendAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<bool> ReactivateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> TerminateAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);
}

public enum PasswordChangeStatus
{
    Changed,
    InvalidCurrentPassword,
    Rejected,
    LockedOut,
    Unauthorized,
}

public sealed record PasswordChangeResult(
    PasswordChangeStatus Status,
    string? Message = null);

public sealed record PasswordResetResult(
    Guid UserId,
    string TemporaryPassword,
    bool TwoFactorReset);

public interface IPasswordManagementService
{
    /// <summary>Changes the caller's password; all sessions are revoked on success.</summary>
    Task<PasswordChangeResult> ChangeOwnPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Issues a temporary password for another internal user and revokes their sessions.</summary>
    Task<PasswordResetResult> ResetPasswordAsync(
        Guid userId,
        bool resetTwoFactor,
        CancellationToken cancellationToken = default);
}

public enum TwoFactorSetupStatus
{
    Enabled,
    AlreadyEnabled,
    Forbidden,
}

public sealed record TwoFactorSetupAttempt(
    TwoFactorSetupStatus Status,
    string? SharedKey,
    IReadOnlyCollection<string>? RecoveryCodes);

public interface ITwoFactorSetupService
{
    Task<TwoFactorSetupAttempt> EnableForCurrentUserAsync(
        CancellationToken cancellationToken = default);
}

public enum StepUpStatus
{
    Succeeded,
    InvalidCode,
    NotEnrolled,
    Unauthorized,
}

public sealed record StepUpToken(string Value, DateTimeOffset ExpiresAtUtc);

public sealed record StepUpResult(StepUpStatus Status, StepUpToken? Token)
{
    public static StepUpResult Unauthorized { get; } =
        new(StepUpStatus.Unauthorized, null);

    public static StepUpResult NotEnrolled { get; } =
        new(StepUpStatus.NotEnrolled, null);

    public static StepUpResult InvalidCode { get; } =
        new(StepUpStatus.InvalidCode, null);

    public static StepUpResult Success(StepUpToken token) =>
        new(StepUpStatus.Succeeded, token);
}

public interface IStepUpAuthenticationService
{
    Task<StepUpResult> CreateAsync(
        string totpCode,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateAsync(
        string stepUpToken,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyTotpForUserAsync(
        Guid userId,
        string totpCode,
        CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    AccessToken CreateAccessToken(AuthenticatedUser user, TokenAudience audience);

    AccessToken CreateStepUpToken(Guid userId, string securityStamp);

    bool TryValidateStepUpToken(
        string token,
        out Guid userId,
        out string securityStamp,
        out string jti,
        out DateTimeOffset expiresAtUtc);
}

public interface IStepUpJtiStore
{
    /// <summary>Marks <paramref name="jti"/> as consumed until <paramref name="expiresAtUtc"/>. Returns false if already used.</summary>
    bool TryConsume(string jti, DateTimeOffset expiresAtUtc);
}

public static class AuthorizationPolicyNames
{
    public const string InternalApi = "InternalApi";
    public const string PortalApi = "PortalApi";
}

public static class JwtClaimNames
{
    public const string Subject = "sub";
    public const string UserName = "unique_name";
    public const string Audience = "aud";
    public const string CustomerId = "customer_id";
    public const string CompanyId = "company_id";
    public const string Permission = "permission";
    public const string SecurityStamp = "security_stamp";
    public const string StepUp = "aaa_stepup";
}

public static class StepUpHeaders
{
    public const string HeaderName = "X-AAA-StepUp";
}
