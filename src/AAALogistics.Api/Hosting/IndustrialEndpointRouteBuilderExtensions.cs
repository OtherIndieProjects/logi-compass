using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class IndustrialEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapIndustrialLogisticsEndpoints(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/companies",
                (IIndustrialLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListCompaniesAsync(cancellationToken)))
            .RequireAnyPermission(
                AppPermissions.CustomerManage,
                AppPermissions.WarehouseManage,
                AppPermissions.InvoiceConfig,
                AppPermissions.UserManageRanges);

        endpoints.MapPost(
                "/companies",
                (
                    CreateCompanyRequest request,
                    IIndustrialLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateCompanyAsync(
                            new CreateCompanyCommand(
                                request.Code,
                                request.LegalName,
                                request.TaxId,
                                request.Currency),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/exchange-rates",
                (IIndustrialLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListExchangeRatesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.InvoiceIssue);

        endpoints.MapPost(
                "/exchange-rates",
                (
                    UpsertExchangeRateRequest request,
                    IIndustrialLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpsertExchangeRateAsync(
                            new UpsertExchangeRateCommand(
                                request.FromCurrency,
                                request.ToCurrency,
                                request.Rate,
                                request.ValidOn),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceIssue);

        endpoints.MapPost(
                "/exchange-rates/convert",
                (
                    ConvertFxRequest request,
                    IIndustrialLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ConvertFxAsync(
                            request.FromCurrency,
                            request.ToCurrency,
                            request.Amount,
                            request.OnDate,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.InvoiceIssue);

        endpoints.MapPost(
                "/commercial-terms",
                (
                    UpsertCommercialTermsRequest request,
                    IIndustrialLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpsertCommercialTermsAsync(
                            new UpsertCustomerCommercialTermsCommand(
                                request.CustomerId,
                                request.CompanyId,
                                request.PaymentTermDays,
                                request.CreditLimit,
                                request.Currency,
                                request.OpenReceivables),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/commercial-terms",
                (
                    Guid customerId,
                    Guid companyId,
                    IIndustrialLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(async () =>
                    {
                        var terms = await service.GetCommercialTermsAsync(
                            customerId,
                            companyId,
                            cancellationToken);
                        return terms
                            ?? throw new LogisticsException(
                                LogisticsErrorKind.NotFound,
                                "AAA-CREDIT-0003",
                                "Sin condiciones comerciales.");
                    }))
            .RequireAnyPermission(AppPermissions.CustomerManage, AppPermissions.InvoiceConfig);

        endpoints.MapPost(
                "/rf/scan",
                (
                    RfScanRequest request,
                    IIndustrialLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ResolveRfScanAsync(
                        request.Code,
                        cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapGet(
                "/integrations/sap/status",
                (ISapIntegrationService sap) =>
                    Results.Json(sap.GetStatus()))
            .RequireAnyPermission(
                AppPermissions.MaterialManage,
                AppPermissions.DeliveryPost);

        endpoints.MapGet(
                "/integrations/sap/materials",
                (
                    ISapIntegrationService sap,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => sap.ListRemoteMaterialsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/integrations/sap/materials/sync",
                (
                    ISapIntegrationService sap,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => sap.SyncMaterialsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/integrations/sap/outbox",
                (
                    ISapIntegrationService sap,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => sap.ListOutboxAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.DeliveryPost);

        endpoints.MapPost(
                "/integrations/sap/deliveries/{deliveryId:guid}/push",
                (
                    Guid deliveryId,
                    ISapIntegrationService sap,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => sap.QueueDeliveryPushAsync(deliveryId, cancellationToken),
                        StatusCodes.Status202Accepted))
            .RequireAuthorization(AppPermissions.DeliveryPost);

        endpoints.MapPost(
                "/integrations/sap/outbox/process",
                (
                    ISapIntegrationService sap,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(async () =>
                    {
                        await sap.ProcessOutboxAsync(cancellationToken);
                        return new { processed = true };
                    }))
            .RequireAuthorization(AppPermissions.DeliveryPost);

        return endpoints;
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

internal sealed record CreateCompanyRequest(
    string Code,
    string LegalName,
    string TaxId,
    string Currency);

internal sealed record UpsertExchangeRateRequest(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly ValidOn);

internal sealed record ConvertFxRequest(
    string FromCurrency,
    string ToCurrency,
    decimal Amount,
    DateOnly? OnDate = null);

internal sealed record UpsertCommercialTermsRequest(
    Guid CustomerId,
    Guid CompanyId,
    int PaymentTermDays,
    decimal CreditLimit,
    string Currency,
    decimal OpenReceivables = 0);

internal sealed record RfScanRequest(string Code);
