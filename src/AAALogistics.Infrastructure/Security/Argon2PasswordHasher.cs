using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Identity;
using Konscious.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Security;

public sealed class Argon2PasswordHasher :
    IPasswordHasher<ApplicationUser>,
    IDisposable
{
    private const int SaltLength = 16;
    private const int HashLength = 32;
    private const int Argon2Version = 19;
    private const int MinimumMemorySizeKb = 19 * 1024;
    private const int MaximumMemorySizeKb = 256 * 1024;
    private const int MaximumIterations = 10;
    private const int MaximumParallelism = 16;
    private const string Prefix = "$argon2id$";

    private readonly PasswordHashingOptions _options;
    private readonly PasswordHasher<ApplicationUser> _pbkdf2Fallback;
    private readonly SemaphoreSlim _concurrencyGate;

    public Argon2PasswordHasher(
        IOptions<PasswordHashingOptions> options,
        IOptions<PasswordHasherOptions> fallbackOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fallbackOptions);

        _options = options.Value;
        _pbkdf2Fallback = new PasswordHasher<ApplicationUser>(fallbackOptions);
        _concurrencyGate = new SemaphoreSlim(
            _options.MaxConcurrentOperations,
            _options.MaxConcurrentOperations);
    }

    public string HashPassword(ApplicationUser user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            var hash = DeriveKey(
                passwordBytes,
                salt,
                _options.MemorySizeKb,
                _options.Iterations,
                _options.DegreeOfParallelism);

            return string.Create(
                CultureInfo.InvariantCulture,
                $"$argon2id$v={Argon2Version}$m={_options.MemorySizeKb},t={_options.Iterations},p={_options.DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public PasswordVerificationResult VerifyHashedPassword(
        ApplicationUser user,
        string hashedPassword,
        string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(hashedPassword);
        ArgumentNullException.ThrowIfNull(providedPassword);

        if (!hashedPassword.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var fallbackResult = _pbkdf2Fallback.VerifyHashedPassword(
                user,
                hashedPassword,
                providedPassword);

            return fallbackResult == PasswordVerificationResult.Failed
                ? PasswordVerificationResult.Failed
                : PasswordVerificationResult.SuccessRehashNeeded;
        }

        return VerifyArgon2id(hashedPassword, providedPassword);
    }

    public void Dispose()
    {
        _concurrencyGate.Dispose();
    }

    private PasswordVerificationResult VerifyArgon2id(
        string encodedHash,
        string providedPassword)
    {
        var parts = encodedHash.Split('$', StringSplitOptions.None);
        if (parts.Length != 6
            || !string.Equals(parts[1], "argon2id", StringComparison.Ordinal)
            || !string.Equals(parts[2], $"v={Argon2Version}", StringComparison.Ordinal)
            || !TryParseParameters(parts[3], out var memorySizeKb, out var iterations, out var parallelism)
            || !ParametersAreSafe(memorySizeKb, iterations, parallelism))
        {
            return PasswordVerificationResult.Failed;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expectedHash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }

        if (salt.Length is < SaltLength or > 64
            || expectedHash.Length is < 16 or > 64)
        {
            return PasswordVerificationResult.Failed;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(providedPassword);
        byte[]? actualHash = null;

        try
        {
            actualHash = DeriveKey(
                passwordBytes,
                salt,
                memorySizeKb,
                iterations,
                parallelism,
                expectedHash.Length);

            if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                return PasswordVerificationResult.Failed;
            }

            return memorySizeKb == _options.MemorySizeKb
                && iterations == _options.Iterations
                && parallelism == _options.DegreeOfParallelism
                    ? PasswordVerificationResult.Success
                    : PasswordVerificationResult.SuccessRehashNeeded;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            if (actualHash is not null)
            {
                CryptographicOperations.ZeroMemory(actualHash);
            }
        }
    }

    private byte[] DeriveKey(
        byte[] password,
        byte[] salt,
        int memorySizeKb,
        int iterations,
        int parallelism,
        int outputLength = HashLength)
    {
        _concurrencyGate.Wait();
        try
        {
            var argon2 = new Argon2id(password)
            {
                Salt = salt,
                MemorySize = memorySizeKb,
                Iterations = iterations,
                DegreeOfParallelism = parallelism,
            };

            return argon2.GetBytes(outputLength);
        }
        finally
        {
            _concurrencyGate.Release();
        }
    }

    private static bool TryParseParameters(
        string encodedParameters,
        out int memorySizeKb,
        out int iterations,
        out int parallelism)
    {
        memorySizeKb = 0;
        iterations = 0;
        parallelism = 0;

        foreach (var parameter in encodedParameters.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = parameter.Split('=', 2, StringSplitOptions.None);
            if (pair.Length != 2
                || !int.TryParse(
                    pair[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return false;
            }

            switch (pair[0])
            {
                case "m" when memorySizeKb == 0:
                    memorySizeKb = value;
                    break;
                case "t" when iterations == 0:
                    iterations = value;
                    break;
                case "p" when parallelism == 0:
                    parallelism = value;
                    break;
                default:
                    return false;
            }
        }

        return memorySizeKb > 0 && iterations > 0 && parallelism > 0;
    }

    private static bool ParametersAreSafe(
        int memorySizeKb,
        int iterations,
        int parallelism) =>
        memorySizeKb is >= MinimumMemorySizeKb and <= MaximumMemorySizeKb
        && iterations is >= 1 and <= MaximumIterations
        && parallelism is >= 1 and <= MaximumParallelism;
}
