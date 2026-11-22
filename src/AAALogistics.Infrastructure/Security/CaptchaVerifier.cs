using AAALogistics.Application.Abstractions;
using AAALogistics.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Security;

public sealed class CaptchaVerifier(IOptions<CaptchaOptions> options) : ICaptchaVerifier
{
    private readonly CaptchaOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public bool IsRequired => _options.RequireProofOfWork;

    public int DifficultyBits => Math.Max(1, _options.DifficultyBits);

    public string CreateChallenge() => ProofOfWork.CreateChallenge();

    public bool Verify(string? challenge, string? nonce) =>
        !IsRequired
        || ProofOfWork.Verify(challenge ?? string.Empty, nonce ?? string.Empty, DifficultyBits);
}
