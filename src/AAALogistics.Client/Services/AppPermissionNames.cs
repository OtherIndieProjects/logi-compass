namespace AAALogistics.Client.Services;

internal static class AppPermissionNames
{
    public const string MaterialManage = "Material.Manage";
    public const string MaterialDelete = "Material.Delete";
    public const string MaterialDeleteApprove = "Material.DeleteApprove";
    public const string WarehouseManage = "Warehouse.Manage";
    public const string LocationManage = "Location.Manage";
    public const string SupplierManage = "Supplier.Manage";
    public const string CustomerManage = "Customer.Manage";
    public const string CarrierManage = "Carrier.Manage";
    public const string PurchaseOrderCreate = "PurchaseOrder.Create";
    public const string PurchaseOrderCancelOpen = "PurchaseOrder.CancelOpen";
    public const string PurchaseOrderClose = "PurchaseOrder.Close";
    public const string GoodsReceiptPost = "GoodsReceipt.Post";
    public const string SalesOrderCreate = "SalesOrder.Create";
    public const string SalesOrderRead = "SalesOrder.Read";
    public const string SalesOrderConfirm = "SalesOrder.Confirm";
    public const string SalesOrderPrepare = "SalesOrder.Prepare";
    public const string SalesOrderCancel = "SalesOrder.Cancel";
    public const string SalesOrderClose = "SalesOrder.Close";
    public const string DeliveryPost = "Delivery.Post";
    public const string DeliveryNoteManage = "DeliveryNote.Manage";
    public const string CmrManage = "Cmr.Manage";
    public const string InvoiceIssue = "Invoice.Issue";
    public const string InvoiceRectify = "Invoice.Rectify";
    public const string InvoicePrint = "Invoice.Print";
    public const string InvoiceConfig = "Invoice.Config";
    public const string ReturnManage = "Return.Manage";
    public const string StockRead = "Stock.Read";
    public const string StockTransfer = "Stock.Transfer";
    public const string StockAdjust = "Stock.Adjust";
    public const string StockReverse = "Stock.Reverse";
    public const string StockCount = "Stock.Count";
    public const string ReportRead = "Report.Read";
    public const string RegistrationApprove = "Registration.Approve";
    public const string RegistrationReject = "Registration.Reject";
    public const string UserRead = "User.Read";
    public const string UserCreate = "User.Create";
    public const string UserManagePermissions = "User.ManagePermissions";
    public const string UserManageRanges = "User.ManageRanges";
    public const string UserSuspend = "User.Suspend";
    public const string UserReactivate = "User.Reactivate";
    public const string UserTerminate = "User.Terminate";
    public const string AdminTableEdit = "Admin.TableEdit";
    public const string AuditVerify = "Audit.Verify";
    public const string BackupManage = "Backup.Manage";
    public const string CustomerSuspend = "Customer.Suspend";
    public const string StockOpeningImport = "Stock.OpeningImport";
    public const string RegistrationExpire = "Registration.Expire";
    public const string SalesOrderCreateOwn = "SalesOrder.CreateOwn";

    public static IReadOnlyList<string> All { get; } =
    [
        MaterialManage,
        MaterialDelete,
        MaterialDeleteApprove,
        WarehouseManage,
        LocationManage,
        SupplierManage,
        CustomerManage,
        CarrierManage,
        PurchaseOrderCreate,
        PurchaseOrderCancelOpen,
        PurchaseOrderClose,
        GoodsReceiptPost,
        SalesOrderCreate,
        SalesOrderRead,
        SalesOrderCreateOwn,
        SalesOrderConfirm,
        SalesOrderPrepare,
        SalesOrderCancel,
        SalesOrderClose,
        DeliveryPost,
        DeliveryNoteManage,
        CmrManage,
        InvoiceIssue,
        InvoiceRectify,
        InvoicePrint,
        InvoiceConfig,
        ReturnManage,
        StockRead,
        StockTransfer,
        StockAdjust,
        StockReverse,
        StockCount,
        StockOpeningImport,
        AuditVerify,
        BackupManage,
        RegistrationApprove,
        RegistrationReject,
        RegistrationExpire,
        UserRead,
        UserCreate,
        UserManagePermissions,
        UserManageRanges,
        UserSuspend,
        UserReactivate,
        UserTerminate,
        CustomerSuspend,
        AdminTableEdit,
        ReportRead,
    ];

    public static IReadOnlyList<string> InternalRanges { get; } =
    [
        "Admin",
        "Gerente",
        "Compras",
        "Ventas",
        "Almacen",
        "Finanzas",
        "Calidad",
    ];
}
