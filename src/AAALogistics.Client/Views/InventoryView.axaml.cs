using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class InventoryView : UserControl
{
    private readonly LogisticsApiClient _client;
    private readonly List<PickerHelpers.PickerItem> _countItems = [];

    public InventoryView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Inventory);
        OfficeUi.BindHelp(ReconcileHelpHost, OfficeHelpTexts.Keys.FieldReconcileStock);
        OfficeUi.BindHelp(OpeningHelpHost, OfficeHelpTexts.Keys.FieldStockOpening);
        OfficeUi.BindHelp(TransferHelpHost, OfficeHelpTexts.Keys.FieldWarehouseTransfer);
        OfficeUi.BindHelp(AdjustHelpHost, OfficeHelpTexts.Keys.FieldStockAdjustment);
        OfficeUi.BindHelp(ReverseHelpHost, OfficeHelpTexts.Keys.FieldReverseMovement);
        OfficeUi.BindHelp(MinQtyHelpHost, OfficeHelpTexts.Keys.FieldSetMinQty);
        OfficeUi.BindHelp(CreateCountHelpHost, OfficeHelpTexts.Keys.FieldCreateCount);
        OfficeUi.BindHelp(PostCountHelpHost, OfficeHelpTexts.Keys.FieldPostInventoryCount);
        OfficeUi.BindHelp(LoadLocationsHelpHost, OfficeHelpTexts.Keys.FieldLoadLocations);
        OfficeUi.BindHelp(LotIdHelpHost, OfficeHelpTexts.Keys.FieldLotId);
        OfficeUi.BindHelp(StatusHelpHost, OfficeHelpTexts.Keys.FieldStockStatus);
        MaterialBox.SelectionChanged += async (_, _) =>
        {
            try
            {
                await ReloadLotsAsync();
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
                StatusText.Text = UiHelpers.ToUserMessage(exception);
            }
        };
        ApplyPermissions();
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyPermissions();
        try
        {
            await _client.Masters.RefreshAsync();
            PickerHelpers.BindMaterials(MaterialBox, _client.Masters.Materials);
            PickerHelpers.BindWarehouses(WarehouseBox, _client.Masters.Warehouses);
            PickerHelpers.BindWarehouses(Warehouse2Box, _client.Masters.Warehouses);
            await ReloadLotsAsync();
            await BindMovementsComboAsync();
            PickerHelpers.BindItems(CountBox, _countItems);
            var tag = SelectedTag();
            if (tag == "movements")
            {
                var movements = await _client.GetMovementsAsync();
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Tipo", "TypeEs"),
                    ("Cantidad", "SignedQuantity"),
                    ("Material", "Material"),
                    ("Almacén", "Warehouse"),
                    ("Motivo", "Reason"));
                var materials = _client.Masters.Materials.ToDictionary(m => m.Id, m => m.Code);
                var warehouses = _client.Masters.Warehouses.ToDictionary(w => w.Id, w => w.Code);
                var rows = movements.Select(movement => new
                {
                    TypeEs = MovementTypeToEs(movement.Type),
                    movement.SignedQuantity,
                    Material = materials.TryGetValue(movement.MaterialId, out var mc) ? mc : "—",
                    Warehouse = warehouses.TryGetValue(movement.WarehouseId, out var wc) ? wc : "—",
                    Reason = movement.Reason ?? "—",
                }).ToList();
                Grid.ItemsSource = rows;
                SetEmpty(rows.Count == 0, "No hay movimientos. Contabilice una recepción, apertura o ajuste.");
            }
            else if (tag == "below")
            {
                var below = await _client.GetBelowMinimumAsync();
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Material", nameof(StockRow.MaterialCode)),
                    ("Almacén", nameof(StockRow.WarehouseCode)),
                    ("Cantidad", nameof(StockRow.Quantity)),
                    ("Mínimo", nameof(StockRow.MinQuantity)));
                Grid.ItemsSource = below;
                SetEmpty(
                    below.Count == 0,
                    "Ningún material está bajo el mínimo. Use Fijar mínimo para definir umbrales.");
            }
            else
            {
                var stock = await _client.GetStockAsync();
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Material", "MaterialCode"),
                    ("Almacén", "WarehouseCode"),
                    ("Ubicación", "Location"),
                    ("Cantidad", "Quantity"),
                    ("Mínimo", "MinQuantity"),
                    ("Estado", "StatusEs"));
                var rows = stock.Select(row => new
                {
                    row.MaterialCode,
                    row.WarehouseCode,
                    Location = string.IsNullOrWhiteSpace(row.LocationCode)
                        ? "raíz"
                        : string.Equals(row.LocationCode, "DEFAULT", StringComparison.OrdinalIgnoreCase)
                            ? "raíz"
                            : row.LocationCode,
                    row.Quantity,
                    row.MinQuantity,
                    StatusEs = OfficeUi.StatusToEs(row.Status),
                }).ToList();
                Grid.ItemsSource = rows;
                SetEmpty(
                    rows.Count == 0,
                    "No hay stock. Use Apertura de stock o contabilice una recepción de compra.");
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private void SetEmpty(bool empty, string hint)
    {
        EmptyHint.Text = hint;
        EmptyHint.IsVisible = empty;
        Grid.IsVisible = !empty;
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        TransferButton.IsVisible = gate.Has(AppPermissionNames.StockTransfer);
        AdjustButton.IsVisible = gate.Has(AppPermissionNames.StockAdjust);
        // Opening posts an adjustment; OpeningImport alone cannot call /stock/adjustments/post.
        OpeningButton.IsVisible = gate.Has(AppPermissionNames.StockAdjust);
        ReverseButton.IsVisible = gate.Has(AppPermissionNames.StockReverse);
        CountButton.IsVisible = gate.Has(AppPermissionNames.StockCount);
        PostCountButton.IsVisible = gate.Has(AppPermissionNames.StockCount);
        ReconcileButton.IsVisible = gate.Has(AppPermissionNames.StockAdjust);
        MinQtyButton.IsVisible = gate.Has(AppPermissionNames.StockAdjust);
        LotsButton.IsVisible = gate.Has(AppPermissionNames.StockRead);
        SetStatusButton.IsVisible = gate.Has(AppPermissionNames.StockAdjust);
    }

    private string SelectedTag() =>
        ViewBox.SelectedItem is ComboBoxItem item && item.Tag is string tag ? tag : "stock";

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnViewChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized)
        {
            await RefreshAsync();
        }
    }

    private async void OnReconcileClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        try
        {
            var result = await _client.ReconcileStockAsync(owner);
            ReconcileText.Text = result.IsConsistent
                ? "Libro de inventario consistente (movimientos = stock)."
                : $"Hay diferencias entre libro y stock: {result.Differences.Count}.";
            if (!result.IsConsistent)
            {
                Grid.ItemsSource = result.Differences;
                EmptyHint.IsVisible = false;
                Grid.IsVisible = true;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnOpeningClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TryIds(out var materialId, out var warehouseId, out var qty))
        {
            StatusText.Text =
                "Para apertura: seleccione material, almacén y cantidad positiva.";
            return;
        }

        if (qty <= 0)
        {
            StatusText.Text = "La apertura requiere cantidad positiva.";
            return;
        }

        try
        {
            await _client.PostAdjustmentAsync(
                owner,
                new PostAdjustmentRequest(
                    materialId,
                    warehouseId,
                    qty,
                    "Opening",
                    ReasonBox.Text?.Trim() is { Length: > 0 } reason
                        ? reason
                        : "Apertura de stock"));
            await UiHelpers.ShowInfoAsync(this, "Apertura de stock contabilizada.");
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

    private async void OnTransferClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null
            || !TryIds(out var materialId, out var source, out var qty)
            || !PickerHelpers.TryGetId(Warehouse2Box, out var dest))
        {
            StatusText.Text = "Seleccione material, almacén origen, destino y cantidad.";
            return;
        }

        if (qty <= 0)
        {
            StatusText.Text = "La cantidad de traspaso debe ser mayor que 0.";
            return;
        }

        if (source == dest)
        {
            StatusText.Text = "El almacén destino debe ser distinto del origen.";
            return;
        }

        try
        {
            await _client.PostTransferAsync(
                owner,
                new PostTransferRequest(
                    materialId,
                    source,
                    dest,
                    qty,
                    ReasonBox.Text?.Trim() is { Length: > 0 } reason
                        ? reason
                        : "Traspaso entre almacenes"));
            await UiHelpers.ShowInfoAsync(this, "Traspaso contabilizado.");
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

    private async void OnAdjustClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TryIds(out var materialId, out var warehouseId, out var qty))
        {
            StatusText.Text =
                "Seleccione material, almacén y cantidad firmada (positiva o negativa).";
            return;
        }

        if (qty == 0)
        {
            StatusText.Text = "La cantidad de ajuste no puede ser 0.";
            return;
        }

        try
        {
            await _client.PostAdjustmentAsync(
                owner,
                new PostAdjustmentRequest(
                    materialId,
                    warehouseId,
                    qty,
                    "Adjustment",
                    ReasonBox.Text?.Trim() is { Length: > 0 } reason
                        ? reason
                        : "Ajuste de stock"));
            await UiHelpers.ShowInfoAsync(this, "Ajuste contabilizado.");
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

    private async Task BindMovementsComboAsync()
    {
        var movements = await _client.GetMovementsAsync();
        var materials = _client.Masters.Materials.ToDictionary(m => m.Id, m => m.Code);
        PickerHelpers.BindItems(
            MovementBox,
            movements.Take(80).Select(movement =>
            {
                var material = materials.TryGetValue(movement.MaterialId, out var code)
                    ? code
                    : "—";
                var label =
                    $"{MovementTypeToEs(movement.Type)} · {material} · {movement.SignedQuantity:0.####} · {movement.PostedUtc:yyyy-MM-dd HH:mm}";
                return new PickerHelpers.PickerItem(movement.Id, label);
            }));
    }

    private async Task ReloadLotsAsync()
    {
        if (!PickerHelpers.TryGetId(MaterialBox, out var materialId))
        {
            PickerHelpers.BindLots(LotBox, Array.Empty<LotResponse>(), includeEmpty: true);
            return;
        }

        var lots = await _client.GetLotsAsync(materialId);
        PickerHelpers.BindLots(LotBox, lots, includeEmpty: true);
    }

    private async void OnReverseClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null
            || !PickerHelpers.TryGetId(MovementBox, out var movementId)
            || movementId == Guid.Empty)
        {
            StatusText.Text = "Seleccione el movimiento a revertir en el desplegable.";
            return;
        }

        try
        {
            await _client.ReverseMovementAsync(
                owner,
                movementId,
                new ReverseMovementRequest(
                    ReasonBox.Text?.Trim() is { Length: > 0 } reason
                        ? reason
                        : "Reverso de movimiento"));
            await UiHelpers.ShowInfoAsync(this, "Movimiento revertido.");
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

    private async void OnMinQtyClick(object? sender, RoutedEventArgs e)
    {
        if (!TryIds(out var materialId, out var warehouseId, out _)
            || !UiHelpers.TryParseDecimal(MinQtyBox.Text, out var min))
        {
            StatusText.Text = "Seleccione material, almacén e indique el mínimo (≥ 0).";
            return;
        }

        if (min < 0)
        {
            StatusText.Text = "El mínimo no puede ser negativo.";
            return;
        }

        try
        {
            await _client.SetMinQuantityAsync(
                new SetMinQuantityRequest(materialId, warehouseId, min));
            await UiHelpers.ShowInfoAsync(this, $"Mínimo fijado en {min}.");
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

    private async void OnCreateCountClick(object? sender, RoutedEventArgs e)
    {
        if (!TryIds(out var materialId, out var warehouseId, out var qty))
        {
            StatusText.Text = "Seleccione material, almacén y cantidad contada.";
            return;
        }

        if (qty < 0)
        {
            StatusText.Text = "La cantidad contada no puede ser negativa.";
            return;
        }

        try
        {
            var count = await _client.CreateInventoryCountAsync(
                new CreateInventoryCountRequest(
                    warehouseId,
                    [new CreateInventoryCountLineRequest(materialId, qty)]));
            _countItems.RemoveAll(item => item.Id == count.Id);
            _countItems.Insert(0, new PickerHelpers.PickerItem(count.Id, count.Number));
            PickerHelpers.BindItems(CountBox, _countItems);
            PickerHelpers.SelectId(CountBox, count.Id);
            await UiHelpers.ShowInfoAsync(
                this,
                $"Conteo {count.Number} creado. Pulse Contabilizar conteo para aplicarlo.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPostCountClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null
            || !PickerHelpers.TryGetId(CountBox, out var countId)
            || countId == Guid.Empty)
        {
            StatusText.Text =
                "Seleccione el conteo en el desplegable (se rellena al crear).";
            return;
        }

        try
        {
            await _client.PostInventoryCountAsync(owner, countId);
            await UiHelpers.ShowInfoAsync(this, "Conteo contabilizado.");
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

    private async void OnLotsClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(MaterialBox, out var materialId))
        {
            StatusText.Text = "Seleccione un material.";
            return;
        }

        try
        {
            var lots = await _client.GetLotsAsync(materialId);
            Grid.ItemsSource = lots;
            SetEmpty(lots.Count == 0, "Sin lotes para el material seleccionado.");
            StatusText.Text = lots.Count == 0
                ? "Sin lotes para el material."
                : $"{lots.Count} lote(s).";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnLoadLocationsClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(WarehouseBox, out var warehouseId))
        {
            StatusText.Text = "Seleccione almacén.";
            return;
        }

        try
        {
            var locations = await _client.GetLocationsAsync(warehouseId);
            PickerHelpers.BindLocations(LocationBox, locations);
            StatusText.Text = locations.Count == 0
                ? "Sin ubicaciones en este almacén. Créelas en el menú Ubicaciones."
                : $"{locations.Count} ubicaciones cargadas.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnSetStatusClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null
            || !PickerHelpers.TryGetId(MaterialBox, out var materialId)
            || !PickerHelpers.TryGetId(WarehouseBox, out var warehouseId)
            || !PickerHelpers.TryGetId(LocationBox, out var locationId))
        {
            StatusText.Text =
                "Material, almacén y ubicación son obligatorios (pulse Cargar ubicaciones).";
            return;
        }

        var lotId = PickerHelpers.TryGetId(LotBox, out var selectedLot)
            ? selectedLot
            : Guid.Empty;

        var status = (StatusBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()
            ?? "Available";
        try
        {
            await _client.SetStockStatusAsync(
                owner,
                new SetStockStatusRequest(
                    materialId,
                    warehouseId,
                    locationId,
                    status,
                    lotId));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Estado cambiado a {OfficeUi.StatusToEs(status)}.");
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

    private bool TryIds(out Guid materialId, out Guid warehouseId, out decimal qty)
    {
        materialId = default;
        warehouseId = default;
        qty = default;
        return PickerHelpers.TryGetId(MaterialBox, out materialId)
            && PickerHelpers.TryGetId(WarehouseBox, out warehouseId)
            && UiHelpers.TryParseDecimal(QtyBox.Text, out qty);
    }

    private static string MovementTypeToEs(string type) =>
        type switch
        {
            "Opening" => "Apertura",
            "Adjustment" => "Ajuste",
            "Transfer" or "TransferOut" or "TransferIn" => "Traspaso",
            "GoodsReceipt" => "Recepción",
            "Delivery" or "GoodsIssue" => "Entrega",
            "Reverse" or "Reversal" => "Reverso",
            "Count" or "InventoryCount" => "Conteo",
            "Return" or "ReturnIn" => "Devolución",
            "Putaway" => "Colocación en hueco",
            _ => OfficeUi.StatusToEs(type),
        };
}
