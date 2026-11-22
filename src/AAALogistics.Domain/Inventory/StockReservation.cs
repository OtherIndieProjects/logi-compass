using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Inventory;

/// <summary>
/// Soft stock reservation created on sales-order confirm (ADR 0023 / ATP).
/// Does not mutate <see cref="StockBalance"/>; delivery consumes remaining qty.
/// </summary>
public sealed class StockReservation : Entity
{
    private StockReservation()
    {
    }

    public StockReservation(
        Guid salesOrderId,
        Guid salesOrderLineId,
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        DateTimeOffset createdUtc)
    {
        if (salesOrderId == Guid.Empty
            || salesOrderLineId == Guid.Empty
            || materialId == Guid.Empty
            || warehouseId == Guid.Empty)
        {
            throw new ArgumentException(
                "El pedido, la línea, el material y el almacén son obligatorios.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "La cantidad reservada debe ser positiva.");
        }

        SalesOrderId = salesOrderId;
        SalesOrderLineId = salesOrderLineId;
        MaterialId = materialId;
        WarehouseId = warehouseId;
        Quantity = quantity;
        CreatedUtc = createdUtc;
        ConsumedQuantity = 0m;
        IsActive = true;
    }

    public Guid SalesOrderId { get; private set; }

    public Guid SalesOrderLineId { get; private set; }

    public Guid MaterialId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public decimal ConsumedQuantity { get; private set; }

    public DateTimeOffset? ReleasedUtc { get; private set; }

    public bool IsActive { get; private set; }

    public decimal RemainingQuantity => Quantity - ConsumedQuantity;

    public void Consume(decimal qty)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException(
                "La reserva no está activa.");
        }

        if (qty <= 0 || qty > RemainingQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(qty),
                qty,
                "La cantidad a consumir supera el remanente de la reserva.");
        }

        ConsumedQuantity += qty;
        if (RemainingQuantity == 0)
        {
            IsActive = false;
        }
    }

    public void Release(DateTimeOffset utc)
    {
        if (!IsActive)
        {
            return;
        }

        ReleasedUtc = utc;
        IsActive = false;
    }

    public void RestoreConsume(decimal qty)
    {
        if (qty <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(qty),
                qty,
                "La cantidad a restaurar debe ser positiva.");
        }

        if (qty > ConsumedQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(qty),
                qty,
                "No se puede restaurar más de lo consumido en la reserva.");
        }

        ConsumedQuantity -= qty;
        if (ReleasedUtc is null)
        {
            IsActive = true;
        }
    }
}
