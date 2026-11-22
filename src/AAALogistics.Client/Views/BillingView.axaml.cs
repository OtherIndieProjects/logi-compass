using Avalonia.Controls;
using Avalonia.Interactivity;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

internal partial class BillingView : UserControl
{
    private bool _issuerReady;
    private readonly LogisticsApiClient _client;
    private readonly DraftLineBoard _draft = new();
    private IReadOnlyList<InvoiceResponse> _invoices = [];
    private IReadOnlyList<SalesOrderRow> _salesOrders = [];
    private IReadOnlyList<DeliverySummaryResponse> _deliveries = [];
    private bool _bindingDelivery;
    private string? _sealRelativePath;
    private string? _sealPngBase64;
    private string? _signatureRelativePath;
    private string? _signaturePngBase64;
    private string? _logoRelativePath;
    private string? _logoPngBase64;

    public BillingView(LogisticsApiClient client)
    {
        _client = client;
        InitializeComponent();
        OfficeUi.AttachInfoHelp(TitleRow, OfficeHelpTexts.Keys.Billing);
        _draft.Bind(DraftLinesGrid);
        OfficeUi.BindHelp(SeriesHelpHost, OfficeHelpTexts.Keys.FieldInvoiceSeries);
        OfficeUi.BindHelp(TaxHelpHost, OfficeHelpTexts.Keys.FieldInvoiceTaxRate);
        OfficeUi.BindHelp(IssueHelpHost, OfficeHelpTexts.Keys.FieldIssueInvoice);
        OfficeUi.BindHelp(RectifyHelpHost, OfficeHelpTexts.Keys.FieldRectifyInvoice);
        OfficeUi.BindHelp(IssuerHelpHost, OfficeHelpTexts.Keys.FieldIssuerProfile);
        OfficeUi.SetTextColumns(
            InvoicesGrid,
            ("Número", "DocumentNumber"),
            ("Tipo", "KindEs"),
            ("Estado", "StatusEs"),
            ("Pedido", "SalesOrder"),
            ("Líneas", "LineCount"),
            ("Total", "TotalAmount"),
            ("IVA %", "TaxRate"),
            ("SIF", "SifShort"),
            ("Fecha", "IssueDate"));
        ApplyPermissions();
    }

    private void ApplyPermissions()
    {
        var gate = _client.Permissions;
        IssueButton.IsVisible = gate.Has(AppPermissionNames.InvoiceIssue);
        RectifyButton.IsVisible = gate.Has(AppPermissionNames.InvoiceRectify);
        PdfButton.IsVisible = gate.Has(AppPermissionNames.InvoiceIssue)
            || gate.Has(AppPermissionNames.InvoicePrint);
        PrintButton.IsVisible = gate.Has(AppPermissionNames.InvoicePrint);
        PreviewPdfButton.IsVisible = PdfButton.IsVisible;
        IssuerButton.IsVisible = gate.Has(AppPermissionNames.InvoiceConfig);
        SealButton.IsVisible = gate.Has(AppPermissionNames.InvoiceConfig);
        SignatureButton.IsVisible = gate.Has(AppPermissionNames.InvoiceConfig);
        LogoButton.IsVisible = gate.Has(AppPermissionNames.InvoiceConfig);
    }

