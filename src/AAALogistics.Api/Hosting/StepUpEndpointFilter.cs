using Microsoft.AspNetCore.Builder;

namespace AAALogistics.Api.Hosting;

/// <summary>
/// SAP-like auth: user + password + permissions only. Step-up/TOTP removed.
/// <see cref="RequireStepUp"/> is kept as a no-op so existing endpoint chains compile.
/// </summary>
internal static class StepUpEndpointExtensions
{
    public static RouteHandlerBuilder RequireStepUp(this RouteHandlerBuilder builder) =>
        builder;
}
