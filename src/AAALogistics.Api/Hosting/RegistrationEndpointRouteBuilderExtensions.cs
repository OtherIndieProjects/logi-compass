using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class RegistrationEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapRegistrationEndpoints(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/registration-requests",
                (
                    string? status,
                    IRegistrationService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListAsync(
                            string.IsNullOrWhiteSpace(status) ? "Pending" : status,
                            cancellationToken)))
            .RequireAuthorization(policy => policy.RequireAssertion(
                context =>
                    context.User.HasClaim(
                        JwtClaimNames.Permission,
                        AppPermissions.RegistrationApprove)
                    || context.User.HasClaim(
                        JwtClaimNames.Permission,
                        AppPermissions.RegistrationReject)))
            .WithName("InternalRegistrationRequests");

        endpoints.MapPost(
                "/registration-requests/{requestId:guid}/approve",
                (
                    Guid requestId,
                    IRegistrationService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ApproveAsync(requestId, cancellationToken)))
            .RequireAuthorization(AppPermissions.RegistrationApprove)
            .RequireStepUp()
            .WithName("InternalApproveRegistration");

        endpoints.MapPost(
                "/registration-requests/{requestId:guid}/reject",
                (
                    Guid requestId,
                    RejectRegistrationRequest request,
                    IRegistrationService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.RejectAsync(
                            requestId,
                            request.Reason,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.RegistrationReject)
            .RequireStepUp()
            .WithName("InternalRejectRegistration");

        return endpoints;
    }

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (LogisticsException exception)
        {
            var status = exception.Kind switch
            {
                LogisticsErrorKind.NotFound => StatusCodes.Status404NotFound,
                LogisticsErrorKind.Conflict => StatusCodes.Status409Conflict,
                LogisticsErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest,
            };
            return Problem(status, exception.Message, exception.Code);
        }
        catch (ArgumentException exception)
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                exception.Message,
                "AAA-PORTAL-0001");
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                StatusCodes.Status409Conflict,
                exception.Message,
                "AAA-PORTAL-0007");
        }
    }

    private static IResult Problem(int statusCode, string title, string code) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            type: $"https://errors.aaalogistics.local/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
}
