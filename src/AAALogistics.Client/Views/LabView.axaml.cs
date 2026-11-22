using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class LabView : UserControl
{
    private readonly LogisticsApiClient _client;
    private readonly List<(Guid Id, string Number)> _waveOrders = [];
    private IReadOnlyList<WaveResponse> _waves = [];
    private IReadOnlyList<RateCardResponse> _rates = [];
    private IReadOnlyList<TransportRouteResponse> _routes = [];

    public LabView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Lab);
        OfficeUi.BindHelp(WaveHelpHost, OfficeHelpTexts.Keys.FieldLabWave);
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        var errors = new List<string>();
        try
        {
            await _client.Masters.RefreshAsync();
            PickerHelpers.BindUnits(FromUnitBox, _client.Masters.Units);
            PickerHelpers.BindUnits(ToUnitBox, _client.Masters.Units);
            var sales = await _client.GetSalesOrdersAsync();
            PickerHelpers.BindSalesOrders(WaveOrderBox, sales);
            PickerHelpers.BindSalesOrders(RouteOrderBox, sales);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            errors.Add(UiHelpers.ToUserMessage(exception));
        }

        await TryRefreshSectionAsync(RefreshWavesAsync, errors);
        await TryRefreshSectionAsync(RefreshUomAsync, errors);
        await TryRefreshSectionAsync(RefreshMrpAsync, errors);
        await TryRefreshSectionAsync(RefreshRatesAsync, errors);
        await TryRefreshSectionAsync(RefreshRoutesAsync, errors);

        if (errors.Count > 0)
        {
            StatusText.Text = string.Join(" · ", errors.Distinct());
        }
    }

    private static async Task TryRefreshSectionAsync(Func<Task> refresh, List<string> errors)
    {
        try
        {
            await refresh();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            var message = exception.Message;
            if (exception is ApiException api
                && (api.Code is "AAA-AUTH-FORBIDDEN" or "AAA-AUTH-0003"
                    || api.Message.Contains("permiso", StringComparison.OrdinalIgnoreCase)
                    || api.Message.Contains("403", StringComparison.Ordinal)))
            {
                message = "Sin permiso en esta sección (403).";
            }

            errors.Add(message);
        }
    }

    private async void OnRefreshWavesClick(object? sender, RoutedEventArgs e) =>
        await RefreshWavesAsync();

    private void OnAddWaveOrderClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(WaveOrderBox, out var id))
        {
            StatusText.Text = "Seleccione un pedido de venta en el desplegable.";
            return;
        }

        if (_waveOrders.Any(row => row.Id == id))
        {
            StatusText.Text = "Ese pedido ya está en la ola.";
            return;
        }

        var label = WaveOrderBox.SelectedItem is PickerHelpers.PickerItem item
            ? item.Label.Split('—')[0].Trim()
            : id.ToString("D");
        _waveOrders.Add((id, label));
        UpdateWaveSelectedText();
        StatusText.Text = string.Empty;
    }

    private void OnClearWaveOrdersClick(object? sender, RoutedEventArgs e)
    {
        _waveOrders.Clear();
        UpdateWaveSelectedText();
    }

    private void UpdateWaveSelectedText() =>
        WaveSelectedText.Text = _waveOrders.Count == 0
            ? "Ningún pedido en la ola todavía."
            : "Pedidos en la ola: " + string.Join(", ", _waveOrders.Select(row => row.Number));

    private async void OnCreateWaveClick(object? sender, RoutedEventArgs e)
    {
        if (_waveOrders.Count == 0)
        {
            StatusText.Text = "Añada al menos un pedido de venta a la ola (desplegable + Añadir).";
            return;
        }

        if (!int.TryParse(WavePriorityBox.Text, out var priority))
        {
            priority = 0;
        }

        try
        {
            await _client.CreateWaveAsync(
                new CreateWaveRequestDto(_waveOrders.Select(row => row.Id).ToList(), priority));
            _waveOrders.Clear();
            UpdateWaveSelectedText();
            await RefreshWavesAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnReleaseWaveClick(object? sender, RoutedEventArgs e)
    {
        if (WavesGrid.SelectedIndex < 0 || WavesGrid.SelectedIndex >= _waves.Count)
        {
            StatusText.Text = "Seleccione una ola.";
            return;
        }

        try
        {
            await _client.ReleaseWaveAsync(_waves[WavesGrid.SelectedIndex].Id);
            await RefreshWavesAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnUpsertUomClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(FromUnitBox, out var from)
            || !PickerHelpers.TryGetId(ToUnitBox, out var to)
            || !UiHelpers.TryParseDecimal(UomFactorBox.Text, out var factor))
        {
            StatusText.Text = "Unidades y factor obligatorios.";
            return;
        }

        try
        {
            await _client.UpsertUnitConversionAsync(
                new UpsertUnitConversionRequestDto(from, to, factor));
            await RefreshUomAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnConvertUomClick(object? sender, RoutedEventArgs e)
    {
        if (!PickerHelpers.TryGetId(FromUnitBox, out var from)
            || !PickerHelpers.TryGetId(ToUnitBox, out var to)
            || !UiHelpers.TryParseDecimal(UomQtyBox.Text, out var qty))
        {
            StatusText.Text = "Unidades y cantidad obligatorias.";
            return;
        }

        try
        {
            var result = await _client.ConvertQuantityAsync(
                new ConvertQuantityRequestDto(from, to, qty));
            UomResultText.Text =
                $"{result.InputQuantity} × {result.Factor} = {result.OutputQuantity}";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnGenerateMrpClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var rows = await _client.GenerateMrpAsync();
            MrpGrid.ItemsSource = rows;
            StatusText.Text = $"{rows.Count} solicitudes generadas.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshMrpClick(object? sender, RoutedEventArgs e) =>
        await RefreshMrpAsync();

    private async void OnUpsertRateClick(object? sender, RoutedEventArgs e)
    {
        if (!UiHelpers.TryParseDecimal(RatePickPriceBox.Text, out var pickPrice))
        {
            StatusText.Text = "Precio PICK inválido.";
            return;
        }

        try
        {
            await _client.UpsertRateCardAsync(
                new UpsertRateCardRequestDto(
                    RateCodeBox.Text?.Trim() ?? "STD",
                    RateNameBox.Text?.Trim() ?? "Estándar",
                    "EUR",
                    [
                        new UpsertRateCardLineRequestDto("PICK", pickPrice, "linea"),
                        new UpsertRateCardLineRequestDto("STORAGE", 0.5m, "pallet-día"),
                    ]));
            await RefreshRatesAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnComputeRateClick(object? sender, RoutedEventArgs e)
    {
        if (_rates.Count == 0)
        {
            StatusText.Text = "No hay tarifas.";
            return;
        }

        try
        {
            var report = await _client.ComputeRateChargesAsync(
                _rates[0].Id,
                new Dictionary<string, decimal> { ["PICK"] = 10m });
            RateResultText.Text =
                $"Total {report.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)} {report.Currency}";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshRateClick(object? sender, RoutedEventArgs e) =>
        await RefreshRatesAsync();

    private async void OnCreateRouteClick(object? sender, RoutedEventArgs e)
    {
        if (!UiHelpers.TryParseDecimal(RouteKmBox.Text, out var km)
            || !UiHelpers.TryParseDecimal(RouteBaseBox.Text, out var baseTariff)
            || !UiHelpers.TryParseDecimal(RoutePerKmBox.Text, out var perKm))
        {
            StatusText.Text = "Km/base/€·km inválidos.";
            return;
        }

        try
        {
            await _client.CreateTransportRouteAsync(
                new CreateTransportRouteRequestDto(
                    RouteCodeBox.Text?.Trim() ?? "R1",
                    RouteNameBox.Text?.Trim() ?? "Ruta",
                    null,
                    km,
                    baseTariff,
                    perKm));
            await RefreshRoutesAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnAddStopClick(object? sender, RoutedEventArgs e)
    {
        if (RoutesGrid.SelectedIndex < 0 || RoutesGrid.SelectedIndex >= _routes.Count)
        {
            StatusText.Text = "Seleccione una ruta.";
            return;
        }

        if (!PickerHelpers.TryGetId(RouteOrderBox, out var salesOrderId))
        {
            StatusText.Text = "Seleccione un pedido de venta para la parada.";
            return;
        }

        try
        {
            var route = _routes[RoutesGrid.SelectedIndex];
            await _client.AddTransportStopAsync(
                route.Id,
                new AddTransportStopRequestDto(
                    route.Stops.Count + 1,
                    salesOrderId,
                    null,
                    null));
            await RefreshRoutesAsync();
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnRefreshRoutesClick(object? sender, RoutedEventArgs e) =>
        await RefreshRoutesAsync();

    private async void OnRfScanClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RfCodeBox.Text))
        {
            StatusText.Text = "Código vacío.";
            return;
        }

        try
        {
            var result = await _client.RfScanAsync(RfCodeBox.Text.Trim());
            RfResultText.Text = $"{result.Kind}: {result.Code}";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnCreateCompanyClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await _client.CreateCompanyAsync(
                new CreateCompanyRequestDto(
                    CompanyCodeBox.Text?.Trim() ?? "AAA",
                    CompanyNameBox.Text?.Trim() ?? "AAA Holding",
                    "B12345678",
                    "EUR"));
            StatusText.Text = "Empresa creada.";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnFxClick(object? sender, RoutedEventArgs e)
    {
        if (!UiHelpers.TryParseDecimal(FxRateBox.Text, out var rate))
        {
            StatusText.Text = "Tipo FX inválido.";
            return;
        }

        try
        {
            await _client.UpsertExchangeRateAsync(
                new UpsertExchangeRateRequestDto(
                    "USD",
                    "EUR",
                    rate,
                    DateOnly.FromDateTime(DateTime.Today)));
            var converted = await _client.ConvertFxAsync(
                new ConvertFxRequestDto("USD", "EUR", 100m));
            FxResultText.Text =
                $"100 USD = {converted.OutputAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)} EUR";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task RefreshWavesAsync()
    {
        _waves = await _client.ListWavesAsync();
        var rows = _waves.Select(row => new
        {
            Número = row.Number,
            Estado = row.Status,
            Prioridad = row.Priority,
            Pedidos = row.SalesOrderIds.Count,
            Id = row.Id,
        }).ToList();
        WavesGrid.ItemsSource = rows;
        WavesEmptyText.IsVisible = rows.Count == 0;
        WavesGrid.IsVisible = rows.Count > 0;
        UpdateLabEmptyHint();
    }

    private async Task RefreshUomAsync()
    {
        var rows = await _client.ListUnitConversionsAsync();
        UomGrid.ItemsSource = rows;
        UomEmptyText.IsVisible = rows.Count == 0;
        UomGrid.IsVisible = rows.Count > 0;
        UpdateLabEmptyHint();
    }

    private async Task RefreshMrpAsync()
    {
        var rows = await _client.ListPurchaseRequisitionsAsync();
        MrpGrid.ItemsSource = rows;
        MrpEmptyText.IsVisible = rows.Count == 0;
        MrpGrid.IsVisible = rows.Count > 0;
        UpdateLabEmptyHint();
    }

    private async Task RefreshRatesAsync()
    {
        _rates = await _client.ListRateCardsAsync();
        var rows = _rates.Select(row => new
        {
            Código = row.Code,
            Nombre = row.Name,
            Moneda = row.Currency,
            Líneas = row.Lines.Count,
            Id = row.Id,
        }).ToList();
        RateGrid.ItemsSource = rows;
        RateEmptyText.IsVisible = rows.Count == 0;
        RateGrid.IsVisible = rows.Count > 0;
        UpdateLabEmptyHint();
    }

    private async Task RefreshRoutesAsync()
    {
        _routes = await _client.ListTransportRoutesAsync();
        var rows = _routes.Select(row => new
        {
            Código = row.Code,
            Nombre = row.Name,
            Km = row.DistanceKm,
            Tarifa = row.ComputedTariff,
            Paradas = row.Stops.Count,
            Id = row.Id,
        }).ToList();
        RoutesGrid.ItemsSource = rows;
        RoutesEmptyText.IsVisible = rows.Count == 0;
        RoutesGrid.IsVisible = rows.Count > 0;
        UpdateLabEmptyHint();
    }

    private void UpdateLabEmptyHint()
    {
        var allEmpty = WavesEmptyText.IsVisible
            && UomEmptyText.IsVisible
            && MrpEmptyText.IsVisible
            && RateEmptyText.IsVisible
            && RoutesEmptyText.IsVisible;
        EmptyHintText.IsVisible = allEmpty;
    }

    private static IReadOnlyList<Guid> ParseGuidCsv(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .ToArray();
    }
}
