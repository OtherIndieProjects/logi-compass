using System.Security.Claims;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class AppendixEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapAppendixLogisticsEndpoints(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/waves",
                (IAppendixLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListWavesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapPost(
                "/waves",
                (
                    CreateWaveRequest request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateWaveAsync(
                            new CreateWaveCommand(request.SalesOrderIds, request.Priority),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapPost(
                "/waves/{waveId:guid}/release",
                (
                    Guid waveId,
                    ClaimsPrincipal user,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ReleaseWaveAsync(
                            waveId,
                            ParseUserId(user),
                            user.Identity?.Name ?? "user",
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapGet(
                "/unit-conversions",
                (IAppendixLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListUnitConversionsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapPost(
                "/unit-conversions",
                (
                    UpsertUnitConversionRequest request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpsertUnitConversionAsync(
                            new UpsertUnitConversionCommand(
                                request.FromUnitId,
                                request.ToUnitId,
                                request.Factor),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/unit-conversions/convert",
                (
                    ConvertQuantityRequest request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ConvertQuantityAsync(
                            request.FromUnitId,
                            request.ToUnitId,
                            request.Quantity,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapGet(
                "/purchase-requisitions",
                (IAppendixLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListPurchaseRequisitionsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/purchase-requisitions/generate",
                (
                    GenerateMrpRequest? request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.GenerateMrpAsync(
                            request?.WarehouseId,
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/purchase-requisitions/{id:guid}/cancel",
                (
                    Guid id,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(async () =>
                    {
                        await service.CancelPurchaseRequisitionAsync(id, cancellationToken);
                        return new { id };
                    }))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapGet(
                "/rate-cards",
                (IAppendixLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListRateCardsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.InvoiceIssue);

        endpoints.MapPost(
                "/rate-cards",
                (
                    UpsertRateCardRequest request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpsertRateCardAsync(
                            new UpsertRateCardCommand(
                                request.Code,
                                request.Name,
                                request.Currency,
                                request.Lines
                                    .Select(line => new UpsertRateCardLineCommand(
                                        line.ServiceCode,
                                        line.UnitPrice,
                                        line.UnitLabel))
                                    .ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceIssue);

        endpoints.MapPost(
                "/rate-cards/{id:guid}/compute",
                (
                    Guid id,
                    ComputeRateChargesRequest request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ComputeRateChargesAsync(
                            id,
                            request.QuantitiesByService,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.InvoiceIssue);

        endpoints.MapGet(
                "/transport-routes",
                (IAppendixLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListTransportRoutesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.DeliveryNoteManage);

        endpoints.MapPost(
                "/transport-routes",
                (
                    CreateTransportRouteRequest request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateTransportRouteAsync(
                            new CreateTransportRouteCommand(
                                request.Code,
                                request.Name,
                                request.CarrierId,
                                request.DistanceKm,
                                request.BaseTariff,
                                request.RatePerKm),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.DeliveryNoteManage);

        endpoints.MapPost(
                "/transport-routes/{routeId:guid}/stops",
                (
                    Guid routeId,
                    AddTransportStopRequest request,
                    IAppendixLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.AddTransportStopAsync(
                            new AddTransportStopCommand(
                                routeId,
                                request.Sequence,
                                request.SalesOrderId,
                                request.DeliveryId,
                                request.AddressSnapshot),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.DeliveryNoteManage);

        return endpoints;
    }

    private static Guid ParseUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }

    private static async Task<IResult> ExecuteAsync<T>(
        Func<Task<T>> action,
        int successStatusCode = StatusCodes.Status200OK)
    {
        try
        {
            var value = await action();
            return Results.Json(value, statusCode: successStatusCode);
        }
        catch (LogisticsException exception)
        {
            return ApiProblems.Problem(
                exception.Kind switch
                {
                    LogisticsErrorKind.NotFound => StatusCodes.Status404NotFound,
                    LogisticsErrorKind.Conflict => StatusCodes.Status409Conflict,
                    LogisticsErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                    _ => StatusCodes.Status400BadRequest,
                },
                exception.Message,
                exception.Code);
        }
        catch (ArgumentException exception)
        {
            return ApiProblems.Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            return ApiProblems.Business(exception);
        }
    }
}

internal sealed record CreateWaveRequest(
    IReadOnlyCollection<Guid> SalesOrderIds,
    int Priority = 0);

internal sealed record UpsertUnitConversionRequest(
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Factor);

internal sealed record ConvertQuantityRequest(
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Quantity);

internal sealed record GenerateMrpRequest(Guid? WarehouseId);

internal sealed record UpsertRateCardRequest(
    string Code,
    string Name,
    string Currency,
    IReadOnlyCollection<UpsertRateCardLineRequest> Lines);

internal sealed record UpsertRateCardLineRequest(
    string ServiceCode,
    decimal UnitPrice,
    string UnitLabel);

internal sealed record ComputeRateChargesRequest(
    Dictionary<string, decimal> QuantitiesByService);

internal sealed record CreateTransportRouteRequest(
    string Code,
    string Name,
    Guid? CarrierId,
    decimal DistanceKm,
    decimal BaseTariff,
    decimal RatePerKm);

internal sealed record AddTransportStopRequest(
    int Sequence,
    Guid? SalesOrderId,
    Guid? DeliveryId,
    string? AddressSnapshot);
