namespace AAALogistics.Application.Logistics;

public sealed record CreateWaveCommand(
    IReadOnlyCollection<Guid> SalesOrderIds,
    int Priority);

public sealed record WaveView(
    Guid Id,
    string Number,
    string Status,
    int Priority,
    DateTimeOffset CreatedUtc,
    IReadOnlyCollection<Guid> SalesOrderIds);

public sealed record UpsertUnitConversionCommand(
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Factor);

public sealed record UnitConversionView(
    Guid Id,
    Guid FromUnitId,
    Guid ToUnitId,
    decimal Factor);

public sealed record ConvertQuantityResult(
    Guid FromUnitId,
    Guid ToUnitId,
    decimal InputQuantity,
    decimal OutputQuantity,
    decimal Factor);

public sealed record PurchaseRequisitionView(
    Guid Id,
    string Number,
    Guid MaterialId,
    Guid WarehouseId,
    decimal Quantity,
    string Status,
    string Source,
    DateTimeOffset CreatedUtc);

public sealed record UpsertRateCardCommand(
    string Code,
    string Name,
    string Currency,
    IReadOnlyCollection<UpsertRateCardLineCommand> Lines);

public sealed record UpsertRateCardLineCommand(
    string ServiceCode,
    decimal UnitPrice,
    string UnitLabel);

public sealed record RateCardView(
    Guid Id,
    string Code,
    string Name,
    string Currency,
    bool IsActive,
    IReadOnlyCollection<RateCardLineView> Lines);

public sealed record RateCardLineView(
    Guid Id,
    string ServiceCode,
    decimal UnitPrice,
    string UnitLabel);

public sealed record RateChargeLine(
    string ServiceCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record RateChargeReport(
    Guid RateCardId,
    string Currency,
    IReadOnlyCollection<RateChargeLine> Lines,
    decimal Total);

public sealed record CreateTransportRouteCommand(
    string Code,
    string Name,
    Guid? CarrierId,
    decimal DistanceKm,
    decimal BaseTariff,
    decimal RatePerKm);

public sealed record AddTransportStopCommand(
    Guid RouteId,
    int Sequence,
    Guid? SalesOrderId,
    Guid? DeliveryId,
    string? AddressSnapshot);

public sealed record TransportRouteView(
    Guid Id,
    string Code,
    string Name,
    Guid? CarrierId,
    decimal DistanceKm,
    decimal BaseTariff,
    decimal RatePerKm,
    decimal ComputedTariff,
    bool IsActive,
    IReadOnlyCollection<TransportStopView> Stops);

public sealed record TransportStopView(
    Guid Id,
    int Sequence,
    Guid? SalesOrderId,
    Guid? DeliveryId,
    string? AddressSnapshot);

public interface IAppendixLogisticsService
{
    Task<WaveView> CreateWaveAsync(
        CreateWaveCommand command,
        CancellationToken cancellationToken = default);

    Task<WaveView> ReleaseWaveAsync(
        Guid waveId,
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<WaveView>> ListWavesAsync(
        CancellationToken cancellationToken = default);

    Task<UnitConversionView> UpsertUnitConversionAsync(
        UpsertUnitConversionCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UnitConversionView>> ListUnitConversionsAsync(
        CancellationToken cancellationToken = default);

    Task<ConvertQuantityResult> ConvertQuantityAsync(
        Guid fromUnitId,
        Guid toUnitId,
        decimal quantity,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PurchaseRequisitionView>> GenerateMrpAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<PurchaseRequisitionView>> ListPurchaseRequisitionsAsync(
        CancellationToken cancellationToken = default);

    Task CancelPurchaseRequisitionAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default);

    Task<RateCardView> UpsertRateCardAsync(
        UpsertRateCardCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RateCardView>> ListRateCardsAsync(
        CancellationToken cancellationToken = default);

    Task<RateChargeReport> ComputeRateChargesAsync(
        Guid rateCardId,
        IReadOnlyDictionary<string, decimal> quantitiesByService,
        CancellationToken cancellationToken = default);

    Task<TransportRouteView> CreateTransportRouteAsync(
        CreateTransportRouteCommand command,
        CancellationToken cancellationToken = default);

    Task<TransportStopView> AddTransportStopAsync(
        AddTransportStopCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TransportRouteView>> ListTransportRoutesAsync(
        CancellationToken cancellationToken = default);
}
