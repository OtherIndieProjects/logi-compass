using System.Globalization;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Appendix;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Sales;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Logistics;

public sealed class AppendixLogisticsService(
    ApplicationDbContext dbContext,
    IClock clock,
    IExtendedLogisticsService extendedLogistics) : IAppendixLogisticsService
{
    private readonly ApplicationDbContext _db = dbContext;
    private readonly IClock _clock = clock;
    private readonly IExtendedLogisticsService _extended = extendedLogistics;

    public async Task<WaveView> CreateWaveAsync(
        CreateWaveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.SalesOrderIds.Count == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-WAVE-0001",
                "La ola requiere al menos un pedido.");
        }

        var distinct = command.SalesOrderIds.Distinct().ToArray();
        var orders = await _db.SalesOrders
            .Where(value => distinct.Contains(value.Id))
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        if (orders.Count != distinct.Length)
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-WAVE-0002",
                "Uno o más pedidos no existen.");
        }

        var number = await NextDocumentAsync("WAVE", "WV", cancellationToken);
        var wave = new Wave(number, command.Priority, _clock.UtcNow);
        _db.Waves.Add(wave);
        foreach (var salesOrderId in distinct)
        {
            _db.WaveLines.Add(new WaveLine(wave.Id, salesOrderId));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await ToWaveViewAsync(wave.Id, cancellationToken);
    }

    public async Task<WaveView> ReleaseWaveAsync(
        Guid waveId,
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        var wave = await _db.Waves.SingleOrDefaultAsync(
            value => value.Id == waveId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-WAVE-0003",
                "La ola no existe.");

        var lines = await _db.WaveLines
            .Where(value => value.WaveId == waveId)
            .ToListAsync(cancellationToken);
        try
        {
            foreach (var line in lines)
            {
                await _extended.JoinPrepAsync(
                    new JoinPrepCommand(line.SalesOrderId, PrepLockMode.Open),
                    userId,
                    userName,
                    cancellationToken);
            }

            wave.Release();
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return await ToWaveViewAsync(wave.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<WaveView>> ListWavesAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _db.Waves.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(100)
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        var result = new List<WaveView>();
        foreach (var id in ids)
        {
            result.Add(await ToWaveViewAsync(id, cancellationToken));
        }

        return result;
    }

    public async Task<UnitConversionView> UpsertUnitConversionAsync(
        UpsertUnitConversionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var fromOk = await _db.UnitsOfMeasure.AnyAsync(
            value => value.Id == command.FromUnitId,
            cancellationToken);
        var toOk = await _db.UnitsOfMeasure.AnyAsync(
            value => value.Id == command.ToUnitId,
            cancellationToken);
        if (!fromOk || !toOk)
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-UOM-0001",
                "Unidad de medida no encontrada.");
        }

        var existing = await _db.UnitConversions.SingleOrDefaultAsync(
            value =>
                value.FromUnitId == command.FromUnitId
                && value.ToUnitId == command.ToUnitId,
            cancellationToken);
        if (existing is null)
        {
            existing = new UnitConversion(
                command.FromUnitId,
                command.ToUnitId,
                command.Factor);
            _db.UnitConversions.Add(existing);
        }
        else
        {
            existing.UpdateFactor(command.Factor);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new UnitConversionView(
            existing.Id,
            existing.FromUnitId,
            existing.ToUnitId,
            existing.Factor);
    }

    public async Task<IReadOnlyCollection<UnitConversionView>> ListUnitConversionsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.UnitConversions.AsNoTracking()
            .OrderBy(value => value.FromUnitId)
            .ThenBy(value => value.ToUnitId)
            .ToListAsync(cancellationToken);
        return rows
            .Select(value => new UnitConversionView(
                value.Id,
                value.FromUnitId,
                value.ToUnitId,
                value.Factor))
            .ToArray();
    }

    public async Task<ConvertQuantityResult> ConvertQuantityAsync(
        Guid fromUnitId,
        Guid toUnitId,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-UOM-0002",
                "La cantidad no puede ser negativa.");
        }

        var conversion = await _db.UnitConversions.AsNoTracking()
            .SingleOrDefaultAsync(
                value =>
                    value.FromUnitId == fromUnitId
                    && value.ToUnitId == toUnitId,
                cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-UOM-0003",
                "No hay factor de conversión para el par de unidades.");

        var target = await _db.UnitsOfMeasure.AsNoTracking()
            .SingleAsync(value => value.Id == toUnitId, cancellationToken);
        var output = conversion.Convert(quantity, target.DecimalScale);
        return new ConvertQuantityResult(
            fromUnitId,
            toUnitId,
            quantity,
            output,
            conversion.Factor);
    }

    public async Task<IReadOnlyCollection<PurchaseRequisitionView>> GenerateMrpAsync(
        Guid? warehouseId,
        CancellationToken cancellationToken = default)
    {
        var warehouses = warehouseId is Guid wid
            ? [wid]
            : await _db.Warehouses.AsNoTracking()
                .Select(value => value.Id)
                .ToListAsync(cancellationToken);

        var created = new List<PurchaseRequisition>();
        foreach (var wh in warehouses)
        {
            var balances = await _db.StockBalances.AsNoTracking()
                .Where(value =>
                    value.WarehouseId == wh
                    && value.Status == StockStatus.Available)
                .GroupBy(value => value.MaterialId)
                .Select(group => new
                {
                    MaterialId = group.Key,
                    OnHand = group.Sum(value => value.Quantity),
                    MinQty = group.Max(value => value.MinQuantity),
                })
                .ToListAsync(cancellationToken);

            foreach (var row in balances.Where(value => value.MinQty > 0 && value.OnHand < value.MinQty))
            {
                var gap = row.MinQty - row.OnHand;
                created.Add(await CreateRequisitionAsync(
                    row.MaterialId,
                    wh,
                    gap,
                    PurchaseRequisitionSource.Minimum,
                    cancellationToken));
            }

            var activeOrderIds = await _db.SalesOrders.AsNoTracking()
                .Where(order =>
                    order.DefaultWarehouseId == wh
                    && (order.Status == SalesOrderStatus.Confirmed
                        || order.Status == SalesOrderStatus.InPreparation
                        || order.Status == SalesOrderStatus.Prepared
                        || order.Status == SalesOrderStatus.PartiallyDelivered))
                .Select(order => order.Id)
                .ToListAsync(cancellationToken);
            var openDemand = await _db.SalesOrderLines.AsNoTracking()
                .Where(value =>
                    value.OpenQuantity > 0
                    && activeOrderIds.Contains(value.SalesOrderId))
                .GroupBy(value => value.MaterialId)
                .Select(group => new
                {
                    MaterialId = group.Key,
                    Demand = group.Sum(value => value.OpenQuantity),
                })
                .ToListAsync(cancellationToken);

            var reserved = await _db.StockReservations.AsNoTracking()
                .Where(value => value.IsActive && value.WarehouseId == wh)
                .GroupBy(value => value.MaterialId)
                .Select(group => new
                {
                    MaterialId = group.Key,
                    Qty = group.Sum(value => value.Quantity - value.ConsumedQuantity),
                })
                .ToDictionaryAsync(
                    value => value.MaterialId,
                    value => value.Qty,
                    cancellationToken);

            var onHandMap = balances.ToDictionary(
                value => value.MaterialId,
                value => value.OnHand);

            foreach (var demand in openDemand)
            {
                onHandMap.TryGetValue(demand.MaterialId, out var onHand);
                reserved.TryGetValue(demand.MaterialId, out var res);
                var atp = Math.Max(0m, onHand - res);
                if (demand.Demand > atp)
                {
                    created.Add(await CreateRequisitionAsync(
                        demand.MaterialId,
                        wh,
                        demand.Demand - atp,
                        PurchaseRequisitionSource.AtpGap,
                        cancellationToken));
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return created.Select(ToPr).ToArray();
    }

    public async Task<IReadOnlyCollection<PurchaseRequisitionView>> ListPurchaseRequisitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PurchaseRequisitions.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToPr).ToArray();
    }

    public async Task CancelPurchaseRequisitionAsync(
        Guid requisitionId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.PurchaseRequisitions.SingleOrDefaultAsync(
            value => value.Id == requisitionId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-MRP-0001",
                "La solicitud de compra no existe.");
        row.Cancel();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RateCardView> UpsertRateCardAsync(
        UpsertRateCardCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = UnitOfMeasure.NormalizeCode(command.Code, 30);
        var card = await _db.RateCards.SingleOrDefaultAsync(
            value => value.Code == code,
            cancellationToken);
        if (card is null)
        {
            card = new RateCard(command.Code, command.Name, command.Currency);
            _db.RateCards.Add(card);
        }
        else
        {
            card.Update(command.Name, command.Currency);
            var oldLines = await _db.RateCardLines
                .Where(value => value.RateCardId == card.Id)
                .ToListAsync(cancellationToken);
            _db.RateCardLines.RemoveRange(oldLines);
        }

        foreach (var line in command.Lines)
        {
            _db.RateCardLines.Add(
                new RateCardLine(
                    card.Id,
                    line.ServiceCode,
                    line.UnitPrice,
                    line.UnitLabel));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await ToRateCardViewAsync(card.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RateCardView>> ListRateCardsAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _db.RateCards.AsNoTracking()
            .OrderBy(value => value.Code)
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        var result = new List<RateCardView>();
        foreach (var id in ids)
        {
            result.Add(await ToRateCardViewAsync(id, cancellationToken));
        }

        return result;
    }

    public async Task<RateChargeReport> ComputeRateChargesAsync(
        Guid rateCardId,
        IReadOnlyDictionary<string, decimal> quantitiesByService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quantitiesByService);
        var card = await _db.RateCards.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == rateCardId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-RATE-0001",
                "Tarifa no encontrada.");
        var lines = await _db.RateCardLines.AsNoTracking()
            .Where(value => value.RateCardId == rateCardId)
            .ToListAsync(cancellationToken);
        var charges = new List<RateChargeLine>();
        foreach (var line in lines)
        {
            if (!quantitiesByService.TryGetValue(line.ServiceCode, out var qty)
                || qty <= 0)
            {
                continue;
            }

            var amount = Math.Round(
                qty * line.UnitPrice,
                2,
                MidpointRounding.AwayFromZero);
            charges.Add(
                new RateChargeLine(line.ServiceCode, qty, line.UnitPrice, amount));
        }

        return new RateChargeReport(
            card.Id,
            card.Currency,
            charges,
            charges.Sum(value => value.Amount));
    }

    public async Task<TransportRouteView> CreateTransportRouteAsync(
        CreateTransportRouteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CarrierId is Guid carrierId)
        {
            var exists = await _db.Carriers.AnyAsync(
                value => value.Id == carrierId,
                cancellationToken);
            if (!exists)
            {
                throw new LogisticsException(
                    LogisticsErrorKind.NotFound,
                    "AAA-TMS-0001",
                    "Transportista no encontrado.");
            }
        }

        var route = new TransportRoute(
            command.Code,
            command.Name,
            command.CarrierId,
            command.DistanceKm,
            command.BaseTariff,
            command.RatePerKm);
        _db.TransportRoutes.Add(route);
        await _db.SaveChangesAsync(cancellationToken);
        return await ToRouteViewAsync(route.Id, cancellationToken);
    }

    public async Task<TransportStopView> AddTransportStopAsync(
        AddTransportStopCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var routeExists = await _db.TransportRoutes.AnyAsync(
            value => value.Id == command.RouteId,
            cancellationToken);
        if (!routeExists)
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-TMS-0002",
                "Ruta no encontrada.");
        }

        if (command.DeliveryId is Guid deliveryId)
        {
            var delivery = await _db.Deliveries.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == deliveryId, cancellationToken)
                ?? throw new LogisticsException(
                    LogisticsErrorKind.NotFound,
                    "AAA-TMS-0003",
                    "Albarán no encontrado.");
            var stop = new TransportStop(
                command.RouteId,
                command.Sequence,
                delivery.SalesOrderId,
                delivery.Id,
                string.IsNullOrWhiteSpace(command.AddressSnapshot)
                    ? delivery.DeliveryAddressSnapshot
                    : command.AddressSnapshot);
            _db.TransportStops.Add(stop);
            await _db.SaveChangesAsync(cancellationToken);
            return new TransportStopView(
                stop.Id,
                stop.Sequence,
                stop.SalesOrderId,
                stop.DeliveryId,
                stop.AddressSnapshot);
        }

        var created = new TransportStop(
            command.RouteId,
            command.Sequence,
            command.SalesOrderId,
            null,
            command.AddressSnapshot);
        _db.TransportStops.Add(created);
        await _db.SaveChangesAsync(cancellationToken);
        return new TransportStopView(
            created.Id,
            created.Sequence,
            created.SalesOrderId,
            created.DeliveryId,
            created.AddressSnapshot);
    }

    public async Task<IReadOnlyCollection<TransportRouteView>> ListTransportRoutesAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _db.TransportRoutes.AsNoTracking()
            .OrderBy(value => value.Code)
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        var result = new List<TransportRouteView>();
        foreach (var id in ids)
        {
            result.Add(await ToRouteViewAsync(id, cancellationToken));
        }

        return result;
    }

    private async Task<PurchaseRequisition> CreateRequisitionAsync(
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        PurchaseRequisitionSource source,
        CancellationToken cancellationToken)
    {
        var number = await NextDocumentAsync("PR", "PR", cancellationToken);
        var row = new PurchaseRequisition(
            number,
            materialId,
            warehouseId,
            quantity,
            source,
            _clock.UtcNow);
        _db.PurchaseRequisitions.Add(row);
        return row;
    }

    private async Task<string> NextDocumentAsync(
        string type,
        string prefix,
        CancellationToken cancellationToken)
    {
        var lockKey = $"document-number:{type}";
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        var range = await _db.DocumentNumberRanges
            .SingleOrDefaultAsync(value => value.DocumentType == type, cancellationToken);
        if (range is null)
        {
            range = new DocumentNumberRange(type, prefix);
            _db.DocumentNumberRanges.Add(range);
        }

        var number = range.TakeNext();
        await _db.SaveChangesAsync(cancellationToken);
        return number;
    }

    private async Task<WaveView> ToWaveViewAsync(
        Guid waveId,
        CancellationToken cancellationToken)
    {
        var wave = await _db.Waves.AsNoTracking()
            .SingleAsync(value => value.Id == waveId, cancellationToken);
        var orderIds = await _db.WaveLines.AsNoTracking()
            .Where(value => value.WaveId == waveId)
            .Select(value => value.SalesOrderId)
            .ToListAsync(cancellationToken);
        return new WaveView(
            wave.Id,
            wave.Number,
            wave.Status.ToString(),
            wave.Priority,
            wave.CreatedUtc,
            orderIds);
    }

    private async Task<RateCardView> ToRateCardViewAsync(
        Guid rateCardId,
        CancellationToken cancellationToken)
    {
        var card = await _db.RateCards.AsNoTracking()
            .SingleAsync(value => value.Id == rateCardId, cancellationToken);
        var lines = await _db.RateCardLines.AsNoTracking()
            .Where(value => value.RateCardId == rateCardId)
            .OrderBy(value => value.ServiceCode)
            .ToListAsync(cancellationToken);
        return new RateCardView(
            card.Id,
            card.Code,
            card.Name,
            card.Currency,
            card.IsActive,
            lines.Select(value => new RateCardLineView(
                value.Id,
                value.ServiceCode,
                value.UnitPrice,
                value.UnitLabel)).ToArray());
    }

    private async Task<TransportRouteView> ToRouteViewAsync(
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var route = await _db.TransportRoutes.AsNoTracking()
            .SingleAsync(value => value.Id == routeId, cancellationToken);
        var stops = await _db.TransportStops.AsNoTracking()
            .Where(value => value.RouteId == routeId)
            .OrderBy(value => value.Sequence)
            .ToListAsync(cancellationToken);
        return new TransportRouteView(
            route.Id,
            route.Code,
            route.Name,
            route.CarrierId,
            route.DistanceKm,
            route.BaseTariff,
            route.RatePerKm,
            route.ComputeTariff(),
            route.IsActive,
            stops.Select(value => new TransportStopView(
                value.Id,
                value.Sequence,
                value.SalesOrderId,
                value.DeliveryId,
                value.AddressSnapshot)).ToArray());
    }

    private static PurchaseRequisitionView ToPr(PurchaseRequisition row) =>
        new(
            row.Id,
            row.Number,
            row.MaterialId,
            row.WarehouseId,
            row.Quantity,
            row.Status.ToString(),
            row.Source.ToString(),
            row.CreatedUtc);
}
