using AAALogistics.Domain.Auditing;
using AAALogistics.Domain.Inventory;

namespace AAALogistics.Application.Logistics;

public enum LogisticsErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
}

[Serializable]
public sealed class LogisticsException : Exception
{
    public LogisticsException()
    {
    }

    public LogisticsException(string message)
        : base(message)
    {
    }

    public LogisticsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public LogisticsException(
        LogisticsErrorKind kind,
        string code,
        string message)
        : base(message)
    {
        Kind = kind;
        Code = code;
    }

    public LogisticsErrorKind Kind { get; } = LogisticsErrorKind.Validation;

    public string Code { get; } = "AAA-LOGISTICS-0001";
}

public sealed record CreateUnitOfMeasureCommand(
    string Code,
    string Name,
    int DecimalScale);

public sealed record UpdateUnitOfMeasureCommand(
    Guid UnitId,
    string Name,
    int DecimalScale);

public sealed record CreateMaterialCommand(
    string Code,
    string DisplayName,
    Guid UnitId,
    decimal CatalogPrice,
    string Currency,
    string? ExternalId,
    bool RequiresLot = false,
    string? Description = null);

public sealed record UpdateMaterialCommand(
    Guid MaterialId,
    string DisplayName,
    decimal CatalogPrice,
    string Currency,
    bool IsActive,
    string? ExternalId,
    bool RequiresLot = false,
    string? Description = null);

public sealed record CreateWarehouseCommand(
    string Code,
    string Name,
    string? SiteKind = null);

public sealed record UpdateWarehouseCommand(
    Guid WarehouseId,
    string Name,
    string? SiteKind = null);

public sealed record CreatePartyCommand(
    string Name,
    string TaxId,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode,
    string? ExternalId,
    int? LeadTimeDays = null);

public sealed record UpdatePartyCommand(
    Guid PartyId,
    string Name,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode,
    bool IsActive,
    string? ExternalId,
    int? LeadTimeDays = null);

public sealed record UnitOfMeasureView(
    Guid Id,
    string Code,
    string Name,
    int DecimalScale);

public sealed record MaterialView(
    Guid Id,
    string Code,
    string DisplayName,
    string Description,
    Guid UnitId,
    decimal CatalogPrice,
    string Currency,
    bool IsActive,
    string? ExternalId,
    bool RequiresLot = false);

public sealed record WarehouseView(
    Guid Id,
    string Code,
    string Name,
    string SiteKind = "Generic");

public sealed record PartyView(
    Guid Id,
    string Name,
    string TaxId,
    string City,
    string CountryCode,
    bool IsActive,
    string? ExternalId,
    string AddressLine1 = "",
    string? AddressLine2 = null,
    string? Region = null,
    string PostalCode = "",
    int? LeadTimeDays = null);

public sealed record EnsureDefaultUnitsResult(
    int CreatedCount,
    IReadOnlyCollection<string> CreatedCodes);

public sealed record CreatePurchaseOrderLineCommand(
    Guid MaterialId,
    decimal Quantity,
    decimal PurchasePrice);

public sealed record CreatePurchaseOrderCommand(
    Guid SupplierId,
    Guid DefaultWarehouseId,
    DateOnly DocumentDate,
    DateOnly? ExpectedReceiptDate,
    string Currency,
    string? ExternalId,
    IReadOnlyCollection<CreatePurchaseOrderLineCommand> Lines);

public sealed record OrderLineView(
    Guid Id,
    Guid MaterialId,
    decimal RequestedQuantity,
    decimal ConfirmedQuantity,
    decimal PostedQuantity,
    decimal CancelledQuantity,
    decimal OpenQuantity);

public sealed record PurchaseOrderView(
    Guid Id,
    string Number,
    string Status,
    IReadOnlyCollection<OrderLineView> Lines,
    Guid SupplierId = default);

public sealed record PostGoodsReceiptLineCommand(
    Guid PurchaseOrderLineId,
    Guid? WarehouseId,
    decimal Quantity,
    string? LotCode = null,
    DateOnly? LotExpiryDate = null,
    Guid OwnerCustomerId = default);

public sealed record PostGoodsReceiptCommand(
    string IdempotencyKey,
    Guid PurchaseOrderId,
    DateOnly DocumentDate,
    IReadOnlyCollection<PostGoodsReceiptLineCommand> Lines);

public sealed record CreateSalesOrderLineCommand(
    Guid MaterialId,
    decimal Quantity);

