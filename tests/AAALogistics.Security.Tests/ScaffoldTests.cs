using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Identity;
using AAALogistics.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AAALogistics.Security.Tests;

public sealed class PasswordHasherTests
{
    private static readonly IOptions<PasswordHasherOptions> FallbackOptions =
        Options.Create(new PasswordHasherOptions());

    [Fact]
    public void Argon2id_Hash_Verifies_And_Rejects_Wrong_Password()
    {
        using var hasher = CreateHasher();
        var user = new ApplicationUser();
        var hash = hasher.HashPassword(user, "a sufficiently long password");

        Assert.StartsWith("$argon2id$", hash, StringComparison.Ordinal);
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(
                user,
                hash,
                "a sufficiently long password"));
        Assert.Equal(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(user, hash, "incorrect password"));
    }

    [Fact]
    public void Valid_Identity_Pbkdf2_Hash_Requests_Rehash()
    {
        var user = new ApplicationUser();
        var pbkdf2 = new PasswordHasher<ApplicationUser>(FallbackOptions);
        var legacyHash = pbkdf2.HashPassword(user, "legacy password");

        using var hasher = CreateHasher();
        var result = hasher.VerifyHashedPassword(
            user,
            legacyHash,
            "legacy password");

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    private static Argon2PasswordHasher CreateHasher() =>
        new(
            Options.Create(
                new PasswordHashingOptions
                {
                    MemorySizeKb = 19 * 1024,
                    Iterations = 2,
                    DegreeOfParallelism = 1,
                }),
            FallbackOptions);
}
