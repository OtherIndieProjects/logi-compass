using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class QuotesView : UserControl
{
    private readonly LogisticsApiClient _client;
    private readonly DraftLineBoard _draft = new();
    private IReadOnlyList<QuoteResponse> _quotes = [];

    public QuotesView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Quotes);
        _draft.Bind(DraftLinesGrid);
        OfficeUi.SetTextColumns(
            QuotesGrid,
            ("Número", "Number"),
            ("Estado", "StatusEs"),
            ("Total", "TotalAmount"),
            ("Válido hasta", "ValidUntil"),
            ("Pedido", "SalesOrder"));
        OfficeUi.SetTextColumns(
            LinesGrid,
            ("Concepto", "Description"),
            ("Cant.", "Quantity"),
            ("Precio", "UnitPrice"),
            ("Importe", "NetAmount"));
        ApplyPermissions();
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        var canWrite = gate.Has(AppPermissionNames.SalesOrderCreate);
        var canRead = gate.Has(AppPermissionNames.SalesOrderRead) || canWrite;
        CreateButton.IsVisible = canWrite;
        SendButton.IsVisible = canWrite;
        AcceptButton.IsVisible = canWrite;
        RejectButton.IsVisible = canWrite;
        PdfButton.IsVisible = canRead;
        PrintButton.IsVisible = canRead;
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyPermissions();
        try
        {
            await _client.Masters.RefreshAsync();
            PickerHelpers.BindParties(CustomerBox, _client.Masters.Customers);
            PickerHelpers.BindWarehouses(WarehouseBox, _client.Masters.Warehouses);
            PickerHelpers.BindMaterials(MaterialBox, _client.Masters.Materials);
            _quotes = await _client.GetQuotesAsync();
            var soNumbers = (await _client.GetSalesOrdersAsync())
                .ToDictionary(order => order.Id, order => order.Number);
            var rows = _quotes.Select(quote => new
            {
                quote.Number,
                StatusEs = StatusToEs(quote.Status),
                quote.TotalAmount,
                quote.ValidUntil,
                SalesOrder = quote.SalesOrderId is Guid soId
                    && soNumbers.TryGetValue(soId, out var soNumber)
                    ? soNumber
                    : "—",
            }).ToList();
            QuotesGrid.ItemsSource = rows;
            EmptyHint.IsVisible = rows.Count == 0;
            QuotesGrid.IsVisible = rows.Count > 0;
            LinesGrid.ItemsSource = null;
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private static string StatusToEs(string status) =>
        status switch
        {
            "Draft" => "Borrador",
            "Sent" => "Enviado",
            "Accepted" => "Aceptado",
            "Rejected" => "Rechazado",
            "Expired" => "Caducado",
            _ => OfficeUi.StatusToEs(status),
        };

    private void OnQuoteSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!TrySelected(out var quote))
        {
            LinesGrid.ItemsSource = null;
            return;
        }

        LinesGrid.ItemsSource = quote.Lines.Select(line => new
        {
            line.Description,
            line.Quantity,
            line.UnitPrice,
            line.NetAmount,
        }).ToList();
        StatusText.Text = string.Empty;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private void OnAddLineClick(object? sender, RoutedEventArgs e)
    {
        if (!TryBuildDraftLine(out var line, out var error))
        {
            StatusText.Text = error;
            return;
        }

        _draft.Add(line);
        _draft.Bind(DraftLinesGrid);
        StatusText.Text = $"{_draft.Count} línea(s) en borrador.";
    }

    private void OnRemoveLineClick(object? sender, RoutedEventArgs e)
    {
        if (!_draft.RemoveAt(DraftLinesGrid.SelectedIndex))
        {
            StatusText.Text = "Seleccione una línea del borrador para quitar.";
            return;
        }

        _draft.Bind(DraftLinesGrid);
        StatusText.Text = $"{_draft.Count} línea(s) en borrador.";
    }

    private bool TryBuildDraftLine(out DraftLineBoard.DraftLine line, out string error)
    {
        line = null!;
        error = string.Empty;
        var materialId = PickerHelpers.SelectedId(MaterialBox);
        if (materialId is null)
        {
            error = "Seleccione material.";
            return false;
        }

        if (!UiHelpers.TryParseDecimal(QtyBox.Text, out var qty) || qty <= 0
            || !UiHelpers.TryParseDecimal(PriceBox.Text, out var price) || price < 0)
        {
            error = "Cantidad y precio deben ser válidos.";
            return false;
        }

        var material = _client.Masters.Materials.FirstOrDefault(row => row.Id == materialId.Value);
        var label = material is null
            ? materialId.Value.ToString("D")[..8]
            : $"{material.Code} — {material.DisplayName}";
        line = new DraftLineBoard.DraftLine(materialId.Value, label, qty, price);
        return true;
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var customerId = PickerHelpers.SelectedId(CustomerBox);
        var warehouseId = PickerHelpers.SelectedId(WarehouseBox);
        if (customerId is null || warehouseId is null)
        {
            StatusText.Text = "Seleccione cliente y almacén.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(TaxBox.Text, out var tax) || tax < 0
            || !int.TryParse(ValidDaysBox.Text?.Trim(), out var days) || days < 1)
        {
            StatusText.Text = "IVA y días de validez deben ser válidos.";
            return;
        }

        var lines = _draft.Lines.ToList();
        if (lines.Count == 0)
        {
            if (!TryBuildDraftLine(out var single, out var error))
            {
                StatusText.Text = error;
                return;
            }

            lines.Add(single);
        }

        try
        {
            var validUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(days));
            await _client.CreateQuoteAsync(
                new CreateQuoteRequestDto(
                    customerId.Value,
                    warehouseId.Value,
                    validUntil,
                    "EUR",
                    tax,
                    lines.Select(line =>
                        new CreateQuoteLineRequestDto(
                            line.MaterialId,
                            line.Quantity,
                            line.UnitPrice)).ToArray()));
            _draft.Clear();
            _draft.Bind(DraftLinesGrid);
            await UiHelpers.ShowInfoAsync(this, $"Presupuesto creado ({lines.Count} línea(s)).");
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnSendClick(object? sender, RoutedEventArgs e) =>
        await RunSelectedAsync(quote => _client.SendQuoteAsync(quote.Id), "Presupuesto marcado como enviado.");

    private async void OnAcceptClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var quote))
        {
            return;
        }

        try
        {
            var updated = await _client.AcceptQuoteAsync(owner, quote.Id);
            string? soLabel = null;
            if (updated.SalesOrderId is Guid soId)
            {
                var orders = await _client.GetSalesOrdersAsync();
                soLabel = orders.FirstOrDefault(order => order.Id == soId)?.Number
                    ?? soId.ToString("D")[..8];
            }

            await UiHelpers.ShowInfoAsync(
                this,
                soLabel is not null
                    ? $"Presupuesto aceptado. Pedido de venta creado ({soLabel})."
                    : "Presupuesto aceptado.");
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRejectClick(object? sender, RoutedEventArgs e) =>
        await RunSelectedAsync(quote => _client.RejectQuoteAsync(quote.Id), "Presupuesto rechazado.");

    private async void OnPdfClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var quote))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadQuotePdfAsync(quote.Id);
            await UiHelpers.SaveBytesAsync(this, $"presupuesto-{quote.Number}.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var quote))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadQuotePdfAsync(quote.Id);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                $"Imprimir presupuesto {quote.Number}",
                $"presupuesto-{quote.Number}.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task RunSelectedAsync(Func<QuoteResponse, Task> action, string okMessage)
    {
        if (!TrySelected(out var quote))
        {
            return;
        }

        try
        {
            await action(quote);
            await UiHelpers.ShowInfoAsync(this, okMessage);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelected(out QuoteResponse quote)
    {
        quote = null!;
        if (QuotesGrid.SelectedIndex < 0 || QuotesGrid.SelectedIndex >= _quotes.Count)
        {
            StatusText.Text = "Seleccione un presupuesto.";
            return false;
        }

        quote = _quotes[QuotesGrid.SelectedIndex];
        return true;
    }
}