public sealed record CreateSalesOrderCommand(
    Guid CustomerId,
    Guid DefaultWarehouseId,
    DateOnly RequestedDeliveryDate,
    string Currency,
    string? ExternalId,
    IReadOnlyCollection<CreateSalesOrderLineCommand> Lines,
    string? Incoterms = null,
    string? CustomsReference = null);

public sealed record UpdateSalesOrderTradeTermsCommand(
    Guid SalesOrderId,
    string? Incoterms,
    string? CustomsReference);

public sealed record SalesOrderView(
    Guid Id,
    string Number,
    string Status,
    IReadOnlyCollection<OrderLineView> Lines,
    Guid CustomerId,
    string? Incoterms = null,
    string? CustomsReference = null);

public sealed record ConfirmSalesOrderResult(
    Guid SalesOrderId,
    string Status,
    IReadOnlyCollection<string> Warnings);

public sealed record AtpView(
    Guid MaterialId,
    Guid WarehouseId,
    decimal AvailableQuantity);

public sealed record PostDeliveryLineCommand(
    Guid SalesOrderLineId,
    Guid? WarehouseId,
    decimal Quantity,
    Guid? LotId = null,
    Guid OwnerCustomerId = default);

public sealed record PostDeliveryCommand(
    string IdempotencyKey,
    Guid SalesOrderId,
    DateOnly DocumentDate,
    IReadOnlyCollection<PostDeliveryLineCommand> Lines,
    bool AllowFefoOverride = false);

public sealed record DeliverySummaryView(
    Guid Id,
    string Number,
    Guid SalesOrderId,
    string? SalesOrderNumber,
    DateOnly DocumentDate,
    Guid CustomerId);

public sealed record GoodsReceiptSummaryView(
    Guid Id,
    string Number,
    Guid PurchaseOrderId,
    string? PurchaseOrderNumber,
    DateOnly DocumentDate,
    Guid SupplierId);

public sealed record DocumentNumberRangeView(
    string DocumentType,
    string Prefix,
    long NextValue);

public sealed record PostTransferCommand(
    string IdempotencyKey,
    Guid MaterialId,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    decimal Quantity,
    string Reason);

public sealed record PostLocationTransferCommand(
    string IdempotencyKey,
    Guid MaterialId,
    Guid WarehouseId,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    decimal Quantity,
    string? Reason = null,
    Guid LotId = default);

public sealed record PostPutawayCommand(
    string IdempotencyKey,
    Guid MaterialId,
    Guid WarehouseId,
    Guid DestinationLocationId,
    decimal Quantity,
    Guid LotId = default,
    string? Reason = null);

public sealed record PickSuggestion(
    Guid LocationId,
    Guid LotId,
    decimal Quantity,
    DateOnly? ExpiryDate = null);

public sealed record LotView(
    Guid Id,
    Guid MaterialId,
    string Code,
    DateOnly? ExpiryDate,
    DateTimeOffset CreatedUtc);

public sealed record PostAdjustmentCommand(
    string IdempotencyKey,
    Guid MaterialId,
    Guid WarehouseId,
    decimal SignedQuantity,
    StockMovementType Type,
    string Reason);

public sealed record PostSalesReturnLineCommand(
    Guid MaterialId,
    Guid WarehouseId,
    Guid? LocationId,
    decimal Quantity,
    Guid LotId = default);

public sealed record PostSalesReturnCommand(
    string IdempotencyKey,
    Guid SalesOrderId,
    Guid? OriginalDeliveryId,
    StockMovementType Type,
    DateOnly DocumentDate,
    string Reason,
    IReadOnlyCollection<PostSalesReturnLineCommand> Lines);

public sealed record ReverseMovementCommand(
    string IdempotencyKey,
    Guid MovementId,
    string Reason);

public sealed record PostingResult(
    Guid DocumentId,
    string? DocumentNumber,
    IReadOnlyCollection<Guid> MovementIds);

public sealed record StockView(
    Guid MaterialId,
    string MaterialCode,
    Guid WarehouseId,
    string WarehouseCode,
    decimal Quantity,
    decimal MinQuantity,
    Guid? LocationId = null,
    string? LocationCode = null,
    Guid LotId = default,
    string Status = "Available");

public sealed record SetStockStatusCommand(
    Guid MaterialId,
    Guid WarehouseId,
    Guid LocationId,
    Guid LotId,
    StockStatus Status);

public sealed record MovementView(
    Guid Id,
    StockMovementType Type,
    Guid MaterialId,
    Guid WarehouseId,
    decimal SignedQuantity,
    string UnitSnapshot,
    string OriginDocumentType,
    Guid? OriginDocumentId,
    Guid? OriginLineId,
    Guid? CorrelatedOperationId,
    Guid? ReversedMovementId,
    DateTimeOffset PostedUtc,
    string? Reason);

