using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Integrations;

public enum SapOutboxStatus
{
    Pending = 0,
    InFlight = 1,
    Succeeded = 2,
    Failed = 3,
}

public sealed class MensajeOutbox : Entity
{
    private MensajeOutbox()
    {
    }

    public MensajeOutbox(
        string messageType,
        string payloadJson,
        string idempotencyKey,
        DateTimeOffset createdUtc)
    {
        MessageType = Require(messageType, 80);
        PayloadJson = Require(payloadJson, 64_000);
        IdempotencyKey = Require(idempotencyKey, 120);
        Status = SapOutboxStatus.Pending;
        AttemptCount = 0;
        CreatedUtc = createdUtc;
        UpdatedUtc = createdUtc;
    }

    public string MessageType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public SapOutboxStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset UpdatedUtc { get; private set; }

    public DateTimeOffset? NextAttemptUtc { get; private set; }

    public void MarkInFlight(DateTimeOffset utc)
    {
        Status = SapOutboxStatus.InFlight;
        AttemptCount++;
        UpdatedUtc = utc;
        LastError = null;
    }

    public void MarkSucceeded(DateTimeOffset utc)
    {
        Status = SapOutboxStatus.Succeeded;
        UpdatedUtc = utc;
        NextAttemptUtc = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset utc, TimeSpan retryDelay)
    {
        Status = SapOutboxStatus.Failed;
        LastError = Truncate(error, 1000);
        UpdatedUtc = utc;
        NextAttemptUtc = utc.Add(retryDelay);
    }

    public void Requeue(DateTimeOffset utc)
    {
        Status = SapOutboxStatus.Pending;
        UpdatedUtc = utc;
        NextAttemptUtc = utc;
    }

    private static string Require(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Valor obligatorio.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

public sealed class MapeoIntegracion : Entity
{
    private MapeoIntegracion()
    {
    }

    public MapeoIntegracion(
        string systemCode,
        string entityType,
        string externalKey,
        Guid internalId,
        DateTimeOffset mappedUtc)
    {
        SystemCode = Require(systemCode, 40);
        EntityType = Require(entityType, 40);
        ExternalKey = Require(externalKey, 120);
        InternalId = internalId == Guid.Empty
            ? throw new ArgumentException("InternalId vacío.")
            : internalId;
        MappedUtc = mappedUtc;
    }

    public string SystemCode { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string ExternalKey { get; private set; } = string.Empty;

    public Guid InternalId { get; private set; }

    public DateTimeOffset MappedUtc { get; private set; }

    public void Touch(Guid internalId, DateTimeOffset utc)
    {
        InternalId = internalId == Guid.Empty
            ? throw new ArgumentException("InternalId vacío.")
            : internalId;
        MappedUtc = utc;
    }

    private static string Require(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Valor obligatorio.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

public sealed class IntercambioExterno : Entity
{
    private IntercambioExterno()
    {
    }

    public IntercambioExterno(
        string direction,
        string messageType,
        string correlationKey,
        string summary,
        DateTimeOffset occurredUtc,
        bool success,
        string? detail = null)
    {
        Direction = Require(direction, 20);
        MessageType = Require(messageType, 80);
        CorrelationKey = Require(correlationKey, 120);
        Summary = Require(summary, 250);
        Detail = string.IsNullOrWhiteSpace(detail) ? null : Truncate(detail.Trim(), 2000);
        OccurredUtc = occurredUtc;
        Success = success;
    }

    public string Direction { get; private set; } = string.Empty;

    public string MessageType { get; private set; } = string.Empty;

    public string CorrelationKey { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string? Detail { get; private set; }

    public DateTimeOffset OccurredUtc { get; private set; }

    public bool Success { get; private set; }

    private static string Require(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Valor obligatorio.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
