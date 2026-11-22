using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AAALogistics.Client.Services;
using AAALogistics.Client.Views;

namespace AAALogistics.Client;

internal partial class MainWindow : Window
{
    private readonly LogisticsApiClient? _client;
    private readonly string _userName;
    private readonly Dictionary<string, Control> _modules = new(StringComparer.Ordinal);
    private string _currentModule = "home";
    private DateTimeOffset _lastRefreshUtc;

    public MainWindow()
    {
        _userName = string.Empty;
        InitializeComponent();
        ApplyBrand();
    }

    public MainWindow(LogisticsApiClient client, string userName)
        : this()
    {
        _client = client;
        _userName = userName;
        UserText.Text = userName;
        Title = $"{BrandTheme.ShortName} — {userName}";
        BrandText.Text = BrandTheme.ShortName;
        BrandTagline.Text = BrandTheme.Tagline;
        StatusLeft.Text = client.BaseUrl;
        BrandTheme.Changed += OnBrandThemeChanged;
        Opened += async (_, _) =>
        {
            await client.RefreshIdentityAsync();
            ApplyNavPermissions();
            await LoadBrandingAsync();
            Navigate("home");
            await RefreshCurrentAsync();
            await RefreshStatusBarAsync();
        };
        Closed += (_, _) => BrandTheme.Changed -= OnBrandThemeChanged;
    }

    private void ApplyBrand()
    {
        Background = BrandTheme.Brush(BrandTheme.Background);
        BrandText.Foreground = Brushes.White;
        BrandText.Text = BrandTheme.ShortName;
        BrandTagline.Text = BrandTheme.Tagline;
        UserText.Foreground = BrandTheme.Brush(BrandTheme.PrimaryLight);
        Title = string.IsNullOrWhiteSpace(_userName)
            ? BrandTheme.ShortName
            : $"{BrandTheme.ShortName} — {_userName}";
        ApplyBrandLogo();
    }

    private void OnBrandThemeChanged() => ApplyBrand();

    private void ApplyBrandLogo()
    {
        if (!string.IsNullOrWhiteSpace(BrandTheme.LogoLocalPath)
            && File.Exists(BrandTheme.LogoLocalPath))
        {
            try
            {
                BrandLogo.Source = new Bitmap(BrandTheme.LogoLocalPath);
                BrandLogo.IsVisible = true;
                return;
            }
            catch (Exception exception) when (exception is IOException or ArgumentException)
            {
            }
        }

        BrandLogo.Source = null;
        BrandLogo.IsVisible = false;
    }

    private async Task LoadBrandingAsync()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            var brand = await _client.GetBrandingAsync();
            string? logoPath = null;
            if (brand.HasLogo)
            {
                try
                {
                    var bytes = await _client.DownloadBrandingLogoAsync();
                    var dir = Path.Combine(AppContext.BaseDirectory, "branding");
                    Directory.CreateDirectory(dir);
                    logoPath = Path.Combine(dir, "logo-cache.bin");
                    await File.WriteAllBytesAsync(logoPath, bytes);
                }
                catch (Exception exception) when (exception is ApiException
                    or HttpRequestException
                    or TaskCanceledException
                    or IOException)
                {
                    logoPath = null;
                }
            }

