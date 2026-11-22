using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AAALogistics.Contracts;
using Avalonia.Controls;

namespace AAALogistics.Client.Services;

internal sealed partial class LogisticsApiClient : IDisposable
{
    public const string DefaultBaseUrl = "http://127.0.0.1:5088";
    public const string StepUpHeaderName = "X-AAA-StepUp";
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private string? _refreshToken;
    private DateTimeOffset _accessExpiresUtc;
    private string? _stepUpToken;
    private DateTimeOffset _stepUpExpiresUtc;

    public LogisticsApiClient(string? baseUrl = null)
    {
        var configured = Environment.GetEnvironmentVariable("AAA_API_BASE_URL");
        var selected = string.IsNullOrWhiteSpace(baseUrl)
            ? configured
            : baseUrl;
        if (string.IsNullOrWhiteSpace(selected))
        {
            selected = DefaultBaseUrl;
        }

        BaseUrl = selected.TrimEnd('/');
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        Permissions = new PermissionGate();
        Masters = new MasterDataCache(this);
    }

    public string BaseUrl { get; }

    public bool HasToken { get; private set; }

    public PermissionGate Permissions { get; }

    public MasterDataCache Masters { get; }

    public DateTimeOffset? StepUpExpiresUtc =>
        string.IsNullOrWhiteSpace(_stepUpToken) ? null : _stepUpExpiresUtc;

    public void Dispose() => _http.Dispose();

    public async Task LoginAsync(
        string userName,
        string password,
        string? totpCode = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(
            ApiRoutes.InternalLogin,
            new LoginRequest(userName, password, totpCode),
            JsonOptions,
            cancellationToken);
        var payload = await ReadAsync<LoginResponse>(response, cancellationToken);
        ApplySession(payload);
        await RefreshIdentityAsync(cancellationToken);
    }

    public async Task RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
        {
            throw new ApiException("No hay refresh token.", "AAA-AUTH-REFRESH");
        }

