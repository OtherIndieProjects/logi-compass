using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AAALogistics.Infrastructure.Security;

public static class ProofOfWork
{
    public static string CreateChallenge() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public static bool Verify(
        string challenge,
        string nonce,
        int difficultyBits)
    {
        if (string.IsNullOrWhiteSpace(challenge)
            || string.IsNullOrWhiteSpace(nonce)
            || difficultyBits <= 0)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{challenge}:{nonce}"));
        var hash = SHA256.HashData(payload);
        var bits = difficultyBits;
        var index = 0;
        while (bits >= 8)
        {
            if (hash[index] != 0)
            {
                return false;
            }

            index++;
            bits -= 8;
        }

        if (bits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - bits));
        return (hash[index] & mask) == 0;
    }
}
