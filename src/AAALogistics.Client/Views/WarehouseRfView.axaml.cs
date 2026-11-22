using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class WarehouseRfView : UserControl
{
    private readonly LogisticsApiClient _client;
    private IReadOnlyList<PrepPickLineResponse> _picks = [];

    public WarehouseRfView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.WarehouseRf);
        OfficeUi.SetTextColumns(
            PicksGrid,
            ("Material", "Material"),
            ("Ubicación", "Location"),
            ("Cantidad", "Quantity"),
            ("Confirmada", "Confirmed"));
        ApplyGates();
        WarehouseBox.SelectionChanged += async (_, _) =>
        {
            try
            {
                await ReloadLocationsAsync();
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
                StatusText.Text = UiHelpers.ToUserMessage(exception);
            }
        };
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
    }

    private void ApplyGates()
    {
        var gate = _client.Permissions;
        PutawayButton.IsVisible = gate.Has(AppPermissionNames.StockTransfer);
        ConfirmPickButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
        CountButton.IsVisible = gate.Has(AppPermissionNames.StockAdjust);
        ScanButton.IsVisible = gate.Has(AppPermissionNames.StockRead);
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyGates();
        try
        {
            await _client.Masters.RefreshAsync();
            PickerHelpers.BindWarehouses(WarehouseBox, _client.Masters.Warehouses);
            PickerHelpers.BindMaterials(MaterialBox, _client.Masters.Materials);
            var orders = (await _client.GetSalesOrdersAsync())
                .Where(order => order.Status is "Confirmed" or "InPreparation" or "Prepared")
                .ToList();
            PickerHelpers.BindSalesOrders(PickOrderBox, orders);
            EmptyHint.IsVisible = orders.Count == 0 && _picks.Count == 0;
            await ReloadLocationsAsync();
            await ReloadLotsAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task ReloadLocationsAsync()
    {
        if (!PickerHelpers.TryGetId(WarehouseBox, out var warehouseId))
        {
            PickerHelpers.BindLocations(LocationBox, Array.Empty<LocationNodeResponse>());
            return;
        }

        var locations = await _client.GetLocationsAsync(warehouseId);
        var bins = locations
            .Where(row => row.IsActive
                && !string.Equals(row.Kind, "Root", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(row.Code, "DEFAULT", StringComparison.OrdinalIgnoreCase))
            .ToList();
        PickerHelpers.BindLocations(LocationBox, bins);
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

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnScanClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ScanBox.Text))
        {
            StatusText.Text = "Indique un código o SSCC.";
            return;
        }

        try
        {
            var result = await _client.RfScanAsync(ScanBox.Text.Trim());
            var label = string.IsNullOrWhiteSpace(result.ResolvedLabel)
                ? result.Code
                : result.ResolvedLabel;
            ScanResultText.Text = $"{result.Kind}: {label}";
            StatusText.Text = string.Empty;
            if (result.ResolvedId is Guid id
                && string.Equals(result.Kind, "Material", StringComparison.OrdinalIgnoreCase))
            {
                PickerHelpers.SelectId(MaterialBox, id);
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPutawayClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(WarehouseBox, out var warehouseId)
            || !PickerHelpers.TryGetId(MaterialBox, out var materialId)
            || !PickerHelpers.TryGetId(LocationBox, out var locationId))
        {
            StatusText.Text = "Seleccione almacén, material y hueco destino.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(PutawayQtyBox.Text, out var qty) || qty <= 0)
        {
            StatusText.Text = "Cantidad inválida.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var lotId = Guid.Empty;
        if (PickerHelpers.TryGetId(LotBox, out var selectedLot) && selectedLot != Guid.Empty)
        {
            lotId = selectedLot;
        }

        try
        {
            await _client.PostPutawayAsync(
                owner,
                new PostPutawayRequest(materialId, warehouseId, locationId, qty, lotId));
            StatusText.Text = "Colocación registrada.";
            await UiHelpers.ShowInfoAsync(this, "Putaway OK.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnLoadPicksClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(PickOrderBox, out var orderId))
        {
            StatusText.Text = "Seleccione un pedido de venta.";
            return;
        }

        try
        {
            await LoadPicksAsync(orderId);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task LoadPicksAsync(Guid orderId)
    {
        await _client.Masters.RefreshAsync();
        _picks = await _client.GetPrepPicksAsync(orderId);
        var materials = _client.Masters.Materials.ToDictionary(
            row => row.Id,
            row => string.IsNullOrWhiteSpace(row.DisplayName)
                ? row.Code
                : $"{row.Code} — {row.DisplayName}");
        var locationLabels = new Dictionary<Guid, string>();
        foreach (var warehouse in _client.Masters.Warehouses)
        {
            try
            {
                foreach (var node in await _client.GetLocationsAsync(warehouse.Id))
                {
                    locationLabels[node.Id] = node.Code;
                }
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
            }
        }

        var rows = _picks.Select(pick => new
        {
            PickId = pick.Id,
            Material = materials.TryGetValue(pick.MaterialId, out var label)
                ? label
                : "—",
            Location = pick.SuggestedLocationId == Guid.Empty
                ? "—"
                : (locationLabels.TryGetValue(pick.SuggestedLocationId, out var loc) ? loc : "—"),
            Quantity = pick.Quantity,
            Confirmed = pick.ConfirmedQuantity,
        }).ToList();
        PicksGrid.ItemsSource = rows;
        EmptyHint.IsVisible = rows.Count == 0;
        StatusText.Text = rows.Count == 0
            ? "Sin líneas de picking. Entre a preparación desde Prep si hace falta."
            : $"{rows.Count} líneas de picking.";
    }

    private async void OnConfirmPickClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(PickOrderBox, out var orderId))
        {
            StatusText.Text = "Seleccione un pedido.";
            return;
        }

        if (PicksGrid.SelectedItem is null)
        {
            StatusText.Text = "Seleccione una línea de picking.";
            return;
        }

        var pickIdProp = PicksGrid.SelectedItem.GetType().GetProperty("PickId");
        if (pickIdProp?.GetValue(PicksGrid.SelectedItem) is not Guid pickId)
        {
            StatusText.Text = "Línea inválida.";
            return;
        }

        var pick = _picks.FirstOrDefault(row => row.Id == pickId);
        if (pick is null)
        {
            StatusText.Text = "Recargue las líneas de picking.";
            return;
        }

        var qty = pick.Quantity - pick.ConfirmedQuantity;
        if (qty <= 0)
        {
            StatusText.Text = "La línea ya está confirmada.";
            return;
        }

        try
        {
            await _client.ConfirmPrepPickAsync(orderId, pickId, qty);
            StatusText.Text = "Línea confirmada.";
            await LoadPicksAsync(orderId);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnCountClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(WarehouseBox, out var warehouseId)
            || !PickerHelpers.TryGetId(MaterialBox, out var materialId))
        {
            StatusText.Text = "Seleccione almacén y material.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(CountQtyBox.Text, out var counted) || counted < 0)
        {
            StatusText.Text = "Cantidad contada inválida.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        try
        {
            var count = await _client.CreateInventoryCountAsync(
                new CreateInventoryCountRequest(
                    warehouseId,
                    [new CreateInventoryCountLineRequest(materialId, counted)]));
            await _client.PostInventoryCountAsync(owner, count.Id);
            StatusText.Text = $"Conteo {count.Number} contabilizado.";
            await UiHelpers.ShowInfoAsync(this, $"Conteo {count.Number} OK.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }
}