        using var response = await _http.PostAsJsonAsync(
            ApiRoutes.InternalRefresh,
            new RefreshRequest(_refreshToken),
            JsonOptions,
            cancellationToken);
        var payload = await ReadAsync<LoginResponse>(response, cancellationToken);
        ApplySession(payload);
        await RefreshIdentityAsync(cancellationToken);
    }

    public async Task EnsureAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!HasToken)
        {
            return;
        }

        if (_accessExpiresUtc > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return;
        }

        await RefreshSessionAsync(cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_refreshToken))
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(
                    ApiRoutes.InternalLogout,
                    new LogoutRequest(_refreshToken),
                    JsonOptions,
                    cancellationToken);
                _ = response.StatusCode;
            }
            catch (HttpRequestException)
            {
            }
        }

        _http.DefaultRequestHeaders.Authorization = null;
        _refreshToken = null;
        _accessExpiresUtc = default;
        HasToken = false;
        ClearStepUp();
        Permissions.Clear();
    }

    public Task<PingRow> PingAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PingRow>(ApiRoutes.InternalPing, cancellationToken);

    public async Task<CurrentUserResponse> GetMeAsync(
        CancellationToken cancellationToken = default)
    {
        var me = await GetAsync<CurrentUserResponse>(
            ApiRoutes.InternalMe,
            cancellationToken);
        Permissions.Replace(me.Roles, me.Permissions);
        return me;
    }

    public async Task RefreshIdentityAsync(CancellationToken cancellationToken = default)
    {
        _ = await GetMeAsync(cancellationToken);
    }

    private void ApplySession(LoginResponse payload)
    {
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.AccessToken);
        _refreshToken = payload.RefreshToken;
        _accessExpiresUtc = payload.ExpiresAtUtc;
        HasToken = true;
        ClearStepUp();
    }

    public async Task ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        using var response = await _http.PostAsJsonAsync(
            ApiRoutes.InternalChangePassword,
            new ChangePasswordRequest(currentPassword, newPassword),
            JsonOptions,
            cancellationToken);
        await ReadAsync<object>(response, cancellationToken);
    }

    public Task<ResetUserPasswordResponse> ResetUserPasswordAsync(
        Window owner,
        Guid userId,
        bool resetTwoFactor,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<ResetUserPasswordResponse>(
            owner,
            ApiRoutes.InternalUserResetPassword(userId),
            new ResetUserPasswordRequest(resetTwoFactor),
            requireIdempotency: false,
            cancellationToken);

    public Task EnsureStepUpAsync(
        Window owner,
        CancellationToken cancellationToken = default)
    {
        // TOTP/step-up removed: critical ops rely on roles/permissions (+ UI confirm).
        _ = owner;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UnitRow>> GetUnitsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<UnitRow>(ApiRoutes.InternalUnits, cancellationToken);

    public Task<UnitRow> CreateUnitAsync(
        CreateUnitOfMeasureRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<UnitRow>(ApiRoutes.InternalUnits, request, cancellationToken);

    public Task<UnitRow> UpdateUnitAsync(
        Guid unitId,
        UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<UnitRow>(
            ApiRoutes.InternalUnitById(unitId),
            request,
            cancellationToken);

    public Task<EnsureDefaultUnitsRow> EnsureDefaultUnitsAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<EnsureDefaultUnitsRow>(
            ApiRoutes.InternalUnitsEnsureDefaults,
            body: null,
            cancellationToken);

    public Task<IReadOnlyList<MaterialRow>> GetMaterialsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<MaterialRow>(ApiRoutes.InternalMaterials, cancellationToken);

    public Task<MaterialRow> CreateMaterialAsync(
        CreateMaterialRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<MaterialRow>(ApiRoutes.InternalMaterials, request, cancellationToken);

    public Task<MaterialRow> UpdateMaterialAsync(
        Guid materialId,
        UpdateMaterialRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<MaterialRow>(
            ApiRoutes.InternalMaterialById(materialId),
            request,
            cancellationToken);

    public Task<IReadOnlyList<WarehouseRow>> GetWarehousesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<WarehouseRow>(ApiRoutes.InternalWarehouses, cancellationToken);

    public Task<WarehouseRow> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<WarehouseRow>(
            ApiRoutes.InternalWarehouses,
            request,
            cancellationToken);

    public Task<WarehouseRow> UpdateWarehouseAsync(
        Guid warehouseId,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<WarehouseRow>(
            ApiRoutes.InternalWarehouseById(warehouseId),
            request,
            cancellationToken);

    public Task DeleteWarehouseAsync(
        Window owner,
        Guid warehouseId,
        CancellationToken cancellationToken = default) =>
        SendCriticalWithStepUpAsync<object>(
            owner,
            HttpMethod.Delete,
            ApiRoutes.InternalWarehouseById(warehouseId),
            body: null,
            requireIdempotency: false,
            cancellationToken);

    public Task<IReadOnlyList<PartyRow>> GetSuppliersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PartyRow>(ApiRoutes.InternalSuppliers, cancellationToken);

    public Task<PartyRow> CreateSupplierAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<PartyRow>(ApiRoutes.InternalSuppliers, request, cancellationToken);

    public Task<PartyRow> UpdateSupplierAsync(
        Guid supplierId,
        UpdatePartyRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<PartyRow>(
            ApiRoutes.InternalSupplierById(supplierId),
            request,
            cancellationToken);

    public Task<IReadOnlyList<PartyRow>> GetCustomersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PartyRow>(ApiRoutes.InternalCustomers, cancellationToken);

    public Task<PartyRow> CreateCustomerAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<PartyRow>(ApiRoutes.InternalCustomers, request, cancellationToken);

    public Task<PartyRow> UpdateCustomerAsync(
        Guid customerId,
        UpdatePartyRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<PartyRow>(
            ApiRoutes.InternalCustomerById(customerId),
            request,
            cancellationToken);

    public Task<IReadOnlyList<PurchaseOrderRow>> GetPurchaseOrdersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PurchaseOrderRow>(
            ApiRoutes.InternalPurchaseOrders,
            cancellationToken);

    public Task<PurchaseOrderRow> GetPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        GetAsync<PurchaseOrderRow>(
            ApiRoutes.InternalPurchaseOrderById(purchaseOrderId),
            cancellationToken);

    public Task<PurchaseOrderRow> CreatePurchaseOrderAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<PurchaseOrderRow>(
            ApiRoutes.InternalPurchaseOrders,
            request,
            cancellationToken);

    public Task<IReadOnlyList<InternalUserResponse>> GetUsersAsync(
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(role)
            ? ApiRoutes.InternalUsers
            : $"{ApiRoutes.InternalUsers}?role={Uri.EscapeDataString(role)}";
        return GetListAsync<InternalUserResponse>(url, cancellationToken);
    }

    public Task<CreateInternalUserResponse> CreateWorkerAsync(
        Window owner,
        CreateInternalUserRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<CreateInternalUserResponse>(
            owner,
            ApiRoutes.InternalUsers,
            request,
            requireIdempotency: false,
            cancellationToken);

    public Task<UserPermissionsResponse> GetUserPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        GetAsync<UserPermissionsResponse>(
            ApiRoutes.InternalUserPermissions(userId),
            cancellationToken);

    public Task<UserPermissionsResponse> SetUserPermissionsAsync(
        Window owner,
        Guid userId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default) =>
        PutCriticalAsync<UserPermissionsResponse>(
            owner,
            ApiRoutes.InternalUserPermissions(userId),
            new SetUserPermissionsRequest(permissions),
            cancellationToken);

    public Task SetUserRangesAsync(
        Window owner,
        Guid userId,
        IReadOnlyCollection<string> ranges,
        CancellationToken cancellationToken = default) =>
        PutCriticalAsync<object>(
            owner,
            ApiRoutes.InternalUserRanges(userId),
            new SetUserRangesRequest(ranges),
            cancellationToken);

    public Task<IReadOnlyList<LocationNodeResponse>> GetLocationsAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default) =>
        GetListAsync<LocationNodeResponse>(
            ApiRoutes.InternalWarehouseLocations(warehouseId),
            cancellationToken);

    public Task<LocationNodeResponse> CreateLocationAsync(
        Guid warehouseId,
        CreateLocationNodeRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<LocationNodeResponse>(
            ApiRoutes.InternalWarehouseLocations(warehouseId),
            request,
            cancellationToken);

    public Task<LocationNodeResponse> UpdateLocationAsync(
        Guid warehouseId,
        Guid locationId,
        UpdateLocationNodeRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<LocationNodeResponse>(
            ApiRoutes.InternalWarehouseLocationById(warehouseId, locationId),
            request,
            cancellationToken);

    public Task DeleteLocationAsync(
        Window owner,
        Guid warehouseId,
        Guid locationId,
        CancellationToken cancellationToken = default) =>
        SendCriticalWithStepUpAsync<object>(
            owner,
            HttpMethod.Delete,
            ApiRoutes.InternalWarehouseLocationById(warehouseId, locationId),
            body: null,
            requireIdempotency: false,
            cancellationToken);

    public Task<IReadOnlyList<OrderPrepSessionResponse>> GetPrepAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        GetListAsync<OrderPrepSessionResponse>(
            ApiRoutes.InternalSalesOrderPrep(salesOrderId),
            cancellationToken);

    public async Task<IReadOnlyList<OrderPrepSessionResponse>> JoinPrepAsync(
        Guid salesOrderId,
        string mode,
        CancellationToken cancellationToken = default)
    {
        var rows = await PostAsync<List<OrderPrepSessionResponse>>(
            ApiRoutes.InternalSalesOrderPrepJoin(salesOrderId),
            new JoinPrepRequestDto(mode),
            cancellationToken);
        return rows ?? [];
    }

    public Task LeavePrepAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        PostAsync<object>(
            ApiRoutes.InternalSalesOrderPrepLeave(salesOrderId),
            null,
            cancellationToken);

    public Task MarkPreparedAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        PostAsync<object>(
            ApiRoutes.InternalSalesOrderMarkPrepared(salesOrderId),
            null,
            cancellationToken);

    public Task<IReadOnlyList<PrepPickLineResponse>> GetPrepPicksAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        GetListAsync<PrepPickLineResponse>(
            ApiRoutes.InternalSalesOrderPrepPicks(salesOrderId),
            cancellationToken);

    public Task<PrepPickLineResponse> ConfirmPrepPickAsync(
        Guid salesOrderId,
        Guid pickId,
        decimal confirmedQuantity,
        CancellationToken cancellationToken = default) =>
        PostAsync<PrepPickLineResponse>(
            ApiRoutes.InternalSalesOrderPrepPickConfirm(salesOrderId, pickId),
            new ConfirmPrepPickRequestDto(confirmedQuantity),
            cancellationToken);

    public Task<IReadOnlyList<CarrierResponse>> GetCarriersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<CarrierResponse>(ApiRoutes.InternalCarriers, cancellationToken);

    public Task<CarrierResponse> CreateCarrierAsync(
        CreateCarrierRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CarrierResponse>(ApiRoutes.InternalCarriers, request, cancellationToken);

    public Task<CmrResponse> CreateCmrAsync(
        CreateCmrRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CmrResponse>(ApiRoutes.InternalCmr, request, cancellationToken);

    public Task<byte[]> DownloadCmrPdfAsync(
        Guid cmrId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalCmrPdf(cmrId), cancellationToken);

    public Task<IReadOnlyList<InvoiceResponse>> GetInvoicesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<InvoiceResponse>(ApiRoutes.InternalInvoices, cancellationToken);

    public Task<InvoiceResponse> IssueInvoiceAsync(
        Window owner,
        IssueInvoiceRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<InvoiceResponse>(
            owner,
            ApiRoutes.InternalInvoices,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<InvoiceResponse> RectifyInvoiceAsync(
        Window owner,
        Guid invoiceId,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<InvoiceResponse>(
            owner,
            ApiRoutes.InternalInvoiceRectify(invoiceId),
            new RectifyInvoiceRequestDto(reason),
            requireIdempotency: true,
            cancellationToken);

    public Task<byte[]> DownloadInvoicePdfAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalInvoicePdf(invoiceId), cancellationToken);

    public Task<IssuerFiscalResponse?> GetIssuerAsync(
        CancellationToken cancellationToken = default) =>
        GetOptionalAsync<IssuerFiscalResponse>(
            ApiRoutes.InternalIssuerFiscal,
            cancellationToken);

    public Task UpsertIssuerAsync(
        Window owner,
        UpsertIssuerRequestDto request,
        CancellationToken cancellationToken = default) =>
        PutCriticalAsync<object>(
            owner,
            ApiRoutes.InternalIssuerFiscal,
            request,
            cancellationToken);

    public Task<AppBrandingResponse> GetBrandingAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<AppBrandingResponse>(ApiRoutes.InternalBranding, cancellationToken);

    public Task<AppBrandingResponse> UpsertBrandingAsync(
        UpsertAppBrandingRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<AppBrandingResponse>(
            ApiRoutes.InternalBranding,
            request,
            cancellationToken);

    public Task<byte[]> DownloadBrandingLogoAsync(
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalBrandingLogo, cancellationToken);

    public Task<MaterialDeletionResponse> RequestMaterialDeletionAsync(
        Window owner,
        Guid materialId,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<MaterialDeletionResponse>(
            owner,
            ApiRoutes.InternalMaterialDeletionRequest(materialId),
            new MaterialDeletionRequestDto(reason),
            requireIdempotency: false,
            cancellationToken);

    public Task ApproveMaterialDeletionAsync(
        Window owner,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<object>(
            owner,
            ApiRoutes.InternalMaterialDeletionApprove(requestId),
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task RejectMaterialDeletionAsync(
        Window owner,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<object>(
            owner,
            $"{ApiRoutes.InternalMaterialDeletionRequests}/{requestId:D}/reject",
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task<IReadOnlyList<MaterialDeletionResponse>> GetMaterialDeletionRequestsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<MaterialDeletionResponse>(
            ApiRoutes.InternalMaterialDeletionRequests,
            cancellationToken);

    public Task<PostingRow> TransferLocationAsync(
        Window owner,
        TransferLocationRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalStockLocationTransfers,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task SuspendUserAsync(
        Window owner,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<object>(
            owner,
            ApiRoutes.InternalUserSuspend(userId),
            new SuspendUserRequest(reason),
            requireIdempotency: false,
            cancellationToken);

    public Task ReactivateUserAsync(
        Window owner,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<object>(
            owner,
            ApiRoutes.InternalUserReactivate(userId),
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task TerminateUserAsync(
        Window owner,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<object>(
            owner,
            ApiRoutes.InternalUserTerminate(userId),
            new SuspendUserRequest(reason),
            requireIdempotency: false,
            cancellationToken);

    public Task<PurchaseOrderRow> CancelPurchaseOrderOpenAsync(
        Window owner,
        Guid purchaseOrderId,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PurchaseOrderRow>(
            owner,
            ApiRoutes.InternalPurchaseOrderCancelOpen(purchaseOrderId),
            new CancelReasonRequest(reason),
            requireIdempotency: false,
            cancellationToken);

    public Task<PurchaseOrderRow> ClosePurchaseOrderAsync(
        Window owner,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PurchaseOrderRow>(
            owner,
            ApiRoutes.InternalPurchaseOrderClose(purchaseOrderId),
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task<byte[]> DownloadPurchaseOrderPdfAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(
            ApiRoutes.InternalPurchaseOrderPdf(purchaseOrderId),
            cancellationToken);

    public Task<byte[]> DownloadSalesOrderPdfAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(
            ApiRoutes.InternalSalesOrderPdf(salesOrderId),
            cancellationToken);

    public Task<IReadOnlyList<QuoteResponse>> GetQuotesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<QuoteResponse>(ApiRoutes.InternalQuotes, cancellationToken);

    public Task<QuoteResponse> CreateQuoteAsync(
        CreateQuoteRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<QuoteResponse>(ApiRoutes.InternalQuotes, request, cancellationToken);

    public Task<QuoteResponse> SendQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default) =>
        PostAsync<QuoteResponse>(ApiRoutes.InternalQuoteSend(quoteId), null, cancellationToken);

    public Task<QuoteResponse> RejectQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default) =>
        PostAsync<QuoteResponse>(ApiRoutes.InternalQuoteReject(quoteId), null, cancellationToken);

    public Task<QuoteResponse> AcceptQuoteAsync(
        Window owner,
        Guid quoteId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<QuoteResponse>(
            owner,
            ApiRoutes.InternalQuoteAccept(quoteId),
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task<byte[]> DownloadQuotePdfAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalQuotePdf(quoteId), cancellationToken);

    public Task<byte[]> DownloadOtifPdfAsync(
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalReportsOtifPdf, cancellationToken);

    public Task<byte[]> DownloadOrdersByStatusPdfAsync(
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(
            ApiRoutes.InternalReportsOrdersByStatusPdf,
            cancellationToken);

    public Task<byte[]> DownloadStockPdfAsync(
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalReportsStockPdf, cancellationToken);

    public Task<PostingRow> PostGoodsReceiptAsync(
        Window owner,
        PostGoodsReceiptRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalGoodsReceipts,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<IReadOnlyList<GoodsReceiptSummaryResponse>> GetGoodsReceiptsAsync(
        Guid? purchaseOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var path = purchaseOrderId is Guid id
            ? $"{ApiRoutes.InternalGoodsReceiptsList}?purchaseOrderId={id:D}"
            : ApiRoutes.InternalGoodsReceiptsList;
        return GetListAsync<GoodsReceiptSummaryResponse>(path, cancellationToken);
    }

    public Task<byte[]> DownloadGoodsReceiptPdfAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(
            ApiRoutes.InternalGoodsReceiptPdf(goodsReceiptId),
            cancellationToken);

    public Task<IReadOnlyList<DocumentNumberRangeResponse>> GetDocumentNumberRangesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<DocumentNumberRangeResponse>(
            ApiRoutes.InternalDocumentNumberRanges,
            cancellationToken);

    public Task<DocumentNumberRangeResponse> UpsertDocumentNumberRangeAsync(
        UpsertDocumentNumberRangeRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<DocumentNumberRangeResponse>(
            ApiRoutes.InternalDocumentNumberRanges,
            request,
            cancellationToken);

    public Task<IReadOnlyList<SalesOrderRow>> GetSalesOrdersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<SalesOrderRow>(ApiRoutes.InternalSalesOrders, cancellationToken);

    public Task<SalesOrderRow> CreateSalesOrderAsync(
        CreateSalesOrderRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SalesOrderRow>(
            ApiRoutes.InternalSalesOrders,
            request,
            cancellationToken);

    public Task<ConfirmSalesOrderRow> ConfirmSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        PostAsync<ConfirmSalesOrderRow>(
            ApiRoutes.InternalSalesOrderConfirm(salesOrderId),
            null,
            cancellationToken);

    public Task<SalesOrderRow> CancelSalesOrderAsync(
        Window owner,
        Guid salesOrderId,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<SalesOrderRow>(
            owner,
            ApiRoutes.InternalSalesOrderCancel(salesOrderId),
            new CancelReasonRequest(reason),
            requireIdempotency: false,
            cancellationToken);

    public Task<SalesOrderRow> CloseSalesOrderAsync(
        Window owner,
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<SalesOrderRow>(
            owner,
            ApiRoutes.InternalSalesOrderClose(salesOrderId),
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task<SalesOrderRow> RejectSalesOrderAsync(
        Window owner,
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<SalesOrderRow>(
            owner,
            ApiRoutes.InternalSalesOrderReject(salesOrderId),
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task<PostingRow> PostDeliveryAsync(
        Window owner,
        PostDeliveryRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalDeliveries,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<PostingRow> PostSalesReturnAsync(
        Window owner,
        PostSalesReturnRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalSalesReturns,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<IReadOnlyList<DeliverySummaryResponse>> GetDeliveriesAsync(
        Guid? salesOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var path = salesOrderId is Guid id
            ? $"{ApiRoutes.InternalDeliveriesList}?salesOrderId={id:D}"
            : ApiRoutes.InternalDeliveriesList;
        return GetListAsync<DeliverySummaryResponse>(path, cancellationToken);
    }

    public Task<byte[]> DownloadDeliveryPdfAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalDeliveryPdf(deliveryId), cancellationToken);

    public Task<byte[]> DownloadPackingListPdfAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalPackingListPdf(deliveryId), cancellationToken);

    public Task<HandlingUnitResponse> CreateHandlingUnitAsync(
        CreateHandlingUnitRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<HandlingUnitResponse>(
            ApiRoutes.InternalHandlingUnits,
            request,
            cancellationToken);

    public Task<IReadOnlyList<HandlingUnitResponse>> GetHandlingUnitsAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default) =>
        GetListAsync<HandlingUnitResponse>(
            ApiRoutes.InternalHandlingUnitsByOrder(salesOrderId),
            cancellationToken);

    public Task<IReadOnlyList<StockRow>> GetStockAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<StockRow>(ApiRoutes.InternalStock, cancellationToken);

    public Task<IReadOnlyList<StockRow>> GetBelowMinimumAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<StockRow>(ApiRoutes.InternalStockBelowMinimum, cancellationToken);

    public Task<IReadOnlyList<MovementRow>> GetMovementsAsync(
        int limit = 200,
        CancellationToken cancellationToken = default) =>
        GetListAsync<MovementRow>(
            $"{ApiRoutes.InternalMovements}?limit={limit}",
            cancellationToken);

    public Task<ReconciliationRow> ReconcileStockAsync(
        Window owner,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<ReconciliationRow>(
            owner,
            ApiRoutes.InternalStockReconcile,
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task<StockRow> SetMinQuantityAsync(
        SetMinQuantityRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<StockRow>(
            ApiRoutes.InternalStockMinQuantity,
            request,
            cancellationToken);

    public Task<StockRow> SetStockStatusAsync(
        Window owner,
        SetStockStatusRequest request,
        CancellationToken cancellationToken = default) =>
        PutCriticalAsync<StockRow>(
            owner,
            ApiRoutes.InternalStockStatus,
            request,
            cancellationToken);

    public Task<PostingRow> PostPutawayAsync(
        Window owner,
        PostPutawayRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalStockPutaway,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<IReadOnlyList<WaveResponse>> ListWavesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<WaveResponse>(ApiRoutes.InternalWaves, cancellationToken);

    public Task<WaveResponse> CreateWaveAsync(
        CreateWaveRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<WaveResponse>(ApiRoutes.InternalWaves, request, cancellationToken);

    public Task<WaveResponse> ReleaseWaveAsync(
        Guid waveId,
        CancellationToken cancellationToken = default) =>
        PostAsync<WaveResponse>(
            ApiRoutes.InternalWaveRelease(waveId),
            new { },
            cancellationToken);

    public Task<IReadOnlyList<UnitConversionResponse>> ListUnitConversionsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<UnitConversionResponse>(
            ApiRoutes.InternalUnitConversions,
            cancellationToken);

    public Task<UnitConversionResponse> UpsertUnitConversionAsync(
        UpsertUnitConversionRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<UnitConversionResponse>(
            ApiRoutes.InternalUnitConversions,
            request,
            cancellationToken);

    public Task<ConvertQuantityResponse> ConvertQuantityAsync(
        ConvertQuantityRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ConvertQuantityResponse>(
            ApiRoutes.InternalUnitConvert,
            request,
            cancellationToken);

    public Task<IReadOnlyList<PurchaseRequisitionResponse>> ListPurchaseRequisitionsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PurchaseRequisitionResponse>(
            ApiRoutes.InternalPurchaseRequisitions,
            cancellationToken);

    public async Task<IReadOnlyList<PurchaseRequisitionResponse>> GenerateMrpAsync(
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await PostAsync<List<PurchaseRequisitionResponse>>(
            ApiRoutes.InternalPurchaseRequisitionsGenerate,
            new GenerateMrpRequestDto(warehouseId),
            cancellationToken);
        return rows ?? [];
    }

    public Task CancelPurchaseRequisitionAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        PostAsync<object>(
            ApiRoutes.InternalPurchaseRequisitionCancel(id),
            new { },
            cancellationToken);

    public Task<IReadOnlyList<RateCardResponse>> ListRateCardsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<RateCardResponse>(ApiRoutes.InternalRateCards, cancellationToken);

    public Task<RateCardResponse> UpsertRateCardAsync(
        UpsertRateCardRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RateCardResponse>(
            ApiRoutes.InternalRateCards,
            request,
            cancellationToken);

    public Task<RateChargeReportResponse> ComputeRateChargesAsync(
        Guid rateCardId,
        Dictionary<string, decimal> quantities,
        CancellationToken cancellationToken = default) =>
        PostAsync<RateChargeReportResponse>(
            ApiRoutes.InternalRateCardCompute(rateCardId),
            new ComputeRateChargesRequestDto(quantities),
            cancellationToken);

    public Task<IReadOnlyList<TransportRouteResponse>> ListTransportRoutesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<TransportRouteResponse>(
            ApiRoutes.InternalTransportRoutes,
            cancellationToken);

    public Task<TransportRouteResponse> CreateTransportRouteAsync(
        CreateTransportRouteRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<TransportRouteResponse>(
            ApiRoutes.InternalTransportRoutes,
            request,
            cancellationToken);

    public Task<TransportStopResponse> AddTransportStopAsync(
        Guid routeId,
        AddTransportStopRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<TransportStopResponse>(
            ApiRoutes.InternalTransportRouteStops(routeId),
            request,
            cancellationToken);

    public Task<CompanyResponse> CreateCompanyAsync(
        CreateCompanyRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CompanyResponse>(
            ApiRoutes.InternalCompanies,
            request,
            cancellationToken);

    public Task UpsertExchangeRateAsync(
        UpsertExchangeRateRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<object>(
            ApiRoutes.InternalExchangeRates,
            request,
            cancellationToken);

    public Task<FxConvertResponse> ConvertFxAsync(
        ConvertFxRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<FxConvertResponse>(
            ApiRoutes.InternalFxConvert,
            request,
            cancellationToken);

    public Task<RfScanResponseDto> RfScanAsync(
        string code,
        CancellationToken cancellationToken = default) =>
        PostAsync<RfScanResponseDto>(
            ApiRoutes.InternalRfScan,
            new RfScanRequestDto(code),
            cancellationToken);

    public Task<IReadOnlyList<LotResponse>> GetLotsAsync(
        Guid materialId,
        CancellationToken cancellationToken = default) =>
        GetListAsync<LotResponse>(
            ApiRoutes.InternalMaterialLots(materialId),
            cancellationToken);

    public Task<PostingRow> PostTransferAsync(
        Window owner,
        PostTransferRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalStockTransfers,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<PostingRow> PostAdjustmentAsync(
        Window owner,
        PostAdjustmentRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalStockAdjustments,
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<PostingRow> ReverseMovementAsync(
        Window owner,
        Guid movementId,
        ReverseMovementRequest request,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalMovementReverse(movementId),
            request,
            requireIdempotency: true,
            cancellationToken);

    public Task<InventoryCountRow> CreateInventoryCountAsync(
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<InventoryCountRow>(
            ApiRoutes.InternalStockCounts,
            request,
            cancellationToken);

    public Task<PostingRow> PostInventoryCountAsync(
        Window owner,
        Guid inventoryCountId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<PostingRow>(
            owner,
            ApiRoutes.InternalStockCountPost(inventoryCountId),
            null,
            requireIdempotency: true,
            cancellationToken);

    public Task<IReadOnlyList<RegistrationRequestRow>> GetRegistrationRequestsAsync(
        string status = "Pending",
        CancellationToken cancellationToken = default) =>
        GetListAsync<RegistrationRequestRow>(
            $"{ApiRoutes.InternalRegistrationRequests}?status={Uri.EscapeDataString(status)}",
            cancellationToken);

    public Task<RegistrationDecisionRow> ApproveRegistrationAsync(
        Window owner,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<RegistrationDecisionRow>(
            owner,
            ApiRoutes.InternalRegistrationApprove(requestId),
            null,
            requireIdempotency: false,
            cancellationToken);

    public Task<RegistrationDecisionRow> RejectRegistrationAsync(
        Window owner,
        Guid requestId,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<RegistrationDecisionRow>(
            owner,
            ApiRoutes.InternalRegistrationReject(requestId),
            new RejectRegistrationRequest(reason),
            requireIdempotency: false,
            cancellationToken);

    public Task<IReadOnlyList<OrderStatusReportRowDto>> GetOrdersByStatusAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<OrderStatusReportRowDto>(
            ApiRoutes.InternalReportsOrdersByStatus,
            cancellationToken);

    public Task<OtifReportResponse> GetOtifAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<OtifReportResponse>(
            ApiRoutes.InternalReportsOtif,
            cancellationToken);

    public Task<AuditEntryPageResponse> GetAuditEntriesAsync(
        int page = 1,
        int pageSize = 100,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        Guid? userId = null,
        string? userName = null,
        string? table = null,
        string? area = null,
        string? action = null,
        bool includePayloads = false,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}",
            $"includePayloads={(includePayloads ? "true" : "false")}",
        };
        if (fromUtc is { } from)
        {
            query.Add($"fromUtc={Uri.EscapeDataString(from.ToString("O"))}");
        }

        if (toUtc is { } to)
        {
            query.Add($"toUtc={Uri.EscapeDataString(to.ToString("O"))}");
        }

        if (userId is { } id)
        {
            query.Add($"userId={id:D}");
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query.Add($"userName={Uri.EscapeDataString(userName.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(table))
        {
            query.Add($"table={Uri.EscapeDataString(table.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            query.Add($"area={Uri.EscapeDataString(area.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query.Add($"action={Uri.EscapeDataString(action.Trim())}");
        }

        return GetAsync<AuditEntryPageResponse>(
            $"{ApiRoutes.InternalAuditEntries}?{string.Join('&', query)}",
            cancellationToken);
    }

    public Task<byte[]> ExportAuditEntriesAsync(
        string format = "csv",
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null,
        Guid? userId = null,
        string? userName = null,
        string? table = null,
        string? area = null,
        string? action = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"format={Uri.EscapeDataString(format)}",
        };
        if (fromUtc is { } from)
        {
            query.Add($"fromUtc={Uri.EscapeDataString(from.ToString("O"))}");
        }

        if (toUtc is { } to)
        {
            query.Add($"toUtc={Uri.EscapeDataString(to.ToString("O"))}");
        }

        if (userId is { } id)
        {
            query.Add($"userId={id:D}");
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query.Add($"userName={Uri.EscapeDataString(userName.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(table))
        {
            query.Add($"table={Uri.EscapeDataString(table.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            query.Add($"area={Uri.EscapeDataString(area.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query.Add($"action={Uri.EscapeDataString(action.Trim())}");
        }

        return DownloadBytesAsync(
            $"{ApiRoutes.InternalAuditEntriesExport}?{string.Join('&', query)}",
            cancellationToken);
    }

    public Task<AuditVerificationResponse> VerifyAuditChainAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<AuditVerificationResponse>(
            ApiRoutes.InternalAuditVerify,
            null,
            cancellationToken);

    public Task<IReadOnlyList<AuditCheckpointResponse>> GetAuditCheckpointsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<AuditCheckpointResponse>(
            ApiRoutes.InternalAuditCheckpoints,
            cancellationToken);

    public Task<AuditCheckpointResponse> CreateAuditCheckpointAsync(
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(note)
            ? ApiRoutes.InternalAuditCheckpoints
            : $"{ApiRoutes.InternalAuditCheckpoints}?note={Uri.EscapeDataString(note.Trim())}";
        return PostAsync<AuditCheckpointResponse>(url, null, cancellationToken);
    }

    public Task<AuditRetentionStatusResponse> GetAuditRetentionAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<AuditRetentionStatusResponse>(
            ApiRoutes.InternalAuditRetention,
            cancellationToken);

    public Task<byte[]> ExportMaterialsAsync(
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalExportMaterials, cancellationToken);

    public Task<byte[]> ExportStockAsync(
        CancellationToken cancellationToken = default) =>
        DownloadBytesAsync(ApiRoutes.InternalExportStock, cancellationToken);

    public async Task<ExcelImportPreviewDto> PreviewMaterialsImportAsync(
        Stream workbook,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(workbook);
        streamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", fileName);
        using var response = await _http.PostAsync(
            ApiRoutes.InternalImportMaterialsPreview,
            content,
            cancellationToken);
        return await ReadAsync<ExcelImportPreviewDto>(response, cancellationToken);
    }

    public async Task<ExcelImportCommitDto> CommitMaterialsImportAsync(
        Stream workbook,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(workbook);
        streamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(streamContent, "file", fileName);
        using var response = await _http.PostAsync(
            ApiRoutes.InternalImportMaterialsCommit,
            content,
            cancellationToken);
        return await ReadAsync<ExcelImportCommitDto>(response, cancellationToken);
    }

    public Task<T> PostCriticalAsync<T>(
        Window owner,
        string relativeUrl,
        object? body,
        bool requireIdempotency = false,
        CancellationToken cancellationToken = default) =>
        SendCriticalWithStepUpAsync<T>(
            owner,
            HttpMethod.Post,
            relativeUrl,
            body,
            requireIdempotency,
            cancellationToken);

    public Task<T> PutCriticalAsync<T>(
        Window owner,
        string relativeUrl,
        object? body,
        CancellationToken cancellationToken = default) =>
        SendCriticalWithStepUpAsync<T>(
            owner,
            HttpMethod.Put,
            relativeUrl,
            body,
            requireIdempotency: false,
            cancellationToken);

    private async Task<T> SendCriticalWithStepUpAsync<T>(
        Window owner,
        HttpMethod method,
        string relativeUrl,
        object? body,
        bool requireIdempotency,
        CancellationToken cancellationToken)
    {
        _ = owner;
        await EnsureAccessTokenAsync(cancellationToken);
        using var response = await SendCriticalAsync(
            method,
            relativeUrl,
            body,
            requireIdempotency,
            cancellationToken);
        return await ReadMappedAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCriticalAsync(
        HttpMethod method,
        string relativeUrl,
        object? body,
        bool requireIdempotency,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (requireIdempotency)
        {
            request.Headers.TryAddWithoutValidation(
                IdempotencyHeaderName,
                Guid.NewGuid().ToString("N"));
        }

        return await _http.SendAsync(request, cancellationToken);
    }

    private async Task<T> ReadMappedAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new ApiException(
                "No tiene permiso para esta operación.",
                "AAA-AUTH-FORBIDDEN");
        }

        return await ReadAsync<T>(response, cancellationToken);
    }

    private void ClearStepUp()
    {
        _stepUpToken = null;
        _stepUpExpiresUtc = default;
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        var rows = await GetAsync<List<T>>(relativeUrl, cancellationToken);
        return rows ?? [];
    }

    private async Task<T> GetAsync<T>(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        using var response = await _http.GetAsync(relativeUrl, cancellationToken);
        return await ReadMappedAsync<T>(response, cancellationToken);
    }

    private async Task<T> PostAsync<T>(
        string relativeUrl,
        object? body,
        CancellationToken cancellationToken)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        using var response = body is null
            ? await _http.PostAsync(relativeUrl, content: null, cancellationToken)
            : await _http.PostAsJsonAsync(
                relativeUrl,
                body,
                JsonOptions,
                cancellationToken);
        return await ReadMappedAsync<T>(response, cancellationToken);
    }

    private async Task<T> PutAsync<T>(
        string relativeUrl,
        object body,
        CancellationToken cancellationToken)
    {
        await EnsureAccessTokenAsync(cancellationToken);
        using var response = await _http.PutAsJsonAsync(
            relativeUrl,
            body,
            JsonOptions,
            cancellationToken);
        return await ReadMappedAsync<T>(response, cancellationToken);
    }

    private async Task<byte[]> DownloadBytesAsync(
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ReadAsync<object>(response, cancellationToken);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public Task<SapStatusRow> GetSapStatusAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SapStatusRow>(ApiRoutes.InternalSapStatus, cancellationToken);

    public Task<IReadOnlyList<SapMaterialRow>> GetSapMaterialsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<SapMaterialRow>(ApiRoutes.InternalSapMaterials, cancellationToken);

    public Task<SapSyncResultRow> SyncSapMaterialsAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<SapSyncResultRow>(
            ApiRoutes.InternalSapMaterialsSync,
            null,
            cancellationToken);

    public Task<IReadOnlyList<SapOutboxRowDto>> GetSapOutboxAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<SapOutboxRowDto>(ApiRoutes.InternalSapOutbox, cancellationToken);

    public Task<SapPushQueuedRow> QueueSapDeliveryPushAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default) =>
        PostAsync<SapPushQueuedRow>(
            ApiRoutes.InternalSapPushDelivery(deliveryId),
            null,
            cancellationToken);

    public Task ProcessSapOutboxAsync(CancellationToken cancellationToken = default) =>
        PostAsync<object>(
            $"{ApiRoutes.InternalSapOutbox}/process",
            null,
            cancellationToken);

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(object)
                || response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return default!;
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(
                JsonOptions,
                cancellationToken);
            if (payload is null)
            {
                throw new ApiException("La respuesta del servidor está vacía.");
            }

            return payload;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var title = TryReadProperty(body, "title");
        var code = TryReadProperty(body, "code");
        throw new ApiException(
            string.IsNullOrWhiteSpace(title)
                ? $"El servidor respondió {(int)response.StatusCode}."
                : title,
            code);
    }

    private static async Task<string?> TryReadCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return TryReadProperty(body, "code");
    }

    private static string? TryReadProperty(string body, string name)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

internal sealed class PingRow
{
    public string Surface { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

internal sealed class EnsureDefaultUnitsRow
{
    public int CreatedCount { get; set; }

    public List<string> CreatedCodes { get; set; } = [];
}

internal sealed class UnitRow
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int DecimalScale { get; set; }
}

internal sealed class MaterialRow
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid UnitId { get; set; }

    public decimal CatalogPrice { get; set; }

    public string Currency { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? ExternalId { get; set; }

    public bool RequiresLot { get; set; }

    public string UnitCode { get; set; } = string.Empty;
}

internal sealed class WarehouseRow
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SiteKind { get; set; } = "Generic";

    /// <summary>Etiqueta ES para rejillas de oficina (API sigue usando SiteKind).</summary>
    public string SiteKindLabel => SiteKind switch
    {
        "Campa" => "Campa (exterior)",
        "Nave" => "Nave (cubierta)",
        "Generic" => "Genérico",
        _ => string.IsNullOrWhiteSpace(SiteKind) ? "Genérico" : SiteKind,
    };
}

internal sealed class PartyRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? ExternalId { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string? Region { get; set; }

    public string PostalCode { get; set; } = string.Empty;

    public int? LeadTimeDays { get; set; }
}

internal sealed class OrderLineRow
{
    public Guid Id { get; set; }

    public Guid MaterialId { get; set; }

    public decimal RequestedQuantity { get; set; }

    public decimal ConfirmedQuantity { get; set; }

    public decimal PostedQuantity { get; set; }

    public decimal CancelledQuantity { get; set; }

    public decimal OpenQuantity { get; set; }
}

internal sealed class PurchaseOrderRow
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public List<OrderLineRow> Lines { get; set; } = [];
}

internal sealed class SalesOrderRow
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public List<OrderLineRow> Lines { get; set; } = [];
}

internal sealed class ConfirmSalesOrderRow
{
    public Guid SalesOrderId { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = [];
}

internal sealed class StockRow
{
    public Guid MaterialId { get; set; }

    public string MaterialCode { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }

    public string WarehouseCode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal MinQuantity { get; set; }

    public Guid? LocationId { get; set; }

    public string? LocationCode { get; set; }

    public Guid LotId { get; set; }

    public string Status { get; set; } = "Available";
}

internal sealed class MovementRow
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid MaterialId { get; set; }

    public Guid WarehouseId { get; set; }

    public decimal SignedQuantity { get; set; }

    public string UnitSnapshot { get; set; } = string.Empty;

    public string OriginDocumentType { get; set; } = string.Empty;

    public DateTimeOffset PostedUtc { get; set; }

    public string? Reason { get; set; }
}

internal sealed class ReconciliationItemRow
{
    public Guid MaterialId { get; set; }

    public Guid WarehouseId { get; set; }

    public decimal BalanceQuantity { get; set; }

    public decimal LedgerQuantity { get; set; }

    public decimal Difference { get; set; }
}

internal sealed class ReconciliationRow
{
    public bool IsConsistent { get; set; }

    public List<ReconciliationItemRow> Differences { get; set; } = [];
}

internal sealed class PostingRow
{
    public Guid DocumentId { get; set; }

    public string? DocumentNumber { get; set; }

    public List<Guid> MovementIds { get; set; } = [];
}

internal sealed class InventoryCountLineRow
{
    public Guid MaterialId { get; set; }

    public decimal SystemQuantity { get; set; }

    public decimal CountedQuantity { get; set; }

    public decimal Difference { get; set; }
}

internal sealed class InventoryCountRow
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid WarehouseId { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<InventoryCountLineRow> Lines { get; set; } = [];
}

internal sealed class RegistrationRequestRow
{
    public Guid Id { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public string? RejectionReason { get; set; }
}

internal sealed class RegistrationDecisionRow
{
    public Guid RequestId { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? CustomerId { get; set; }

    public Guid? UserId { get; set; }

    public string? TemporaryPassword { get; set; }
}

internal sealed class OrderStatusReportRowDto
{
    public string DocumentKind { get; set; } = string.Empty;

    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? PartyId { get; set; }
}

internal sealed class ExcelImportPreviewDto
{
    public string Kind { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public List<ExcelImportPreviewRowDto> Rows { get; set; } = [];
}

internal sealed class ExcelImportPreviewRowDto
{
    public int RowNumber { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Errors { get; set; } = [];
}

internal sealed class ExcelImportCommitDto
{
    public int CreatedOrUpdated { get; set; }

    public List<string> Messages { get; set; } = [];
}

internal sealed class SapStatusRow
{
    public bool Enabled { get; set; }

    public string Mode { get; set; } = string.Empty;

    public string? BaseUrlHint { get; set; }
}

internal sealed class SapMaterialRow
{
    public string MaterialNumber { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

internal sealed class SapSyncResultRow
{
    public int Listed { get; set; }

    public int Created { get; set; }

    public int Updated { get; set; }

    public int Mapped { get; set; }
}

internal sealed class SapOutboxRowDto
{
    public Guid Id { get; set; }

    public string MessageType { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public DateTimeOffset? NextAttemptUtc { get; set; }
}

internal sealed class SapPushQueuedRow
{
    public Guid OutboxId { get; set; }

    public Guid DeliveryId { get; set; }

    public string Status { get; set; } = string.Empty;
}

internal sealed class ApiException : Exception
{
    public ApiException(string message, string? code = null)
        : base(message)
    {
        Code = code;
    }

    public ApiException(string message, Exception innerException, string? code = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string? Code { get; }
}
