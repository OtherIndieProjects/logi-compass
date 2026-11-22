using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Sales;

public enum SalesOrderStatus
{
    Requested = 0,
    Confirmed = 1,
    PartiallyDelivered = 2,
    Delivered = 3,
    Cancelled = 4,
    Closed = 5,
    Rejected = 6,
    InPreparation = 7,
    Prepared = 8,
}

public sealed class SalesOrder : Entity, IPersonDataEntity
{
    private SalesOrder()
    {
    }

    internal SalesOrder(
        string number,
        Guid customerId,
        Guid defaultWarehouseId,
        DateOnly requestedDeliveryDate,
        string currency,
        string deliveryAddressLine1,
        string? deliveryAddressLine2,
        string deliveryCity,
        string? deliveryRegion,
        string deliveryPostalCode,
        string deliveryCountryCode,
        DateTimeOffset createdUtc,
        string? externalId)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        CustomerId = customerId;
        DefaultWarehouseId = defaultWarehouseId;
        RequestedDeliveryDate = requestedDeliveryDate;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        DeliveryAddressLine1 = UnitOfMeasure.Required(
            deliveryAddressLine1,
            nameof(deliveryAddressLine1),
            200);
        DeliveryAddressLine2 = Optional(deliveryAddressLine2, 200);
        DeliveryCity = UnitOfMeasure.Required(
            deliveryCity,
            nameof(deliveryCity),
            100);
        DeliveryRegion = Optional(deliveryRegion, 100);
        DeliveryPostalCode = UnitOfMeasure.Required(
            deliveryPostalCode,
            nameof(deliveryPostalCode),
            30);
        DeliveryCountryCode = UnitOfMeasure.NormalizeCode(
            deliveryCountryCode,
            2);
        CreatedUtc = createdUtc;
        ExternalId = Optional(externalId, 100);
        Status = SalesOrderStatus.Requested;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public Guid DefaultWarehouseId { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset? ConfirmedUtc { get; private set; }

    public DateOnly RequestedDeliveryDate { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string DeliveryAddressLine1 { get; private set; } = string.Empty;

    public string? DeliveryAddressLine2 { get; private set; }

    public string DeliveryCity { get; private set; } = string.Empty;

    public string? DeliveryRegion { get; private set; }

    public string DeliveryPostalCode { get; private set; } = string.Empty;

    public string DeliveryCountryCode { get; private set; } = string.Empty;

    public string? ExternalId { get; private set; }

    public string? Incoterms { get; private set; }

    public string? CustomsReference { get; private set; }

    public SalesOrderStatus Status { get; private set; }

    Guid IPersonDataEntity.PersonId => CustomerId;

    internal void SetTradeTerms(string? incoterms, string? customsReference)
    {
        Incoterms = Optional(incoterms, 40);
        CustomsReference = Optional(customsReference, 80);
    }

    internal void Confirm(
        IEnumerable<SalesOrderLine> lines,
        DateTimeOffset utc)
    {
        if (Status != SalesOrderStatus.Requested)
        {
            throw new InvalidOperationException(
                "Solo se puede confirmar un pedido solicitado.");
        }

        var snapshot = lines.ToArray();
        if (snapshot.Length == 0)
        {
            throw new InvalidOperationException(
                "El pedido debe contener líneas.");
        }

        foreach (var line in snapshot)
        {
            line.Confirm();
        }

        ConfirmedUtc = utc;
        Status = SalesOrderStatus.Confirmed;
    }

    internal void Cancel(IEnumerable<SalesOrderLine> lines)
    {
        if (Status is SalesOrderStatus.Delivered
            or SalesOrderStatus.Cancelled
            or SalesOrderStatus.Closed
            or SalesOrderStatus.Rejected)
        {
            throw new InvalidOperationException(
                "El pedido de venta no admite cancelación.");
        }

        foreach (var line in lines)
        {
            line.CancelRemainingOpen();
        }

        Status = SalesOrderStatus.Cancelled;
    }

    internal void StartPreparation()
    {
        if (Status is not (SalesOrderStatus.Confirmed
            or SalesOrderStatus.InPreparation
            or SalesOrderStatus.PartiallyDelivered))
        {
            throw new InvalidOperationException(
                "Solo se puede preparar un pedido confirmado.");
        }

        Status = SalesOrderStatus.InPreparation;
    }

    internal void MarkPrepared()
    {
        if (Status is not (SalesOrderStatus.InPreparation
            or SalesOrderStatus.Confirmed))
        {
            throw new InvalidOperationException(
                "El pedido no está en preparación.");
        }

        Status = SalesOrderStatus.Prepared;
    }

    /// <summary>
    /// After a full delivery reverse, <see cref="RecalculateStatus"/> may demote
    /// to <see cref="SalesOrderStatus.Confirmed"/>. Restore Prepared when prep
    /// coverage still covers open quantity (caller verifies coverage).
    /// </summary>
    internal void RestorePreparedAfterReverse()
    {
        if (Status == SalesOrderStatus.Prepared)
        {
            return;
        }

        if (Status != SalesOrderStatus.Confirmed)
        {
            throw new InvalidOperationException(
                "Solo se puede restaurar Preparado tras un reverso que deje el pedido Confirmado.");
        }

        Status = SalesOrderStatus.Prepared;
    }

    internal void Close(IEnumerable<SalesOrderLine> lines)
    {
        if (Status is SalesOrderStatus.Cancelled
            or SalesOrderStatus.Closed
            or SalesOrderStatus.Rejected)
        {
            throw new InvalidOperationException(
                "El pedido de venta ya está cerrado o cancelado.");
        }

        // Prepared = flujo 3PL; Confirmed/entregas = compatibilidad y Gerente/Admin.
        if (Status is not (SalesOrderStatus.Prepared
            or SalesOrderStatus.Confirmed
            or SalesOrderStatus.InPreparation
            or SalesOrderStatus.Delivered
            or SalesOrderStatus.PartiallyDelivered))
        {
            throw new InvalidOperationException(
                "El pedido no admite cierre en su estado actual.");
        }

        foreach (var line in lines)
        {
            line.CancelRemainingOpen();
        }

        Status = SalesOrderStatus.Closed;
    }

    internal void Reject()
    {
        if (Status != SalesOrderStatus.Requested)
        {
            throw new InvalidOperationException(
                "Solo se puede rechazar un pedido solicitado.");
        }

        Status = SalesOrderStatus.Rejected;
    }

    internal void RecalculateStatus(IEnumerable<SalesOrderLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (Status is SalesOrderStatus.Cancelled
            or SalesOrderStatus.Closed
            or SalesOrderStatus.Rejected)
        {
            return;
        }

        var snapshot = lines.ToArray();
        var posted = snapshot.Sum(line => line.PostedQuantity);
        var open = snapshot.Sum(line => line.OpenQuantity);
        if (open == 0)
        {
            Status = SalesOrderStatus.Delivered;
            return;
        }

        if (posted > 0)
        {
            Status = SalesOrderStatus.PartiallyDelivered;
            return;
        }

        if (Status is SalesOrderStatus.InPreparation or SalesOrderStatus.Prepared)
        {
            return;
        }

        Status = SalesOrderStatus.Confirmed;
    }

    private static string? Optional(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : UnitOfMeasure.Required(value, nameof(value), maxLength);
}

public sealed class SalesOrderLine : Entity
{
    private SalesOrderLine()
    {
    }

    internal SalesOrderLine(
        Guid salesOrderId,
        Guid materialId,
        decimal requestedQuantity,
        decimal catalogPriceSnapshot,
        string currency,
        string unitSnapshot)
    {
        if (requestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestedQuantity),
                requestedQuantity,
                "La cantidad solicitada debe ser positiva.");
        }

        if (catalogPriceSnapshot < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(catalogPriceSnapshot),
                catalogPriceSnapshot,
                "El precio no puede ser negativo.");
        }

        SalesOrderId = salesOrderId;
        MaterialId = materialId;
        RequestedQuantity = requestedQuantity;
        CatalogPriceSnapshot = catalogPriceSnapshot;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid SalesOrderId { get; private set; }

    public Guid MaterialId { get; private set; }

    public decimal RequestedQuantity { get; private set; }

    public decimal ConfirmedQuantity { get; private set; }

    public decimal PostedQuantity { get; private set; }

    public decimal CancelledQuantity { get; private set; }

    public decimal CatalogPriceSnapshot { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string UnitSnapshot { get; private set; } = string.Empty;

    public decimal OpenQuantity =>
        ConfirmedQuantity - PostedQuantity - CancelledQuantity;

    internal void Confirm()
    {
        if (ConfirmedQuantity != 0)
        {
            throw new InvalidOperationException(
                "La línea ya está confirmada.");
        }

        ConfirmedQuantity = RequestedQuantity;
    }

    internal void ApplyDelivery(decimal quantity)
    {
        if (quantity <= 0 || quantity > OpenQuantity)
        {
            throw new InvalidOperationException(
                "La entrega supera la cantidad abierta.");
        }

        PostedQuantity += quantity;
    }

    internal void ReverseDelivery(decimal quantity)
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

public sealed class Delivery : Entity, IPersonDataEntity
{
    private Delivery()
    {
    }

    internal Delivery(
        string number,
        Guid salesOrderId,
        Guid customerId,
        Guid defaultWarehouseId,
        DateOnly documentDate,
        DateTimeOffset postedUtc,
        string deliveryAddressSnapshot)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        SalesOrderId = salesOrderId;
        CustomerId = customerId;
        DefaultWarehouseId = defaultWarehouseId;
        DocumentDate = documentDate;
        PostedUtc = postedUtc;
        DeliveryAddressSnapshot = UnitOfMeasure.Required(
            deliveryAddressSnapshot,
            nameof(deliveryAddressSnapshot),
            800);
    }

    public string Number { get; private set; } = string.Empty;

    public Guid SalesOrderId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid DefaultWarehouseId { get; private set; }

    public DateOnly DocumentDate { get; private set; }

    public DateTimeOffset PostedUtc { get; private set; }

    public string DeliveryAddressSnapshot { get; private set; } = string.Empty;

    Guid IPersonDataEntity.PersonId => CustomerId;
}

public sealed class DeliveryLine : Entity
{
    private DeliveryLine()
    {
    }

    internal DeliveryLine(
        Guid deliveryId,
        Guid salesOrderLineId,
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        string unitSnapshot)
    {
        DeliveryId = deliveryId;
        SalesOrderLineId = salesOrderLineId;
        MaterialId = materialId;
        WarehouseId = warehouseId;
        Quantity = quantity;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
    }

    public Guid DeliveryId { get; private set; }

    public Guid SalesOrderLineId { get; private set; }

    public Guid MaterialId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;
}
