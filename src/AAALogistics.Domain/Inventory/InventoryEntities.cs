using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Inventory;

public enum StockMovementType
{
    Opening,
    Receipt,
    Delivery,
    TransferIn,
    TransferOut,
    Adjustment,
    Reversal,
    ReturnIn,
    ReturnOut,
}

/// <summary>
/// Didactic stock quality status. Only <see cref="Available"/> counts for ATP/FEFO.
/// </summary>
public enum StockStatus
{
    Available = 0,
    Quality = 1,
    Blocked = 2,
    Damaged = 3,
}

public sealed class StockBalance : Entity
{
    private StockBalance()
    {
    }

    internal StockBalance(
        Guid materialId,
        Guid warehouseId,
        Guid locationId,
        Guid lotId = default,
        decimal minQuantity = 0,
        StockStatus status = StockStatus.Available,
        Guid ownerCustomerId = default)
    {
        MaterialId = Required(materialId, nameof(materialId));
        WarehouseId = Required(warehouseId, nameof(warehouseId));
        LocationId = Required(locationId, nameof(locationId));
        // Guid.Empty is the intentional non-lot sentinel.
        LotId = lotId;
        // Guid.Empty = stock propio del operador 3PL (no multi-dueño).
        OwnerCustomerId = ownerCustomerId;
        Status = status;
        if (minQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minQuantity),
                minQuantity,
                "El stock mínimo no puede ser negativo.");
        }

        MinQuantity = minQuantity;
    }

    public Guid MaterialId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid LocationId { get; private set; }

    /// <summary>Lot id, or <see cref="Guid.Empty"/> for non-lot stock.</summary>
    public Guid LotId { get; private set; }

    /// <summary>
    /// Dueño comercial del stock (3PL multi-owner). <see cref="Guid.Empty"/> = operador.
    /// </summary>
    public Guid OwnerCustomerId { get; private set; }

    public StockStatus Status { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal MinQuantity { get; private set; }

    internal void SetStatus(StockStatus status) => Status = status;

    internal void Apply(decimal signedQuantity)
    {
        if (signedQuantity == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signedQuantity),
                "El movimiento no puede tener cantidad cero.");
        }

        var resultingQuantity = Quantity + signedQuantity;
        if (resultingQuantity < 0)
        {
            throw new InvalidOperationException(
                "La operación produciría stock negativo.");
        }

        Quantity = resultingQuantity;
    }

    internal void SetMinQuantity(decimal minQuantity)
    {
        if (minQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minQuantity),
                minQuantity,
                "El stock mínimo no puede ser negativo.");
        }

        MinQuantity = minQuantity;
    }

    /// <summary>
    /// Migrates a legacy empty LocationId onto a concrete DEFAULT (or other) node.
    /// </summary>
    internal void AssignLocation(Guid locationId)
    {
        if (locationId == Guid.Empty)
        {
            throw new ArgumentException(
                "La ubicación es obligatoria.",
                nameof(locationId));
        }

        if (LocationId != Guid.Empty && LocationId != locationId)
        {
            throw new InvalidOperationException(
                "El saldo ya está asignado a otra ubicación.");
        }

        LocationId = locationId;
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException(
                "El identificador es obligatorio.",
                parameterName)
            : value;
}

public sealed class StockMovement : Entity
{
    private StockMovement()
    {
    }

    private StockMovement(
        StockMovementType type,
        Guid materialId,
        Guid warehouseId,
        Guid locationId,
        Guid lotId,
        Guid ownerCustomerId,
        decimal signedQuantity,
        string unitSnapshot,
        string originDocumentType,
        Guid? originDocumentId,
        Guid? originLineId,
        Guid? correlatedOperationId,
        Guid? reversedMovementId,
        DateTimeOffset postedUtc,
        Guid? actorId,
        string? reason)
    {
        if (materialId == Guid.Empty
            || warehouseId == Guid.Empty
            || locationId == Guid.Empty)
        {
            throw new ArgumentException(
                "El material, el almacén y la ubicación son obligatorios.");
        }

        ValidateSign(type, signedQuantity);
        if ((type is StockMovementType.Adjustment
                or StockMovementType.Opening
                or StockMovementType.Reversal)
            && string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "El motivo es obligatorio para aperturas, ajustes y reversos.",
                nameof(reason));
        }

        if (type == StockMovementType.Reversal
            && reversedMovementId is null)
        {
            throw new ArgumentException(
                "Un reverso debe enlazar el movimiento original.",
                nameof(reversedMovementId));
        }

        Type = type;
        MaterialId = materialId;
        WarehouseId = warehouseId;
        LocationId = locationId;
        LotId = lotId;
        OwnerCustomerId = ownerCustomerId;
        SignedQuantity = signedQuantity;
        UnitSnapshot = UnitOfMeasure.NormalizeCode(unitSnapshot, 16);
        OriginDocumentType = UnitOfMeasure.Required(
            originDocumentType,
            nameof(originDocumentType),
            40);
        OriginDocumentId = originDocumentId;
        OriginLineId = originLineId;
        CorrelatedOperationId = correlatedOperationId;
        ReversedMovementId = reversedMovementId;
        PostedUtc = postedUtc;
        ActorId = actorId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public StockMovementType Type { get; private set; }

    public Guid MaterialId { get; private set; }

    public Guid WarehouseId { get; private set; }

    public Guid LocationId { get; private set; }

    /// <summary>Lot id, or <see cref="Guid.Empty"/> when the movement is non-lot.</summary>
    public Guid LotId { get; private set; }

