using System.Globalization;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class AuditView : UserControl
{
    private readonly LogisticsApiClient _client;
    private string _area = "accesos";
    private int _page = 1;
    private const int PageSize = 100;
    private IReadOnlyList<AuditEntryResponse> _rows = [];

    public AuditView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Audit);
        OfficeUi.SetTextColumns(
            Grid,
            ("Secuencia", nameof(AuditRow.Secuencia)),
            ("Tabla", nameof(AuditRow.Tabla)),
            ("Registro", nameof(AuditRow.Registro)),
            ("Acción", nameof(AuditRow.Accion)),
            ("Usuario", nameof(AuditRow.Usuario)),
            ("IP", nameof(AuditRow.Ip)),
            ("Fecha UTC", nameof(AuditRow.FechaUtc)));
        ApplyAdminButtons();
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyAdminButtons();
        try
        {
            var fromUtc = ParseDate(FromBox.Text, startOfDay: true);
            var toUtc = ParseDate(ToBox.Text, startOfDay: false);
            var page = await _client.GetAuditEntriesAsync(
                page: _page,
                pageSize: PageSize,
                fromUtc: fromUtc,
                toUtc: toUtc,
                userName: string.IsNullOrWhiteSpace(UserBox.Text) ? null : UserBox.Text,
                table: string.IsNullOrWhiteSpace(TableBox.Text) ? null : TableBox.Text,
                area: _area,
                action: string.IsNullOrWhiteSpace(ActionBox.Text) ? null : ActionBox.Text,
                includePayloads: true);
            _rows = page.Items;
            Grid.ItemsSource = _rows.Select(ToRow).ToList();
            EmptyHint.IsVisible = _rows.Count == 0;
            Grid.IsVisible = _rows.Count > 0;
            var totalPages = Math.Max(1, (int)Math.Ceiling(page.TotalCount / (double)PageSize));
            PageText.Text = $"Pág. {page.Page} / {totalPages} ({page.TotalCount} entradas)";
            ClearDetail();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private void ApplyAdminButtons()
    {
        var isAdmin = _client.Permissions.Roles.Any(role =>
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
        VerifyButton.IsVisible = isAdmin;
        CheckpointButton.IsVisible = isAdmin;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        _page = 1;
        await RefreshAsync();
    }

    private async void OnPrevPageClick(object? sender, RoutedEventArgs e)
    {
        if (_page <= 1)
        {
            return;
        }

        _page--;
        await RefreshAsync();
    }

    private async void OnNextPageClick(object? sender, RoutedEventArgs e)
    {
        _page++;
        await RefreshAsync();
    }

    private async void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AreaTabs.SelectedItem is not TabItem item || item.Tag is not string tag)
        {
            return;
        }

        _area = tag;
        _page = 1;
        if (IsInitialized)
        {
            await RefreshAsync();
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Grid.SelectedItem is not AuditRow row)
        {
            ClearDetail();
            return;
        }

        var entry = _rows.FirstOrDefault(value => value.Sequence == row.Secuencia);
        if (entry is null)
        {
            ClearDetail();
            return;
        }

        DetailMeta.Text =
            $"#{entry.Sequence} · {entry.TableName} · {entry.Action} · {entry.UserName ?? entry.UserId?.ToString("D") ?? "—"} · {entry.Utc:u}";
        BeforeText.Text = FormatPayload(entry.OldJson);
        AfterText.Text = FormatPayload(entry.NewJson);
    }

    private async void OnExportCsvClick(object? sender, RoutedEventArgs e) =>
        await ExportAsync("csv");

    private async void OnExportJsonClick(object? sender, RoutedEventArgs e) =>
        await ExportAsync("json");

    private async void OnVerifyClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            var result = await _client.VerifyAuditChainAsync();
            StatusText.Text = result.IsValid
                ? result.Message
                : $"Cadena inválida: {result.Message}";
            if (!result.IsValid)
            {
                await UiHelpers.ShowErrorAsync(this, new InvalidOperationException(result.Message));
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnCheckpointClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            var created = await _client.CreateAuditCheckpointAsync("office-ui");
            StatusText.Text =
                $"Punto de control creado hasta secuencia {created.ThroughSequence}.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnExportPdfClick(object? sender, RoutedEventArgs e) =>
        await ExportAsync("pdf");

    private async void OnPrintPdfClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            var bytes = await DownloadAuditExportAsync("pdf");
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir auditoría",
                "auditoria.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task ExportAsync(string format)
    {
        StatusText.Text = string.Empty;
        try
        {
            var bytes = await DownloadAuditExportAsync(format);
            if (format == "pdf")
            {
                await UiHelpers.SaveBytesAsync(this, "auditoria.pdf", bytes);
                StatusText.Text = "PDF de auditoría guardado.";
                return;
            }

            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is null)
            {
                StatusText.Text = "No se pudo abrir el diálogo de guardado.";
                return;
            }

            var file = await top.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Exportar auditoría",
                    SuggestedFileName = format == "json" ? "auditoria.json" : "auditoria.csv",
                    FileTypeChoices =
                    [
                        FilePickerFileTypes.All,
                    ],
                });
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(bytes);
            StatusText.Text = "Exportación guardada.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private Task<byte[]> DownloadAuditExportAsync(string format)
    {
        var fromUtc = ParseDate(FromBox.Text, startOfDay: true);
        var toUtc = ParseDate(ToBox.Text, startOfDay: false);
        return _client.ExportAuditEntriesAsync(
            format,
            fromUtc,
            toUtc,
            userName: string.IsNullOrWhiteSpace(UserBox.Text) ? null : UserBox.Text,
            table: string.IsNullOrWhiteSpace(TableBox.Text) ? null : TableBox.Text,
            area: _area,
            action: string.IsNullOrWhiteSpace(ActionBox.Text) ? null : ActionBox.Text);
    }

    private void ClearDetail()
    {
        DetailMeta.Text = "Seleccione una fila para ver los cambios.";
        BeforeText.Text = "—";
        AfterText.Text = "—";
    }

    private static AuditRow ToRow(AuditEntryResponse entry) =>
        new(
            entry.Sequence,
            entry.TableName,
            entry.RecordId,
            TranslateAction(entry.Action),
            entry.UserName ?? entry.UserId?.ToString("D") ?? "—",
            entry.Ip ?? "—",
            entry.Utc.ToString("u", CultureInfo.InvariantCulture));

    private static string TranslateAction(string action) =>
        action switch
        {
            "INSERT" => "Alta",
            "UPDATE" => "Modificación",
            "DELETE" => "Baja",
            "LOGIN" => "Inicio de sesión",
            "LOGIN_FAILED" => "Inicio fallido",
            "LOGOUT" => "Cierre de sesión",
            "CRYPTO_SHRED" => "Borrado criptográfico",
            _ => action,
        };

    private static string FormatPayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "—";
        }

        if (json.StartsWith('[') && json.Contains("cifrado", StringComparison.OrdinalIgnoreCase))
        {
            return json;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return json;
            }

            var builder = new StringBuilder();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(TranslateField(property.Name));
                builder.Append(": ");
                builder.Append(property.Value.ToString());
            }

            return builder.Length == 0 ? "—" : builder.ToString();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static string TranslateField(string name) =>
        name switch
        {
            "UserName" or "userName" => "Usuario",
            "Email" => "Correo",
            "DisplayName" or "display_name" => "Nombre",
            "Description" => "Descripción",
            "Code" => "Código",
            "Quantity" or "Cantidad" => "Cantidad",
            "Status" or "Estado" => "Estado",
            "IsActive" => "Activo",
            "CustomerId" => "Cliente",
            "SupplierId" => "Proveedor",
            "WarehouseId" => "Almacén",
            "MaterialId" => "Material",
            "UnitCode" => "Unidad",
            "Price" => "Precio",
            "Currency" => "Moneda",
            "Reason" or "reason" => "Motivo",
            "audience" => "Audiencia",
            "SuspensionReason" => "Motivo suspensión",
            "SuspendedUtc" => "Suspendido UTC",
            _ => name,
        };

    private static DateTimeOffset? ParseDate(string? text, bool startOfDay)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateOnly.TryParse(
                text.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            var time = startOfDay ? TimeOnly.MinValue : new TimeOnly(23, 59, 59);
            return new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);
        }

        if (DateTimeOffset.TryParse(
                text.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            return dto;
        }

        return null;
    }

    private sealed record AuditRow(
        long Secuencia,
        string Tabla,
        string Registro,
        string Accion,
        string Usuario,
        string Ip,
        string FechaUtc);
}
