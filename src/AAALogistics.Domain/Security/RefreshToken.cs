using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Security;

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset createdUtc,
        DateTimeOffset expiresUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "El usuario es obligatorio.",
                nameof(userId));
        }

        if (familyId == Guid.Empty)
        {
            throw new ArgumentException(
                "La familia del token es obligatoria.",
                nameof(familyId));
        }

        if (tokenHash is null
            || tokenHash.Length != 64
            || tokenHash.Any(value => !char.IsAsciiHexDigit(value)))
        {
            throw new ArgumentException(
                "El hash del refresh token debe ser SHA-256 en hexadecimal.",
                nameof(tokenHash));
        }

        if (expiresUtc <= createdUtc)
        {
            throw new ArgumentException(
                "El refresh token debe caducar después de crearse.",
                nameof(expiresUtc));
        }

        UserId = userId;
        TokenHash = tokenHash.ToLowerInvariant();
        FamilyId = familyId;
        CreatedUtc = createdUtc;
        ExpiresUtc = expiresUtc;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public Guid FamilyId { get; private set; }

    public DateTimeOffset ExpiresUtc { get; private set; }

    public DateTimeOffset? RevokedUtc { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public bool IsActive(DateTimeOffset utc) =>
        RevokedUtc is null && ExpiresUtc > utc;

    public void Revoke(DateTimeOffset utc, Guid? replacedByTokenId)
    {
        if (RevokedUtc is not null)
        {
            return;
        }

        RevokedUtc = utc;
        ReplacedByTokenId = replacedByTokenId;
    }
}
