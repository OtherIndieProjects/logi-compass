using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Sales;

/// <summary>
/// FEFO pick suggestion generated when joining order prep (ADR 0023 B1).
/// </summary>
public sealed class PrepPickLine : Entity
{
    private PrepPickLine()
    {
    }

    public PrepPickLine(
        Guid salesOrderId,
        Guid salesOrderLineId,
        Guid materialId,
        Guid suggestedLocationId,
        Guid? suggestedLotId,
        decimal quantity)
    {
        if (salesOrderId == Guid.Empty
            || salesOrderLineId == Guid.Empty
            || materialId == Guid.Empty
            || suggestedLocationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Pedido, línea, material y ubicación sugerida son obligatorios.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "La cantidad sugerida debe ser positiva.");
        }

        if (suggestedLotId == Guid.Empty)
        {
            suggestedLotId = null;
        }

        SalesOrderId = salesOrderId;
        SalesOrderLineId = salesOrderLineId;
        MaterialId = materialId;
        SuggestedLocationId = suggestedLocationId;
        SuggestedLotId = suggestedLotId;
        Quantity = quantity;
        ConfirmedQuantity = 0m;
        ConsumedQuantity = 0m;
    }

    public Guid SalesOrderId { get; private set; }

    public Guid SalesOrderLineId { get; private set; }

    public Guid MaterialId { get; private set; }

    public Guid SuggestedLocationId { get; private set; }

    public Guid? SuggestedLotId { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal ConfirmedQuantity { get; private set; }

    public decimal ConsumedQuantity { get; private set; }

    public DateTimeOffset? ConfirmedUtc { get; private set; }

    public decimal RemainingConfirmed => ConfirmedQuantity - ConsumedQuantity;

    public void Confirm(decimal confirmedQuantity, DateTimeOffset utc)
    {
        if (confirmedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmedQuantity),
                confirmedQuantity,
                "La cantidad confirmada debe ser positiva.");
        }

        if (confirmedQuantity > Quantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmedQuantity),
                confirmedQuantity,
                "La cantidad confirmada no puede superar la sugerida.");
        }

        if (confirmedQuantity < ConsumedQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confirmedQuantity),
                confirmedQuantity,
                "La cantidad confirmada no puede ser inferior a la ya consumida en entrega.");
        }

        ConfirmedQuantity = confirmedQuantity;
        ConfirmedUtc = utc;
    }

    public void Consume(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "La cantidad consumida debe ser positiva.");
        }

        if (quantity > RemainingConfirmed)
        {
            throw new InvalidOperationException(
                "No se puede consumir más picking del confirmado restante.");
        }

        ConsumedQuantity += quantity;
    }

    public void RestoreConsume(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "La cantidad a restaurar debe ser positiva.");
        }

        if (quantity > ConsumedQuantity)
        {
            throw new InvalidOperationException(
                "No se puede restaurar más picking del consumido.");
        }

        ConsumedQuantity -= quantity;
    }
}
