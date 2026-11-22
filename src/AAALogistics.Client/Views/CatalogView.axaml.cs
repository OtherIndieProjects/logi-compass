using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class CatalogView : UserControl
{
    private readonly LogisticsApiClient _client;
    private string _tab = "materials";
    private object? _rawRows;

    public CatalogView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Maestros);
        WireTabHelpHeaders();
        UomHelpHost.Content = OfficeUi.InfoButton(OfficeHelpTexts.Keys.FieldUomConversion);
        ApplyPermissions();
        ConfigureColumns();
        ConfigureConversionsGrid();
    }

    private void WireTabHelpHeaders()
    {
        foreach (var item in EntityTabs.Items.OfType<TabItem>())
        {
            var tag = item.Tag as string ?? string.Empty;
            var (label, helpKey) = tag switch
            {
                "units" => ("Unidades", OfficeHelpTexts.Keys.MaestrosUnits),
                "warehouses" => ("Almacenes", OfficeHelpTexts.Keys.MaestrosWarehouses),
                "suppliers" => ("Proveedores", OfficeHelpTexts.Keys.MaestrosSuppliers),
                "customers" => ("Clientes", OfficeHelpTexts.Keys.MaestrosCustomers),
                _ => ("Materiales", OfficeHelpTexts.Keys.MaestrosMaterials),
            };
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            };
            header.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
            });
            header.Children.Add(OfficeUi.InfoButton(helpKey));
            item.Header = header;
        }
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyPermissions();
        try
        {
            // Sin auto-seed: maestros vacíos hasta que el usuario cree o pulse Seed UN/KG/L/PCS.
            await _client.Masters.RefreshAsync();
            BindCurrentTab();
            if (_tab == "units")
            {
                await RefreshConversionsAsync();
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

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        NewButton.IsVisible = CanCreateCurrent();
        EditButton.IsVisible = CanCreateCurrent();
        MoreButton.IsVisible = _tab == "materials"
            && (gate.Has(AppPermissionNames.MaterialManage)
                || gate.Has(AppPermissionNames.MaterialDelete));
        // Visible con Material.Delete; si solo hay MaterialManage, Desactivar cubre la baja suave.
        DeleteMaterialButton.IsVisible = _tab == "materials"
            && (gate.Has(AppPermissionNames.MaterialDelete)
                || gate.Has(AppPermissionNames.MaterialManage));
        DeactivateButton.IsVisible = CanToggleActiveCurrent();
        UnitsToolsPanel.IsVisible = _tab == "units";
        UpdateDeactivateButtonLabel();
    }

    private bool CanToggleActiveCurrent() =>
        _tab switch
        {
            "materials" => _client.Permissions.Has(AppPermissionNames.MaterialManage),
            "suppliers" => _client.Permissions.Has(AppPermissionNames.SupplierManage),
            "customers" => _client.Permissions.Has(AppPermissionNames.CustomerManage),
            // Unidades y almacenes no exponen IsActive en el PUT actual.
            _ => false,
        };

    private bool CanManageMaterials() =>
        _client.Permissions.Has(AppPermissionNames.MaterialManage);

    private bool CanCreateCurrent()
    {
        var gate = _client.Permissions;
        return _tab switch
        {
            "units" => gate.Has(AppPermissionNames.MaterialManage),
            "warehouses" => gate.Has(AppPermissionNames.WarehouseManage),
            "suppliers" => gate.Has(AppPermissionNames.SupplierManage),
            "customers" => gate.Has(AppPermissionNames.CustomerManage),
            _ => gate.Has(AppPermissionNames.MaterialManage),
        };
    }

    private void ConfigureColumns()
    {
        Grid.Columns.Clear();
        Grid.AutoGenerateColumns = false;
        switch (_tab)
        {
            case "units":
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Código", nameof(UnitRow.Code)),
                    ("Nombre", nameof(UnitRow.Name)),
                    ("Decimales", nameof(UnitRow.DecimalScale)));
                break;
            case "warehouses":
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Código", nameof(WarehouseRow.Code)),
                    ("Nombre", nameof(WarehouseRow.Name)),
                    ("Tipo", nameof(WarehouseRow.SiteKindLabel)));
                break;
            case "suppliers":
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Nombre", nameof(PartyRow.Name)),
                    ("NIF/CIF", nameof(PartyRow.TaxId)),
                    ("Ciudad", nameof(PartyRow.City)),
                    ("País", nameof(PartyRow.CountryCode)),
                    ("Plazo", nameof(PartyRow.LeadTimeDays)));
                OfficeUi.SetCheckColumn(Grid, "Activo", nameof(PartyRow.IsActive));
                break;
            case "customers":
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Nombre", nameof(PartyRow.Name)),
                    ("NIF/CIF", nameof(PartyRow.TaxId)),
                    ("Ciudad", nameof(PartyRow.City)),
                    ("País", nameof(PartyRow.CountryCode)));
                OfficeUi.SetCheckColumn(Grid, "Activo", nameof(PartyRow.IsActive));
                break;
            default:
                OfficeUi.SetTextColumns(
                    Grid,
                    ("Código", nameof(MaterialRow.Code)),
                    ("Nombre", nameof(MaterialRow.DisplayName)),
                    ("Unidad", nameof(MaterialRow.UnitCode)),
                    ("Precio", nameof(MaterialRow.CatalogPrice)),
                    ("Moneda", nameof(MaterialRow.Currency)),
                    ("Id externo", nameof(MaterialRow.ExternalId)));
                OfficeUi.SetCheckColumn(Grid, "Activo", nameof(MaterialRow.IsActive));
                OfficeUi.SetCheckColumn(Grid, "Lote", nameof(MaterialRow.RequiresLot));
                break;
        }
    }

    private void ConfigureConversionsGrid()
    {
        ConversionsGrid.Columns.Clear();
        OfficeUi.SetTextColumns(
            ConversionsGrid,
            ("Desde", "Desde"),
            ("Hacia", "Hacia"),
            ("Factor", "Factor"));
    }

    private void BindCurrentTab()
    {
        ConfigureColumns();
        HintText.Text = _tab switch
        {
            "units" => "Pulse Nuevo… para crear una unidad, o Seed UN/KG/L/PCS. Defina conversiones abajo.",
            "warehouses" => "Pulse Nuevo… o Editar… para gestionar almacenes (incluye Tipo). Baja de nodos físicos: menú Ubicaciones.",
            "suppliers" => "Pulse Nuevo…, Editar… o Desactivar/Activar (dirección completa y plazo).",
            "customers" => "Pulse Nuevo…, Editar… o Desactivar/Activar (dirección, id externo).",
            _ => _client.Masters.Units.Count == 0
                ? "No hay unidades. Vaya a Unidades o pulse Actualizar para seed UN/KG/L/PCS antes de crear material."
                : "Pulse Nuevo… / Editar… / Desactivar o Eliminar / Dar de baja… (Excel en Más acciones).",
        };

        _rawRows = _tab switch
        {
            "units" => _client.Masters.Units,
            "warehouses" => _client.Masters.Warehouses,
            "suppliers" => _client.Masters.Suppliers,
            "customers" => _client.Masters.Customers,
            _ => _client.Masters.Materials,
        };
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<object> filtered = _tab switch
        {
            "units" => FilterUnits(query),
            "warehouses" => FilterWarehouses(query),
            "suppliers" or "customers" => FilterParties(query),
            _ => FilterMaterials(query),
        };
        var list = filtered.ToList();
        Grid.ItemsSource = list;
        EmptyHint.IsVisible = list.Count == 0;
        Grid.IsVisible = list.Count > 0;
        UpdateDeactivateButtonLabel();
    }

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        UpdateDeactivateButtonLabel();

    private void UpdateDeactivateButtonLabel()
    {
        var active = Grid.SelectedItem switch
        {
            MaterialRow material => material.IsActive,
            PartyRow party => party.IsActive,
            _ => true,
        };
        DeactivateButton.Content = active ? "Desactivar" : "Activar";
        DeactivateButton.IsEnabled = Grid.SelectedItem is MaterialRow or PartyRow
            && CanToggleActiveCurrent();
    }

    private async void OnDeleteMaterialClick(object? sender, RoutedEventArgs e) =>
        await RequestDeletionAsync();

    private async void OnDeactivateClick(object? sender, RoutedEventArgs e)
    {
        if (!CanToggleActiveCurrent())
        {
            StatusText.Text = "No tiene permiso para desactivar en esta pestaña.";
            return;
        }

        try
        {
            bool? becameActive = null;
            switch (_tab)
            {
                case "materials" when Grid.SelectedItem is MaterialRow material:
                {
                    becameActive = !material.IsActive;
                    var request = new UpdateMaterialRequest(
                        MaterialDisplayName(material),
                        material.CatalogPrice,
                        material.Currency,
                        becameActive.Value,
                        material.ExternalId,
                        material.RequiresLot,
                        material.Description);
                    await _client.UpdateMaterialAsync(material.Id, request);
                    break;
                }
                case "suppliers" when Grid.SelectedItem is PartyRow supplier:
                {
                    becameActive = !supplier.IsActive;
                    await _client.UpdateSupplierAsync(
                        supplier.Id,
                        PartyWithActive(supplier, becameActive.Value));
                    break;
                }
                case "customers" when Grid.SelectedItem is PartyRow customer:
                {
                    becameActive = !customer.IsActive;
                    await _client.UpdateCustomerAsync(
                        customer.Id,
                        PartyWithActive(customer, becameActive.Value));
                    break;
                }
                default:
                    StatusText.Text = "Seleccione una fila con estado Activo.";
                    return;
            }

            await RefreshAsync();
            StatusText.Text = string.Empty;
            await UiHelpers.ShowInfoAsync(
                this,
                becameActive is false
                    ? "Registro desactivado (Activo = no)."
                    : becameActive is true
                        ? "Registro activado."
                        : "Estado actualizado.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private static UpdatePartyRequest PartyWithActive(PartyRow party, bool isActive) =>
        new(
            party.Name,
            party.AddressLine1,
            party.AddressLine2,
            party.City,
            party.Region,
            string.IsNullOrWhiteSpace(party.PostalCode) ? "00000" : party.PostalCode,
            party.CountryCode,
            isActive,
            party.ExternalId,
            party.LeadTimeDays);

    private IEnumerable<object> FilterMaterials(string query)
    {
        IEnumerable<MaterialRow> rows = _client.Masters.Materials;
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(row =>
                row.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
                || MaterialDisplayName(row).Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.UnitCode.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return rows;
    }

    private IEnumerable<object> FilterUnits(string query)
    {
        IEnumerable<UnitRow> rows = _client.Masters.Units;
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(row =>
                row.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return rows;
    }

    private IEnumerable<object> FilterWarehouses(string query)
    {
        IEnumerable<WarehouseRow> rows = _client.Masters.Warehouses;
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(row =>
                row.Code.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.SiteKind.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return rows;
    }

    private IEnumerable<object> FilterParties(string query)
    {
        IEnumerable<PartyRow> rows = _tab == "suppliers"
            ? _client.Masters.Suppliers
            : _client.Masters.Customers;
        if (!string.IsNullOrWhiteSpace(query))
        {
            rows = rows.Where(row =>
                row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.TaxId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || row.City.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return rows;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) =>
        ApplyFilter();

    private async void OnTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }

        if (EntityTabs.SelectedItem is TabItem tab && tab.Tag is string tag)
        {
            _tab = tag;
            ApplyPermissions();
            await RefreshAsync();
        }
    }

    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        if (!CanCreateCurrent())
        {
            StatusText.Text = "No tiene permiso para crear en esta pestaña.";
            return;
        }

        try
        {
            switch (_tab)
            {
                case "units":
                {
                    var request = await MasterCreateDialogs.PromptUnitAsync(owner);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.CreateUnitAsync(request);
                    break;
                }
                case "warehouses":
                {
                    var request = await MasterCreateDialogs.PromptWarehouseAsync(owner);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.CreateWarehouseAsync(request);
                    break;
                }
                case "suppliers":
                {
                    var request = await MasterCreateDialogs.PromptPartyAsync(
                        owner,
                        "Nuevo proveedor",
                        includeLeadTime: true);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.CreateSupplierAsync(request);
                    break;
                }
                case "customers":
                {
                    var request = await MasterCreateDialogs.PromptPartyAsync(
                        owner,
                        "Nuevo cliente");
                    if (request is null)
                    {
                        return;
                    }

                    await _client.CreateCustomerAsync(request);
                    break;
                }
                default:
                {
                    await _client.Masters.RefreshAsync();
                    if (_client.Masters.Units.Count == 0)
                    {
                        StatusText.Text =
                            "No hay unidades. Cree unidades (pestaña Unidades) antes del material.";
                        await AppDialog.ShowMessageAsync(
                            owner,
                            "Sin unidades",
                            "No se puede crear material sin unidades. Abra Unidades → Nuevo… o Seed UN/KG/L/PCS (opcional).");
                        return;
                    }

                    var request = await MasterCreateDialogs.PromptMaterialAsync(
                        owner,
                        _client.Masters.Units);
                    if (request is null)
                    {
                        StatusText.Text =
                            "Alta de material cancelada o incompleta (unidad obligatoria).";
                        return;
                    }

                    await _client.CreateMaterialAsync(request);
                    break;
                }
            }

            await RefreshAsync();
            await UiHelpers.ShowInfoAsync(this, "Registro creado correctamente.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        if (!CanCreateCurrent())
        {
            StatusText.Text = "No tiene permiso para editar en esta pestaña.";
            return;
        }

        if (Grid.SelectedItem is null)
        {
            StatusText.Text = "Seleccione una fila para editar.";
            return;
        }

        try
        {
            switch (_tab)
            {
                case "units" when Grid.SelectedItem is UnitRow unit:
                {
                    var request = await MasterCreateDialogs.PromptEditUnitAsync(owner, unit);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.UpdateUnitAsync(unit.Id, request);
                    break;
                }
                case "warehouses" when Grid.SelectedItem is WarehouseRow warehouse:
                {
                    var request = await MasterCreateDialogs.PromptEditWarehouseAsync(
                        owner,
                        warehouse);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.UpdateWarehouseAsync(warehouse.Id, request);
                    break;
                }
                case "suppliers" when Grid.SelectedItem is PartyRow supplier:
                {
                    var request = await MasterCreateDialogs.PromptEditPartyAsync(
                        owner,
                        $"Editar proveedor {supplier.TaxId}",
                        supplier,
                        includeLeadTime: true);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.UpdateSupplierAsync(supplier.Id, request);
                    break;
                }
                case "customers" when Grid.SelectedItem is PartyRow customer:
                {
                    var request = await MasterCreateDialogs.PromptEditPartyAsync(
                        owner,
                        $"Editar cliente {customer.TaxId}",
                        customer);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.UpdateCustomerAsync(customer.Id, request);
                    break;
                }
                case "materials" when Grid.SelectedItem is MaterialRow material:
                {
                    var request = await MasterCreateDialogs.PromptEditMaterialAsync(
                        owner,
                        material);
                    if (request is null)
                    {
                        return;
                    }

                    await _client.UpdateMaterialAsync(material.Id, request);
                    break;
                }
                default:
                    StatusText.Text = "Seleccione una fila válida.";
                    return;
            }

            await RefreshAsync();
            await UiHelpers.ShowInfoAsync(this, "Registro actualizado.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnSeedUnitsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.EnsureDefaultUnitsAsync();
            await RefreshAsync();
            StatusText.Text = result.CreatedCount == 0
                ? "Las unidades UN/KG/L/PCS ya existían."
                : $"Creadas: {string.Join(", ", result.CreatedCodes)}";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshConversionsClick(object? sender, RoutedEventArgs e) =>
        await RefreshConversionsAsync();

    private async void OnNewConversionClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        try
        {
            await _client.Masters.RefreshAsync();
            var prompt = await MasterCreateDialogs.PromptUnitConversionAsync(
                owner,
                _client.Masters.Units);
            if (prompt is null)
            {
                return;
            }

            await _client.UpsertUnitConversionAsync(
                new UpsertUnitConversionRequestDto(prompt.Value.From, prompt.Value.To, prompt.Value.Factor));
            await RefreshConversionsAsync();
            await UiHelpers.ShowInfoAsync(this, "Conversión guardada.");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task RefreshConversionsAsync()
    {
        try
        {
            var rows = await _client.ListUnitConversionsAsync();
            var units = _client.Masters.Units.ToDictionary(unit => unit.Id, unit => unit.Code);
            ConversionsGrid.ItemsSource = rows.Select(row => new
            {
                Desde = units.TryGetValue(row.FromUnitId, out var from) ? from : "—",
                Hacia = units.TryGetValue(row.ToUnitId, out var to) ? to : "—",
                Factor = row.Factor,
            }).ToList();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
        }
    }

    private async void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        if (_tab != "materials")
        {
            StatusText.Text = "Más acciones solo aplica a Materiales.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var choice = await PromptMoreActionsAsync(owner);
        switch (choice)
        {
            case "export":
                await ExportMaterialsAsync();
                break;
            case "preview":
                await RunMaterialsImportAsync(commit: false);
                break;
            case "commit":
                await RunMaterialsImportAsync(commit: true);
                break;
            case "delete":
                await RequestDeletionAsync();
                break;
        }
    }

    private static async Task<string?> PromptMoreActionsAsync(Window owner)
    {
        string? choice = null;
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
        panel.Children.Add(OfficeUi.Muted("Acciones adicionales de materiales:"));
        var dialog = new Window
        {
            Title = "Más acciones",
            Width = 380,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = panel,
        };
        void Add(string key, string label)
        {
            var button = new Button
            {
                Content = label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            button.Click += (_, _) =>
            {
                choice = key;
                dialog.Close();
            };
            panel.Children.Add(button);
        }

        Add("export", "Exportar materiales a Excel");
        Add("preview", "Vista previa importación Excel");
        Add("commit", "Importar materiales (confirmar)");
        Add("delete", "Eliminar / Dar de baja (con aprobación si hay stock)");
        var cancel = new Button { Content = "Cerrar", Classes = { "primary" } };
        cancel.Click += (_, _) => dialog.Close();
        panel.Children.Add(cancel);
        await dialog.ShowDialog(owner);
        return choice;
    }

    private async Task ExportMaterialsAsync()
    {
        try
        {
            var bytes = await _client.ExportMaterialsAsync();
            await UiHelpers.SaveBytesAsync(this, "materials.xlsx", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task RunMaterialsImportAsync(bool commit)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null)
        {
            StatusText.Text = "No hay diálogo de apertura disponible.";
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = commit ? "Importar materiales" : "Vista previa importación",
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("Excel")
                    {
                        Patterns = ["*.xlsx"],
                    },
                ],
            });
        if (files.Count == 0)
        {
            return;
        }

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            var name = files[0].Name;
            if (commit)
            {
                var result = await _client.CommitMaterialsImportAsync(stream, name);
                await UiHelpers.ShowInfoAsync(
                    this,
                    $"Importados/actualizados: {result.CreatedOrUpdated}");
                await RefreshAsync();
            }
            else
            {
                var preview = await _client.PreviewMaterialsImportAsync(stream, name);
                StatusText.Text =
                    $"Vista previa: {preview.ValidRows}/{preview.TotalRows} filas válidas.";
                Grid.AutoGenerateColumns = true;
                Grid.Columns.Clear();
                Grid.ItemsSource = preview.Rows;
                EmptyHint.IsVisible = false;
                Grid.IsVisible = true;
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

    private async Task RequestDeletionAsync()
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || _tab != "materials")
        {
            StatusText.Text = "Seleccione la pestaña Materiales y una fila.";
            return;
        }

        if (Grid.SelectedItem is not MaterialRow material)
        {
            StatusText.Text = "Seleccione el material a eliminar / dar de baja.";
            return;
        }

        var gate = _client.Permissions;
        var canFormal = gate.Has(AppPermissionNames.MaterialDelete);
        var canSoft = gate.Has(AppPermissionNames.MaterialManage);
        if (!canFormal && !canSoft)
        {
            StatusText.Text = "Sin permiso para eliminar o desactivar materiales.";
            return;
        }

        var reason = await AppDialog.PromptAsync(
            owner,
            "Eliminar / Dar de baja",
            canFormal
                ? $"Motivo de baja para {material.Code} (con stock → aprobación 4 ojos):"
                : $"Motivo para desactivar {material.Code} (sin permiso de baja formal):");
        if (reason is null || reason.Length < 3)
        {
            StatusText.Text = "Motivo obligatorio (mín. 3 caracteres).";
            return;
        }

        if (canFormal)
        {
            try
            {
                var request = await _client.RequestMaterialDeletionAsync(
                    owner,
                    material.Id,
                    reason);
                await UiHelpers.ShowInfoAsync(
                    this,
                    request.HasStock
                        ? $"Solicitud de baja de {material.Code} pendiente de aprobación 4 ojos (Oficina / Inicio)."
                        : $"Material {material.Code} eliminado/desactivado (sin stock).");
                await RefreshAsync();
                return;
            }
            catch (Exception exception) when (exception is ApiException api
                && api.Code is "AAA-AUTH-FORBIDDEN" or "AAA-AUTH-0003")
            {
                if (!canSoft)
                {
                    StatusText.Text = exception.Message;
                    await UiHelpers.ShowErrorAsync(this, exception);
                    return;
                }

                await SoftDeactivateMaterialAsync(
                    material,
                    $"Baja formal no permitida ({exception.Message}). Material desactivado en su lugar.");
                return;
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
                StatusText.Text = exception.Message;
                await UiHelpers.ShowErrorAsync(this, exception);
                return;
            }
        }

        await SoftDeactivateMaterialAsync(
            material,
            "Material desactivado (sin permiso Material.Delete; use Desactivar/Activar para revertir).");
    }

    private async Task SoftDeactivateMaterialAsync(MaterialRow material, string info)
    {
        try
        {
            await _client.UpdateMaterialAsync(
                material.Id,
                new UpdateMaterialRequest(
                    MaterialDisplayName(material),
                    material.CatalogPrice,
                    material.Currency,
                    IsActive: false,
                    material.ExternalId,
                    material.RequiresLot,
                    material.Description));
            await RefreshAsync();
            StatusText.Text = string.Empty;
            await UiHelpers.ShowInfoAsync(this, info);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private static string MaterialDisplayName(MaterialRow material) =>
        string.IsNullOrWhiteSpace(material.DisplayName)
            ? material.Description
            : material.DisplayName;
}
