namespace AAALogistics.Client.Services;

/// <summary>
/// Catálogo de módulos del Studio. P0 registra todos; cada fase marca Enabled=true al entregarse.
/// Logi Compass = brújula dentro de AAA Logistics Studio.
/// </summary>
/// <param name="AnyPermission">El módulo se muestra si el usuario tiene al menos uno (vacío = todos).</param>
internal sealed record StudioModule(
    string Id,
    string Phase,
    string Title,
    string NavTag,
    string HelpKey,
    string Summary,
    bool Enabled,
    string[]? AnyPermission = null);

internal static class StudioModuleRegistry
{
    private static readonly List<StudioModule> Modules =
    [
        new(
            "studio-hub",
            "P0",
            "Mapa del Studio",
            "studio",
            "studio.hub",
            "Índice de módulos AAA Logistics Studio (Logi Compass = brújula logística).",
            Enabled: true),
        new(
            "pricing",
            "P1",
            "Precios / IVA / crédito / RMA",
            "pricing",
            "studio.pricing",
            "Impuestos (IVA), condiciones de precio, crédito de cliente y devoluciones (RMA).",
            Enabled: true,
            [
                AppPermissionNames.InvoiceIssue,
                AppPermissionNames.InvoiceConfig,
                AppPermissionNames.CustomerManage,
                AppPermissionNames.ReturnManage,
            ]),
        new(
            "mm",
            "P2",
            "Compras MM avanzado",
            "mm",
            "studio.mm",
            "Contratos marco, peticiones de oferta, facturas de proveedor, conciliación 3-way y valoración de stock.",
            Enabled: true,
            [
                AppPermissionNames.PurchaseOrderCreate,
                AppPermissionNames.StockRead,
                AppPermissionNames.MaterialManage,
                AppPermissionNames.InvoiceConfig,
            ]),
        new(
            "finance",
            "P3",
            "Finanzas FI/CO lite",
            "finance",
            "studio.finance",
            "Plan de cuentas PGC básico, centros de coste, asientos manuales cuadrados y conciliación debe/haber.",
            Enabled: true,
            [AppPermissionNames.ReportRead, AppPermissionNames.InvoiceConfig]),
        new(
            "pp",
            "P4",
            "Producción PP lite",
            "pp",
            "studio.pp",
            "Listas de materiales, rutas, órdenes de fabricación (registro) y cálculo MRP de reaprovisionamiento.",
            Enabled: true,
            [AppPermissionNames.MaterialManage, AppPermissionNames.PurchaseOrderCreate]),
        new(
            "qm",
            "P5",
            "Calidad QM",
            "qm",
            "studio.qm",
            "Planes de inspección, bloqueos de lote (informativos) y certificados de calidad.",
            Enabled: true,
            [
                AppPermissionNames.MaterialManage,
                AppPermissionNames.StockRead,
                AppPermissionNames.StockAdjust,
            ]),
        new(
            "org",
            "P6",
            "Sociedad / planta",
            "org",
            "studio.org",
            "Plantas por sociedad, almacenes por planta y sociedad/planta de cada usuario (informativa: no filtra datos).",
            Enabled: true,
            [
                AppPermissionNames.WarehouseManage,
                AppPermissionNames.UserRead,
                AppPermissionNames.UserManageRanges,
            ]),
        new(
            "workflow",
            "P7",
            "Aprobaciones",
            "workflow",
            "studio.workflow",
            "Reglas por importe y rango, solicitudes de aprobación y decisión con cuatro ojos.",
            Enabled: true,
            [
                AppPermissionNames.UserRead,
                AppPermissionNames.UserManagePermissions,
                AppPermissionNames.PurchaseOrderCreate,
            ]),
        new(
            "integrations",
            "P8",
            "Intercambio de datos",
            "inthub",
            "studio.inthub",
            "Registro de conectores versionados y bandejas de entrada y salida de mensajes.",
            Enabled: true,
            [AppPermissionNames.MaterialManage, AppPermissionNames.DeliveryPost]),
        new(
            "bi",
            "P9",
            "Reporting / BI",
            "bi",
            "studio.bi",
            "Stock valorado, antigüedad de cobros (aging) y OTIF.",
            Enabled: true,
            [AppPermissionNames.ReportRead]),
        new(
            "hr",
            "P10",
            "RRHH lite",
            "hr",
            "studio.hr",
            "Fichas de empleado ligadas a cuentas de usuario (sin nómina).",
            Enabled: true,
            [AppPermissionNames.UserRead, AppPermissionNames.UserCreate]),
        new(
            "ext",
            "P11",
            "Extensibilidad",
            "ext",
            "studio.ext",
            "Registro de eventos de dominio, plugins documentados y transportes de configuración (JSON).",
            Enabled: true,
            [
                AppPermissionNames.ReportRead,
                AppPermissionNames.MaterialManage,
                AppPermissionNames.AdminTableEdit,
            ]),
    ];

    public static IReadOnlyList<StudioModule> All => Modules;

    public static IReadOnlyList<StudioModule> EnabledOnly =>
        Modules.Where(m => m.Enabled).ToArray();

    public static void SetEnabled(string id, bool enabled)
    {
        var index = Modules.FindIndex(m =>
            string.Equals(m.Id, id, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var current = Modules[index];
        Modules[index] = current with { Enabled = enabled };
    }

    public static StudioModule? FindByNavTag(string navTag) =>
        Modules.FirstOrDefault(m =>
            string.Equals(m.NavTag, navTag, StringComparison.Ordinal));

    public static bool HasAccess(StudioModule module, PermissionGate gate) =>
        module.AnyPermission is not { Length: > 0 } permissions || gate.HasAny(permissions);

    public static bool IsAvailable(string navTag, PermissionGate gate) =>
        FindByNavTag(navTag) is { Enabled: true } module && HasAccess(module, gate);

    /// <summary>
    /// True si hay al menos un módulo operativo (excl. hub) con claim del usuario.
    /// </summary>
    public static bool HasAnyModuleAccess(PermissionGate gate) =>
        EnabledOnly.Any(module =>
            !string.Equals(module.Id, "studio-hub", StringComparison.Ordinal)
            && HasAccess(module, gate));

    public static IReadOnlyList<StudioModule> VisibleFor(PermissionGate gate) =>
        EnabledOnly
            .Where(module =>
                string.Equals(module.Id, "studio-hub", StringComparison.Ordinal)
                    ? HasAnyModuleAccess(gate)
                    : HasAccess(module, gate))
            .ToArray();
}
