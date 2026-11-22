using System.Globalization;
using System.Text;
using System.Text.Json;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Auditing;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Documents;
using AAALogistics.Infrastructure.Identity;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Auditing;

internal sealed record PendingAuditChange(
    string TableName,
    string RecordId,
    string Action,
    string? OldJson,
    string? NewJson,
    Guid? PersonId);

public sealed class AuditChainService(
    PersonDataEncryptionService encryptionService,
    IRequestContext requestContext,
    IClock clock)
{
    private const long AdvisoryLockId = 4_141_554_449;

    private readonly PersonDataEncryptionService _encryptionService =
        encryptionService
        ?? throw new ArgumentNullException(nameof(encryptionService));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    internal async Task AppendChangesAsync(
        ApplicationDbContext dbContext,
        IReadOnlyCollection<PendingAuditChange> changes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            return;
        }

        await AcquireLockAsync(dbContext, cancellationToken);
        var head = await GetHeadAsync(dbContext, cancellationToken);

        foreach (var change in changes)
        {
            var oldJson = change.OldJson;
            var newJson = change.NewJson;
            if (change.PersonId is { } personId)
            {
                oldJson = oldJson is null
                    ? null
                    : await _encryptionService.EncryptAsync(
                        dbContext,
                        personId,
                        oldJson,
                        cancellationToken);
                newJson = newJson is null
                    ? null
                    : await _encryptionService.EncryptAsync(
                        dbContext,
                        personId,
                        newJson,
                        cancellationToken);
            }

            Append(
                dbContext,
                head,
                change.TableName,
                change.RecordId,
                change.Action,
                oldJson,
                newJson,
                _requestContext.UserId,
                _requestContext.IpAddress,
                _clock.UtcNow);
        }
    }

    internal async Task AppendExplicitAsync(
        ApplicationDbContext dbContext,
        string tableName,
        string recordId,
        string action,
        string? oldJson,
        string? newJson,
        CancellationToken cancellationToken,
        Guid? userId = null,
        string? ipAddress = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        await AcquireLockAsync(dbContext, cancellationToken);
        var head = await GetHeadAsync(dbContext, cancellationToken);
        Append(
            dbContext,
            head,
            tableName,
            recordId,
            action,
            oldJson,
            newJson,
            userId ?? _requestContext.UserId,
            ipAddress ?? _requestContext.IpAddress,
            _clock.UtcNow);
    }

    private static async Task AcquireLockAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "La cadena de auditoría requiere una transacción activa.");
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({AdvisoryLockId})",
            cancellationToken);
    }

    private static async Task<AuditChainHead> GetHeadAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Always refresh after the advisory lock: a long-lived DbContext may
        // still track a stale head while other connections advanced the chain.
        var head = await dbContext.AuditChainHeads.SingleAsync(
            value => value.Id == AuditChainHead.SingletonId,
            cancellationToken);
        await dbContext.Entry(head).ReloadAsync(cancellationToken);
        return head;
    }

    private static void Append(
        ApplicationDbContext dbContext,
        AuditChainHead head,
        string tableName,
        string recordId,
        string action,
        string? oldJson,
        string? newJson,
        Guid? userId,
        string? ipAddress,
        DateTimeOffset utc)
    {
        // PostgreSQL timestamptz is microsecond-precise; normalize before hashing
        // so verify matches after round-trip persistence.
        var normalizedUtc = TruncateToMicroseconds(utc);
        var unsigned = new AuditChainValue(
            head.LastSequence + 1,
            head.LastHash,
            string.Empty,
            tableName,
            recordId,
            action,
            oldJson,
            newJson,
            userId,
            ipAddress,
            normalizedUtc);
        var value = unsigned with
        {
            EntryHash = AuditChainIntegrity.ComputeHash(unsigned),
        };

        dbContext.AuditLogEntries.Add(new AuditLogEntry(value));
        head.Advance(value.Sequence, value.EntryHash);
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        var ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMicrosecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}

