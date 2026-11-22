namespace AAALogistics.Domain.Security;

public static class RangoPermissionCatalog
{
    public static IReadOnlyList<string> ForRole(string roleName) =>
        roleName switch
        {
            AppRoles.Admin => AppPermissions.All,
            AppRoles.Gerente => Gerente,
            AppRoles.Compras => Compras,
            AppRoles.Ventas => Ventas,
            AppRoles.Almacen => Almacen,
            AppRoles.Finanzas => Finanzas,
            AppRoles.Calidad => Calidad,
            AppRoles.Customer => Cliente,
            // Legado: misma matriz que el destino SAP (por si JWT llega antes de migrar).
            AppRoles.GerenteLogistica => Gerente,
            AppRoles.CoordinadorLogistica => UnionForRoles([AppRoles.Almacen, AppRoles.Compras]),
            AppRoles.Administrativo => Almacen,
            AppRoles.AsistenteLogistica => Almacen,
            AppRoles.OperarioLogistica => Almacen,
            AppRoles.Worker => Almacen,
            _ => [],
        };

    public static IReadOnlyList<string> UnionForRoles(IEnumerable<string> roles)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            foreach (var permission in ForRole(role))
            {
                set.Add(permission);
            }
        }

        return set.Order(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> Gerente { get; } =
        AppPermissions.All
            .Where(permission =>
                permission != AppPermissions.SalesOrderCreateOwn
                && permission != AppPermissions.UserTerminate)
            .ToArray();

    private static IReadOnlyList<string> Compras { get; } =
    [
        AppPermissions.MaterialManage,
        AppPermissions.SupplierManage,
        AppPermissions.PurchaseOrderCreate,
        AppPermissions.PurchaseOrderCancelOpen,
        AppPermissions.PurchaseOrderClose,
        AppPermissions.GoodsReceiptPost,
        AppPermissions.StockRead,
        AppPermissions.ReportRead,
    ];

    private static IReadOnlyList<string> Ventas { get; } =
    [
        AppPermissions.CustomerManage,
        AppPermissions.SalesOrderCreate,
        AppPermissions.SalesOrderRead,
        AppPermissions.SalesOrderConfirm,
        AppPermissions.SalesOrderPrepare,
        AppPermissions.SalesOrderCancel,
        AppPermissions.DeliveryPost,
        AppPermissions.DeliveryNoteManage,
        AppPermissions.InvoiceIssue,
        AppPermissions.InvoiceRectify,
        AppPermissions.InvoicePrint,
        AppPermissions.ReturnManage,
        AppPermissions.StockRead,
        AppPermissions.ReportRead,
    ];

    private static IReadOnlyList<string> Almacen { get; } =
    [
        AppPermissions.MaterialManage,
        AppPermissions.MaterialDeleteApprove,
        AppPermissions.WarehouseManage,
        AppPermissions.LocationManage,
        AppPermissions.CarrierManage,
        AppPermissions.SalesOrderRead,
        AppPermissions.SalesOrderPrepare,
        AppPermissions.SalesOrderClose,
        AppPermissions.DeliveryPost,
        AppPermissions.DeliveryNoteManage,
        AppPermissions.CmrManage,
        AppPermissions.InvoicePrint,
        AppPermissions.StockRead,
        AppPermissions.StockTransfer,
        AppPermissions.StockAdjust,
        AppPermissions.StockReverse,
        AppPermissions.StockCount,
        AppPermissions.StockOpeningImport,
        AppPermissions.ReportRead,
    ];

    private static IReadOnlyList<string> Finanzas { get; } =
    [
        AppPermissions.InvoiceIssue,
        AppPermissions.InvoiceRectify,
        AppPermissions.InvoicePrint,
        AppPermissions.InvoiceConfig,
        AppPermissions.StockRead,
        AppPermissions.ReportRead,
        AppPermissions.AuditVerify,
    ];

    private static IReadOnlyList<string> Calidad { get; } =
    [
        AppPermissions.MaterialManage,
        AppPermissions.StockRead,
        AppPermissions.StockAdjust,
        AppPermissions.ReportRead,
        AppPermissions.AuditVerify,
    ];

    private static IReadOnlyList<string> Cliente { get; } =
    [
        AppPermissions.SalesOrderCreateOwn,
        AppPermissions.InvoicePrint,
    ];
}
