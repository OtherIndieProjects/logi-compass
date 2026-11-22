using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using AAALogistics.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AAALogistics.Integration.Tests;

/// <summary>
/// Identity's authenticator provider validates RFC 6238 (step 30s) but
/// <c>GenerateAsync</c> returns an empty string. Tests mint a code from the
/// stored Base32 key the same way <c>AuthenticatorTokenProvider.ValidateAsync</c>
/// checks it.
/// </summary>
internal static class TestTotp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static async Task<string> IssueAsync(
        IServiceProvider services,
        string userName)
    {
        await using var scope = services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(userName)
            ?? throw new InvalidOperationException(
                $"No existe el usuario '{userName}'.");
        var sharedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(sharedKey))
        {
            throw new InvalidOperationException(
                "El administrador no tiene clave TOTP.");
        }

        var timestep = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        var code = ComputeTotp(FromBase32(sharedKey), timestep);
        return code.ToString("D6", CultureInfo.InvariantCulture);
    }

    private static int ComputeTotp(byte[] key, ulong timestepNumber)
    {
        Span<byte> counter = stackalloc byte[8];
        BitConverter.TryWriteBytes(
            counter,
            IPAddress.HostToNetworkOrder((long)timestepNumber));
        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.TryHashData(key, counter, hash, out _);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        return binary % 1_000_000;
    }

    private static byte[] FromBase32(string input)
    {
        var span = input.AsSpan().TrimEnd('=');
        var output = new byte[span.Length * 5 / 8];
        var bitIndex = 0;
        var charIndex = 0;
        var outputBits = 0;
        var outputIndex = 0;
        while (outputIndex < output.Length)
        {
            var alphabetIndex = Base32Alphabet.IndexOf(
                char.ToUpperInvariant(span[charIndex]));
            if (alphabetIndex < 0)
            {
                throw new FormatException("La clave TOTP no es Base32.");
            }

            var bits = Math.Min(5 - bitIndex, 8 - outputBits);
            output[outputIndex] = (byte)(output[outputIndex] << bits);
            output[outputIndex] |= (byte)(alphabetIndex >> (5 - (bitIndex + bits)));
            bitIndex += bits;
            if (bitIndex >= 5)
            {
                charIndex++;
                bitIndex = 0;
            }

            outputBits += bits;
            if (outputBits >= 8)
            {
                outputIndex++;
                outputBits = 0;
            }
        }

        return output;
    }
}
