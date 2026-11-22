namespace AAALogistics.Contracts;

public static class ApiRoutes
{
    public const string InternalLogin = "/api/v1/internal/auth/login";
    public const string PortalLogin = "/api/v1/portal/auth/login";
    public const string InternalRefresh = "/api/v1/internal/auth/refresh";
    public const string PortalRefresh = "/api/v1/portal/auth/refresh";
    public const string InternalLogout = "/api/v1/internal/auth/logout";
    public const string PortalLogout = "/api/v1/portal/auth/logout";
    public const string InternalTwoFactorSetup = "/api/v1/internal/auth/2fa/setup";
    public const string InternalStepUp = "/api/v1/internal/auth/step-up";
    public const string InternalChangePassword = "/api/v1/internal/auth/change-password";
    public const string InternalMe = "/api/v1/internal/me";
    public const string InternalUsers = "/api/v1/internal/users";
    public const string InternalPing = "/api/v1/internal/ping";
    public const string PortalPing = "/api/v1/portal/ping";
    public const string InternalUnits = "/api/v1/internal/units";
    public const string InternalUnitsEnsureDefaults =
        "/api/v1/internal/units/ensure-defaults";
    public const string InternalMaterials = "/api/v1/internal/materials";
    public const string InternalWarehouses = "/api/v1/internal/warehouses";
    public const string InternalSuppliers = "/api/v1/internal/suppliers";
    public const string InternalCustomers = "/api/v1/internal/customers";
    public const string InternalPurchaseOrders =
        "/api/v1/internal/purchase-orders";
    public const string InternalGoodsReceipts =
        "/api/v1/internal/goods-receipts/post";
    public const string InternalGoodsReceiptsList =
        "/api/v1/internal/goods-receipts";
    public const string InternalDocumentNumberRanges =
        "/api/v1/internal/document-number-ranges";
    public const string InternalSalesOrders =
        "/api/v1/internal/sales-orders";
    public const string InternalQuotes =
        "/api/v1/internal/quotes";
    public const string InternalExportMaterials =
        "/api/v1/internal/exports/materials.xlsx";
    public const string InternalExportStock =
        "/api/v1/internal/exports/stock.xlsx";
    public const string InternalImportMaterialsPreview =
        "/api/v1/internal/imports/materials/preview";
    public const string InternalImportMaterialsCommit =
        "/api/v1/internal/imports/materials/commit";
    public const string PortalRegistration = "/api/v1/portal/registration";
    public const string PortalRegistrationPowChallenge =
        "/api/v1/portal/registration/pow-challenge";
    public const string InternalRfScan = "/api/v1/internal/rf/scan";
    public const string InternalHubOperations = "/hubs/operations";
    public const string PortalCatalog = "/api/v1/portal/catalog";
    public const string PortalSalesOrders = "/api/v1/portal/sales-orders";
    public const string InternalRegistrationRequests =
        "/api/v1/internal/registration-requests";
    public const string InternalDeliveries =
        "/api/v1/internal/deliveries/post";
    public const string InternalDeliveriesList =
        "/api/v1/internal/deliveries";
    public const string InternalStock = "/api/v1/internal/stock";
    public const string InternalMovements = "/api/v1/internal/stock/movements";
    public const string InternalStockBelowMinimum =
        "/api/v1/internal/stock/below-minimum";
    public const string InternalStockReconcile =
        "/api/v1/internal/stock/reconcile";
    public const string InternalStockTransfers =
        "/api/v1/internal/stock/transfers/post";
    public const string InternalStockLocationTransfers =
        "/api/v1/internal/stock/location-transfers";
    public const string InternalStockPutaway =
        "/api/v1/internal/stock/putaway";
    public const string InternalStockAdjustments =
        "/api/v1/internal/stock/adjustments/post";
    public const string InternalStockMinQuantity =
        "/api/v1/internal/stock/min-quantity";
    public const string InternalStockStatus =
        "/api/v1/internal/stock/status";
    public const string InternalStockCounts = "/api/v1/internal/stock/counts";
    public const string InternalReportsOrdersByStatus =
        "/api/v1/internal/reports/orders-by-status";
    public const string InternalReportsOrdersByStatusPdf =
        "/api/v1/internal/reports/orders-by-status/pdf";
    public const string InternalReportsOtif =
        "/api/v1/internal/reports/otif";
    public const string InternalReportsStockPdf =
        "/api/v1/internal/reports/stock/pdf";
    public const string InternalReportsOtifPdf =
        "/api/v1/internal/reports/otif/pdf";
    public const string InternalAuditEntries =
        "/api/v1/internal/audit/entries";
    public const string InternalAuditEntriesExport =
        "/api/v1/internal/audit/entries/export";
    public const string InternalAuditVerify =
        "/api/v1/internal/audit/verify";
    public const string InternalAuditCheckpoints =
        "/api/v1/internal/audit/checkpoints";
    public const string InternalAuditRetention =
        "/api/v1/internal/audit/retention";
    public const string InternalHandlingUnits =
        "/api/v1/internal/handling-units";
    public const string InternalSalesReturns =
        "/api/v1/internal/sales-returns/post";
    public const string InternalCarriers = "/api/v1/internal/carriers";
    public const string InternalCmr = "/api/v1/internal/cmr";
    public const string InternalInvoices = "/api/v1/internal/invoices";
    public const string InternalIssuerFiscal = "/api/v1/internal/issuer-fiscal";
    public const string InternalBranding = "/api/v1/internal/branding";
    public const string InternalBrandingLogo = "/api/v1/internal/branding/logo";
    public const string InternalMaterialDeletionRequests =
        "/api/v1/internal/material-deletion-requests";
    public const string InternalAdminTableEditEvaluate =
        "/api/v1/internal/admin/table-edit/evaluate";
    public const string PortalInvoices = "/api/v1/portal/invoices";
    public const string PortalWarehouses = "/api/v1/portal/warehouses";
    public const string PortalAtp = "/api/v1/portal/atp";
    public const string InternalWaves = "/api/v1/internal/waves";
    public const string InternalUnitConversions =
        "/api/v1/internal/unit-conversions";
    public const string InternalPurchaseRequisitions =
        "/api/v1/internal/purchase-requisitions";
    public const string InternalRateCards = "/api/v1/internal/rate-cards";
    public const string InternalTransportRoutes =
        "/api/v1/internal/transport-routes";
    public const string InternalCompanies = "/api/v1/internal/companies";
    public const string InternalExchangeRates =
        "/api/v1/internal/exchange-rates";
    public const string InternalCommercialTerms =
        "/api/v1/internal/commercial-terms";
    public const string InternalStudioBase = "/api/v1/internal/studio";
    public const string InternalStudioTaxCodes = $"{InternalStudioBase}/tax-codes";
    public const string InternalStudioPriceConditions = $"{InternalStudioBase}/price-conditions";
    public const string InternalStudioRma = $"{InternalStudioBase}/rma";
    public const string InternalStudioCreditSummary = $"{InternalStudioBase}/credit-summary";
    public const string InternalStudioFrameworkContracts = $"{InternalStudioBase}/framework-contracts";
    public const string InternalStudioRfq = $"{InternalStudioBase}/rfq";
    public const string InternalStudioThreeWayMatch = $"{InternalStudioBase}/three-way-match";
    public const string InternalStudioMaterialValuations = $"{InternalStudioBase}/material-valuations";
    public const string InternalStudioSupplierInvoices = $"{InternalStudioBase}/supplier-invoices";
    public const string InternalStudioGlAccounts = $"{InternalStudioBase}/gl-accounts";
    public const string InternalStudioGlAccountsSeedSpain =
        $"{InternalStudioBase}/gl-accounts/seed-spain";
    public const string InternalStudioCostCenters = $"{InternalStudioBase}/cost-centers";
    public const string InternalStudioJournals = $"{InternalStudioBase}/journals";
    public const string InternalStudioJournalReconciliation =
        $"{InternalStudioBase}/journals/reconciliation";
    public const string InternalStudioBoms = $"{InternalStudioBase}/boms";
    public const string InternalStudioRoutes = $"{InternalStudioBase}/routes";
    public const string InternalStudioProductionOrders = $"{InternalStudioBase}/production-orders";
    public const string InternalStudioMrpGenerate = $"{InternalStudioBase}/mrp/generate";
    public const string InternalStudioInspectionPlans = $"{InternalStudioBase}/inspection-plans";
    public const string InternalStudioQualityHolds = $"{InternalStudioBase}/quality-holds";
    public const string InternalStudioQualityCertificates = $"{InternalStudioBase}/quality-certificates";
    public const string InternalStudioPlants = $"{InternalStudioBase}/plants";
    public const string InternalStudioPlantWarehouses = $"{InternalStudioBase}/plant-warehouses";
    public const string InternalStudioUserOrgContext = $"{InternalStudioBase}/user-org-context";
    public const string InternalStudioApprovalRules = $"{InternalStudioBase}/approval-rules";
    public const string InternalStudioApprovalRequests = $"{InternalStudioBase}/approval-requests";
    public const string InternalStudioIntegrationConnectors =
        $"{InternalStudioBase}/integration-connectors";
    public const string InternalStudioIntegrationInbox = $"{InternalStudioBase}/integration-inbox";
    public const string InternalStudioIntegrationOutbox = $"{InternalStudioBase}/integration-outbox";
    public const string InternalStudioIntegrationMappings =
        $"{InternalStudioBase}/integration-mappings";
    public const string InternalStudioReportsValuedStock =
        $"{InternalStudioBase}/reports/valued-stock";
    public const string InternalStudioReportsAging = $"{InternalStudioBase}/reports/aging";
    public const string InternalStudioReportsOtif = $"{InternalStudioBase}/reports/otif";
    public const string InternalStudioEmployees = $"{InternalStudioBase}/employees";
    public const string InternalStudioDomainEvents = $"{InternalStudioBase}/domain-events";
    public const string InternalStudioPlugins = $"{InternalStudioBase}/plugins";
    public const string InternalStudioConfigTransport = $"{InternalStudioBase}/config-transport";
    public const string InternalSapMaterials =
        "/api/v1/internal/integrations/sap/materials";
    public const string InternalSapMaterialsSync =
        "/api/v1/internal/integrations/sap/materials/sync";
    public const string InternalSapOutbox =
        "/api/v1/internal/integrations/sap/outbox";
    public const string InternalSapStatus =
        "/api/v1/internal/integrations/sap/status";
    public const string HealthLive = "/health/live";
    public const string HealthReady = "/health/ready";

