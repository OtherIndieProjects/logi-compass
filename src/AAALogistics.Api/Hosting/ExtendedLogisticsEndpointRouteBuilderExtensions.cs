using System.Security.Claims;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Branding;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Billing;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Security;
using AAALogistics.Domain.Warehouse;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class ExtendedLogisticsEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapExtendedLogisticsEndpoints(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/warehouses/{warehouseId:guid}/locations",
                (
                    Guid warehouseId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListLocationsAsync(warehouseId, cancellationToken)))
            .RequireAnyPermission(
                AppPermissions.StockRead,
                AppPermissions.LocationManage,
                AppPermissions.StockTransfer);

        endpoints.MapPost(
                "/warehouses/{warehouseId:guid}/locations",
                (
                    Guid warehouseId,
                    CreateLocationRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateLocationAsync(
                            new CreateLocationNodeCommand(
                                warehouseId,
                                request.Code,
                                request.Name,
                                ParseLocationKind(request.Kind),
                                request.ParentId),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.LocationManage);

        endpoints.MapPut(
                "/warehouses/{warehouseId:guid}/locations/{locationId:guid}",
                (
                    Guid warehouseId,
                    Guid locationId,
                    UpdateLocationRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpdateLocationAsync(
                            new UpdateLocationNodeCommand(
                                warehouseId,
                                locationId,
                                request.Name,
                                request.IsActive),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.LocationManage);

        endpoints.MapDelete(
                "/warehouses/{warehouseId:guid}/locations/{locationId:guid}",
                async (
                    Guid warehouseId,
                    Guid locationId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await service.DeleteLocationAsync(
                            warehouseId,
                            locationId,
                            cancellationToken);
                        return Results.NoContent();
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                    catch (ArgumentException exception)
                    {
                        return ApiProblems.Validation(exception);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return ApiProblems.Business(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.LocationManage)
            .RequireStepUp();

        endpoints.MapGet(
                "/sales-orders/{salesOrderId:guid}/prep",
                (
                    Guid salesOrderId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListActivePrepAsync(salesOrderId, cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/prep/join",
                (
                    Guid salesOrderId,
                    JoinPrepRequest request,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryUser(principal, out var userId, out var userName))
                    {
                        return Task.FromResult(UnauthorizedUser());
                    }

                    return ExecuteAsync(
                        () => service.JoinPrepAsync(
                            new JoinPrepCommand(
                                salesOrderId,
                                ParsePrepMode(request.Mode)),
                            userId,
                            userName,
                            cancellationToken));
                })
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/prep/leave",
                async (
                    Guid salesOrderId,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryUser(principal, out var userId, out _))
                    {
                        return UnauthorizedUser();
                    }

                    try
                    {
                        await service.LeavePrepAsync(
                            salesOrderId,
                            userId,
                            cancellationToken);
                        return Results.NoContent();
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/prep/mark-prepared",
                async (
                    Guid salesOrderId,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryUser(principal, out var userId, out _))
                    {
                        return UnauthorizedUser();
                    }

                    try
                    {
                        await service.MarkPreparedAsync(
                            salesOrderId,
                            userId,
                            cancellationToken);
                        return Results.NoContent();
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapGet(
                "/sales-orders/{salesOrderId:guid}/prep/picks",
                (
                    Guid salesOrderId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListPrepPicksAsync(
                            salesOrderId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/prep/picks/{pickId:guid}/confirm",
                async (
                    Guid salesOrderId,
                    Guid pickId,
                    ConfirmPrepPickRequest request,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryUser(principal, out var userId, out _))
                    {
                        return UnauthorizedUser();
                    }

                    try
                    {
                        var pick = await service.ConfirmPrepPickAsync(
                            new ConfirmPrepPickCommand(
                                salesOrderId,
                                pickId,
                                request.ConfirmedQuantity,
                                userId),
                            cancellationToken);
                        return Results.Ok(pick);
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderPrepare);

        endpoints.MapGet(
                "/carriers",
                (IExtendedLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListCarriersAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.CarrierManage);

        endpoints.MapPost(
                "/carriers",
                (
                    CreateCarrierRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateCarrierAsync(
                            new CreateCarrierCommand(
                                request.Code,
                                request.Name,
                                request.TaxId,
                                request.Phone),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.CarrierManage);

        endpoints.MapPost(
                "/cmr",
                (
                    CreateCmrRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateCmrAsync(
                            new CreateCmrCommand(
                                request.SalesOrderId,
                                request.DeliveryId,
                                request.CarrierId,
                                request.VehiclePlate,
                                request.Packages,
                                request.Remarks),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.CmrManage);

        endpoints.MapGet(
                "/cmr/{cmrId:guid}/pdf",
                async (
                    Guid cmrId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await service.RenderCmrPdfAsync(
                            cmrId,
                            cancellationToken);
                        return Results.File(bytes, "application/pdf", $"cmr-{cmrId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.CmrManage);

        endpoints.MapPost(
                "/handling-units",
                (
                    CreateHandlingUnitRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateHandlingUnitAsync(
                            new CreateHandlingUnitCommand(
                                request.SalesOrderId,
                                request.DeliveryId,
                                request.PackageCount,
                                request.Sscc),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.DeliveryNoteManage);

        endpoints.MapGet(
                "/handling-units",
                async (
                    Guid salesOrderId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var units = await service.ListHandlingUnitsByOrderAsync(
                            salesOrderId,
                            cancellationToken);
                        return Results.Ok(
                            units.Select(value => new HandlingUnitResponse(
                                value.Id,
                                value.Sscc,
                                value.SalesOrderId,
                                value.DeliveryId,
                                value.PackageCount,
                                value.CreatedUtc)).ToArray());
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.DeliveryNoteManage);

        endpoints.MapGet(
                "/deliveries/{deliveryId:guid}/packing-list.pdf",
                async (
                    Guid deliveryId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await service.RenderPackingListPdfAsync(
                            deliveryId,
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            $"packing-{deliveryId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.DeliveryNoteManage);

        endpoints.MapGet(
                "/invoices",
                (IExtendedLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListInvoicesAsync(cancellationToken)))
            .RequireAnyPermission(
                AppPermissions.InvoicePrint,
                AppPermissions.InvoiceIssue,
                AppPermissions.InvoiceRectify);

        endpoints.MapPost(
                "/invoices",
                (
                    HttpRequest httpRequest,
                    IssueInvoiceRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
                    if (string.IsNullOrWhiteSpace(idempotencyKey))
                    {
                        idempotencyKey = request.IdempotencyKey ?? string.Empty;
                    }

                    if (string.IsNullOrWhiteSpace(idempotencyKey))
                    {
                        return Task.FromResult<IResult>(
                            Problem(
                                StatusCodes.Status400BadRequest,
                                "Idempotency-Key es obligatorio.",
                                "AAA-IDEMPOTENCY-0001"));
                    }

                    return ExecuteAsync(
                        () => service.IssueInvoiceAsync(
                            new IssueInvoiceCommand(
                                request.CustomerId,
                                request.SalesOrderId,
                                request.SeriesCode,
                                request.TaxRate,
                                request.Lines.Select(line =>
                                    new IssueInvoiceLineCommand(
                                        line.Description,
                                        line.Quantity,
                                        line.UnitPrice)).ToArray(),
                                idempotencyKey),
                            cancellationToken),
                        StatusCodes.Status201Created);
                })
            .RequireAuthorization(AppPermissions.InvoiceIssue)
            .RequireStepUp();

        endpoints.MapPost(
                "/invoices/{invoiceId:guid}/rectify",
                (
                    Guid invoiceId,
                    HttpRequest httpRequest,
                    RectifyInvoiceRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
                    if (string.IsNullOrWhiteSpace(idempotencyKey))
                    {
                        return Task.FromResult<IResult>(
                            Problem(
                                StatusCodes.Status400BadRequest,
                                "Idempotency-Key es obligatorio.",
                                "AAA-IDEMPOTENCY-0001"));
                    }

                    return ExecuteAsync(
                        () => service.RectifyInvoiceAsync(
                            invoiceId,
                            request.Reason,
                            idempotencyKey,
                            cancellationToken));
                })
            .RequireAuthorization(AppPermissions.InvoiceRectify)
            .RequireStepUp();

        endpoints.MapGet(
                "/invoices/{invoiceId:guid}/pdf",
                async (
                    Guid invoiceId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
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
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.InvoicePrint);

        endpoints.MapGet(
                "/quotes",
                (IExtendedLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListQuotesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderRead);

        endpoints.MapPost(
                "/quotes",
                (
                    CreateQuoteRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateQuoteAsync(
                            new CreateQuoteCommand(
                                request.CustomerId,
                                request.DefaultWarehouseId,
                                request.ValidUntil,
                                request.Currency,
                                request.TaxRate,
                                request.Lines.Select(line =>
                                    new CreateQuoteLineCommand(
                                        line.MaterialId,
                                        line.Quantity,
                                        line.UnitPrice)).ToArray(),
                                request.Notes),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.SalesOrderCreate);

        endpoints.MapPost(
                "/quotes/{quoteId:guid}/send",
                (
                    Guid quoteId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.SendQuoteAsync(quoteId, cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderCreate);

        endpoints.MapPost(
                "/quotes/{quoteId:guid}/accept",
                (
                    Guid quoteId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.AcceptQuoteAsync(quoteId, cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderCreate)
            .RequireStepUp();

        endpoints.MapPost(
                "/quotes/{quoteId:guid}/reject",
                (
                    Guid quoteId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.RejectQuoteAsync(quoteId, cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderCreate);

        endpoints.MapGet(
                "/quotes/{quoteId:guid}/pdf",
                async (
                    Guid quoteId,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await service.RenderQuotePdfAsync(
                            quoteId,
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            $"presupuesto-{quoteId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderRead);

        endpoints.MapGet(
                "/issuer-fiscal",
                async (
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var issuer = await service.GetIssuerAsync(cancellationToken);
                        if (issuer is null)
                        {
                            return Results.NoContent();
                        }

                        return Results.Ok(ToIssuerResponse(issuer));
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.InvoicePrint);

        endpoints.MapPut(
                "/issuer-fiscal",
                (
                    UpsertIssuerRequest request,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    var sealOk = TryDecodePngBase64(
                        request.SealPngBase64,
                        out var sealBytes,
                        out var sealError);
                    var signatureOk = TryDecodePngBase64(
                        request.SignaturePngBase64,
                        out var signatureBytes,
                        out var signatureError);
                    var logoOk = TryDecodePngBase64(
                        request.LogoPngBase64,
                        out var logoBytes,
                        out var logoError);
                    if (!sealOk || !signatureOk || !logoOk)
                    {
                        return Task.FromResult<IResult>(
                            Problem(
                                StatusCodes.Status400BadRequest,
                                sealError ?? signatureError ?? logoError
                                ?? "PNG Base64 inválido.",
                                "AAA-FAC-0011"));
                    }

                    return ExecuteAsync(
                        async () =>
                        {
                            var saved = await service.UpsertIssuerAsync(
                                new UpsertIssuerCommand(
                                    request.LegalName,
                                    request.TaxId,
                                    request.AddressLine,
                                    request.City,
                                    request.PostalCode,
                                    request.CountryCode,
                                    request.Phone,
                                    request.Email,
                                    request.Website,
                                    request.SignerName,
                                    request.SignerTitle,
                                    request.Iban,
                                    request.BankName,
                                    request.PaymentMethod,
                                    request.PaymentDueDays,
                                    request.LegalFooterText,
                                    request.IrpfPercent,
                                    request.SealPngRelativePath,
                                    sealBytes,
                                    request.SignaturePngRelativePath,
                                    signatureBytes,
                                    request.LogoPngRelativePath,
                                    logoBytes),
                                cancellationToken);
                            return ToIssuerResponse(saved);
                        });
                })
            .RequireAuthorization(AppPermissions.InvoiceConfig)
            .RequireStepUp();

        endpoints.MapGet(
                "/branding",
                async (
                    IAppBrandingStore branding,
                    CancellationToken cancellationToken) =>
                {
                    var snapshot = await branding.GetAsync(cancellationToken);
                    return Results.Ok(ToBrandingResponse(snapshot));
                })
            .RequireAuthorization();

        endpoints.MapGet(
                "/branding/logo",
                async (
                    IAppBrandingStore branding,
                    CancellationToken cancellationToken) =>
                {
                    var logo = await branding.TryReadLogoAsync(cancellationToken);
                    if (logo is null)
                    {
                        return Results.NotFound();
                    }

                    return Results.File(logo.Value.Bytes, logo.Value.ContentType, "logo");
                })
            .RequireAuthorization();

        endpoints.MapPut(
                "/branding",
                async (
                    UpsertAppBrandingRequest request,
                    IAppBrandingStore branding,
                    CancellationToken cancellationToken) =>
                {
                    byte[]? logoBytes = null;
                    if (!string.IsNullOrWhiteSpace(request.LogoBase64))
                    {
                        try
                        {
                            logoBytes = Convert.FromBase64String(request.LogoBase64);
                        }
                        catch (FormatException)
                        {
                            return Problem(
                                StatusCodes.Status400BadRequest,
                                "Logo Base64 no es válido.",
                                "AAA-BRAND-0007");
                        }
                    }

                    try
                    {
                        var saved = await branding.UpsertAsync(
                            new UpsertAppBrandingCommand(
                                request.DisplayName,
                                request.AccentHex,
                                request.Tagline,
                                request.LogoFileName,
                                logoBytes,
                                request.ClearLogo),
                            cancellationToken);
                        return Results.Ok(ToBrandingResponse(saved));
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/material-deletion-requests",
                (IExtendedLogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListMaterialDeletionRequestsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialDeleteApprove);

        endpoints.MapPost(
                "/materials/{materialId:guid}/deletion-requests",
                (
                    Guid materialId,
                    MaterialDeletionRequestBody request,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryUser(principal, out var userId, out _))
                    {
                        return Task.FromResult(UnauthorizedUser());
                    }

                    return ExecuteAsync(
                        () => service.RequestMaterialDeletionAsync(
                            materialId,
                            userId,
                            request.Reason,
                            cancellationToken),
                        StatusCodes.Status201Created);
                })
            .RequireAuthorization(AppPermissions.MaterialDelete)
            .RequireStepUp();

        endpoints.MapPost(
                "/material-deletion-requests/{requestId:guid}/approve",
                async (
                    Guid requestId,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryUser(principal, out var userId, out _))
                    {
                        return UnauthorizedUser();
                    }

                    try
                    {
                        await service.ApproveMaterialDeletionAsync(
                            requestId,
                            userId,
                            cancellationToken);
                        return Results.NoContent();
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.MaterialDeleteApprove)
            .RequireStepUp();

        endpoints.MapPost(
                "/material-deletion-requests/{requestId:guid}/reject",
                async (
                    Guid requestId,
                    ClaimsPrincipal principal,
                    IExtendedLogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!TryUser(principal, out var userId, out _))
                    {
                        return UnauthorizedUser();
                    }

                    try
                    {
                        await service.RejectMaterialDeletionAsync(
                            requestId,
                            userId,
                            cancellationToken);
                        return Results.NoContent();
                    }
                    catch (LogisticsException exception)
                    {
                        return MapLogistics(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.MaterialDeleteApprove)
            .RequireStepUp();

        return endpoints;
    }

    private static LocationNodeKind ParseLocationKind(string? kind)
    {
        if (Enum.TryParse<LocationNodeKind>(kind, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return LocationNodeKind.Zone;
    }

    private static PrepLockMode ParsePrepMode(string? mode)
    {
        if (Enum.TryParse<PrepLockMode>(mode, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return PrepLockMode.Open;
    }

    private static bool TryUser(
        ClaimsPrincipal principal,
        out Guid userId,
        out string userName)
    {
        userName = principal.FindFirstValue(JwtClaimNames.UserName)
            ?? principal.Identity?.Name
            ?? "usuario";
        var subject = principal.FindFirstValue(JwtClaimNames.Subject);
        return Guid.TryParse(subject, out userId);
    }

    private static IResult UnauthorizedUser() =>
        Problem(
            StatusCodes.Status401Unauthorized,
            "Usuario no autenticado.",
            "AAA-AUTH-0001");

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
            return MapLogistics(exception);
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

    private static IResult MapLogistics(LogisticsException exception)
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

    private static IResult Problem(int statusCode, string title, string code) =>
        Results.Problem(
            statusCode: statusCode,
            title: title,
            type: $"https://errors.aaalogistics.local/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
            });

    private static AppBrandingResponse ToBrandingResponse(AppBrandingSnapshot snapshot) =>
        new(
            snapshot.DisplayName,
            snapshot.AccentHex,
            snapshot.Tagline,
            snapshot.LogoRelativePath,
            snapshot.HasLogo);

    private static IssuerFiscalResponse ToIssuerResponse(IssuerFiscalProfile issuer) =>
        new(
            issuer.Id,
            issuer.LegalName,
            issuer.TaxId,
            issuer.AddressLine,
            issuer.City,
            issuer.PostalCode,
            issuer.CountryCode,
            issuer.Phone,
            issuer.Email,
            issuer.Website,
            issuer.SignerName,
            issuer.SignerTitle,
            issuer.Iban,
            issuer.BankName,
            issuer.PaymentMethod,
            issuer.PaymentDueDays,
            issuer.LegalFooterText,
            issuer.IrpfPercent,
            issuer.SealPngRelativePath,
            issuer.SignaturePngRelativePath,
            issuer.LogoPngRelativePath,
            issuer.VerifactuMode);

    private static bool TryDecodePngBase64(
        string? base64,
        out byte[]? bytes,
        out string? error)
    {
        bytes = null;
        error = null;
        if (string.IsNullOrWhiteSpace(base64))
        {
            return true;
        }

        try
        {
            bytes = Convert.FromBase64String(base64);
            return true;
        }
        catch (FormatException)
        {
            error = "PNG Base64 no es válido.";
            return false;
        }
    }
}

internal sealed record CreateLocationRequest(
    string Code,
    string Name,
    string Kind,
    Guid? ParentId);

internal sealed record UpdateLocationRequest(
    string Name,
    bool IsActive);

internal sealed record JoinPrepRequest(string Mode);

internal sealed record ConfirmPrepPickRequest(decimal ConfirmedQuantity);

internal sealed record CreateCarrierRequest(
    string Code,
    string Name,
    string? TaxId,
    string? Phone);

internal sealed record CreateCmrRequest(
    Guid SalesOrderId,
    Guid? DeliveryId,
    Guid CarrierId,
    string VehiclePlate,
    int Packages,
    string? Remarks);

internal sealed record IssueInvoiceRequest(
    Guid CustomerId,
    Guid? SalesOrderId,
    string SeriesCode,
    decimal TaxRate,
    IReadOnlyCollection<IssueInvoiceLineRequest> Lines,
    string? IdempotencyKey = null);

internal sealed record IssueInvoiceLineRequest(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

internal sealed record RectifyInvoiceRequest(string Reason);

internal sealed record UpsertIssuerRequest(
    string LegalName,
    string TaxId,
    string AddressLine,
    string City,
    string PostalCode,
    string CountryCode,
    string? SealPngRelativePath = null,
    string? SealPngBase64 = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    string? SignerName = null,
    string? SignerTitle = null,
    string? Iban = null,
    string? BankName = null,
    string? PaymentMethod = null,
    int PaymentDueDays = 30,
    string? LegalFooterText = null,
    decimal IrpfPercent = 0,
    string? SignaturePngRelativePath = null,
    string? SignaturePngBase64 = null,
    string? LogoPngRelativePath = null,
    string? LogoPngBase64 = null);

internal sealed record MaterialDeletionRequestBody(string Reason);

internal sealed record CreateQuoteLineRequest(
    Guid MaterialId,
    decimal Quantity,
    decimal UnitPrice);

internal sealed record CreateQuoteRequest(
    Guid CustomerId,
    Guid DefaultWarehouseId,
    DateOnly ValidUntil,
    string Currency,
    decimal TaxRate,
    IReadOnlyCollection<CreateQuoteLineRequest> Lines,
    string? Notes = null);
