using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Purchasing;

public enum PurchaseOrderStatus
{
    Draft = 0,
    Submitted = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4,
    Closed = 5,
}

public sealed class PurchaseOrder : Entity
{
    private PurchaseOrder()
    {
    }

    internal PurchaseOrder(
        string number,
        Guid supplierId,
        DateOnly documentDate,
        DateOnly? expectedReceiptDate,
        Guid defaultWarehouseId,
        string currency,
        DateTimeOffset createdUtc,
        string? externalId)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        SupplierId = Required(supplierId, nameof(supplierId));
        DocumentDate = documentDate;
        ExpectedReceiptDate = expectedReceiptDate;
        DefaultWarehouseId = Required(
            defaultWarehouseId,
            nameof(defaultWarehouseId));
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        CreatedUtc = createdUtc;
        ExternalId = string.IsNullOrWhiteSpace(externalId)
            ? null
            : UnitOfMeasure.Required(externalId, nameof(externalId), 100);
        Status = PurchaseOrderStatus.Submitted;
    }

    internal void MarkDraft() => Status = PurchaseOrderStatus.Draft;

    internal void Submit()
    {
        if (Status is not PurchaseOrderStatus.Draft and not PurchaseOrderStatus.Submitted)
        {
            throw new InvalidOperationException(
                "Solo se puede enviar un borrador o un pedido ya enviado.");
        }

        Status = PurchaseOrderStatus.Submitted;
    }

    internal void CancelOpen(IEnumerable<PurchaseOrderLine> lines)
    {
        EnsureCancellable();
        foreach (var line in lines)
        {
            line.CancelRemainingOpen();
        }

        Status = PurchaseOrderStatus.Cancelled;
    }

    internal void Close(IEnumerable<PurchaseOrderLine> lines)
    {
        if (Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Closed)
        {
            throw new InvalidOperationException(
                "El pedido ya está cerrado o cancelado.");
        }

        foreach (var line in lines)
        {
            line.CancelRemainingOpen();
        }

        Status = PurchaseOrderStatus.Closed;
    }

    private void EnsureCancellable()
    {
        if (Status is PurchaseOrderStatus.Cancelled
            or PurchaseOrderStatus.Closed
            or PurchaseOrderStatus.Received)
        {
            throw new InvalidOperationException(
                "El pedido no admite cancelación de cantidad abierta.");
        }
    }

    public string Number { get; private set; } = string.Empty;

    public Guid SupplierId { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateOnly DocumentDate { get; private set; }

    public DateOnly? ExpectedReceiptDate { get; private set; }

    public Guid DefaultWarehouseId { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string? ExternalId { get; private set; }

    public PurchaseOrderStatus Status { get; private set; }

    internal void RecalculateStatus(IEnumerable<PurchaseOrderLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var snapshot = lines.ToArray();
        if (snapshot.Length == 0)
        {
            throw new InvalidOperationException(
                "El pedido debe contener líneas.");
        }

        if (Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Closed)
        {
            return;
        }

        var posted = snapshot.Sum(line => line.PostedQuantity);
        var open = snapshot.Sum(line => line.OpenQuantity);
        Status = open == 0
            ? PurchaseOrderStatus.Received
            : posted > 0
                ? PurchaseOrderStatus.PartiallyReceived
                : PurchaseOrderStatus.Submitted;
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException(
                "El identificador es obligatorio.",
                parameterName)
            : value;
}

public sealed class PurchaseOrderLine : Entity
{
    private PurchaseOrderLine()
    {
    }

    internal PurchaseOrderLine(
        Guid purchaseOrderId,
        Guid materialId,
        decimal requestedQuantity,
        decimal purchasePrice,
        string currency,
        string unitSnapshot)
    {
        if (purchaseOrderId == Guid.Empty || materialId == Guid.Empty)
        {
            throw new ArgumentException(
                "El pedido y el material son obligatorios.");
        }

        if (requestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedQuantity),
                requestedQuantity,
                "La cantidad solicitada debe ser positiva.");
        }

        if (purchasePrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(purchasePrice),
                purchasePrice,
                "El precio no puede ser negativo.");
        }

        PurchaseOrderId = purchaseOrderId;
        MaterialId = materialId;
        RequestedQuantity = requestedQuantity;
        ConfirmedQuantity = requestedQuantity;
        PurchasePrice = purchasePrice;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid PurchaseOrderId { get; private set; }

    public Guid MaterialId { get; private set; }

    public decimal RequestedQuantity { get; private set; }

    public decimal ConfirmedQuantity { get; private set; }

    public decimal PostedQuantity { get; private set; }

    public decimal CancelledQuantity { get; private set; }

    public decimal PurchasePrice { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string UnitSnapshot { get; private set; } = string.Empty;

    public decimal OpenQuantity =>
        ConfirmedQuantity - PostedQuantity - CancelledQuantity;

    internal void ApplyReceipt(decimal quantity)
    {
        if (quantity <= 0 || quantity > OpenQuantity)
        {
            throw new InvalidOperationException(
                "La recepción supera la cantidad abierta.");
        }

        PostedQuantity += quantity;
    }

    internal void ReverseReceipt(decimal quantity)
    {
        if (quantity <= 0 || quantity > PostedQuantity)
        {
            throw new InvalidOperationException(
                "La cantidad a revertir no es válida.");
        }

        PostedQuantity -= quantity;
    }

    internal void CancelRemainingOpen()
    {
        if (OpenQuantity <= 0)
        {
            return;
        }

        CancelledQuantity += OpenQuantity;
    }
}

public sealed class GoodsReceipt : Entity
{
    private GoodsReceipt()
    {
    }

    internal GoodsReceipt(
        string number,
        Guid purchaseOrderId,
        Guid supplierId,
        Guid defaultWarehouseId,
        DateOnly documentDate,
        DateTimeOffset postedUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        PurchaseOrderId = purchaseOrderId;
        SupplierId = supplierId;
        DefaultWarehouseId = defaultWarehouseId;
        DocumentDate = documentDate;
        PostedUtc = postedUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid PurchaseOrderId { get; private set; }

    public Guid SupplierId { get; private set; }

    public Guid DefaultWarehouseId { get; private set; }

    public DateOnly DocumentDate { get; private set; }

    public DateTimeOffset PostedUtc { get; private set; }
}

public sealed class GoodsReceiptLine : Entity
{
    private GoodsReceiptLine()
    {
    }

    internal GoodsReceiptLine(
        Guid goodsReceiptId,
        Guid purchaseOrderLineId,
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        string unitSnapshot)
    {
        GoodsReceiptId = goodsReceiptId;
        PurchaseOrderLineId = purchaseOrderLineId;
        MaterialId = materialId;
        WarehouseId = warehouseId;
        Quantity = quantity;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid GoodsReceiptId { get; private set; }

    public Guid PurchaseOrderLineId { get; private set; }

    public Guid MaterialId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;
}
