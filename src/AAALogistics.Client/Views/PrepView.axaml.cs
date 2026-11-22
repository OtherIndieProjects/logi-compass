using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class PrepView : UserControl
{
    private readonly LogisticsApiClient _client;
    private readonly Avalonia.Threading.DispatcherTimer _presenceTimer;
    private IReadOnlyList<SalesOrderRow> _orders = [];
    private IReadOnlyList<PrepPickLineResponse> _picks = [];
    private Dictionary<Guid, string> _materialLabels = [];
    private Dictionary<Guid, string> _locationLabels = [];
    private Dictionary<Guid, string> _lotLabels = [];
    private Guid? _selectedOrderId;

    public PrepView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Prep);
        OfficeUi.BindHelp(ModeHelpHost, OfficeHelpTexts.Keys.FieldPrepMode);
        OfficeUi.BindHelp(JoinHelpHost, OfficeHelpTexts.Keys.FieldJoinPrep);
        OfficeUi.BindHelp(LeaveHelpHost, OfficeHelpTexts.Keys.FieldLeavePrep);
        OfficeUi.BindHelp(MarkPreparedHelpHost, OfficeHelpTexts.Keys.FieldMarkPrepared);
        OfficeUi.BindHelp(ConfirmPickHelpHost, OfficeHelpTexts.Keys.FieldConfirmPick);
        OfficeUi.BindHelp(ClosePrepHelpHost, OfficeHelpTexts.Keys.FieldClosePrepOrder);
        OfficeUi.SetTextColumns(
            OrdersGrid,
            ("Número", "Number"),
            ("Estado", "StatusEs"),
            ("Líneas", "Lines"));
        OfficeUi.SetTextColumns(
            PresenceGrid,
            ("Usuario", "User"),
            ("Modo", "ModeEs"));
        OfficeUi.SetTextColumns(
            LinesGrid,
            ("Material", "Material"),
            ("Pedida", "Requested"),
            ("Abierta", "Open"));
        OfficeUi.SetTextColumns(
            PicksGrid,
            ("Material", "Material"),
            ("Ubicación", "Location"),
            ("Lote", "Lot"),
            ("Cantidad", "Quantity"),
            ("Confirmada", "Confirmed"));
        ApplyPermissions();
        _presenceTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15),
        };
        _presenceTimer.Tick += async (_, _) =>
        {
            if (_selectedOrderId is { } id)
            {
                await LoadPresenceAsync(id);
            }
        };
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        JoinButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
        LeaveButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
        MarkPreparedButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
        ConfirmPickButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
        CloseButton.IsVisible = gate.Has(AppPermissionNames.SalesOrderClose);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _presenceTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _presenceTimer.Stop();
        base.OnDetachedFromVisualTree(e);
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
            _orders = (await _client.GetSalesOrdersAsync())
                .Where(order => order.Status is "Confirmed" or "InPreparation" or "Prepared")
                .ToList();
            var rows = _orders.Select(order => new
            {
                order.Number,
                StatusEs = OfficeUi.StatusToEs(order.Status),
                Lines = order.Lines.Count,
            }).ToList();
            OrdersGrid.ItemsSource = rows;
            EmptyHint.IsVisible = rows.Count == 0;
            OrdersGrid.IsVisible = rows.Count > 0;
            if (_selectedOrderId is { } selected
                && _orders.All(order => order.Id != selected))
            {
                _selectedOrderId = null;
                LinesGrid.ItemsSource = null;
                PresenceGrid.ItemsSource = null;
                PicksGrid.ItemsSource = null;
                PresenceText.Text = string.Empty;
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

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnOrderSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        LinesGrid.ItemsSource = order.Lines.Select(line => new
        {
            Material = MaterialLabel(line.MaterialId),
            Requested = line.RequestedQuantity,
            Open = line.OpenQuantity,
        }).ToList();
        _selectedOrderId = order.Id;
        StatusText.Text = string.Empty;
        await LoadPresenceAsync(order.Id);
        await LoadPicksAsync(order.Id);
    }

    private async Task LoadPresenceAsync(Guid salesOrderId)
    {
        try
        {
            var sessions = await _client.GetPrepAsync(salesOrderId);
            PresenceGrid.ItemsSource = sessions.Select(session => new
            {
                User = session.UserName,
                ModeEs = OfficeUi.StatusToEs(session.Mode),
            }).ToList();
            PresenceText.Text = sessions.Count == 0
                ? "Nadie en preparación. Pulse Entrar a preparación."
                : string.Join(
                    " · ",
                    sessions.Select(
                        session => $"{session.UserName} ({OfficeUi.StatusToEs(session.Mode)})"));
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            PresenceText.Text = exception.Message;
        }
    }

    private async Task LoadPicksAsync(Guid salesOrderId)
    {
        try
        {
            _picks = await _client.GetPrepPicksAsync(salesOrderId);
            await EnsureLocationAndLotLabelsAsync(_picks);
            PicksGrid.ItemsSource = _picks.Select(pick => new
            {
                Material = MaterialLabel(pick.MaterialId),
                Location = pick.SuggestedLocationId == Guid.Empty
                    ? "—"
                    : (_locationLabels.TryGetValue(pick.SuggestedLocationId, out var loc)
                        ? loc
                        : "—"),
                Lot = pick.SuggestedLotId is { } lotId
                    && _lotLabels.TryGetValue(lotId, out var lot)
                    ? lot
                    : "—",
                pick.Quantity,
                Confirmed = pick.ConfirmedQuantity,
            }).ToList();
            if (_picks.Count == 0)
            {
                StatusText.Text =
                    "Sin líneas de recogida sugeridas. Entre a preparación o verifique stock/ubicaciones.";
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            _picks = [];
            PicksGrid.ItemsSource = null;
            StatusText.Text = exception.Message;
        }
    }

    private async Task EnsureLocationAndLotLabelsAsync(IReadOnlyList<PrepPickLineResponse> picks)
    {
        foreach (var warehouse in _client.Masters.Warehouses)
        {
            try
            {
                var nodes = await _client.GetLocationsAsync(warehouse.Id);
                foreach (var node in nodes)
                {
                    _locationLabels[node.Id] = string.IsNullOrWhiteSpace(node.Name)
                        ? node.Code
                        : $"{node.Code} — {node.Name}";
                }
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
                // Sigue con el resto de almacenes.
            }
        }

        foreach (var materialId in picks.Select(pick => pick.MaterialId).Distinct())
        {
            try
            {
                var lots = await _client.GetLotsAsync(materialId);
                foreach (var lot in lots)
                {
                    _lotLabels[lot.Id] = lot.Code;
                }
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
                // Sigue con el resto de materiales.
            }
        }
    }

    private string MaterialLabel(Guid materialId) =>
        _materialLabels.TryGetValue(materialId, out var label)
            ? label
            : "—";

    private async void OnJoinClick(object? sender, RoutedEventArgs e)
    {
        if (!JoinButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para preparación.";
            return;
        }

        if (!TrySelected(out var order))
        {
            return;
        }

        var mode = (ModeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Open";
        try
        {
            var sessions = await _client.JoinPrepAsync(order.Id, mode);
            PresenceGrid.ItemsSource = sessions.Select(session => new
            {
                User = session.UserName,
                ModeEs = OfficeUi.StatusToEs(session.Mode),
            }).ToList();
            await RefreshAsync();
            await LoadPresenceAsync(order.Id);
            await LoadPicksAsync(order.Id);
            StatusText.Text = "Sesión de preparación activa.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnLeaveClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        try
        {
            await _client.LeavePrepAsync(order.Id);
            await LoadPresenceAsync(order.Id);
            StatusText.Text = "Ha salido de la preparación.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnMarkPreparedClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        if (order.Status is "Prepared")
        {
            StatusText.Text = "El pedido ya está marcado como Preparado. Continúe en Ventas → Contabilizar entrega.";
            return;
        }

        try
        {
            await _client.MarkPreparedAsync(order.Id);
            await RefreshAsync();
            StatusText.Text = "Pedido marcado Preparado. Puede contabilizar la entrega en Ventas.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnConfirmPickClick(object? sender, RoutedEventArgs e)
    {
        if (_selectedOrderId is not { } orderId)
        {
            StatusText.Text = "Seleccione un pedido.";
            return;
        }

        if (PicksGrid.SelectedIndex < 0 || PicksGrid.SelectedIndex >= _picks.Count)
        {
            StatusText.Text = "Seleccione una línea de recogida sugerida (FEFO).";
            return;
        }

        var pick = _picks[PicksGrid.SelectedIndex];
        try
        {
            await _client.ConfirmPrepPickAsync(orderId, pick.Id, pick.Quantity);
            await LoadPicksAsync(orderId);
            StatusText.Text = "Recogida confirmada.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = exception.Message;
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out var order))
        {
            return;
        }

        try
        {
            var owner = UiHelpers.FindWindow(this);
            if (owner is null)
            {
                return;
            }

            await _client.CloseSalesOrderAsync(owner, order.Id);
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
}
