using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AAALogistics.Client.Services;
using AAALogistics.Contracts;

namespace AAALogistics.Client.Views;

/// <summary>Vistas Studio P1–P11. Cada fase sustituye el cuerpo vacío por UI operativa.</summary>
internal abstract class StudioModuleViewBase : UserControl
{
    private readonly TextBlock _status = new() { Classes = { "error" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _empty = new()
    {
        Classes = { "muted" },
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        MaxWidth = 480,
    };

    private readonly StackPanel _body = new() { Spacing = 10 };
    private readonly DataGrid _grid = new() { AutoGenerateColumns = false, IsVisible = false };

    internal LogisticsApiClient Client { get; }

    internal TextBlock StatusText => _status;

    internal TextBlock EmptyHint => _empty;

    internal DataGrid Grid => _grid;

    internal StackPanel Body => _body;

    internal string ListMode { get; set; } = string.Empty;

    internal object? UiState { get; set; }

    protected StudioModuleViewBase(
        LogisticsApiClient client,
        string title,
        string helpKey,
        string emptyHint)
    {
        Client = client;
        var titleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        titleRow.Children.Add(new TextBlock
        {
            Text = title,
            Classes = { "page-title" },
            VerticalAlignment = VerticalAlignment.Center,
        });
        OfficeUi.AttachInfoHelp(titleRow, helpKey);
        _empty.Text = emptyHint;

        var root = new DockPanel();
        var top = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(0, 0, 0, 8) };
        top.Children.Add(titleRow);
        top.Children.Add(_body);
        top.Children.Add(_empty);
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);
        _status.Margin = new Avalonia.Thickness(0, 8, 0, 0);
        root.Children.Add(top);
        root.Children.Add(_status);
        root.Children.Add(_grid);
        Content = root;
    }

    internal void SetEmptyVisible(bool empty)
    {
        _empty.IsVisible = empty;
        _grid.IsVisible = !empty;
    }

    public abstract Task RefreshAsync();
}

