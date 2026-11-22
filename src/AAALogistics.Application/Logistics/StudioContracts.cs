using AAALogistics.Domain.Studio;

namespace AAALogistics.Application.Logistics;

// ——— P1 Pricing / IVA / RMA ———

public sealed record CreateTaxCodeCommand(
    string Code,
    string Name,
    decimal RatePercent,
    string CountryCode);

public sealed record TaxCodeView(
    Guid Id,
    string Code,
    string Name,
    decimal RatePercent,
    string CountryCode,
    bool IsActive);

public sealed record CreatePriceConditionCommand(
    string Code,
    string Name,
    decimal PercentOff,
    decimal AmountOff,
    Guid? MaterialId,
    Guid? CustomerId,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record PriceConditionView(
    Guid Id,
    string Code,
    string Name,
    decimal PercentOff,
    decimal AmountOff,
    Guid? MaterialId,
    Guid? CustomerId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive);

public sealed record CreateRmaCommand(
    Guid CustomerId,
    Guid? SalesOrderId,
    string Reason,
    IReadOnlyCollection<CreateRmaLineCommand> Lines);

public sealed record CreateRmaLineCommand(
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record RmaView(
    Guid Id,
    string Number,
    Guid CustomerId,
    Guid? SalesOrderId,
    string Reason,
    string Status,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<RmaLineView> Lines);

public sealed record RmaLineView(
    Guid Id,
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record CreditSummaryRow(
    Guid CustomerId,
    Guid CompanyId,
    decimal CreditLimit,
    decimal OpenReceivables,
    string Currency,
    bool IsCreditBlocked);

// ——— P2 MM ———

public sealed record CreateFrameworkContractCommand(
    Guid SupplierId,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Currency,
    IReadOnlyCollection<CreateFrameworkContractLineCommand> Lines);

public sealed record CreateFrameworkContractLineCommand(
    Guid MaterialId,
    decimal TargetQty,
    decimal UnitPrice,
    string UnitSnapshot);

public sealed record FrameworkContractView(
    Guid Id,
    string Number,
    Guid SupplierId,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Currency,
    string Status,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<FrameworkContractLineView> Lines);

public sealed record FrameworkContractLineView(
    Guid Id,
    Guid MaterialId,
    decimal TargetQty,
    decimal UnitPrice,
    string UnitSnapshot);

public sealed record CreateRfqCommand(
    string Title,
    DateOnly? DueDate,
    IReadOnlyCollection<CreateRfqLineCommand> Lines);

public sealed record CreateRfqLineCommand(
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record RfqView(
    Guid Id,
    string Number,
    string Title,
    string Status,
    DateTimeOffset CreatedUtc,
    DateOnly? DueDate,
    IReadOnlyCollection<RfqLineView> Lines);

public sealed record RfqLineView(
    Guid Id,
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record CreateThreeWayMatchCommand(
    Guid PurchaseOrderId,
    Guid? GoodsReceiptId,
    Guid? SupplierInvoiceId,
    decimal PoAmount,
    decimal GrAmount,
    decimal InvoiceAmount,
    string? Notes);

public sealed record ThreeWayMatchView(
    Guid Id,
    Guid PurchaseOrderId,
    Guid? GoodsReceiptId,
    Guid? SupplierInvoiceId,
    decimal PoAmount,
    decimal GrAmount,
    decimal InvoiceAmount,
    bool Matched,
    string Notes,
    DateTimeOffset CreatedUtc);

public sealed record UpsertMaterialValuationCommand(
    Guid MaterialId,
    Guid? WarehouseId,
    StockValuationMethod Method,
    decimal StandardPrice,
    decimal MovingAveragePrice,
    string Currency);

public sealed record MaterialValuationView(
    Guid Id,
    Guid MaterialId,
    Guid? WarehouseId,
    string Method,
    decimal StandardPrice,
    decimal MovingAveragePrice,
    decimal UnitValue,
    string Currency);

public sealed record CreateSupplierInvoiceStubCommand(
    Guid SupplierId,
    Guid? PurchaseOrderId,
    decimal Amount,
    string Currency,
    DateOnly InvoiceDate);

public sealed record SupplierInvoiceStubView(
    Guid Id,
    string Number,
    Guid SupplierId,
    Guid? PurchaseOrderId,
    decimal Amount,
    string Currency,
    DateOnly InvoiceDate);

// ——— P3 FI/CO ———

public sealed record GlAccountView(
    Guid Id,
    string Code,
    string Name,
    string AccountType,
    bool IsActive);

public sealed record CreateCostCenterCommand(
    string Code,
    string Name,
    Guid? CompanyId);

public sealed record CostCenterView(
    Guid Id,
    string Code,
    string Name,
    Guid? CompanyId,
    bool IsActive);

public sealed record PostJournalEntryCommand(
    string Source,
    string Description,
    DateOnly PostingDate,
    IReadOnlyCollection<PostJournalLineCommand> Lines);

public sealed record PostJournalLineCommand(
    Guid GlAccountId,
    Guid? CostCenterId,
    decimal Debit,
    decimal Credit);

public sealed record JournalEntryView(
    Guid Id,
    string Number,
    string Source,
    string Description,
    DateOnly PostingDate,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<JournalLineView> Lines);

public sealed record JournalLineView(
    Guid Id,
    Guid GlAccountId,
    Guid? CostCenterId,
    decimal Debit,
    decimal Credit);

public sealed record JournalReconciliationSummary(
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    int EntryCount,
    bool IsBalanced);

// ——— P4 PP ———

public sealed record CreateBomCommand(
    string Code,
    Guid ParentMaterialId,
    string Description,
    IReadOnlyCollection<CreateBomComponentCommand> Components);

public sealed record CreateBomComponentCommand(
    Guid ComponentMaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record BomView(
    Guid Id,
    string Code,
    Guid ParentMaterialId,
    string Description,
    bool IsActive,
    IReadOnlyCollection<BomComponentView> Components);

public sealed record BomComponentView(
    Guid Id,
    Guid ComponentMaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record CreateProductionRouteCommand(
    string Code,
    Guid MaterialId,
    string Name,
    IReadOnlyCollection<CreateRouteOperationCommand> Operations);

public sealed record CreateRouteOperationCommand(
    int Sequence,
    string WorkCenter,
    decimal SetupMinutes,
    decimal RunMinutes);

public sealed record ProductionRouteView(
    Guid Id,
    string Code,
    Guid MaterialId,
    string Name,
    bool IsActive,
    IReadOnlyCollection<RouteOperationView> Operations);

public sealed record RouteOperationView(
    Guid Id,
    int Sequence,
    string WorkCenter,
    decimal SetupMinutes,
    decimal RunMinutes);

public sealed record CreateProductionOrderCommand(
    Guid MaterialId,
    Guid? BomId,
    Guid? RouteId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record ProductionOrderView(
    Guid Id,
    string Number,
    Guid MaterialId,
    Guid? BomId,
    Guid? RouteId,
    decimal Quantity,
    string UnitSnapshot,
    string Status,
    DateTimeOffset CreatedUtc);

public sealed record MrpTriggerResult(
    string Mode,
    string? Note,
    IReadOnlyCollection<Guid>? RequisitionIds);

// ——— P5 QM ———

public sealed record CreateInspectionPlanCommand(
    string Code,
    Guid MaterialId,
    string Name);

public sealed record InspectionPlanView(
    Guid Id,
    string Code,
    Guid MaterialId,
    string Name,
    bool IsActive);

public sealed record CreateQualityHoldCommand(
    Guid LotId,
    Guid? MaterialId,
    string Reason);

public sealed record QualityHoldView(
    Guid Id,
    Guid LotId,
    string LotCode,
    Guid? MaterialId,
    string Reason,
    string Status,
    DateTimeOffset CreatedUtc,
    int StockBalancesUpdated = 0);

public sealed record CreateQualityCertificateCommand(
    Guid MaterialId,
    Guid? LotId,
    string Result);

public sealed record QualityCertificateView(
    Guid Id,
    string Number,
    Guid MaterialId,
    Guid? LotId,
    string Result,
    DateTimeOffset IssuedUtc);

// ——— P6 Org ———

public sealed record CreatePlantCommand(
    Guid CompanyId,
    string Code,
    string Name);

public sealed record PlantView(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    bool IsActive);

public sealed record LinkPlantWarehouseCommand(
    Guid PlantId,
    Guid WarehouseId);

public sealed record PlantWarehouseView(
    Guid Id,
    Guid PlantId,
    Guid WarehouseId);

public sealed record SetUserOrgContextCommand(
    Guid UserId,
    Guid? CompanyId,
    Guid? PlantId);

public sealed record UserOrgContextView(
    Guid Id,
    Guid UserId,
    Guid? CompanyId,
    Guid? PlantId);

// ——— P7 Workflow ———

public sealed record CreateApprovalRuleCommand(
    string DocumentKind,
    decimal MinAmount,
    string RequiredRole,
    string Name);

public sealed record ApprovalRuleView(
    Guid Id,
    string DocumentKind,
    decimal MinAmount,
    string RequiredRole,
    string Name,
    bool IsActive);

public sealed record CreateApprovalRequestCommand(
    string DocumentKind,
    Guid DocumentId,
    decimal Amount,
    string Summary,
    Guid? RequestedByUserId = null);

public sealed record ApprovalRequestView(
    Guid Id,
    string DocumentKind,
    Guid DocumentId,
    decimal Amount,
    string Summary,
    string Status,
    DateTimeOffset CreatedUtc,
    Guid? DecidedByUserId,
    DateTimeOffset? DecidedUtc,
    Guid? RequestedByUserId = null,
    string? RequiredRole = null);

/// <summary>Decision by the authenticated user; <paramref name="Roles"/> are the caller's rangos.</summary>
public sealed record DecideApprovalCommand(Guid UserId, IReadOnlyCollection<string> Roles);

public static class ApprovalDocumentKinds
{
    public const string PurchaseOrder = "PURCHASEORDER";
    public const string SalesOrder = "SALESORDER";
    public const string SupplierInvoice = "SUPPLIERINVOICE";

    public static IReadOnlyList<string> All { get; } = [PurchaseOrder, SalesOrder, SupplierInvoice];
}

// ——— P8 Integration ———

public sealed record CreateIntegrationConnectorCommand(
    string Code,
    string Name,
    string Kind,
    string Version);

public sealed record IntegrationConnectorView(
    Guid Id,
    string Code,
    string Name,
    string Kind,
    string Version,
    bool IsActive);

public sealed record ReceiveIntegrationInboxCommand(
    Guid? ConnectorId,
    string MessageType,
    string Payload);

public sealed record IntegrationInboxView(
    Guid Id,
    Guid? ConnectorId,
    string MessageType,
    bool Processed,
    DateTimeOffset ReceivedUtc);

public sealed record IntegrationOutboxView(
    Guid Id,
    string MessageType,
    string IdempotencyKey,
    string Status,
    int AttemptCount,
    DateTimeOffset CreatedUtc);

// ——— P9 Reports ———

public sealed record ValuedStockRow(
    Guid MaterialId,
    Guid WarehouseId,
    decimal Quantity,
    decimal UnitValue,
    decimal TotalValue,
    string Currency);

public sealed record AgingRow(
    Guid CustomerId,
    string CustomerName,
    decimal OpenAmount,
    int BucketDays,
    string Currency);

// ——— P10 HR ———

public sealed record CreateEmployeeProfileCommand(
    Guid UserId,
    string EmployeeNumber,
    string DisplayName,
    string Department,
    DateOnly? HireDate);

public sealed record UpdateEmployeeProfileCommand(
    string DisplayName,
    string Department,
    DateOnly? HireDate,
    bool IsActive);

public sealed record EmployeeProfileView(
    Guid Id,
    Guid UserId,
    string EmployeeNumber,
    string DisplayName,
    string Department,
    DateOnly? HireDate,
    bool IsActive);

// ——— P11 Extensibility ———

public sealed record AppendDomainEventCommand(
    string EventName,
    string AggregateType,
    Guid AggregateId,
    string Payload);

public sealed record DomainEventLogView(
    Guid Id,
    string EventName,
    string AggregateType,
    Guid AggregateId,
    DateTimeOffset OccurredUtc);

public sealed record RegisterPluginCommand(
    string Code,
    string Name,
    string HookPoint,
    string Description);

public sealed record PluginRegistrationView(
    Guid Id,
    string Code,
    string Name,
    string HookPoint,
    bool IsEnabled);

public sealed record PluginInvokeView(
    Guid PluginId,
    string Code,
    string HookPoint,
    string Outcome,
    Guid DomainEventId,
    string ResultPayload);

public sealed record CreateConfigTransportCommand(
    string Code,
    string Environment,
    string PayloadJson);

public sealed record ConfigTransportView(
    Guid Id,
    string Code,
    string Environment,
    DateTimeOffset CreatedUtc);

public interface IStudioService
{
    // P1
    Task<TaxCodeView> CreateTaxCodeAsync(
        CreateTaxCodeCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TaxCodeView>> ListTaxCodesAsync(
        CancellationToken cancellationToken = default);

    Task<PriceConditionView> CreatePriceConditionAsync(
        CreatePriceConditionCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PriceConditionView>> ListPriceConditionsAsync(
        CancellationToken cancellationToken = default);

    Task<RmaView> CreateRmaAsync(
        CreateRmaCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RmaView>> ListRmasAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CreditSummaryRow>> ListCreditSummaryAsync(
        CancellationToken cancellationToken = default);

    // P2
    Task<FrameworkContractView> CreateFrameworkContractAsync(
        CreateFrameworkContractCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FrameworkContractView>> ListFrameworkContractsAsync(
        CancellationToken cancellationToken = default);

    Task<RfqView> CreateRfqAsync(
        CreateRfqCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RfqView>> ListRfqsAsync(
        CancellationToken cancellationToken = default);

    Task<ThreeWayMatchView> CreateThreeWayMatchAsync(
        CreateThreeWayMatchCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ThreeWayMatchView>> ListThreeWayMatchesAsync(
        CancellationToken cancellationToken = default);

    Task<MaterialValuationView> UpsertMaterialValuationAsync(
        UpsertMaterialValuationCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MaterialValuationView>> ListMaterialValuationsAsync(
        CancellationToken cancellationToken = default);

    Task<SupplierInvoiceStubView> CreateSupplierInvoiceStubAsync(
        CreateSupplierInvoiceStubCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SupplierInvoiceStubView>> ListSupplierInvoiceStubsAsync(
        CancellationToken cancellationToken = default);

    Task<JournalEntryView> PostJournalFromSupplierInvoiceAsync(
        Guid supplierInvoiceId,
        CancellationToken cancellationToken = default);

    // P3
    Task<int> SeedSpainChartOfAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GlAccountView>> ListGlAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<CostCenterView> CreateCostCenterAsync(
        CreateCostCenterCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CostCenterView>> ListCostCentersAsync(
        CancellationToken cancellationToken = default);

    Task<JournalEntryView> PostJournalEntryAsync(
        PostJournalEntryCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<JournalEntryView>> ListJournalEntriesAsync(
        CancellationToken cancellationToken = default);

    Task<JournalReconciliationSummary> GetJournalReconciliationSummaryAsync(
        CancellationToken cancellationToken = default);

    // P4
    Task<BomView> CreateBomAsync(
        CreateBomCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BomView>> ListBomsAsync(
        CancellationToken cancellationToken = default);

    Task<ProductionRouteView> CreateProductionRouteAsync(
        CreateProductionRouteCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProductionRouteView>> ListProductionRoutesAsync(
        CancellationToken cancellationToken = default);

    Task<ProductionOrderView> CreateProductionOrderAsync(
        CreateProductionOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProductionOrderView>> ListProductionOrdersAsync(
        CancellationToken cancellationToken = default);

    Task<ProductionOrderView> ReleaseProductionOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<ProductionOrderView> CompleteProductionOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<MrpTriggerResult> TriggerMrpAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    // P5
    Task<InspectionPlanView> CreateInspectionPlanAsync(
        CreateInspectionPlanCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InspectionPlanView>> ListInspectionPlansAsync(
        CancellationToken cancellationToken = default);

    Task<QualityHoldView> CreateQualityHoldAsync(
        CreateQualityHoldCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<QualityHoldView>> ListQualityHoldsAsync(
        CancellationToken cancellationToken = default);

    Task<QualityHoldView> ReleaseQualityHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default);

    Task<QualityHoldView> RejectQualityHoldAsync(
        Guid holdId,
        CancellationToken cancellationToken = default);

    Task<QualityCertificateView> CreateQualityCertificateAsync(
        CreateQualityCertificateCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<QualityCertificateView>> ListQualityCertificatesAsync(
        CancellationToken cancellationToken = default);

    // P6
    Task<PlantView> CreatePlantAsync(
        CreatePlantCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlantView>> ListPlantsAsync(
        CancellationToken cancellationToken = default);

    Task<PlantWarehouseView> LinkPlantWarehouseAsync(
        LinkPlantWarehouseCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PlantWarehouseView>> ListPlantWarehousesAsync(
        CancellationToken cancellationToken = default);

    Task<UserOrgContextView> GetUserOrgContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserOrgContextView> SetUserOrgContextAsync(
        SetUserOrgContextCommand command,
        CancellationToken cancellationToken = default);

    // P7
    Task<ApprovalRuleView> CreateApprovalRuleAsync(
        CreateApprovalRuleCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ApprovalRuleView>> ListApprovalRulesAsync(
        CancellationToken cancellationToken = default);

    Task<ApprovalRequestView> CreateApprovalRequestAsync(
        CreateApprovalRequestCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ApprovalRequestView>> ListApprovalRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<ApprovalRequestView> ApproveRequestAsync(
        Guid requestId,
        DecideApprovalCommand command,
        CancellationToken cancellationToken = default);

    Task<ApprovalRequestView> RejectRequestAsync(
        Guid requestId,
        DecideApprovalCommand command,
        CancellationToken cancellationToken = default);

    // P8
    Task<IntegrationConnectorView> CreateIntegrationConnectorAsync(
        CreateIntegrationConnectorCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IntegrationConnectorView>> ListIntegrationConnectorsAsync(
        CancellationToken cancellationToken = default);

    Task<IntegrationInboxView> ReceiveIntegrationInboxAsync(
        ReceiveIntegrationInboxCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IntegrationInboxView>> ListIntegrationInboxAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IntegrationOutboxView>> ListIntegrationOutboxAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SapFieldMappingView>> ListIntegrationMappingsAsync(
        CancellationToken cancellationToken = default);

    Task<SapFieldMappingView> UpsertIntegrationMappingAsync(
        string systemCode,
        string entityType,
        string externalKey,
        Guid internalId,
        CancellationToken cancellationToken = default);

    // P9
    Task<IReadOnlyCollection<ValuedStockRow>> GetValuedStockReportAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AgingRow>> GetAgingReportAsync(
        CancellationToken cancellationToken = default);

    Task<OtifReportRow?> GetOtifReportPassthroughAsync(
        CancellationToken cancellationToken = default);

    // P10
    Task<EmployeeProfileView> CreateEmployeeProfileAsync(
        CreateEmployeeProfileCommand command,
        CancellationToken cancellationToken = default);

    Task<EmployeeProfileView> UpdateEmployeeProfileAsync(
        Guid employeeId,
        UpdateEmployeeProfileCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EmployeeProfileView>> ListEmployeeProfilesAsync(
        CancellationToken cancellationToken = default);

    // P11
    Task<DomainEventLogView> AppendDomainEventAsync(
        AppendDomainEventCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DomainEventLogView>> ListDomainEventsAsync(
        CancellationToken cancellationToken = default);

    Task<PluginRegistrationView> RegisterPluginAsync(
        RegisterPluginCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PluginRegistrationView>> ListPluginsAsync(
        CancellationToken cancellationToken = default);

    Task<PluginInvokeView> InvokePluginAsync(
        Guid pluginId,
        string parametersJson,
        CancellationToken cancellationToken = default);

    Task<ConfigTransportView> CreateConfigTransportAsync(
        CreateConfigTransportCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ConfigTransportView>> ListConfigTransportsAsync(
        CancellationToken cancellationToken = default);
}