using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class ReportsView : UserControl
{
    private readonly LogisticsApiClient _client;

    public ReportsView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Reports);
        OfficeUi.SetTextColumns(
            Grid,
            ("Tipo", "DocumentKindEs"),
            ("Número", "Number"),
            ("Estado", "StatusEs"));
        var canReport = client.Permissions.Has(AppPermissionNames.ReportRead);
        PdfOrdersButton.IsVisible = canReport;
        PrintOrdersButton.IsVisible = canReport;
        PdfOtifButton.IsVisible = canReport;
        PrintOtifButton.IsVisible = canReport;
        var canStock = client.Permissions.Has(AppPermissionNames.StockRead);
        PdfStockButton.IsVisible = canStock;
        PrintStockButton.IsVisible = canStock;
        ExportStockButton.IsVisible = canStock;
        AgingButton.IsVisible = canReport;
        ValuedStockButton.IsVisible = canStock;
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        try
        {
            var rows = await _client.GetOrdersByStatusAsync();
            var projected = rows.Select(row => new
            {
                DocumentKindEs = row.DocumentKind switch
                {
                    "PurchaseOrder" => "Pedido compra",
                    "SalesOrder" => "Pedido venta",
                    _ => row.DocumentKind,
                },
                row.Number,
                StatusEs = OfficeUi.StatusToEs(row.Status),
            }).ToList();
            Grid.ItemsSource = projected;
            EmptyHint.IsVisible = projected.Count == 0;
            Grid.IsVisible = projected.Count > 0;
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnOtifClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            var otif = await _client.GetOtifAsync();
            OtifText.Text =
                $"Entrega a tiempo (OTIF): {otif.OnTimeOrders}/{otif.DeliveredOrders} " +
                $"({otif.OtifPercent}%). Criterio: fecha de entrega ≤ fecha pedida.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPdfOrdersClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _client.DownloadOrdersByStatusPdfAsync();
            await UiHelpers.SaveBytesAsync(this, "pedidos-por-estado.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintOrdersClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _client.DownloadOrdersByStatusPdfAsync();
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir pedidos por estado",
                "pedidos-por-estado.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPdfOtifClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _client.DownloadOtifPdfAsync();
            await UiHelpers.SaveBytesAsync(this, "informe-otif.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintOtifClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _client.DownloadOtifPdfAsync();
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir OTIF",
                "informe-otif.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPdfStockClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _client.DownloadStockPdfAsync();
            await UiHelpers.SaveBytesAsync(this, "informe-stock.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintStockClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _client.DownloadStockPdfAsync();
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir stock",
                "informe-stock.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnExportStockClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var bytes = await _client.ExportStockAsync();
            await UiHelpers.SaveBytesAsync(this, "stock.xlsx", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnAgingClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            var rows = await _client.GetStudioAgingReportAsync();
            OfficeUi.SetTextColumns(
                Grid,
                ("Cliente", nameof(AgingReportRow.CustomerName)),
                ("Importe abierto", nameof(AgingReportRow.OpenAmount)),
                ("Días", nameof(AgingReportRow.BucketDays)),
                ("Moneda", nameof(AgingReportRow.Currency)));
            Grid.ItemsSource = rows.ToList();
            EmptyHint.IsVisible = rows.Count == 0;
            EmptyHint.Text =
                "Sin antigüedad de cobros. Revise términos comerciales / facturas abiertas.";
            Grid.IsVisible = rows.Count > 0;
            OtifText.Text = $"Antigüedad cobros: {rows.Count} fila(s).";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnValuedStockClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        try
        {
            await _client.Masters.RefreshAsync();
            var rows = await _client.GetStudioValuedStockReportAsync();
            OfficeUi.SetTextColumns(
                Grid,
                ("Material", "MaterialId"),
                ("Almacén", "WarehouseId"),
                ("Cantidad", nameof(ValuedStockReportRow.Quantity)),
                ("Valor unit.", nameof(ValuedStockReportRow.UnitValue)),
                ("Valor total", nameof(ValuedStockReportRow.TotalValue)),
                ("Moneda", nameof(ValuedStockReportRow.Currency)));
            var materials = _client.Masters.Materials.ToDictionary(m => m.Id, m => m.Code);
            var warehouses = _client.Masters.Warehouses.ToDictionary(w => w.Id, w => w.Code);
            var projected = rows.Select(row => new
            {
                MaterialId = materials.TryGetValue(row.MaterialId, out var mc) ? mc : "—",
                WarehouseId = warehouses.TryGetValue(row.WarehouseId, out var wc) ? wc : "—",
                row.Quantity,
                row.UnitValue,
                row.TotalValue,
                row.Currency,
            }).ToList();
            Grid.ItemsSource = projected;
            EmptyHint.IsVisible = projected.Count == 0;
            EmptyHint.Text = "Sin stock valorado.";
            Grid.IsVisible = projected.Count > 0;
            OtifText.Text = $"Stock valorado: {projected.Count} fila(s).";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }
}