    public async Task RefreshAsync()
    {
        StatusText.Text = string.Empty;
        ApplyPermissions();
        try
        {
            await _client.Masters.RefreshAsync();
            PickerHelpers.BindParties(CustomerBox, _client.Masters.Customers);
            _salesOrders = await _client.GetSalesOrdersAsync();
            _deliveries = await _client.GetDeliveriesAsync();
            BindSalesOrdersForCustomer();
            BindDeliveriesForCustomer();
            await LoadIssuerAsync();
            _invoices = await _client.GetInvoicesAsync();
            var orderNumbers = _salesOrders.ToDictionary(order => order.Id, order => order.Number);
            var rows = _invoices.Select(invoice => new
            {
                invoice.DocumentNumber,
                KindEs = InvoiceKindToEs(invoice.Kind),
                StatusEs = OfficeUi.StatusToEs(invoice.Status),
                SalesOrder = invoice.SalesOrderId is Guid soId
                    && orderNumbers.TryGetValue(soId, out var number)
                    ? number
                    : "—",
                LineCount = invoice.Lines.Count == 0 ? "—" : invoice.Lines.Count.ToString(),
                invoice.TotalAmount,
                invoice.TaxRate,
                SifShort = string.IsNullOrWhiteSpace(invoice.SifHash)
                    ? "—"
                    : (invoice.SifHash.Length <= 12
                        ? invoice.SifHash
                        : invoice.SifHash[..12] + "…"),
                invoice.IssueDate,
            }).ToList();
            InvoicesGrid.ItemsSource = rows;
            EmptyHint.IsVisible = rows.Count == 0;
            InvoicesGrid.IsVisible = rows.Count > 0;
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            StatusText.Text = UiHelpers.ToUserMessage(exception);
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async Task LoadIssuerAsync()
    {
        if (!_client.Permissions.Has(AppPermissionNames.InvoicePrint)
            && !_client.Permissions.Has(AppPermissionNames.InvoiceConfig))
        {
            return;
        }

        var issuer = await _client.GetIssuerAsync();
        if (issuer is null)
        {
            _issuerReady = false;
            return;
        }

        _issuerReady = !string.IsNullOrWhiteSpace(issuer.LegalName)
            && !string.IsNullOrWhiteSpace(issuer.TaxId);
        IssuerNameBox.Text = issuer.LegalName;
        IssuerTaxBox.Text = issuer.TaxId;
        IssuerAddrBox.Text = issuer.AddressLine;
        IssuerCityBox.Text = issuer.City;
        IssuerPostalBox.Text = issuer.PostalCode;
        IssuerPhoneBox.Text = issuer.Phone ?? string.Empty;
        IssuerEmailBox.Text = issuer.Email ?? string.Empty;
        IssuerWebBox.Text = issuer.Website ?? string.Empty;
        SignerNameBox.Text = issuer.SignerName ?? string.Empty;
        SignerTitleBox.Text = string.IsNullOrWhiteSpace(issuer.SignerTitle)
            ? "Administrador"
            : issuer.SignerTitle;
        IbanBox.Text = issuer.Iban ?? string.Empty;
        BankBox.Text = issuer.BankName ?? string.Empty;
        PaymentMethodBox.Text = issuer.PaymentMethod ?? string.Empty;
        PaymentDueDaysBox.Text = issuer.PaymentDueDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
        IrpfBox.Text = issuer.IrpfPercent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        LegalFooterBox.Text = string.IsNullOrWhiteSpace(issuer.LegalFooterText)
            ? "Operación sujeta y no exenta de IVA. Factura emitida conforme al RD 1619/2012."
            : issuer.LegalFooterText;
        _sealRelativePath = issuer.SealPngRelativePath;
        _signatureRelativePath = issuer.SignaturePngRelativePath;
        _logoRelativePath = issuer.LogoPngRelativePath;
        SealPathText.Text = _sealRelativePath ?? string.Empty;
        SignaturePathText.Text = _signatureRelativePath ?? string.Empty;
        LogoPathText.Text = _logoRelativePath ?? string.Empty;
    }

    private void OnCustomerChanged(object? sender, SelectionChangedEventArgs e)
    {
        BindSalesOrdersForCustomer();
        BindDeliveriesForCustomer();
    }

    private void OnSalesOrderChanged(object? sender, SelectionChangedEventArgs e) =>
        SeedDraftFromSalesOrder();

    private void OnDeliveryChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_bindingDelivery)
        {
            return;
        }

        if (!PickerHelpers.TryGetId(DeliveryBox, out var deliveryId) || deliveryId == Guid.Empty)
        {
            return;
        }

        var delivery = _deliveries.FirstOrDefault(row => row.Id == deliveryId);
        if (delivery is null)
        {
            return;
        }

        _bindingDelivery = true;
        try
        {
            PickerHelpers.SelectId(CustomerBox, delivery.CustomerId);
            BindSalesOrdersForCustomer();
            PickerHelpers.SelectId(SalesOrderBox, delivery.SalesOrderId);
            SeedDraftFromSalesOrder();
            StatusText.Text =
                $"Albarán {delivery.Number}: borrador desde pedido {delivery.SalesOrderNumber ?? "—"}. Emita la factura.";
        }
        finally
        {
            _bindingDelivery = false;
        }
    }

