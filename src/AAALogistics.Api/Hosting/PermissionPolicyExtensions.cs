using AAALogistics.Application.Authentication;
using Microsoft.AspNetCore.Builder;

namespace AAALogistics.Api.Hosting;

internal static class PermissionPolicyExtensions
{
    /// <summary>Allows the call when the caller holds at least one of the permissions.</summary>
    public static RouteHandlerBuilder RequireAnyPermission(
        this RouteHandlerBuilder builder,
        params string[] permissions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (permissions.Length == 0)
        {
            throw new ArgumentException("Indique al menos un permiso.", nameof(permissions));
        }

        return builder.RequireAuthorization(policy => policy.RequireAssertion(
            context => permissions.Any(permission =>
                context.User.HasClaim(JwtClaimNames.Permission, permission))));
    }
}
