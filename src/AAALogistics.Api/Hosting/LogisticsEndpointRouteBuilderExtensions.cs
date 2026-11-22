using AAALogistics.Application.Authentication;
using AAALogistics.Application.Documents;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class LogisticsEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapLogisticsEndpoints(
        this RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/units",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListUnitsAsync(cancellationToken)))
            .RequireAnyPermission(MasterDataReadPermissions.Materials);
        endpoints.MapPost(
                "/units",
                (
                    CreateUnitOfMeasureRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateUnitOfMeasureAsync(
                            new CreateUnitOfMeasureCommand(
                                request.Code,
                                request.Name,
                                request.DecimalScale),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);
        endpoints.MapPost(
                "/units/ensure-defaults",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.EnsureDefaultUnitsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);
        endpoints.MapPut(
                "/units/{unitId:guid}",
                (
                    Guid unitId,
                    UpdateUnitOfMeasureRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpdateUnitOfMeasureAsync(
                            new UpdateUnitOfMeasureCommand(
                                unitId,
                                request.Name,
                                request.DecimalScale),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/materials",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListMaterialsAsync(cancellationToken)))
            .RequireAnyPermission(MasterDataReadPermissions.Materials);
        endpoints.MapPost(
                "/materials",
                (
                    CreateMaterialRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () =>
                        {
                            var displayName = ResolveMaterialDisplayName(
                                request.DisplayName,
                                request.Description);
                            return service.CreateMaterialAsync(
                                new CreateMaterialCommand(
                                    request.Code,
                                    displayName,
                                    request.UnitId,
                                    request.CatalogPrice,
                                    request.Currency,
                                    request.ExternalId,
                                    request.RequiresLot,
                                    request.Description ?? displayName),
                                cancellationToken);
                        },
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);
        endpoints.MapPut(
                "/materials/{materialId:guid}",
                (
                    Guid materialId,
                    UpdateMaterialRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () =>
                        {
                            var displayName = ResolveMaterialDisplayName(
                                request.DisplayName,
                                request.Description);
                            return service.UpdateMaterialAsync(
                                new UpdateMaterialCommand(
                                    materialId,
                                    displayName,
                                    request.CatalogPrice,
                                    request.Currency,
                                    request.IsActive,
                                    request.ExternalId,
                                    request.RequiresLot,
                                    request.Description ?? displayName),
                                cancellationToken);
                        }))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/warehouses",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListWarehousesAsync(cancellationToken)))
            .RequireAnyPermission(MasterDataReadPermissions.Warehouses);
        endpoints.MapPost(
                "/warehouses",
                (
                    CreateWarehouseRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateWarehouseAsync(
                            new CreateWarehouseCommand(
                                request.Code,
                                request.Name,
                                request.SiteKind),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.WarehouseManage);
        endpoints.MapPut(
                "/warehouses/{warehouseId:guid}",
                (
                    Guid warehouseId,
                    UpdateWarehouseRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpdateWarehouseAsync(
                            new UpdateWarehouseCommand(
                                warehouseId,
                                request.Name,
                                request.SiteKind),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.WarehouseManage);
        endpoints.MapDelete(
                "/warehouses/{warehouseId:guid}",
                async (
                    Guid warehouseId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        await service.DeleteWarehouseAsync(
                            warehouseId,
                            cancellationToken);
                        return Results.NoContent();
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
                        return ApiProblems.Validation(exception);
                    }
                    catch (InvalidOperationException exception)
                    {
                        return ApiProblems.Business(exception);
                    }
                })
            .RequireAuthorization(AppPermissions.WarehouseManage)
            .RequireStepUp();

        endpoints.MapGet(
                "/suppliers",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListSuppliersAsync(cancellationToken)))
            .RequireAnyPermission(MasterDataReadPermissions.Suppliers);
        endpoints.MapPost(
                "/suppliers",
                (
                    CreatePartyRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateSupplierAsync(
                            ToCommand(request),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.SupplierManage);
        endpoints.MapPut(
                "/suppliers/{supplierId:guid}",
                (
                    Guid supplierId,
                    UpdatePartyRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpdateSupplierAsync(
                            ToUpdateCommand(supplierId, request),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SupplierManage);

        endpoints.MapGet(
                "/customers",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListCustomersAsync(cancellationToken)))
            .RequireAnyPermission(MasterDataReadPermissions.Customers);
        endpoints.MapPost(
                "/customers",
                (
                    CreatePartyRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateCustomerAsync(
                            ToCommand(request),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.CustomerManage);
        endpoints.MapPut(
                "/customers/{customerId:guid}",
                (
                    Guid customerId,
                    UpdatePartyRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpdateCustomerAsync(
                            ToUpdateCommand(customerId, request),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.CustomerManage);

        endpoints.MapGet(
                "/purchase-orders",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListPurchaseOrdersAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);
        endpoints.MapGet(
                "/purchase-orders/{purchaseOrderId:guid}",
                (
                    Guid purchaseOrderId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.GetPurchaseOrderAsync(
                            purchaseOrderId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);
        endpoints.MapPost(
                "/purchase-orders",
                (
                    CreatePurchaseOrderRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreatePurchaseOrderAsync(
                            new CreatePurchaseOrderCommand(
                                request.SupplierId,
                                request.DefaultWarehouseId,
                                request.DocumentDate,
                                request.ExpectedReceiptDate,
                                request.Currency,
                                request.ExternalId,
                                request.Lines.Select(line =>
                                    new CreatePurchaseOrderLineCommand(
                                        line.MaterialId,
                                        line.Quantity,
                                        line.PurchasePrice)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/goods-receipts/post",
                (
                    PostGoodsReceiptRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecutePostingAsync(
                        httpRequest,
                        key => service.PostGoodsReceiptAsync(
                            new PostGoodsReceiptCommand(
                                key,
                                request.PurchaseOrderId,
                                request.DocumentDate,
                                request.Lines.Select(line =>
                                    new PostGoodsReceiptLineCommand(
                                        line.PurchaseOrderLineId,
                                        line.WarehouseId,
                                        line.Quantity,
                                        line.LotCode,
                                        line.LotExpiryDate,
                                        line.OwnerCustomerId)).ToArray()),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.GoodsReceiptPost)
            .RequireStepUp();

        endpoints.MapGet(
                "/goods-receipts",
                (
                    Guid? purchaseOrderId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListGoodsReceiptsAsync(
                            purchaseOrderId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.GoodsReceiptPost);

        endpoints.MapGet(
                "/goods-receipts/{goodsReceiptId:guid}/pdf",
                async (
                    Guid goodsReceiptId,
                    IDocumentPdfService pdfs,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await pdfs.RenderGoodsReceiptAsync(
                            goodsReceiptId,
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            $"gr-{goodsReceiptId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            exception.Kind == LogisticsErrorKind.NotFound
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.GoodsReceiptPost);

        endpoints.MapGet(
                "/document-number-ranges",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListDocumentNumberRangesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapPut(
                "/document-number-ranges",
                (
                    UpsertDocumentNumberRangeRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpsertDocumentNumberRangeAsync(
                            request.DocumentType,
                            request.Prefix,
                            request.NextValue,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/sales-orders",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListSalesOrdersAsync(cancellationToken)))
            .RequireAnyPermission(
                AppPermissions.SalesOrderRead,
                AppPermissions.SalesOrderCreate,
                AppPermissions.SalesOrderPrepare,
                AppPermissions.SalesOrderClose,
                AppPermissions.CmrManage);
        endpoints.MapPost(
                "/sales-orders",
                (
                    CreateSalesOrderRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateSalesOrderAsync(
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
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.SalesOrderCreate);

        endpoints.MapPut(
                "/sales-orders/{salesOrderId:guid}/trade-terms",
                (
                    Guid salesOrderId,
                    UpdateSalesOrderTradeTermsRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpdateSalesOrderTradeTermsAsync(
                            new UpdateSalesOrderTradeTermsCommand(
                                salesOrderId,
                                request.Incoterms,
                                request.CustomsReference),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderCreate);

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/confirm",
                (
                    Guid salesOrderId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ConfirmSalesOrderAsync(
                            salesOrderId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderConfirm);

        endpoints.MapGet(
                "/deliveries",
                (
                    Guid? salesOrderId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListDeliveriesAsync(salesOrderId, cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderRead);

        endpoints.MapPost(
                "/deliveries/post",
                (
                    PostDeliveryRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (request.AllowFefoOverride
                        && !httpRequest.HttpContext.User.IsInRole(AppRoles.Admin))
                    {
                        return Task.FromResult<IResult>(
                            Problem(
                                StatusCodes.Status403Forbidden,
                                "AllowFefoOverride requiere rol Admin.",
                                "AAA-DELIVERY-0009"));
                    }

                    return ExecutePostingAsync(
                        httpRequest,
                        key => service.PostDeliveryAsync(
                            new PostDeliveryCommand(
                                key,
                                request.SalesOrderId,
                                request.DocumentDate,
                                request.Lines.Select(line =>
                                    new PostDeliveryLineCommand(
                                        line.SalesOrderLineId,
                                        line.WarehouseId,
                                        line.Quantity,
                                        line.LotId,
                                        line.OwnerCustomerId)).ToArray(),
                                request.AllowFefoOverride),
                            cancellationToken));
                })
            .RequireAuthorization(AppPermissions.DeliveryPost)
            .RequireStepUp();

        endpoints.MapPost(
                "/sales-returns/post",
                (
                    PostSalesReturnRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!Enum.TryParse<StockMovementType>(
                            request.Type,
                            ignoreCase: true,
                            out var type)
                        || type is not (StockMovementType.ReturnIn
                            or StockMovementType.ReturnOut))
                    {
                        return Task.FromResult<IResult>(
                            Problem(
                                StatusCodes.Status400BadRequest,
                                "El tipo debe ser ReturnIn o ReturnOut.",
                                "AAA-RETURN-0001"));
                    }

                    return ExecutePostingAsync(
                        httpRequest,
                        key => service.PostSalesReturnAsync(
                            new PostSalesReturnCommand(
                                key,
                                request.SalesOrderId,
                                request.OriginalDeliveryId,
                                type,
                                request.DocumentDate,
                                request.Reason,
                                request.Lines.Select(line =>
                                    new PostSalesReturnLineCommand(
                                        line.MaterialId,
                                        line.WarehouseId,
                                        line.LocationId,
                                        line.Quantity,
                                        line.LotId)).ToArray()),
                            cancellationToken));
                })
            .RequireAuthorization(AppPermissions.ReturnManage)
            .RequireStepUp();

        endpoints.MapPost(
                "/stock/transfers/post",
                (
                    PostTransferRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecutePostingAsync(
                        httpRequest,
                        key => service.PostTransferAsync(
                            new PostTransferCommand(
                                key,
                                request.MaterialId,
                                request.SourceWarehouseId,
                                request.DestinationWarehouseId,
                                request.Quantity,
                                request.Reason),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockTransfer)
            .RequireStepUp();

        endpoints.MapPost(
                "/stock/location-transfers",
                (
                    PostLocationTransferRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecutePostingAsync(
                        httpRequest,
                        key => service.PostLocationTransferAsync(
                            new PostLocationTransferCommand(
                                key,
                                request.MaterialId,
                                request.WarehouseId,
                                request.SourceLocationId,
                                request.DestinationLocationId,
                                request.Quantity,
                                request.Reason,
                                request.LotId),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockTransfer)
            .RequireStepUp();

        endpoints.MapPost(
                "/stock/putaway",
                (
                    PostPutawayRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecutePostingAsync(
                        httpRequest,
                        key => service.PostPutawayAsync(
                            new PostPutawayCommand(
                                key,
                                request.MaterialId,
                                request.WarehouseId,
                                request.DestinationLocationId,
                                request.Quantity,
                                request.LotId,
                                request.Reason),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockTransfer)
            .RequireStepUp();

        endpoints.MapGet(
                "/materials/{materialId:guid}/lots",
                (
                    Guid materialId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListLotsAsync(materialId, cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapPost(
                "/stock/adjustments/post",
                (
                    PostAdjustmentRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    PostAdjustmentAsync(
                        request,
                        httpRequest,
                        service,
                        cancellationToken))
            .RequireAuthorization(AppPermissions.StockAdjust)
            .RequireStepUp();

        endpoints.MapPost(
                "/stock/movements/{movementId:guid}/reverse",
                (
                    Guid movementId,
                    ReverseMovementRequest request,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecutePostingAsync(
                        httpRequest,
                        key => service.ReverseMovementAsync(
                            new ReverseMovementCommand(
                                key,
                                movementId,
                                request.Reason),
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockReverse)
            .RequireStepUp();

        endpoints.MapGet(
                "/stock",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.GetStockAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);
        endpoints.MapGet(
                "/stock/movements",
                (
                    int? limit,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.GetMovementsAsync(
                            limit ?? 200,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);
        endpoints.MapPost(
                "/stock/reconcile",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ReconcileStockAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.StockAdjust)
            .RequireStepUp();

        endpoints.MapPost(
                "/purchase-orders/{purchaseOrderId:guid}/cancel-open",
                (
                    Guid purchaseOrderId,
                    CancelReasonRequest? body,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CancelPurchaseOrderOpenAsync(
                            purchaseOrderId,
                            body?.Reason ?? string.Empty,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCancelOpen)
            .RequireStepUp();

        endpoints.MapPost(
                "/purchase-orders/{purchaseOrderId:guid}/close",
                (
                    Guid purchaseOrderId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ClosePurchaseOrderAsync(
                            purchaseOrderId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderClose)
            .RequireStepUp();

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/cancel",
                (
                    Guid salesOrderId,
                    CancelReasonRequest? body,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CancelSalesOrderAsync(
                            salesOrderId,
                            body?.Reason ?? string.Empty,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderCancel)
            .RequireStepUp();

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/close",
                (
                    Guid salesOrderId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CloseSalesOrderAsync(
                            salesOrderId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderClose)
            .RequireStepUp();

        endpoints.MapPost(
                "/sales-orders/{salesOrderId:guid}/reject",
                (
                    Guid salesOrderId,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.RejectSalesOrderAsync(
                            salesOrderId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.SalesOrderCancel)
            .RequireStepUp();

        endpoints.MapPut(
                "/stock/min-quantity",
                (
                    SetMinQuantityRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.SetMinQuantityAsync(
                            request.MaterialId,
                            request.WarehouseId,
                            request.MinQuantity,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockAdjust);

        endpoints.MapPut(
                "/stock/status",
                (
                    SetStockStatusRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                {
                    if (!Enum.TryParse<StockStatus>(
                            request.Status,
                            ignoreCase: true,
                            out var status))
                    {
                        return Task.FromResult<IResult>(
                            Problem(
                                StatusCodes.Status400BadRequest,
                                "Estado de stock no válido.",
                                "AAA-STOCK-0012"));
                    }

                    return ExecuteAsync(
                        () => service.SetStockStatusAsync(
                            new SetStockStatusCommand(
                                request.MaterialId,
                                request.WarehouseId,
                                request.LocationId,
                                request.LotId,
                                status),
                            cancellationToken));
                })
            .RequireAuthorization(AppPermissions.StockAdjust)
            .RequireStepUp();

        endpoints.MapGet(
                "/stock/below-minimum",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ListBelowMinimumAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapPost(
                "/stock/counts",
                (
                    CreateInventoryCountRequest request,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateInventoryCountAsync(
                            new CreateInventoryCountCommand(
                                request.WarehouseId,
                                request.Lines.Select(line =>
                                    new CreateInventoryCountLineCommand(
                                        line.MaterialId,
                                        line.CountedQuantity)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.StockCount);

        endpoints.MapPost(
                "/stock/counts/{inventoryCountId:guid}/post",
                (
                    Guid inventoryCountId,
                    HttpRequest httpRequest,
                    ILogisticsService service,
                    CancellationToken cancellationToken) =>
                    ExecutePostingAsync(
                        httpRequest,
                        key => service.PostInventoryCountAsync(
                            key,
                            inventoryCountId,
                            cancellationToken)))
            .RequireAuthorization(AppPermissions.StockCount)
            .RequireStepUp();

        endpoints.MapGet(
                "/reports/orders-by-status",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ReportOrdersByStatusAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapGet(
                "/reports/otif",
                (ILogisticsService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ReportOtifAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapGet(
                "/audit/entries",
                async (
                    IAuditService audit,
                    int? limit,
                    int? page,
                    int? pageSize,
                    DateTimeOffset? fromUtc,
                    DateTimeOffset? toUtc,
                    Guid? userId,
                    string? userName,
                    string? table,
                    string? area,
                    string? action,
                    bool? includePayloads,
                    CancellationToken cancellationToken) =>
                {
                    var size = Math.Clamp(
                        pageSize ?? limit ?? 100,
                        1,
                        500);
                    var result = await audit.QueryEntriesAsync(
                        new AuditEntryQuery(
                            fromUtc,
                            toUtc,
                            userId,
                            userName,
                            table,
                            area,
                            action,
                            page ?? 1,
                            size,
                            includePayloads ?? false),
                        cancellationToken);
                    return Results.Ok(
                        new AuditEntryPageResponse(
                            result.Items.Select(ToAuditEntryResponse).ToArray(),
                            result.TotalCount,
                            result.Page,
                            result.PageSize));
                })
            .RequireAuthorization(AppPermissions.AuditVerify);

        endpoints.MapGet(
                "/audit/entries/export",
                async (
                    IAuditService audit,
                    HttpContext httpContext,
                    string? format,
                    DateTimeOffset? fromUtc,
                    DateTimeOffset? toUtc,
                    Guid? userId,
                    string? userName,
                    string? table,
                    string? area,
                    string? action,
                    CancellationToken cancellationToken) =>
                {
                    var normalized = string.IsNullOrWhiteSpace(format)
                        ? "csv"
                        : format.Trim().ToLowerInvariant();
                    var bytes = await audit.ExportEntriesAsync(
                        new AuditEntryQuery(
                            fromUtc,
                            toUtc,
                            userId,
                            userName,
                            table,
                            area,
                            action,
                            1,
                            5_000,
                            IncludePayloads: true),
                        normalized,
                        cancellationToken);
                    var (contentType, fileName) = normalized switch
                    {
                        "json" => ("application/json", "auditoria.json"),
                        "pdf" => ("application/pdf", "auditoria.pdf"),
                        _ => ("text/csv", "auditoria.csv"),
                    };
                    httpContext.Response.Headers.ContentDisposition =
                        $"attachment; filename=\"{fileName}\"";
                    return Results.File(bytes, contentType, fileName);
                })
            .RequireAuthorization(AppPermissions.AuditVerify);

        endpoints.MapPost(
                "/audit/verify",
                async (IAuditService audit, CancellationToken cancellationToken) =>
                {
                    var result = await audit.VerifyAsync(cancellationToken);
                    return Results.Ok(
                        new AuditVerificationResponse(
                            result.IsValid,
                            result.EntryCount,
                            result.InvalidSequence,
                            result.Message));
                })
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));

        endpoints.MapGet(
                "/audit/checkpoints",
                async (IAuditService audit, CancellationToken cancellationToken) =>
                {
                    var rows = await audit.ListCheckpointsAsync(cancellationToken);
                    return Results.Ok(
                        rows.Select(value => new AuditCheckpointResponse(
                            value.Id,
                            value.CreatedUtc,
                            value.ThroughSequence,
                            value.ThroughHash,
                            value.ExternalPath,
                            value.CreatedByUserId,
                            value.Note)).ToArray());
                })
            .RequireAuthorization(AppPermissions.AuditVerify);

        endpoints.MapPost(
                "/audit/checkpoints",
                async (
                    IAuditService audit,
                    string? note,
                    CancellationToken cancellationToken) =>
                {
                    var created = await audit.CreateCheckpointAsync(
                        note,
                        cancellationToken);
                    return Results.Ok(
                        new AuditCheckpointResponse(
                            created.Id,
                            created.CreatedUtc,
                            created.ThroughSequence,
                            created.ThroughHash,
                            created.ExternalPath,
                            created.CreatedByUserId,
                            created.Note));
                })
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));

        endpoints.MapGet(
                "/audit/retention",
                async (IAuditService audit, CancellationToken cancellationToken) =>
                {
                    var status = await audit.GetRetentionStatusAsync(cancellationToken);
                    return Results.Ok(
                        new AuditRetentionStatusResponse(
                            status.RetentionDays,
                            status.EntriesOlderThanRetention,
                            status.LatestCheckpointSequence,
                            status.PurgeBlockedWithoutCheckpoint,
                            status.Message));
                })
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin));

        endpoints.MapGet(
                "/purchase-orders/{purchaseOrderId:guid}/pdf",
                async (
                    Guid purchaseOrderId,
                    IDocumentPdfService pdfs,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await pdfs.RenderPurchaseOrderAsync(
                            purchaseOrderId,
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            $"po-{purchaseOrderId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            exception.Kind == LogisticsErrorKind.NotFound
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapGet(
                "/sales-orders/{salesOrderId:guid}/pdf",
                async (
                    Guid salesOrderId,
                    IDocumentPdfService pdfs,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await pdfs.RenderSalesOrderAsync(
                            salesOrderId,
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            $"so-{salesOrderId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            exception.Kind == LogisticsErrorKind.NotFound
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.SalesOrderCreate);

        endpoints.MapGet(
                "/deliveries/{deliveryId:guid}/pdf",
                async (
                    Guid deliveryId,
                    IDocumentPdfService pdfs,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await pdfs.RenderDeliveryAsync(
                            deliveryId,
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            $"dn-{deliveryId:N}.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            exception.Kind == LogisticsErrorKind.NotFound
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.DeliveryPost);

        endpoints.MapGet(
                "/reports/otif/pdf",
                async (
                    IDocumentPdfService pdfs,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await pdfs.RenderOtifReportAsync(cancellationToken);
                        return Results.File(bytes, "application/pdf", "informe-otif.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            exception.Kind == LogisticsErrorKind.NotFound
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapGet(
                "/reports/orders-by-status/pdf",
                async (
                    IDocumentPdfService pdfs,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await pdfs.RenderOrdersByStatusReportAsync(
                            cancellationToken);
                        return Results.File(
                            bytes,
                            "application/pdf",
                            "pedidos-por-estado.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            exception.Kind == LogisticsErrorKind.NotFound
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapGet(
                "/reports/stock/pdf",
                async (
                    IDocumentPdfService pdfs,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var bytes = await pdfs.RenderStockReportAsync(cancellationToken);
                        return Results.File(bytes, "application/pdf", "informe-stock.pdf");
                    }
                    catch (LogisticsException exception)
                    {
                        return Problem(
                            exception.Kind == LogisticsErrorKind.NotFound
                                ? StatusCodes.Status404NotFound
                                : StatusCodes.Status400BadRequest,
                            exception.Message,
                            exception.Code);
                    }
                })
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapExtendedLogisticsEndpoints();
        endpoints.MapAppendixLogisticsEndpoints();
        endpoints.MapIndustrialLogisticsEndpoints();
        endpoints.MapGroup("/studio").MapStudioEndpoints();

        return endpoints;
    }

    private static string ResolveMaterialDisplayName(
        string? displayName,
        string? description)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            return description.Trim();
        }

        return string.Empty;
    }

    private static CreatePartyCommand ToCommand(CreatePartyRequest request) =>
        new(
            request.Name,
            request.TaxId,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Region,
            request.PostalCode,
            request.CountryCode,
            request.ExternalId,
            request.LeadTimeDays);

    private static UpdatePartyCommand ToUpdateCommand(
        Guid partyId,
        UpdatePartyRequest request) =>
        new(
            partyId,
            request.Name,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Region,
            request.PostalCode,
            request.CountryCode,
            request.IsActive,
            request.ExternalId,
            request.LeadTimeDays);

    private static Task<IResult> PostAdjustmentAsync(
        PostAdjustmentRequest request,
        HttpRequest httpRequest,
        ILogisticsService service,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<StockMovementType>(
                request.Type,
                ignoreCase: true,
                out var type))
        {
            return Task.FromResult<IResult>(
                Problem(
                    StatusCodes.Status400BadRequest,
                    "El tipo debe ser Opening o Adjustment.",
                    "AAA-ADJUSTMENT-0002"));
        }

        return ExecutePostingAsync(
            httpRequest,
            key => service.PostAdjustmentAsync(
                new PostAdjustmentCommand(
                    key,
                    request.MaterialId,
                    request.WarehouseId,
                    request.SignedQuantity,
                    type,
                    request.Reason),
                cancellationToken));
    }

    private static Task<IResult> ExecutePostingAsync<T>(
        HttpRequest request,
        Func<string, Task<T>> action)
    {
        var key = request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<IResult>(
                Problem(
                    StatusCodes.Status400BadRequest,
                    "Idempotency-Key es obligatorio.",
                    "AAA-IDEMPOTENCY-0001"));
        }

        return ExecuteAsync(() => action(key));
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
            return ApiProblems.Validation(exception);
        }
        catch (InvalidOperationException exception)
        {
            return ApiProblems.Business(exception);
        }
    }

    private static AuditEntryResponse ToAuditEntryResponse(AuditEntryView value) =>
        new(
            value.Sequence,
            value.TableName,
            value.RecordId,
            value.Action,
            value.UserId,
            value.UserName,
            value.Ip,
            value.Utc,
            value.OldJson,
            value.NewJson);

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