    public static string InternalSapPushDelivery(Guid deliveryId) =>
        $"/api/v1/internal/integrations/sap/deliveries/{deliveryId:D}/push";

    public static string InternalFxConvert =>
        $"{InternalExchangeRates}/convert";

    public static string InternalWaveRelease(Guid id) =>
        $"{InternalWaves}/{id:D}/release";

    public static string InternalUnitConvert =>
        $"{InternalUnitConversions}/convert";

    public static string InternalPurchaseRequisitionsGenerate =>
        $"{InternalPurchaseRequisitions}/generate";

    public static string InternalPurchaseRequisitionCancel(Guid id) =>
        $"{InternalPurchaseRequisitions}/{id:D}/cancel";

    public static string InternalRateCardCompute(Guid id) =>
        $"{InternalRateCards}/{id:D}/compute";

    public static string InternalTransportRouteStops(Guid id) =>
        $"{InternalTransportRoutes}/{id:D}/stops";

    public static string PortalInvoicePdf(Guid id) =>
        $"{PortalInvoices}/{id:D}/pdf";

    public static string InternalHandlingUnitsByOrder(Guid salesOrderId) =>
        $"{InternalHandlingUnits}?salesOrderId={salesOrderId:D}";

    public static string InternalPackingListPdf(Guid deliveryId) =>
        $"/api/v1/internal/deliveries/{deliveryId:D}/packing-list.pdf";

