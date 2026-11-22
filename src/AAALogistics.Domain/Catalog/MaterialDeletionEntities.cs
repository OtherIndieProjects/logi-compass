using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Catalog;

public enum MaterialDeletionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public sealed class MaterialDeletionRequest : Entity
{
    private MaterialDeletionRequest()
    {
    }

    public MaterialDeletionRequest(
        Guid materialId,
        Guid requestedByUserId,
        string reason,
        bool hasStock,
        DateTimeOffset requestedUtc)
    {
        MaterialId = materialId;
        RequestedByUserId = requestedByUserId;
        Reason = UnitOfMeasure.Required(reason, nameof(reason), 500);
        HasStock = hasStock;
        Status = MaterialDeletionStatus.Pending;
        RequestedUtc = requestedUtc;
    }

    public Guid MaterialId { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public bool HasStock { get; private set; }

    public MaterialDeletionStatus Status { get; private set; }

    public DateTimeOffset RequestedUtc { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedUtc { get; private set; }

    public void Approve(Guid byUserId, DateTimeOffset utc)
    {
        EnsurePending();
        if (byUserId == RequestedByUserId)
        {
            throw new InvalidOperationException(
                "La aprobación 4 ojos requiere un segundo usuario.");
        }

        Status = MaterialDeletionStatus.Approved;
        DecidedByUserId = byUserId;
        DecidedUtc = utc;
    }

    public void Reject(Guid byUserId, DateTimeOffset utc)
    {
        EnsurePending();
        Status = MaterialDeletionStatus.Rejected;
        DecidedByUserId = byUserId;
        DecidedUtc = utc;
    }

    private void EnsurePending()
    {
        if (Status != MaterialDeletionStatus.Pending)
        {
            throw new InvalidOperationException(
                "La solicitud ya fue decidida.");
        }
    }
}
