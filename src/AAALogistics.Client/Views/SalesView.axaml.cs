using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class SalesView : UserControl
{
    private readonly LogisticsApiClient _client;
    private readonly DraftLineBoard _draft = new();
    private IReadOnlyList<SalesOrderRow> _orders = [];
    private Dictionary<Guid, string> _materialLabels = [];

    public SalesView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Sales);
        _draft.Bind(DraftLinesGrid);
        OfficeUi.BindHelp(ConfirmHelpHost, OfficeHelpTexts.Keys.FieldConfirmSalesOrder);
        OfficeUi.BindHelp(CancelHelpHost, OfficeHelpTexts.Keys.FieldCancelSalesOrder);
        OfficeUi.BindHelp(CloseHelpHost, OfficeHelpTexts.Keys.FieldCloseSalesOrder);
        OfficeUi.BindHelp(RejectHelpHost, OfficeHelpTexts.Keys.FieldRejectSalesOrder);
        OfficeUi.BindHelp(IncotermsHelpHost, OfficeHelpTexts.Keys.FieldIncoterms);
        OfficeUi.BindHelp(CustomsHelpHost, OfficeHelpTexts.Keys.FieldCustomsRef);
        OfficeUi.BindHelp(DnHelpHost, OfficeHelpTexts.Keys.FieldPostDelivery);
        OfficeUi.BindHelp(ReturnHelpHost, OfficeHelpTexts.Keys.FieldSalesReturn);
        OfficeUi.BindHelp(DeliveryPdfHelpHost, OfficeHelpTexts.Keys.FieldDeliveryPdf);
        OfficeUi.BindHelp(PackingHelpHost, OfficeHelpTexts.Keys.FieldPackingList);
        OfficeUi.BindHelp(HuHelpHost, OfficeHelpTexts.Keys.FieldCreateHuSscc);
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
            ("Entregada", "Posted"));
        ApplyPermissions();
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        CreateButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderCreate);
        ConfirmButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderConfirm);
        CancelButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderCancel);
        CloseButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderClose);
        RejectButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderCancel);
        PostDnButton.IsVisible = gate.Has(AppPermissionNames.DeliveryPost);
        PostAllDnButton.IsVisible = gate.Has(AppPermissionNames.DeliveryPost);
        ReturnButton.IsVisible = gate.Has(AppPermissionNames.ReturnManage);
        PdfSoButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderCreate);
        PrintSoButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderCreate);
        DeliveryPdfButton.IsVisible = gate.Has(AppPermissionNames.DeliveryPost);
        PrintDeliveryButton.IsVisible = gate.Has(AppPermissionNames.DeliveryPost);
        PackingPdfButton.IsVisible = gate.Has(AppPermissionNames.DeliveryNoteManage);
        PrintPackingButton.IsVisible = gate.Has(AppPermissionNames.DeliveryNoteManage);
        CreateHuButton.IsVisible = gate.Has(AppPermissionNames.DeliveryNoteManage);
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
            PickerHelpers.BindParties(CustomerBox, _client.Masters.Customers);
            PickerHelpers.BindWarehouses(WarehouseBox, _client.Masters.Warehouses);
            PickerHelpers.BindMaterials(MaterialBox, _client.Masters.Materials);
            _orders = await _client.GetSalesOrdersAsync();
            BindOrdersGrid();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
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
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private void OnOrderSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (OrdersGrid.SelectedIndex < 0 || OrdersGrid.SelectedIndex >= _orders.Count)
        {
            LinesGrid.ItemsSource = null;
            _ = BindDeliveriesAsync(null);
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
        StatusText.Text = string.Empty;
        if (order.Lines.Count > 0)
        {
            DnQtyBox.Text = order.Lines[0].OpenQuantity.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        _ = BindDeliveriesAsync(order.Id);
    }

    private async Task BindDeliveriesAsync(Guid? salesOrderId, Guid? selectId = null)
    {
        try
        {
            var rows = (await _client.GetDeliveriesAsync(salesOrderId)).ToList();
            PickerHelpers.BindDeliveries(DeliveryBox, rows);
            if (selectId is Guid id)
            {
                PickerHelpers.SelectId(DeliveryBox, id);
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
        }
    }

    private bool TrySelectedDelivery(out Guid deliveryId)
    {
        if (PickerHelpers.TryGetId(DeliveryBox, out deliveryId) && deliveryId != Guid.Empty)
        {
            return true;
        }

        StatusText.Text =
            "Seleccione una entrega (albarán) por número. Se rellena al contabilizar la entrega.";
        return false;
    }

    private string MaterialLabel(Guid materialId) =>
        _materialLabels.TryGetValue(materialId, out var label)
            ? label
            : materialId.ToString("D");

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
            || qty <= 0)
        {
            error = "Seleccione material y cantidad > 0.";
            return false;
        }

        var label = _materialLabels.TryGetValue(materialId, out var known)
            ? known
            : MaterialLabel(materialId);
        line = new DraftLineBoard.DraftLine(materialId, label, qty, 0m);
        return true;
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = string.Empty;
        if (!CreateButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para crear pedidos de venta.";
            return;
        }

        try
        {
            if (!PickerHelpers.TryGetId(CustomerBox, out var customerId)
                || !PickerHelpers.TryGetId(WarehouseBox, out var warehouseId))
            {
                StatusText.Text = "Seleccione cliente y almacén.";
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

            await _client.CreateSalesOrderAsync(
                new CreateSalesOrderRequest(
                    customerId,
                    warehouseId,
                    DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                    "EUR",
                    null,
                    lines.Select(line =>
                        new CreateSalesOrderLineRequest(line.MaterialId, line.Quantity)).ToArray(),
                    string.IsNullOrWhiteSpace(IncotermsBox.Text)
                        ? null
                        : IncotermsBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(CustomsBox.Text)
                        ? null
                        : CustomsBox.Text.Trim()));
            _draft.Clear();
            _draft.Bind(DraftLinesGrid);
            await UiHelpers.ShowInfoAsync(this, $"Pedido de venta creado ({lines.Count} línea(s)).");
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

    private async void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        if (!ConfirmButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para confirmar pedidos.";
            return;
        }

        if (!TrySelected(out var order))
        {
            return;
        }

        if (order.Status is not "Requested")
        {
            StatusText.Text =
                $"Solo se confirman pedidos en estado Solicitado. Actual: {OfficeUi.StatusToEs(order.Status)}.";
            return;
        }

        try
        {
            var result = await _client.ConfirmSalesOrderAsync(order.Id);
            if (result.Warnings.Count > 0)
            {
                await UiHelpers.ShowInfoAsync(
                    this,
                    "Confirmado con avisos de disponibilidad:\n"
                    + string.Join("\n", result.Warnings));
                StatusText.Text = string.Join("; ", result.Warnings);
            }
            else
            {
                StatusText.Text = "Pedido confirmado (disponibilidad cubierta).";
            }

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

    private async void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (!CancelButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para cancelar pedidos.";
            return;
        }

        if (!TrySelected(out var order))
        {
            return;
        }

        if (IsTerminal(order.Status))
        {
            StatusText.Text =
                $"El pedido {order.Number} está {OfficeUi.StatusToEs(order.Status).ToLowerInvariant()} y no se puede cancelar.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var reason = await AppDialog.PromptAsync(
            owner,
            "Cancelar pedido",
            "Indique el motivo de cancelación (obligatorio).");
        if (reason is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            StatusText.Text = "El motivo de cancelación es obligatorio.";
            return;
        }

        await RunSelectedAsync(id => _client.CancelSalesOrderAsync(owner, id, reason.Trim()));
    }

    private async void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (!CloseButton.IsVisible)
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

        await RunSelectedAsync(id => _client.CloseSalesOrderAsync(owner, id));
    }

    private async void OnRejectClick(object? sender, RoutedEventArgs e)
    {
        if (!RejectButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para rechazar pedidos.";
            return;
        }

        if (!TrySelected(out var order))
        {
            return;
        }

        if (order.Status is not "Requested")
        {
            StatusText.Text =
                $"Solo se rechazan pedidos Solicitados. Actual: {OfficeUi.StatusToEs(order.Status)}.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        await RunSelectedAsync(id => _client.RejectSalesOrderAsync(owner, id));
    }

    private async void OnPostDnClick(object? sender, RoutedEventArgs e)
    {
        if (!PostDnButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para contabilizar entregas.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var order))
        {
            return;
        }

        // Misma regla que PostingService (AAA-DELIVERY-0002): solo Preparado / Parcialmente entregado.
        if (order.Status is not "Prepared" and not "PartiallyDelivered")
        {
            StatusText.Text =
                $"Para contabilizar entrega (PGI) el pedido debe estar Preparado (ALMACÉN → Preparación → Marcar preparado). Actual: {OfficeUi.StatusToEs(order.Status)}.";
            return;
        }

        if (order.Lines.Count == 0
            || !UiHelpers.TryParseDecimal(DnQtyBox.Text, out var qty)
            || qty <= 0)
        {
            StatusText.Text = "Seleccione un pedido con líneas e indique cantidad de entrega > 0.";
            return;
        }

        var line = order.Lines[0];
        if (LinesGrid.SelectedIndex >= 0 && LinesGrid.SelectedIndex < order.Lines.Count)
        {
            line = order.Lines[LinesGrid.SelectedIndex];
        }

        if (line.OpenQuantity <= 0)
        {
            StatusText.Text = "La línea no tiene cantidad abierta para entregar.";
            return;
        }

        if (qty > line.OpenQuantity)
        {
            StatusText.Text =
                $"La cantidad ({qty}) supera la abierta ({line.OpenQuantity}).";
            return;
        }

        try
        {
            var result = await _client.PostDeliveryAsync(
                owner,
                new PostDeliveryRequest(
                    order.Id,
                    DateOnly.FromDateTime(DateTime.Today),
                    [new PostDeliveryLineRequest(line.Id, null, qty)]));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Entrega contabilizada: {result.DocumentNumber ?? result.DocumentId.ToString("D")}");
            await RefreshAsync();
            await BindDeliveriesAsync(order.Id, result.DocumentId);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPostAllDnClick(object? sender, RoutedEventArgs e)
    {
        if (!PostAllDnButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para contabilizar entregas.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var order))
        {
            return;
        }

        if (order.Status is not "Prepared" and not "PartiallyDelivered")
        {
            StatusText.Text =
                $"Para entregar abiertas el pedido debe estar Preparado. Actual: {OfficeUi.StatusToEs(order.Status)}.";
            return;
        }

        var openLines = order.Lines.Where(line => line.OpenQuantity > 0).ToArray();
        if (openLines.Length == 0)
        {
            StatusText.Text = "No hay cantidades abiertas que entregar.";
            return;
        }

        try
        {
            var result = await _client.PostDeliveryAsync(
                owner,
                new PostDeliveryRequest(
                    order.Id,
                    DateOnly.FromDateTime(DateTime.Today),
                    openLines.Select(line =>
                        new PostDeliveryLineRequest(line.Id, null, line.OpenQuantity)).ToArray()));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Entrega de {openLines.Length} línea(s): {result.DocumentNumber ?? result.DocumentId.ToString("D")}");
            await RefreshAsync();
            await BindDeliveriesAsync(order.Id, result.DocumentId);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPdfClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadSalesOrderPdfAsync(order.Id);
            await UiHelpers.SaveBytesAsync(this, $"pedido-venta-{order.Number}.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
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
            var bytes = await _client.DownloadSalesOrderPdfAsync(order.Id);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                $"Imprimir pedido {order.Number}",
                $"pedido-venta-{order.Number}.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnDeliveryPdfClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedDelivery(out var id))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadDeliveryPdfAsync(id);
            await UiHelpers.SaveBytesAsync(this, $"entrega-{id:N}.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintDeliveryClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedDelivery(out var id))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadDeliveryPdfAsync(id);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir entrega",
                $"entrega-{id:N}.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPackingListClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedDelivery(out var id))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadPackingListPdfAsync(id);
            await UiHelpers.SaveBytesAsync(this, $"lista-empaque-{id:N}.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintPackingClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedDelivery(out var id))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadPackingListPdfAsync(id);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir lista de empaque",
                $"lista-empaque-{id:N}.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnCreateHuClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        Guid? deliveryId = PickerHelpers.TryGetId(DeliveryBox, out var parsedDelivery)
            && parsedDelivery != Guid.Empty
            ? parsedDelivery
            : null;

        try
        {
            var unit = await _client.CreateHandlingUnitAsync(
                new CreateHandlingUnitRequest(
                    order.Id,
                    deliveryId,
                    1));
            await UiHelpers.ShowInfoAsync(this, $"Bulto creado · SSCC {unit.Sscc}");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnReturnClick(object? sender, RoutedEventArgs e)
    {
        if (!ReturnButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para devoluciones.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var order))
        {
            return;
        }

        if (order.Lines.Count == 0
            || !UiHelpers.TryParseDecimal(DnQtyBox.Text, out var qty)
            || qty <= 0
            || !PickerHelpers.TryGetId(WarehouseBox, out var warehouseId)
            || !PickerHelpers.TryGetId(MaterialBox, out var materialId))
        {
            StatusText.Text =
                "Para devolución: material, almacén y cantidad (> 0) son obligatorios.";
            return;
        }

        Guid? deliveryId = PickerHelpers.TryGetId(DeliveryBox, out var parsed)
            && parsed != Guid.Empty
            ? parsed
            : null;

        try
        {
            var result = await _client.PostSalesReturnAsync(
                owner,
                new PostSalesReturnRequest(
                    order.Id,
                    deliveryId,
                    "ReturnIn",
                    DateOnly.FromDateTime(DateTime.Today),
                    "Devolución comercial",
                    [
                        new PostSalesReturnLineRequest(
                            materialId,
                            warehouseId,
                            null,
                            qty),
                    ]));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Devolución contabilizada: {result.DocumentId:D}");
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
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelected(out SalesOrderRow order)
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
        status is "Cancelled" or "Canceled" or "Closed" or "Delivered" or "Rejected";
}