    /// <summary>Stock owner; <see cref="Guid.Empty"/> = house.</summary>
    public Guid OwnerCustomerId { get; private set; }

    public decimal SignedQuantity { get; private set; }

    public string UnitSnapshot { get; private set; } = string.Empty;

    public string OriginDocumentType { get; private set; } = string.Empty;

    public Guid? OriginDocumentId { get; private set; }

    public Guid? OriginLineId { get; private set; }

    public Guid? CorrelatedOperationId { get; private set; }

    public Guid? ReversedMovementId { get; private set; }

    public DateTimeOffset PostedUtc { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? Reason { get; private set; }

    internal static StockMovement Create(
        StockMovementType type,
        Guid materialId,
        Guid warehouseId,
        Guid locationId,
        decimal signedQuantity,
        string unitSnapshot,
        string originDocumentType,
        Guid? originDocumentId,
        Guid? originLineId,
        Guid? correlatedOperationId,
        DateTimeOffset postedUtc,
        Guid? actorId,
        string? reason = null,
        Guid lotId = default,
        Guid ownerCustomerId = default) =>
        new(
            type,
            materialId,
            warehouseId,
            locationId,
            lotId,
            ownerCustomerId,
            signedQuantity,
            unitSnapshot,
            originDocumentType,
            originDocumentId,
            originLineId,
            correlatedOperationId,
            null,
            postedUtc,
            actorId,
            reason);

    internal static StockMovement CreateReversal(
        StockMovement original,
        bool alreadyReversed,
        DateTimeOffset postedUtc,
        Guid? actorId,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(original);

        if (alreadyReversed)
        {
            throw new InvalidOperationException(
                "El movimiento ya fue revertido.");
        }

        if (original.Type == StockMovementType.Reversal)
        {
            throw new InvalidOperationException(
                "No se puede revertir un movimiento reverso.");
        }

        return new StockMovement(
            StockMovementType.Reversal,
            original.MaterialId,
            original.WarehouseId,
            original.LocationId,
            original.LotId,
            original.OwnerCustomerId,
            -original.SignedQuantity,
            original.UnitSnapshot,
            original.OriginDocumentType,
            original.OriginDocumentId,
            original.OriginLineId,
            original.CorrelatedOperationId,
            original.Id,
            postedUtc,
            actorId,
            reason);
    }

    private static void ValidateSign(
        StockMovementType type,
        decimal signedQuantity)
    {
        if (signedQuantity == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(signedQuantity),
                "La cantidad firmada no puede ser cero.");
        }

        var shouldBePositive = type is StockMovementType.Opening
            or StockMovementType.Receipt
            or StockMovementType.TransferIn
            or StockMovementType.ReturnIn;
        var shouldBeNegative = type is StockMovementType.Delivery
            or StockMovementType.TransferOut
            or StockMovementType.ReturnOut;

        if ((shouldBePositive && signedQuantity < 0)
            || (shouldBeNegative && signedQuantity > 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(signedQuantity),
                "El signo no corresponde al tipo de movimiento.");
        }
    }
}

public enum InventoryCountStatus
{
    Draft = 0,
    Posted = 1,
    Discarded = 2,
}

public sealed class InventoryCount : Entity
{
    private InventoryCount()
    {
    }

    public InventoryCount(
        string number,
        Guid warehouseId,
        DateTimeOffset createdUtc,
        Guid? actorId)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        WarehouseId = Required(warehouseId, nameof(warehouseId));
        CreatedUtc = createdUtc;
        ActorId = actorId;
        Status = InventoryCountStatus.Draft;
    }

    public string Number { get; private set; } = string.Empty;

    public Guid WarehouseId { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset? PostedUtc { get; private set; }

    public Guid? ActorId { get; private set; }

    public InventoryCountStatus Status { get; private set; }

    public void Discard()
    {
        if (Status != InventoryCountStatus.Draft)
        {
            throw new InvalidOperationException(
                "Solo se puede descartar un recuento en borrador.");
        }

        Status = InventoryCountStatus.Discarded;
    }

    public void MarkPosted(DateTimeOffset utc)
    {
        if (Status != InventoryCountStatus.Draft)
        {
            throw new InvalidOperationException(
                "Solo se puede contabilizar un recuento en borrador.");
        }

        PostedUtc = utc;
        Status = InventoryCountStatus.Posted;
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException(
                "El identificador es obligatorio.",
                parameterName)
            : value;
}

public sealed class InventoryCountLine : Entity
{
    private InventoryCountLine()
    {
    }

    public InventoryCountLine(
        Guid inventoryCountId,
        Guid materialId,
        decimal systemQuantity,
        decimal countedQuantity)
    {
        if (inventoryCountId == Guid.Empty || materialId == Guid.Empty)
        {
            throw new ArgumentException(
                "El recuento y el material son obligatorios.");
        }

        if (systemQuantity < 0 || countedQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(countedQuantity),
                "Las cantidades de recuento no pueden ser negativas.");
        }

        InventoryCountId = inventoryCountId;
        MaterialId = materialId;
        SystemQuantity = systemQuantity;
        CountedQuantity = countedQuantity;
    }

    public Guid InventoryCountId { get; private set; }

    public Guid MaterialId { get; private set; }

    public decimal SystemQuantity { get; private set; }

    public decimal CountedQuantity { get; private set; }

    public decimal Difference => CountedQuantity - SystemQuantity;
}