internal sealed class PricingView : StudioModuleViewBase
{
    public PricingView(LogisticsApiClient client)
        : base(
            client,
            "Precios / IVA / crédito / RMA",
            OfficeHelpTexts.Keys.StudioPricing,
            "Sin condiciones ni impuestos aún. Use Nuevo IVA / Nueva condición / Crédito / Nuevo RMA. Elija cliente y material en los desplegables.")
    {
        StudioModuleViewsBootstrap.BuildPricing(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshPricingAsync(this);
}

internal sealed class MmAdvancedView : StudioModuleViewBase
{
    public MmAdvancedView(LogisticsApiClient client)
        : base(
            client,
            "Compras MM avanzado",
            OfficeHelpTexts.Keys.StudioMm,
            "Sin contratos, RFQ, valoraciones ni facturas proveedor. Cree un contrato, RFQ, valoración o factura.")
    {
        StudioModuleViewsBootstrap.BuildMm(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshMmAsync(this);
}

internal sealed class FinanceView : StudioModuleViewBase
{
    public FinanceView(LogisticsApiClient client)
        : base(
            client,
            "Finanzas FI/CO lite",
            OfficeHelpTexts.Keys.StudioFinance,
            "Sin cuentas ni asientos. Siembre el plan ES, cree un centro de coste o contabilice un asiento en partida doble.")
    {
        StudioModuleViewsBootstrap.BuildFinance(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshFinanceAsync(this);
}

internal sealed class ProductionView : StudioModuleViewBase
{
    public ProductionView(LogisticsApiClient client)
        : base(
            client,
            "Producción PP lite",
            OfficeHelpTexts.Keys.StudioPp,
            "Sin BOM ni órdenes de fabricación. Cree una lista de materiales o una OF.")
    {
        StudioModuleViewsBootstrap.BuildPp(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshPpAsync(this);
}

internal sealed class QualityView : StudioModuleViewBase
{
    public QualityView(LogisticsApiClient client)
        : base(
            client,
            "Calidad QM",
            OfficeHelpTexts.Keys.StudioQm,
            "Sin planes ni holds. Cree un plan o un hold de lote (informativo; use Inventario para bloquear stock).")
    {
        StudioModuleViewsBootstrap.BuildQm(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshQmAsync(this);
}

internal sealed class OrgContextView : StudioModuleViewBase
{
    public OrgContextView(LogisticsApiClient client)
        : base(
            client,
            "Sociedad / planta",
            OfficeHelpTexts.Keys.StudioOrg,
            "Sin plantas. Cree una planta, vincule almacén y fije contexto (vacío = vista global admin).")
    {
        StudioModuleViewsBootstrap.BuildOrg(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshOrgAsync(this);
}

internal sealed class WorkflowView : StudioModuleViewBase
{
    public WorkflowView(LogisticsApiClient client)
        : base(
            client,
            "Aprobaciones",
            OfficeHelpTexts.Keys.StudioWorkflow,
            "Cola de aprobaciones vacía. Defina una regla o espere peticiones pendientes.")
    {
        StudioModuleViewsBootstrap.BuildWorkflow(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshWorkflowAsync(this);
}

internal sealed class IntegrationHubView : StudioModuleViewBase
{
    public IntegrationHubView(LogisticsApiClient client)
        : base(
            client,
            "Intercambio de datos",
            OfficeHelpTexts.Keys.StudioIntHub,
            "Sin conectores ni mensajes. Registre un conector, reciba un mensaje en la bandeja o guarde un mapeo externo↔interno.")
    {
        StudioModuleViewsBootstrap.BuildIntHub(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshIntHubAsync(this);
}

internal sealed class BiDashboardView : StudioModuleViewBase
{
    public BiDashboardView(LogisticsApiClient client)
        : base(
            client,
            "Reporting / BI",
            OfficeHelpTexts.Keys.StudioBi,
            "Sin filas BI aún. Calcule stock valorado, aging u OTIF.")
    {
        StudioModuleViewsBootstrap.BuildBi(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshBiAsync(this);
}

internal sealed class HrLiteView : StudioModuleViewBase
{
    public HrLiteView(LogisticsApiClient client)
        : base(
            client,
            "RRHH lite",
            OfficeHelpTexts.Keys.StudioHr,
            "Sin fichas de empleado. Cree o actualice una ficha ligada a una cuenta existente (sin nómina).")
    {
        StudioModuleViewsBootstrap.BuildHr(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshHrAsync(this);
}

internal sealed class ExtensibilityView : StudioModuleViewBase
{
    public ExtensibilityView(LogisticsApiClient client)
        : base(
            client,
            "Extensibilidad",
            OfficeHelpTexts.Keys.StudioExt,
            "Sin hooks ni transportes. Registre un plugin, pruébelo o exporte un transporte de configuración.")
    {
        StudioModuleViewsBootstrap.BuildExt(this);
    }

    public override Task RefreshAsync() => StudioModuleViewsBootstrap.RefreshExtAsync(this);
}

/// <summary>Bootstrap UI/API de módulos Studio P1–P11.</summary>
internal static class StudioModuleViewsBootstrap
{
    private static StackPanel ButtonRow(params Control[] children)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        foreach (var child in children)
        {
            row.Children.Add(child);
        }

        return row;
    }

    private static Button Primary(string label) =>
        new() { Content = label, Classes = { "primary" }, MinHeight = 32 };

    private static void Wire(
        StudioModuleViewBase view,
        Button button,
        Func<Task> action)
    {
        button.Click += async (_, _) =>
        {
            view.StatusText.Text = string.Empty;
            try
            {
                await action();
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
                view.StatusText.Text = UiHelpers.ToUserMessage(exception);
                await UiHelpers.ShowErrorAsync(view, exception);
            }
        };
    }

    private static void SetList<T>(
        StudioModuleViewBase view,
        IReadOnlyList<T> rows,
        params (string header, string property)[] columns)
    {
        OfficeUi.SetTextColumns(view.Grid, columns);
        view.Grid.ItemsSource = rows;
        view.SetEmptyVisible(rows.Count == 0);
    }

    private static Guid? OptionalGuid(string? text) =>
        UiHelpers.TryParseGuid(text, out var value) ? value : null;

    private static Guid? OptionalId(ComboBox box) => PickerHelpers.SelectedId(box);

    private static async Task BindMastersAsync(StudioModuleViewBase view)
    {
        await view.Client.Masters.RefreshAsync();
    }

  #region P1 Pricing

    private sealed class PricingFields
    {
        public ComboBox CustomerId = PickerHelpers.Create();
        public ComboBox MaterialId = PickerHelpers.Create();
        public TextBox LineQty = OfficeUi.Box("Cantidad > 0", 120);
        public TextBox TaxCode = OfficeUi.Box("Ej. ES21", 120);
        public TextBox TaxName = OfficeUi.Box("Ej. IVA 21%", 160);
        public TextBox TaxRate = OfficeUi.Box("Ej. 21", 80);
        public TextBox PriceCode = OfficeUi.Box("Ej. PC-DEMO", 120);
        public TextBox PriceName = OfficeUi.Box("Nombre condición", 160);
    }

    public static void BuildPricing(PricingView view)
    {
        var fields = new PricingFields
        {
            TaxCode = { Text = "ES21" },
            TaxName = { Text = "IVA 21%" },
            TaxRate = { Text = "21" },
            PriceCode = { Text = "PC-DEMO" },
            PriceName = { Text = "Condición demo" },
        };
        view.UiState = fields;
        view.ListMode = "tax";

        var listTax = Primary("Listar IVA");
        var newTax = Primary("Nuevo IVA");
        var listPrice = Primary("Listar condiciones");
        var newPrice = Primary("Nueva condición");
        var listCredit = Primary("Crédito clientes");
        var listRma = Primary("Listar RMA");
        var newRma = Primary("Nuevo RMA");

        Wire(view, listTax, async () =>
        {
            view.ListMode = "tax";
            await RefreshPricingAsync(view);
        });
        Wire(view, newTax, async () =>
        {
            if (!UiHelpers.TryParseDecimal(fields.TaxRate.Text, out var rate))
            {
                view.StatusText.Text = "Indique el tipo impositivo.";
                return;
            }

            await view.Client.CreateStudioTaxCodeAsync(
                new CreateTaxCodeRequestDto(
                    fields.TaxCode.Text?.Trim() ?? "ES21",
                    fields.TaxName.Text?.Trim() ?? "IVA 21%",
                    rate,
                    "ES"));
            view.ListMode = "tax";
            view.StatusText.Text = "IVA creado.";
            await RefreshPricingAsync(view);
        });
        Wire(view, listPrice, async () =>
        {
            view.ListMode = "price";
            await RefreshPricingAsync(view);
        });
        Wire(view, newPrice, async () =>
        {
            await view.Client.CreateStudioPriceConditionAsync(
                new CreatePriceConditionRequestDto(
                    fields.PriceCode.Text?.Trim() ?? "PC-DEMO",
                    fields.PriceName.Text?.Trim() ?? "Condición demo",
                    0,
                    0,
                    null,
                    OptionalId(fields.CustomerId),
                    DateOnly.FromDateTime(DateTime.Today),
                    null));
            view.ListMode = "price";
            view.StatusText.Text = "Condición creada.";
            await RefreshPricingAsync(view);
        });
        Wire(view, listCredit, async () =>
        {
            view.ListMode = "credit";
            await RefreshPricingAsync(view);
        });
        Wire(view, listRma, async () =>
        {
            view.ListMode = "rma";
            await RefreshPricingAsync(view);
        });
        Wire(view, newRma, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.CustomerId, out var customerId))
            {
                view.StatusText.Text = "Seleccione un cliente en el desplegable.";
                return;
            }

            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material para la línea RMA.";
                return;
            }

            if (!UiHelpers.TryParseDecimal(fields.LineQty.Text, out var qty) || qty <= 0)
            {
                view.StatusText.Text = "Indique cantidad positiva de la línea RMA.";
                return;
            }

            await view.Client.CreateStudioRmaAsync(
                new CreateRmaRequestDto(
                    customerId,
                    null,
                    "Devolución demo",
                    [new CreateRmaLineRequestDto(materialId, qty, "UD")]));
            view.ListMode = "rma";
            view.StatusText.Text = "RMA creado.";
            await RefreshPricingAsync(view);
        });

        view.Body.Children.Add(ButtonRow(
            listTax,
            newTax,
            listPrice,
            newPrice,
            listCredit,
            listRma,
            newRma));
        view.Body.Children.Add(OfficeUi.LabeledField("Código IVA", fields.TaxCode, 150, OfficeHelpTexts.Keys.FieldTaxCode));
        view.Body.Children.Add(OfficeUi.LabeledField("Nombre IVA", fields.TaxName, 150, OfficeHelpTexts.Keys.FieldTaxCode));
        view.Body.Children.Add(OfficeUi.LabeledField("% IVA", fields.TaxRate, 150, OfficeHelpTexts.Keys.FieldTaxCode));
        view.Body.Children.Add(OfficeUi.LabeledField("Cód. condición", fields.PriceCode, 150, OfficeHelpTexts.Keys.FieldPriceCondition));
        view.Body.Children.Add(OfficeUi.LabeledField("Nombre condición", fields.PriceName, 150, OfficeHelpTexts.Keys.FieldPriceCondition));
        view.Body.Children.Add(OfficeUi.LabeledField("Cliente", fields.CustomerId, 150, OfficeHelpTexts.Keys.FieldCustomerGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Material", fields.MaterialId, 150, OfficeHelpTexts.Keys.FieldMaterialGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Cantidad RMA (devolución)", fields.LineQty, 150, OfficeHelpTexts.Keys.FieldRmaQty));
    }

    public static async Task RefreshPricingAsync(PricingView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            await BindMastersAsync(view);
            if (view.UiState is PricingFields pricingFields)
            {
                PickerHelpers.BindParties(pricingFields.CustomerId, view.Client.Masters.Customers);
                PickerHelpers.BindMaterials(pricingFields.MaterialId, view.Client.Masters.Materials);
            }

            switch (view.ListMode)
            {
                case "price":
                    var prices = await view.Client.ListStudioPriceConditionsAsync();
                    SetList(
                        view,
                        prices,
                        ("Código", "Code"),
                        ("Nombre", "Name"),
                        ("%", "PercentOff"),
                        ("Importe", "AmountOff"),
                        ("Activo", "IsActive"));
                    break;
                case "credit":
                    var credit = await view.Client.ListStudioCreditSummaryAsync();
                    var creditRows = credit.Select(row => new
                    {
                        Cliente = ResolvePartyLabel(view, row.CustomerId, suppliers: false),
                        row.CreditLimit,
                        row.OpenReceivables,
                        row.Currency,
                        Bloqueado = row.IsCreditBlocked ? "Sí" : "No",
                    }).ToList();
                    SetList(
                        view,
                        creditRows,
                        ("Cliente", "Cliente"),
                        ("Límite", "CreditLimit"),
                        ("Abierto", "OpenReceivables"),
                        ("Moneda", "Currency"),
                        ("Bloqueado", "Bloqueado"));
                    break;
                case "rma":
                    var rmas = await view.Client.ListStudioRmasAsync();
                    var rmaRows = rmas.Select(row => new
                    {
                        row.Number,
                        Cliente = ResolvePartyLabel(view, row.CustomerId, suppliers: false),
                        Motivo = row.Reason,
                        Estado = row.Status,
                    }).ToList();
                    SetList(
                        view,
                        rmaRows,
                        ("Número", "Number"),
                        ("Cliente", "Cliente"),
                        ("Motivo", "Motivo"),
                        ("Estado", "Estado"));
                    break;
                default:
                    view.ListMode = "tax";
                    var taxes = await view.Client.ListStudioTaxCodesAsync();
                    SetList(
                        view,
                        taxes,
                        ("Código", "Code"),
                        ("Nombre", "Name"),
                        ("%", "RatePercent"),
                        ("País", "CountryCode"),
                        ("Activo", "IsActive"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P2 MM

    private sealed class MmFields
    {
        public ComboBox SupplierId = PickerHelpers.Create();
        public ComboBox PoId = PickerHelpers.Create();
        public ComboBox MaterialId = PickerHelpers.Create();
        public ComboBox WarehouseId = PickerHelpers.Create();
        public ComboBox InvoiceId = PickerHelpers.Create();
        public ComboBox ValMethod = CreateValuationMethodBox();
        public TextBox ValPrice = OfficeUi.Box("Valor unitario", 120);
        public TextBox InvoiceAmount = OfficeUi.Box("Importe > 0", 140);
    }

    private static ComboBox CreateValuationMethodBox()
    {
        var box = PickerHelpers.Create(220);
        box.ItemsSource = new[]
        {
            new PickerHelpers.PickerItem(Guid.Parse("00000000-0000-0000-0000-000000000000"), "0 — Precio estándar"),
            new PickerHelpers.PickerItem(Guid.Parse("00000000-0000-0000-0000-000000000001"), "1 — Media móvil"),
        };
        box.DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PickerHelpers.PickerItem.Label));
        box.SelectedIndex = 0;
        return box;
    }

    private static int SelectedValuationMethod(ComboBox box) =>
        box.SelectedIndex is 0 or 1 ? box.SelectedIndex : -1;

    public static void BuildMm(MmAdvancedView view)
    {
        var fields = new MmFields
        {
            ValPrice = { Text = "10" },
            InvoiceAmount = { Text = "100" },
        };
        view.UiState = fields;
        view.ListMode = "contracts";

        var listContracts = Primary("Listar contratos");
        var newContract = Primary("Nuevo contrato");
        var listRfq = Primary("Listar RFQ");
        var newRfq = Primary("Nueva RFQ");
        var listMatch = Primary("Listar 3-way");
        var newMatch = Primary("Nuevo 3-way");
        var listVal = Primary("Valoraciones");
        var upsertVal = Primary("Guardar valoración");
        var listInv = Primary("Facturas proveedor");
        var newInv = Primary("Nueva factura proveedor");

        Wire(view, listContracts, async () =>
        {
            view.ListMode = "contracts";
            await RefreshMmAsync(view);
        });
        Wire(view, newContract, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.SupplierId, out var supplierId))
            {
                view.StatusText.Text = "Seleccione un proveedor.";
                return;
            }

            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material para la línea del contrato.";
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            await view.Client.CreateStudioFrameworkContractAsync(
                new CreateFrameworkContractRequestDto(
                    supplierId,
                    today,
                    today.AddYears(1),
                    "EUR",
                    [new CreateFrameworkContractLineRequestDto(materialId, 100, 1, "UD")]));
            view.ListMode = "contracts";
            view.StatusText.Text = "Contrato marco creado.";
            await RefreshMmAsync(view);
        });
        Wire(view, listRfq, async () =>
        {
            view.ListMode = "rfq";
            await RefreshMmAsync(view);
        });
        Wire(view, newRfq, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material para la solicitud de oferta (RFQ).";
                return;
            }

            await view.Client.CreateStudioRfqAsync(
                new CreateRfqRequestDto(
                    "RFQ Studio demo",
                    DateOnly.FromDateTime(DateTime.Today.AddDays(7)),
                    [new CreateRfqLineRequestDto(materialId, 10, "UD")]));
            view.ListMode = "rfq";
            view.StatusText.Text = "RFQ creada.";
            await RefreshMmAsync(view);
        });
        Wire(view, listMatch, async () =>
        {
            view.ListMode = "match";
            await RefreshMmAsync(view);
        });
        Wire(view, newMatch, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.PoId, out var poId))
            {
                view.StatusText.Text = "Seleccione un pedido de compra.";
                return;
            }

            Guid? invoiceId = PickerHelpers.TryGetId(fields.InvoiceId, out var inv) && inv != Guid.Empty
                ? inv
                : null;
            var invoiceAmount = 0m;
            if (invoiceId is null
                && UiHelpers.TryParseDecimal(fields.InvoiceAmount.Text, out var typed)
                && typed > 0)
            {
                invoiceAmount = typed;
            }

            var match = await view.Client.CreateStudioThreeWayMatchAsync(
                new CreateThreeWayMatchRequestDto(
                    poId,
                    null,
                    invoiceId,
                    0,
                    0,
                    invoiceAmount,
                    "3-way Studio (importes PO/GR calculados en servidor)"));
            view.ListMode = "match";
            view.StatusText.Text = match.Matched
                ? "Conciliación 3-way OK (PO = GR = factura)."
                : $"Conciliación registrada con diferencias (PO {match.PoAmount:0.##} / GR {match.GrAmount:0.##} / Fact {match.InvoiceAmount:0.##}).";
            await RefreshMmAsync(view);
        });
        Wire(view, listVal, async () =>
        {
            view.ListMode = "valuations";
            await RefreshMmAsync(view);
        });
        Wire(view, upsertVal, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material para la valoración.";
                return;
            }

            var method = SelectedValuationMethod(fields.ValMethod);
            if (method < 0)
            {
                view.StatusText.Text = "Seleccione el método de valoración (estándar o media).";
                return;
            }

            if (!UiHelpers.TryParseDecimal(fields.ValPrice.Text, out var price) || price < 0)
            {
                view.StatusText.Text = "Indique un valor unitario válido (≥ 0).";
                return;
            }

            await view.Client.UpsertStudioMaterialValuationAsync(
                new UpsertMaterialValuationRequestDto(
                    materialId,
                    OptionalId(fields.WarehouseId),
                    method,
                    method == 0 ? price : 0m,
                    method == 1 ? price : 0m,
                    "EUR"));
            view.ListMode = "valuations";
            view.StatusText.Text = "Valoración guardada.";
            await RefreshMmAsync(view);
        });
        Wire(view, listInv, async () =>
        {
            view.ListMode = "invoices";
            await RefreshMmAsync(view);
        });
        Wire(view, newInv, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.SupplierId, out var supplierId))
            {
                view.StatusText.Text = "Seleccione el proveedor de la factura.";
                return;
            }

            if (!UiHelpers.TryParseDecimal(fields.InvoiceAmount.Text, out var amount) || amount <= 0)
            {
                view.StatusText.Text = "Indique un importe de factura mayor que cero.";
                return;
            }

            await view.Client.CreateStudioSupplierInvoiceAsync(
                new CreateSupplierInvoiceStubRequestDto(
                    supplierId,
                    OptionalId(fields.PoId),
                    amount,
                    "EUR",
                    DateOnly.FromDateTime(DateTime.Today)));
            view.ListMode = "invoices";
            view.StatusText.Text = "Factura proveedor creada.";
            await RefreshMmAsync(view);
        });

        view.Body.Children.Add(ButtonRow(
            listContracts,
            newContract,
            listRfq,
            newRfq,
            OfficeUi.WithHelp(listMatch, OfficeHelpTexts.Keys.FieldThreeWayMatch),
            OfficeUi.WithHelp(newMatch, OfficeHelpTexts.Keys.FieldThreeWayMatch)));
        view.Body.Children.Add(ButtonRow(
            listVal,
            upsertVal,
            listInv,
            OfficeUi.WithHelp(newInv, OfficeHelpTexts.Keys.FieldSupplierInvoice)));
        view.Body.Children.Add(OfficeUi.LabeledField("Proveedor", fields.SupplierId, 160, OfficeHelpTexts.Keys.FieldSupplierGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Pedido de compra", fields.PoId, 160, OfficeHelpTexts.Keys.FieldPoGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Factura proveedor (3-way)", fields.InvoiceId, 160, OfficeHelpTexts.Keys.FieldThreeWayMatch));
        view.Body.Children.Add(OfficeUi.LabeledField("Material", fields.MaterialId, 160, OfficeHelpTexts.Keys.FieldMaterialGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Almacén (opcional)", fields.WarehouseId, 160, OfficeHelpTexts.Keys.FieldMrpWarehouse));
        view.Body.Children.Add(OfficeUi.LabeledField("Método valoración", fields.ValMethod, 160, OfficeHelpTexts.Keys.FieldValuationMethod));
        view.Body.Children.Add(OfficeUi.LabeledField("Valor unitario", fields.ValPrice, 160, OfficeHelpTexts.Keys.FieldValuationMethod));
        view.Body.Children.Add(OfficeUi.LabeledField("Importe factura prov.", fields.InvoiceAmount, 160, OfficeHelpTexts.Keys.FieldSupplierInvoice));
    }

    public static async Task RefreshMmAsync(MmAdvancedView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            await BindMastersAsync(view);
            if (view.UiState is MmFields mmFields)
            {
                PickerHelpers.BindParties(mmFields.SupplierId, view.Client.Masters.Suppliers);
                PickerHelpers.BindMaterials(mmFields.MaterialId, view.Client.Masters.Materials);
                PickerHelpers.BindWarehouses(mmFields.WarehouseId, view.Client.Masters.Warehouses);
                var pos = await view.Client.GetPurchaseOrdersAsync();
                PickerHelpers.BindPurchaseOrders(mmFields.PoId, pos);
                var invoices = await view.Client.ListStudioSupplierInvoicesAsync();
                PickerHelpers.BindItems(
                    mmFields.InvoiceId,
                    new[] { new PickerHelpers.PickerItem(Guid.Empty, "(sin factura — use importe)") }
                        .Concat(invoices.Select(inv =>
                            new PickerHelpers.PickerItem(
                                inv.Id,
                                $"{inv.Number} · {inv.Amount:0.##} {inv.Currency}"))));
            }

            switch (view.ListMode)
            {
                case "rfq":
                    var rfqs = await view.Client.ListStudioRfqsAsync();
                    SetList(view, rfqs, ("Número", "Number"), ("Título", "Title"), ("Estado", "Status"));
                    break;
                case "match":
                    var matches = await view.Client.ListStudioThreeWayMatchesAsync();
                    var poMap = (await view.Client.GetPurchaseOrdersAsync())
                        .ToDictionary(po => po.Id, po => po.Number);
                    var matchRows = matches.Select(row => new
                    {
                        PO = poMap.TryGetValue(row.PurchaseOrderId, out var num)
                            ? num
                            : row.PurchaseOrderId.ToString("D")[..8] + "…",
                        PoEur = row.PoAmount,
                        GrEur = row.GrAmount,
                        FacturaEur = row.InvoiceAmount,
                        OK = row.Matched ? "Sí" : "No",
                    }).ToList();
                    SetList(
                        view,
                        matchRows,
                        ("PO", "PO"),
                        ("PO €", "PoEur"),
                        ("GR €", "GrEur"),
                        ("Factura €", "FacturaEur"),
                        ("OK", "OK"));
                    break;
                case "valuations":
                    var vals = await view.Client.ListStudioMaterialValuationsAsync();
                    var valRows = vals.Select(row => new
                    {
                        Material = ResolveMaterialLabel(view, row.MaterialId),
                        Metodo = row.Method switch
                        {
                            "0" or "Standard" or "Estándar" => "Estándar",
                            "1" or "MovingAverage" or "Media" => "Media",
                            _ => row.Method,
                        },
                        Valor = row.UnitValue,
                        row.Currency,
                    }).ToList();
                    SetList(
                        view,
                        valRows,
                        ("Material", "Material"),
                        ("Método", "Metodo"),
                        ("Valor", "Valor"),
                        ("Moneda", "Currency"));
                    break;
                case "invoices":
                    var invoices = await view.Client.ListStudioSupplierInvoicesAsync();
                    var invoicePoMap = (await view.Client.GetPurchaseOrdersAsync())
                        .ToDictionary(po => po.Id, po => po.Number);
                    var invoiceRows = invoices.Select(row => new
                    {
                        row.Number,
                        Proveedor = ResolvePartyLabel(view, row.SupplierId, suppliers: true),
                        PO = row.PurchaseOrderId is Guid poId
                            && invoicePoMap.TryGetValue(poId, out var poNum)
                            ? poNum
                            : "—",
                        Importe = row.Amount,
                        Fecha = row.InvoiceDate,
                    }).ToList();
                    SetList(
                        view,
                        invoiceRows,
                        ("Número", "Number"),
                        ("Proveedor", "Proveedor"),
                        ("PO", "PO"),
                        ("Importe", "Importe"),
                        ("Fecha", "Fecha"));
                    break;
                default:
                    view.ListMode = "contracts";
                    var contracts = await view.Client.ListStudioFrameworkContractsAsync();
                    var contractRows = contracts.Select(row => new
                    {
                        row.Number,
                        Proveedor = ResolvePartyLabel(view, row.SupplierId, suppliers: true),
                        Estado = row.Status,
                        row.Currency,
                    }).ToList();
                    SetList(
                        view,
                        contractRows,
                        ("Número", "Number"),
                        ("Proveedor", "Proveedor"),
                        ("Estado", "Estado"),
                        ("Moneda", "Currency"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P3 Finance

    private sealed class FinanceFields
    {
        public TextBox CcCode = OfficeUi.Box("Ej. CC01", 140);
        public TextBox CcName = OfficeUi.Box("Nombre centro", 200);
        public TextBox JeSource = OfficeUi.Box("Ej. MANUAL", 140);
        public TextBox JeDesc = OfficeUi.Box("Descripción", 220);
        public ComboBox Line1Account = PickerHelpers.Create();
        public TextBox Line1Debit = OfficeUi.Box("Importe debe", 100);
        public ComboBox Line2Account = PickerHelpers.Create();
        public TextBox Line2Credit = OfficeUi.Box("Importe haber", 100);
        public ComboBox SupplierInvoiceId = PickerHelpers.Create();
    }

    public static void BuildFinance(FinanceView view)
    {
        var fields = new FinanceFields
        {
            CcCode = { Text = "CC01" },
            CcName = { Text = "Centro demo" },
            JeSource = { Text = "MANUAL" },
            JeDesc = { Text = "Asiento Studio demo" },
            Line1Debit = { Text = "100" },
            Line2Credit = { Text = "100" },
        };
        view.UiState = fields;
        view.ListMode = "accounts";

        var seed = Primary("Sembrar CoA ES");
        var listAccounts = Primary("Listar cuentas");
        var listCc = Primary("Centros de coste");
        var newCc = Primary("Nuevo centro");
        var listJournal = Primary("Asientos");
        var postJournal = Primary("Contabilizar asiento");
        var fromInvoice = Primary("Asiento desde factura proveedor");
        var recon = Primary("Conciliación");

        Wire(view, seed, async () =>
        {
            var count = await view.Client.SeedStudioSpainCoaAsync();
            view.StatusText.Text = $"Plan ES sembrado ({count} cuentas).";
            view.ListMode = "accounts";
            await RefreshFinanceAsync(view);
        });
        Wire(view, listAccounts, async () =>
        {
            view.ListMode = "accounts";
            await RefreshFinanceAsync(view);
        });
        Wire(view, listCc, async () =>
        {
            view.ListMode = "costcenters";
            await RefreshFinanceAsync(view);
        });
        Wire(view, newCc, async () =>
        {
            await view.Client.CreateStudioCostCenterAsync(
                new CreateCostCenterRequestDto(
                    fields.CcCode.Text?.Trim() ?? "CC01",
                    fields.CcName.Text?.Trim() ?? "Centro demo",
                    null));
            view.ListMode = "costcenters";
            view.StatusText.Text = "Centro de coste creado.";
            await RefreshFinanceAsync(view);
        });
        Wire(view, listJournal, async () =>
        {
            view.ListMode = "journals";
            await RefreshFinanceAsync(view);
        });
        Wire(view, fromInvoice, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.SupplierInvoiceId, out var invoiceId)
                || invoiceId == Guid.Empty)
            {
                view.StatusText.Text = "Seleccione una factura proveedor (Studio → Compras MM).";
                return;
            }

            var entry = await view.Client.PostStudioJournalFromSupplierInvoiceAsync(invoiceId);
            view.ListMode = "journals";
            view.StatusText.Text = $"Asiento {entry.Number} desde factura (600 Debe / 400 Haber).";
            await RefreshFinanceAsync(view);
        });
        Wire(view, postJournal, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.Line1Account, out var account1)
                || !PickerHelpers.TryGetId(fields.Line2Account, out var account2))
            {
                view.StatusText.Text = "Seleccione cuenta Debe y cuenta Haber en los desplegables.";
                return;
            }

            if (!UiHelpers.TryParseDecimal(fields.Line1Debit.Text, out var debit) || debit <= 0)
            {
                view.StatusText.Text = "La línea 1 debe tener un importe Debe mayor que cero.";
                return;
            }

            if (!UiHelpers.TryParseDecimal(fields.Line2Credit.Text, out var credit) || credit <= 0)
            {
                view.StatusText.Text = "La línea 2 debe tener un importe Haber mayor que cero.";
                return;
            }

            if (Math.Abs(debit - credit) > 0.0001m)
            {
                view.StatusText.Text = "Partida doble: Debe y Haber deben cuadrar antes de contabilizar.";
                return;
            }

            var posted = await view.Client.PostStudioJournalAsync(
                new PostJournalEntryRequestDto(
                    fields.JeSource.Text?.Trim() ?? "MANUAL",
                    fields.JeDesc.Text?.Trim() ?? "Asiento Studio",
                    DateOnly.FromDateTime(DateTime.Today),
                    [
                        new PostJournalLineRequestDto(account1, null, debit, 0m),
                        new PostJournalLineRequestDto(account2, null, 0m, credit),
                    ]));
            view.ListMode = "journals";
            view.StatusText.Text = $"Asiento contabilizado: {posted.Number}.";
            await RefreshFinanceAsync(view);
        });
        Wire(view, recon, async () =>
        {
            var summary = await view.Client.GetStudioJournalReconciliationAsync();
            view.SetEmptyVisible(true);
            view.Grid.ItemsSource = null;
            view.StatusText.Text =
                $"Conciliación: débito {summary.TotalDebit}, crédito {summary.TotalCredit}, " +
                $"diferencia {summary.Difference}, asientos {summary.EntryCount}, " +
                $"equilibrado: {(summary.IsBalanced ? "sí" : "no")}.";
        });

        view.Body.Children.Add(ButtonRow(
            seed,
            listAccounts,
            listCc,
            newCc,
            listJournal,
            OfficeUi.WithHelp(postJournal, OfficeHelpTexts.Keys.FieldJournalDoubleEntry),
            OfficeUi.WithHelp(fromInvoice, OfficeHelpTexts.Keys.FieldSupplierInvoice),
            recon));
        view.Body.Children.Add(OfficeUi.LabeledField("Cód. centro coste", fields.CcCode, 160));
        view.Body.Children.Add(OfficeUi.LabeledField("Nombre centro", fields.CcName, 160));
        view.Body.Children.Add(OfficeUi.LabeledField("Origen asiento", fields.JeSource, 160, OfficeHelpTexts.Keys.FieldJournalSource));
        view.Body.Children.Add(OfficeUi.LabeledField("Descripción", fields.JeDesc, 160));
        view.Body.Children.Add(OfficeUi.LabeledField("Cuenta debe", fields.Line1Account, 160, OfficeHelpTexts.Keys.FieldGlAccount));
        view.Body.Children.Add(OfficeUi.LabeledField("Importe debe", fields.Line1Debit, 160, OfficeHelpTexts.Keys.FieldJournalDoubleEntry));
        view.Body.Children.Add(OfficeUi.LabeledField("Cuenta haber", fields.Line2Account, 160, OfficeHelpTexts.Keys.FieldGlAccount));
        view.Body.Children.Add(OfficeUi.LabeledField("Importe haber", fields.Line2Credit, 160, OfficeHelpTexts.Keys.FieldJournalDoubleEntry));
        view.Body.Children.Add(OfficeUi.LabeledField("Factura proveedor", fields.SupplierInvoiceId, 160, OfficeHelpTexts.Keys.FieldSupplierInvoice));
    }

    public static async Task RefreshFinanceAsync(FinanceView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            var accounts = await view.Client.ListStudioGlAccountsAsync();
            if (view.UiState is FinanceFields financeFields)
            {
                PickerHelpers.BindGlAccounts(financeFields.Line1Account, accounts);
                PickerHelpers.BindGlAccounts(financeFields.Line2Account, accounts);
                var invoices = await view.Client.ListStudioSupplierInvoicesAsync();
                PickerHelpers.BindItems(
                    financeFields.SupplierInvoiceId,
                    invoices.Select(inv =>
                        new PickerHelpers.PickerItem(
                            inv.Id,
                            $"{inv.Number} · {inv.Amount:0.##} {inv.Currency}")));
            }

            switch (view.ListMode)
            {
                case "costcenters":
                    var cc = await view.Client.ListStudioCostCentersAsync();
                    SetList(view, cc, ("Código", "Code"), ("Nombre", "Name"), ("Activo", "IsActive"));
                    break;
                case "journals":
                    var journals = await view.Client.ListStudioJournalsAsync();
                    SetList(
                        view,
                        journals,
                        ("Número", "Number"),
                        ("Origen", "Source"),
                        ("Descripción", "Description"),
                        ("Fecha", "PostingDate"));
                    break;
                default:
                    view.ListMode = "accounts";
                    SetList(
                        view,
                        accounts,
                        ("Código", "Code"),
                        ("Nombre", "Name"),
                        ("Tipo", "AccountType"),
                        ("Activo", "IsActive"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P4 PP

    private sealed class PpFields
    {
        public ComboBox MaterialId = PickerHelpers.Create();
        public ComboBox ComponentMaterialId = PickerHelpers.Create();
        public TextBox BomCode = OfficeUi.Box("Ej. BOM-DEMO", 140);
        public TextBox RouteCode = OfficeUi.Box("Ej. RT-DEMO", 140);
        public ComboBox WarehouseId = PickerHelpers.Create();
    }

    public static void BuildPp(ProductionView view)
    {
        var fields = new PpFields
        {
            BomCode = { Text = "BOM-DEMO" },
            RouteCode = { Text = "RT-DEMO" },
        };
        view.UiState = fields;
        view.ListMode = "bom";

        var listBom = Primary("Listar BOM");
        var newBom = Primary("Nuevo BOM");
        var listRoute = Primary("Listar rutas");
        var newRoute = Primary("Nueva ruta");
        var listOf = Primary("Listar OF");
        var newOf = Primary("Nueva OF");
        var releaseOf = Primary("Liberar OF");
        var completeOf = Primary("Completar OF");
        var mrp = Primary("Disparar MRP");

        Wire(view, listBom, async () => { view.ListMode = "bom"; await RefreshPpAsync(view); });
        Wire(view, newBom, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione el material padre (lista de materiales / BOM).";
                return;
            }

            if (!PickerHelpers.TryGetId(fields.ComponentMaterialId, out var componentId))
            {
                view.StatusText.Text = "Seleccione el material componente del BOM.";
                return;
            }

            await view.Client.CreateStudioBomAsync(
                new CreateBomRequestDto(
                    fields.BomCode.Text?.Trim() ?? "BOM-DEMO",
                    materialId,
                    "BOM Studio",
                    [new CreateBomComponentRequestDto(componentId, 1, "UD")]));
            view.ListMode = "bom";
            view.StatusText.Text = "BOM creada.";
            await RefreshPpAsync(view);
        });
        Wire(view, listRoute, async () => { view.ListMode = "routes"; await RefreshPpAsync(view); });
        Wire(view, newRoute, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material para la ruta.";
                return;
            }

            await view.Client.CreateStudioProductionRouteAsync(
                new CreateProductionRouteRequestDto(
                    fields.RouteCode.Text?.Trim() ?? "RT-DEMO",
                    materialId,
                    "Ruta demo",
                    [new CreateRouteOperationRequestDto(10, "WC-DEMO", 5, 15)]));
            view.ListMode = "routes";
            view.StatusText.Text = "Ruta creada.";
            await RefreshPpAsync(view);
        });
        Wire(view, listOf, async () => { view.ListMode = "orders"; await RefreshPpAsync(view); });
        Wire(view, newOf, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material para la orden de fabricacion (OF).";
                return;
            }

            await view.Client.CreateStudioProductionOrderAsync(
                new CreateProductionOrderRequestDto(materialId, null, null, 1, "UD"));
            view.ListMode = "orders";
            view.StatusText.Text = "OF creada.";
            await RefreshPpAsync(view);
        });
        Wire(view, releaseOf, async () =>
        {
            if (view.Grid.SelectedItem is null
                || ReadAnonymousGuid(view.Grid.SelectedItem, "OrderId") is not Guid orderId)
            {
                view.StatusText.Text = "Seleccione una OF planificada en la rejilla (Listar OF).";
                return;
            }

            await view.Client.ReleaseStudioProductionOrderAsync(orderId);
            view.ListMode = "orders";
            view.StatusText.Text = "OF liberada.";
            await RefreshPpAsync(view);
        });
        Wire(view, completeOf, async () =>
        {
            if (view.Grid.SelectedItem is null
                || ReadAnonymousGuid(view.Grid.SelectedItem, "OrderId") is not Guid orderId)
            {
                view.StatusText.Text = "Seleccione una OF liberada en la rejilla (Listar OF).";
                return;
            }

            await view.Client.CompleteStudioProductionOrderAsync(orderId);
            view.ListMode = "orders";
            view.StatusText.Text = "OF completada.";
            await RefreshPpAsync(view);
        });
        Wire(view, mrp, async () =>
        {
            var result = await view.Client.TriggerStudioMrpAsync(OptionalId(fields.WarehouseId));
            view.SetEmptyVisible(true);
            view.StatusText.Text = $"MRP: modo {result.Mode}. {(result.Note ?? string.Empty)}".Trim();
        });

        view.Body.Children.Add(ButtonRow(listBom, newBom, listRoute, newRoute, listOf, newOf));
        view.Body.Children.Add(ButtonRow(releaseOf, completeOf, mrp));
        view.Body.Children.Add(OfficeUi.LabeledField("Material padre", fields.MaterialId, 160, OfficeHelpTexts.Keys.FieldBomParent));
        view.Body.Children.Add(OfficeUi.LabeledField("Componente BOM", fields.ComponentMaterialId, 160, OfficeHelpTexts.Keys.FieldBomComponent));
        view.Body.Children.Add(OfficeUi.LabeledField("Código BOM", fields.BomCode, 160));
        view.Body.Children.Add(OfficeUi.LabeledField("Código ruta", fields.RouteCode, 160));
        view.Body.Children.Add(OfficeUi.LabeledField("Almacén MRP", fields.WarehouseId, 160, OfficeHelpTexts.Keys.FieldMrpWarehouse));
    }

    public static async Task RefreshPpAsync(ProductionView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            await BindMastersAsync(view);
            if (view.UiState is PpFields ppFields)
            {
                PickerHelpers.BindMaterials(ppFields.MaterialId, view.Client.Masters.Materials);
                PickerHelpers.BindMaterials(ppFields.ComponentMaterialId, view.Client.Masters.Materials);
                PickerHelpers.BindWarehouses(ppFields.WarehouseId, view.Client.Masters.Warehouses);
            }

            switch (view.ListMode)
            {
                case "routes":
                    var routes = await view.Client.ListStudioProductionRoutesAsync();
                    var routeRows = routes.Select(route => new
                    {
                        route.Code,
                        Material = ResolveMaterialLabel(view, route.MaterialId),
                        route.Name,
                    }).ToList();
                    SetList(
                        view,
                        routeRows,
                        ("Código", "Code"),
                        ("Material", "Material"),
                        ("Nombre", "Name"));
                    break;
                case "orders":
                    var orders = await view.Client.ListStudioProductionOrdersAsync();
                    var orderRows = orders.Select(order => new
                    {
                        OrderId = order.Id,
                        order.Number,
                        Material = ResolveMaterialLabel(view, order.MaterialId),
                        Estado = order.Status,
                    }).ToList();
                    SetList(
                        view,
                        orderRows,
                        ("Número", "Number"),
                        ("Material", "Material"),
                        ("Estado", "Estado"));
                    break;
                default:
                    view.ListMode = "bom";
                    var boms = await view.Client.ListStudioBomsAsync();
                    var bomRows = boms.Select(bom => new
                    {
                        bom.Code,
                        Material = ResolveMaterialLabel(view, bom.ParentMaterialId),
                        bom.Description,
                    }).ToList();
                    SetList(
                        view,
                        bomRows,
                        ("Código", "Code"),
                        ("Material", "Material"),
                        ("Descripción", "Description"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P5 QM

    private sealed class QmFields
    {
        public ComboBox MaterialId = PickerHelpers.Create();
        public ComboBox LotId = PickerHelpers.Create(280);
        public TextBox PlanCode = OfficeUi.Box("Ej. QM-DEMO", 120);
        public Dictionary<Guid, string> LotLabels { get; } = new();
    }

    public static void BuildQm(QualityView view)
    {
        var fields = new QmFields { PlanCode = { Text = "QM-DEMO" } };
        view.UiState = fields;
        view.ListMode = "plans";

        var listPlans = Primary("Listar planes");
        var newPlan = Primary("Nuevo plan");
        var listHolds = Primary("Holds");
        var newHold = Primary("Nuevo hold");
        var release = Primary("Liberar hold");
        var listCert = Primary("Certificados");
        var newCert = Primary("Nuevo certificado");

        fields.MaterialId.SelectionChanged += async (_, _) =>
        {
            try
            {
                await ReloadQmLotsAsync(view);
            }
            catch (Exception exception) when (exception is ApiException
                or HttpRequestException
                or TaskCanceledException)
            {
                view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            }
        };

        Wire(view, listPlans, async () => { view.ListMode = "plans"; await RefreshQmAsync(view); });
        Wire(view, newPlan, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material.";
                return;
            }

            await view.Client.CreateStudioInspectionPlanAsync(
                new CreateInspectionPlanRequestDto(
                    fields.PlanCode.Text?.Trim() ?? "QM-DEMO",
                    materialId,
                    "Plan inspección demo"));
            view.ListMode = "plans";
            view.StatusText.Text = "Plan creado.";
            await RefreshQmAsync(view);
        });
        Wire(view, listHolds, async () => { view.ListMode = "holds"; await RefreshQmAsync(view); });
        Wire(view, newHold, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.LotId, out var lotId) || lotId == Guid.Empty)
            {
                view.StatusText.Text = "Seleccione un lote en el desplegable (tras elegir material).";
                return;
            }

            var created = await view.Client.CreateStudioQualityHoldAsync(
                new CreateQualityHoldRequestDto(
                    lotId,
                    OptionalId(fields.MaterialId),
                    "Hold demo Studio"));
            view.ListMode = "holds";
            view.StatusText.Text = created.StockBalancesUpdated > 0
                ? $"Hold creado · stock del lote pasado a Calidad ({created.StockBalancesUpdated} saldos)."
                : "Hold creado (sin saldos de stock con ese lote).";
            await RefreshQmAsync(view);
        });
        Wire(view, release, async () =>
        {
            if (view.Grid.SelectedItem is null)
            {
                view.StatusText.Text = "Seleccione un hold en la rejilla.";
                return;
            }

            var holdId = ReadAnonymousGuid(view.Grid.SelectedItem, "HoldId");
            if (holdId is null)
            {
                view.StatusText.Text = "Seleccione un hold en la rejilla.";
                return;
            }

            var owner = UiHelpers.FindWindow(view);
            if (owner is null)
            {
                return;
            }

            var released = await view.Client.ReleaseStudioQualityHoldAsync(owner, holdId.Value);
            view.StatusText.Text = released.StockBalancesUpdated > 0
                ? $"Hold liberado · stock del lote vuelto a Disponible ({released.StockBalancesUpdated} saldos)."
                : "Hold liberado.";
            await RefreshQmAsync(view);
        });
        Wire(view, listCert, async () => { view.ListMode = "certs"; await RefreshQmAsync(view); });
        Wire(view, newCert, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
            {
                view.StatusText.Text = "Seleccione un material.";
                return;
            }

            Guid? lotId = PickerHelpers.TryGetId(fields.LotId, out var selectedLot) && selectedLot != Guid.Empty
                ? selectedLot
                : null;
            await view.Client.CreateStudioQualityCertificateAsync(
                new CreateQualityCertificateRequestDto(materialId, lotId, "OK"));
            view.ListMode = "certs";
            view.StatusText.Text = "Certificado emitido.";
            await RefreshQmAsync(view);
        });

        view.Body.Children.Add(ButtonRow(
            listPlans,
            newPlan,
            listHolds,
            OfficeUi.WithHelp(newHold, OfficeHelpTexts.Keys.FieldQmLotHold),
            OfficeUi.WithHelp(release, OfficeHelpTexts.Keys.FieldQmRelease),
            listCert,
            newCert));
        view.Body.Children.Add(OfficeUi.LabeledField("Código plan", fields.PlanCode, 150));
        view.Body.Children.Add(OfficeUi.LabeledField("Material", fields.MaterialId, 150, OfficeHelpTexts.Keys.FieldMaterialGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Lote", fields.LotId, 150, OfficeHelpTexts.Keys.FieldQmLotHold));
    }

    private static async Task ReloadQmLotsAsync(QualityView view)
    {
        if (view.UiState is not QmFields fields)
        {
            return;
        }

        fields.LotLabels.Clear();
        if (!PickerHelpers.TryGetId(fields.MaterialId, out var materialId))
        {
            PickerHelpers.BindLots(fields.LotId, Array.Empty<LotResponse>(), includeEmpty: true);
            return;
        }

        var lots = await view.Client.GetLotsAsync(materialId);
        foreach (var lot in lots)
        {
            fields.LotLabels[lot.Id] = lot.ExpiryDate is { } expiry
                ? $"{lot.Code} · caduca {expiry:yyyy-MM-dd}"
                : lot.Code;
        }

        PickerHelpers.BindLots(fields.LotId, lots, includeEmpty: true);
    }

    private static Guid? ReadAnonymousGuid(object row, string propertyName)
    {
        var prop = row.GetType().GetProperty(propertyName);
        if (prop?.GetValue(row) is Guid id)
        {
            return id;
        }

        return null;
    }

    private static string ResolveMaterialLabel(StudioModuleViewBase view, Guid materialId)
    {
        var row = view.Client.Masters.Materials.FirstOrDefault(m => m.Id == materialId);
        if (row is null)
        {
            return materialId.ToString("D")[..8] + "…";
        }

        var name = string.IsNullOrWhiteSpace(row.DisplayName) ? row.Description : row.DisplayName;
        return $"{row.Code} — {name}";
    }

    private static string ResolveWarehouseLabel(StudioModuleViewBase view, Guid warehouseId)
    {
        var row = view.Client.Masters.Warehouses.FirstOrDefault(w => w.Id == warehouseId);
        return row is null ? warehouseId.ToString("D")[..8] + "…" : $"{row.Code} — {row.Name}";
    }

    private static string ResolvePartyLabel(StudioModuleViewBase view, Guid partyId, bool suppliers)
    {
        var rows = suppliers ? view.Client.Masters.Suppliers : view.Client.Masters.Customers;
        var row = rows.FirstOrDefault(party => party.Id == partyId);
        return row is null ? partyId.ToString("D")[..8] + "…" : $"{row.Name} ({row.TaxId})";
    }

    public static async Task RefreshQmAsync(QualityView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            await BindMastersAsync(view);
            if (view.UiState is QmFields qmFields)
            {
                PickerHelpers.BindMaterials(qmFields.MaterialId, view.Client.Masters.Materials);
                await ReloadQmLotsAsync(view);
            }

            switch (view.ListMode)
            {
                case "holds":
                    var holds = await view.Client.ListStudioQualityHoldsAsync();
                    var holdRows = holds.Select(hold => new
                    {
                        HoldId = hold.Id,
                        Lote = string.IsNullOrWhiteSpace(hold.LotCode)
                            ? hold.LotId.ToString("D")[..8] + "…"
                            : hold.LotCode,
                        Material = hold.MaterialId is Guid mid
                            ? ResolveMaterialLabel(view, mid)
                            : "—",
                        Motivo = hold.Reason,
                        Estado = hold.Status,
                    }).ToList();
                    SetList(
                        view,
                        holdRows,
                        ("Lote", "Lote"),
                        ("Material", "Material"),
                        ("Motivo", "Motivo"),
                        ("Estado", "Estado"));
                    break;
                case "certs":
                    var certs = await view.Client.ListStudioQualityCertificatesAsync();
                    var certRows = certs.Select(cert => new
                    {
                        cert.Number,
                        Material = ResolveMaterialLabel(view, cert.MaterialId),
                        Resultado = cert.Result,
                    }).ToList();
                    SetList(
                        view,
                        certRows,
                        ("Número", "Number"),
                        ("Material", "Material"),
                        ("Resultado", "Resultado"));
                    break;
                default:
                    view.ListMode = "plans";
                    var plans = await view.Client.ListStudioInspectionPlansAsync();
                    var planRows = plans.Select(plan => new
                    {
                        plan.Code,
                        Material = ResolveMaterialLabel(view, plan.MaterialId),
                        plan.Name,
                    }).ToList();
                    SetList(
                        view,
                        planRows,
                        ("Código", "Code"),
                        ("Material", "Material"),
                        ("Nombre", "Name"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P6 Org

    private sealed class OrgFields
    {
        public ComboBox UserId = PickerHelpers.Create();
        public ComboBox CompanyId = PickerHelpers.Create();
        public ComboBox PlantId = PickerHelpers.Create();
        public ComboBox WarehouseId = PickerHelpers.Create();
        public TextBox PlantCode = OfficeUi.Box("Ej. PLT01", 120);
        public TextBox PlantName = OfficeUi.Box("Nombre planta", 180);
    }

    public static void BuildOrg(OrgContextView view)
    {
        var fields = new OrgFields { PlantCode = { Text = "PLT01" }, PlantName = { Text = "Planta demo" } };
        view.UiState = fields;
        view.ListMode = "plants";

        var listPlants = Primary("Listar plantas");
        var newPlant = Primary("Nueva planta");
        var link = Primary("Vincular almacén");
        var listLinks = Primary("Enlaces");
        var getCtx = Primary("Leer contexto");
        var setCtx = Primary("Fijar contexto");

        Wire(view, listPlants, async () => { view.ListMode = "plants"; await RefreshOrgAsync(view); });
        Wire(view, newPlant, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.CompanyId, out var companyId))
            {
                view.StatusText.Text = "Seleccione una sociedad.";
                return;
            }

            await view.Client.CreateStudioPlantAsync(
                new CreatePlantRequestDto(
                    companyId,
                    fields.PlantCode.Text?.Trim() ?? "PLT01",
                    fields.PlantName.Text?.Trim() ?? "Planta demo"));
            view.ListMode = "plants";
            view.StatusText.Text = "Planta creada.";
            await RefreshOrgAsync(view);
        });
        Wire(view, link, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.PlantId, out var plantId)
                || !PickerHelpers.TryGetId(fields.WarehouseId, out var warehouseId))
            {
                view.StatusText.Text = "Seleccione planta y almacén.";
                return;
            }

            await view.Client.LinkStudioPlantWarehouseAsync(
                new LinkPlantWarehouseRequestDto(plantId, warehouseId));
            view.ListMode = "links";
            view.StatusText.Text = "Almacén vinculado.";
            await RefreshOrgAsync(view);
        });
        Wire(view, listLinks, async () => { view.ListMode = "links"; await RefreshOrgAsync(view); });
        Wire(view, getCtx, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.UserId, out var userId))
            {
                view.StatusText.Text = "Seleccione un usuario.";
                return;
            }

