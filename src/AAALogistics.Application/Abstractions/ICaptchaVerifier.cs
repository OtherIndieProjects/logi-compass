namespace AAALogistics.Application.Abstractions;

public interface ICaptchaVerifier
{
    bool IsRequired { get; }

    int DifficultyBits { get; }

    string CreateChallenge();

    bool Verify(string? challenge, string? nonce);
}
