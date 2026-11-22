namespace AAALogistics.Domain.Security;

/// <summary>
/// Absolute blacklist for any future admin table editor.
/// These tables/columns are never editable via generic row editors.
/// </summary>
public static class AdminTableBlacklist
{
    public static IReadOnlySet<string> Tables { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "stock_balances",
            "stock_movements",
            "stock_reservations",
            "goods_receipts",
            "goods_receipt_lines",
            "deliveries",
            "delivery_lines",
            "invoices",
            "invoice_lines",
            "invoice_series",
            "issuer_fiscal_profiles",
            "order_prep_sessions",
            "cmr_documents",
            "location_nodes",
            "material_deletion_requests",
            "lots",
            "prep_pick_lines",
            "handling_units",
            "audit_log_entries",
            "audit_chain_heads",
            "person_data_keys",
            "refresh_tokens",
            "identity_users",
            "idempotency_records",
        };

    public static IReadOnlySet<string> Columns { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password_hash",
            "security_stamp",
            "concurrency_stamp",
            "authenticator_key",
            "key_material",
            "token_hash",
            "entry_hash",
            "prev_hash",
            "quantity",
            "signed_quantity",
            "status",
            "confirmed_quantity",
            "posted_quantity",
            "cancelled_quantity",
        };

    public static bool IsForbidden(string tableName) =>
        Tables.Contains(tableName);

    public static bool IsColumnForbidden(string columnName) =>
        Columns.Contains(columnName);

    public static (bool Allowed, string Message) EvaluateEdit(
        string tableName,
        IEnumerable<string> columns)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return (false, "La tabla es obligatoria.");
        }

        if (IsForbidden(tableName))
        {
            return (
                false,
                $"La tabla '{tableName}' está en la lista negra absoluta.");
        }

        foreach (var column in columns)
        {
            if (IsColumnForbidden(column))
            {
                return (
                    false,
                    $"La columna '{column}' está prohibida en el editor.");
            }
        }

        return (
            true,
            "Edición permitida para administrador con backup previo.");
    }
}
