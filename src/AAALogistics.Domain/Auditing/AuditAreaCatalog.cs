namespace AAALogistics.Domain.Auditing;

/// <summary>
/// Maps UI audit sections to table-name prefixes / exact tables for filtering.
/// </summary>
public static class AuditAreaCatalog
{
    public const string Accesos = "accesos";
    public const string Maestros = "maestros";
    public const string Compras = "compras";
    /// <summary>Legacy alias: same filter key as <see cref="Ventas"/>.</summary>
    public const string VentasPrep = "ventas";
    public const string Ventas = "ventas";
    public const string Preparacion = "preparacion";
    public const string Inventario = "inventario";
    public const string Facturas = "facturas";
    public const string Cuentas = "cuentas";
    public const string StudioSistema = "studio";

    public static IReadOnlyList<string> AllAreas { get; } =
    [
        Accesos,
        Maestros,
        Compras,
        Ventas,
        Preparacion,
        Inventario,
        Facturas,
        Cuentas,
        StudioSistema,
    ];

    public static bool TryResolveTables(
        string? area,
        out IReadOnlyList<string> exactTables,
        out IReadOnlyList<string> prefixes)
    {
        exactTables = [];
        prefixes = [];
        if (string.IsNullOrWhiteSpace(area))
        {
            return false;
        }

        switch (area.Trim().ToLowerInvariant())
        {
            case Accesos:
                exactTables = ["auth_sessions"];
                return true;
            case Maestros:
                exactTables =
                [
                    "materials",
                    "units_of_measure",
                    "warehouses",
                    "locations",
                    "suppliers",
                    "customers",
                    "carriers",
                    "unit_conversions",
                ];
                return true;
            case Compras:
                exactTables =
                [
                    "purchase_orders",
                    "purchase_order_lines",
                    "goods_receipts",
                    "goods_receipt_lines",
                    "purchase_requisitions",
                    "purchase_requisition_lines",
                ];
                return true;
            case Ventas:
                exactTables =
                [
                    "sales_orders",
                    "sales_order_lines",
                    "deliveries",
                    "delivery_lines",
                    "sales_returns",
                    "delivery_notes",
                    "cmr_documents",
                ];
                return true;
            case Preparacion:
                exactTables =
                [
                    "order_prep_sessions",
                    "prep_pick_lines",
                    "handling_units",
                ];
                return true;
            case Inventario:
                prefixes =
                [
                    "stock_",
                ];
                exactTables =
                [
                    "lots",
                    "inventory_counts",
                    "inventory_count_lines",
                ];
                return true;
            case Facturas:
                exactTables =
                [
                    "invoices",
                    "invoice_lines",
                    "invoice_series",
                    "issuer_fiscal_profiles",
                    "tax_codes",
                ];
                return true;
            case Cuentas:
                exactTables =
                [
                    "identity_users",
                    "identity_roles",
                    "identity_user_roles",
                    "identity_user_claims",
                    "identity_role_claims",
                    "registration_requests",
                    "legal_acceptances",
                ];
                return true;
            case StudioSistema:
                prefixes =
                [
                    "studio_",
                    "gl_",
                    "journal_",
                    "bom_",
                    "qm_",
                    "hr_",
                    "plugin_",
                    "approval_",
                    "connector_",
                    "rfq_",
                    "rma_",
                    "mensajes_",
                    "mapeos_",
                    "intercambios_",
                ];
                exactTables =
                [
                    "person_data_keys",
                    "backup_records",
                    "document_number_ranges",
                    "system_settings",
                    "companies",
                    "exchange_rates",
                    "rate_cards",
                    "transport_routes",
                ];
                return true;
            default:
                return false;
        }
    }

    public static bool MatchesArea(string tableName, string? area)
    {
        if (!TryResolveTables(area, out var exact, out var prefixes))
        {
            return true;
        }

        foreach (var name in exact)
        {
            if (string.Equals(tableName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var prefix in prefixes)
        {
            if (tableName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