public sealed record ReconciliationItem(
    Guid MaterialId,
    Guid WarehouseId,
    decimal BalanceQuantity,
    decimal LedgerQuantity,
    decimal Difference);

public sealed record ReconciliationResult(
    bool IsConsistent,
    IReadOnlyCollection<ReconciliationItem> Differences);

public sealed record DemoSeedResult(
    bool Created,
    IReadOnlyCollection<Guid> OpeningMovementIds,
    string Message);

public sealed record WipeCatalogResult(
    int RowsDeleted,
    string Message);

public interface ILogisticsService
{
    Task<UnitOfMeasureView> CreateUnitOfMeasureAsync(
        CreateUnitOfMeasureCommand command,
        CancellationToken cancellationToken = default);

    Task<UnitOfMeasureView> UpdateUnitOfMeasureAsync(
        UpdateUnitOfMeasureCommand command,
        CancellationToken cancellationToken = default);

    Task<EnsureDefaultUnitsResult> EnsureDefaultUnitsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UnitOfMeasureView>> ListUnitsAsync(
        CancellationToken cancellationToken = default);

    Task<MaterialView> CreateMaterialAsync(
        CreateMaterialCommand command,
        CancellationToken cancellationToken = default);

    Task<MaterialView> UpdateMaterialAsync(
        UpdateMaterialCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MaterialView>> ListMaterialsAsync(
        CancellationToken cancellationToken = default);

    Task<WarehouseView> CreateWarehouseAsync(
        CreateWarehouseCommand command,
        CancellationToken cancellationToken = default);

    Task DeleteWarehouseAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default);

