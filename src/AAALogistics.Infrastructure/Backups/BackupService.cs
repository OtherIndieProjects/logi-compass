using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Auditing;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AAALogistics.Infrastructure.Backups;

public sealed class BackupService : IBackupService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly BackupOptions _options;
    private readonly IClock _clock;
    private readonly string _connectionString;

    public BackupService(
        ApplicationDbContext dbContext,
        IOptions<BackupOptions> options,
        IClock clock,
        IConfiguration configuration)
    {
        _dbContext =
            dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _options =
            options?.Value ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _connectionString =
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default es obligatorio.");
    }

    public async Task<BackupInfo> CreateAsync(
        bool gzip,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetFullPath(_options.Directory);
        Directory.CreateDirectory(directory);
        var stem =
            $"aaalogistics-{_clock.UtcNow:yyyyMMdd-HHmmss}-{Guid.CreateVersion7():N}";
        var rawPath = Path.Combine(directory, $"{stem}.dump");
        var usedCopyFallback = false;
        BackupRecord? record = null;

        try
        {
            var dumped = await TryRunPgDumpAsync(rawPath, cancellationToken);
            if (!dumped)
            {
                if (!_options.AllowSqlFallback)
                {
                    throw new InvalidOperationException(
                        "pg_dump no está disponible o no se pudo iniciar. "
                        + "La copia falla cerrado. Instale el cliente de PostgreSQL "
                        + "o establezca Backups:AllowSqlFallback=true solo si acepta "
                        + "el formato SQL COPY.");
                }

                usedCopyFallback = true;
                rawPath = Path.Combine(directory, $"{stem}.sql");
                await RunSqlCopyFallbackAsync(rawPath, cancellationToken);
            }

            var packagedPath = gzip ? $"{rawPath}.gz" : rawPath;
            if (gzip)
            {
                await CompressAsync(rawPath, packagedPath, cancellationToken);
                File.Delete(rawPath);
            }

            var key = BackupEncryption.LoadOrCreateKey(_options.EncryptionKeyPath);
            var finalPath = $"{packagedPath}.enc";
            try
            {
                await BackupEncryption.EncryptFileAsync(
                    packagedPath,
                    finalPath,
                    key,
                    cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                TryDelete(packagedPath);
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    finalPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            record = new BackupRecord(
                Path.GetFileName(finalPath),
                _clock.UtcNow);
            _dbContext.BackupRecords.Add(record);

            var fileInfo = new FileInfo(finalPath);
            await using var stream = new FileStream(
                finalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                useAsync: true);
            var hash = Convert.ToHexStringLower(
                await SHA256.HashDataAsync(stream, cancellationToken));
            record.Complete(fileInfo.Length, hash, _clock.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToInfo(record);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                  || !cancellationToken.IsCancellationRequested)
        {
            TryDelete(rawPath);
            TryDelete($"{rawPath}.gz");
            TryDelete($"{rawPath}.enc");
            TryDelete($"{rawPath}.gz.enc");
            record ??= new BackupRecord(
                Path.GetFileName(rawPath) + (gzip ? ".gz.enc" : ".enc"),
                _clock.UtcNow);
            if (_dbContext.Entry(record).State == EntityState.Detached)
            {
                _dbContext.BackupRecords.Add(record);
            }

            var message = usedCopyFallback
                ? $"SQL COPY fallback falló: {exception.Message}"
                : exception.Message;
            record.Fail(Truncate(message, 2_000), _clock.UtcNow);
            await _dbContext.SaveChangesAsync(CancellationToken.None);
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-BACKUP-0001",
                $"No se pudo crear el backup: {exception.Message}");
        }
    }

    public async Task<IReadOnlyCollection<BackupInfo>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.BackupRecords
            .AsNoTracking()
            .OrderByDescending(value => value.StartedUtc)
            .Select(value => new BackupInfo(
                value.Id,
                value.FileName,
                value.Status,
                value.SizeBytes,
                value.Sha256,
                value.Error,
                value.StartedUtc,
                value.CompletedUtc))
            .ToListAsync(cancellationToken);

    public async Task<RestoreResult> RestoreAsync(
        string dumpPath,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(confirmation, "YES", StringComparison.Ordinal))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-BACKUP-0002",
                "La restauración exige la confirmación exacta YES.");
        }

        if (string.IsNullOrWhiteSpace(dumpPath))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-BACKUP-0003",
                "Indique la ruta del volcado.");
        }

        var fullPath = Path.GetFullPath(dumpPath);
        if (!File.Exists(fullPath))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-BACKUP-0004",
                "El archivo de backup no existe.");
        }

        await EnsureSha256MatchesAsync(fullPath, cancellationToken);

        var cleanup = new List<string>();
        try
        {
            var workingPath = fullPath;
            if (fullPath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
            {
                var key = BackupEncryption.LoadOrCreateKey(_options.EncryptionKeyPath);
                var decrypted = Path.Combine(
                    Path.GetTempPath(),
                    $"aaa-restore-{Guid.CreateVersion7():N}");
                cleanup.Add(decrypted);
                try
                {
                    await BackupEncryption.DecryptFileAsync(
                        fullPath,
                        decrypted,
                        key,
                        cancellationToken);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(key);
                }

                if (fullPath.Contains(".gz.enc", StringComparison.OrdinalIgnoreCase))
                {
                    var ungzip = decrypted + ".dump";
                    cleanup.Add(ungzip);
                    await DecompressAsync(decrypted, ungzip, cancellationToken);
                    workingPath = ungzip;
                }
                else
                {
                    workingPath = decrypted;
                }
            }

            await RunPgRestoreAsync(workingPath, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not LogisticsException
                  and not OperationCanceledException)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-BACKUP-0005",
                $"No se pudo restaurar: {exception.Message}");
        }
        finally
        {
            foreach (var path in cleanup)
            {
                TryDelete(path);
            }
        }

        return new RestoreResult(Path.GetFileName(fullPath), _clock.UtcNow);
    }

    private async Task EnsureSha256MatchesAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(fullPath);
        var record = await _dbContext.BackupRecords
            .AsNoTracking()
            .Where(value => value.FileName == fileName && value.Sha256 != null)
            .OrderByDescending(value => value.StartedUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (record?.Sha256 is null)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-BACKUP-0006",
                "No hay un registro de backup con SHA-256 para ese archivo. "
                + "La restauración falla cerrado.");
        }

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            useAsync: true);
        var hash = Convert.ToHexStringLower(
            await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(hash, record.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-BACKUP-0007",
                "El SHA-256 del archivo no coincide con el registro. "
                + "La restauración falla cerrado.");
        }
    }

    private static async Task DecompressAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            useAsync: true);
        await using var gzip = new GZipStream(source, CompressionMode.Decompress);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            useAsync: true);
        await gzip.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// Returns false when pg_dump is missing or cannot start, so the caller
    /// can fall back to SQL COPY. Returns true when a custom-format dump was
    /// written successfully.
    /// </summary>
    private async Task<bool> TryRunPgDumpAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnectionStringBuilder(_connectionString);
        var processStart = new ProcessStartInfo
        {
            FileName = _options.PgDumpPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        processStart.ArgumentList.Add("--format=custom");
        processStart.ArgumentList.Add("--no-owner");
        processStart.ArgumentList.Add("--no-privileges");
        processStart.ArgumentList.Add($"--file={outputPath}");
        processStart.ArgumentList.Add(
            $"--dbname=host={connection.Host} port={connection.Port} "
            + $"dbname={connection.Database} user={connection.Username}");
        if (!string.IsNullOrEmpty(connection.Password))
        {
            processStart.Environment["PGPASSWORD"] = connection.Password;
        }

        using var process = new Process { StartInfo = processStart };
        try
        {
            if (!process.Start())
            {
                return false;
            }
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(_options.TimeoutMinutes));
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        _ = process.StandardOutput.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            TryDelete(outputPath);
            throw new InvalidOperationException(
                $"pg_dump terminó con código {process.ExitCode}: "
                + Truncate(stderr, 1_500));
        }

        return true;
    }

    private async Task RunPgRestoreAsync(
        string dumpPath,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnectionStringBuilder(_connectionString);
        var processStart = new ProcessStartInfo
        {
            FileName = ResolvePgRestorePath(),
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        processStart.ArgumentList.Add("--clean");
        processStart.ArgumentList.Add("--if-exists");
        processStart.ArgumentList.Add("--no-owner");
        processStart.ArgumentList.Add("--no-privileges");
        processStart.ArgumentList.Add("--exit-on-error");
        processStart.ArgumentList.Add("--single-transaction");
        processStart.ArgumentList.Add(
            $"--dbname=host={connection.Host} port={connection.Port} "
            + $"dbname={connection.Database} user={connection.Username}");
        processStart.ArgumentList.Add(dumpPath);
        if (!string.IsNullOrEmpty(connection.Password))
        {
            processStart.Environment["PGPASSWORD"] = connection.Password;
        }

        using var process = new Process { StartInfo = processStart };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException(
                    "pg_restore no se pudo iniciar. La restauración falla cerrado.");
            }
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "pg_restore no está disponible. La restauración falla cerrado.",
                exception);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(_options.TimeoutMinutes));
        var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
        _ = process.StandardOutput.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"pg_restore terminó con código {process.ExitCode}: "
                + Truncate(stderr, 1_500));
        }
    }

    private string ResolvePgRestorePath()
    {
        var dumpPath = _options.PgDumpPath;
        if (dumpPath.EndsWith("pg_dump", StringComparison.Ordinal))
        {
            return string.Concat(
                dumpPath.AsSpan(0, dumpPath.Length - "pg_dump".Length),
                "pg_restore");
        }

        return "pg_restore";
    }

    /// <summary>
    /// Fallback when pg_dump is unavailable and Backups:AllowSqlFallback is true.
    /// TO STDOUT as plain SQL. Limitation: no roles/extensions/large objects,
    /// and restore is not a drop-in for custom-format dumps.
    /// </summary>
    private async Task RunSqlCopyFallbackAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var writer = new StreamWriter(
            outputPath,
            append: false,
            Encoding.UTF8);
        await writer.WriteLineAsync(
            "-- AAA Logistics SQL COPY fallback backup");
        await writer.WriteLineAsync(
            "-- Limitación: sin roles, extensiones ni objetos grandes; "
            + "usar solo si pg_dump no está disponible.");
        await writer.WriteLineAsync(
            string.Create(
                CultureInfo.InvariantCulture,
                $"-- Generado UTC: {_clock.UtcNow:O}"));
        await writer.WriteLineAsync("BEGIN;");
        await writer.WriteLineAsync("SET session_replication_role = replica;");

        var tables = new List<string>();
        await using (var listCommand = connection.CreateCommand())
        {
            listCommand.CommandText =
                """
                SELECT c.relname
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public'
                  AND c.relkind = 'r'
                ORDER BY c.relname;
                """;
            await using var reader = await listCommand.ExecuteReaderAsync(
                cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quoted = QuoteIdent(table);
            await writer.WriteLineAsync($"-- table {table}");
            await writer.WriteLineAsync($"COPY {quoted} FROM stdin;");
            await using var copy = connection.BeginTextExport(
                $"COPY {quoted} TO STDOUT");
            while (true)
            {
                var line = await copy.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                await writer.WriteLineAsync(line);
            }

            await writer.WriteLineAsync("\\.");
        }

        await writer.WriteLineAsync("SET session_replication_role = DEFAULT;");
        await writer.WriteLineAsync("COMMIT;");
        await writer.FlushAsync(cancellationToken);
    }

    private static string QuoteIdent(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static async Task CompressAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            useAsync: true);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            useAsync: true);
        await using var gzip = new GZipStream(
            destination,
            CompressionLevel.SmallestSize);
        await source.CopyToAsync(gzip, cancellationToken);
    }

    private static BackupInfo ToInfo(BackupRecord value) =>
        new(
            value.Id,
            value.FileName,
            value.Status,
            value.SizeBytes,
            value.Sha256,
            value.Error,
            value.StartedUtc,
            value.CompletedUtc);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // El registro de fallo permanece aunque no se pueda limpiar.
        }
        catch (UnauthorizedAccessException)
        {
            // El registro de fallo permanece aunque no se pueda limpiar.
        }
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length];
}
