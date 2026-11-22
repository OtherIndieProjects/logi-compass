using System.Text.Json;
using Avalonia;
using Avalonia.Media;

namespace AAALogistics.Client.Services;

internal static class BrandTheme
{
    public static string? LogoLocalPath { get; private set; }

    public static event Action? Changed;
    public static string Primary { get; private set; } = "#0854A0";

    public static string PrimaryDark { get; private set; } = "#354A5F";

    public static string PrimaryLight { get; private set; } = "#EBF5FE";

    public static string Secondary { get; private set; } = "#5B738B";

    public static string Accent { get; private set; } = "#0A6ED1";

    public static string Background { get; private set; } = "#F5F6F7";

    public static string Surface { get; private set; } = "#FFFFFF";

    public static string Text { get; private set; } = "#32363A";

    public static string TextSecondary { get; private set; } = "#6A6D70";

    public static string Border { get; private set; } = "#D9D9D9";

    public static string Error { get; private set; } = "#BB0000";

    public static string ErrorFill { get; private set; } = "#FFEBEB";

    public static string Warning { get; private set; } = "#E9730C";

    public static string WarningFill { get; private set; } = "#FEF7F1";

    public static string Success { get; private set; } = "#107E3E";

    public static string SuccessFill { get; private set; } = "#F1FDF6";

    public static string ApplicationName { get; private set; } =
        "AAA Logistics Studio";

    public static string ShortName { get; private set; } = "AAA Logistics Studio";

    public static string Tagline { get; private set; } =
        "Logi Compass — brújula logística dentro del studio";

    static BrandTheme()
    {
        try
        {
            var path = FindBrandFile();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            ApplicationName = ReadString(root, "applicationName", ApplicationName);
            ShortName = ReadString(root, "shortName", ShortName);
            Tagline = ReadString(root, "tagline", Tagline);

            if (!root.TryGetProperty("colors", out var colors))
            {
                return;
            }

            Primary = ReadHex(colors, "primary", Primary);
            PrimaryDark = ReadHex(colors, "primaryDark", PrimaryDark);
            PrimaryLight = ReadHex(colors, "primaryLight", PrimaryLight);
            Secondary = ReadHex(colors, "secondary", Secondary);
            Accent = ReadHex(colors, "accent", Accent);
            if (colors.TryGetProperty("neutral", out var neutral))
            {
                Background = ReadHex(neutral, "background", Background);
                Surface = ReadHex(neutral, "surface", Surface);
                Text = ReadHex(neutral, "text", Text);
                TextSecondary = ReadHex(neutral, "textSecondary", TextSecondary);
                Border = ReadHex(neutral, "border", Border);
            }

            if (colors.TryGetProperty("status", out var status))
            {
                Error = ReadHex(status, "error", Error);
                ErrorFill = ReadHex(status, "errorFill", ErrorFill);
                Warning = ReadHex(status, "warning", Warning);
                WarningFill = ReadHex(status, "warningFill", WarningFill);
                Success = ReadHex(status, "success", Success);
                SuccessFill = ReadHex(status, "successFill", SuccessFill);
            }
        }
        catch (Exception exception) when (exception is IOException
            or JsonException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
        }
    }

    public static SolidColorBrush Brush(string hex) =>
        new(Color.Parse(hex));

    /// <summary>Aplica marca del servidor (o fallback local) en runtime sin reiniciar.</summary>
    public static void ApplyRuntime(
        string displayName,
        string accentHex,
        string? tagline = null,
        string? logoLocalPath = null)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            ApplicationName = displayName.Trim();
            ShortName = displayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(tagline))
        {
            Tagline = tagline.Trim();
        }

        if (!string.IsNullOrWhiteSpace(accentHex)
            && accentHex.Trim().Length == 7
            && accentHex.Trim()[0] == '#')
        {
            Accent = accentHex.Trim();
            Primary = Accent;
        }

        LogoLocalPath = string.IsNullOrWhiteSpace(logoLocalPath) ? null : logoLocalPath;
        TryUpdateAppAccentResource(Accent);
        PersistLocalFallback();
        Changed?.Invoke();
    }

    public static void PersistLocalFallback()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "branding");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "brand.json");
            var json =
                $$"""
                {
                  "applicationName": {{JsonSerializer.Serialize(ApplicationName)}},
                  "shortName": {{JsonSerializer.Serialize(ShortName)}},
                  "tagline": {{JsonSerializer.Serialize(Tagline)}},
                  "colors": { "accent": {{JsonSerializer.Serialize(Accent)}}, "primary": {{JsonSerializer.Serialize(Primary)}} }
                }
                """;
            File.WriteAllText(path, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryUpdateAppAccentResource(string accentHex)
    {
        try
        {
            if (Application.Current?.Resources is null)
            {
                return;
            }

            Application.Current.Resources["Lc.Accent"] = new SolidColorBrush(Color.Parse(accentHex));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
        }
    }

    private static string FindBrandFile()
    {
        var besideOutput = Path.Combine(
            AppContext.BaseDirectory,
            "branding",
            "brand.json");
        if (File.Exists(besideOutput))
        {
            return besideOutput;
        }

        foreach (var start in new[]
                 {
                     AppContext.BaseDirectory,
                     Directory.GetCurrentDirectory(),
                 })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "branding",
                    "brand.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return string.Empty;
    }

    private static string ReadString(
        JsonElement parent,
        string name,
        string fallback)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return fallback;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    private static string ReadHex(
        JsonElement parent,
        string name,
        string fallback)
    {
        if (!parent.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return fallback;
        }

        var hex = value.GetString();
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var trimmed = hex.Trim();
        return trimmed.Length == 7 && trimmed[0] == '#'
            ? trimmed
            : fallback;
    }
}
