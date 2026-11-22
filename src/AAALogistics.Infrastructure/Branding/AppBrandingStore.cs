using System.Text.Json;
using System.Text.Json.Serialization;
using AAALogistics.Application.Branding;
using AAALogistics.Application.Logistics;
using Microsoft.Extensions.Hosting;

namespace AAALogistics.Infrastructure.Branding;

public sealed class AppBrandingStore(IHostEnvironment hostEnvironment) : IAppBrandingStore
{
    public const string DefaultDisplayName = "Logi Compass";
    public const string DefaultAccentHex = "#0A6ED1";
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static ReadOnlySpan<byte> PngSignature =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

    private readonly string _brandingRoot = Path.Combine(
        hostEnvironment.ContentRootPath,
        "branding");

    private string ManifestPath => Path.Combine(_brandingRoot, "app-branding.json");

    public Task<AppBrandingSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadManifest());
    }

    public async Task<AppBrandingSnapshot> UpsertAsync(
        UpsertAppBrandingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var displayName = command.DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-BRAND-0001",
                "El nombre comercial es obligatorio.");
        }

        if (displayName.Length > 80)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-BRAND-0002",
                "El nombre comercial no puede superar 80 caracteres.");
        }

        var accent = NormalizeAccent(command.AccentHex);
        var tagline = string.IsNullOrWhiteSpace(command.Tagline)
            ? null
            : command.Tagline.Trim();
        if (tagline is { Length: > 160 })
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-BRAND-0003",
                "El lema no puede superar 160 caracteres.");
        }

        Directory.CreateDirectory(_brandingRoot);
        var current = ReadManifest();
        string? logoRelative = current.LogoRelativePath;

        if (command.ClearLogo)
        {
            DeleteLogoFiles();
            logoRelative = null;
        }
        else if (command.LogoBytes is { Length: > 0 })
        {
            if (command.LogoBytes.Length > MaxLogoBytes)
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Validation,
                    "AAA-BRAND-0004",
                    "El logo no puede superar 2 MB.");
            }

            var extension = ResolveExtension(command.LogoFileName, command.LogoBytes);
            if (extension is null)
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Validation,
                    "AAA-BRAND-0005",
                    "Solo se admiten PNG, JPG o JPEG (no SVG).");
            }

            DeleteLogoFiles();
            var fileName = $"logo{extension}";
            var absolute = Path.Combine(_brandingRoot, fileName);
            await File.WriteAllBytesAsync(absolute, command.LogoBytes, cancellationToken);
            logoRelative = $"branding/{fileName}";
        }

        var file = new BrandingFile(
            displayName,
            accent,
            tagline,
            logoRelative);
        await File.WriteAllTextAsync(
            ManifestPath,
            JsonSerializer.Serialize(file, JsonOptions),
            cancellationToken);

        // Mirror classic brand.json shortName so offline Client fallback stays in sync.
        await WriteClientFallbackAsync(file, cancellationToken);

        return ToSnapshot(file);
    }

    public async Task<(byte[] Bytes, string ContentType)?> TryReadLogoAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = ReadManifest();
        if (string.IsNullOrWhiteSpace(snapshot.LogoRelativePath))
        {
            return null;
        }

        var absolute = ResolveAbsolute(snapshot.LogoRelativePath);
        if (!File.Exists(absolute))
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(absolute, cancellationToken);
        var contentType = absolute.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";
        return (bytes, contentType);
    }

    private AppBrandingSnapshot ReadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            return new AppBrandingSnapshot(
                DefaultDisplayName,
                DefaultAccentHex,
                "Oficina logística",
                null,
                false);
        }

        try
        {
            var json = File.ReadAllText(ManifestPath);
            var file = JsonSerializer.Deserialize<BrandingFile>(json, JsonOptions);
            if (file is null || string.IsNullOrWhiteSpace(file.DisplayName))
            {
                return new AppBrandingSnapshot(
                    DefaultDisplayName,
                    DefaultAccentHex,
                    null,
                    null,
                    false);
            }

            return ToSnapshot(file);
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return new AppBrandingSnapshot(
                DefaultDisplayName,
                DefaultAccentHex,
                null,
                null,
                false);
        }
    }

    private async Task WriteClientFallbackAsync(
        BrandingFile file,
        CancellationToken cancellationToken)
    {
        var fallback = new
        {
            applicationName = file.DisplayName,
            shortName = file.DisplayName,
            companyName = file.DisplayName,
            tagline = file.Tagline ?? "Logi Compass",
            colors = new
            {
                accent = file.AccentHex,
                primary = file.AccentHex,
            },
        };
        var path = Path.Combine(_brandingRoot, "brand.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(fallback, JsonOptions),
            cancellationToken);
    }

    private void DeleteLogoFiles()
    {
        foreach (var pattern in new[] { "logo.png", "logo.jpg", "logo.jpeg", "logo.webp" })
        {
            var path = Path.Combine(_brandingRoot, pattern);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private string ResolveAbsolute(string relative)
    {
        var normalized = relative.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("branding/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["branding/".Length..];
        }

        return Path.GetFullPath(Path.Combine(_brandingRoot, normalized));
    }

    private static AppBrandingSnapshot ToSnapshot(BrandingFile file)
    {
        var hasLogo = !string.IsNullOrWhiteSpace(file.LogoRelativePath);
        return new AppBrandingSnapshot(
            file.DisplayName.Trim(),
            NormalizeAccent(file.AccentHex),
            string.IsNullOrWhiteSpace(file.Tagline) ? null : file.Tagline.Trim(),
            string.IsNullOrWhiteSpace(file.LogoRelativePath) ? null : file.LogoRelativePath,
            hasLogo);
    }

    private static string NormalizeAccent(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return DefaultAccentHex;
        }

        var trimmed = hex.Trim();
        if (trimmed.Length == 7
            && trimmed[0] == '#'
            && trimmed[1..].All(static c => Uri.IsHexDigit(c)))
        {
            return trimmed.ToUpperInvariant();
        }

        throw new LogisticsException(
            LogisticsErrorKind.Validation,
            "AAA-BRAND-0006",
            "El color de acento debe ser hex #RRGGBB.");
    }

    private static string? ResolveExtension(string? fileName, byte[] bytes)
    {
        var fromName = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (fromName is ".png" or ".jpg" or ".jpeg")
        {
            if (fromName == ".png" && bytes.AsSpan().StartsWith(PngSignature))
            {
                return ".png";
            }

            if (fromName is ".jpg" or ".jpeg" && bytes.AsSpan().StartsWith(JpegSignature))
            {
                return ".jpg";
            }
        }

        if (bytes.AsSpan().StartsWith(PngSignature))
        {
            return ".png";
        }

        if (bytes.AsSpan().StartsWith(JpegSignature))
        {
            return ".jpg";
        }

        // WEBP: RIFF....WEBP
        if (bytes.Length >= 12
            && bytes[0] == (byte)'R'
            && bytes[1] == (byte)'I'
            && bytes[2] == (byte)'F'
            && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W'
            && bytes[9] == (byte)'E'
            && bytes[10] == (byte)'B'
            && bytes[11] == (byte)'P')
        {
            return ".webp";
        }

        return null;
    }

    private sealed record BrandingFile(
        string DisplayName,
        string AccentHex,
        string? Tagline,
        string? LogoRelativePath);
}
