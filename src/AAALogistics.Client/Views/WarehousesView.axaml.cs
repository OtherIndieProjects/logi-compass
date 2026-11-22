using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class WarehousesView : UserControl
{
    private readonly LogisticsApiClient _client;
    private IReadOnlyList<WarehouseRow> _warehouses = [];
    private IReadOnlyList<LocationNodeResponse> _locations = [];
    private IReadOnlyList<LocationGridRow> _gridRows = [];

    public WarehousesView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Ubicaciones);
        OfficeUi.BindHelp(DefaultHelpHost, OfficeHelpTexts.Keys.FieldDefaultLocation);
        OfficeUi.BindHelp(ParentHelpHost, OfficeHelpTexts.Keys.FieldLocationParent);
        OfficeUi.BindHelp(KindHelpHost, OfficeHelpTexts.Keys.FieldLocationKind);
        OfficeUi.BindHelp(LocationTransferHelpHost, OfficeHelpTexts.Keys.FieldLocationTransfer);
        OfficeUi.BindHelp(PutawayHelpHost, OfficeHelpTexts.Keys.FieldPutaway);
        OfficeUi.SetTextColumns(
            WarehousesGrid,
            ("Código", nameof(WarehouseRow.Code)),
            ("Nombre", nameof(WarehouseRow.Name)));
        ConfigureLocationColumns();
        var gate = client.Permissions;
        var canManage = gate.Has(AppPermissionNames.LocationManage);
        var canManageWarehouse = gate.Has(AppPermissionNames.WarehouseManage);
        CreateLocationButton.IsVisible = canManage;
        EditLocationButton.IsVisible = canManage;
        ToggleLocationButton.IsVisible = canManage;
        DeleteLocationButton.IsVisible = canManage;
        NewWarehouseButton.IsVisible = canManageWarehouse;
        DeleteWarehouseButton.IsVisible = canManageWarehouse;
        LocationTransferButton.IsVisible = gate.Has(AppPermissionNames.StockTransfer);
        PutawayButton.IsVisible = gate.Has(AppPermissionNames.StockTransfer);
        BindParentRootOnly();
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        try
        {
            await _client.Masters.RefreshAsync();
            _warehouses = _client.Masters.Warehouses;
            WarehousesGrid.ItemsSource = _warehouses;
            PickerHelpers.BindWarehouses(WarehouseBox, _warehouses);
            PickerHelpers.BindMaterials(MaterialBox, _client.Masters.Materials);
            MaterialBox.SelectionChanged -= OnPutawayMaterialChanged;
            MaterialBox.SelectionChanged += OnPutawayMaterialChanged;
            await ReloadPutawayLotsAsync();
            if (_warehouses.Count == 0)
            {
                _locations = [];
                BindLocationGrid();
                var canCreateWarehouse = _client.Permissions.Has(AppPermissionNames.WarehouseManage);
                EmptyHint.Text = canCreateWarehouse
                    ? "Crea el primer almacén…"
                    : "No hay almacenes. Se necesita el permiso Warehouse.Manage para crearlos aquí (o en Maestros → Almacenes).";
                EmptyHint.IsVisible = true;
                LocationsGrid.IsVisible = false;
            }
            else if (WarehousesGrid.SelectedIndex < 0)
            {
                WarehousesGrid.SelectedIndex = 0;
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

    private void ConfigureLocationColumns()
    {
        LocationsGrid.Columns.Clear();
        OfficeUi.SetTextColumns(
            LocationsGrid,
            ("Código", nameof(LocationGridRow.CodeLabel)),
            ("Nombre", nameof(LocationGridRow.Name)),
            ("Tipo", nameof(LocationGridRow.KindEs)),
            ("Padre", nameof(LocationGridRow.ParentLabel)));
        OfficeUi.SetCheckColumn(LocationsGrid, "Activo", nameof(LocationGridRow.IsActive));
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnWarehouseSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (WarehousesGrid.SelectedIndex < 0
            || WarehousesGrid.SelectedIndex >= _warehouses.Count)
        {
            return;
        }

        var warehouse = _warehouses[WarehousesGrid.SelectedIndex];
        WarehouseBox.SelectedIndex = WarehousesGrid.SelectedIndex;
        await LoadLocationsAsync(warehouse.Id);
    }

    private void OnLocationSelected(object? sender, SelectionChangedEventArgs e)
    {
        UpdateToggleButtonLabel();
        if (LocationsGrid.SelectedItem is LocationGridRow row)
        {
            SelectParentInCombo(row.Id);
        }
    }

    private void UpdateToggleButtonLabel()
    {
        if (LocationsGrid.SelectedItem is LocationGridRow row)
        {
            ToggleLocationButton.Content = row.IsActive ? "Desactivar" : "Activar";
            ToggleLocationButton.IsEnabled = true;
        }
        else
        {
            ToggleLocationButton.Content = "Desactivar";
            ToggleLocationButton.IsEnabled = false;
        }
    }

    private async Task LoadLocationsAsync(Guid warehouseId)
    {
        try
        {
            _locations = await _client.GetLocationsAsync(warehouseId);
            BindLocationGrid();
            BindParentCombo();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
        }
    }

    private void BindLocationGrid()
    {
        var byId = _locations.ToDictionary(row => row.Id);
        _gridRows = _locations
            .Select(row =>
            {
                var parentLabel = "— (raíz)";
                if (row.ParentId is { } parentId
                    && byId.TryGetValue(parentId, out var parent))
                {
                    parentLabel = FormatLocationCode(parent);
                }

                return new LocationGridRow(
                    row.Id,
                    row.ParentId,
                    FormatLocationCode(row),
                    row.Name,
                    KindToEs(row.Kind),
                    parentLabel,
                    row.IsActive,
                    row.Code,
                    row.Kind);
            })
            .OrderBy(row => row.RawCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
        LocationsGrid.ItemsSource = _gridRows;
        var empty = _gridRows.Count == 0;
        EmptyHint.Text =
            "No hay ubicaciones listadas. Cree zona → … → hueco con «Ubicación padre» (raíz del almacén).";
        EmptyHint.IsVisible = empty;
        LocationsGrid.IsVisible = !empty;
        UpdateToggleButtonLabel();
    }

    private void BindParentRootOnly()
    {
        ParentBox.ItemsSource = new[]
        {
            new PickerHelpers.PickerItem(Guid.Empty, "(Raíz)"),
        };
        ParentBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PickerHelpers.PickerItem.Label));
        ParentBox.SelectedIndex = 0;
    }

    private void BindParentCombo()
    {
        var items = new List<PickerHelpers.PickerItem>
        {
            new(Guid.Empty, "(Raíz)"),
        };
        items.AddRange(
            _locations
                .Where(row => row.IsActive)
                .OrderBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
                .Select(row => new PickerHelpers.PickerItem(
                    row.Id,
                    $"{FormatLocationCode(row)} — {row.Name} ({KindToEs(row.Kind)})")));
        var previous = PickerHelpers.SelectedId(ParentBox);
        ParentBox.ItemsSource = items;
        ParentBox.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PickerHelpers.PickerItem.Label));
        if (previous is { } id && id != Guid.Empty)
        {
            SelectParentInCombo(id);
        }
        else
        {
            ParentBox.SelectedIndex = 0;
        }
    }

    private void SelectParentInCombo(Guid locationId)
    {
        if (ParentBox.ItemsSource is not IEnumerable<PickerHelpers.PickerItem> items)
        {
            return;
        }

        var list = items.ToList();
        var index = list.FindIndex(item => item.Id == locationId);
        if (index >= 0)
        {
            ParentBox.SelectedIndex = index;
        }
    }

    private Guid? SelectedParentId()
    {
        var id = PickerHelpers.SelectedId(ParentBox);
        return id is null || id == Guid.Empty ? null : id;
    }

    private async void OnTransferClick(object? sender, RoutedEventArgs e)
    {
        var warehouseId = PickerHelpers.SelectedId(WarehouseBox);
        var materialId = PickerHelpers.SelectedId(MaterialBox);
        if (warehouseId is null || materialId is null)
        {
            StatusText.Text = "Almacén, material y ubicación destino (selección) obligatorios.";
            return;
        }

        if (LocationsGrid.SelectedItem is not LocationGridRow selected)
        {
            StatusText.Text = "Seleccione ubicación destino.";
            return;
        }

        if (selected.ParentId is null)
        {
            StatusText.Text = "Elija una ubicación hija como destino; origen = padre.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(TransferQtyBox.Text, out var qty)
            || qty <= 0)
        {
            StatusText.Text = "Cantidad inválida.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            StatusText.Text = "Ventana no disponible.";
            return;
        }

        try
        {
            await _client.TransferLocationAsync(
                owner,
                new TransferLocationRequest(
                    materialId.Value,
                    warehouseId.Value,
                    selected.ParentId.Value,
                    selected.Id,
                    qty));
            await UiHelpers.ShowInfoAsync(this, "Traspaso entre ubicaciones registrado.");
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
        var warehouseId = PickerHelpers.SelectedId(WarehouseBox);
        var materialId = PickerHelpers.SelectedId(MaterialBox);
        if (warehouseId is null || materialId is null)
        {
            StatusText.Text = "Almacén y material obligatorios.";
            return;
        }

        if (LocationsGrid.SelectedItem is not LocationGridRow destination)
        {
            StatusText.Text =
                "Seleccione el hueco destino (distinto de la raíz del almacén).";
            return;
        }

        if (string.Equals(destination.RawCode, "DEFAULT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(destination.Kind, "Root", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text =
                "Elija un hueco distinto de la raíz del almacén.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(TransferQtyBox.Text, out var qty)
            || qty <= 0)
        {
            StatusText.Text = "Cantidad inválida.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            StatusText.Text = "Ventana no disponible.";
            return;
        }

        var lotId = Guid.Empty;
        if (PickerHelpers.TryGetId(PutawayLotBox, out var selectedLot)
            && selectedLot != Guid.Empty)
        {
            lotId = selectedLot;
        }

        try
        {
            await _client.PostPutawayAsync(
                owner,
                new PostPutawayRequest(
                    materialId.Value,
                    warehouseId.Value,
                    destination.Id,
                    qty,
                    lotId));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Colocación en hueco {destination.CodeLabel} registrada.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var warehouseId = PickerHelpers.SelectedId(WarehouseBox);
        if (warehouseId is null)
        {
            StatusText.Text = "Seleccione almacén.";
            return;
        }

        var code = CodeBox.Text?.Trim() ?? string.Empty;
        var name = NameBox.Text?.Trim() ?? string.Empty;
        var kind = (KindBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Zone";
        if (code.Length < 1 || name.Length < 1)
        {
            StatusText.Text = "Código y nombre obligatorios.";
            return;
        }

        try
        {
            await _client.CreateLocationAsync(
                warehouseId.Value,
                new CreateLocationNodeRequest(code, name, kind, SelectedParentId()));
            CodeBox.Text = string.Empty;
            NameBox.Text = string.Empty;
            await LoadLocationsAsync(warehouseId.Value);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        var warehouseId = PickerHelpers.SelectedId(WarehouseBox);
        if (warehouseId is null)
        {
            StatusText.Text = "Seleccione almacén.";
            return;
        }

        if (LocationsGrid.SelectedItem is not LocationGridRow selected)
        {
            StatusText.Text = "Seleccione una ubicación para editar.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var nameBox = OfficeUi.Box("Nombre", 260);
        nameBox.Text = selected.Name;
        var active = new CheckBox
        {
            Content = "Activo",
            IsChecked = selected.IsActive,
        };
        var accepted = await PromptSimpleAsync(
            owner,
            $"Editar ubicación {selected.CodeLabel}",
            OfficeUi.Muted($"Código: {selected.RawCode} · Tipo: {selected.KindEs}"),
            OfficeUi.LabeledField("Nombre", nameBox),
            active);
        if (!accepted || string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return;
        }

        try
        {
            await _client.UpdateLocationAsync(
                warehouseId.Value,
                selected.Id,
                new UpdateLocationNodeRequest(
                    nameBox.Text.Trim(),
                    active.IsChecked == true));
            await LoadLocationsAsync(warehouseId.Value);
            await UiHelpers.ShowInfoAsync(this, "Ubicación actualizada.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnToggleActiveClick(object? sender, RoutedEventArgs e)
    {
        var warehouseId = PickerHelpers.SelectedId(WarehouseBox);
        if (warehouseId is null)
        {
            StatusText.Text = "Seleccione almacén.";
            return;
        }

        if (LocationsGrid.SelectedItem is not LocationGridRow selected)
        {
            StatusText.Text = "Seleccione una ubicación.";
            return;
        }

        if (string.Equals(selected.RawCode, "DEFAULT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(selected.Kind, "Root", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "La raíz del almacén no se desactiva.";
            return;
        }

        var makeActive = !selected.IsActive;
        try
        {
            await _client.UpdateLocationAsync(
                warehouseId.Value,
                selected.Id,
                new UpdateLocationNodeRequest(selected.Name, makeActive));
            await LoadLocationsAsync(warehouseId.Value);
            StatusText.Text = string.Empty;
            await UiHelpers.ShowInfoAsync(
                this,
                makeActive ? "Ubicación activada." : "Ubicación desactivada.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        var warehouseId = PickerHelpers.SelectedId(WarehouseBox);
        if (warehouseId is null)
        {
            StatusText.Text = "Seleccione almacén.";
            return;
        }

        if (LocationsGrid.SelectedItem is not LocationGridRow selected)
        {
            StatusText.Text = "Seleccione una ubicación.";
            return;
        }

        var isRoot = string.Equals(selected.RawCode, "DEFAULT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(selected.Kind, "Root", StringComparison.OrdinalIgnoreCase);

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var confirm = await AppDialog.PromptAsync(
            owner,
            isRoot ? "Eliminar almacén (raíz)" : "Eliminar ubicación",
            isRoot
                ? $"La raíz es el almacén. Escriba ELIMINAR para borrar el almacén completo «{_warehouses.FirstOrDefault(w => w.Id == warehouseId)?.Code ?? "…"}» (solo si no hay stock ni documentos)."
                : $"Escriba ELIMINAR para borrar «{selected.CodeLabel}». Si tiene stock o historial, use Desactivar.");
        if (!string.Equals(confirm?.Trim(), "ELIMINAR", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "Eliminación cancelada.";
            return;
        }

        try
        {
            await _client.DeleteLocationAsync(owner, warehouseId.Value, selected.Id);
            if (isRoot)
            {
                await RefreshAsync();
                await UiHelpers.ShowInfoAsync(this, "Almacén eliminado.");
            }
            else
            {
                await LoadLocationsAsync(warehouseId.Value);
                await UiHelpers.ShowInfoAsync(this, "Ubicación eliminada.");
            }

            StatusText.Text = string.Empty;
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnNewWarehouseClick(object? sender, RoutedEventArgs e)
    {
        if (!_client.Permissions.Has(AppPermissionNames.WarehouseManage))
        {
            StatusText.Text =
                "Falta el permiso Warehouse.Manage para crear almacenes.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            StatusText.Text = "Ventana no disponible.";
            return;
        }

        try
        {
            var request = await MasterCreateDialogs.PromptWarehouseAsync(owner);
            if (request is null)
            {
                return;
            }

            var created = await _client.CreateWarehouseAsync(request);
            await RefreshAsync();
            var index = _warehouses.ToList().FindIndex(row => row.Id == created.Id);
            if (index >= 0)
            {
                WarehousesGrid.SelectedIndex = index;
            }

            StatusText.Text = string.Empty;
            await UiHelpers.ShowInfoAsync(
                this,
                $"Almacén {created.Code} creado. La raíz usa el mismo código.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnDeleteWarehouseClick(object? sender, RoutedEventArgs e)
    {
        if (!_client.Permissions.Has(AppPermissionNames.WarehouseManage))
        {
            StatusText.Text =
                "Falta el permiso Warehouse.Manage para eliminar almacenes.";
            return;
        }

        if (WarehousesGrid.SelectedIndex < 0
            || WarehousesGrid.SelectedIndex >= _warehouses.Count)
        {
            StatusText.Text = "Seleccione un almacén en la rejilla izquierda.";
            return;
        }

        var warehouse = _warehouses[WarehousesGrid.SelectedIndex];
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            StatusText.Text = "Ventana no disponible.";
            return;
        }

        var confirm = await AppDialog.PromptAsync(
            owner,
            "Eliminar almacén",
            $"Escriba ELIMINAR para borrar el almacén «{warehouse.Code}» con su raíz/zonas vacías. Bloqueado si hay stock, movimientos o documentos.");
        if (!string.Equals(confirm?.Trim(), "ELIMINAR", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "Eliminación de almacén cancelada.";
            return;
        }

        try
        {
            await _client.DeleteWarehouseAsync(owner, warehouse.Id);
            await RefreshAsync();
            StatusText.Text = string.Empty;
            await UiHelpers.ShowInfoAsync(this, $"Almacén {warehouse.Code} eliminado.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private static async Task<bool> PromptSimpleAsync(
        Window owner,
        string title,
        params Control[] fields)
    {
        var accepted = false;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        foreach (var field in fields)
        {
            panel.Children.Add(field);
        }

        var ok = new Button { Content = "Guardar", Classes = { "primary" }, MinWidth = 96 };
        var cancel = new Button { Content = "Cancelar", MinWidth = 96 };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = panel,
        };
        ok.Click += (_, _) =>
        {
            accepted = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(owner);
        return accepted;
    }

    private async void OnPutawayMaterialChanged(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            await ReloadPutawayLotsAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
        }
    }

    private async Task ReloadPutawayLotsAsync()
    {
        if (!PickerHelpers.TryGetId(MaterialBox, out var materialId))
        {
            PickerHelpers.BindLots(PutawayLotBox, Array.Empty<LotResponse>(), includeEmpty: true);
            return;
        }

        var lots = await _client.GetLotsAsync(materialId);
        PickerHelpers.BindLots(PutawayLotBox, lots, includeEmpty: true);
    }

    private static string FormatLocationCode(LocationNodeResponse row) =>
        string.Equals(row.Code, "DEFAULT", StringComparison.OrdinalIgnoreCase)
            ? "raíz"
            : row.Code;

    private static string KindToEs(string kind) =>
        kind switch
        {
            "Root" => "Raíz",
            "Zone" => "Zona",
            "Aisle" => "Pasillo",
            "Column" => "Columna",
            "Row" => "Fila",
            "Position" => "Posición",
            "Height" => "Altura",
            "Bin" => "Hueco",
            _ => kind,
        };

    private sealed record LocationGridRow(
        Guid Id,
        Guid? ParentId,
        string CodeLabel,
        string Name,
        string KindEs,
        string ParentLabel,
        bool IsActive,
        string RawCode,
        string Kind);
}
