using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class PurchasesView : UserControl
{
    private readonly LogisticsApiClient _client;
    private readonly DraftLineBoard _draft = new();
    private IReadOnlyList<PurchaseOrderRow> _orders = [];
    private Dictionary<Guid, string> _materialLabels = [];

    public PurchasesView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Purchases);
        _draft.Bind(DraftLinesGrid);
        OfficeUi.BindHelp(CancelPoHelpHost, OfficeHelpTexts.Keys.FieldCancelOpenPo);
        OfficeUi.BindHelp(ClosePoHelpHost, OfficeHelpTexts.Keys.FieldClosePo);
        OfficeUi.BindHelp(GrHelpHost, OfficeHelpTexts.Keys.FieldGoodsReceipt);
        OfficeUi.BindHelp(LotHelpHost, OfficeHelpTexts.Keys.FieldLotCode);
        OfficeUi.BindHelp(LotExpiryHelpHost, OfficeHelpTexts.Keys.FieldLotExpiry);
        OfficeUi.SetTextColumns(
            OrdersGrid,
            ("Número", "Number"),
            ("Estado", "StatusEs"),
            ("Líneas", "Lines"));
        OfficeUi.SetTextColumns(
            LinesGrid,
            ("Material", "Material"),
            ("Pedida", "Requested"),
            ("Abierta", "Open"),
            ("Recibida", "Posted"));
        ApplyPermissions();
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        CreatePoButton.IsVisible = gate.Has(AppPermissionNames.PurchaseOrderCreate);
        CancelPoButton.IsVisible = gate.Has(AppPermissionNames.PurchaseOrderCancelOpen);
        ClosePoButton.IsVisible = gate.Has(AppPermissionNames.PurchaseOrderClose);
        PdfPoButton.IsVisible = gate.Has(AppPermissionNames.PurchaseOrderCreate);
        PrintPoButton.IsVisible = gate.Has(AppPermissionNames.PurchaseOrderCreate);
        PostGrButton.IsVisible = gate.Has(AppPermissionNames.GoodsReceiptPost);
        PostAllGrButton.IsVisible = gate.Has(AppPermissionNames.GoodsReceiptPost);
        PdfGrButton.IsVisible = gate.Has(AppPermissionNames.GoodsReceiptPost);
        PrintGrButton.IsVisible = gate.Has(AppPermissionNames.GoodsReceiptPost);
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyPermissions();
        try
        {
            await _client.Masters.RefreshAsync();
            _materialLabels = _client.Masters.Materials.ToDictionary(
                material => material.Id,
                material =>
                {
                    var name = string.IsNullOrWhiteSpace(material.DisplayName)
                        ? material.Description
                        : material.DisplayName;
                    return string.IsNullOrWhiteSpace(name)
                        ? material.Code
                        : $"{material.Code} — {name}";
                });
            PickerHelpers.BindParties(SupplierBox, _client.Masters.Suppliers);
            PickerHelpers.BindWarehouses(WarehouseBox, _client.Masters.Warehouses);
            PickerHelpers.BindMaterials(MaterialBox, _client.Masters.Materials);
            _orders = await _client.GetPurchaseOrdersAsync();
            BindOrdersGrid();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private void BindOrdersGrid()
    {
        var rows = _orders.Select(order => new
        {
            order.Number,
            StatusEs = OfficeUi.StatusToEs(order.Status),
            Lines = order.Lines.Count,
        }).ToList();
        OrdersGrid.ItemsSource = rows;
        EmptyHint.IsVisible = rows.Count == 0;
        OrdersGrid.IsVisible = rows.Count > 0;
        LinesGrid.ItemsSource = null;
        SelectedText.Text = string.Empty;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private void OnOrderSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (OrdersGrid.SelectedIndex < 0 || OrdersGrid.SelectedIndex >= _orders.Count)
        {
            LinesGrid.ItemsSource = null;
            SelectedText.Text = string.Empty;
            return;
        }

        var order = _orders[OrdersGrid.SelectedIndex];
        LinesGrid.ItemsSource = order.Lines.Select(line => new
        {
            Material = MaterialLabel(line.MaterialId),
            Requested = line.RequestedQuantity,
            Open = line.OpenQuantity,
            Posted = line.PostedQuantity,
        }).ToList();
        SelectedText.Text =
            $"{order.Number} · {OfficeUi.StatusToEs(order.Status)}";
        if (order.Lines.Count > 0)
        {
            GrQtyBox.Text = order.Lines[0].OpenQuantity.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        _ = BindGoodsReceiptsAsync(order.Id);
    }

    private string MaterialLabel(Guid materialId) =>
        _materialLabels.TryGetValue(materialId, out var label)
            ? label
            : materialId.ToString("D");

    private async Task BindGoodsReceiptsAsync(Guid? purchaseOrderId, Guid? selectId = null)
    {
        try
        {
            var rows = await _client.GetGoodsReceiptsAsync(purchaseOrderId);
            PickerHelpers.BindItems(
                GoodsReceiptBox,
                rows.Select(row =>
                {
                    var po = string.IsNullOrWhiteSpace(row.PurchaseOrderNumber)
                        ? string.Empty
                        : $" · {row.PurchaseOrderNumber}";
                    return new PickerHelpers.PickerItem(
                        row.Id,
                        $"{row.Number}{po} · {row.DocumentDate:yyyy-MM-dd}");
                }));
            if (selectId is Guid id)
            {
                PickerHelpers.SelectId(GoodsReceiptBox, id);
            }
            else if (GoodsReceiptBox.ItemCount > 0 && GoodsReceiptBox.SelectedIndex < 0)
            {
                GoodsReceiptBox.SelectedIndex = 0;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
        }
    }

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
        if (!PickerHelpers.TryGetId(MaterialBox, out var materialId)
            || !UiHelpers.TryParseDecimal(QtyBox.Text, out var qty)
            || !UiHelpers.TryParseDecimal(PriceBox.Text, out var price)
            || qty <= 0
            || price < 0)
        {
            error = "Seleccione material, cantidad > 0 y precio ≥ 0.";
            return false;
        }

        line = new DraftLineBoard.DraftLine(
            materialId,
            MaterialLabel(materialId),
            qty,
            price);
        return true;
    }

    private async void OnCreatePoClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        if (!CreatePoButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para crear pedidos de compra.";
            return;
        }

        try
        {
            if (!PickerHelpers.TryGetId(SupplierBox, out var supplierId)
                || !PickerHelpers.TryGetId(WarehouseBox, out var warehouseId))
            {
                StatusText.Text = "Seleccione proveedor y almacén.";
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

            var created = await _client.CreatePurchaseOrderAsync(
                new CreatePurchaseOrderRequest(
                    supplierId,
                    warehouseId,
                    DateOnly.FromDateTime(DateTime.Today),
                    null,
                    "EUR",
                    null,
                    lines.Select(line =>
                        new CreatePurchaseOrderLineRequest(
                            line.MaterialId,
                            line.Quantity,
                            line.UnitPrice)).ToArray()));
            _draft.Clear();
            _draft.Bind(DraftLinesGrid);
            await UiHelpers.ShowInfoAsync(
                this,
                $"Pedido creado: {created.Number} ({lines.Count} línea(s))");
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnCancelPoClick(object? sender, RoutedEventArgs e)
    {
        if (!CancelPoButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para cancelar/anular pedidos.";
            return;
        }

        if (!TrySelected(out var order))
        {
            return;
        }

        if (IsTerminal(order.Status))
        {
            StatusText.Text =
                $"El pedido {order.Number} está {OfficeUi.StatusToEs(order.Status).ToLowerInvariant()} y no admite anulación de cantidad abierta.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var reason = await AppDialog.PromptAsync(
            owner,
            "Cancelar / anular pedido",
            "Indique el motivo de cancelación (obligatorio). Se anula la cantidad aún abierta.");
        if (reason is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            StatusText.Text = "El motivo de cancelación es obligatorio.";
            return;
        }

        await RunSelectedAsync(id => _client.CancelPurchaseOrderOpenAsync(owner, id, reason.Trim()));
    }

    private async void OnClosePoClick(object? sender, RoutedEventArgs e)
    {
        if (!ClosePoButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para cerrar pedidos.";
            return;
        }

        if (!TrySelected(out var order))
        {
            return;
        }

        if (IsTerminal(order.Status))
        {
            StatusText.Text =
                $"El pedido {order.Number} ya está {OfficeUi.StatusToEs(order.Status).ToLowerInvariant()}.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        if (!await OfficeUi.ConfirmAsync(
                owner,
                $"¿Cerrar el pedido {order.Number}? No se podrán contabilizar más recepciones."))
        {
            return;
        }

        await RunSelectedAsync(id => _client.ClosePurchaseOrderAsync(owner, id));
    }

    private async void OnPdfClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadPurchaseOrderPdfAsync(order.Id);
            await UiHelpers.SaveBytesAsync(this, $"pedido-compra-{order.Number}.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadPurchaseOrderPdfAsync(order.Id);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                $"Imprimir pedido {order.Number}",
                $"pedido-compra-{order.Number}.pdf");
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

    private async void OnPostGrClick(object? sender, RoutedEventArgs e)
    {
        if (!PostGrButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para contabilizar recepciones.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var order) || order.Lines.Count == 0)
        {
            StatusText.Text = "Seleccione un pedido con líneas.";
            return;
        }

        if (IsTerminal(order.Status))
        {
            StatusText.Text =
                $"El pedido {order.Number} está {OfficeUi.StatusToEs(order.Status).ToLowerInvariant()} y no admite recepción.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(GrQtyBox.Text, out var qty) || qty <= 0)
        {
            StatusText.Text = "Indique una cantidad de recepción válida (mayor que 0).";
            return;
        }

        try
        {
            var line = order.Lines[0];
            if (LinesGrid.SelectedIndex >= 0 && LinesGrid.SelectedIndex < order.Lines.Count)
            {
                line = order.Lines[LinesGrid.SelectedIndex];
            }

            if (line.OpenQuantity <= 0)
            {
                StatusText.Text =
                    "La línea seleccionada no tiene cantidad abierta. Elija otra línea o cancele/cierre el pedido.";
                return;
            }

            if (qty > line.OpenQuantity)
            {
                StatusText.Text =
                    $"La cantidad ({qty}) supera la abierta ({line.OpenQuantity}).";
                return;
            }

            DateOnly? expiry = null;
            if (!string.IsNullOrWhiteSpace(LotExpiryBox.Text)
                && DateOnly.TryParse(
                    LotExpiryBox.Text.Trim(),
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var parsedExpiry))
            {
                expiry = parsedExpiry;
            }
            else if (!string.IsNullOrWhiteSpace(LotExpiryBox.Text))
            {
                StatusText.Text = "Caducidad inválida. Use formato AAAA-MM-DD.";
                return;
            }

            var lotCode = string.IsNullOrWhiteSpace(LotCodeBox.Text)
                ? null
                : LotCodeBox.Text.Trim();
            var result = await _client.PostGoodsReceiptAsync(
                owner,
                new PostGoodsReceiptRequest(
                    order.Id,
                    DateOnly.FromDateTime(DateTime.Today),
                    [
                        new PostGoodsReceiptLineRequest(
                            line.Id,
                            null,
                            qty,
                            lotCode,
                            expiry),
                    ]));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Recepción contabilizada: {result.DocumentNumber ?? result.DocumentId.ToString("D")}");
            await BindGoodsReceiptsAsync(order.Id, result.DocumentId);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPostAllGrClick(object? sender, RoutedEventArgs e)
    {
        if (!PostAllGrButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para contabilizar recepciones.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var order) || order.Lines.Count == 0)
        {
            StatusText.Text = "Seleccione un pedido con líneas.";
            return;
        }

        if (IsTerminal(order.Status))
        {
            StatusText.Text =
                $"El pedido {order.Number} está {OfficeUi.StatusToEs(order.Status).ToLowerInvariant()} y no admite recepción.";
            return;
        }

        var openLines = order.Lines.Where(line => line.OpenQuantity > 0).ToArray();
        if (openLines.Length == 0)
        {
            StatusText.Text = "No hay cantidades abiertas que recibir.";
            return;
        }

        try
        {
            var result = await _client.PostGoodsReceiptAsync(
                owner,
                new PostGoodsReceiptRequest(
                    order.Id,
                    DateOnly.FromDateTime(DateTime.Today),
                    openLines.Select(line =>
                        new PostGoodsReceiptLineRequest(
                            line.Id,
                            null,
                            line.OpenQuantity,
                            null,
                            null)).ToArray()));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Recepción de {openLines.Length} línea(s): {result.DocumentNumber ?? result.DocumentId.ToString("D")}");
            await BindGoodsReceiptsAsync(order.Id, result.DocumentId);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelectedGoodsReceipt(out Guid receiptId)
    {
        if (PickerHelpers.TryGetId(GoodsReceiptBox, out receiptId) && receiptId != Guid.Empty)
        {
            return true;
        }

        StatusText.Text = "Seleccione una recepción por número.";
        return false;
    }

    private async void OnPdfGrClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedGoodsReceipt(out var id))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadGoodsReceiptPdfAsync(id);
            var label = (GoodsReceiptBox.SelectedItem as PickerHelpers.PickerItem)?.Label
                ?? id.ToString("N")[..8];
            await UiHelpers.SaveBytesAsync(this, $"recepcion-{id:N}.pdf", bytes);
            StatusText.Text = $"PDF recepción guardado ({label}).";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintGrClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedGoodsReceipt(out var id))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadGoodsReceiptPdfAsync(id);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir recepción",
                $"recepcion-{id:N}.pdf");
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

    private async Task RunSelectedAsync(Func<Guid, Task> action)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        try
        {
            await action(order.Id);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelected(out PurchaseOrderRow order)
    {
        order = null!;
        if (OrdersGrid.SelectedIndex < 0 || OrdersGrid.SelectedIndex >= _orders.Count)
        {
            StatusText.Text = "Seleccione un pedido en la lista.";
            return false;
        }

        order = _orders[OrdersGrid.SelectedIndex];
        return true;
    }

    private static bool IsTerminal(string status) =>
        status is "Cancelled" or "Canceled" or "Closed" or "Received";
}