    private void BindSalesOrdersForCustomer()
    {
        var customerId = PickerHelpers.SelectedId(CustomerBox);
        var filtered = customerId is null
            ? Array.Empty<SalesOrderRow>()
            : _salesOrders.Where(order => order.CustomerId == customerId.Value).ToArray();
        var items = new List<PickerHelpers.PickerItem>
        {
            new(Guid.Empty, "(Sin pedido — líneas libres)"),
        };
        items.AddRange(
            filtered.Select(order =>
                new PickerHelpers.PickerItem(
                    order.Id,
                    $"{order.Number} — {OfficeUi.StatusToEs(order.Status)}")));
        PickerHelpers.BindItems(SalesOrderBox, items);
        if (!_bindingDelivery)
        {
            SeedDraftFromSalesOrder();
        }
    }

    private void BindDeliveriesForCustomer()
    {
        var customerId = PickerHelpers.SelectedId(CustomerBox);
        var filtered = customerId is null
            ? Array.Empty<DeliverySummaryResponse>()
            : _deliveries.Where(row => row.CustomerId == customerId.Value).ToArray();
        var items = new List<PickerHelpers.PickerItem>
        {
            new(Guid.Empty, "(Sin albarán)"),
        };
        items.AddRange(
            filtered.Select(row =>
                new PickerHelpers.PickerItem(
                    row.Id,
                    $"{row.Number} · {row.SalesOrderNumber ?? "SO"} · {row.DocumentDate:dd/MM/yyyy}")));
        PickerHelpers.BindItems(DeliveryBox, items);
    }

    private void SeedDraftFromSalesOrder()
    {
        if (!PickerHelpers.TryGetId(SalesOrderBox, out var soId) || soId == Guid.Empty)
        {
            return;
        }

        var order = _salesOrders.FirstOrDefault(row => row.Id == soId);
        if (order is null || order.Lines.Count == 0)
        {
            return;
        }

        var materials = _client.Masters.Materials.ToDictionary(row => row.Id);
        var lines = order.Lines
            .Where(line => line.OpenQuantity > 0 || line.RequestedQuantity > 0)
            .Select(line =>
            {
                materials.TryGetValue(line.MaterialId, out var material);
                var label = material is null
                    ? line.MaterialId.ToString("D")[..8]
                    : $"{material.Code} — {material.DisplayName}";
                var qty = line.OpenQuantity > 0 ? line.OpenQuantity : line.RequestedQuantity;
                var price = material?.CatalogPrice ?? 0m;
                return new DraftLineBoard.DraftLine(
                    line.MaterialId,
                    label,
                    qty,
                    price,
                    label);
            })
            .ToList();
        if (lines.Count == 0)
        {
            return;
        }

        _draft.Replace(lines);
        _draft.Bind(DraftLinesGrid);
        StatusText.Text = $"Borrador: {lines.Count} línea(s) del pedido {order.Number}.";
    }

    private void OnAddLineClick(object? sender, RoutedEventArgs e)
    {
        if (!TryBuildDraftLine(out var line, out var error))
        {
            StatusText.Text = error;
            return;
        }

        _draft.Add(line);
        _draft.Bind(DraftLinesGrid);
        StatusText.Text = $"{_draft.Count} línea(s) en borrador.";
    }

    private void OnRemoveLineClick(object? sender, RoutedEventArgs e)
    {
        if (!_draft.RemoveAt(DraftLinesGrid.SelectedIndex))
        {
            StatusText.Text = "Seleccione una línea del borrador para quitar.";
            return;
        }

        _draft.Bind(DraftLinesGrid);
        StatusText.Text = $"{_draft.Count} línea(s) en borrador.";
    }

    private bool TryBuildDraftLine(out DraftLineBoard.DraftLine line, out string error)
    {
        line = null!;
        error = string.Empty;
        if (!UiHelpers.TryParseDecimal(QtyBox.Text, out var qty)
            || !UiHelpers.TryParseDecimal(PriceBox.Text, out var price)
            || qty <= 0
            || price < 0)
        {
            error = "Cantidad > 0 y precio ≥ 0.";
            return false;
        }

        var description = DescBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            error = "El concepto es obligatorio.";
            return false;
        }

