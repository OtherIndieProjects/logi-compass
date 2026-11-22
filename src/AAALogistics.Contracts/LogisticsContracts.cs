namespace AAALogistics.Contracts;

public sealed record CreateUnitOfMeasureRequest(
    string Code,
    string Name,
    int DecimalScale);

public sealed record UpdateUnitOfMeasureRequest(
    string Name,
    int DecimalScale);

public sealed record CreateMaterialRequest(
    string Code,
    string? DisplayName,
    Guid UnitId,
    decimal CatalogPrice,
    string Currency,
    string? ExternalId,
    bool RequiresLot = false,
    string? Description = null);

public sealed record UpdateMaterialRequest(
    string? DisplayName,
    decimal CatalogPrice,
    string Currency,
    bool IsActive,
    string? ExternalId,
    bool RequiresLot = false,
    string? Description = null);

public sealed record CreateWarehouseRequest(
    string Code,
    string Name,
    string? SiteKind = null);

public sealed record UpdateWarehouseRequest(
    string Name,
    string? SiteKind = null);

public sealed record CreatePartyRequest(
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

public sealed record UpdatePartyRequest(
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

public sealed record CreatePurchaseOrderLineRequest(
    Guid MaterialId,
    decimal Quantity,
    decimal PurchasePrice);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid DefaultWarehouseId,
    DateOnly DocumentDate,
    DateOnly? ExpectedReceiptDate,
    string Currency,
    string? ExternalId,
    IReadOnlyCollection<CreatePurchaseOrderLineRequest> Lines);

public sealed record PostGoodsReceiptLineRequest(
    Guid PurchaseOrderLineId,
    Guid? WarehouseId,
    decimal Quantity,
    string? LotCode = null,
    DateOnly? LotExpiryDate = null,
    Guid OwnerCustomerId = default);

public sealed record PostGoodsReceiptRequest(
    Guid PurchaseOrderId,
    DateOnly DocumentDate,
    IReadOnlyCollection<PostGoodsReceiptLineRequest> Lines);

public sealed record CreateSalesOrderLineRequest(
    Guid MaterialId,
    decimal Quantity);

public sealed record CreateSalesOrderRequest(
    Guid CustomerId,
    Guid DefaultWarehouseId,
    DateOnly RequestedDeliveryDate,
    string Currency,
    string? ExternalId,
    IReadOnlyCollection<CreateSalesOrderLineRequest> Lines,
    string? Incoterms = null,
    string? CustomsReference = null);

public sealed record UpdateSalesOrderTradeTermsRequest(
    string? Incoterms,
    string? CustomsReference);

public sealed record PostSalesReturnLineRequest(
    Guid MaterialId,
    Guid WarehouseId,
    Guid? LocationId,
    decimal Quantity,
    Guid LotId = default);

public sealed record PostSalesReturnRequest(
    Guid SalesOrderId,
    Guid? OriginalDeliveryId,
    string Type,
    DateOnly DocumentDate,
    string Reason,
    IReadOnlyCollection<PostSalesReturnLineRequest> Lines);

public sealed record CreateHandlingUnitRequest(
    Guid SalesOrderId,
    Guid? DeliveryId,
    int PackageCount,
    string? Sscc = null);

public sealed record HandlingUnitResponse(
    Guid Id,
    string Sscc,
    Guid SalesOrderId,
    Guid? DeliveryId,
    int PackageCount,
    DateTimeOffset CreatedUtc);

public sealed record OtifReportResponse(
    int DeliveredOrders,
    int OnTimeOrders,
    decimal OtifPercent);

public sealed record AuditEntryResponse(
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

public sealed record AuditEntryPageResponse(
    IReadOnlyList<AuditEntryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AuditCheckpointResponse(
    Guid Id,
    DateTimeOffset CreatedUtc,
    long ThroughSequence,
    string ThroughHash,
    string ExternalPath,
    Guid? CreatedByUserId,
    string? Note);

public sealed record AuditRetentionStatusResponse(
    int RetentionDays,
    long EntriesOlderThanRetention,
    long? LatestCheckpointSequence,
    bool PurgeBlockedWithoutCheckpoint,
    string Message);

public sealed record AuditVerificationResponse(
    bool IsValid,
    long EntryCount,
    long? InvalidSequence,
    string Message);

public sealed record PostDeliveryLineRequest(
    Guid SalesOrderLineId,
    Guid? WarehouseId,
    decimal Quantity,
    Guid? LotId = null,
    Guid OwnerCustomerId = default);

public sealed record PostDeliveryRequest(
    Guid SalesOrderId,
    DateOnly DocumentDate,
    IReadOnlyCollection<PostDeliveryLineRequest> Lines,
    bool AllowFefoOverride = false);

public sealed record PostPutawayRequest(
    Guid MaterialId,
    Guid WarehouseId,
    Guid DestinationLocationId,
    decimal Quantity,
    Guid LotId = default,
    string? Reason = null);

public sealed record LotResponse(
    Guid Id,
    Guid MaterialId,
    string Code,
    DateOnly? ExpiryDate,
    DateTimeOffset CreatedUtc);

public sealed record PrepPickLineResponse(
    Guid Id,
    Guid SalesOrderId,
    Guid SalesOrderLineId,
    Guid MaterialId,
    Guid SuggestedLocationId,
    Guid? SuggestedLotId,
    decimal Quantity,
    decimal ConfirmedQuantity,
    DateTimeOffset? ConfirmedUtc);

public sealed record ConfirmPrepPickRequestDto(decimal ConfirmedQuantity);

public sealed record PostTransferRequest(
    Guid MaterialId,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    decimal Quantity,
    string Reason);

public sealed record PostAdjustmentRequest(
    Guid MaterialId,
    Guid WarehouseId,
    decimal SignedQuantity,
    string Type,
    string Reason);

public sealed record ReverseMovementRequest(string Reason);

public sealed record SetMinQuantityRequest(
    Guid MaterialId,
    Guid WarehouseId,
    decimal MinQuantity);

public sealed record CreateInventoryCountLineRequest(
    Guid MaterialId,
    decimal CountedQuantity);

public sealed record CreateInventoryCountRequest(
    Guid WarehouseId,
    IReadOnlyCollection<CreateInventoryCountLineRequest> Lines);

public sealed record AdminTableEditRequest(
    string TableName,
    string RecordId,
    IReadOnlyDictionary<string, string?> Values);

public sealed record PortalCatalogItemResponse(
    Guid Id,
    string Code,
    string Description,
    decimal CatalogPrice,
    string Currency);

public sealed record PortalWarehouseResponse(
    Guid Id,
    string Code,
    string Name);

public sealed record PortalAtpResponse(
    Guid MaterialId,
    Guid WarehouseId,
    decimal AvailableQuantity);

public sealed record SubmitRegistrationRequest(
    string CompanyName,
    string TaxId,
    string ContactName,
    string Email,
    string UserName,
    string AddressLine1,
    string City,
    string PostalCode,
    string CountryCode,
    bool AcceptTerms,
    string? Website = null,
    string? PowChallenge = null,
    string? PowNonce = null);

public sealed record PowChallengeResponse(
    string Challenge,
    int DifficultyBits);

public sealed record RejectRegistrationRequest(string Reason);

public sealed record RegistrationSubmittedResponse(
    Guid Id,
    string Status,
    string Message);

public sealed record PortalSalesOrderLineRow(
    Guid Id,
    Guid MaterialId,
    decimal RequestedQuantity,
    decimal OpenQuantity);

public sealed record PortalSalesOrderRow(
    Guid Id,
    string Number,
    string Status,
    Guid CustomerId,
    IReadOnlyList<PortalSalesOrderLineRow> Lines);

public sealed record LocationNodeResponse(
    Guid Id,
    Guid WarehouseId,
    Guid? ParentId,
    string Code,
    string Name,
    string Kind,
    bool IsActive);

public sealed record CreateLocationNodeRequest(
    string Code,
    string Name,
    string Kind,
    Guid? ParentId);

public sealed record UpdateLocationNodeRequest(
    string Name,
    bool IsActive);

public sealed record OrderPrepSessionResponse(
    Guid Id,
    Guid SalesOrderId,
    Guid UserId,
    string UserName,
    string Mode,
    DateTimeOffset JoinedUtc);

public sealed record JoinPrepRequestDto(string Mode);

public sealed record CarrierResponse(
    Guid Id,
    string Code,
    string Name,
    string? TaxId,
    string? Phone,
    bool IsActive);

public sealed record CreateCarrierRequestDto(
    string Code,
    string Name,
    string? TaxId,
    string? Phone);

public sealed record CreateCmrRequestDto(
    Guid SalesOrderId,
    Guid? DeliveryId,
    Guid CarrierId,
    string VehiclePlate,
    int Packages,
    string? Remarks);

public sealed record CmrResponse(
    Guid Id,
    string Number,
    Guid SalesOrderId,
    Guid CarrierId,
    string VehiclePlate,
    int Packages,
    DateTimeOffset CreatedUtc);

public sealed record IssueInvoiceLineRequestDto(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public sealed record IssueInvoiceRequestDto(
    Guid CustomerId,
    Guid? SalesOrderId,
    string SeriesCode,
    decimal TaxRate,
    IReadOnlyCollection<IssueInvoiceLineRequestDto> Lines,
    string? IdempotencyKey = null);

public sealed record InvoiceLineResponse(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public sealed record InvoiceResponse(
    Guid Id,
    string DocumentNumber,
    string Kind,
    string Status,
    Guid CustomerId,
    Guid? SalesOrderId,
    decimal NetAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    string SifHash,
    string? PreviousSifHash,
    DateOnly IssueDate,
    IReadOnlyList<InvoiceLineResponse> Lines);

public sealed record RectifyInvoiceRequestDto(string Reason);

/// <summary>Marca del producto (UI/PDF). Distinto del perfil fiscal emisor.</summary>
public sealed record AppBrandingResponse(
    string DisplayName,
    string AccentHex,
    string? Tagline,
    string? LogoRelativePath,
    bool HasLogo);

public sealed record UpsertAppBrandingRequest(
    string DisplayName,
    string AccentHex,
    string? Tagline = null,
    string? LogoFileName = null,
    string? LogoBase64 = null,
    bool ClearLogo = false);

public sealed record QuoteLineResponse(
    Guid Id,
    Guid MaterialId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal NetAmount);

public sealed record DeliverySummaryResponse(
    Guid Id,
    string Number,
    Guid SalesOrderId,
    string? SalesOrderNumber,
    DateOnly DocumentDate,
    Guid CustomerId);

public sealed record GoodsReceiptSummaryResponse(
    Guid Id,
    string Number,
    Guid PurchaseOrderId,
    string? PurchaseOrderNumber,
    DateOnly DocumentDate,
    Guid SupplierId);

public sealed record DocumentNumberRangeResponse(
    string DocumentType,
    string Prefix,
    long NextValue);

public sealed record UpsertDocumentNumberRangeRequest(
    string DocumentType,
    string Prefix,
    long NextValue);

public sealed record QuoteResponse(
    Guid Id,
    string Number,
    string Status,
    Guid CustomerId,
    Guid DefaultWarehouseId,
    DateOnly IssueDate,
    DateOnly ValidUntil,
    string Currency,
    decimal TaxRate,
    decimal NetAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    Guid? SalesOrderId,
    string? Notes,
    IReadOnlyList<QuoteLineResponse> Lines);

public sealed record CreateQuoteLineRequestDto(
    Guid MaterialId,
    decimal Quantity,
    decimal UnitPrice);

public sealed record CreateQuoteRequestDto(
    Guid CustomerId,
    Guid DefaultWarehouseId,
    DateOnly ValidUntil,
    string Currency,
    decimal TaxRate,
    IReadOnlyCollection<CreateQuoteLineRequestDto> Lines,
    string? Notes = null);

public sealed record IssuerFiscalResponse(
    Guid Id,
    string LegalName,
    string TaxId,
    string AddressLine,
    string City,
    string PostalCode,
    string CountryCode,
    string? Phone,
    string? Email,
    string? Website,
    string? SignerName,
    string? SignerTitle,
    string? Iban,
    string? BankName,
    string? PaymentMethod,
    int PaymentDueDays,
    string? LegalFooterText,
    decimal IrpfPercent,
    string? SealPngRelativePath,
    string? SignaturePngRelativePath,
    string? LogoPngRelativePath,
    string VerifactuMode);

public sealed record UpsertIssuerRequestDto(
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

public sealed record SetStockStatusRequest(
    Guid MaterialId,
    Guid WarehouseId,
    Guid LocationId,
    string Status,
    Guid LotId = default);

public sealed record MaterialDeletionRequestDto(string Reason);

public sealed record MaterialDeletionResponse(
    Guid Id,
    Guid MaterialId,
    Guid RequestedByUserId,
    string Reason,
    bool HasStock,
    string Status,
    DateTimeOffset RequestedUtc);

public sealed record TransferLocationRequest(
    Guid MaterialId,
    Guid WarehouseId,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    decimal Quantity,
    string? Reason = null);

public sealed record PostLocationTransferRequest(
    Guid MaterialId,
    Guid WarehouseId,
    Guid SourceLocationId,
    Guid DestinationLocationId,
    decimal Quantity,
    string? Reason = null,
    Guid LotId = default);

public sealed record CreateWaveRequestDto(
    IReadOnlyCollection<Guid> SalesOrderIds,
    int Priority = 0);

public sealed record WaveResponse(
    Guid Id,
    string Number,
    string Status,
    int Priority,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<Guid> SalesOrderIds);

public sealed record UpsertUnitConversionRequestDto(
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Factor);

public sealed record UnitConversionResponse(
    Guid Id,
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Factor);

public sealed record ConvertQuantityRequestDto(
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Quantity);

public sealed record ConvertQuantityResponse(
    Guid FromUnitId,
    Guid ToUnitId,
    decimal InputQuantity,
    decimal OutputQuantity,
    decimal Factor);

public sealed record GenerateMrpRequestDto(Guid? WarehouseId);

public sealed record PurchaseRequisitionResponse(
    Guid Id,
    string Number,
    Guid MaterialId,
    Guid WarehouseId,
    decimal Quantity,
    string Status,
    string Source,
    DateTimeOffset CreatedUtc);

public sealed record UpsertRateCardRequestDto(
    string Code,
    string Name,
    string Currency,
    IReadOnlyCollection<UpsertRateCardLineRequestDto> Lines);

public sealed record UpsertRateCardLineRequestDto(
    string ServiceCode,
    decimal UnitPrice,
    string UnitLabel);

public sealed record RateCardResponse(
    Guid Id,
    string Code,
    string Name,
    string Currency,
    bool IsActive,
    IReadOnlyCollection<RateCardLineResponse> Lines);

public sealed record RateCardLineResponse(
    Guid Id,
    string ServiceCode,
    decimal UnitPrice,
    string UnitLabel);

public sealed record ComputeRateChargesRequestDto(
    Dictionary<string, decimal> QuantitiesByService);

public sealed record RateChargeReportResponse(
    Guid RateCardId,
    string Currency,
    IReadOnlyCollection<RateChargeLineResponse> Lines,
    decimal Total);

public sealed record RateChargeLineResponse(
    string ServiceCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record CreateTransportRouteRequestDto(
    string Code,
    string Name,
    Guid? CarrierId,
    decimal DistanceKm,
    decimal BaseTariff,
    decimal RatePerKm);

public sealed record AddTransportStopRequestDto(
    int Sequence,
    Guid? SalesOrderId,
    Guid? DeliveryId,
    string? AddressSnapshot);

public sealed record TransportRouteResponse(
    Guid Id,
    string Code,
    string Name,
    Guid? CarrierId,
    decimal DistanceKm,
    decimal BaseTariff,
    decimal RatePerKm,
    decimal ComputedTariff,
    bool IsActive,
    IReadOnlyCollection<TransportStopResponse> Stops);

public sealed record TransportStopResponse(
    Guid Id,
    int Sequence,
    Guid? SalesOrderId,
    Guid? DeliveryId,
    string? AddressSnapshot);

public sealed record CreateCompanyRequestDto(
    string Code,
    string LegalName,
    string TaxId,
    string Currency);

public sealed record CompanyResponse(
    Guid Id,
    string Code,
    string LegalName,
    string TaxId,
    string Currency,
    bool IsActive);

public sealed record UpsertCommercialTermsRequestDto(
    Guid CustomerId,
    Guid CompanyId,
    int PaymentTermDays,
    decimal CreditLimit,
    string Currency,
    decimal OpenReceivables = 0);

public sealed record CustomerCommercialTermsResponse(
    Guid Id,
    Guid CustomerId,
    Guid CompanyId,
    int PaymentTermDays,
    decimal CreditLimit,
    decimal OpenReceivables,
    string Currency,
    bool IsCreditBlocked);

public sealed record UpsertExchangeRateRequestDto(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly ValidOn);

public sealed record ConvertFxRequestDto(
    string FromCurrency,
    string ToCurrency,
    decimal Amount,
    DateOnly? OnDate = null);

public sealed record FxConvertResponse(
    string FromCurrency,
    string ToCurrency,
    decimal InputAmount,
    decimal OutputAmount,
    decimal Rate,
    DateOnly ValidOn);

public sealed record RfScanRequestDto(string Code);

public sealed record RfScanResponseDto(
    string Code,
    string Kind,
    DateTimeOffset ScannedUtc,
    Guid? ResolvedId = null,
    string? ResolvedLabel = null);
