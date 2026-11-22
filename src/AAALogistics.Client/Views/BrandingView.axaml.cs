using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class BrandingView : UserControl
{
    private readonly LogisticsApiClient _client;
    private string? _pendingLogoFileName;
    private string? _pendingLogoBase64;
    private bool _clearLogo;
    private byte[]? _previewBytes;

    public BrandingView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Branding);
        ApplyPermissions();
    }

    private void ApplyPermissions()
    {
        var canEdit = _client.Permissions.Has(AppPermissionNames.InvoiceConfig)
            || _client.Permissions.IsAdmin;
        SaveButton.IsVisible = canEdit;
        PickLogoButton.IsVisible = canEdit;
        ClearLogoButton.IsVisible = canEdit;
        DisplayNameBox.IsEnabled = canEdit;
        AccentBox.IsEnabled = canEdit;
        TaglineBox.IsEnabled = canEdit;
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyPermissions();
        try
        {
            var brand = await _client.GetBrandingAsync();
            DisplayNameBox.Text = brand.DisplayName;
            AccentBox.Text = brand.AccentHex;
            TaglineBox.Text = brand.Tagline ?? string.Empty;
            _pendingLogoBase64 = null;
            _pendingLogoFileName = null;
            _clearLogo = false;
            _previewBytes = null;
            if (brand.HasLogo)
            {
                try
                {
                    _previewBytes = await _client.DownloadBrandingLogoAsync();
                }
                catch (Exception exception) when (exception is ApiException
                    or HttpRequestException
                    or TaskCanceledException)
                {
                    _previewBytes = null;
                }
            }

            UpdatePreview();
            ApplyToShell(brand, persistLogo: true);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnPickLogoClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Elegir logo (PNG/JPG)",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Imágenes")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
                    },
                ],
            });
        if (files.Count == 0)
        {
            return;
        }

        await using var stream = await files[0].OpenReadAsync();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        if (memory.Length > 2 * 1024 * 1024)
        {
            StatusText.Text = "El logo no puede superar 2 MB.";
            return;
        }

        _previewBytes = memory.ToArray();
        _pendingLogoBase64 = Convert.ToBase64String(_previewBytes);
        _pendingLogoFileName = files[0].Name;
        _clearLogo = false;
        UpdatePreview();
        StatusText.Text = $"Logo listo: {_pendingLogoFileName}. Pulse Guardar marca.";
    }

    private void OnClearLogoClick(object? sender, RoutedEventArgs e)
    {
        _clearLogo = true;
        _pendingLogoBase64 = null;
        _pendingLogoFileName = null;
        _previewBytes = null;
        UpdatePreview();
        StatusText.Text = "Logo se quitará al guardar.";
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!SaveButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso (Invoice.Config / Admin).";
            return;
        }

        var name = DisplayNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusText.Text = "El nombre comercial es obligatorio.";
            return;
        }

        var accent = AccentBox.Text?.Trim() ?? "#0A6ED1";
        try
        {
            var saved = await _client.UpsertBrandingAsync(
                new UpsertAppBrandingRequest(
                    name,
                    accent,
                    TaglineBox.Text?.Trim(),
                    _pendingLogoFileName,
                    _pendingLogoBase64,
                    _clearLogo));
            _pendingLogoBase64 = null;
            _pendingLogoFileName = null;
            _clearLogo = false;
            if (saved.HasLogo && _previewBytes is null)
            {
                _previewBytes = await _client.DownloadBrandingLogoAsync();
            }

            if (!saved.HasLogo)
            {
                _previewBytes = null;
            }

            UpdatePreview();
            ApplyToShell(saved, persistLogo: true);
            StatusText.Text = "Marca guardada. Cabecera y acento aplicados.";
            await UiHelpers.ShowInfoAsync(this, "Marca actualizada.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private void UpdatePreview()
    {
        var name = DisplayNameBox.Text?.Trim();
        PreviewName.Text = string.IsNullOrWhiteSpace(name) ? "Logi Compass" : name;
        PreviewTagline.Text = TaglineBox.Text?.Trim() ?? string.Empty;
        try
        {
            var accent = AccentBox.Text?.Trim() ?? "#0A6ED1";
            AccentSwatch.Background = new SolidColorBrush(Color.Parse(accent));
        }
        catch (FormatException)
        {
        }

        if (_previewBytes is { Length: > 0 })
        {
            LogoPreview.Source = new Bitmap(new MemoryStream(_previewBytes));
        }
        else
        {
            LogoPreview.Source = null;
        }
    }

    private void ApplyToShell(AppBrandingResponse brand, bool persistLogo)
    {
        string? logoPath = null;
        if (persistLogo && _previewBytes is { Length: > 0 })
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "branding");
                Directory.CreateDirectory(dir);
                var ext = DetectExtension(_previewBytes);
                logoPath = Path.Combine(dir, $"logo-cache{ext}");
                File.WriteAllBytes(logoPath, _previewBytes);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logoPath = null;
            }
        }

        BrandTheme.ApplyRuntime(
            brand.DisplayName,
            brand.AccentHex,
            brand.Tagline,
            logoPath);
    }

    private static string DetectExtension(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50)
        {
            return ".png";
        }

        return ".jpg";
    }
}
