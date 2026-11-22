namespace AAALogistics.Application.Logistics;

public sealed record CreateCompanyCommand(
    string Code,
    string LegalName,
    string TaxId,
    string Currency);

public sealed record CompanyView(
    Guid Id,
    string Code,
    string LegalName,
    string TaxId,
    string Currency,
    bool IsActive);

public sealed record UpsertExchangeRateCommand(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly ValidOn);

public sealed record ExchangeRateView(
    Guid Id,
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateOnly ValidOn);

public sealed record UpsertCustomerCommercialTermsCommand(
    Guid CustomerId,
    Guid CompanyId,
    int PaymentTermDays,
    decimal CreditLimit,
    string Currency,
    decimal OpenReceivables = 0);

public sealed record CustomerCommercialTermsView(
    Guid Id,
    Guid CustomerId,
    Guid CompanyId,
    int PaymentTermDays,
    decimal CreditLimit,
    decimal OpenReceivables,
    string Currency,
    bool IsCreditBlocked);

public sealed record FxConvertResult(
    string FromCurrency,
    string ToCurrency,
    decimal InputAmount,
    decimal OutputAmount,
    decimal Rate,
    DateOnly ValidOn);

public interface IIndustrialLogisticsService
{
    Task<CompanyView> CreateCompanyAsync(
        CreateCompanyCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CompanyView>> ListCompaniesAsync(
        CancellationToken cancellationToken = default);

    Task<ExchangeRateView> UpsertExchangeRateAsync(
        UpsertExchangeRateCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExchangeRateView>> ListExchangeRatesAsync(
        CancellationToken cancellationToken = default);

    Task<FxConvertResult> ConvertFxAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        DateOnly? onDate = null,
        CancellationToken cancellationToken = default);

    Task<CustomerCommercialTermsView> UpsertCommercialTermsAsync(
        UpsertCustomerCommercialTermsCommand command,
        CancellationToken cancellationToken = default);

    Task<CustomerCommercialTermsView?> GetCommercialTermsAsync(
        Guid customerId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task EnsureCreditAllowsConfirmAsync(
        Guid customerId,
        Guid? companyId,
        decimal orderAmount,
        CancellationToken cancellationToken = default);

    Task<RfScanResult> ResolveRfScanAsync(
        string code,
        CancellationToken cancellationToken = default);
}

public sealed record RfScanResult(
    string Code,
    string Kind,
    DateTimeOffset ScannedUtc,
    Guid? ResolvedId = null,
    string? ResolvedLabel = null);

/// <summary>Realtime ops notifications after stock-mutating postings.</summary>
public interface IOperationsRealtime
{
    Task NotifyStockChangedAsync(
        Guid warehouseId,
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>Opt-in SAP façade (lab mock or real OData client).</summary>
public interface ISapIntegrationClient
{
    bool IsEnabled { get; }

    string Mode { get; }

    Task<IReadOnlyList<SapMaterialDto>> ListMaterialsAsync(
        CancellationToken cancellationToken = default);

    Task PushDeliveryAsync(
        Guid deliveryId,
        string payloadJson,
        CancellationToken cancellationToken = default);
}

public sealed record SapMaterialDto(string MaterialNumber, string Description);

public sealed record SapStatusView(
    bool Enabled,
    string Mode,
    string? BaseUrlHint);

public sealed record SapMaterialSyncResult(
    int Listed,
    int Created,
    int Updated,
    int Mapped);

public sealed record SapOutboxRow(
    Guid Id,
    string MessageType,
    string IdempotencyKey,
    string Status,
    int AttemptCount,
    string? LastError,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    DateTimeOffset? NextAttemptUtc);

public sealed record SapPushQueuedResult(
    Guid OutboxId,
    Guid DeliveryId,
    string Status);

public sealed record SapFieldMappingView(
    Guid Id,
    string SystemCode,
    string EntityType,
    string ExternalKey,
    Guid InternalId,
    DateTimeOffset MappedUtc);

public interface ISapIntegrationService
{
    SapStatusView GetStatus();

    Task<IReadOnlyList<SapMaterialDto>> ListRemoteMaterialsAsync(
        CancellationToken cancellationToken = default);

    Task<SapMaterialSyncResult> SyncMaterialsAsync(
        CancellationToken cancellationToken = default);

    Task<SapPushQueuedResult> QueueDeliveryPushAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SapOutboxRow>> ListOutboxAsync(
        CancellationToken cancellationToken = default);

    Task ProcessOutboxAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SapFieldMappingView>> ListMappingsAsync(
        CancellationToken cancellationToken = default);

    Task<SapFieldMappingView> UpsertMappingAsync(
        string systemCode,
        string entityType,
        string externalKey,
        Guid internalId,
        CancellationToken cancellationToken = default);
}
