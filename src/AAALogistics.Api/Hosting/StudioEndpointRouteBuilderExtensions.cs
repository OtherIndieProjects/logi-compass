using System.Security.Claims;
using AAALogistics.Application.Authentication;
using AAALogistics.Application.Logistics;
using AAALogistics.Contracts;
using AAALogistics.Domain.Security;
using AAALogistics.Domain.Studio;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AAALogistics.Api.Hosting;

internal static class StudioEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapStudioEndpoints(this RouteGroupBuilder endpoints)
    {
        MapP1(endpoints);
        MapP2(endpoints);
        MapP3(endpoints);
        MapP4(endpoints);
        MapP5(endpoints);
        MapP6(endpoints);
        MapP7(endpoints);
        MapP8(endpoints);
        MapP9(endpoints);
        MapP10(endpoints);
        MapP11(endpoints);
        return endpoints;
    }

    private static void MapP1(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/tax-codes",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListTaxCodesAsync(cancellationToken)))
            .RequireAnyPermission(AppPermissions.InvoiceIssue, AppPermissions.InvoiceConfig);

        endpoints.MapPost(
                "/tax-codes",
                (
                    CreateTaxCodeRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateTaxCodeAsync(
                            new CreateTaxCodeCommand(
                                request.Code,
                                request.Name,
                                request.RatePercent,
                                request.CountryCode),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/price-conditions",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListPriceConditionsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.CustomerManage);

        endpoints.MapPost(
                "/price-conditions",
                (
                    CreatePriceConditionRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreatePriceConditionAsync(
                            new CreatePriceConditionCommand(
                                request.Code,
                                request.Name,
                                request.PercentOff,
                                request.AmountOff,
                                request.MaterialId,
                                request.CustomerId,
                                request.ValidFrom,
                                request.ValidTo),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.CustomerManage);

        endpoints.MapGet(
                "/rma",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListRmasAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReturnManage);

        endpoints.MapPost(
                "/rma",
                (
                    CreateRmaRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateRmaAsync(
                            new CreateRmaCommand(
                                request.CustomerId,
                                request.SalesOrderId,
                                request.Reason,
                                request.Lines.Select(line => new CreateRmaLineCommand(
                                    line.MaterialId,
                                    line.Quantity,
                                    line.UnitSnapshot)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.ReturnManage);

        endpoints.MapGet(
                "/credit-summary",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListCreditSummaryAsync(cancellationToken)))
            .RequireAnyPermission(AppPermissions.CustomerManage, AppPermissions.InvoiceConfig);
    }

    private static void MapP2(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/framework-contracts",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListFrameworkContractsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/framework-contracts",
                (
                    CreateFrameworkContractRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateFrameworkContractAsync(
                            new CreateFrameworkContractCommand(
                                request.SupplierId,
                                request.ValidFrom,
                                request.ValidTo,
                                request.Currency,
                                request.Lines.Select(line => new CreateFrameworkContractLineCommand(
                                    line.MaterialId,
                                    line.TargetQty,
                                    line.UnitPrice,
                                    line.UnitSnapshot)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapGet(
                "/rfq",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListRfqsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/rfq",
                (
                    CreateRfqRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateRfqAsync(
                            new CreateRfqCommand(
                                request.Title,
                                request.DueDate,
                                request.Lines.Select(line => new CreateRfqLineCommand(
                                    line.MaterialId,
                                    line.Quantity,
                                    line.UnitSnapshot)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapGet(
                "/three-way-match",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListThreeWayMatchesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/three-way-match",
                (
                    CreateThreeWayMatchRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateThreeWayMatchAsync(
                            new CreateThreeWayMatchCommand(
                                request.PurchaseOrderId,
                                request.GoodsReceiptId,
                                request.SupplierInvoiceId,
                                request.PoAmount,
                                request.GrAmount,
                                request.InvoiceAmount,
                                request.Notes),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapGet(
                "/material-valuations",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListMaterialValuationsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapPost(
                "/material-valuations",
                (
                    UpsertMaterialValuationRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.UpsertMaterialValuationAsync(
                            new UpsertMaterialValuationCommand(
                                request.MaterialId,
                                request.WarehouseId,
                                (StockValuationMethod)request.Method,
                                request.StandardPrice,
                                request.MovingAveragePrice,
                                request.Currency),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/supplier-invoices",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListSupplierInvoiceStubsAsync(cancellationToken)))
            .RequireAnyPermission(AppPermissions.PurchaseOrderCreate, AppPermissions.InvoiceConfig);

        endpoints.MapPost(
                "/supplier-invoices",
                (
                    CreateSupplierInvoiceStubRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateSupplierInvoiceStubAsync(
                            new CreateSupplierInvoiceStubCommand(
                                request.SupplierId,
                                request.PurchaseOrderId,
                                request.Amount,
                                request.Currency,
                                request.InvoiceDate),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/supplier-invoices/{invoiceId:guid}/journal",
                (
                    Guid invoiceId,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.PostJournalFromSupplierInvoiceAsync(invoiceId, cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceConfig);
    }

    private static void MapP3(RouteGroupBuilder endpoints)
    {
        endpoints.MapPost(
                "/gl-accounts/seed-spain",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.SeedSpainChartOfAccountsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/gl-accounts",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListGlAccountsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapGet(
                "/cost-centers",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListCostCentersAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapPost(
                "/cost-centers",
                (
                    CreateCostCenterRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateCostCenterAsync(
                            new CreateCostCenterCommand(
                                request.Code,
                                request.Name,
                                request.CompanyId),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/journals",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListJournalEntriesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapPost(
                "/journals",
                (
                    PostJournalEntryRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.PostJournalEntryAsync(
                            new PostJournalEntryCommand(
                                request.Source,
                                request.Description,
                                request.PostingDate,
                                request.Lines.Select(line => new PostJournalLineCommand(
                                    line.GlAccountId,
                                    line.CostCenterId,
                                    line.Debit,
                                    line.Credit)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.InvoiceConfig);

        endpoints.MapGet(
                "/journals/reconciliation",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.GetJournalReconciliationSummaryAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);
    }

    private static void MapP4(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/boms",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListBomsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/boms",
                (
                    CreateBomRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateBomAsync(
                            new CreateBomCommand(
                                request.Code,
                                request.ParentMaterialId,
                                request.Description,
                                request.Components.Select(line => new CreateBomComponentCommand(
                                    line.ComponentMaterialId,
                                    line.Quantity,
                                    line.UnitSnapshot)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/routes",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListProductionRoutesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/routes",
                (
                    CreateProductionRouteRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateProductionRouteAsync(
                            new CreateProductionRouteCommand(
                                request.Code,
                                request.MaterialId,
                                request.Name,
                                request.Operations.Select(line => new CreateRouteOperationCommand(
                                    line.Sequence,
                                    line.WorkCenter,
                                    line.SetupMinutes,
                                    line.RunMinutes)).ToArray()),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/production-orders",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListProductionOrdersAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/production-orders",
                (
                    CreateProductionOrderRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateProductionOrderAsync(
                            new CreateProductionOrderCommand(
                                request.MaterialId,
                                request.BomId,
                                request.RouteId,
                                request.Quantity,
                                request.UnitSnapshot),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/production-orders/{orderId:guid}/release",
                (
                    Guid orderId,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ReleaseProductionOrderAsync(orderId, cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/production-orders/{orderId:guid}/complete",
                (
                    Guid orderId,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.CompleteProductionOrderAsync(orderId, cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/mrp/generate",
                (
                    StudioGenerateMrpRequestDto? request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.TriggerMrpAsync(request?.WarehouseId, cancellationToken)))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);
    }

    private static void MapP5(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/inspection-plans",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListInspectionPlansAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/inspection-plans",
                (
                    CreateInspectionPlanRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateInspectionPlanAsync(
                            new CreateInspectionPlanCommand(
                                request.Code,
                                request.MaterialId,
                                request.Name),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/quality-holds",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListQualityHoldsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapPost(
                "/quality-holds",
                (
                    CreateQualityHoldRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateQualityHoldAsync(
                            new CreateQualityHoldCommand(
                                request.LotId,
                                request.MaterialId,
                                request.Reason),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.StockAdjust);

        endpoints.MapPost(
                "/quality-holds/{holdId:guid}/release",
                (
                    Guid holdId,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ReleaseQualityHoldAsync(holdId, cancellationToken)))
            .RequireAuthorization(AppPermissions.StockAdjust)
            .RequireStepUp();

        endpoints.MapPost(
                "/quality-holds/{holdId:guid}/reject",
                (
                    Guid holdId,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.RejectQualityHoldAsync(holdId, cancellationToken)))
            .RequireAuthorization(AppPermissions.StockAdjust)
            .RequireStepUp();

        endpoints.MapGet(
                "/quality-certificates",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListQualityCertificatesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.StockRead);

        endpoints.MapPost(
                "/quality-certificates",
                (
                    CreateQualityCertificateRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateQualityCertificateAsync(
                            new CreateQualityCertificateCommand(
                                request.MaterialId,
                                request.LotId,
                                request.Result),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.StockAdjust);
    }

    private static void MapP6(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/plants",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListPlantsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.WarehouseManage);

        endpoints.MapPost(
                "/plants",
                (
                    CreatePlantRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreatePlantAsync(
                            new CreatePlantCommand(
                                request.CompanyId,
                                request.Code,
                                request.Name),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.WarehouseManage);

        endpoints.MapGet(
                "/plant-warehouses",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListPlantWarehousesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.WarehouseManage);

        endpoints.MapPost(
                "/plant-warehouses",
                (
                    LinkPlantWarehouseRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.LinkPlantWarehouseAsync(
                            new LinkPlantWarehouseCommand(request.PlantId, request.WarehouseId),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.WarehouseManage);

        endpoints.MapGet(
                "/user-org-context",
                (
                    Guid userId,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.GetUserOrgContextAsync(userId, cancellationToken)))
            .RequireAuthorization(AppPermissions.UserRead);

        endpoints.MapPut(
                "/user-org-context",
                (
                    SetUserOrgContextRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.SetUserOrgContextAsync(
                        new SetUserOrgContextCommand(
                            request.UserId,
                            request.CompanyId,
                            request.PlantId),
                        cancellationToken)))
            .RequireAuthorization(AppPermissions.UserManageRanges)
            .RequireStepUp();
    }

    private static void MapP7(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/approval-rules",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListApprovalRulesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.UserRead);

        endpoints.MapPost(
                "/approval-rules",
                (
                    CreateApprovalRuleRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateApprovalRuleAsync(
                            new CreateApprovalRuleCommand(
                                request.DocumentKind,
                                request.MinAmount,
                                request.RequiredRole,
                                request.Name),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.UserManagePermissions);

        endpoints.MapGet(
                "/approval-requests",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListApprovalRequestsAsync(cancellationToken)))
            .RequireAnyPermission(
                AppPermissions.UserRead,
                AppPermissions.UserManagePermissions,
                AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/approval-requests",
                (
                    CreateApprovalRequestRequestDto request,
                    ClaimsPrincipal principal,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateApprovalRequestAsync(
                            new CreateApprovalRequestCommand(
                                request.DocumentKind,
                                request.DocumentId,
                                request.Amount,
                                request.Summary,
                                CurrentUserId(principal)),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.PurchaseOrderCreate);

        endpoints.MapPost(
                "/approval-requests/{requestId:guid}/approve",
                (
                    Guid requestId,
                    ClaimsPrincipal principal,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ApproveRequestAsync(
                        requestId,
                        Decision(principal),
                        cancellationToken)))
            .RequireAuthorization(AppPermissions.UserManagePermissions)
            .RequireStepUp();

        endpoints.MapPost(
                "/approval-requests/{requestId:guid}/reject",
                (
                    Guid requestId,
                    ClaimsPrincipal principal,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.RejectRequestAsync(
                        requestId,
                        Decision(principal),
                        cancellationToken)))
            .RequireAuthorization(AppPermissions.UserManagePermissions)
            .RequireStepUp();
    }

    private static Guid CurrentUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtClaimNames.Subject), out var userId)
            && userId != Guid.Empty
            ? userId
            : throw new LogisticsException(
                LogisticsErrorKind.Forbidden,
                "AAA-AUTH-0003",
                "El token no contiene una identidad válida.");

    private static DecideApprovalCommand Decision(ClaimsPrincipal principal) =>
        new(
            CurrentUserId(principal),
            principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray());

    private static void MapP8(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/integration-connectors",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListIntegrationConnectorsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/integration-connectors",
                (
                    CreateIntegrationConnectorRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateIntegrationConnectorAsync(
                            new CreateIntegrationConnectorCommand(
                                request.Code,
                                request.Name,
                                request.Kind,
                                request.Version),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/integration-inbox",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListIntegrationInboxAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/integration-inbox",
                (
                    ReceiveIntegrationInboxRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.ReceiveIntegrationInboxAsync(
                            new ReceiveIntegrationInboxCommand(
                                request.ConnectorId,
                                request.MessageType,
                                request.Payload),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapGet(
                "/integration-outbox",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListIntegrationOutboxAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.DeliveryPost);

        endpoints.MapGet(
                "/integration-mappings",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(async () =>
                    {
                        var rows = await service.ListIntegrationMappingsAsync(cancellationToken);
                        return rows.Select(value => new StudioIntegrationMappingResponse(
                            value.Id,
                            value.SystemCode,
                            value.EntityType,
                            value.ExternalKey,
                            value.InternalId,
                            value.MappedUtc)).ToArray();
                    }))
            .RequireAuthorization(AppPermissions.MaterialManage);

        endpoints.MapPost(
                "/integration-mappings",
                (
                    UpsertStudioIntegrationMappingRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        async () =>
                        {
                            var row = await service.UpsertIntegrationMappingAsync(
                                request.SystemCode,
                                request.EntityType,
                                request.ExternalKey,
                                request.InternalId,
                                cancellationToken);
                            return new StudioIntegrationMappingResponse(
                                row.Id,
                                row.SystemCode,
                                row.EntityType,
                                row.ExternalKey,
                                row.InternalId,
                                row.MappedUtc);
                        },
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.MaterialManage);
    }

    private static void MapP9(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/reports/valued-stock",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.GetValuedStockReportAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapGet(
                "/reports/aging",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.GetAgingReportAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapGet(
                "/reports/otif",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.GetOtifReportPassthroughAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);
    }

    private static void MapP10(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/employees",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListEmployeeProfilesAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.UserRead);

        endpoints.MapPost(
                "/employees",
                (
                    CreateEmployeeProfileRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateEmployeeProfileAsync(
                            new CreateEmployeeProfileCommand(
                                request.UserId,
                                request.EmployeeNumber,
                                request.DisplayName,
                                request.Department,
                                request.HireDate),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.UserCreate);

        endpoints.MapPut(
                "/employees/{employeeId:guid}",
                (
                    Guid employeeId,
                    UpdateEmployeeProfileRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.UpdateEmployeeProfileAsync(
                        employeeId,
                        new UpdateEmployeeProfileCommand(
                            request.DisplayName,
                            request.Department,
                            request.HireDate,
                            request.IsActive),
                        cancellationToken)))
            .RequireAuthorization(AppPermissions.UserManagePermissions);
    }

    private static void MapP11(RouteGroupBuilder endpoints)
    {
        endpoints.MapGet(
                "/domain-events",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListDomainEventsAsync(cancellationToken)))
            .RequireAuthorization(AppPermissions.ReportRead);

        endpoints.MapPost(
                "/domain-events",
                (
                    AppendDomainEventRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.AppendDomainEventAsync(
                            new AppendDomainEventCommand(
                                request.EventName,
                                request.AggregateType,
                                request.AggregateId,
                                request.Payload),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.AdminTableEdit);

        endpoints.MapGet(
                "/plugins",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListPluginsAsync(cancellationToken)))
            .RequireAnyPermission(AppPermissions.MaterialManage, AppPermissions.AdminTableEdit);

        endpoints.MapPost(
                "/plugins",
                (
                    RegisterPluginRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.RegisterPluginAsync(
                            new RegisterPluginCommand(
                                request.Code,
                                request.Name,
                                request.HookPoint,
                                request.Description),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.AdminTableEdit);

        endpoints.MapPost(
                "/plugins/{pluginId:guid}/invoke",
                (
                    Guid pluginId,
                    InvokePluginRequestDto? request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(async () =>
                    {
                        var result = await service.InvokePluginAsync(
                            pluginId,
                            request?.ParametersJson ?? "{}",
                            cancellationToken);
                        return new InvokePluginResponse(
                            result.PluginId,
                            result.Code,
                            result.HookPoint,
                            result.Outcome,
                            result.DomainEventId,
                            result.ResultPayload);
                    }))
            .RequireAuthorization(AppPermissions.AdminTableEdit);

        endpoints.MapGet(
                "/config-transport",
                (IStudioService service, CancellationToken cancellationToken) =>
                    ExecuteAsync(() => service.ListConfigTransportsAsync(cancellationToken)))
            .RequireAnyPermission(AppPermissions.MaterialManage, AppPermissions.AdminTableEdit);

        endpoints.MapPost(
                "/config-transport",
                (
                    CreateConfigTransportRequestDto request,
                    IStudioService service,
                    CancellationToken cancellationToken) =>
                    ExecuteAsync(
                        () => service.CreateConfigTransportAsync(
                            new CreateConfigTransportCommand(
                                request.Code,
                                request.Environment,
                                request.PayloadJson),
                            cancellationToken),
                        StatusCodes.Status201Created))
            .RequireAuthorization(AppPermissions.AdminTableEdit);
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
            return ApiProblems.Logistics(exception);
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