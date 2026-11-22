using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Operations;

public enum IdempotencyStatus
{
    Processing,
    Completed,
}

public sealed class IdempotencyRecord : Entity
{
    private IdempotencyRecord()
    {
    }

    internal IdempotencyRecord(
        string commandType,
        string key,
        string requestHash,
        DateTimeOffset createdUtc)
    {
        CommandType = UnitOfMeasure.Required(
            commandType,
            nameof(commandType),
            80);
        Key = UnitOfMeasure.Required(key, nameof(key), 200);
        RequestHash = UnitOfMeasure.Required(
            requestHash,
            nameof(requestHash),
            64);
        CreatedUtc = createdUtc;
        Status = IdempotencyStatus.Processing;
    }

    public string CommandType { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public IdempotencyStatus Status { get; private set; }

    public string? ResultJson { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset? CompletedUtc { get; private set; }

    internal void Complete(string resultJson, DateTimeOffset completedUtc)
    {
        if (Status == IdempotencyStatus.Completed)
        {
            throw new InvalidOperationException(
                "La operación idempotente ya está completada.");
        }

        ResultJson = UnitOfMeasure.Required(
            resultJson,
            nameof(resultJson),
            32_000);
        CompletedUtc = completedUtc;
        Status = IdempotencyStatus.Completed;
    }
}