public sealed class AuditService(
    ApplicationDbContext dbContext,
    PersonDataEncryptionService encryptionService,
    AuditChainService chainService,
    IOptions<AuditOptions> auditOptions,
    IRequestContext requestContext,
    IClock clock) : IAuditService
{
    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly PersonDataEncryptionService _encryptionService =
        encryptionService
        ?? throw new ArgumentNullException(nameof(encryptionService));

    private readonly AuditChainService _chainService =
        chainService ?? throw new ArgumentNullException(nameof(chainService));

    private readonly AuditOptions _auditOptions =
        auditOptions?.Value ?? throw new ArgumentNullException(nameof(auditOptions));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<AuditVerificationResult> VerifyAsync(
        CancellationToken cancellationToken = default)
    {
        var persistedEntries = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .OrderBy(value => value.Sequence)
            .ToListAsync(cancellationToken);
        var entries = persistedEntries
            .Select(value => value.ToChainValue())
            .ToList();
        var result = AuditChainIntegrity.Verify(entries);
        if (!result.IsValid)
        {
            return result;
        }

        var head = await _dbContext.AuditChainHeads
            .AsNoTracking()
            .SingleAsync(
                value => value.Id == AuditChainHead.SingletonId,
                cancellationToken);
        var last = entries.LastOrDefault();
        var expectedSequence = last?.Sequence ?? 0;
        var expectedHash = last?.EntryHash ?? AuditChainIntegrity.GenesisHash;
        if (head.LastSequence != expectedSequence
            || !string.Equals(
                head.LastHash,
                expectedHash,
                StringComparison.Ordinal))
        {
            return new AuditVerificationResult(
                false,
                entries.Count,
                head.LastSequence,
                "La cabecera de la cadena no coincide con la última entrada.");
        }

        return result;
    }

    public async Task<IReadOnlyCollection<AuditEntryView>> ListRecentEntriesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var page = await QueryEntriesAsync(
            new AuditEntryQuery(Page: 1, PageSize: Math.Clamp(limit, 1, 500)),
            cancellationToken);
        return page.Items;
    }

    public async Task<AuditEntryPage> QueryEntriesAsync(
        AuditEntryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = Math.Max(1, query.Page);
        // List UI caps at 500 via API; export may request up to 5000.
        var pageSize = Math.Clamp(query.PageSize, 1, 5_000);
        var filtered = await BuildFilteredQueryAsync(query, cancellationToken);
        var total = await filtered.CountAsync(cancellationToken);
        var rows = await filtered
            .OrderByDescending(value => value.Sequence)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userNames = await ResolveUserNamesAsync(
            rows.Select(value => value.UserId).Where(id => id.HasValue).Select(id => id!.Value),
            cancellationToken);

        var items = new List<AuditEntryView>(rows.Count);
        foreach (var row in rows)
        {
            string? oldJson = null;
            string? newJson = null;
            if (query.IncludePayloads)
            {
                oldJson = await RevealPayloadAsync(row.EncryptedOldJson, cancellationToken);
                newJson = await RevealPayloadAsync(row.EncryptedNewJson, cancellationToken);
            }

            userNames.TryGetValue(row.UserId ?? Guid.Empty, out var userName);
            items.Add(
                new AuditEntryView(
                    row.Sequence,
                    row.TableName,
                    row.RecordId,
                    row.Action,
                    row.UserId,
                    userName,
                    row.Ip,
                    row.Utc,
                    oldJson,
                    newJson));
        }

        return new AuditEntryPage(items, total, page, pageSize);
    }

    public async Task<byte[]> ExportEntriesAsync(
        AuditEntryQuery query,
        string format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var exportQuery = query with
        {
            Page = 1,
            PageSize = 5_000,
            IncludePayloads = true,
        };
        var page = await QueryEntriesAsync(exportQuery, cancellationToken);
        var normalized = (format ?? "csv").Trim().ToLowerInvariant();
        return normalized switch
        {
            "json" => Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(
                    page.Items,
                    new JsonSerializerOptions { WriteIndented = true })),
            "csv" => Encoding.UTF8.GetBytes(ToCsv(page.Items)),
            "pdf" => ToAuditPdf(page.Items),
            _ => throw new ArgumentException(
                "Formato de exportación no admitido (use csv, json o pdf).",
                nameof(format)),
        };
    }

    private static byte[] ToAuditPdf(IReadOnlyList<AuditEntryView> items)
    {
        var rows = items.Take(36).Select(entry => (IReadOnlyList<string>)
        [
            entry.Sequence.ToString(CultureInfo.InvariantCulture),
            entry.TableName,
            entry.Action,
            entry.UserName ?? entry.UserId?.ToString("N")[..8] ?? "—",
            entry.Utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            entry.RecordId.Length > 18 ? entry.RecordId[..18] + "…" : entry.RecordId,
        ]).ToArray();
        var footer = new List<string>
        {
            $"Entradas en extracto: {Math.Min(36, items.Count)} de {items.Count} (máx. exportación 5000).",
        };
        if (items.Count > 36)
        {
            footer.Add("Para el listado completo exporte CSV o JSON.");
        }

        return ReportPdf.Render(
            "EXTRACTO DE AUDITORÍA",
            [$"Generado: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC"],
            ["Seq", "Tabla", "Acción", "Usuario", "Cuándo", "Registro"],
            [42, 110, 70, 90, 100, 120],
            [true, false, false, false, false, false],
            rows,
            footer);
    }

    public async Task<AuditCheckpointView> CreateCheckpointAsync(
        string? note,
        CancellationToken cancellationToken = default)
    {
        var verification = await VerifyAsync(cancellationToken);
        if (!verification.IsValid)
        {
            throw new InvalidOperationException(
                "No se puede crear un punto de control con la cadena inválida: "
                + verification.Message);
        }

        var head = await _dbContext.AuditChainHeads
            .AsNoTracking()
            .SingleAsync(
                value => value.Id == AuditChainHead.SingletonId,
                cancellationToken);

        var directory = ResolveCheckpointDirectory();
        Directory.CreateDirectory(directory);
        var stamp = _clock.UtcNow.UtcDateTime.ToString(
            "yyyyMMddTHHmmss",
            CultureInfo.InvariantCulture);
        var fileName = $"audit-checkpoint-{stamp}-seq{head.LastSequence}.json";
        var fullPath = Path.Combine(directory, fileName);

        var entries = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .OrderBy(value => value.Sequence)
            .Select(value => new
            {
                value.Sequence,
                value.PrevHash,
                value.EntryHash,
                value.TableName,
                value.RecordId,
                value.Action,
                value.UserId,
                value.Ip,
                value.Utc,
            })
            .ToListAsync(cancellationToken);

        var manifest = new
        {
            createdUtc = _clock.UtcNow,
            throughSequence = head.LastSequence,
            throughHash = head.LastHash,
            entryCount = entries.Count,
            verification = verification.Message,
            entries,
        };
        await File.WriteAllTextAsync(
            fullPath,
            JsonSerializer.Serialize(
                manifest,
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        var checkpoint = new AuditCheckpoint(
            Guid.CreateVersion7(),
            _clock.UtcNow,
            head.LastSequence,
            head.LastHash,
            fullPath,
            _requestContext.UserId,
            note);
        _dbContext.AuditCheckpoints.Add(checkpoint);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToCheckpointView(checkpoint);
    }

    public async Task<IReadOnlyList<AuditCheckpointView>> ListCheckpointsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.AuditCheckpoints
            .AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToCheckpointView).ToList();
    }

    public async Task<AuditRetentionStatus> GetRetentionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var days = Math.Max(1, _auditOptions.RetentionDays);
        var cutoff = _clock.UtcNow.AddDays(-days);
        var older = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .CountAsync(value => value.Utc < cutoff, cancellationToken);
        var latest = await _dbContext.AuditCheckpoints
            .AsNoTracking()
            .OrderByDescending(value => value.ThroughSequence)
            .Select(value => (long?)value.ThroughSequence)
            .FirstOrDefaultAsync(cancellationToken);
        var blocked = older > 0 && latest is null;
        var message = blocked
            ? "Hay entradas fuera de retención sin punto de control; el purgado físico está bloqueado."
            : older == 0
                ? "No hay entradas fuera del plazo de retención."
                : latest is null
                    ? "Cree un punto de control antes de cualquier purga."
                    : $"Hay {older} entradas anteriores a {days} días; el último punto de control cubre hasta la secuencia {latest}.";
        return new AuditRetentionStatus(
            days,
            older,
            latest,
            blocked || (older > 0 && latest is null),
            message);
    }

    public async Task CryptoShredPersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _encryptionService.DestroyKeyAsync(
            _dbContext,
            personId,
            cancellationToken);
        await _chainService.AppendExplicitAsync(
            _dbContext,
            "person_data_keys",
            personId.ToString("D"),
            "CRYPTO_SHRED",
            null,
            "{\"keyMaterial\":\"destroyed\"}",
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task<string?> TryDecryptPersonPayloadAsync(
        Guid personId,
        string payload,
        CancellationToken cancellationToken = default) =>
        _encryptionService.TryDecryptAsync(
            _dbContext,
            personId,
            payload,
            cancellationToken);

    private async Task<IQueryable<AuditLogEntry>> BuildFilteredQueryAsync(
        AuditEntryQuery query,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditLogEntry> filtered = _dbContext.AuditLogEntries.AsNoTracking();
        if (query.FromUtc is { } fromUtc)
        {
            filtered = filtered.Where(value => value.Utc >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            filtered = filtered.Where(value => value.Utc <= toUtc);
        }

        if (query.UserId is { } userId)
        {
            filtered = filtered.Where(value => value.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            var needle = query.UserName.Trim().ToUpperInvariant();
            var matchingIds = await _dbContext.Users
                .AsNoTracking()
                .Where(user =>
                    user.NormalizedUserName != null
                    && user.NormalizedUserName.Contains(needle))
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
            filtered = filtered.Where(value =>
                value.UserId.HasValue && matchingIds.Contains(value.UserId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.TableName))
        {
            var table = query.TableName.Trim();
            filtered = filtered.Where(value => value.TableName == table);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            filtered = filtered.Where(value => value.Action == action);
        }

        filtered = ApplyAreaFilter(filtered, query.Area);
        return filtered;
    }

    private static IQueryable<AuditLogEntry> ApplyAreaFilter(
        IQueryable<AuditLogEntry> filtered,
        string? area)
    {
        if (!AuditAreaCatalog.TryResolveTables(area, out var exactTables, out var prefixes))
        {
            return filtered;
        }

        var exact = exactTables.ToArray();
        if (prefixes.Count == 0)
        {
            return filtered.Where(value => exact.Contains(value.TableName));
        }

        // Prefix lists are small and known; expand to StartsWith so EF can translate.
        return area!.Trim().ToLowerInvariant() switch
        {
            AuditAreaCatalog.Inventario => filtered.Where(value =>
                exact.Contains(value.TableName)
                || value.TableName.StartsWith("stock_")),
            AuditAreaCatalog.StudioSistema => filtered.Where(value =>
                exact.Contains(value.TableName)
                || value.TableName.StartsWith("studio_")
                || value.TableName.StartsWith("gl_")
                || value.TableName.StartsWith("journal_")
                || value.TableName.StartsWith("bom_")
                || value.TableName.StartsWith("qm_")
                || value.TableName.StartsWith("hr_")
                || value.TableName.StartsWith("plugin_")
                || value.TableName.StartsWith("approval_")
                || value.TableName.StartsWith("connector_")
                || value.TableName.StartsWith("rfq_")
                || value.TableName.StartsWith("rma_")
                || value.TableName.StartsWith("mensajes_")
                || value.TableName.StartsWith("mapeos_")
                || value.TableName.StartsWith("intercambios_")),
            _ => filtered.Where(value => exact.Contains(value.TableName)),
        };
    }

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.UserName ?? user.Id.ToString("D"),
                cancellationToken);
    }

    private async Task<string?> RevealPayloadAsync(
        string? payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        var separator = payload.IndexOf(':', StringComparison.Ordinal);
        if (separator == 32
            && Guid.TryParseExact(payload.AsSpan(0, separator), "N", out var personId))
        {
            var decrypted = await _encryptionService.TryDecryptAsync(
                _dbContext,
                personId,
                payload,
                cancellationToken);
            return decrypted ?? "[cifrado — clave no disponible o shred]";
        }

        return payload;
    }

    private string ResolveCheckpointDirectory()
    {
        var configured = string.IsNullOrWhiteSpace(_auditOptions.CheckpointDirectory)
            ? Path.Combine("Data", "audit-checkpoints")
            : _auditOptions.CheckpointDirectory;
        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(configured);
    }

    private static AuditCheckpointView ToCheckpointView(AuditCheckpoint checkpoint) =>
        new(
            checkpoint.Id,
            checkpoint.CreatedUtc,
            checkpoint.ThroughSequence,
            checkpoint.ThroughHash,
            checkpoint.ExternalPath,
            checkpoint.CreatedByUserId,
            checkpoint.Note);

    private static string ToCsv(IReadOnlyList<AuditEntryView> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "secuencia,tabla,registro,accion,usuario_id,usuario,ip,utc,antes,despues");
        foreach (var item in items)
        {
            builder.Append(Csv(item.Sequence.ToString(CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(Csv(item.TableName));
            builder.Append(',');
            builder.Append(Csv(item.RecordId));
            builder.Append(',');
            builder.Append(Csv(item.Action));
            builder.Append(',');
            builder.Append(Csv(item.UserId?.ToString("D")));
            builder.Append(',');
            builder.Append(Csv(item.UserName));
            builder.Append(',');
            builder.Append(Csv(item.Ip));
            builder.Append(',');
            builder.Append(Csv(item.Utc.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(Csv(item.OldJson));
            builder.Append(',');
            builder.Append(Csv(item.NewJson));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
