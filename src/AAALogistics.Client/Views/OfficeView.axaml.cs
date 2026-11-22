using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class OfficeView : UserControl
{
    private readonly LogisticsApiClient _client;
    private IReadOnlyList<SalesOrderRow> _orders = [];
    private IReadOnlyList<CarrierResponse> _carriers = [];
    private IReadOnlyList<MaterialDeletionResponse> _deletions = [];
    private IReadOnlyList<DeliverySummaryResponse> _deliveries = [];
    private Guid? _lastCmrId;

    public OfficeView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Office);
        OfficeUi.BindHelp(CmrHelpHost, OfficeHelpTexts.Keys.FieldCmrVsDelivery);
        OfficeUi.BindHelp(CarrierHelpHost, OfficeHelpTexts.Keys.FieldCreateCarrier);
        OfficeUi.BindHelp(CmrPdfHelpHost, OfficeHelpTexts.Keys.FieldCmrPdf);
        OfficeUi.BindHelp(DnPdfHelpHost, OfficeHelpTexts.Keys.FieldDeliveryNotePdf);
        OfficeUi.BindHelp(ApproveDeletionHelpHost, OfficeHelpTexts.Keys.FieldApproveDeletion);
        OfficeUi.BindHelp(RejectDeletionHelpHost, OfficeHelpTexts.Keys.FieldRejectDeletion);
        OfficeUi.SetTextColumns(
            OrdersGrid,
            ("Número", "Number"),
            ("Estado", "StatusEs"));
        OfficeUi.SetTextColumns(
            CarriersGrid,
            ("Código", "Code"),
            ("Nombre", "Name"),
            ("Activo", "ActiveEs"));
        OfficeUi.SetTextColumns(
            DeletionGrid,
            ("Código", "MaterialCode"),
            ("Nombre", "MaterialName"),
            ("Estado", "StatusEs"),
            ("Motivo", "Reason"),
            ("Solicitado", "RequestedUtc"));
        ApplyGates();
    }

    private void ApplyGates()
    {
        var gate = _client.Permissions;
        CreateCarrierButton.IsVisible = gate.Has(AppPermissionNames.CarrierManage);
        CreateCmrButton.IsVisible = gate.Has(AppPermissionNames.CmrManage);
        CmrPdfButton.IsVisible = gate.Has(AppPermissionNames.CmrManage);
        CmrPrintButton.IsVisible = gate.Has(AppPermissionNames.CmrManage);
        DnPdfButton.IsVisible = gate.HasAny(
            AppPermissionNames.DeliveryPost,
            AppPermissionNames.DeliveryNoteManage,
            AppPermissionNames.CmrManage);
        DnPrintButton.IsVisible = DnPdfButton.IsVisible;
        ApproveDeletionButton.IsVisible = gate.Has(AppPermissionNames.MaterialDeleteApprove);
        RejectDeletionButton.IsVisible = gate.Has(AppPermissionNames.MaterialDeleteApprove);
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyGates();
        try
        {
            _orders = (await _client.GetSalesOrdersAsync())
                .Where(order => order.Status is "Prepared" or "Closed" or "Confirmed")
                .ToList();
            OrdersGrid.ItemsSource = _orders.Select(order => new
            {
                order.Number,
                StatusEs = OfficeUi.StatusToEs(order.Status),
            }).ToList();
            if (DnPdfButton.IsVisible)
            {
                _deliveries = await _client.GetDeliveriesAsync();
                PickerHelpers.BindDeliveries(DeliveryBox, _deliveries);
                if (DeliveryBox.ItemCount > 0 && DeliveryBox.SelectedIndex < 0)
                {
                    DeliveryBox.SelectedIndex = 0;
                }
            }

            if (_client.Permissions.Has(AppPermissionNames.CarrierManage)
                || _client.Permissions.Has(AppPermissionNames.CmrManage))
            {
                _carriers = await _client.GetCarriersAsync();
                CarriersGrid.ItemsSource = _carriers.Select(carrier => new
                {
                    carrier.Code,
                    carrier.Name,
                    ActiveEs = carrier.IsActive ? "Sí" : "No",
                }).ToList();
                CarrierBox.ItemsSource = _carriers
                    .Where(carrier => carrier.IsActive)
                    .Select(carrier => new ComboBoxItem
                    {
                        Content = $"{carrier.Code} — {carrier.Name}",
                        Tag = carrier.Id,
                    })
                    .ToList();
                if (CarrierBox.ItemCount > 0)
                {
                    CarrierBox.SelectedIndex = 0;
                }
            }

            if (_client.Permissions.Has(AppPermissionNames.MaterialDeleteApprove))
            {
                _deletions = (await _client.GetMaterialDeletionRequestsAsync())
                    .Where(value => value.Status == "Pending")
                    .ToList();
                await _client.Masters.RefreshAsync();
                var materials = _client.Masters.Materials.ToDictionary(m => m.Id);
                DeletionGrid.ItemsSource = _deletions.Select(deletion =>
                {
                    materials.TryGetValue(deletion.MaterialId, out var material);
                    var name = material is null
                        ? deletion.MaterialId.ToString("D")
                        : (string.IsNullOrWhiteSpace(material.DisplayName)
                            ? material.Description
                            : material.DisplayName);
                    return new
                    {
                        MaterialCode = material?.Code ?? "—",
                        MaterialName = name,
                        StatusEs = OfficeUi.StatusToEs(deletion.Status),
                        deletion.Reason,
                        deletion.RequestedUtc,
                    };
                }).ToList();
            }

            EmptyHint.IsVisible = _orders.Count == 0
                && _carriers.Count == 0
                && _deletions.Count == 0;
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnCreateCarrierClick(object? sender, RoutedEventArgs e)
    {
        var code = CarrierCodeBox.Text?.Trim() ?? string.Empty;
        var name = CarrierNameBox.Text?.Trim() ?? string.Empty;
        if (code.Length < 1 || name.Length < 1)
        {
            StatusText.Text = "Indique código y nombre del transportista.";
            return;
        }

        try
        {
            await _client.CreateCarrierAsync(
                new CreateCarrierRequestDto(code, name, null, null));
            await UiHelpers.ShowInfoAsync(this, "Transportista creado.");
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

    private async void OnCreateCmrClick(object? sender, RoutedEventArgs e)
    {
        if (!CreateCmrButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para emitir CMR.";
            return;
        }

        if (OrdersGrid.SelectedIndex < 0 || OrdersGrid.SelectedIndex >= _orders.Count)
        {
            StatusText.Text = "Seleccione un pedido en la lista.";
            return;
        }

        if (CarrierBox.SelectedItem is not ComboBoxItem selected
            || selected.Tag is not Guid carrierId)
        {
            StatusText.Text = "Seleccione transportista (o créelo arriba).";
            return;
        }

        if (!int.TryParse(PackagesBox.Text, CultureInfo.InvariantCulture, out var packages)
            || packages < 1)
        {
            StatusText.Text = "Indique el número de bultos (≥ 1).";
            return;
        }

        try
        {
            Guid? deliveryId = PickerHelpers.TryGetId(DeliveryBox, out var parsedDelivery)
                && parsedDelivery != Guid.Empty
                ? parsedDelivery
                : null;
            var cmr = await _client.CreateCmrAsync(
                new CreateCmrRequestDto(
                    _orders[OrdersGrid.SelectedIndex].Id,
                    deliveryId,
                    carrierId,
                    string.IsNullOrWhiteSpace(PlateBox.Text)
                        ? "SIN-MAT"
                        : PlateBox.Text.Trim(),
                    packages,
                    null));
            _lastCmrId = cmr.Id;
            LastCmrText.Text = $"CMR emitido: {cmr.Number}";
            await UiHelpers.ShowInfoAsync(this, $"CMR {cmr.Number} emitido.");
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
        if (_lastCmrId is null)
        {
            StatusText.Text = "Emita un CMR primero.";
            return;
        }

        try
        {
            var bytes = await _client.DownloadCmrPdfAsync(_lastCmrId.Value);
            await UiHelpers.SaveBytesAsync(this, $"cmr-{_lastCmrId:N}.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintCmrClick(object? sender, RoutedEventArgs e)
    {
        if (_lastCmrId is null)
        {
            StatusText.Text = "Emita un CMR primero.";
            return;
        }

        try
        {
            var bytes = await _client.DownloadCmrPdfAsync(_lastCmrId.Value);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir CMR",
                $"cmr-{_lastCmrId:N}.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelectedDelivery(out Guid deliveryId)
    {
        if (PickerHelpers.TryGetId(DeliveryBox, out deliveryId) && deliveryId != Guid.Empty)
        {
            return true;
        }

        StatusText.Text =
            "Seleccione un albarán / entrega por número (listado de Ventas → Contabilizar entrega).";
        return false;
    }

    private async void OnDeliveryPdfClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedDelivery(out var deliveryId))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadDeliveryPdfAsync(deliveryId);
            await UiHelpers.SaveBytesAsync(this, $"albaran-{deliveryId:N}.pdf", bytes);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPrintDeliveryClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelectedDelivery(out var deliveryId))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadDeliveryPdfAsync(deliveryId);
            await PrintDocumentHelper.ShowAsync(
                this,
                bytes,
                "Imprimir albarán",
                $"albaran-{deliveryId:N}.pdf");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnApproveDeletionClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelectedDeletion(out var request))
        {
            return;
        }

        try
        {
            await _client.ApproveMaterialDeletionAsync(owner, request.Id);
            await UiHelpers.ShowInfoAsync(this, "Baja de material aprobada.");
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRejectDeletionClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelectedDeletion(out var request))
        {
            return;
        }

        try
        {
            await _client.RejectMaterialDeletionAsync(owner, request.Id);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelectedDeletion(out MaterialDeletionResponse request)
    {
        request = null!;
        if (DeletionGrid.SelectedIndex < 0 || DeletionGrid.SelectedIndex >= _deletions.Count)
        {
            StatusText.Text = "Seleccione una solicitud de baja.";
            return false;
        }

        request = _deletions[DeletionGrid.SelectedIndex];
        return true;
    }
}