            BrandTheme.ApplyRuntime(
                brand.DisplayName,
                brand.AccentHex,
                brand.Tagline,
                logoPath);
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            ApplyBrand();
        }
    }

    private void ApplyNavPermissions()
    {
        if (_client is null)
        {
            return;
        }

        var gate = _client.Permissions;
        NavCatalog.IsVisible = gate.HasAny(
            AppPermissionNames.MaterialManage,
            AppPermissionNames.WarehouseManage,
            AppPermissionNames.SupplierManage,
            AppPermissionNames.CustomerManage);
        NavPurchases.IsVisible = gate.Has(AppPermissionNames.PurchaseOrderCreate);
        NavSales.IsVisible = gate.HasAny(
            AppPermissionNames.SalesOrderCreate,
            AppPermissionNames.SalesOrderRead);
        NavQuotes.IsVisible = gate.HasAny(
            AppPermissionNames.SalesOrderCreate,
            AppPermissionNames.SalesOrderRead);
        NavPrep.IsVisible = gate.Has(AppPermissionNames.SalesOrderPrepare);
        NavWarehouses.IsVisible = gate.Has(AppPermissionNames.LocationManage);
        NavOffice.IsVisible = gate.HasAny(
            AppPermissionNames.CmrManage,
            AppPermissionNames.CarrierManage,
            AppPermissionNames.SalesOrderClose,
            AppPermissionNames.MaterialDeleteApprove);
        NavBilling.IsVisible = gate.HasAny(
            AppPermissionNames.InvoiceIssue,
            AppPermissionNames.InvoicePrint);
        NavInventory.IsVisible = gate.Has(AppPermissionNames.StockRead);
        NavWarehouseRf.IsVisible = gate.HasAny(
            AppPermissionNames.StockRead,
            AppPermissionNames.StockTransfer,
            AppPermissionNames.StockAdjust,
            AppPermissionNames.SalesOrderPrepare);
        NavRegistration.IsVisible = gate.HasAny(
            AppPermissionNames.RegistrationApprove,
            AppPermissionNames.RegistrationReject);
        NavReports.IsVisible = gate.Has(AppPermissionNames.ReportRead);
        NavAudit.IsVisible = gate.Has(AppPermissionNames.AuditVerify);
        NavAccounts.IsVisible = gate.Has(AppPermissionNames.UserRead);
        NavBranding.IsVisible = gate.Has(AppPermissionNames.InvoiceConfig) || gate.IsAdmin;
        NavIntegrations.IsVisible = gate.HasAny(
            AppPermissionNames.MaterialManage,
            AppPermissionNames.DeliveryPost);
        NavLab.IsVisible = gate.HasAny(
            AppPermissionNames.StockRead,
            AppPermissionNames.MaterialManage,
            AppPermissionNames.SalesOrderPrepare);

        ApplyStudioNavVisibility();
        ApplyNavSectionVisibility();
    }

    private void ApplyStudioNavVisibility()
    {
        if (_client is null)
        {
            return;
        }

        var gate = _client.Permissions;
        bool On(string id) =>
            StudioModuleRegistry.All.FirstOrDefault(m =>
                string.Equals(m.Id, id, StringComparison.Ordinal)) is { Enabled: true } module
            && StudioModuleRegistry.HasAccess(module, gate);

        NavPricing.IsVisible = On("pricing");
        NavMm.IsVisible = On("mm");
        NavFinance.IsVisible = On("finance");
        NavPp.IsVisible = On("pp");
        NavQm.IsVisible = On("qm");
        NavOrg.IsVisible = On("org");
        NavWorkflow.IsVisible = On("workflow");
        NavIntHub.IsVisible = On("integrations");
        NavBi.IsVisible = On("bi");
        NavHr.IsVisible = On("hr");
        NavExt.IsVisible = On("ext");
        NavStudio.IsVisible = StudioModuleRegistry.HasAnyModuleAccess(gate);
    }

    private void ApplyNavSectionVisibility()
    {
        NavGroupStudio.IsVisible = NavStudio.IsVisible
            || NavPricing.IsVisible
            || NavMm.IsVisible
            || NavFinance.IsVisible
            || NavPp.IsVisible
            || NavQm.IsVisible
            || NavOrg.IsVisible
            || NavWorkflow.IsVisible
            || NavIntHub.IsVisible
            || NavBi.IsVisible
            || NavHr.IsVisible
            || NavExt.IsVisible;
        NavGroupMaestros.IsVisible = NavCatalog.IsVisible || NavWarehouses.IsVisible;
        NavGroupCompras.IsVisible = NavPurchases.IsVisible;
        NavGroupVentas.IsVisible =
            NavSales.IsVisible || NavQuotes.IsVisible || NavBilling.IsVisible;
        NavGroupAlmacen.IsVisible =
            NavInventory.IsVisible
            || NavPrep.IsVisible
            || NavWarehouseRf.IsVisible
            || NavOffice.IsVisible
            || NavLab.IsVisible;
        NavGroupSistema.IsVisible = NavRegistration.IsVisible
            || NavReports.IsVisible
            || NavAudit.IsVisible
            || NavAccounts.IsVisible
            || NavBranding.IsVisible
            || NavIntegrations.IsVisible;
    }

    private void OnNavClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            Navigate(tag);
            _ = RefreshCurrentAsync();
        }
    }

    private void Navigate(string tag)
    {
        if (_client is not null)
        {
            var allowed = tag switch
            {
                "catalog" => _client.Permissions.HasAny(
                    AppPermissionNames.MaterialManage,
                    AppPermissionNames.WarehouseManage,
                    AppPermissionNames.SupplierManage,
                    AppPermissionNames.CustomerManage),
                "purchases" => _client.Permissions.Has(AppPermissionNames.PurchaseOrderCreate),
                "sales" => _client.Permissions.HasAny(
                    AppPermissionNames.SalesOrderCreate,
                    AppPermissionNames.SalesOrderRead),
                "quotes" => _client.Permissions.HasAny(
                    AppPermissionNames.SalesOrderCreate,
                    AppPermissionNames.SalesOrderRead),
                "prep" => _client.Permissions.Has(AppPermissionNames.SalesOrderPrepare),
                "warehouses" => _client.Permissions.Has(AppPermissionNames.LocationManage),
                "office" => _client.Permissions.HasAny(
                    AppPermissionNames.CmrManage,
                    AppPermissionNames.CarrierManage,
                    AppPermissionNames.SalesOrderClose,
                    AppPermissionNames.MaterialDeleteApprove),
                "billing" => _client.Permissions.HasAny(
                    AppPermissionNames.InvoiceIssue,
                    AppPermissionNames.InvoicePrint),
                "inventory" => _client.Permissions.Has(AppPermissionNames.StockRead),
                "registration" => _client.Permissions.HasAny(
                    AppPermissionNames.RegistrationApprove,
                    AppPermissionNames.RegistrationReject),
                "reports" => _client.Permissions.Has(AppPermissionNames.ReportRead),
                "audit" => _client.Permissions.Has(AppPermissionNames.AuditVerify),
                "accounts" => _client.Permissions.Has(AppPermissionNames.UserRead),
                "branding" => _client.Permissions.Has(AppPermissionNames.InvoiceConfig)
                    || _client.Permissions.IsAdmin,
                "integrations" => _client.Permissions.HasAny(
                    AppPermissionNames.MaterialManage,
                    AppPermissionNames.DeliveryPost),
                "lab" => _client.Permissions.HasAny(
                    AppPermissionNames.StockRead,
                    AppPermissionNames.MaterialManage,
                    AppPermissionNames.SalesOrderPrepare),
                "studio" => StudioModuleRegistry.HasAnyModuleAccess(_client.Permissions),
                "pricing" or "mm" or "finance" or "pp" or "qm" or "org"
                    or "workflow" or "inthub" or "bi" or "hr" or "ext" =>
                    StudioModuleRegistry.IsAvailable(tag, _client.Permissions),
                _ => true,
            };
            if (!allowed)
            {
                tag = "home";
            }
        }

        _currentModule = tag;
        foreach (var child in NavPanel.Children.OfType<Button>())
        {
            child.Classes.Remove("selected");
            if (child.Tag is string childTag
                && string.Equals(childTag, tag, StringComparison.Ordinal))
            {
                child.Classes.Add("selected");
            }
        }

        WorkArea.Content = GetOrCreateModule(tag);
    }

    private Control GetOrCreateModule(string tag)
    {
        if (_modules.TryGetValue(tag, out var existing))
        {
            return existing;
        }

        if (_client is null)
        {
            return new TextBlock { Text = "Sin sesión." };
        }

        Control created = tag switch
        {
            "catalog" => new CatalogView(_client),
            "purchases" => new PurchasesView(_client),
            "sales" => new SalesView(_client),
            "quotes" => new QuotesView(_client),
            "prep" => new PrepView(_client),
            "warehouses" => new WarehousesView(_client),
            "office" => new OfficeView(_client),
            "billing" => new BillingView(_client),
            "inventory" => new InventoryView(_client),
            "warehouserf" => new WarehouseRfView(_client),
            "registration" => new RegistrationView(_client),
            "reports" => new ReportsView(_client),
            "audit" => new AuditView(_client),
            "accounts" => new AccountsView(_client),
            "branding" => new BrandingView(_client),
            "lab" => new LabView(_client),
            "integrations" => new SapIntegrationsView(_client),
            "studio" => CreateStudioHub(),
            "pricing" => new PricingView(_client),
            "mm" => new MmAdvancedView(_client),
            "finance" => new FinanceView(_client),
            "pp" => new ProductionView(_client),
            "qm" => new QualityView(_client),
            "org" => new OrgContextView(_client),
            "workflow" => new WorkflowView(_client),
            "inthub" => new IntegrationHubView(_client),
            "bi" => new BiDashboardView(_client),
            "hr" => new HrLiteView(_client),
            "ext" => new ExtensibilityView(_client),
            _ => CreateHome(_client),
        };
        _modules[tag] = created;
        return created;
    }

    private HomeView CreateHome(LogisticsApiClient client)
    {
        var home = new HomeView(client);
        home.PendingChanged += (below, regs) =>
        {
            PendingBadge.Text = below + regs > 0
                ? $"● {below + regs} pendientes"
                : string.Empty;
        };
        home.NavigateRequested += tag =>
        {
            Navigate(tag);
            _ = RefreshCurrentAsync();
        };
        return home;
    }

    private StudioHubView CreateStudioHub()
    {
        var hub = new StudioHubView(_client);
        hub.NavigateRequested += tag =>
        {
            Navigate(tag);
            _ = RefreshCurrentAsync();
        };
        return hub;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await RefreshCurrentAsync();
        await RefreshStatusBarAsync();
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            await RefreshCurrentAsync();
            await RefreshStatusBarAsync();
        }
    }

    private async void OnLogoutClick(object? sender, RoutedEventArgs e)
    {
        if (_client is not null)
        {
            try
            {
                await _client.LogoutAsync();
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
            }

            _client.Dispose();
        }

        var login = new LoginWindow();
        if (global::Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = login;
        }

        login.Show();
        Close();
    }

    private async Task RefreshCurrentAsync()
    {
        if (WorkArea.Content is null)
        {
            return;
        }

        try
        {
            switch (WorkArea.Content)
            {
                case HomeView home:
                    await home.RefreshAsync();
                    break;
                case CatalogView catalog:
                    await catalog.RefreshAsync();
                    break;
                case PurchasesView purchases:
                    await purchases.RefreshAsync();
                    break;
                case SalesView sales:
                    await sales.RefreshAsync();
                    break;
                case QuotesView quotes:
                    await quotes.RefreshAsync();
                    break;
                case PrepView prep:
                    await prep.RefreshAsync();
                    break;
                case WarehousesView warehouses:
                    await warehouses.RefreshAsync();
                    break;
                case OfficeView office:
                    await office.RefreshAsync();
                    break;
                case BillingView billing:
                    await billing.RefreshAsync();
                    break;
                case InventoryView inventory:
                    await inventory.RefreshAsync();
                    break;
                case WarehouseRfView warehouseRf:
                    await warehouseRf.RefreshAsync();
                    break;
                case RegistrationView registration:
                    await registration.RefreshAsync();
                    break;
                case ReportsView reports:
                    await reports.RefreshAsync();
                    break;
                case AuditView audit:
                    await audit.RefreshAsync();
                    break;
                case AccountsView accounts:
                    await accounts.RefreshAsync();
                    break;
                case BrandingView branding:
                    await branding.RefreshAsync();
                    break;
                case LabView lab:
                    await lab.RefreshAsync();
                    break;
                case SapIntegrationsView sap:
                    await sap.RefreshAsync();
                    break;
                case StudioHubView studio:
                    studio.Refresh();
                    break;
                case PricingView pricing:
                    await pricing.RefreshAsync();
                    break;
                case MmAdvancedView mm:
                    await mm.RefreshAsync();
                    break;
                case FinanceView finance:
                    await finance.RefreshAsync();
                    break;
                case ProductionView pp:
                    await pp.RefreshAsync();
                    break;
                case QualityView qm:
                    await qm.RefreshAsync();
                    break;
                case OrgContextView org:
                    await org.RefreshAsync();
                    break;
                case WorkflowView workflow:
                    await workflow.RefreshAsync();
                    break;
                case IntegrationHubView inthub:
                    await inthub.RefreshAsync();
                    break;
                case BiDashboardView bi:
                    await bi.RefreshAsync();
                    break;
                case HrLiteView hr:
                    await hr.RefreshAsync();
                    break;
                case ExtensibilityView ext:
                    await ext.RefreshAsync();
                    break;
            }

            _lastRefreshUtc = DateTimeOffset.Now;
            StatusCenter.Text = $"Actualizado {_lastRefreshUtc:HH:mm:ss} · F5";
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await AppDialog.ShowMessageAsync(this, BrandTheme.ShortName, exception.Message);
        }
    }

    private async Task RefreshStatusBarAsync()
    {
        if (_client is null)
        {
            return;
        }

        try
        {
            await _client.EnsureAccessTokenAsync();
            var ping = await _client.PingAsync();
            StatusRight.Text =
                $"{ping.Surface}/{ping.Status} · perms {_client.Permissions.Permissions.Count}";
            StatusLeft.Text = $"{_client.BaseUrl} · {_userName}";
        }
        catch (Exception)
        {
            StatusRight.Text = "API no responde";
        }
    }
}
