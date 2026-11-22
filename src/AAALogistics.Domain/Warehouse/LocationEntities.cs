using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Warehouse;

public enum WarehouseSiteKind
{
    Generic = 0,
    Campa = 1,
    Nave = 2,
}

public enum LocationNodeKind
{
    Root = 0,
    Zone = 1,
    Aisle = 2,
    Column = 3,
    Row = 4,
    Position = 5,
    Height = 6,
    Bin = 7,
}

public sealed class LocationNode : Entity
{
    private LocationNode()
    {
    }

    public LocationNode(
        Guid warehouseId,
        string code,
        string name,
        LocationNodeKind kind,
        Guid? parentId = null)
    {
        WarehouseId = warehouseId == Guid.Empty
            ? throw new ArgumentException("Almacén obligatorio.", nameof(warehouseId))
            : warehouseId;
        Code = UnitOfMeasure.NormalizeCode(code, 40);
        Name = UnitOfMeasure.Required(name, nameof(name), 150);
        Kind = kind;
        ParentId = parentId;
        IsActive = true;
    }

    public Guid WarehouseId { get; private set; }

    public Guid? ParentId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public LocationNodeKind Kind { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name) =>
        Name = UnitOfMeasure.Required(name, nameof(name), 150);

    public void Recode(string code) =>
        Code = UnitOfMeasure.NormalizeCode(code, 40);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
