namespace AAALogistics.Contracts;

public sealed record CreateTaxCodeRequestDto(
    string Code,
    string Name,
    decimal RatePercent,
    string CountryCode);

public sealed record TaxCodeResponse(
    Guid Id,
    string Code,
    string Name,
    decimal RatePercent,
    string CountryCode,
    bool IsActive);

public sealed record CreatePriceConditionRequestDto(
    string Code,
    string Name,
    decimal PercentOff,
    decimal AmountOff,
    Guid? MaterialId,
    Guid? CustomerId,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record PriceConditionResponse(
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

public sealed record CreateRmaLineRequestDto(
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record CreateRmaRequestDto(
    Guid CustomerId,
    Guid? SalesOrderId,
    string Reason,
    IReadOnlyCollection<CreateRmaLineRequestDto> Lines);

public sealed record RmaLineResponse(
    Guid Id,
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record RmaResponse(
    Guid Id,
    string Number,
    Guid CustomerId,
    Guid? SalesOrderId,
    string Reason,
    string Status,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<RmaLineResponse> Lines);

public sealed record CreditSummaryResponse(
    Guid CustomerId,
    Guid CompanyId,
    decimal CreditLimit,
    decimal OpenReceivables,
    string Currency,
    bool IsCreditBlocked);

public sealed record CreateFrameworkContractLineRequestDto(
    Guid MaterialId,
    decimal TargetQty,
    decimal UnitPrice,
    string UnitSnapshot);

public sealed record CreateFrameworkContractRequestDto(
    Guid SupplierId,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Currency,
    IReadOnlyCollection<CreateFrameworkContractLineRequestDto> Lines);

public sealed record FrameworkContractLineResponse(
    Guid Id,
    Guid MaterialId,
    decimal TargetQty,
    decimal UnitPrice,
    string UnitSnapshot);

public sealed record FrameworkContractResponse(
    Guid Id,
    string Number,
    Guid SupplierId,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Currency,
    string Status,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<FrameworkContractLineResponse> Lines);

public sealed record CreateRfqLineRequestDto(
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record CreateRfqRequestDto(
    string Title,
    DateOnly? DueDate,
    IReadOnlyCollection<CreateRfqLineRequestDto> Lines);

public sealed record RfqLineResponse(
    Guid Id,
    Guid MaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record RfqResponse(
    Guid Id,
    string Number,
    string Title,
    string Status,
    DateTimeOffset CreatedUtc,
    DateOnly? DueDate,
    IReadOnlyCollection<RfqLineResponse> Lines);

public sealed record CreateThreeWayMatchRequestDto(
    Guid PurchaseOrderId,
    Guid? GoodsReceiptId,
    Guid? SupplierInvoiceId,
    decimal PoAmount,
    decimal GrAmount,
    decimal InvoiceAmount,
    string? Notes);

public sealed record ThreeWayMatchResponse(
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

public sealed record UpsertMaterialValuationRequestDto(
    Guid MaterialId,
    Guid? WarehouseId,
    int Method,
    decimal StandardPrice,
    decimal MovingAveragePrice,
    string Currency);

public sealed record MaterialValuationResponse(
    Guid Id,
    Guid MaterialId,
    Guid? WarehouseId,
    string Method,
    decimal StandardPrice,
    decimal MovingAveragePrice,
    decimal UnitValue,
    string Currency);

public sealed record CreateSupplierInvoiceStubRequestDto(
    Guid SupplierId,
    Guid? PurchaseOrderId,
    decimal Amount,
    string Currency,
    DateOnly InvoiceDate);

public sealed record SupplierInvoiceStubResponse(
    Guid Id,
    string Number,
    Guid SupplierId,
    Guid? PurchaseOrderId,
    decimal Amount,
    string Currency,
    DateOnly InvoiceDate);

public sealed record GlAccountResponse(
    Guid Id,
    string Code,
    string Name,
    string AccountType,
    bool IsActive);

public sealed record CreateCostCenterRequestDto(
    string Code,
    string Name,
    Guid? CompanyId);

public sealed record CostCenterResponse(
    Guid Id,
    string Code,
    string Name,
    Guid? CompanyId,
    bool IsActive);

public sealed record PostJournalLineRequestDto(
    Guid GlAccountId,
    Guid? CostCenterId,
    decimal Debit,
    decimal Credit);

public sealed record PostJournalEntryRequestDto(
    string Source,
    string Description,
    DateOnly PostingDate,
    IReadOnlyCollection<PostJournalLineRequestDto> Lines);

public sealed record JournalLineResponse(
    Guid Id,
    Guid GlAccountId,
    Guid? CostCenterId,
    decimal Debit,
    decimal Credit);

public sealed record JournalEntryResponse(
    Guid Id,
    string Number,
    string Source,
    string Description,
    DateOnly PostingDate,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<JournalLineResponse> Lines);

public sealed record JournalReconciliationResponse(
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    int EntryCount,
    bool IsBalanced);

public sealed record CreateBomComponentRequestDto(
    Guid ComponentMaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record CreateBomRequestDto(
    string Code,
    Guid ParentMaterialId,
    string Description,
    IReadOnlyCollection<CreateBomComponentRequestDto> Components);

public sealed record BomComponentResponse(
    Guid Id,
    Guid ComponentMaterialId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record BomResponse(
    Guid Id,
    string Code,
    Guid ParentMaterialId,
    string Description,
    bool IsActive,
    IReadOnlyCollection<BomComponentResponse> Components);

public sealed record CreateRouteOperationRequestDto(
    int Sequence,
    string WorkCenter,
    decimal SetupMinutes,
    decimal RunMinutes);

public sealed record CreateProductionRouteRequestDto(
    string Code,
    Guid MaterialId,
    string Name,
    IReadOnlyCollection<CreateRouteOperationRequestDto> Operations);

public sealed record RouteOperationResponse(
    Guid Id,
    int Sequence,
    string WorkCenter,
    decimal SetupMinutes,
    decimal RunMinutes);

public sealed record ProductionRouteResponse(
    Guid Id,
    string Code,
    Guid MaterialId,
    string Name,
    bool IsActive,
    IReadOnlyCollection<RouteOperationResponse> Operations);

public sealed record CreateProductionOrderRequestDto(
    Guid MaterialId,
    Guid? BomId,
    Guid? RouteId,
    decimal Quantity,
    string UnitSnapshot);

public sealed record ProductionOrderResponse(
    Guid Id,
    string Number,
    Guid MaterialId,
    Guid? BomId,
    Guid? RouteId,
    decimal Quantity,
    string UnitSnapshot,
    string Status,
    DateTimeOffset CreatedUtc);

public sealed record StudioGenerateMrpRequestDto(Guid? WarehouseId);

public sealed record MrpTriggerResponse(
    string Mode,
    string? Note,
    IReadOnlyCollection<Guid>? RequisitionIds);

public sealed record CreateInspectionPlanRequestDto(
    string Code,
    Guid MaterialId,
    string Name);

public sealed record InspectionPlanResponse(
    Guid Id,
    string Code,
    Guid MaterialId,
    string Name,
    bool IsActive);

public sealed record CreateQualityHoldRequestDto(
    Guid LotId,
    Guid? MaterialId,
    string Reason);

public sealed record QualityHoldResponse(
    Guid Id,
    Guid LotId,
    string LotCode,
    Guid? MaterialId,
    string Reason,
    string Status,
    DateTimeOffset CreatedUtc,
    int StockBalancesUpdated = 0);

public sealed record CreateQualityCertificateRequestDto(
    Guid MaterialId,
    Guid? LotId,
    string Result);

public sealed record QualityCertificateResponse(
    Guid Id,
    string Number,
    Guid MaterialId,
    Guid? LotId,
    string Result,
    DateTimeOffset IssuedUtc);

public sealed record CreatePlantRequestDto(
    Guid CompanyId,
    string Code,
    string Name);

public sealed record PlantResponse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    bool IsActive);

public sealed record LinkPlantWarehouseRequestDto(
    Guid PlantId,
    Guid WarehouseId);

public sealed record PlantWarehouseResponse(
    Guid Id,
    Guid PlantId,
    Guid WarehouseId);

public sealed record SetUserOrgContextRequestDto(
    Guid UserId,
    Guid? CompanyId,
    Guid? PlantId);

public sealed record UserOrgContextResponse(
    Guid Id,
    Guid UserId,
    Guid? CompanyId,
    Guid? PlantId);

public sealed record CreateApprovalRuleRequestDto(
    string DocumentKind,
    decimal MinAmount,
    string RequiredRole,
    string Name);

public sealed record ApprovalRuleResponse(
    Guid Id,
    string DocumentKind,
    decimal MinAmount,
    string RequiredRole,
    string Name,
    bool IsActive);

public sealed record CreateApprovalRequestRequestDto(
    string DocumentKind,
    Guid DocumentId,
    decimal Amount,
    string Summary);

public sealed record ApprovalRequestResponse(
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

public sealed record CreateIntegrationConnectorRequestDto(
    string Code,
    string Name,
    string Kind,
    string Version);

public sealed record IntegrationConnectorResponse(
    Guid Id,
    string Code,
    string Name,
    string Kind,
    string Version,
    bool IsActive);

public sealed record ReceiveIntegrationInboxRequestDto(
    Guid? ConnectorId,
    string MessageType,
    string Payload);

public sealed record IntegrationInboxResponse(
    Guid Id,
    Guid? ConnectorId,
    string MessageType,
    bool Processed,
    DateTimeOffset ReceivedUtc);

public sealed record IntegrationOutboxResponse(
    Guid Id,
    string MessageType,
    string IdempotencyKey,
    string Status,
    int AttemptCount,
    DateTimeOffset CreatedUtc);

public sealed record UpsertStudioIntegrationMappingRequestDto(
    string SystemCode,
    string EntityType,
    string ExternalKey,
    Guid InternalId);

public sealed record StudioIntegrationMappingResponse(
    Guid Id,
    string SystemCode,
    string EntityType,
    string ExternalKey,
    Guid InternalId,
    DateTimeOffset MappedUtc);

public sealed record ValuedStockReportRow(
    Guid MaterialId,
    Guid WarehouseId,
    decimal Quantity,
    decimal UnitValue,
    decimal TotalValue,
    string Currency);

public sealed record AgingReportRow(
    Guid CustomerId,
    string CustomerName,
    decimal OpenAmount,
    int BucketDays,
    string Currency);

public sealed record CreateEmployeeProfileRequestDto(
    Guid UserId,
    string EmployeeNumber,
    string DisplayName,
    string Department,
    DateOnly? HireDate);

public sealed record UpdateEmployeeProfileRequestDto(
    string DisplayName,
    string Department,
    DateOnly? HireDate,
    bool IsActive);

public sealed record EmployeeProfileResponse(
    Guid Id,
    Guid UserId,
    string EmployeeNumber,
    string DisplayName,
    string Department,
    DateOnly? HireDate,
    bool IsActive);

public sealed record AppendDomainEventRequestDto(
    string EventName,
    string AggregateType,
    Guid AggregateId,
    string Payload);

public sealed record DomainEventLogResponse(
    Guid Id,
    string EventName,
    string AggregateType,
    Guid AggregateId,
    DateTimeOffset OccurredUtc);

public sealed record RegisterPluginRequestDto(
    string Code,
    string Name,
    string HookPoint,
    string Description);

public sealed record PluginRegistrationResponse(
    Guid Id,
    string Code,
    string Name,
    string HookPoint,
    bool IsEnabled);

public sealed record InvokePluginRequestDto(string ParametersJson);

public sealed record InvokePluginResponse(
    Guid PluginId,
    string Code,
    string HookPoint,
    string Outcome,
    Guid DomainEventId,
    string ResultPayload);

public sealed record CreateConfigTransportRequestDto(
    string Code,
    string Environment,
    string PayloadJson);

public sealed record ConfigTransportResponse(
    Guid Id,
    string Code,
    string Environment,
    DateTimeOffset CreatedUtc);