namespace AAALogistics.Application.Branding;

public interface IAppBrandingStore
{
    Task<AppBrandingSnapshot> GetAsync(CancellationToken cancellationToken = default);

    Task<AppBrandingSnapshot> UpsertAsync(
        UpsertAppBrandingCommand command,
        CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string ContentType)?> TryReadLogoAsync(
        CancellationToken cancellationToken = default);
}

public sealed record AppBrandingSnapshot(
    string DisplayName,
    string AccentHex,
    string? Tagline,
    string? LogoRelativePath,
    bool HasLogo);

public sealed record UpsertAppBrandingCommand(
    string DisplayName,
    string AccentHex,
    string? Tagline,
    string? LogoFileName,
    byte[]? LogoBytes,
    bool ClearLogo);
