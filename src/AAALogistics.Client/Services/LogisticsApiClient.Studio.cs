using System.Text.Json;
using AAALogistics.Contracts;
using Avalonia.Controls;

namespace AAALogistics.Client.Services;

internal sealed partial class LogisticsApiClient
{
    public Task<IReadOnlyList<TaxCodeResponse>> ListStudioTaxCodesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<TaxCodeResponse>(ApiRoutes.InternalStudioTaxCodes, cancellationToken);

    public Task<TaxCodeResponse> CreateStudioTaxCodeAsync(
        CreateTaxCodeRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<TaxCodeResponse>(ApiRoutes.InternalStudioTaxCodes, request, cancellationToken);

    public Task<IReadOnlyList<PriceConditionResponse>> ListStudioPriceConditionsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PriceConditionResponse>(
            ApiRoutes.InternalStudioPriceConditions,
            cancellationToken);

    public Task<PriceConditionResponse> CreateStudioPriceConditionAsync(
        CreatePriceConditionRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<PriceConditionResponse>(
            ApiRoutes.InternalStudioPriceConditions,
            request,
            cancellationToken);

    public Task<IReadOnlyList<RmaResponse>> ListStudioRmasAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<RmaResponse>(ApiRoutes.InternalStudioRma, cancellationToken);

    public Task<RmaResponse> CreateStudioRmaAsync(
        CreateRmaRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RmaResponse>(ApiRoutes.InternalStudioRma, request, cancellationToken);

    public Task<IReadOnlyList<CreditSummaryResponse>> ListStudioCreditSummaryAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<CreditSummaryResponse>(
            ApiRoutes.InternalStudioCreditSummary,
            cancellationToken);

    public Task<IReadOnlyList<FrameworkContractResponse>> ListStudioFrameworkContractsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<FrameworkContractResponse>(
            ApiRoutes.InternalStudioFrameworkContracts,
            cancellationToken);

    public Task<FrameworkContractResponse> CreateStudioFrameworkContractAsync(
        CreateFrameworkContractRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<FrameworkContractResponse>(
            ApiRoutes.InternalStudioFrameworkContracts,
            request,
            cancellationToken);

    public Task<IReadOnlyList<RfqResponse>> ListStudioRfqsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<RfqResponse>(ApiRoutes.InternalStudioRfq, cancellationToken);

    public Task<RfqResponse> CreateStudioRfqAsync(
        CreateRfqRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<RfqResponse>(ApiRoutes.InternalStudioRfq, request, cancellationToken);

    public Task<IReadOnlyList<ThreeWayMatchResponse>> ListStudioThreeWayMatchesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<ThreeWayMatchResponse>(
            ApiRoutes.InternalStudioThreeWayMatch,
            cancellationToken);

    public Task<ThreeWayMatchResponse> CreateStudioThreeWayMatchAsync(
        CreateThreeWayMatchRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ThreeWayMatchResponse>(
            ApiRoutes.InternalStudioThreeWayMatch,
            request,
            cancellationToken);

    public Task<IReadOnlyList<MaterialValuationResponse>> ListStudioMaterialValuationsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<MaterialValuationResponse>(
            ApiRoutes.InternalStudioMaterialValuations,
            cancellationToken);

    public Task<MaterialValuationResponse> UpsertStudioMaterialValuationAsync(
        UpsertMaterialValuationRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<MaterialValuationResponse>(
            ApiRoutes.InternalStudioMaterialValuations,
            request,
            cancellationToken);

    public Task<IReadOnlyList<GlAccountResponse>> ListStudioGlAccountsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<GlAccountResponse>(ApiRoutes.InternalStudioGlAccounts, cancellationToken);

    public Task<int> SeedStudioSpainCoaAsync(
        CancellationToken cancellationToken = default) =>
        PostAsync<int>(ApiRoutes.InternalStudioGlAccountsSeedSpain, new { }, cancellationToken);

    public Task<IReadOnlyList<JournalEntryResponse>> ListStudioJournalsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<JournalEntryResponse>(ApiRoutes.InternalStudioJournals, cancellationToken);

    public Task<JournalEntryResponse> PostStudioJournalAsync(
        PostJournalEntryRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<JournalEntryResponse>(
            ApiRoutes.InternalStudioJournals,
            request,
            cancellationToken);

    public Task<JournalReconciliationResponse> GetStudioJournalReconciliationAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<JournalReconciliationResponse>(
            ApiRoutes.InternalStudioJournalReconciliation,
            cancellationToken);

    public Task<MrpTriggerResponse> TriggerStudioMrpAsync(
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default) =>
        PostAsync<MrpTriggerResponse>(
            ApiRoutes.InternalStudioMrpGenerate,
            new StudioGenerateMrpRequestDto(warehouseId),
            cancellationToken);

    public Task<IReadOnlyList<ValuedStockReportRow>> GetStudioValuedStockReportAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<ValuedStockReportRow>(
            ApiRoutes.InternalStudioReportsValuedStock,
            cancellationToken);

    public Task<IReadOnlyList<AgingReportRow>> GetStudioAgingReportAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<AgingReportRow>(
            ApiRoutes.InternalStudioReportsAging,
            cancellationToken);

    public Task<OtifReportResponse?> GetStudioOtifPassthroughAsync(
        CancellationToken cancellationToken = default) =>
        GetOptionalAsync<OtifReportResponse>(ApiRoutes.InternalStudioReportsOtif, cancellationToken);

    public Task<IReadOnlyList<EmployeeProfileResponse>> ListStudioEmployeesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<EmployeeProfileResponse>(
            ApiRoutes.InternalStudioEmployees,
            cancellationToken);

    public Task<IReadOnlyList<DomainEventLogResponse>> ListStudioDomainEventsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<DomainEventLogResponse>(
            ApiRoutes.InternalStudioDomainEvents,
            cancellationToken);

    public Task<IReadOnlyList<IntegrationOutboxResponse>> ListStudioIntegrationOutboxAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<IntegrationOutboxResponse>(
            ApiRoutes.InternalStudioIntegrationOutbox,
            cancellationToken);

    public Task<IReadOnlyList<CostCenterResponse>> ListStudioCostCentersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<CostCenterResponse>(ApiRoutes.InternalStudioCostCenters, cancellationToken);

    public Task<CostCenterResponse> CreateStudioCostCenterAsync(
        CreateCostCenterRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CostCenterResponse>(
            ApiRoutes.InternalStudioCostCenters,
            request,
            cancellationToken);

    public Task<IReadOnlyList<BomResponse>> ListStudioBomsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<BomResponse>(ApiRoutes.InternalStudioBoms, cancellationToken);

    public Task<BomResponse> CreateStudioBomAsync(
        CreateBomRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<BomResponse>(ApiRoutes.InternalStudioBoms, request, cancellationToken);

    public Task<IReadOnlyList<ProductionRouteResponse>> ListStudioProductionRoutesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<ProductionRouteResponse>(ApiRoutes.InternalStudioRoutes, cancellationToken);

    public Task<ProductionRouteResponse> CreateStudioProductionRouteAsync(
        CreateProductionRouteRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ProductionRouteResponse>(
            ApiRoutes.InternalStudioRoutes,
            request,
            cancellationToken);

    public Task<IReadOnlyList<ProductionOrderResponse>> ListStudioProductionOrdersAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<ProductionOrderResponse>(
            ApiRoutes.InternalStudioProductionOrders,
            cancellationToken);

    public Task<ProductionOrderResponse> CreateStudioProductionOrderAsync(
        CreateProductionOrderRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ProductionOrderResponse>(
            ApiRoutes.InternalStudioProductionOrders,
            request,
            cancellationToken);

    public Task<ProductionOrderResponse> ReleaseStudioProductionOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        PostAsync<ProductionOrderResponse>(
            ApiRoutes.InternalStudioProductionOrderRelease(orderId),
            new { },
            cancellationToken);

    public Task<ProductionOrderResponse> CompleteStudioProductionOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default) =>
        PostAsync<ProductionOrderResponse>(
            ApiRoutes.InternalStudioProductionOrderComplete(orderId),
            new { },
            cancellationToken);

    public Task<JournalEntryResponse> PostStudioJournalFromSupplierInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        PostAsync<JournalEntryResponse>(
            ApiRoutes.InternalStudioSupplierInvoiceJournal(invoiceId),
            new { },
            cancellationToken);

    public Task<IReadOnlyList<InspectionPlanResponse>> ListStudioInspectionPlansAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<InspectionPlanResponse>(
            ApiRoutes.InternalStudioInspectionPlans,
            cancellationToken);

    public Task<InspectionPlanResponse> CreateStudioInspectionPlanAsync(
        CreateInspectionPlanRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<InspectionPlanResponse>(
            ApiRoutes.InternalStudioInspectionPlans,
            request,
            cancellationToken);

    public Task<IReadOnlyList<QualityHoldResponse>> ListStudioQualityHoldsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<QualityHoldResponse>(
            ApiRoutes.InternalStudioQualityHolds,
            cancellationToken);

    public Task<QualityHoldResponse> CreateStudioQualityHoldAsync(
        CreateQualityHoldRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<QualityHoldResponse>(
            ApiRoutes.InternalStudioQualityHolds,
            request,
            cancellationToken);

    public Task<QualityHoldResponse> ReleaseStudioQualityHoldAsync(
        Window owner,
        Guid holdId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<QualityHoldResponse>(
            owner,
            ApiRoutes.InternalStudioQualityHoldRelease(holdId),
            new { },
            requireIdempotency: false,
            cancellationToken);

    public Task<QualityHoldResponse> RejectStudioQualityHoldAsync(
        Window owner,
        Guid holdId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<QualityHoldResponse>(
            owner,
            ApiRoutes.InternalStudioQualityHoldReject(holdId),
            new { },
            requireIdempotency: false,
            cancellationToken);

    public Task<IReadOnlyList<QualityCertificateResponse>> ListStudioQualityCertificatesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<QualityCertificateResponse>(
            ApiRoutes.InternalStudioQualityCertificates,
            cancellationToken);

    public Task<QualityCertificateResponse> CreateStudioQualityCertificateAsync(
        CreateQualityCertificateRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<QualityCertificateResponse>(
            ApiRoutes.InternalStudioQualityCertificates,
            request,
            cancellationToken);

    public Task<IReadOnlyList<PlantResponse>> ListStudioPlantsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PlantResponse>(ApiRoutes.InternalStudioPlants, cancellationToken);

    public Task<PlantResponse> CreateStudioPlantAsync(
        CreatePlantRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<PlantResponse>(ApiRoutes.InternalStudioPlants, request, cancellationToken);

    public Task<IReadOnlyList<PlantWarehouseResponse>> ListStudioPlantWarehousesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PlantWarehouseResponse>(
            ApiRoutes.InternalStudioPlantWarehouses,
            cancellationToken);

    public Task<PlantWarehouseResponse> LinkStudioPlantWarehouseAsync(
        LinkPlantWarehouseRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<PlantWarehouseResponse>(
            ApiRoutes.InternalStudioPlantWarehouses,
            request,
            cancellationToken);

    public Task<UserOrgContextResponse?> GetStudioUserOrgContextAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        GetOptionalAsync<UserOrgContextResponse>(
            $"{ApiRoutes.InternalStudioUserOrgContext}?userId={userId:D}",
            cancellationToken);

    public Task<UserOrgContextResponse> SetStudioUserOrgContextAsync(
        Window owner,
        SetUserOrgContextRequestDto request,
        CancellationToken cancellationToken = default) =>
        PutCriticalAsync<UserOrgContextResponse>(
            owner,
            ApiRoutes.InternalStudioUserOrgContext,
            request,
            cancellationToken);

    public Task<IReadOnlyList<ApprovalRuleResponse>> ListStudioApprovalRulesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<ApprovalRuleResponse>(
            ApiRoutes.InternalStudioApprovalRules,
            cancellationToken);

    public Task<ApprovalRuleResponse> CreateStudioApprovalRuleAsync(
        CreateApprovalRuleRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ApprovalRuleResponse>(
            ApiRoutes.InternalStudioApprovalRules,
            request,
            cancellationToken);

    public Task<IReadOnlyList<ApprovalRequestResponse>> ListStudioApprovalRequestsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<ApprovalRequestResponse>(
            ApiRoutes.InternalStudioApprovalRequests,
            cancellationToken);

    public Task<ApprovalRequestResponse> CreateStudioApprovalRequestAsync(
        CreateApprovalRequestRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ApprovalRequestResponse>(
            ApiRoutes.InternalStudioApprovalRequests,
            request,
            cancellationToken);

    public Task<ApprovalRequestResponse> ApproveStudioApprovalRequestAsync(
        Window owner,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<ApprovalRequestResponse>(
            owner,
            ApiRoutes.InternalStudioApprovalRequestApprove(requestId),
            new { },
            requireIdempotency: false,
            cancellationToken);

    public Task<ApprovalRequestResponse> RejectStudioApprovalRequestAsync(
        Window owner,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        PostCriticalAsync<ApprovalRequestResponse>(
            owner,
            ApiRoutes.InternalStudioApprovalRequestReject(requestId),
            new { },
            requireIdempotency: false,
            cancellationToken);

    public Task<IReadOnlyList<IntegrationConnectorResponse>> ListStudioIntegrationConnectorsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<IntegrationConnectorResponse>(
            ApiRoutes.InternalStudioIntegrationConnectors,
            cancellationToken);

    public Task<IntegrationConnectorResponse> CreateStudioIntegrationConnectorAsync(
        CreateIntegrationConnectorRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<IntegrationConnectorResponse>(
            ApiRoutes.InternalStudioIntegrationConnectors,
            request,
            cancellationToken);

    public Task<IReadOnlyList<IntegrationInboxResponse>> ListStudioIntegrationInboxAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<IntegrationInboxResponse>(
            ApiRoutes.InternalStudioIntegrationInbox,
            cancellationToken);

    public Task<IntegrationInboxResponse> ReceiveStudioIntegrationInboxAsync(
        ReceiveIntegrationInboxRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<IntegrationInboxResponse>(
            ApiRoutes.InternalStudioIntegrationInbox,
            request,
            cancellationToken);

    public Task<IReadOnlyList<StudioIntegrationMappingResponse>> ListStudioIntegrationMappingsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<StudioIntegrationMappingResponse>(
            ApiRoutes.InternalStudioIntegrationMappings,
            cancellationToken);

    public Task<StudioIntegrationMappingResponse> UpsertStudioIntegrationMappingAsync(
        UpsertStudioIntegrationMappingRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<StudioIntegrationMappingResponse>(
            ApiRoutes.InternalStudioIntegrationMappings,
            request,
            cancellationToken);

    public Task<EmployeeProfileResponse> CreateStudioEmployeeAsync(
        CreateEmployeeProfileRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<EmployeeProfileResponse>(
            ApiRoutes.InternalStudioEmployees,
            request,
            cancellationToken);

    public Task<DomainEventLogResponse> AppendStudioDomainEventAsync(
        AppendDomainEventRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<DomainEventLogResponse>(
            ApiRoutes.InternalStudioDomainEvents,
            request,
            cancellationToken);

    public Task<IReadOnlyList<PluginRegistrationResponse>> ListStudioPluginsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<PluginRegistrationResponse>(ApiRoutes.InternalStudioPlugins, cancellationToken);

    public Task<PluginRegistrationResponse> RegisterStudioPluginAsync(
        RegisterPluginRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<PluginRegistrationResponse>(
            ApiRoutes.InternalStudioPlugins,
            request,
            cancellationToken);

    public Task<InvokePluginResponse> InvokeStudioPluginAsync(
        Guid pluginId,
        InvokePluginRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<InvokePluginResponse>(
            ApiRoutes.InternalStudioPluginInvoke(pluginId),
            request,
            cancellationToken);

    public Task<IReadOnlyList<ConfigTransportResponse>> ListStudioConfigTransportsAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<ConfigTransportResponse>(
            ApiRoutes.InternalStudioConfigTransport,
            cancellationToken);

    public Task<ConfigTransportResponse> CreateStudioConfigTransportAsync(
        CreateConfigTransportRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<ConfigTransportResponse>(
            ApiRoutes.InternalStudioConfigTransport,
            request,
            cancellationToken);

    public Task<IReadOnlyList<SupplierInvoiceStubResponse>> ListStudioSupplierInvoicesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<SupplierInvoiceStubResponse>(
            ApiRoutes.InternalStudioSupplierInvoices,
            cancellationToken);

    public Task<SupplierInvoiceStubResponse> CreateStudioSupplierInvoiceAsync(
        CreateSupplierInvoiceStubRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SupplierInvoiceStubResponse>(
            ApiRoutes.InternalStudioSupplierInvoices,
            request,
            cancellationToken);

    public Task<EmployeeProfileResponse> UpdateStudioEmployeeAsync(
        Guid employeeId,
        UpdateEmployeeProfileRequestDto request,
        CancellationToken cancellationToken = default) =>
        PutAsync<EmployeeProfileResponse>(
            ApiRoutes.InternalStudioEmployeeById(employeeId),
            request,
            cancellationToken);

    public Task<IReadOnlyList<CompanyResponse>> ListCompaniesAsync(
        CancellationToken cancellationToken = default) =>
        GetListAsync<CompanyResponse>(ApiRoutes.InternalCompanies, cancellationToken);

    public Task<CustomerCommercialTermsResponse> UpsertCommercialTermsAsync(
        UpsertCommercialTermsRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostAsync<CustomerCommercialTermsResponse>(
            ApiRoutes.InternalCommercialTerms,
            request,
            cancellationToken);

    /// <summary>GET whose success body may be JSON <c>null</c> (no record yet).</summary>
    private async Task<T?> GetOptionalAsync<T>(
        string relativeUrl,
        CancellationToken cancellationToken)
        where T : class
    {
        await EnsureAccessTokenAsync(cancellationToken);
        using var response = await _http.GetAsync(relativeUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return await ReadMappedAsync<T>(response, cancellationToken);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body) || body.Trim() == "null"
            ? null
            : JsonSerializer.Deserialize<T>(body, JsonOptions);
    }
}