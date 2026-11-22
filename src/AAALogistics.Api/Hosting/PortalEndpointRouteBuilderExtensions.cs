using System.Security.Claims;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class PortalEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapPortalContentEndpoints(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/catalog",
                async (
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var materials = await service.ListMaterialsAsync(
                        cancellationToken);
                    var items = materials
                        .Where(value => value.IsActive)
                        .OrderBy(value => value.Code, StringComparer.Ordinal)
                        .Select(value => new PortalCatalogItemResponse(
                            value.Id,
                            value.Code,
                            string.IsNullOrWhiteSpace(value.DisplayName)
                                ? value.Description
                                : value.DisplayName,
                            value.CatalogPrice,
                            value.Currency))
                        .ToArray();
                    return Results.Ok(items);
                })
            .RequireAuthorization(AppPermissions.SalesOrderCreateOwn)
            .WithName("PortalCatalog");

        endpoints.MapGet(
                "/warehouses",
                async (
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var warehouses = await service.ListWarehousesAsync(
                        cancellationToken);
                    var items = warehouses
                        .OrderBy(value => value.Code, StringComparer.Ordinal)
                        .Select(value => new PortalWarehouseResponse(
                            value.Id,
                            value.Code,
                            value.Name))
                        .ToArray();
                    return Results.Ok(items);
                })
            .RequireAuthorization(AppPermissions.SalesOrderCreateOwn)
            .WithName("PortalWarehouses");

        endpoints.MapGet(
                "/atp",
                async (
                    Guid materialId,
                    Guid warehouseId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var atp = await service.GetAtpAsync(
                            materialId,
                            warehouseId,
                            cancellationToken);
                        return Results.Ok(
                            new PortalAtpResponse(
                                atp.MaterialId,
                                atp.WarehouseId,
                                atp.AvailableQuantity));
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderCreateOwn)
            .WithName("PortalAtp");

        endpoints.MapGet(
                "/sales-orders",
                async (
                    ClaimsPrincipal principal,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var customerClaim = principal.FindFirstValue(
                        JwtClaimNames.CustomerId);
                    if (!Guid.TryParse(customerClaim, out var customerId))
                    {
                        return Problem(
                            StatusCodes.Status403Forbidden,
                            "Cliente no identificado.",
                            "AAA-PORTAL-0021");
                    }

                    try
                    {
                        var orders = await service.ListSalesOrdersAsync(
                            cancellationToken,
                            customerId);
                        return Results.Ok(orders);
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderCreateOwn)
            .WithName("PortalSalesOrders");

        endpoints.MapPost(
                "/sales-orders",
                async (
                    CreateSalesOrderRequest request,
                    ClaimsPrincipal principal,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var customerClaim = principal.FindFirstValue(
                        JwtClaimNames.CustomerId);
                    if (!Guid.TryParse(customerClaim, out var customerId)
                        || customerId != request.CustomerId)
                    {
                        return Problem(
                            StatusCodes.Status403Forbidden,
                            "Solo puede crear pedidos para su propio cliente.",
                            "AAA-PORTAL-0020");
                    }

                    try
                    {
                        var created = await service.CreateSalesOrderAsync(
                            new CreateSalesOrderCommand(
                                request.CustomerId,
                                request.DefaultWarehouseId,
                                request.RequestedDeliveryDate,
                                request.Currency,
                                request.ExternalId,
                                request.Lines.Select(line =>
                                    new CreateSalesOrderLineCommand(
                                        line.MaterialId,
                                        line.Quantity)).ToArray(),
                                request.Incoterms,
                                request.CustomsReference),
                            cancellationToken);
                        return Results.Json(
                            created,
                            statusCode: StatusCodes.Status201Created);
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderCreateOwn)
            .WithName("PortalCreateSalesOrder");

        endpoints.MapGet(
                "/invoices",
                async (
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var customerClaim = principal.FindFirstValue(
                        JwtClaimNames.CustomerId);
                    if (!Guid.TryParse(customerClaim, out var customerId))
                    {
                        return Problem(
                            StatusCodes.Status403Forbidden,
                            "Cliente no identificado.",
                            "AAA-PORTAL-0021");
                    }

                    try
                    {
                        var invoices = await service.ListInvoicesAsync(
                            cancellationToken,
                            customerId);
                        var own = invoices
                            .Select(value => new InvoiceResponse(
                                value.Id,
                                value.DocumentNumber,
                                value.Kind,
                                value.Status,
                                value.CustomerId,
                                value.SalesOrderId,
                                value.NetAmount,
                                value.TaxRate,
                                value.TaxAmount,
                                value.TotalAmount,
                                value.SifHash,
                                value.PreviousSifHash,
                                value.IssueDate,
                                value.Lines
                                    .Select(line => new InvoiceLineResponse(
                                        line.Description,
                                        line.Quantity,
                                        line.UnitPrice))
                                    .ToArray()))
                            .ToArray();
                        return Results.Ok(own);
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.InvoicePrint)
            .WithName("PortalInvoices");

        endpoints.MapGet(
                "/invoices/{invoiceId:guid}/pdf",
                async (
                    Guid invoiceId,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var customerClaim = principal.FindFirstValue(
                        JwtClaimNames.CustomerId);
                    if (!Guid.TryParse(customerClaim, out var customerId))
                    {
                        return Problem(
                            StatusCodes.Status403Forbidden,
                            "Cliente no identificado.",
                            "AAA-PORTAL-0021");
                    }

                    try
                    {
                        var invoices = await service.ListInvoicesAsync(
                            cancellationToken,
                            customerId);
                        if (invoices.All(value => value.Id != invoiceId))
                        {
                            return Problem(
                                StatusCodes.Status404NotFound,
                                "Factura no encontrada.",
                                "AAA-PORTAL-0022");
                        }

                        var bytes = await service.RenderInvoicePdfAsync(
                            invoiceId,
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            $"invoice-{invoiceId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.InvoicePrint)
            .WithName("PortalInvoicePdf");

        endpoints.MapGet(
                "/registration/pow-challenge",
                (AAALogistics.Application.Abstractions.ICaptchaVerifier captcha) =>
                    Results.Ok(
                        new PowChallengeResponse(
                            captcha.CreateChallenge(),
                            captcha.DifficultyBits)))
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        endpoints.MapPost(
                "/registration",
                async (
                    SubmitRegistrationRequest request,
                    IRegistrationService service,
                    AAALogistics.Application.Abstractions.ICaptchaVerifier captcha,
                    CancellationToken cancellationToken) =>
                {
                    if (request is null)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "La solicitud de registro está vacía.",
                            "AAA-PORTAL-0001");
                    }

                    if (!string.IsNullOrWhiteSpace(request.Website))
                    {
                        return Results.NoContent();
                    }

                    if (!request.AcceptTerms)
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "Hay que aceptar los textos legales para enviar la solicitud.",
                            "AAA-PORTAL-0004");
                    }

                    if (!captcha.Verify(request.PowChallenge, request.PowNonce))
                    {
                        return Problem(
                            StatusCodes.Status400BadRequest,
                            "Prueba de trabajo (PoW) inválida o ausente.",
                            "AAA-PORTAL-0005");
                    }

                    try
                    {
                        var created = await service.SubmitAsync(
                            new SubmitRegistrationCommand(
                                request.CompanyName,
                                request.TaxId,
                                request.ContactName,
                                request.Email,
                                request.UserName,
                                request.AddressLine1,
                                request.City,
                                request.PostalCode,
                                request.CountryCode,
                                request.AcceptTerms),
                            cancellationToken);
                        return Results.Json(
                            new RegistrationSubmittedResponse(
                                created.Id,
                                created.Status,
                                "La solicitud quedó pendiente de aprobación."),
                            statusCode: StatusCodes.Status201Created);
                    }
                    catch (LogisticsException exception)
                    {
                        var status = exception.Kind switch
                        {
                            LogisticsErrorKind.NotFound =>
                                StatusCodes.Status404NotFound,
                            LogisticsErrorKind.Conflict =>
                                StatusCodes.Status409Conflict,
                            LogisticsErrorKind.Forbidden =>
                                StatusCodes.Status403Forbidden,
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
                })
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("PortalRegistration");

        return endpoints;
    }

    private static IResult Problem(
        int statusCode,
        string title,
        string code) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            type: $"https://errors.aaalogistics.local/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });
}