    public static string InternalSalesOrderTradeTerms(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/trade-terms";

    public static string InternalWarehouseLocations(Guid warehouseId) =>
        $"{InternalWarehouses}/{warehouseId:D}/locations";

    public static string InternalWarehouseLocationById(Guid warehouseId, Guid locationId) =>
        $"{InternalWarehouses}/{warehouseId:D}/locations/{locationId:D}";


    public static string InternalSalesOrderPrep(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/prep";

    public static string InternalSalesOrderPrepJoin(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/prep/join";

    public static string InternalSalesOrderPrepLeave(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/prep/leave";

    public static string InternalSalesOrderMarkPrepared(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/prep/mark-prepared";

    public static string InternalSalesOrderPrepPicks(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/prep/picks";

    public static string InternalSalesOrderPrepPickConfirm(Guid id, Guid pickId) =>
        $"{InternalSalesOrders}/{id:D}/prep/picks/{pickId:D}/confirm";

    public static string InternalUnitById(Guid unitId) =>
        $"{InternalUnits}/{unitId:D}";

    public static string InternalMaterialById(Guid materialId) =>
        $"{InternalMaterials}/{materialId:D}";

    public static string InternalWarehouseById(Guid warehouseId) =>
        $"{InternalWarehouses}/{warehouseId:D}";

    public static string InternalSupplierById(Guid supplierId) =>
        $"{InternalSuppliers}/{supplierId:D}";

    public static string InternalCustomerById(Guid customerId) =>
        $"{InternalCustomers}/{customerId:D}";

    public static string InternalMaterialLots(Guid materialId) =>
        $"{InternalMaterials}/{materialId:D}/lots";

    public static string InternalCmrPdf(Guid id) =>
        $"{InternalCmr}/{id:D}/pdf";

    public static string InternalInvoicePdf(Guid id) =>
        $"{InternalInvoices}/{id:D}/pdf";

    public static string InternalInvoiceRectify(Guid id) =>
        $"{InternalInvoices}/{id:D}/rectify";

    public static string InternalMaterialDeletionRequest(Guid materialId) =>
        $"{InternalMaterials}/{materialId:D}/deletion-requests";

    public static string InternalMaterialDeletionApprove(Guid requestId) =>
        $"{InternalMaterialDeletionRequests}/{requestId:D}/approve";

    public static string InternalMaterialDeletionReject(Guid requestId) =>
        $"{InternalMaterialDeletionRequests}/{requestId:D}/reject";

    public static string InternalPurchaseOrderById(Guid id) =>
        $"{InternalPurchaseOrders}/{id:D}";

    public static string InternalPurchaseOrderCancelOpen(Guid id) =>
        $"{InternalPurchaseOrders}/{id:D}/cancel-open";

    public static string InternalPurchaseOrderClose(Guid id) =>
        $"{InternalPurchaseOrders}/{id:D}/close";

    public static string InternalPurchaseOrderPdf(Guid id) =>
        $"{InternalPurchaseOrders}/{id:D}/pdf";

    public static string InternalGoodsReceiptPdf(Guid id) =>
        $"{InternalGoodsReceiptsList}/{id:D}/pdf";

    public static string InternalSalesOrderPdf(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/pdf";

    public static string InternalQuoteById(Guid id) =>
        $"{InternalQuotes}/{id:D}";

    public static string InternalQuotePdf(Guid id) =>
        $"{InternalQuotes}/{id:D}/pdf";

    public static string InternalQuoteSend(Guid id) =>
        $"{InternalQuotes}/{id:D}/send";

    public static string InternalQuoteAccept(Guid id) =>
        $"{InternalQuotes}/{id:D}/accept";

    public static string InternalQuoteReject(Guid id) =>
        $"{InternalQuotes}/{id:D}/reject";

    public static string InternalSalesOrderConfirm(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/confirm";

    public static string InternalSalesOrderCancel(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/cancel";

    public static string InternalSalesOrderClose(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/close";

    public static string InternalSalesOrderReject(Guid id) =>
        $"{InternalSalesOrders}/{id:D}/reject";

    public static string InternalDeliveryPdf(Guid id) =>
        $"/api/v1/internal/deliveries/{id:D}/pdf";

    public static string InternalMovementReverse(Guid id) =>
        $"{InternalMovements}/{id:D}/reverse";

    public static string InternalStockCountPost(Guid id) =>
        $"{InternalStockCounts}/{id:D}/post";

    public static string InternalRegistrationApprove(Guid id) =>
        $"{InternalRegistrationRequests}/{id:D}/approve";

    public static string InternalRegistrationReject(Guid id) =>
        $"{InternalRegistrationRequests}/{id:D}/reject";

    public static string InternalUserPermissions(Guid id) =>
        $"{InternalUsers}/{id:D}/permissions";

    public static string InternalUserRanges(Guid id) =>
        $"{InternalUsers}/{id:D}/ranges";

    public static string InternalUserCompany(Guid id) =>
        $"{InternalUsers}/{id:D}/company";

    public static string InternalUserSuspend(Guid id) =>
        $"{InternalUsers}/{id:D}/suspend";

    public static string InternalUserReactivate(Guid id) =>
        $"{InternalUsers}/{id:D}/reactivate";

    public static string InternalUserTerminate(Guid id) =>
        $"{InternalUsers}/{id:D}/terminate";

    public static string InternalUserResetPassword(Guid id) =>
        $"{InternalUsers}/{id:D}/reset-password";

    public static string InternalStudioEmployeeById(Guid id) =>
        $"{InternalStudioEmployees}/{id:D}";

    public static string InternalStudioPluginInvoke(Guid pluginId) =>
        $"{InternalStudioPlugins}/{pluginId:D}/invoke";

    public static string InternalStudioQualityHoldRelease(Guid holdId) =>
        $"{InternalStudioQualityHolds}/{holdId:D}/release";

    public static string InternalStudioQualityHoldReject(Guid holdId) =>
        $"{InternalStudioQualityHolds}/{holdId:D}/reject";

    public static string InternalStudioProductionOrderRelease(Guid orderId) =>
        $"{InternalStudioProductionOrders}/{orderId:D}/release";

    public static string InternalStudioProductionOrderComplete(Guid orderId) =>
        $"{InternalStudioProductionOrders}/{orderId:D}/complete";

    public static string InternalStudioSupplierInvoiceJournal(Guid invoiceId) =>
        $"{InternalStudioSupplierInvoices}/{invoiceId:D}/journal";

    public static string InternalStudioApprovalRequestApprove(Guid requestId) =>
        $"{InternalStudioApprovalRequests}/{requestId:D}/approve";

    public static string InternalStudioApprovalRequestReject(Guid requestId) =>
        $"{InternalStudioApprovalRequests}/{requestId:D}/reject";
}
