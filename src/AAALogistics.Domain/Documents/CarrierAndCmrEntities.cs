using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Documents;

public sealed class Carrier : Entity
{
    private Carrier()
    {
    }

    public Carrier(string code, string name, string? taxId, string? phone)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        TaxId = string.IsNullOrWhiteSpace(taxId)
            ? null
            : UnitOfMeasure.NormalizeCode(taxId, 40);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? TaxId { get; private set; }

    public string? Phone { get; private set; }

    public bool IsActive { get; private set; }

    public void Update(string name, string? taxId, string? phone)
    {
        Name = UnitOfMeasure.Required(name, nameof(name), 200);
        TaxId = string.IsNullOrWhiteSpace(taxId)
            ? null
            : UnitOfMeasure.NormalizeCode(taxId, 40);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
    }
}

public sealed class CmrDocument : Entity
{
    private CmrDocument()
    {
    }

    public CmrDocument(
        string number,
        Guid salesOrderId,
        Guid? deliveryId,
        Guid carrierId,
        string vehiclePlate,
        int packages,
        string? remarks,
        DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        SalesOrderId = salesOrderId;
        DeliveryId = deliveryId;
        CarrierId = carrierId;
        VehiclePlate = UnitOfMeasure.Required(vehiclePlate, nameof(vehiclePlate), 20);
        Packages = packages < 0
            ? throw new ArgumentOutOfRangeException(nameof(packages))
            : packages;
        Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        CreatedUtc = createdUtc;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid SalesOrderId { get; private set; }

    public Guid? DeliveryId { get; private set; }

    public Guid CarrierId { get; private set; }

    public string VehiclePlate { get; private set; } = string.Empty;

    public int Packages { get; private set; }

    public string? Remarks { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }
}
