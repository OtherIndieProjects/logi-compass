using System.Security.Cryptography;
using System.Text;
using AAALogistics.Application.Abstractions;
using AAALogistics.Domain.Auditing;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Auditing;

public sealed class PiiMasterKeyProvider
{
    private const int KeyLength = 32;
    private readonly byte[] _key;

    public PiiMasterKeyProvider(IOptions<AuditOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _key = LoadOrCreate(options.Value.PiiMasterKeyPath);
    }

    internal string Protect(Guid personId, ReadOnlySpan<byte> plaintext) =>
        Encrypt(
            "k1",
            _key,
            plaintext,
            Encoding.UTF8.GetBytes($"person-key:{personId:N}"));

    internal byte[] Unprotect(Guid personId, string envelope) =>
        Decrypt(
            "k1",
            _key,
            envelope,
            Encoding.UTF8.GetBytes($"person-key:{personId:N}"));

    private static byte[] LoadOrCreate(string configuredPath)
    {
        var path = Path.GetFullPath(configuredPath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "Audit:PiiMasterKeyPath no contiene un directorio válido.");
        Directory.CreateDirectory(directory);

        if (!File.Exists(path))
        {
            var generated = RandomNumberGenerator.GetBytes(KeyLength);
            var options = new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Share = FileShare.None,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode =
                    UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            try
            {
                using var stream = new FileStream(path, options);
                stream.Write(generated);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Otro proceso creó la clave de instalación.
            }
            finally
            {
                CryptographicOperations.ZeroMemory(generated);
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var persisted = File.ReadAllBytes(path);
        if (persisted.Length != KeyLength)
        {
            CryptographicOperations.ZeroMemory(persisted);
            throw new InvalidOperationException(
                "La clave maestra de PII debe tener exactamente 32 bytes.");
        }

        return persisted;
    }

    internal static string Encrypt(
        string version,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(key, tag.Length))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }

        return string.Join(
            '.',
            version,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(tag),
            Convert.ToBase64String(ciphertext));
    }

    internal static byte[] Decrypt(
        string expectedVersion,
        ReadOnlySpan<byte> key,
        string envelope,
        ReadOnlySpan<byte> associatedData)
    {
        var parts = envelope.Split('.', StringSplitOptions.None);
        if (parts.Length != 4
            || !string.Equals(
                parts[0],
                expectedVersion,
                StringComparison.Ordinal))
        {
            throw new CryptographicException(
                "El sobre cifrado tiene un formato desconocido.");
        }

        var nonce = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var ciphertext = Convert.FromBase64String(parts[3]);
        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, tag.Length))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        }

        return plaintext;
    }
}

public sealed class PersonDataEncryptionService(
    PiiMasterKeyProvider masterKeyProvider,
    IClock clock)
{
    private readonly PiiMasterKeyProvider _masterKeyProvider =
        masterKeyProvider
        ?? throw new ArgumentNullException(nameof(masterKeyProvider));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    internal async Task<string> EncryptAsync(
        ApplicationDbContext dbContext,
        Guid personId,
        string plaintext,
        CancellationToken cancellationToken)
    {
        var key = await GetOrCreateKeyAsync(
            dbContext,
            personId,
            cancellationToken);
        try
        {
            var encrypted = PiiMasterKeyProvider.Encrypt(
                "p1",
                key,
                Encoding.UTF8.GetBytes(plaintext),
                Encoding.UTF8.GetBytes($"audit:{personId:N}"));
            return $"{personId:N}:{encrypted}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal async Task<string?> TryDecryptAsync(
        ApplicationDbContext dbContext,
        Guid personId,
        string payload,
        CancellationToken cancellationToken)
    {
        var separator = payload.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0
            || !Guid.TryParseExact(
                payload.AsSpan(0, separator),
                "N",
                out var payloadPersonId)
            || payloadPersonId != personId)
        {
            return null;
        }

        var personKey = await dbContext.PersonDataKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.PersonId == personId,
                cancellationToken);
        if (personKey?.KeyMaterial is null)
        {
            return null;
        }

        var key = _masterKeyProvider.Unprotect(
            personId,
            personKey.KeyMaterial);
        try
        {
            var plaintext = PiiMasterKeyProvider.Decrypt(
                "p1",
                key,
                payload[(separator + 1)..],
                Encoding.UTF8.GetBytes($"audit:{personId:N}"));
            try
            {
                return Encoding.UTF8.GetString(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    internal async Task DestroyKeyAsync(
        ApplicationDbContext dbContext,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var personKey = await dbContext.PersonDataKeys
            .SingleOrDefaultAsync(
                value => value.PersonId == personId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "No existe una clave de auditoría para la persona.");

        if (personKey.KeyMaterial is not null)
        {
            personKey.Destroy(_clock.UtcNow);
        }
    }

    private async Task<byte[]> GetOrCreateKeyAsync(
        ApplicationDbContext dbContext,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var tracked = dbContext.PersonDataKeys.Local
            .SingleOrDefault(value => value.PersonId == personId);
        var personKey = tracked
            ?? await dbContext.PersonDataKeys.SingleOrDefaultAsync(
                value => value.PersonId == personId,
                cancellationToken);

        if (personKey is not null)
        {
            if (personKey.KeyMaterial is null)
            {
                throw new InvalidOperationException(
                    "Los datos personales ya fueron sometidos a borrado criptográfico.");
            }

            return _masterKeyProvider.Unprotect(
                personId,
                personKey.KeyMaterial);
        }

        var generated = RandomNumberGenerator.GetBytes(32);
        var wrapped = _masterKeyProvider.Protect(personId, generated);
        dbContext.PersonDataKeys.Add(
            new PersonDataKey(personId, wrapped, _clock.UtcNow));
        return generated;
    }
}
