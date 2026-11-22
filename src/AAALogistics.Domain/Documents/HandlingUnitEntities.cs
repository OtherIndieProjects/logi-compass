using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Documents;

/// <summary>
/// Didactic SSCC handling unit for packing lists / CMR package counts.
/// </summary>
public sealed class HandlingUnit : Entity
{
    private HandlingUnit()
    {
    }

    public HandlingUnit(
        string sscc,
        Guid salesOrderId,
        Guid? deliveryId,
        int packageCount,
        DateTimeOffset createdUtc)
    {
        Sscc = UnitOfMeasure.NormalizeCode(sscc, 20);
        if (Sscc.Length is < 8 or > 20)
        {
            throw new ArgumentException(
                "El SSCC debe tener entre 8 y 20 caracteres.",
                nameof(sscc));
        }

        if (salesOrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "El pedido de venta es obligatorio.",
                nameof(salesOrderId));
        }

        if (packageCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(packageCount),
                "El número de bultos debe ser al menos 1.");
        }

        SalesOrderId = salesOrderId;
        DeliveryId = deliveryId;
        PackageCount = packageCount;
        CreatedUtc = createdUtc;
    }

    public string Sscc { get; private set; } = string.Empty;

    public Guid SalesOrderId { get; private set; }

    public Guid? DeliveryId { get; private set; }

    public int PackageCount { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public void AssignDelivery(Guid deliveryId)
    {
        if (deliveryId == Guid.Empty)
        {
            throw new ArgumentException(
                "La entrega es obligatoria.",
                nameof(deliveryId));
        }

        DeliveryId = deliveryId;
    }
}
