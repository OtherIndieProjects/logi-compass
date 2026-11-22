using System.Security.Cryptography;

namespace AAALogistics.Infrastructure.Backups;

internal static class BackupEncryption
{
    private const int KeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;

    public static byte[] LoadOrCreateKey(string configuredPath)
    {
        var path = Path.GetFullPath(configuredPath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException(
                "Backups:EncryptionKeyPath no contiene un directorio válido.");
        Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                directory,
                UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute);
        }

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
                // concurrent create
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

        var key = File.ReadAllBytes(path);
        if (key.Length != KeyLength)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException(
                "La clave de cifrado de backups debe tener exactamente 32 bytes.");
        }

        return key;
    }

    public static async Task EncryptFileAsync(
        string plainPath,
        string cipherPath,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var plainBytes = await File.ReadAllBytesAsync(plainPath, cancellationToken);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(key, TagLength))
        {
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        CryptographicOperations.ZeroMemory(plainBytes);

        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode =
                UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        await using (var stream = new FileStream(cipherPath, options))
        {
            await stream.WriteAsync(nonce, cancellationToken);
            await stream.WriteAsync(tag, cancellationToken);
            await stream.WriteAsync(cipherBytes, cancellationToken);
        }

        CryptographicOperations.ZeroMemory(cipherBytes);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                cipherPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public static async Task DecryptFileAsync(
        string cipherPath,
        string plainPath,
        byte[] key,
        CancellationToken cancellationToken)
    {
        var payload = await File.ReadAllBytesAsync(cipherPath, cancellationToken);
        if (payload.Length < NonceLength + TagLength + 1)
        {
            throw new InvalidOperationException(
                "El archivo cifrado de backup está truncado o no es válido.");
        }

        var nonce = payload.AsSpan(0, NonceLength);
        var tag = payload.AsSpan(NonceLength, TagLength);
        var cipher = payload.AsSpan(NonceLength + TagLength);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(key, TagLength))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            Mode = FileMode.Create,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode =
                UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        await using (var stream = new FileStream(plainPath, options))
        {
            await stream.WriteAsync(plain, cancellationToken);
        }

        CryptographicOperations.ZeroMemory(plain);
    }
}
