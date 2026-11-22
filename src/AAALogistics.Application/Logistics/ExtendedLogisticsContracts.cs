using AAALogistics.Domain.Billing;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Documents;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Warehouse;

namespace AAALogistics.Application.Logistics;

public sealed record LocationNodeView(
    Guid Id,
    Guid WarehouseId,
    Guid? ParentId,
    string Code,
    string Name,
    string Kind,
    bool IsActive);

public sealed record CreateLocationNodeCommand(
    Guid WarehouseId,
    string Code,
    string Name,
    LocationNodeKind Kind,
    Guid? ParentId);

public sealed record UpdateLocationNodeCommand(
    Guid WarehouseId,
    Guid LocationId,
    string Name,
    bool IsActive);

public sealed record OrderPrepSessionView(
    Guid Id,
    Guid SalesOrderId,
    Guid UserId,
    string UserName,
    string Mode,
    DateTimeOffset JoinedUtc);

public sealed record JoinPrepCommand(
    Guid SalesOrderId,
    PrepLockMode Mode);

public sealed record PrepPickLineView(
    Guid Id,
    Guid SalesOrderId,
    Guid SalesOrderLineId,
    Guid MaterialId,
    Guid SuggestedLocationId,
    Guid? SuggestedLotId,
    decimal Quantity,
    decimal ConfirmedQuantity,
    DateTimeOffset? ConfirmedUtc);

public sealed record ConfirmPrepPickCommand(
    Guid SalesOrderId,
    Guid PrepPickLineId,
    decimal ConfirmedQuantity,
    Guid UserId);

public sealed record CarrierView(
    Guid Id,
    string Code,
    string Name,
    string? TaxId,
    string? Phone,
    bool IsActive);

public sealed record CreateCarrierCommand(
    string Code,
    string Name,
    string? TaxId,
    string? Phone);

public sealed record CreateCmrCommand(
    Guid SalesOrderId,
    Guid? DeliveryId,
    Guid CarrierId,
    string VehiclePlate,
    int Packages,
    string? Remarks);

public sealed record CmrView(
    Guid Id,
    string Number,
    Guid SalesOrderId,
    Guid CarrierId,
    string VehiclePlate,
    int Packages,
    DateTimeOffset CreatedUtc);

public sealed record IssueInvoiceCommand(
    Guid CustomerId,
    Guid? SalesOrderId,
    string SeriesCode,
    decimal TaxRate,
    IReadOnlyCollection<IssueInvoiceLineCommand> Lines,
    string IdempotencyKey);

public sealed record IssueInvoiceLineCommand(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public sealed record InvoiceLineView(
    string Description,
    decimal Quantity,
    decimal UnitPrice);

public sealed record InvoiceView(
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
    IReadOnlyCollection<InvoiceLineView> Lines);

public sealed record MaterialDeletionRequestView(
    Guid Id,
    Guid MaterialId,
    Guid RequestedByUserId,
    string Reason,
    bool HasStock,
    string Status,
    DateTimeOffset RequestedUtc);

public sealed record CreateHandlingUnitCommand(
    Guid SalesOrderId,
    Guid? DeliveryId,
    int PackageCount,
    string? Sscc = null);

public sealed record HandlingUnitView(
    Guid Id,
    string Sscc,
    Guid SalesOrderId,
    Guid? DeliveryId,
    int PackageCount,
    DateTimeOffset CreatedUtc);

public interface IExtendedLogisticsService
{
    Task<LocationNodeView> CreateLocationAsync(
        CreateLocationNodeCommand command,
        CancellationToken cancellationToken = default);

    Task<LocationNodeView> UpdateLocationAsync(
        UpdateLocationNodeCommand command,
        CancellationToken cancellationToken = default);

    Task DeleteLocationAsync(
        Guid warehouseId,
        Guid locationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LocationNodeView>> ListLocationsAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderPrepSessionView>> JoinPrepAsync(
        JoinPrepCommand command,
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default);

    Task LeavePrepAsync(
        Guid salesOrderId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task MarkPreparedAsync(
        Guid salesOrderId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderPrepSessionView>> ListActivePrepAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PrepPickLineView>> ListPrepPicksAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<PrepPickLineView> ConfirmPrepPickAsync(
        ConfirmPrepPickCommand command,
        CancellationToken cancellationToken = default);

    Task<CarrierView> CreateCarrierAsync(
        CreateCarrierCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CarrierView>> ListCarriersAsync(
        CancellationToken cancellationToken = default);

    Task<CmrView> CreateCmrAsync(
        CreateCmrCommand command,
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderCmrPdfAsync(
        Guid cmrId,
        CancellationToken cancellationToken = default);

    Task<HandlingUnitView> CreateHandlingUnitAsync(
        CreateHandlingUnitCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<HandlingUnitView>> ListHandlingUnitsByOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderPackingListPdfAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    Task<InvoiceView> IssueInvoiceAsync(
        IssueInvoiceCommand command,
        CancellationToken cancellationToken = default);

    Task<InvoiceView> RectifyInvoiceAsync(
        Guid invoiceId,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InvoiceView>> ListInvoicesAsync(
        CancellationToken cancellationToken = default,
        Guid? customerId = null);

    Task<byte[]> RenderInvoicePdfAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<IssuerFiscalProfile> UpsertIssuerAsync(
        UpsertIssuerCommand command,
        CancellationToken cancellationToken = default);

    Task<MaterialDeletionRequestView> RequestMaterialDeletionAsync(
        Guid materialId,
        Guid requestedByUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task ApproveMaterialDeletionAsync(
        Guid requestId,
        Guid approvedByUserId,
        CancellationToken cancellationToken = default);

    Task RejectMaterialDeletionAsync(
        Guid requestId,
        Guid rejectedByUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<MaterialDeletionRequestView>> ListMaterialDeletionRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<IssuerFiscalProfile?> GetIssuerAsync(
        CancellationToken cancellationToken = default);

    Task<QuoteView> CreateQuoteAsync(
        CreateQuoteCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<QuoteView>> ListQuotesAsync(
        CancellationToken cancellationToken = default);

    Task<QuoteView> SendQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default);

    Task<QuoteView> RejectQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default);

    Task<QuoteView> AcceptQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderQuotePdfAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default);
}

public sealed record CreateQuoteLineCommand(
    Guid MaterialId,
    decimal Quantity,
    decimal UnitPrice);

public sealed record CreateQuoteCommand(
    Guid CustomerId,
    Guid DefaultWarehouseId,
    DateOnly ValidUntil,
    string Currency,
    decimal TaxRate,
    IReadOnlyCollection<CreateQuoteLineCommand> Lines,
    string? Notes = null);

public sealed record QuoteLineView(
    Guid Id,
    Guid MaterialId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal NetAmount);

public sealed record QuoteView(
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
    IReadOnlyCollection<QuoteLineView> Lines);

public sealed record UpsertIssuerCommand(
    string LegalName,
    string TaxId,
    string AddressLine,
    string City,
    string PostalCode,
    string CountryCode,
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
    string? SealPngRelativePath = null,
    byte[]? SealPngBytes = null,
    string? SignaturePngRelativePath = null,
    byte[]? SignaturePngBytes = null,
    string? LogoPngRelativePath = null,
    byte[]? LogoPngBytes = null);