            var ctx = await view.Client.GetStudioUserOrgContextAsync(userId);
            view.SetEmptyVisible(true);
            view.StatusText.Text = ctx is null
                ? "Sin contexto org para ese usuario."
                : $"Contexto: sociedad {ctx.CompanyId}, planta {ctx.PlantId}.";
        });
        Wire(view, setCtx, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.UserId, out var userId))
            {
                view.StatusText.Text = "Seleccione un usuario.";
                return;
            }

            var owner = UiHelpers.FindWindow(view);
            if (owner is null)
            {
                return;
            }

            await view.Client.SetStudioUserOrgContextAsync(
                owner,
                new SetUserOrgContextRequestDto(
                    userId,
                    OptionalId(fields.CompanyId),
                    OptionalId(fields.PlantId)));
            view.StatusText.Text = "Contexto actualizado.";
        });

        view.Body.Children.Add(ButtonRow(
            listPlants,
            newPlant,
            link,
            listLinks,
            getCtx,
            OfficeUi.WithHelp(setCtx, OfficeHelpTexts.Keys.FieldOrgContext)));
        view.Body.Children.Add(OfficeUi.LabeledField("Usuario", fields.UserId, 150, OfficeHelpTexts.Keys.FieldUserGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Sociedad", fields.CompanyId, 150, OfficeHelpTexts.Keys.FieldOrgContext));
        view.Body.Children.Add(OfficeUi.LabeledField("Planta", fields.PlantId, 150, OfficeHelpTexts.Keys.FieldOrgContext));
        view.Body.Children.Add(OfficeUi.LabeledField("Almacén", fields.WarehouseId, 150, OfficeHelpTexts.Keys.FieldMrpWarehouse));
        view.Body.Children.Add(OfficeUi.LabeledField("Código planta", fields.PlantCode, 150));
        view.Body.Children.Add(OfficeUi.LabeledField("Nombre planta", fields.PlantName, 150));
    }

    public static async Task RefreshOrgAsync(OrgContextView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            await BindMastersAsync(view);
            var companies = await view.Client.ListCompaniesAsync();
            var plants = await view.Client.ListStudioPlantsAsync();
            var users = await view.Client.GetUsersAsync();
            if (view.UiState is OrgFields orgFields)
            {
                PickerHelpers.BindCompanies(orgFields.CompanyId, companies);
                PickerHelpers.BindPlants(orgFields.PlantId, plants);
                PickerHelpers.BindWarehouses(orgFields.WarehouseId, view.Client.Masters.Warehouses);
                PickerHelpers.BindUsers(orgFields.UserId, users);
            }

            if (view.ListMode == "links")
            {
                var links = await view.Client.ListStudioPlantWarehousesAsync();
                var plantMap = plants.ToDictionary(p => p.Id, p => $"{p.Code} — {p.Name}");
                var linkRows = links.Select(link => new
                {
                    Planta = plantMap.TryGetValue(link.PlantId, out var pl) ? pl : link.PlantId.ToString("D")[..8] + "…",
                    Almacen = ResolveWarehouseLabel(view, link.WarehouseId),
                }).ToList();
                SetList(view, linkRows, ("Planta", "Planta"), ("Almacén", "Almacen"));
                return;
            }

            view.ListMode = "plants";
            var companyMap = companies.ToDictionary(c => c.Id, c => $"{c.Code} — {c.LegalName}");
            var plantRows = plants.Select(plant => new
            {
                plant.Code,
                plant.Name,
                Sociedad = companyMap.TryGetValue(plant.CompanyId, out var co)
                    ? co
                    : plant.CompanyId.ToString("D")[..8] + "…",
            }).ToList();
            SetList(
                view,
                plantRows,
                ("Código", "Code"),
                ("Nombre", "Name"),
                ("Sociedad", "Sociedad"));
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P7 Workflow

    public static void BuildWorkflow(WorkflowView view)
    {
        view.ListMode = "queue";

        var listRules = Primary("Listar reglas");
        var newRule = Primary("Nueva regla");
        var listQueue = Primary("Cola pendiente");
        var approve = Primary("Aprobar petición");
        var reject = Primary("Rechazar petición");

        Wire(view, listRules, async () => { view.ListMode = "rules"; await RefreshWorkflowAsync(view); });
        Wire(view, newRule, async () =>
        {
            await view.Client.CreateStudioApprovalRuleAsync(
                new CreateApprovalRuleRequestDto("PurchaseOrder", 1000, "Approver", "Regla demo"));
            view.ListMode = "rules";
            view.StatusText.Text = "Regla creada.";
            await RefreshWorkflowAsync(view);
        });
        Wire(view, listQueue, async () => { view.ListMode = "queue"; await RefreshWorkflowAsync(view); });
        Wire(view, approve, async () =>
        {
            if (view.Grid.SelectedItem is not ApprovalRequestResponse item)
            {
                view.StatusText.Text = "Seleccione una petición.";
                return;
            }

            var owner = UiHelpers.FindWindow(view);
            if (owner is null)
            {
                return;
            }

            await view.Client.ApproveStudioApprovalRequestAsync(owner, item.Id);
            view.StatusText.Text = "Aprobada.";
            await RefreshWorkflowAsync(view);
        });
        Wire(view, reject, async () =>
        {
            if (view.Grid.SelectedItem is not ApprovalRequestResponse item)
            {
                view.StatusText.Text = "Seleccione una petición.";
                return;
            }

            var owner = UiHelpers.FindWindow(view);
            if (owner is null)
            {
                return;
            }

            await view.Client.RejectStudioApprovalRequestAsync(owner, item.Id);
            view.StatusText.Text = "Rechazada.";
            await RefreshWorkflowAsync(view);
        });

        view.Body.Children.Add(ButtonRow(listRules, newRule, listQueue, approve, reject));
    }

    public static async Task RefreshWorkflowAsync(WorkflowView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            if (view.ListMode == "rules")
            {
                var rules = await view.Client.ListStudioApprovalRulesAsync();
                SetList(
                    view,
                    rules,
                    ("Documento", "DocumentKind"),
                    ("Importe mín.", "MinAmount"),
                    ("Rol", "RequiredRole"),
                    ("Nombre", "Name"));
                return;
            }

            view.ListMode = "queue";
            var queue = await view.Client.ListStudioApprovalRequestsAsync();
            SetList(
                view,
                queue,
                ("Documento", "DocumentKind"),
                ("Importe", "Amount"),
                ("Resumen", "Summary"),
                ("Estado", "Status"));
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P8 IntHub

  private sealed class IntHubFields
    {
        public TextBox ConnectorCode = OfficeUi.Box("Ej. CONN-DEMO", 140);
        public ComboBox ConnectorId = PickerHelpers.Create();
        public TextBox MessageType = OfficeUi.Box("Ej. Material.Upsert", 160);
        public TextBox Payload = OfficeUi.Box("Pegue aquí el JSON del mensaje", 320);
        public TextBox MapSystem = OfficeUi.Box("Ej. SAP", 100);
        public TextBox MapEntity = OfficeUi.Box("Ej. Material", 120);
        public TextBox MapExternal = OfficeUi.Box("Clave en el sistema externo", 160);
        public ComboBox MapInternal = PickerHelpers.Create();
    }

    public static void BuildIntHub(IntegrationHubView view)
    {
        var fields = new IntHubFields
        {
            ConnectorCode = { Text = "CONN-DEMO" },
            MessageType = { Text = "Material.Upsert" },
            Payload = { Text = "{\"demo\":true}" },
            MapSystem = { Text = "SAP" },
            MapEntity = { Text = "Material" },
            MapExternal = { Text = "EXT-001" },
        };
        view.UiState = fields;
        view.ListMode = "connectors";

        var listConn = Primary("Ver conectores");
        var newConn = Primary("Registrar conector");
        var listIn = Primary("Bandeja de entrada");
        var receive = Primary("Recibir mensaje");
        var listOut = Primary("Bandeja de salida");
        var listMap = Primary("Ver mapeos");
        var upsertMap = Primary("Guardar mapeo");

        Wire(view, listConn, async () => { view.ListMode = "connectors"; await RefreshIntHubAsync(view); });
        Wire(view, newConn, async () =>
        {
            await view.Client.CreateStudioIntegrationConnectorAsync(
                new CreateIntegrationConnectorRequestDto(
                    fields.ConnectorCode.Text?.Trim() ?? "CONN-DEMO",
                    "Conector demo",
                    "REST",
                    "1.0"));
            view.ListMode = "connectors";
            view.StatusText.Text = "Conector registrado.";
            await RefreshIntHubAsync(view);
        });
        Wire(view, listIn, async () => { view.ListMode = "inbox"; await RefreshIntHubAsync(view); });
        Wire(view, receive, async () =>
        {
            var payload = fields.Payload.Text?.Trim() ?? "{}";
            if (string.IsNullOrWhiteSpace(payload))
            {
                view.StatusText.Text = "Indique el contenido JSON del mensaje.";
                return;
            }

            await view.Client.ReceiveStudioIntegrationInboxAsync(
                new ReceiveIntegrationInboxRequestDto(
                    OptionalId(fields.ConnectorId),
                    fields.MessageType.Text?.Trim() ?? "Material.Upsert",
                    payload));
            view.ListMode = "inbox";
            view.StatusText.Text = "Mensaje recibido en la bandeja de entrada.";
            await RefreshIntHubAsync(view);
        });
        Wire(view, listOut, async () => { view.ListMode = "outbox"; await RefreshIntHubAsync(view); });
        Wire(view, listMap, async () => { view.ListMode = "mappings"; await RefreshIntHubAsync(view); });
        Wire(view, upsertMap, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.MapInternal, out var internalId))
            {
                view.StatusText.Text = "Seleccione el material interno del mapeo.";
                return;
            }

            await view.Client.UpsertStudioIntegrationMappingAsync(
                new UpsertStudioIntegrationMappingRequestDto(
                    fields.MapSystem.Text?.Trim() ?? "SAP",
                    fields.MapEntity.Text?.Trim() ?? "Material",
                    fields.MapExternal.Text?.Trim() ?? "EXT-001",
                    internalId));
            view.ListMode = "mappings";
            view.StatusText.Text = "Mapeo guardado.";
            await RefreshIntHubAsync(view);
        });

        view.Body.Children.Add(ButtonRow(
            listConn,
            OfficeUi.WithHelp(newConn, OfficeHelpTexts.Keys.FieldConnector),
            OfficeUi.WithHelp(listIn, OfficeHelpTexts.Keys.FieldInbox),
            OfficeUi.WithHelp(receive, OfficeHelpTexts.Keys.FieldReceiveMessage),
            OfficeUi.WithHelp(listOut, OfficeHelpTexts.Keys.FieldOutbox)));
        view.Body.Children.Add(ButtonRow(
            listMap,
            OfficeUi.WithHelp(upsertMap, OfficeHelpTexts.Keys.FieldExternalMapping)));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Código conector",
            fields.ConnectorCode,
            150,
            OfficeHelpTexts.Keys.FieldConnector));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Conector (opcional)",
            fields.ConnectorId,
            150,
            OfficeHelpTexts.Keys.FieldConnector));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Tipo de mensaje",
            fields.MessageType,
            150,
            OfficeHelpTexts.Keys.FieldReceiveMessage));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Contenido del mensaje",
            fields.Payload,
            150,
            OfficeHelpTexts.Keys.FieldPayload));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Sistema externo",
            fields.MapSystem,
            150,
            OfficeHelpTexts.Keys.FieldExternalMapping));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Tipo de entidad",
            fields.MapEntity,
            150,
            OfficeHelpTexts.Keys.FieldExternalMapping));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Clave externa",
            fields.MapExternal,
            150,
            OfficeHelpTexts.Keys.FieldExternalMapping));
        view.Body.Children.Add(OfficeUi.LabeledField(
            "Material interno (mapeo)",
            fields.MapInternal,
            150,
            OfficeHelpTexts.Keys.FieldExternalMapping));
    }

    public static async Task RefreshIntHubAsync(IntegrationHubView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            await BindMastersAsync(view);
            var connectors = await view.Client.ListStudioIntegrationConnectorsAsync();
            if (view.UiState is IntHubFields intFields)
            {
                PickerHelpers.BindConnectors(intFields.ConnectorId, connectors);
                PickerHelpers.BindMaterials(intFields.MapInternal, view.Client.Masters.Materials);
            }

            switch (view.ListMode)
            {
                case "inbox":
                    var inbox = await view.Client.ListStudioIntegrationInboxAsync();
                    view.EmptyHint.Text =
                        "No hay mensajes en la bandeja de entrada. Pulse Recibir mensaje con un JSON válido.";
                    SetList(view, inbox, ("Tipo", "MessageType"), ("Procesado", "Processed"), ("Recibido", "ReceivedUtc"));
                    break;
                case "outbox":
                    var outbox = await view.Client.ListStudioIntegrationOutboxAsync();
                    view.EmptyHint.Text =
                        "No hay mensajes en la bandeja de salida.";
                    SetList(view, outbox, ("Tipo", "MessageType"), ("Estado", "Status"), ("Intentos", "AttemptCount"));
                    break;
                case "mappings":
                    var mappings = await view.Client.ListStudioIntegrationMappingsAsync();
                    SetList(
                        view,
                        mappings,
                        ("Sistema", "SystemCode"),
                        ("Entidad", "EntityType"),
                        ("Externo", "ExternalKey"),
                        ("Interno", "InternalId"),
                        ("UTC", "MappedUtc"));
                    break;
                default:
                    view.ListMode = "connectors";
                    SetList(view, connectors, ("Código", "Code"), ("Nombre", "Name"), ("Versión", "Version"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P9 BI

    public static void BuildBi(BiDashboardView view)
    {
        view.ListMode = "valued";

        var valued = Primary("Stock valorado");
        var aging = Primary("Antigüedad de cobro (aging)");
        var otif = Primary("OTIF (a tiempo y completo)");

        Wire(view, valued, async () => { view.ListMode = "valued"; await RefreshBiAsync(view); });
        Wire(view, aging, async () => { view.ListMode = "aging"; await RefreshBiAsync(view); });
        Wire(view, otif, async () => { view.ListMode = "otif"; await RefreshBiAsync(view); });

        view.Body.Children.Add(ButtonRow(valued, aging, otif));
    }

    public static async Task RefreshBiAsync(BiDashboardView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            switch (view.ListMode)
            {
                case "aging":
                    var aging = await view.Client.GetStudioAgingReportAsync();
                    SetList(
                        view,
                        aging,
                        ("Cliente", "CustomerName"),
                        ("Importe", "OpenAmount"),
                        ("Bucket", "BucketDays"),
                        ("Moneda", "Currency"));
                    break;
                case "otif":
                    var otif = await view.Client.GetStudioOtifPassthroughAsync();
                    view.SetEmptyVisible(true);
                    view.Grid.ItemsSource = null;
                    view.StatusText.Text = otif is null
                        ? "Sin datos OTIF."
                        : $"OTIF: {otif.OtifPercent}% ({otif.OnTimeOrders}/{otif.DeliveredOrders} entregas)";
                    break;
                default:
                    view.ListMode = "valued";
                    await BindMastersAsync(view);
                    var stock = await view.Client.GetStudioValuedStockReportAsync();
                    var mats = view.Client.Masters.Materials.ToDictionary(m => m.Id, m => m.Code);
                    var whs = view.Client.Masters.Warehouses.ToDictionary(w => w.Id, w => w.Code);
                    var stockRows = stock.Select(row => new
                    {
                        Material = mats.TryGetValue(row.MaterialId, out var mc) ? mc : row.MaterialId.ToString("D"),
                        Warehouse = whs.TryGetValue(row.WarehouseId, out var wc) ? wc : row.WarehouseId.ToString("D"),
                        row.Quantity,
                        row.TotalValue,
                    }).ToList();
                    SetList(
                        view,
                        stockRows,
                        ("Material", "Material"),
                        ("Almacén", "Warehouse"),
                        ("Cantidad", "Quantity"),
                        ("Valor total", "TotalValue"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P10 HR

    private sealed class HrFields
    {
        public ComboBox UserId = PickerHelpers.Create();
        public TextBox EmpNum = OfficeUi.Box("Ej. E001", 120);
        public TextBox Display = OfficeUi.Box("Nombre completo", 180);
        public TextBox Department = OfficeUi.Box("Ej. Operaciones", 160);
        public TextBox Active = OfficeUi.Box("sí / no", 100);
        public IReadOnlyList<EmployeeProfileResponse> Rows = [];
    }

    public static void BuildHr(HrLiteView view)
    {
        var fields = new HrFields
        {
            EmpNum = { Text = "E001" },
            Display = { Text = "Empleado demo" },
            Department = { Text = "Operaciones" },
            Active = { Text = "sí" },
        };
        view.UiState = fields;
        view.ListMode = "employees";

        var list = Primary("Listar");
        var create = Primary("Nuevo empleado");
        var loadSelected = Primary("Cargar selección");
        var update = Primary("Actualizar empleado");

        Wire(view, list, async () => await RefreshHrAsync(view));
        Wire(view, create, async () =>
        {
            if (!PickerHelpers.TryGetId(fields.UserId, out var userId))
            {
                view.StatusText.Text = "Seleccione una cuenta de usuario (Cuentas).";
                return;
            }

            await view.Client.CreateStudioEmployeeAsync(
                new CreateEmployeeProfileRequestDto(
                    userId,
                    fields.EmpNum.Text?.Trim() ?? "E001",
                    fields.Display.Text?.Trim() ?? "Empleado demo",
                    fields.Department.Text?.Trim() ?? "Operaciones",
                    DateOnly.FromDateTime(DateTime.Today)));
            view.StatusText.Text = "Empleado creado.";
            await RefreshHrAsync(view);
        });
        Wire(view, loadSelected, async () =>
        {
            if (view.Grid.SelectedItem is not EmployeeProfileResponse selected
                && (view.Grid.SelectedIndex < 0
                    || view.Grid.SelectedIndex >= fields.Rows.Count))
            {
                view.StatusText.Text = "Seleccione un empleado en la lista.";
                return;
            }

            var row = view.Grid.SelectedItem as EmployeeProfileResponse
                ?? fields.Rows[view.Grid.SelectedIndex];
            PickerHelpers.SelectId(fields.UserId, row.UserId);
            fields.EmpNum.Text = row.EmployeeNumber;
            fields.Display.Text = row.DisplayName;
            fields.Department.Text = row.Department;
            fields.Active.Text = row.IsActive ? "sí" : "no";
            view.StatusText.Text = $"Cargado {row.EmployeeNumber} ({row.Id:D}).";
            await Task.CompletedTask;
        });
        Wire(view, update, async () =>
        {
            EmployeeProfileResponse? row = view.Grid.SelectedItem as EmployeeProfileResponse;
            if (row is null
                && view.Grid.SelectedIndex >= 0
                && view.Grid.SelectedIndex < fields.Rows.Count)
            {
                row = fields.Rows[view.Grid.SelectedIndex];
            }

            if (row is null)
            {
                view.StatusText.Text = "Seleccione el empleado a actualizar.";
                return;
            }

            var activeText = fields.Active.Text?.Trim() ?? "sí";
            var isActive = activeText is "sí" or "si" or "true" or "1" or "S" or "s";
            await view.Client.UpdateStudioEmployeeAsync(
                row.Id,
                new UpdateEmployeeProfileRequestDto(
                    fields.Display.Text?.Trim() ?? row.DisplayName,
                    fields.Department.Text?.Trim() ?? row.Department,
                    row.HireDate,
                    isActive));
            view.StatusText.Text = "Empleado actualizado.";
            await RefreshHrAsync(view);
        });

        view.Body.Children.Add(ButtonRow(list, create, loadSelected, update));
        view.Body.Children.Add(OfficeUi.LabeledField("Usuario", fields.UserId, 140, OfficeHelpTexts.Keys.FieldUserGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Nº empleado", fields.EmpNum, 140));
        view.Body.Children.Add(OfficeUi.LabeledField("Nombre", fields.Display, 140));
        view.Body.Children.Add(OfficeUi.LabeledField("Departamento", fields.Department, 140));
        view.Body.Children.Add(OfficeUi.LabeledField("Activo", fields.Active, 140));
    }

    public static async Task RefreshHrAsync(HrLiteView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            var users = await view.Client.GetUsersAsync();
            if (view.UiState is HrFields hrPickers)
            {
                PickerHelpers.BindUsers(hrPickers.UserId, users);
            }

            var rows = await view.Client.ListStudioEmployeesAsync();
            if (view.UiState is HrFields fields)
            {
                fields.Rows = rows;
            }

            SetList(
                view,
                rows,
                ("Nº", "EmployeeNumber"),
                ("Nombre", "DisplayName"),
                ("Departamento", "Department"),
                ("Activo", "IsActive"));
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion

  #region P11 Ext

    private sealed class ExtFields
    {
        public TextBox EventName = OfficeUi.Box("Ej. Order.Created", 160);
        public TextBox PluginCode = OfficeUi.Box("Ej. PLUGIN-DEMO", 140);
        public ComboBox PluginId = PickerHelpers.Create();
        public TextBox PluginParams = OfficeUi.Box("JSON de parámetros", 280);
        public TextBox TransportCode = OfficeUi.Box("Ej. TR-DEMO", 160);
        public IReadOnlyList<PluginRegistrationResponse> Plugins = [];
    }

    public static void BuildExt(ExtensibilityView view)
    {
        var fields = new ExtFields
        {
            EventName = { Text = "Order.Created" },
            PluginCode = { Text = "PLUGIN-DEMO" },
            PluginParams = { Text = "{\"dryRun\":true}" },
            TransportCode = { Text = "TR-DEMO" },
        };
        view.UiState = fields;
        view.ListMode = "events";

        var listEvents = Primary("Eventos");
        var append = Primary("Registrar evento");
        var listPlugins = Primary("Plugins");
        var regPlugin = Primary("Nuevo plugin");
        var testPlugin = Primary("Probar plugin");
        var listTransport = Primary("Transportes");
        var newTransport = Primary("Nuevo transporte");

        Wire(view, listEvents, async () => { view.ListMode = "events"; await RefreshExtAsync(view); });
        Wire(view, append, async () =>
        {
            await view.Client.AppendStudioDomainEventAsync(
                new AppendDomainEventRequestDto(
                    fields.EventName.Text?.Trim() ?? "Order.Created",
                    "Order",
                    Guid.NewGuid(),
                    "{}"));
            view.ListMode = "events";
            view.StatusText.Text = "Evento registrado.";
            await RefreshExtAsync(view);
        });
        Wire(view, listPlugins, async () => { view.ListMode = "plugins"; await RefreshExtAsync(view); });
        Wire(view, regPlugin, async () =>
        {
            await view.Client.RegisterStudioPluginAsync(
                new RegisterPluginRequestDto(
                    fields.PluginCode.Text?.Trim() ?? "PLUGIN-DEMO",
                    "Plugin demo",
                    "BeforeSave",
                    "Hook demo"));
            view.ListMode = "plugins";
            view.StatusText.Text = "Plugin registrado.";
            await RefreshExtAsync(view);
        });
        Wire(view, testPlugin, async () =>
        {
            Guid pluginId;
            if (PickerHelpers.TryGetId(fields.PluginId, out pluginId))
            {
                // picker
            }
            else if (view.Grid.SelectedItem is PluginRegistrationResponse selected)
            {
                pluginId = selected.Id;
            }
            else if (view.Grid.SelectedIndex >= 0
                && view.Grid.SelectedIndex < fields.Plugins.Count)
            {
                pluginId = fields.Plugins[view.Grid.SelectedIndex].Id;
            }
            else
            {
                view.StatusText.Text = "Indique PluginId o seleccione un plugin en la lista.";
                return;
            }

            var result = await view.Client.InvokeStudioPluginAsync(
                pluginId,
                new InvokePluginRequestDto(fields.PluginParams.Text?.Trim() ?? "{}"));
            view.ListMode = "events";
            view.StatusText.Text =
                $"Plugin {result.Code} ({result.HookPoint}): {result.Outcome}. Evento {result.DomainEventId:D}.";
            await RefreshExtAsync(view);
        });
        Wire(view, listTransport, async () => { view.ListMode = "transport"; await RefreshExtAsync(view); });
        Wire(view, newTransport, async () =>
        {
            await view.Client.CreateStudioConfigTransportAsync(
                new CreateConfigTransportRequestDto(
                    fields.TransportCode.Text?.Trim() ?? "TR-DEMO",
                    "DEV",
                    "{}"));
            view.ListMode = "transport";
            view.StatusText.Text = "Transporte creado.";
            await RefreshExtAsync(view);
        });

        view.Body.Children.Add(ButtonRow(
            listEvents,
            append,
            listPlugins,
            regPlugin,
            OfficeUi.WithHelp(testPlugin, OfficeHelpTexts.Keys.FieldPluginGuid),
            listTransport,
            newTransport));
        view.Body.Children.Add(OfficeUi.LabeledField("Evento", fields.EventName, 150));
        view.Body.Children.Add(OfficeUi.LabeledField("Código plugin", fields.PluginCode, 150));
        view.Body.Children.Add(OfficeUi.LabeledField("Plugin", fields.PluginId, 150, OfficeHelpTexts.Keys.FieldPluginGuid));
        view.Body.Children.Add(OfficeUi.LabeledField("Parámetros JSON", fields.PluginParams, 150, OfficeHelpTexts.Keys.FieldPayload));
        view.Body.Children.Add(OfficeUi.LabeledField("Código transporte", fields.TransportCode, 150));
    }

    public static async Task RefreshExtAsync(ExtensibilityView view)
    {
        view.StatusText.Text = string.Empty;
        try
        {
            switch (view.ListMode)
            {
                case "plugins":
                    var plugins = await view.Client.ListStudioPluginsAsync();
                    if (view.UiState is ExtFields fields)
                    {
                        fields.Plugins = plugins;
                        PickerHelpers.BindPlugins(fields.PluginId, plugins);
                    }

                    SetList(view, plugins, ("Código", "Code"), ("Hook", "HookPoint"), ("Activo", "IsEnabled"));
                    break;
                case "transport":
                    var transports = await view.Client.ListStudioConfigTransportsAsync();
                    SetList(view, transports, ("Código", "Code"), ("Entorno", "Environment"), ("Creado", "CreatedUtc"));
                    break;
                default:
                    view.ListMode = "events";
                    var events = await view.Client.ListStudioDomainEventsAsync();
                    SetList(
                        view,
                        events,
                        ("Evento", "EventName"),
                        ("Agregado", "AggregateType"),
                        ("Cuándo", "OccurredUtc"));
                    break;
            }
        }
        catch (Exception exception) when (exception is ApiException
            or HttpRequestException
            or TaskCanceledException)
        {
            view.StatusText.Text = UiHelpers.ToUserMessage(exception);
            view.SetEmptyVisible(true);
            await UiHelpers.ShowErrorAsync(view, exception);
        }
    }

  #endregion
}
