using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Inventory;

/// <summary>
/// Material lot / batch for FEFO picking (ADR 0023 B1).
/// Unique per (MaterialId, Code). Guid.Empty on stock means non-lot sentinel.
/// </summary>
public sealed class Lot : Entity
{
    private Lot()
    {
    }

    public Lot(
        Guid materialId,
        string code,
        DateOnly? expiryDate,
        DateTimeOffset createdUtc)
    {
        if (materialId == Guid.Empty)
        {
            throw new ArgumentException(
                "El material es obligatorio.",
                nameof(materialId));
        }

        MaterialId = materialId;
        Code = UnitOfMeasure.NormalizeCode(code, 40);
        ExpiryDate = expiryDate;
        CreatedUtc = createdUtc;
    }

    public Guid MaterialId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public DateOnly? ExpiryDate { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }
}