    Task<WarehouseView> UpdateWarehouseAsync(
        UpdateWarehouseCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WarehouseView>> ListWarehousesAsync(
        CancellationToken cancellationToken = default);

    Task<PartyView> CreateSupplierAsync(
        CreatePartyCommand command,
        CancellationToken cancellationToken = default);

    Task<PartyView> UpdateSupplierAsync(
        UpdatePartyCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PartyView>> ListSuppliersAsync(
        CancellationToken cancellationToken = default);

    Task<PartyView> CreateCustomerAsync(
        CreatePartyCommand command,
        CancellationToken cancellationToken = default);

    Task<PartyView> UpdateCustomerAsync(
        UpdatePartyCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PartyView>> ListCustomersAsync(
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderView> CreatePurchaseOrderAsync(
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PurchaseOrderView>> ListPurchaseOrdersAsync(
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderView> GetPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostGoodsReceiptAsync(
        PostGoodsReceiptCommand command,
        CancellationToken cancellationToken = default);

    Task<SalesOrderView> CreateSalesOrderAsync(
        CreateSalesOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<SalesOrderView> UpdateSalesOrderTradeTermsAsync(
        UpdateSalesOrderTradeTermsCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SalesOrderView>> ListSalesOrdersAsync(
        CancellationToken cancellationToken = default,
        Guid? customerId = null);

    Task<ConfirmSalesOrderResult> ConfirmSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<AtpView> GetAtpAsync(
        Guid materialId,
        Guid warehouseId,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostDeliveryAsync(
        PostDeliveryCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DeliverySummaryView>> ListDeliveriesAsync(
        Guid? salesOrderId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<GoodsReceiptSummaryView>> ListGoodsReceiptsAsync(
        Guid? purchaseOrderId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DocumentNumberRangeView>> ListDocumentNumberRangesAsync(
        CancellationToken cancellationToken = default);

    Task<DocumentNumberRangeView> UpsertDocumentNumberRangeAsync(
        string documentType,
        string prefix,
        long nextValue,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostTransferAsync(
        PostTransferCommand command,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostLocationTransferAsync(
        PostLocationTransferCommand command,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostPutawayAsync(
        PostPutawayCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PickSuggestion>> SuggestPickAsync(
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LotView>> ListLotsAsync(
        Guid materialId,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostAdjustmentAsync(
        PostAdjustmentCommand command,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostSalesReturnAsync(
        PostSalesReturnCommand command,
        CancellationToken cancellationToken = default);

    Task<PostingResult> ReverseMovementAsync(
        ReverseMovementCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StockView>> GetStockAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MovementView>> GetMovementsAsync(
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<ReconciliationResult> ReconcileStockAsync(
        CancellationToken cancellationToken = default);

    Task<DemoSeedResult> SeedDemoAsync(
        CancellationToken cancellationToken = default);

    Task<WipeCatalogResult> WipeCatalogAsync(
        string confirm,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderView> CancelPurchaseOrderOpenAsync(
        Guid purchaseOrderId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderView> ClosePurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<SalesOrderView> CancelSalesOrderAsync(
        Guid salesOrderId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<SalesOrderView> CloseSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<SalesOrderView> RejectSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<StockView> SetMinQuantityAsync(
        Guid materialId,
        Guid warehouseId,
        decimal minQuantity,
        CancellationToken cancellationToken = default);

    Task<StockView> SetStockStatusAsync(
        SetStockStatusCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StockView>> ListBelowMinimumAsync(
        CancellationToken cancellationToken = default);

    Task<InventoryCountView> CreateInventoryCountAsync(
        CreateInventoryCountCommand command,
        CancellationToken cancellationToken = default);

    Task<PostingResult> PostInventoryCountAsync(
        string idempotencyKey,
        Guid inventoryCountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderStatusReportRow>> ReportOrdersByStatusAsync(
        CancellationToken cancellationToken = default);

    Task<OtifReportRow> ReportOtifAsync(
        CancellationToken cancellationToken = default);
}

public sealed record CreateInventoryCountLineCommand(
    Guid MaterialId,
    decimal CountedQuantity);

public sealed record CreateInventoryCountCommand(
    Guid WarehouseId,
    IReadOnlyCollection<CreateInventoryCountLineCommand> Lines);

public sealed record InventoryCountLineView(
    Guid MaterialId,
    decimal SystemQuantity,
    decimal CountedQuantity,
    decimal Difference);

public sealed record InventoryCountView(
    Guid Id,
    string Number,
    Guid WarehouseId,
    string Status,
    IReadOnlyCollection<InventoryCountLineView> Lines);

public sealed record OrderStatusReportRow(
    string DocumentKind,
    Guid Id,
    string Number,
    string Status,
    Guid? PartyId);

public sealed record OtifReportRow(
    int DeliveredOrders,
    int OnTimeOrders,
    decimal OtifPercent);

public sealed record AuditEntryView(
    long Sequence,
    string TableName,
    string RecordId,
    string Action,
    Guid? UserId,
    string? UserName,
    string? Ip,
    DateTimeOffset Utc,
    string? OldJson = null,
    string? NewJson = null);

public sealed record AuditEntryQuery(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    Guid? UserId = null,
    string? UserName = null,
    string? TableName = null,
    string? Area = null,
    string? Action = null,
    int Page = 1,
    int PageSize = 100,
    bool IncludePayloads = false);

public sealed record AuditEntryPage(
    IReadOnlyList<AuditEntryView> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AuditCheckpointView(
    Guid Id,
    DateTimeOffset CreatedUtc,
    long ThroughSequence,
    string ThroughHash,
    string ExternalPath,
    Guid? CreatedByUserId,
    string? Note);

public sealed record AuditRetentionStatus(
    int RetentionDays,
    long EntriesOlderThanRetention,
    long? LatestCheckpointSequence,
    bool PurgeBlockedWithoutCheckpoint,
    string Message);

public sealed record BackupInfo(
    Guid Id,
    string FileName,
    BackupStatus Status,
    long? SizeBytes,
    string? Sha256,
    string? Error,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc);

public sealed record RestoreResult(
    string FileName,
    DateTimeOffset RestoredUtc);

public interface IBackupService
{
    Task<BackupInfo> CreateAsync(
        bool gzip,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<BackupInfo>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<RestoreResult> RestoreAsync(
        string dumpPath,
        string confirmation,
        CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task<AuditVerificationResult> VerifyAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AuditEntryView>> ListRecentEntriesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<AuditEntryPage> QueryEntriesAsync(
        AuditEntryQuery query,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportEntriesAsync(
        AuditEntryQuery query,
        string format,
        CancellationToken cancellationToken = default);

    Task<AuditCheckpointView> CreateCheckpointAsync(
        string? note,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditCheckpointView>> ListCheckpointsAsync(
        CancellationToken cancellationToken = default);

    Task<AuditRetentionStatus> GetRetentionStatusAsync(
        CancellationToken cancellationToken = default);

    Task CryptoShredPersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default);

    Task<string?> TryDecryptPersonPayloadAsync(
        Guid personId,
        string payload,
        CancellationToken cancellationToken = default);
}

public interface IVerifactuSubmitter
{
    Task SubmitAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}

public interface IRequestContext
{
    Guid? UserId { get; }

    Guid? CustomerId { get; }

    Guid? CompanyId { get; }

    string? IpAddress { get; }
}

public interface ICustomerRlsSession
{
    Task ApplyAsync(CancellationToken cancellationToken = default);
}
