using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class AccountsView : UserControl
{
    private readonly LogisticsApiClient _client;
    private IReadOnlyList<InternalUserResponse> _users = [];
    private IReadOnlyList<DocumentNumberRangeResponse> _docRanges = [];
    private readonly Dictionary<string, CheckBox> _permissionBoxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _rangeBoxes = new(StringComparer.Ordinal);

    public AccountsView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Accounts);
        OfficeUi.BindHelp(SuspendHelpHost, OfficeHelpTexts.Keys.FieldSuspendUser);
        OfficeUi.BindHelp(RangesHelpHost, OfficeHelpTexts.Keys.FieldAccountRanges);
        OfficeUi.SetTextColumns(
            UsersGrid,
            ("Usuario", "UserName"),
            ("Rangos", "Roles"),
            ("Estado", "StateEs"),
            ("Permisos manuales", "CustomEs"),
            ("Motivo", "SuspensionReason"));
        OfficeUi.SetTextColumns(
            DocRangesGrid,
            ("Tipo", nameof(DocumentNumberRangeResponse.DocumentType)),
            ("Prefijo", nameof(DocumentNumberRangeResponse.Prefix)),
            ("Próximo", nameof(DocumentNumberRangeResponse.NextValue)));
        BuildRangeChecks();
        BuildPermissionChecks();
        SeedDocTypeBox();
        ApplyPermissions();
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        CreateUserButton.IsVisible = gate.Has(AppPermissionNames.UserCreate);
        SuspendButton.IsVisible = gate.Has(AppPermissionNames.UserSuspend);
        ReactivateButton.IsVisible = gate.Has(AppPermissionNames.UserReactivate);
        TerminateButton.IsVisible = gate.Has(AppPermissionNames.UserTerminate);
        var adminOnlyOverrides = gate.IsAdmin;
        ManualPermissionsPanel.IsVisible = adminOnlyOverrides;
        SavePermissionsButton.IsVisible = adminOnlyOverrides;
        var canDocRanges = gate.Has(AppPermissionNames.InvoiceConfig) || gate.IsAdmin;
        DocRangesPanel.IsVisible = canDocRanges;
        SaveDocRangeButton.IsVisible = canDocRanges;
        if (!adminOnlyOverrides)
        {
            PermHint.Text =
                "Los permisos manuales solo los puede cambiar un Administrador. Use rangos.";
        }
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyPermissions();
        try
        {
            _users = await _client.GetUsersAsync();
            var rows = _users.Select(user => new
            {
                user.UserName,
                Roles = OfficeUi.RangesToEs(user.Roles),
                StateEs = user.IsSuspended ? "Suspendido" : "Activo",
                CustomEs = user.HasCustomPermissions ? "Sí" : "No",
                SuspensionReason = user.SuspensionReason ?? "—",
            }).ToList();
            UsersGrid.ItemsSource = rows;
            EmptyHint.IsVisible = rows.Count == 0;
            UsersGrid.IsVisible = rows.Count > 0;
            if (DocRangesPanel.IsVisible)
            {
                await RefreshDocRangesAsync();
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

    private void SeedDocTypeBox()
    {
        DocTypeBox.Items.Clear();
        foreach (var type in new[]
                 {
                     "PurchaseOrder", "SalesOrder", "GoodsReceipt", "Delivery", "Cmr", "Quote", "Invoice",
                 })
        {
            DocTypeBox.Items.Add(type);
        }

        if (DocTypeBox.ItemCount > 0)
        {
            DocTypeBox.SelectedIndex = 0;
        }
    }

    private async Task RefreshDocRangesAsync()
    {
        _docRanges = await _client.GetDocumentNumberRangesAsync();
        DocRangesGrid.ItemsSource = _docRanges.ToList();
    }

    private async void OnRefreshDocRangesClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await RefreshDocRangesAsync();
            StatusText.Text = $"{_docRanges.Count} rango(s) de numeración.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private void OnDocRangeSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (DocRangesGrid.SelectedIndex < 0
            || DocRangesGrid.SelectedIndex >= _docRanges.Count)
        {
            return;
        }

        var row = _docRanges[DocRangesGrid.SelectedIndex];
        DocTypeBox.SelectedItem = row.DocumentType;
        DocPrefixBox.Text = row.Prefix;
        DocNextBox.Text = row.NextValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private async void OnSaveDocRangeClick(object? sender, RoutedEventArgs e)
    {
        if (!SaveDocRangeButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para numeración documental.";
            return;
        }

        var type = DocTypeBox.SelectedItem?.ToString()?.Trim()
            ?? DocTypeBox.Text?.Trim()
            ?? string.Empty;
        var prefix = DocPrefixBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(prefix))
        {
            StatusText.Text = "Indique tipo y prefijo.";
            return;
        }

        if (!long.TryParse(
                DocNextBox.Text?.Trim(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var next)
            || next < 1)
        {
            StatusText.Text = "Próximo número debe ser entero ≥ 1.";
            return;
        }

        try
        {
            await _client.UpsertDocumentNumberRangeAsync(
                new UpsertDocumentNumberRangeRequest(type, prefix, next));
            await RefreshDocRangesAsync();
            StatusText.Text = $"Numeración {type} → {prefix}-{next:D8}.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private void BuildRangeChecks()
    {
        RangePanel.Children.Clear();
        _rangeBoxes.Clear();
        foreach (var range in AppPermissionNames.InternalRanges.Append("Cliente"))
        {
            var box = new CheckBox
            {
                Content = OfficeUi.RangeToEs(range),
                Margin = new Avalonia.Thickness(0, 0, 12, 6),
                MinWidth = 160,
            };
            _rangeBoxes[range] = box;
            RangePanel.Children.Add(box);
        }
    }

    private void BuildPermissionChecks()
    {
        PermissionPanel.Children.Clear();
        _permissionBoxes.Clear();
        foreach (var permission in AppPermissionNames.All)
        {
            var box = new CheckBox
            {
                Content = OfficeUi.PermissionToEs(permission),
                Margin = new Avalonia.Thickness(0, 0, 12, 6),
                MinWidth = 220,
            };
            _permissionBoxes[permission] = box;
            PermissionPanel.Children.Add(box);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnUserSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!TrySelected(out var user))
        {
            return;
        }

        var canEditRanges = _client.Permissions.Has(AppPermissionNames.UserManageRanges)
            || _client.Permissions.Has(AppPermissionNames.UserManagePermissions);
        foreach (var pair in _rangeBoxes)
        {
            pair.Value.IsChecked = user.Roles.Contains(pair.Key, StringComparer.Ordinal);
            pair.Value.IsEnabled = canEditRanges;
        }

        try
        {
            var payload = await _client.GetUserPermissionsAsync(user.Id);
            var canEdit = !payload.IsAdmin
                && _client.Permissions.IsAdmin;
            foreach (var pair in _permissionBoxes)
            {
                pair.Value.IsChecked = payload.Permissions.Contains(pair.Key);
                pair.Value.IsEnabled = canEdit;
            }

            PermHint.Text = !_client.Permissions.IsAdmin
                ? "Los permisos manuales solo los puede cambiar un Administrador. Use rangos."
                : payload.IsAdmin
                    ? "Administrador: permisos completos (no editables)."
                    : payload.IsCustom
                        ? "Permisos manuales activos. Preferible asignar rangos."
                        : "Permisos efectivos = unión de rangos.";
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
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var name = NewUserBox.Text?.Trim() ?? string.Empty;
        if (name.Length < 3)
        {
            StatusText.Text = "Indique un nombre de usuario (mín. 3).";
            return;
        }

        var initialRanges = _rangeBoxes
            .Where(pair => pair.Value.IsChecked == true)
            .Select(pair => pair.Key)
            .ToArray();
        if (initialRanges.Length == 0)
        {
            StatusText.Text = "Seleccione al menos un rango antes de crear la cuenta.";
            return;
        }

        if (initialRanges.Contains("Cliente", StringComparer.Ordinal)
            && initialRanges.Any(r => !string.Equals(r, "Cliente", StringComparison.Ordinal)))
        {
            StatusText.Text = "Cliente es exclusivo: no combinar con rangos internos.";
            return;
        }

        try
        {
            var created = await _client.CreateWorkerAsync(
                owner,
                new CreateInternalUserRequest(
                    name,
                    null,
                    initialRanges));
            await UiHelpers.ShowInfoAsync(
                this,
                $"Creado {created.UserName}. Contraseña temporal: {created.TemporaryPassword}");
            NewUserBox.Text = string.Empty;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnSaveRangesClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var user))
        {
            return;
        }

        var selected = _rangeBoxes
            .Where(pair => pair.Value.IsChecked == true)
            .Select(pair => pair.Key)
            .ToArray();
        if (selected.Length == 0)
        {
            StatusText.Text = "Seleccione al menos un rango.";
            return;
        }

        try
        {
            await _client.SetUserRangesAsync(owner, user.Id, selected);
            await UiHelpers.ShowInfoAsync(
                this,
                "Rangos guardados. El usuario debe volver a entrar.");
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnSavePermissionsClick(object? sender, RoutedEventArgs e)
    {
        if (!_client.Permissions.IsAdmin)
        {
            StatusText.Text = "Solo un administrador puede guardar permisos manuales.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var user))
        {
            return;
        }

        var selected = _permissionBoxes
            .Where(pair => pair.Value.IsChecked == true)
            .Select(pair => pair.Key)
            .ToArray();
        try
        {
            await _client.SetUserPermissionsAsync(owner, user.Id, selected);
            await UiHelpers.ShowInfoAsync(
                this,
                "Permisos guardados. El usuario debe volver a entrar.");
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnSuspendClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var user))
        {
            return;
        }

        if (user.IsSuspended)
        {
            StatusText.Text = "El usuario ya está suspendido. Use Reactivar o Dar de baja.";
            return;
        }

        var reason = ReasonBox.Text?.Trim() ?? string.Empty;
        if (reason.Length < 3)
        {
            StatusText.Text = "Indique el motivo de suspensión (mín. 3 caracteres).";
            return;
        }

        try
        {
            await _client.SuspendUserAsync(owner, user.Id, reason);
            await UiHelpers.ShowInfoAsync(this, $"Usuario {user.UserName} suspendido.");
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

    private async void OnReactivateClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var user))
        {
            return;
        }

        if (!user.IsSuspended)
        {
            StatusText.Text = "El usuario no está suspendido.";
            return;
        }

        try
        {
            await _client.ReactivateUserAsync(owner, user.Id);
            await UiHelpers.ShowInfoAsync(this, $"Usuario {user.UserName} reactivado.");
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

    private async void OnTerminateClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var user))
        {
            return;
        }

        var reason = ReasonBox.Text?.Trim() ?? string.Empty;
        if (reason.Length < 3)
        {
            StatusText.Text = "Indique el motivo de baja (mín. 3 caracteres).";
            return;
        }

        if (!await OfficeUi.ConfirmAsync(
                owner,
                $"¿Dar de baja definitiva a «{user.UserName}»? Esta acción no se puede deshacer desde la UI."))
        {
            return;
        }

        try
        {
            await _client.TerminateUserAsync(owner, user.Id, reason);
            await UiHelpers.ShowInfoAsync(this, $"Baja registrada para {user.UserName}.");
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

    private bool TrySelected(out InternalUserResponse user)
    {
        user = null!;
        if (UsersGrid.SelectedIndex < 0 || UsersGrid.SelectedIndex >= _users.Count)
        {
            StatusText.Text = "Seleccione un usuario.";
            return false;
        }

        user = _users[UsersGrid.SelectedIndex];
        return true;
    }
}