        line = new DraftLineBoard.DraftLine(
            Guid.Empty,
            description,
            qty,
            price,
            description);
        return true;
    }

    private static string InvoiceKindToEs(string kind) =>
        kind switch
        {
            "Ordinary" or "Standard" or "Invoice" => "Ordinaria",
            "Rectifying" or "Credit" or "CreditNote" => "Rectificativa / abono",
            _ => kind,
        };

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) =>
        await RefreshAsync();

    private async void OnIssueClick(object? sender, RoutedEventArgs e)
    {
        if (!IssueButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para emitir facturas.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        if (!_issuerReady)
        {
            StatusText.Text =
                "Configure y guarde «Datos de mi empresa» (razón social y CIF) antes de emitir.";
            return;
        }

        var customerId = PickerHelpers.SelectedId(CustomerBox);
        if (customerId is null)
        {
            StatusText.Text = "Seleccione cliente.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(TaxBox.Text, out var tax) || tax < 0)
        {
            StatusText.Text = "IVA debe ser numérico ≥ 0.";
            return;
        }

        var draftLines = _draft.Lines.ToList();
        if (draftLines.Count == 0)
        {
            if (!TryBuildDraftLine(out var single, out var error))
            {
                StatusText.Text = error;
                return;
            }

            draftLines.Add(single);
        }

        Guid? salesOrderId = null;
        var selectedSo = PickerHelpers.SelectedId(SalesOrderBox);
        if (selectedSo is Guid soId && soId != Guid.Empty)
        {
            salesOrderId = soId;
        }

        try
        {
            await _client.IssueInvoiceAsync(
                owner,
                new IssueInvoiceRequestDto(
                    customerId.Value,
                    salesOrderId,
                    SeriesBox.Text?.Trim() ?? "A",
                    tax,
                    draftLines.Select(line =>
                        new IssueInvoiceLineRequestDto(
                            line.Description ?? line.Label,
                            line.Quantity,
                            line.UnitPrice)).ToArray()));
            _draft.Clear();
            _draft.Bind(DraftLinesGrid);
            await UiHelpers.ShowInfoAsync(
                this,
                $"Factura emitida ({draftLines.Count} línea(s)).");
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

    private async void OnRectifyClick(object? sender, RoutedEventArgs e)
    {
        if (!RectifyButton.IsVisible)
        {
            StatusText.Text = "No tiene permiso para rectificativas / abonos.";
            return;
        }

        var owner = UiHelpers.FindWindow(this);
        if (owner is null || !TrySelected(out var invoice))
        {
            return;
        }

        var reason = RectifyReasonBox.Text?.Trim() ?? string.Empty;
        if (reason.Length < 3)
        {
            StatusText.Text = "Indique el motivo de la rectificativa (mín. 3 caracteres).";
            return;
        }

        try
        {
            await _client.RectifyInvoiceAsync(owner, invoice.Id, reason);
            await UiHelpers.ShowInfoAsync(this, "Rectificativa / abono emitido.");
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

    private async void OnPdfClick(object? sender, RoutedEventArgs e) =>
        await DownloadOrPrintSelectedAsync(print: false);

    private async void OnPrintClick(object? sender, RoutedEventArgs e) =>
        await DownloadOrPrintSelectedAsync(print: true);

    private async void OnPreviewPdfClick(object? sender, RoutedEventArgs e)
    {
        if (!TrySelected(out _))
        {
            StatusText.Text =
                "Seleccione una factura emitida para previsualizar el PDF con el perfil de empresa.";
            return;
        }

        await DownloadOrPrintSelectedAsync(print: false);
    }

    private async Task DownloadOrPrintSelectedAsync(bool print)
    {
        if (!TrySelected(out var invoice))
        {
            return;
        }

        try
        {
            var bytes = await _client.DownloadInvoicePdfAsync(invoice.Id);
            var fileName = $"factura-{invoice.DocumentNumber.Replace('/', '-')}.pdf";
            if (print)
            {
                await PrintDocumentHelper.ShowAsync(
                    this,
                    bytes,
                    $"Imprimir factura {invoice.DocumentNumber}",
                    fileName);
            }
            else
            {
                await UiHelpers.SaveBytesAsync(this, fileName, bytes);
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnSealClick(object? sender, RoutedEventArgs e) =>
        await LoadPngAsync(
            title: "Seleccionar sello PNG",
            relativePath: "seals/issuer-seal.png",
            setBase64: value => _sealPngBase64 = value,
            setRelative: value => _sealRelativePath = value,
            pathLabel: SealPathText);

    private async void OnSignatureClick(object? sender, RoutedEventArgs e) =>
        await LoadPngAsync(
            title: "Seleccionar firma PNG",
            relativePath: "seals/issuer-signature.png",
            setBase64: value => _signaturePngBase64 = value,
            setRelative: value => _signatureRelativePath = value,
            pathLabel: SignaturePathText);

    private async void OnLogoClick(object? sender, RoutedEventArgs e) =>
        await LoadPngAsync(
            title: "Seleccionar logo PNG",
            relativePath: "seals/issuer-logo.png",
            setBase64: value => _logoPngBase64 = value,
            setRelative: value => _logoRelativePath = value,
            pathLabel: LogoPathText);

    private async Task LoadPngAsync(
        string title,
        string relativePath,
        Action<string?> setBase64,
        Action<string?> setRelative,
        TextBlock pathLabel)
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
                Title = title,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("PNG")
                    {
                        Patterns = ["*.png"],
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
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            var bytes = memory.ToArray();
            if (bytes.Length > 512 * 1024)
            {
                StatusText.Text = "La imagen supera 512 KB. Reduzca el PNG e inténtelo de nuevo.";
                return;
            }

            setBase64(Convert.ToBase64String(bytes));
            setRelative(relativePath);
            pathLabel.Text = relativePath;
            StatusText.Text = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private async void OnIssuerClick(object? sender, RoutedEventArgs e)
    {
        var owner = UiHelpers.FindWindow(this);
        if (owner is null)
        {
            return;
        }

        var legalName = IssuerNameBox.Text?.Trim() ?? string.Empty;
        var taxId = IssuerTaxBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(legalName))
        {
            StatusText.Text = "Indique la razón social / nombre de empresa.";
            return;
        }

        if (string.IsNullOrWhiteSpace(taxId))
        {
            StatusText.Text = "Indique el CIF/NIF del emisor.";
            return;
        }

        var address = IssuerAddrBox.Text?.Trim() ?? string.Empty;
        var city = IssuerCityBox.Text?.Trim() ?? string.Empty;
        var postal = IssuerPostalBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address)
            || string.IsNullOrWhiteSpace(city)
            || string.IsNullOrWhiteSpace(postal))
        {
            StatusText.Text = "Indique dirección, código postal y ciudad del emisor.";
            return;
        }

        if (!int.TryParse(
                PaymentDueDaysBox.Text?.Trim(),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var dueDays)
            || dueDays < 0)
        {
            StatusText.Text = "El plazo de pago (días) debe ser un entero ≥ 0.";
            return;
        }

        if (!UiHelpers.TryParseDecimal(IrpfBox.Text, out var irpf) || irpf < 0 || irpf > 100)
        {
            StatusText.Text = "IRPF % debe ser un número entre 0 y 100 (0 = no mostrar en PDF).";
            return;
        }

        try
        {
            await _client.UpsertIssuerAsync(
                owner,
                new UpsertIssuerRequestDto(
                    legalName,
                    taxId,
                    address,
                    city,
                    postal,
                    "ES",
                    _sealRelativePath,
                    _sealPngBase64,
                    IssuerPhoneBox.Text?.Trim(),
                    IssuerEmailBox.Text?.Trim(),
                    IssuerWebBox.Text?.Trim(),
                    SignerNameBox.Text?.Trim(),
                    SignerTitleBox.Text?.Trim(),
                    IbanBox.Text?.Trim(),
                    BankBox.Text?.Trim(),
                    PaymentMethodBox.Text?.Trim(),
                    dueDays,
                    LegalFooterBox.Text?.Trim(),
                    irpf,
                    _signatureRelativePath,
                    _signaturePngBase64,
                    _logoRelativePath,
                    _logoPngBase64));
            _sealPngBase64 = null;
            _signaturePngBase64 = null;
            _logoPngBase64 = null;
            await LoadIssuerAsync();
            await UiHelpers.ShowInfoAsync(
                this,
                "Datos de mi empresa guardados. Saldrán en el PDF de factura (cabecera, pago, firma y pie).");
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            await UiHelpers.ShowErrorAsync(this, exception);
        }
    }

    private bool TrySelected(out InvoiceResponse invoice)
    {
        invoice = null!;
        if (InvoicesGrid.SelectedIndex < 0 || InvoicesGrid.SelectedIndex >= _invoices.Count)
        {
            StatusText.Text = "Seleccione una factura.";
            return false;
        }

        invoice = _invoices[InvoicesGrid.SelectedIndex];
        return true;
    }
}
