using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Auditing;

public sealed class AuditLogEntry
{
    private AuditLogEntry()
    {
    }

    internal AuditLogEntry(AuditChainValue value)
    {
        Sequence = value.Sequence;
        PrevHash = value.PrevHash;
        EntryHash = value.EntryHash;
        TableName = value.TableName;
        RecordId = value.RecordId;
        Action = value.Action;
        EncryptedOldJson = value.EncryptedOldJson;
        EncryptedNewJson = value.EncryptedNewJson;
        UserId = value.UserId;
        Ip = value.Ip;
        Utc = value.Utc;
    }

    public long Sequence { get; private set; }

    public string PrevHash { get; private set; } = string.Empty;

    public string EntryHash { get; private set; } = string.Empty;

    public string TableName { get; private set; } = string.Empty;

    public string RecordId { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string? EncryptedOldJson { get; private set; }

    public string? EncryptedNewJson { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Ip { get; private set; }

    public DateTimeOffset Utc { get; private set; }

    public AuditChainValue ToChainValue() =>
        new(
            Sequence,
            PrevHash,
            EntryHash,
            TableName,
            RecordId,
            Action,
            EncryptedOldJson,
            EncryptedNewJson,
            UserId,
            Ip,
            Utc);
}

public sealed class AuditChainHead
{
    public const int SingletonId = 1;

    private AuditChainHead()
    {
    }

    public AuditChainHead(int id)
    {
        if (id != SingletonId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(id),
                id,
                "Solo puede existir una cabecera de auditoría.");
        }

        Id = id;
        LastHash = AuditChainIntegrity.GenesisHash;
    }

    public int Id { get; private set; }

    public long LastSequence { get; private set; }

    public string LastHash { get; private set; } = string.Empty;

    internal void Advance(long sequence, string hash)
    {
        if (sequence != LastSequence + 1)
        {
            throw new InvalidOperationException(
                "La secuencia de auditoría no es consecutiva.");
        }

        LastSequence = sequence;
        LastHash = hash;
    }
}

/// <summary>
/// Punto de control de auditoría: ancla de secuencia/hash
/// con manifiesto externo junto a backups. No reescribe la cadena.
/// </summary>
public sealed class AuditCheckpoint
{
    private AuditCheckpoint()
    {
    }

    public AuditCheckpoint(
        Guid id,
        DateTimeOffset createdUtc,
        long throughSequence,
        string throughHash,
        string externalPath,
        Guid? createdByUserId,
        string? note)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador es obligatorio.", nameof(id));
        }

        if (throughSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(throughSequence));
        }

        ThroughHash = string.IsNullOrWhiteSpace(throughHash)
            ? throw new ArgumentException("El hash es obligatorio.", nameof(throughHash))
            : throughHash;
        ExternalPath = string.IsNullOrWhiteSpace(externalPath)
            ? throw new ArgumentException("La ruta externa es obligatoria.", nameof(externalPath))
            : externalPath;
        Id = id;
        CreatedUtc = createdUtc;
        ThroughSequence = throughSequence;
        CreatedByUserId = createdByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid Id { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public long ThroughSequence { get; private set; }

    public string ThroughHash { get; private set; } = string.Empty;

    public string ExternalPath { get; private set; } = string.Empty;

    public Guid? CreatedByUserId { get; private set; }

    public string? Note { get; private set; }
}

public sealed class PersonDataKey
{
    private PersonDataKey()
    {
    }

    internal PersonDataKey(
        Guid personId,
        string keyMaterial,
        DateTimeOffset createdUtc)
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException(
                "La persona es obligatoria.",
                nameof(personId));
        }

        PersonId = personId;
        KeyMaterial = string.IsNullOrWhiteSpace(keyMaterial)
            ? throw new ArgumentException(
                "El material de clave es obligatorio.",
                nameof(keyMaterial))
            : keyMaterial;
        CreatedUtc = createdUtc;
    }

    public Guid PersonId { get; private set; }

    /// <summary>
    /// Per-person key wrapped with the installation master key.
    /// </summary>
    public string? KeyMaterial { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset? DestroyedUtc { get; private set; }

    internal void Destroy(DateTimeOffset utc)
    {
        KeyMaterial = null;
        DestroyedUtc = utc;
    }
}

public enum BackupStatus
{
    Running,
    Completed,
    Failed,
}

public sealed class BackupRecord : Entity
{
    private BackupRecord()
    {
    }

    internal BackupRecord(string fileName, DateTimeOffset startedUtc)
    {
        FileName = string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException(
                "El nombre del backup es obligatorio.",
                nameof(fileName))
            : fileName;
        StartedUtc = startedUtc;
        Status = BackupStatus.Running;
    }

    public string FileName { get; private set; } = string.Empty;

    public BackupStatus Status { get; private set; }

    public long? SizeBytes { get; private set; }

    public string? Sha256 { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset StartedUtc { get; private set; }

    public DateTimeOffset? CompletedUtc { get; private set; }

    internal void Complete(
        long sizeBytes,
        string sha256,
        DateTimeOffset completedUtc)
    {
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        CompletedUtc = completedUtc;
        Status = BackupStatus.Completed;
    }

    internal void Fail(string error, DateTimeOffset completedUtc)
    {
        Error = error;
        CompletedUtc = completedUtc;
        Status = BackupStatus.Failed;
    }
}

public sealed record AuditChainValue(
    long Sequence,
    string PrevHash,
    string EntryHash,
    string TableName,
    string RecordId,
    string Action,
    string? EncryptedOldJson,
    string? EncryptedNewJson,
    Guid? UserId,
    string? Ip,
    DateTimeOffset Utc);

public sealed record AuditVerificationResult(
    bool IsValid,
    long EntryCount,
    long? InvalidSequence,
    string Message);

public static class AuditChainIntegrity
{
    public const string GenesisHash =
        "0000000000000000000000000000000000000000000000000000000000000000";

    public static string ComputeHash(AuditChainValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var payload = string.Join(
            "|",
            Encode(value.TableName),
            Encode(value.RecordId),
            Encode(value.Action),
            Encode(value.EncryptedOldJson),
            Encode(value.EncryptedNewJson),
            value.UserId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty,
            Encode(value.Ip),
            value.Utc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture));
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{value.Sequence}|{value.PrevHash}|{payload}");

        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static AuditVerificationResult Verify(
        IEnumerable<AuditChainValue> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var expectedSequence = 1L;
        var expectedPrevHash = GenesisHash;
        long count = 0;

        foreach (var entry in entries.OrderBy(value => value.Sequence))
        {
            count++;
            if (entry.Sequence != expectedSequence)
            {
                return new AuditVerificationResult(
                    false,
                    count,
                    entry.Sequence,
                    "La secuencia no es consecutiva.");
            }

            if (!string.Equals(
                    entry.PrevHash,
                    expectedPrevHash,
                    StringComparison.Ordinal))
            {
                return new AuditVerificationResult(
                    false,
                    count,
                    entry.Sequence,
                    "El enlace con el hash anterior no coincide.");
            }

            var computed = ComputeHash(entry);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(computed),
                    Encoding.ASCII.GetBytes(entry.EntryHash)))
            {
                return new AuditVerificationResult(
                    false,
                    count,
                    entry.Sequence,
                    "El hash de la entrada no coincide.");
            }

            expectedPrevHash = entry.EntryHash;
            expectedSequence++;
        }

        return new AuditVerificationResult(
            true,
            count,
            null,
            "La cadena de auditoría es válida.");
    }

    private static string Encode(string? value) =>
        value is null
            ? "-"
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
