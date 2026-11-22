using AAALogistics.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AAALogistics.Host.Hosting;

public sealed class SurfaceIsolationMiddleware(
    RequestDelegate next,
    IOptions<ListenerOptions> listeners)
{
    private readonly RequestDelegate _next =
        next ?? throw new ArgumentNullException(nameof(next));

    private readonly int _internalPort =
        ListenerAddress.Parse(
            listeners?.Value.InternalUrl
            ?? throw new ArgumentNullException(nameof(listeners))).Port;

    private readonly int _portalPort =
        ListenerAddress.Parse(
            listeners.Value.PortalUrl).Port;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var port = context.Connection.LocalPort;
        var path = context.Request.Path;
        if (port == _portalPort && !IsAllowed(path, "/api/v1/portal"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (port == _internalPort && !IsAllowed(path, "/api/v1/internal"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context);
    }

    private static bool IsAllowed(PathString path, string apiPrefix) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/openapi")
        || path.StartsWithSegments("/scalar")
        || path.StartsWithSegments(apiPrefix);
}
