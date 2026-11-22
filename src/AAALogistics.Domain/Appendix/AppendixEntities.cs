using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Appendix;

public enum WaveStatus
{
    Draft = 0,
    Released = 1,
    Closed = 2,
}

public enum PurchaseRequisitionStatus
{
    Open = 0,
    Ordered = 1,
    Cancelled = 2,
}

public enum PurchaseRequisitionSource
{
    Demand = 0,
    Minimum = 1,
    AtpGap = 2,
}

public sealed class Wave : Entity
{
    private Wave()
    {
    }

    public Wave(string number, int priority, DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        Priority = priority;
        Status = WaveStatus.Draft;
        CreatedUtc = createdUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public WaveStatus Status { get; private set; }

    public int Priority { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public void Release()
    {
        if (Status != WaveStatus.Draft)
        {
            throw new InvalidOperationException("Solo borrador puede liberarse.");
        }

        Status = WaveStatus.Released;
    }

    public void Close()
    {
        Status = WaveStatus.Closed;
    }
}

public sealed class WaveLine : Entity
{
    private WaveLine()
    {
    }

    public WaveLine(Guid waveId, Guid salesOrderId)
    {
        WaveId = waveId;
        SalesOrderId = salesOrderId;
    }

    public Guid WaveId { get; private set; }

    public Guid SalesOrderId { get; private set; }
}

public sealed class UnitConversion : Entity
{
    private UnitConversion()
    {
    }

    public UnitConversion(Guid fromUnitId, Guid toUnitId, decimal factor)
    {
        if (fromUnitId == toUnitId)
        {
            throw new ArgumentException("Las unidades deben ser distintas.");
        }

        if (factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        FromUnitId = fromUnitId;
        ToUnitId = toUnitId;
        Factor = factor;
    }

    public Guid FromUnitId { get; private set; }

    public Guid ToUnitId { get; private set; }

    public decimal Factor { get; private set; }

    public void UpdateFactor(decimal factor)
    {
        if (factor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor));
        }

        Factor = factor;
    }

    public decimal Convert(decimal quantity, int targetScale) =>
        Math.Round(quantity * Factor, targetScale, MidpointRounding.AwayFromZero);
}

public sealed class PurchaseRequisition : Entity
{
    private PurchaseRequisition()
    {
    }

    public PurchaseRequisition(
        string number,
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        PurchaseRequisitionSource source,
        DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        MaterialId = materialId;
        WarehouseId = warehouseId;
        Quantity = quantity <= 0
            ? throw new ArgumentOutOfRangeException(nameof(quantity))
            : quantity;
        Source = source;
        Status = PurchaseRequisitionStatus.Open;
        CreatedUtc = createdUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid MaterialId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public decimal Quantity { get; private set; }

    public PurchaseRequisitionStatus Status { get; private set; }

    public PurchaseRequisitionSource Source { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public void Cancel()
    {
        if (Status != PurchaseRequisitionStatus.Open)
        {
            throw new InvalidOperationException("Solo abierta puede cancelarse.");
        }

        Status = PurchaseRequisitionStatus.Cancelled;
    }

    public void MarkOrdered()
    {
        if (Status != PurchaseRequisitionStatus.Open)
        {
            throw new InvalidOperationException("Solo abierta puede pedirse.");
        }

        Status = PurchaseRequisitionStatus.Ordered;
    }
}

public sealed class RateCard : Entity
{
    private RateCard()
    {
    }

    public RateCard(string code, string name, string currency)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void Update(string name, string currency)
    {
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
    }
}

public sealed class RateCardLine : Entity
{
    private RateCardLine()
    {
    }

    public RateCardLine(
        Guid rateCardId,
        string serviceCode,
        decimal unitPrice,
        string unitLabel)
    {
        RateCardId = rateCardId;
        ServiceCode = UnitOfMeasure.NormalizeCode(serviceCode, 30);
        UnitPrice = unitPrice < 0
            ? throw new ArgumentOutOfRangeException(nameof(unitPrice))
            : unitPrice;
        UnitLabel = UnitOfMeasure.Required(unitLabel, nameof(unitLabel), 40);
    }

    public Guid RateCardId { get; private set; }

    public string ServiceCode { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public string UnitLabel { get; private set; } = string.Empty;

    public void Update(decimal unitPrice, string unitLabel)
    {
        UnitPrice = unitPrice < 0
            ? throw new ArgumentOutOfRangeException(nameof(unitPrice))
            : unitPrice;
        UnitLabel = UnitOfMeasure.Required(unitLabel, nameof(unitLabel), 40);
    }
}

public sealed class TransportRoute : Entity
{
    private TransportRoute()
    {
    }

    public TransportRoute(
        string code,
        string name,
        Guid? carrierId,
        decimal distanceKm,
        decimal baseTariff,
        decimal ratePerKm)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        CarrierId = carrierId;
        DistanceKm = distanceKm < 0
            ? throw new ArgumentOutOfRangeException(nameof(distanceKm))
            : distanceKm;
        BaseTariff = baseTariff < 0
            ? throw new ArgumentOutOfRangeException(nameof(baseTariff))
            : baseTariff;
        RatePerKm = ratePerKm < 0
            ? throw new ArgumentOutOfRangeException(nameof(ratePerKm))
            : ratePerKm;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public Guid? CarrierId { get; private set; }

    public decimal DistanceKm { get; private set; }

    public decimal BaseTariff { get; private set; }

    public decimal RatePerKm { get; private set; }

    public bool IsActive { get; private set; }

    public decimal ComputeTariff() =>
        Math.Round(BaseTariff + (DistanceKm * RatePerKm), 2, MidpointRounding.AwayFromZero);
}

public sealed class TransportStop : Entity
{
    private TransportStop()
    {
    }

    public TransportStop(
        Guid routeId,
        int sequence,
        Guid? salesOrderId,
        Guid? deliveryId,
        string? addressSnapshot)
    {
        RouteId = routeId;
        Sequence = sequence < 0
            ? throw new ArgumentOutOfRangeException(nameof(sequence))
            : sequence;
        SalesOrderId = salesOrderId;
        DeliveryId = deliveryId;
        AddressSnapshot = string.IsNullOrWhiteSpace(addressSnapshot)
            ? null
            : addressSnapshot.Trim();
    }

    public Guid RouteId { get; private set; }

    public int Sequence { get; private set; }

    public Guid? SalesOrderId { get; private set; }

    public Guid? DeliveryId { get; private set; }

    public string? AddressSnapshot { get; private set; }
}
