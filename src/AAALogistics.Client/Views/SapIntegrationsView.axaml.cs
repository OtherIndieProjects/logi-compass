using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class SapIntegrationsView : UserControl
{
    private readonly LogisticsApiClient _client;

    public SapIntegrationsView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Sap);
        OfficeUi.BindHelp(EnqueueHelpHost, OfficeHelpTexts.Keys.FieldSapEnqueue);
        ApplyPermissions();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var status = await _client.GetSapStatusAsync();
            StatusBanner.Text =
                $"Modo {status.Mode} · Enabled={status.Enabled}"
                + (string.IsNullOrWhiteSpace(status.BaseUrlHint)
                    ? " · sin BaseUrl (mock lab)"
                    : $" · {status.BaseUrlHint}");
            StatusText.Text = string.Empty;
            await BindDeliveriesAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusBanner.Text = "No se pudo leer el estado SAP.";
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task BindDeliveriesAsync()
    {
        try
        {
            var rows = await _client.GetDeliveriesAsync();
            PickerHelpers.BindDeliveries(DeliveryBox, rows);
            if (DeliveryBox.ItemCount > 0 && DeliveryBox.SelectedIndex < 0)
            {
                DeliveryBox.SelectedIndex = 0;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
        }
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        var materials = gate.Has(AppPermissionNames.MaterialManage);
        var delivery = gate.Has(AppPermissionNames.DeliveryPost);
        ListButton.IsEnabled = materials;
        SyncButton.IsEnabled = materials;
        OutboxButton.IsEnabled = delivery;
        ProcessButton.IsEnabled = delivery;
        PushButton.IsEnabled = delivery;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnListMaterialsClick(object? sender, RoutedEventArgs e) =>
        await ListMaterialsAsync();

    private async void OnSyncClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result = await _client.SyncSapMaterialsAsync();
            StatusText.Text =
                $"Sync OK · listed={result.Listed} created={result.Created} updated={result.Updated}";
            await ListMaterialsAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnOutboxClick(object? sender, RoutedEventArgs e) =>
        await LoadOutboxAsync();

    private async void OnProcessClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _client.ProcessSapOutboxAsync();
            StatusText.Text = "Ciclo outbox ejecutado.";
            await LoadOutboxAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnPushClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(DeliveryBox, out var deliveryId)
            || deliveryId == Guid.Empty)
        {
            StatusText.Text = "Seleccione un albarán / entrega en el desplegable.";
            return;
        }

        try
        {
            var queued = await _client.QueueSapDeliveryPushAsync(deliveryId);
            StatusText.Text =
                $"Encolado {queued.OutboxId:D} · estado {queued.Status}";
            await LoadOutboxAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task ListMaterialsAsync()
    {
        try
        {
            var rows = await _client.GetSapMaterialsAsync();
            Grid.ItemsSource = rows;
            StatusText.Text = $"{rows.Count} materiales remotos.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task LoadOutboxAsync()
    {
        try
        {
            var rows = await _client.GetSapOutboxAsync();
            Grid.ItemsSource = rows;
            StatusText.Text = $"{rows.Count} mensajes outbox.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }
}
