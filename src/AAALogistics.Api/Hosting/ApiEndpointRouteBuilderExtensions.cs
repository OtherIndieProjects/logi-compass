using System.Security.Claims;
using AAALogistics.Application.Authentication;
using AAALogistics.Contracts;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

public static class ApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapAaaLogisticsApi(this IEndpointRouteBuilder endpoints)
    {
        var internalApi = endpoints
            .MapGroup("/api/v1/internal")
            .RequireAuthorization(AuthorizationPolicyNames.InternalApi);
        var portalApi = endpoints
            .MapGroup("/api/v1/portal")
            .RequireAuthorization(AuthorizationPolicyNames.PortalApi);

        internalApi
            .MapPost(
                "/auth/login",
                (
                    LoginRequest request,
                    IUserAuthenticationService authenticationService,
                    IJwtTokenService tokenService,
                    CancellationToken cancellationToken) =>
                    LoginAsync(
                        request,
                        TokenAudience.Internal,
                        authenticationService,
                        tokenService,
                        cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("InternalLogin");

        internalApi
            .MapPost(
                "/auth/refresh",
                (
                    RefreshRequest request,
                    IUserAuthenticationService authenticationService,
                    IJwtTokenService tokenService,
                    CancellationToken cancellationToken) =>
                    RefreshAsync(
                        request,
                        TokenAudience.Internal,
                        authenticationService,
                        tokenService,
                        cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("InternalRefresh");

        internalApi
            .MapPost(
                "/auth/logout",
                (
                    LogoutRequest request,
                    IUserAuthenticationService authenticationService,
                    CancellationToken cancellationToken) =>
                    LogoutAsync(request, authenticationService, cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("InternalLogout");

        internalApi
            .MapPost(
                "/auth/2fa/setup",
                () => Problem(
                    StatusCodes.Status410Gone,
                    "TOTP/2FA eliminado. Acceso = usuario + contraseña + permisos.",
                    "AAA-AUTH-2FA-REMOVED"))
            .WithName("InternalTwoFactorSetup");

        internalApi
            .MapPost(
                "/auth/step-up",
                () => Problem(
                    StatusCodes.Status410Gone,
                    "Step-up TOTP eliminado. Las operaciones críticas usan roles/permisos.",
                    "AAA-AUTH-STEPUP-REMOVED"))
            .RequireRateLimiting("auth")
            .WithName("InternalStepUp");

        internalApi
            .MapPost(
                "/auth/change-password",
                async (
                    ChangePasswordRequest? request,
                    IPasswordManagementService passwords,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "Indique la contraseña actual y la nueva.",
                            "AAA-AUTH-PWD-0001");
                    }

                    var result = await passwords.ChangeOwnPasswordAsync(
                        request.CurrentPassword,
                        request.NewPassword,
                        cancellationToken);
                    return result.Status switch
                    {
                        PasswordChangeStatus.Changed => Results.NoContent(),
                        PasswordChangeStatus.InvalidCurrentPassword => Problem(
                            StatusCodes.Status400BadRequest,
                            result.Message ?? "La contraseña actual no es correcta.",
                            "AAA-AUTH-PWD-0002"),
                        PasswordChangeStatus.Rejected => Problem(
                            StatusCodes.Status400BadRequest,
                            result.Message ?? "La nueva contraseña no es válida.",
                            "AAA-AUTH-PWD-0003"),
                        PasswordChangeStatus.LockedOut => Problem(
                            StatusCodes.Status423Locked,
                            result.Message ?? "La cuenta está bloqueada.",
                            "AAA-AUTH-PWD-0004"),
                        _ => Problem(
                            StatusCodes.Status401Unauthorized,
                            "Usuario no autenticado.",
                            "AAA-AUTH-0001"),
                    };
                })
            .RequireRateLimiting("auth")
            .WithName("InternalChangePassword");

        internalApi
            .MapGet(
                "/users",
                async (
                    string? role,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    var users = await accounts.ListInternalUsersAsync(
                        role,
                        cancellationToken);
                    return Results.Ok(
                        users.Select(user => new InternalUserResponse(
                            user.Id,
                            user.UserName,
                            user.Roles,
                            user.IsSuspended,
                            user.SuspensionReason,
                            user.HasCustomPermissions,
                            user.LockoutEndUtc)).ToArray());
                })
            .RequireAuthorization(AppPermissions.UserRead)
            .WithName("InternalListUsers");

        internalApi
            .MapPost(
                "/users",
                async (
                    CreateInternalUserRequest? request,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null || string.IsNullOrWhiteSpace(request.UserName))
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "El nombre de usuario es obligatorio.",
                            "AAA-AUTH-0010");
                    }

                    try
                    {
                        var created = await accounts.CreateWorkerAsync(
                            request.UserName,
                            request.TemporaryPassword,
                            cancellationToken,
                            request.InitialRanges);
                        return Results.Json(
                            new CreateInternalUserResponse(
                                created.Id,
                                created.UserName,
                                created.TemporaryPassword,
                                created.Roles),
                            statusCode: StatusCodes.Status201Created);
                    }
                    catch (ArgumentException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            "AAA-AUTH-0010");
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Problem(
                            StatusCodes.Status409Conflict,
                            exception.Message,
                            "AAA-AUTH-0011");
                    }
                })
            .RequireAuthorization(AppPermissions.UserCreate)
            .RequireStepUp()
            .WithName("InternalCreateUser");

        internalApi
            .MapGet(
                "/users/{userId:guid}/permissions",
                async (
                    Guid userId,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var permissions = await accounts.GetPermissionsAsync(
                            userId,
                            cancellationToken);
                        var users = await accounts.ListInternalUsersAsync(
                            null,
                            cancellationToken);
                        var target = users.FirstOrDefault(user => user.Id == userId);
                        var isAdmin = target?.Roles.Contains(
                            AppRoles.Admin,
                            StringComparer.Ordinal) == true;
                        return Results.Ok(
                            new UserPermissionsResponse(
                                userId,
                                permissions,
                                target?.HasCustomPermissions == true,
                                isAdmin));
                    }
                    catch (KeyNotFoundException)
                    {
                        return Problem(
                            StatusCodes.Status404NotFound,
                            "El usuario no existe.",
                            "AAA-AUTH-0004");
                    }
                })
            .RequireAuthorization(AppPermissions.UserRead)
            .WithName("InternalGetUserPermissions");

        internalApi
            .MapPut(
                "/users/{userId:guid}/ranges",
                async (
                    Guid userId,
                    SetUserRangesRequest? request,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "El cuerpo de rangos es obligatorio.",
                            "AAA-AUTH-0014");
                    }

                    try
                    {
                        await accounts.SetRangesAsync(
                            userId,
                            request.Ranges,
                            cancellationToken);
                        return Results.NoContent();
                    }
                    catch (KeyNotFoundException)
                    {
                        return Problem(
                            StatusCodes.Status404NotFound,
                            "El usuario no existe.",
                            "AAA-AUTH-0004");
                    }
                    catch (ArgumentException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            "AAA-AUTH-0014");
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Problem(
                            StatusCodes.Status409Conflict,
                            exception.Message,
                            "AAA-AUTH-0015");
                    }
                })
            .RequireAuthorization(AppPermissions.UserManageRanges)
            .RequireStepUp()
            .WithName("InternalSetUserRanges");

        internalApi
            .MapPut(
                "/users/{userId:guid}/company",
                async (
                    Guid userId,
                    SetUserCompanyRequest? request,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "El cuerpo de empresa es obligatorio.",
                            "AAA-AUTH-0016");
                    }

                    try
                    {
                        await accounts.SetCompanyAsync(
                            userId,
                            request.CompanyId,
                            cancellationToken);
                        return Results.NoContent();
                    }
                    catch (KeyNotFoundException)
                    {
                        return Problem(
                            StatusCodes.Status404NotFound,
                            "El usuario no existe.",
                            "AAA-AUTH-0004");
                    }
                    catch (ArgumentException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            "AAA-AUTH-0016");
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Problem(
                            StatusCodes.Status409Conflict,
                            exception.Message,
                            "AAA-AUTH-0017");
                    }
                })
            .RequireAuthorization(AppPermissions.UserManageRanges)
            .RequireStepUp()
            .WithName("InternalSetUserCompany");

        internalApi
            .MapPut(
                "/users/{userId:guid}/permissions",
                async (
                    Guid userId,
                    SetUserPermissionsRequest? request,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "El cuerpo de permisos es obligatorio.",
                            "AAA-AUTH-0012");
                    }

                    try
                    {
                        await accounts.SetPermissionsAsync(
                            userId,
                            request.Permissions,
                            cancellationToken);
                        var permissions = await accounts.GetPermissionsAsync(
                            userId,
                            cancellationToken);
                        return Results.Ok(
                            new UserPermissionsResponse(
                                userId,
                                permissions,
                                IsCustom: true,
                                IsAdmin: false));
                    }
                    catch (KeyNotFoundException)
                    {
                        return Problem(
                            StatusCodes.Status404NotFound,
                            "El usuario no existe.",
                            "AAA-AUTH-0004");
                    }
                    catch (ArgumentException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            "AAA-AUTH-0012");
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Problem(
                            StatusCodes.Status409Conflict,
                            exception.Message,
                            "AAA-AUTH-0013");
                    }
                })
            .RequireAuthorization(AppPermissions.UserManagePermissions)
            .RequireStepUp()
            .WithName("InternalSetUserPermissions");

        internalApi
            .MapPost(
                "/users/{userId:guid}/suspend",
                async (
                    Guid userId,
                    SuspendUserRequest? request,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null || string.IsNullOrWhiteSpace(request.Reason))
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "El motivo de suspensión es obligatorio.",
                            "AAA-AUTH-0006");
                    }

                    try
                    {
                        var suspended = await accounts.SuspendAsync(
                            userId,
                            request.Reason,
                            cancellationToken);
                        return suspended
                            ? Results.NoContent()
                            : Problem(
                                StatusCodes.Status404NotFound,
                                "El usuario no existe.",
                                "AAA-AUTH-0004");
                    }
                    catch (ArgumentException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            "AAA-AUTH-0006");
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Problem(
                            StatusCodes.Status409Conflict,
                            exception.Message,
                            "AAA-AUTH-0018");
                    }
                })
            .RequireAuthorization(AppPermissions.UserSuspend)
            .RequireStepUp()
            .WithName("InternalSuspendUser");

        internalApi
            .MapPost(
                "/users/{userId:guid}/reactivate",
                async (
                    Guid userId,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    var ok = await accounts.ReactivateAsync(userId, cancellationToken);
                    return ok
                        ? Results.NoContent()
                        : Problem(
                            StatusCodes.Status404NotFound,
                            "El usuario no existe.",
                            "AAA-AUTH-0004");
                })
            .RequireAuthorization(AppPermissions.UserReactivate)
            .RequireStepUp()
            .WithName("InternalReactivateUser");

        internalApi
            .MapPost(
                "/users/{userId:guid}/terminate",
                async (
                    Guid userId,
                    SuspendUserRequest? request,
                    IUserAccountService accounts,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null || string.IsNullOrWhiteSpace(request.Reason))
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "El motivo de baja es obligatorio.",
                            "AAA-AUTH-0006");
                    }

                    try
                    {
                        var ok = await accounts.TerminateAsync(
                            userId,
                            request.Reason,
                            cancellationToken);
                        return ok
                            ? Results.NoContent()
                            : Problem(
                                StatusCodes.Status404NotFound,
                                "El usuario no existe.",
                                "AAA-AUTH-0004");
                    }
                    catch (ArgumentException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            "AAA-AUTH-0006");
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Problem(
                            StatusCodes.Status409Conflict,
                            exception.Message,
                            "AAA-AUTH-0018");
                    }
                })
            .RequireAuthorization(AppPermissions.UserTerminate)
            .RequireStepUp()
            .WithName("InternalTerminateUser");

        internalApi
            .MapPost(
                "/users/{userId:guid}/reset-password",
                async (
                    Guid userId,
                    ResetUserPasswordRequest? request,
                    IPasswordManagementService passwords,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var reset = await passwords.ResetPasswordAsync(
                            userId,
                            request?.ResetTwoFactor ?? false,
                            cancellationToken);
                        return Results.Ok(
                            new ResetUserPasswordResponse(
                                reset.UserId,
                                reset.TemporaryPassword,
                                reset.TwoFactorReset));
                    }
                    catch (KeyNotFoundException)
                    {
                        return Problem(
                            StatusCodes.Status404NotFound,
                            "El usuario no existe.",
                            "AAA-AUTH-0004");
                    }
                    catch (InvalidOperationException exception)
                    {
                        return Problem(
                            StatusCodes.Status409Conflict,
                            exception.Message,
                            "AAA-AUTH-0019");
                    }
                })
            .RequireAuthorization(AppPermissions.UserCreate)
            .RequireStepUp()
            .WithName("InternalResetUserPassword");

        portalApi
            .MapPost(
                "/auth/login",
                (
                    LoginRequest request,
                    IUserAuthenticationService authenticationService,
                    IJwtTokenService tokenService,
                    CancellationToken cancellationToken) =>
                    LoginAsync(
                        request,
                        TokenAudience.Portal,
                        authenticationService,
                        tokenService,
                        cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("PortalLogin");

        portalApi
            .MapPost(
                "/auth/refresh",
                (
                    RefreshRequest request,
                    IUserAuthenticationService authenticationService,
                    IJwtTokenService tokenService,
                    CancellationToken cancellationToken) =>
                    RefreshAsync(
                        request,
                        TokenAudience.Portal,
                        authenticationService,
                        tokenService,
                        cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("PortalRefresh");

        portalApi
            .MapPost(
                "/auth/logout",
                (
                    LogoutRequest request,
                    IUserAuthenticationService authenticationService,
                    CancellationToken cancellationToken) =>
                    LogoutAsync(request, authenticationService, cancellationToken))
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("PortalLogout");

        internalApi.MapGet("/ping", () => Results.Ok(new { surface = "internal", status = "ok" }))
            .WithName("InternalPing");

        portalApi.MapGet("/ping", () => Results.Ok(new { surface = "portal", status = "ok" }))
            .WithName("PortalPing");

        internalApi
            .MapGet("/me", GetCurrentUser)
            .WithName("InternalMe");

        internalApi.MapLogisticsEndpoints();
        internalApi.MapRegistrationEndpoints();
        internalApi.MapExportEndpoints();
        portalApi.MapPortalContentEndpoints();

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        TokenAudience audience,
        IUserAuthenticationService authenticationService,
        IJwtTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName)
            || string.IsNullOrEmpty(request.Password))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Solicitud de inicio de sesión no válida.",
                "AAA-AUTH-0002");
        }

        var result = await authenticationService.AuthenticateAsync(
            request.UserName,
            request.Password,
            request.TotpCode,
            audience,
            cancellationToken);
        return ToSessionResult(result, audience, tokenService);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        TokenAudience audience,
        IUserAuthenticationService authenticationService,
        IJwtTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "El refresh token es obligatorio.",
                "AAA-AUTH-0005");
        }

        var result = await authenticationService.RefreshAsync(
            request.RefreshToken,
            audience,
            cancellationToken);
        return ToSessionResult(result, audience, tokenService);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutRequest request,
        IUserAuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "El refresh token es obligatorio.",
                "AAA-AUTH-0005");
        }

        await authenticationService.LogoutAsync(
            request.RefreshToken,
            cancellationToken);
        return Results.NoContent();
    }

    private static IResult ToSessionResult(
        AuthenticationResult result,
        TokenAudience audience,
        IJwtTokenService tokenService)
    {
        // TwoFactorRequired kept in enum for wire compat but login never emits it.
        if (result.Status == AuthenticationStatus.TwoFactorRequired)
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Credenciales no válidas.",
                "AAA-AUTH-0001");
        }

        if (result.Status != AuthenticationStatus.Succeeded
            || result.Session is null)
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "Credenciales no válidas.",
                "AAA-AUTH-0001");
        }

        var accessToken = tokenService.CreateAccessToken(
            result.Session.User,
            audience);
        return Results.Ok(
            new LoginResponse(
                accessToken.Value,
                "Bearer",
                accessToken.ExpiresAtUtc,
                result.Session.RefreshToken,
                result.Session.RefreshExpiresAtUtc,
                result.Session.User.CustomerId));
    }

    private static IResult GetCurrentUser(ClaimsPrincipal principal)
    {
        if (!Guid.TryParse(
                principal.FindFirstValue(JwtClaimNames.Subject),
                out var userId)
            || string.IsNullOrWhiteSpace(principal.Identity?.Name))
        {
            return Problem(
                StatusCodes.Status401Unauthorized,
                "El token no contiene una identidad válida.",
                "AAA-AUTH-0003");
        }

        Guid? customerId = null;
        var customerIdClaim = principal.FindFirstValue(JwtClaimNames.CustomerId);
        if (Guid.TryParse(customerIdClaim, out var parsedCustomerId))
        {
            customerId = parsedCustomerId;
        }

        Guid? companyId = null;
        var companyIdClaim = principal.FindFirstValue(JwtClaimNames.CompanyId);
        if (Guid.TryParse(companyIdClaim, out var parsedCompanyId))
        {
            companyId = parsedCompanyId;
        }

        var roles = principal
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var permissions = principal
            .FindAll(JwtClaimNames.Permission)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return Results.Ok(
            new CurrentUserResponse(
                userId,
                principal.Identity.Name,
                customerId,
                roles,
                permissions,
                companyId));
    }

    private static IResult Problem(
        int statusCode,
        string title,
        string code,
        bool challenge = false)
    {
        var extensions = new Dictionary<string, object?>
        {
            ["code"] = code,
        };
        if (challenge)
        {
            extensions["challenge"] = true;
        }

        return Results.Problem(
            statusCode: statusCode,
            title: title,
            type: $"https://errors.aaalogistics.local/{code}",
            extensions: extensions);
    }
}
