using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AAALogistics.Client.Services;

namespace AAALogistics.Client.Views;

internal partial class HomeView : UserControl
{
    private readonly LogisticsApiClient _client;
    private readonly DispatcherTimer _timer;
    public event Action<int, int>? PendingChanged;
    public event Action<string>? NavigateRequested;

    public HomeView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Home);
        OfficeUi.SetTextColumns(PrepGrid, ("Número", "Number"), ("Estado", "StatusEs"));
        OfficeUi.SetTextColumns(OfficeGrid, ("Número", "Number"), ("Estado", "StatusEs"));
        OfficeUi.SetTextColumns(OrdersGrid, ("Número", "Number"), ("Estado", "StatusEs"), ("Líneas", "Lines"));
        OfficeUi.SetTextColumns(
            RegistrationGrid,
            ("Empresa", "CompanyName"),
            ("Contacto", "ContactName"),
            ("Estado", "StatusEs"));
        OfficeUi.SetTextColumns(
            DeletionGrid,
            ("Código", "MaterialCode"),
            ("Nombre", "MaterialName"),
            ("Estado", "StatusEs"),
            ("Motivo", "Reason"));
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _timer.Tick += async (_, _) => await RefreshAsync(silent: true);
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public async Task RefreshAsync(bool silent = false)
    {
        if (!silent)
        {
            StatusText.Text = string.Empty;
        }

        try
        {
            var gate = _client.Permissions;
            var me = await _client.GetMeAsync();
            var ping = await _client.PingAsync();
            IdentityText.Text =
                $"{BrandTheme.ShortName} · {me.UserName} · rangos: {OfficeUi.RangesToEs(me.Roles)} · permisos: {me.Permissions.Count} · API {ping.Surface}/{ping.Status}";
            ApplyTileVisibility(gate);

            PrepPanel.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
            CardPrep.IsVisible = PrepPanel.IsVisible;
            OfficePanel.IsVisible = gate.HasAny(
                AppPermissionNames.SalesOrderClose,
                AppPermissionNames.CmrManage,
                AppPermissionNames.MaterialDeleteApprove);
            CardOffice.IsVisible = OfficePanel.IsVisible;
            BelowPanel.IsVisible = gate.Has(AppPermissionNames.StockRead);
            CardStock.IsVisible = BelowPanel.IsVisible;
            RegistrationPanel.IsVisible = gate.HasAny(
                AppPermissionNames.RegistrationApprove,
                AppPermissionNames.RegistrationReject);
            DeletionPanel.IsVisible = gate.Has(AppPermissionNames.MaterialDeleteApprove);
            OrdersPanel.IsVisible = gate.HasAny(
                AppPermissionNames.SalesOrderCreate,
                AppPermissionNames.SalesOrderRead);

            var below = BelowPanel.IsVisible
                ? await _client.GetBelowMinimumAsync()
                : [];
            var regs = RegistrationPanel.IsVisible
                ? await _client.GetRegistrationRequestsAsync()
                : [];
            var orders = OrdersPanel.IsVisible || PrepPanel.IsVisible || OfficePanel.IsVisible
                ? await _client.GetSalesOrdersAsync()
                : [];
            var deletions = DeletionPanel.IsVisible
                ? await _client.GetMaterialDeletionRequestsAsync()
                : [];

            OfficeUi.SetTextColumns(
                BelowMinGrid,
                ("Material", nameof(StockRow.MaterialCode)),
                ("Almacén", nameof(StockRow.WarehouseCode)),
                ("Cantidad", nameof(StockRow.Quantity)),
                ("Mínimo", nameof(StockRow.MinQuantity)));
            BelowMinGrid.ItemsSource = below;
            BelowEmpty.IsVisible = below.Count == 0;
            BelowMinGrid.IsVisible = below.Count > 0;

            RegistrationGrid.ItemsSource = regs.Select(row => new
            {
                row.CompanyName,
                row.ContactName,
                StatusEs = OfficeUi.StatusToEs(row.Status),
            }).ToList();

            OrdersGrid.ItemsSource = orders.Select(order => new
            {
                order.Number,
                StatusEs = OfficeUi.StatusToEs(order.Status),
                Lines = order.Lines.Count,
            }).ToList();

            var prepRows = orders
                .Where(order => order.Status is "Confirmed" or "InPreparation")
                .Select(order => new
                {
                    order.Number,
                    StatusEs = OfficeUi.StatusToEs(order.Status),
                })
                .ToList();
            PrepGrid.ItemsSource = prepRows;
            PrepEmpty.IsVisible = prepRows.Count == 0;
            PrepGrid.IsVisible = prepRows.Count > 0;

            var officeRows = orders
                .Where(order => order.Status == "Prepared")
                .Select(order => new
                {
                    order.Number,
                    StatusEs = OfficeUi.StatusToEs(order.Status),
                })
                .ToList();
            OfficeGrid.ItemsSource = officeRows;
            OfficeEmpty.IsVisible = officeRows.Count == 0;
            OfficeGrid.IsVisible = officeRows.Count > 0;

            if (DeletionPanel.IsVisible && deletions.Count > 0)
            {
                await _client.Masters.RefreshAsync();
            }

            var materials = _client.Masters.Materials.ToDictionary(m => m.Id);
            DeletionGrid.ItemsSource = deletions
                .Where(value => value.Status == "Pending")
                .Select(value =>
                {
                    materials.TryGetValue(value.MaterialId, out var material);
                    var name = material is null
                        ? value.MaterialId.ToString("D")
                        : (string.IsNullOrWhiteSpace(material.DisplayName)
                            ? material.Description
                            : material.DisplayName);
                    return new
                    {
                        MaterialCode = material?.Code ?? "—",
                        MaterialName = name,
                        StatusEs = OfficeUi.StatusToEs(value.Status),
                        value.Reason,
                    };
                })
                .ToList();

            var prepCount = PrepPanel.IsVisible ? prepRows.Count : 0;
            var officeCount = OfficePanel.IsVisible ? officeRows.Count : 0;
            var deletionCount = deletions.Count(value => value.Status == "Pending");
            CountPrep.Text = prepCount.ToString();
            CountOffice.Text = (officeCount + deletionCount).ToString();
            CountStock.Text = below.Count.ToString();
            BadgeText.Text =
                $"Pendientes: prep {prepCount} · oficina {officeCount} · stock↓ {below.Count} · registros {regs.Count} · bajas {deletionCount}";
            PendingChanged?.Invoke(below.Count + prepCount + officeCount + deletionCount, regs.Count);

            ChartPrepPanel.IsVisible = PrepPanel.IsVisible;
            ChartStockPanel.IsVisible = BelowPanel.IsVisible;
            ChartOrdersPanel.IsVisible = OrdersPanel.IsVisible;
            UpdateCharts(orders, below, prepRows.Count);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            if (!silent)
            {
                StatusText.Text = UiHelpers.ToUserMessage(exception);
                await UiHelpers.ShowErrorAsync(this, exception);
            }
        }
    }

    private void UpdateCharts(
        IReadOnlyList<SalesOrderRow> orders,
        IReadOnlyList<StockRow> below,
        int prepTotal)
    {
        var confirmed = orders.Count(order => order.Status == "Confirmed");
        var inPrep = orders.Count(order => order.Status == "InPreparation");
        PrepChart.SetSeries(
        [
            ("Confirmado", confirmed, BrandTheme.Accent),
            ("En prep.", inPrep, BrandTheme.Primary),
            ("Cola", prepTotal, BrandTheme.Secondary),
        ]);

        var stockBars = below
            .Select(row =>
            {
                var deficit = Math.Max(0, (double)(row.MinQuantity - row.Quantity));
                return (Label: row.MaterialCode, Value: deficit > 0 ? deficit : 1d, ColorHex: (string?)BrandTheme.Warning);
            })
            .OrderByDescending(row => row.Value)
            .Take(5)
            .ToList();
        StockChart.SetSeries(stockBars);

        var openStatuses = new (string Label, string Status, string Color)[]
        {
            ("Borrador", "Draft", BrandTheme.Secondary),
            ("Confirm.", "Confirmed", BrandTheme.Accent),
            ("En prep.", "InPreparation", BrandTheme.Primary),
            ("Prepar.", "Prepared", BrandTheme.Success),
            ("Entreg.", "Delivered", BrandTheme.PrimaryDark),
        };
        var orderBars = openStatuses
            .Select(item => (
                item.Label,
                (double)orders.Count(order => order.Status == item.Status),
                (string?)item.Color))
            .Where(item => item.Item2 > 0)
            .ToList();
        OrdersChart.SetSeries(orderBars);
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private void OnGoCatalog(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("catalog");

    private void OnGoWarehouses(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("warehouses");

    private void OnGoPurchases(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("purchases");

    private void OnGoSales(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("sales");

    private void OnGoQuotes(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("quotes");

    private void OnGoReports(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("reports");

    private void OnGoAudit(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("audit");

    private void OnGoPrep(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("prep");

    private void ApplyTileVisibility(PermissionGate gate)
    {
        TileCatalog.IsVisible = gate.HasAny(
            AppPermissionNames.MaterialManage,
            AppPermissionNames.WarehouseManage,
            AppPermissionNames.SupplierManage,
            AppPermissionNames.CustomerManage);
        TileWarehouses.IsVisible = gate.Has(AppPermissionNames.LocationManage);
        TilePurchases.IsVisible = gate.Has(AppPermissionNames.PurchaseOrderCreate);
        TileSales.IsVisible = gate.HasAny(
            AppPermissionNames.SalesOrderCreate,
            AppPermissionNames.SalesOrderRead);
        TileQuotes.IsVisible = TileSales.IsVisible;
        TileBilling.IsVisible = gate.HasAny(
            AppPermissionNames.InvoiceIssue,
            AppPermissionNames.InvoicePrint);
        TileInventory.IsVisible = gate.Has(AppPermissionNames.StockRead);
        TilePrep.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
        TileOffice.IsVisible = gate.HasAny(
            AppPermissionNames.CmrManage,
            AppPermissionNames.CarrierManage,
            AppPermissionNames.SalesOrderClose,
            AppPermissionNames.MaterialDeleteApprove);
        TileReports.IsVisible = gate.Has(AppPermissionNames.ReportRead);
        TileAudit.IsVisible = gate.Has(AppPermissionNames.AuditVerify);
        TileAccounts.IsVisible = gate.Has(AppPermissionNames.UserRead);
        TileRegistration.IsVisible = gate.HasAny(
            AppPermissionNames.RegistrationApprove,
            AppPermissionNames.RegistrationReject);
    }

    private void OnGoInventory(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("inventory");

    private void OnGoBilling(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("billing");

    private void OnGoOffice(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("office");

    private void OnGoAccounts(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("accounts");

    private void OnGoRegistration(object? sender, RoutedEventArgs e) =>
        NavigateRequested?.Invoke("registration");
}
