namespace AAALogistics.Domain.Security;

/// <summary>
/// Permissions that may list master data (read-only) because the holder needs
/// it to pick materials, warehouses or parties in their own documents.
/// Create/edit keeps requiring the matching *.Manage permission.
/// </summary>
public static class MasterDataReadPermissions
{
    public static string[] Materials { get; } =
    [
        AppPermissions.MaterialManage,
        AppPermissions.StockRead,
        AppPermissions.SalesOrderCreate,
        AppPermissions.SalesOrderRead,
        AppPermissions.SalesOrderPrepare,
        AppPermissions.PurchaseOrderCreate,
        AppPermissions.GoodsReceiptPost,
        AppPermissions.InvoiceIssue,
        AppPermissions.ReturnManage,
    ];

    public static string[] Warehouses { get; } =
    [
        AppPermissions.WarehouseManage,
        AppPermissions.LocationManage,
        AppPermissions.StockRead,
        AppPermissions.StockTransfer,
        AppPermissions.SalesOrderCreate,
        AppPermissions.SalesOrderRead,
        AppPermissions.PurchaseOrderCreate,
        AppPermissions.GoodsReceiptPost,
        AppPermissions.DeliveryPost,
    ];

    public static string[] Suppliers { get; } =
    [
        AppPermissions.SupplierManage,
        AppPermissions.PurchaseOrderCreate,
        AppPermissions.GoodsReceiptPost,
    ];

    public static string[] Customers { get; } =
    [
        AppPermissions.CustomerManage,
        AppPermissions.SalesOrderCreate,
        AppPermissions.SalesOrderClose,
        AppPermissions.InvoiceIssue,
        AppPermissions.InvoicePrint,
        AppPermissions.InvoiceRectify,
        AppPermissions.DeliveryPost,
        AppPermissions.DeliveryNoteManage,
        AppPermissions.CmrManage,
        AppPermissions.ReturnManage,
    ];
}
