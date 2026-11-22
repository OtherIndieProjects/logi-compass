using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Operations;
using AAALogistics.Domain.Purchasing;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Logistics;

public sealed partial class LogisticsService
{
    private static readonly JsonSerializerOptions IdempotencyJsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<PostingResult> PostGoodsReceiptAsync(
        PostGoodsReceiptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsurePostingLines(command.Lines.Count);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(PostGoodsReceiptAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var order = await _dbContext.PurchaseOrders.SingleOrDefaultAsync(
            value => value.Id == command.PurchaseOrderId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-RECEIPT-0001",
                "El pedido de compra no existe.");
        var requestedLineIds = command.Lines
            .Select(value => value.PurchaseOrderLineId)
            .ToArray();
        if (requestedLineIds.Distinct().Count() != requestedLineIds.Length)
        {
            throw Validation(
                "AAA-RECEIPT-0002",
                "No se puede repetir una línea del pedido.");
        }

        var orderLines = await _dbContext.PurchaseOrderLines
            .Where(value =>
                value.PurchaseOrderId == order.Id
                && requestedLineIds.Contains(value.Id))
            .ToListAsync(cancellationToken);
        if (orderLines.Count != command.Lines.Count)
        {
            throw Validation(
                "AAA-RECEIPT-0003",
                "Una línea no pertenece al pedido de compra.");
        }

        var materials = await LoadMaterialsAsync(
            orderLines.Select(value => value.MaterialId),
            cancellationToken);
        var warehouses = command.Lines
            .Select(value => value.WarehouseId ?? order.DefaultWarehouseId)
            .Distinct()
            .ToArray();
        await EnsureWarehousesExistAsync(warehouses, cancellationToken);

        var lineMap = orderLines.ToDictionary(value => value.Id);
        var defaultLocations = await ResolveDefaultLocationsAsync(
            warehouses,
            cancellationToken);

        // Resolve / create lots before locking so advisory keys include LotId.
        var resolvedLots = new Dictionary<Guid, Guid>();
        foreach (var postedLine in command.Lines)
        {
            var orderLine = lineMap[postedLine.PurchaseOrderLineId];
            var (material, _) = GetMaterial(materials, orderLine.MaterialId);
            var lotId = await ResolveReceiptLotAsync(
                material,
                postedLine.LotCode,
                postedLine.LotExpiryDate,
                cancellationToken);
            resolvedLots[postedLine.PurchaseOrderLineId] = lotId;
        }

        var stockKeys = command.Lines
            .Select(value =>
            {
                var line = lineMap[value.PurchaseOrderLineId];
                var warehouseId =
                    value.WarehouseId ?? order.DefaultWarehouseId;
                return (
                    line.MaterialId,
                    warehouseId,
                    defaultLocations[warehouseId],
                    resolvedLots[value.PurchaseOrderLineId],
                    value.OwnerCustomerId);
            })
            .ToArray();
        foreach (var pair in stockKeys
                     .Select(value => (value.MaterialId, value.warehouseId))
                     .Distinct()
                     .OrderBy(value => value.MaterialId)
                     .ThenBy(value => value.warehouseId))
        {
            await AcquireAtpAdvisoryLockAsync(
                pair.MaterialId,
                pair.warehouseId,
                cancellationToken);
        }

        await LockStockOwnedAsync(stockKeys, cancellationToken);

        var number = await TakeDocumentNumberAsync(
            "GOODS_RECEIPT",
            "GR",
            cancellationToken);
        var receipt = new GoodsReceipt(
            number,
            order.Id,
            order.SupplierId,
            order.DefaultWarehouseId,
            command.DocumentDate,
            _clock.UtcNow);
        var movementIds = new List<Guid>();
        _dbContext.GoodsReceipts.Add(receipt);

        foreach (var postedLine in command.Lines)
        {
            var orderLine = lineMap[postedLine.PurchaseOrderLineId];
            var (_, unit) = GetMaterial(materials, orderLine.MaterialId);
            EnsureQuantityScale(postedLine.Quantity, unit);
            orderLine.ApplyReceipt(postedLine.Quantity);

            var warehouseId =
                postedLine.WarehouseId ?? order.DefaultWarehouseId;
            var locationId = defaultLocations[warehouseId];
            var lotId = resolvedLots[postedLine.PurchaseOrderLineId];
            var balance = await GetOrCreateBalanceAsync(
                orderLine.MaterialId,
                warehouseId,
                cancellationToken,
                locationId,
                lotId,
                postedLine.OwnerCustomerId);
            balance.Apply(postedLine.Quantity);

            var receiptLine = new GoodsReceiptLine(
                receipt.Id,
                orderLine.Id,
                orderLine.MaterialId,
                warehouseId,
                postedLine.Quantity,
                unit.Code);
            var movement = StockMovement.Create(
                StockMovementType.Receipt,
                orderLine.MaterialId,
                warehouseId,
                locationId,
                postedLine.Quantity,
                unit.Code,
                "GoodsReceipt",
                receipt.Id,
                orderLine.Id,
                null,
                _clock.UtcNow,
                _requestContext.UserId,
                lotId: lotId,
                ownerCustomerId: postedLine.OwnerCustomerId);
            _dbContext.GoodsReceiptLines.Add(receiptLine);
            _dbContext.StockMovements.Add(movement);
            movementIds.Add(movement.Id);
        }

        order.RecalculateStatus(
            await _dbContext.PurchaseOrderLines
                .Where(value => value.PurchaseOrderId == order.Id)
                .ToListAsync(cancellationToken));
        var result = new PostingResult(receipt.Id, receipt.Number, movementIds);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var warehouseId in command.Lines
                     .Select(value => value.WarehouseId ?? receipt.DefaultWarehouseId)
                     .Distinct())
        {
            await NotifyStockAsync(
                warehouseId,
                $"GR {receipt.Number}",
                cancellationToken);
        }

        return result;
    }

    public async Task<PostingResult> PostDeliveryAsync(
        PostDeliveryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsurePostingLines(command.Lines.Count);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(PostDeliveryAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var order = await _dbContext.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == command.SalesOrderId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-DELIVERY-0001",
                "El pedido de venta no existe.");
        if (order.Status is not (
            SalesOrderStatus.Prepared
            or SalesOrderStatus.PartiallyDelivered))
        {
            throw Validation(
                "AAA-DELIVERY-0002",
                "El pedido debe estar preparado (o parcialmente entregado) antes de entregar.");
        }

        var requestedLineIds = command.Lines
            .Select(value => value.SalesOrderLineId)
            .ToArray();
        if (requestedLineIds.Distinct().Count() != requestedLineIds.Length)
        {
            throw Validation(
                "AAA-DELIVERY-0003",
                "No se puede repetir una línea del pedido.");
        }

        var orderLines = await _dbContext.SalesOrderLines
            .Where(value =>
                value.SalesOrderId == order.Id
                && requestedLineIds.Contains(value.Id))
            .ToListAsync(cancellationToken);
        if (orderLines.Count != command.Lines.Count)
        {
            throw Validation(
                "AAA-DELIVERY-0004",
                "Una línea no pertenece al pedido de venta.");
        }

        var materials = await LoadMaterialsAsync(
            orderLines.Select(value => value.MaterialId),
            cancellationToken);
        var warehouses = command.Lines
            .Select(value => value.WarehouseId ?? order.DefaultWarehouseId)
            .Distinct()
            .ToArray();
        await EnsureWarehousesExistAsync(warehouses, cancellationToken);

        var lineMap = orderLines.ToDictionary(value => value.Id);

        // ATP advisory + lock all Available grains, then plan picks under lock.
        var materialWarehousePairs = command.Lines
            .Select(value =>
            {
                var line = lineMap[value.SalesOrderLineId];
                return (
                    line.MaterialId,
                    WarehouseId: value.WarehouseId ?? order.DefaultWarehouseId);
            })
            .Distinct()
            .OrderBy(value => value.MaterialId)
            .ThenBy(value => value.WarehouseId)
            .ToArray();
        foreach (var pair in materialWarehousePairs)
        {
            var atpLockKey = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"atp:{pair.MaterialId:N}:{pair.WarehouseId:N}");
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({atpLockKey}, 0))",
                cancellationToken);
        }

        var materialIdsForLock = materialWarehousePairs
            .Select(value => value.MaterialId)
            .Distinct()
            .ToArray();
        var warehouseIdsForLock = materialWarehousePairs
            .Select(value => value.WarehouseId)
            .Distinct()
            .ToArray();
        var ownerIdsForDelivery = command.Lines
            .Select(value => value.OwnerCustomerId)
            .Distinct()
            .ToArray();
        var availableGrains = await _dbContext.StockBalances
            .Where(value =>
                materialIdsForLock.Contains(value.MaterialId)
                && warehouseIdsForLock.Contains(value.WarehouseId)
                && value.Status == StockStatus.Available
                && ownerIdsForDelivery.Contains(value.OwnerCustomerId))
            .Select(value => new
            {
                value.MaterialId,
                value.WarehouseId,
                value.LocationId,
                value.LotId,
                value.OwnerCustomerId,
            })
            .ToListAsync(cancellationToken);
        await LockStockOwnedAsync(
            availableGrains.Select(value =>
                (value.MaterialId, value.WarehouseId, value.LocationId, value.LotId, value.OwnerCustomerId)),
            cancellationToken);

        var confirmedPrepPicks = await _dbContext.PrepPickLines
            .Where(value =>
                value.SalesOrderId == order.Id
                && value.ConfirmedUtc != null
                && value.ConfirmedQuantity > value.ConsumedQuantity)
            .OrderBy(value => value.Id)
            .ToListAsync(cancellationToken);
        var prepByLine = confirmedPrepPicks
            .GroupBy(value => value.SalesOrderLineId)
            .ToDictionary(group => group.Key, group => group.ToList());

        // Prepared or partially delivered always require prep binding (unless Admin override).
        var requirePrepBinding =
            order.Status is SalesOrderStatus.Prepared
                or SalesOrderStatus.PartiallyDelivered;
        var picksByLine = new Dictionary<Guid, IReadOnlyList<PickSuggestion>>();
        var prepConsumptions = new List<(PrepPickLine Pick, decimal Qty)>();
        foreach (var postedLine in command.Lines)
        {
            var orderLine = lineMap[postedLine.SalesOrderLineId];
            var (material, _) = GetMaterial(materials, orderLine.MaterialId);
            var warehouseId =
                postedLine.WarehouseId ?? order.DefaultWarehouseId;

            IReadOnlyList<PickSuggestion> picks;
            // Admin override: always FEFO on requested warehouse (ignore prep binding).
            if (command.AllowFefoOverride)
            {
                picks = await SuggestPickAsync(
                    orderLine.MaterialId,
                    warehouseId,
                    postedLine.Quantity,
                    cancellationToken,
                    material.RequiresLot ? postedLine.LotId : null,
                    postedLine.OwnerCustomerId);
                if (picks.Sum(value => value.Quantity) < postedLine.Quantity)
                {
                    throw Validation(
                        "AAA-DELIVERY-0006",
                        material.RequiresLot
                            ? $"FEFO insuficiente para el material {orderLine.MaterialId:D}."
                            : $"Stock insuficiente (todas las ubicaciones) para el material {orderLine.MaterialId:D}.");
                }
            }
            else if (prepByLine.TryGetValue(orderLine.Id, out var prepLines))
            {
                if (warehouseId != order.DefaultWarehouseId)
                {
                    throw Validation(
                        "AAA-DELIVERY-0010",
                        $"El almacén de entrega debe coincidir con el de preparación ({order.DefaultWarehouseId:D}) salvo AllowFefoOverride Admin.");
                }

                var remainingToPost = postedLine.Quantity;
                var allocated = new List<PickSuggestion>();
                foreach (var prep in prepLines)
                {
                    if (remainingToPost <= 0)
                    {
                        break;
                    }

                    var take = Math.Min(prep.RemainingConfirmed, remainingToPost);
                    if (take <= 0)
                    {
                        continue;
                    }

                    allocated.Add(
                        new PickSuggestion(
                            prep.SuggestedLocationId,
                            prep.SuggestedLotId ?? Guid.Empty,
                            take));
                    prepConsumptions.Add((prep, take));
                    remainingToPost -= take;
                }

                if (remainingToPost > 0)
                {
                    throw Validation(
                        "AAA-DELIVERY-0007",
                        $"Picking confirmado insuficiente para la línea {orderLine.Id:D}.");
                }

                picks = allocated;
            }
            else if (requirePrepBinding)
            {
                throw Validation(
                    "AAA-DELIVERY-0008",
                    $"Pedido preparado exige picking confirmado para la línea {orderLine.Id:D} (o AllowFefoOverride Admin).");
            }
            else
            {
                picks = await SuggestPickAsync(
                    orderLine.MaterialId,
                    warehouseId,
                    postedLine.Quantity,
                    cancellationToken,
                    material.RequiresLot ? postedLine.LotId : null,
                    postedLine.OwnerCustomerId);
                if (picks.Sum(value => value.Quantity) < postedLine.Quantity)
                {
                    throw Validation(
                        "AAA-DELIVERY-0006",
                        material.RequiresLot
                            ? $"FEFO insuficiente para el material {orderLine.MaterialId:D}."
                            : $"Stock insuficiente (todas las ubicaciones) para el material {orderLine.MaterialId:D}.");
                }
            }

            picksByLine[orderLine.Id] = picks;
        }

        var deliveryLineIds = command.Lines
            .Select(value => value.SalesOrderLineId)
            .ToArray();
        var ownReservations = await _dbContext.StockReservations
            .Where(value =>
                value.SalesOrderId == order.Id
                && value.IsActive
                && deliveryLineIds.Contains(value.SalesOrderLineId))
            .ToListAsync(cancellationToken);
        var ownByLine = ownReservations
            .GroupBy(value => value.SalesOrderLineId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var materialIds = materialIdsForLock;
        var warehouseIds = warehouseIdsForLock;

        var onHandRows = await _dbContext.StockBalances
            .Where(value =>
                materialIds.Contains(value.MaterialId)
                && warehouseIds.Contains(value.WarehouseId)
                && value.Status == StockStatus.Available
                && ownerIdsForDelivery.Contains(value.OwnerCustomerId))
            .GroupBy(value => new
            {
                value.MaterialId,
                value.WarehouseId,
                value.OwnerCustomerId,
            })
            .Select(group => new
            {
                group.Key.MaterialId,
                group.Key.WarehouseId,
                group.Key.OwnerCustomerId,
                Quantity = group.Sum(value => value.Quantity),
            })
            .ToListAsync(cancellationToken);
        var onHandMap = onHandRows.ToDictionary(
            value => (value.MaterialId, value.WarehouseId, value.OwnerCustomerId),
            value => value.Quantity);

        // Reservations are house-stock only; apply only when posting Empty owner.
        var otherReservedRows = await _dbContext.StockReservations
            .Where(value =>
                value.IsActive
                && value.SalesOrderId != order.Id
                && materialIds.Contains(value.MaterialId)
                && warehouseIds.Contains(value.WarehouseId))
            .GroupBy(value => new { value.MaterialId, value.WarehouseId })
            .Select(group => new
            {
                group.Key.MaterialId,
                group.Key.WarehouseId,
                Quantity = group.Sum(
                    value => value.Quantity - value.ConsumedQuantity),
            })
            .ToListAsync(cancellationToken);
        var otherReservedMap = otherReservedRows.ToDictionary(
            value => (value.MaterialId, value.WarehouseId),
            value => value.Quantity);

        var number = await TakeDocumentNumberAsync(
            "DELIVERY",
            "DN",
            cancellationToken);
        var address = string.Join(
            ", ",
            new[]
            {
                order.DeliveryAddressLine1,
                order.DeliveryAddressLine2,
                order.DeliveryPostalCode,
                order.DeliveryCity,
                order.DeliveryRegion,
                order.DeliveryCountryCode,
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var delivery = new Delivery(
            number,
            order.Id,
            order.CustomerId,
            order.DefaultWarehouseId,
            command.DocumentDate,
            _clock.UtcNow,
            address);
        _dbContext.Deliveries.Add(delivery);
        var movementIds = new List<Guid>();

        foreach (var postedLine in command.Lines)
        {
            var orderLine = lineMap[postedLine.SalesOrderLineId];
            var (_, unit) = GetMaterial(materials, orderLine.MaterialId);
            EnsureQuantityScale(postedLine.Quantity, unit);

            var warehouseId =
                postedLine.WarehouseId ?? order.DefaultWarehouseId;
            onHandMap.TryGetValue(
                (orderLine.MaterialId, warehouseId, postedLine.OwnerCustomerId),
                out var onHand);
            var otherReserved = 0m;
            if (postedLine.OwnerCustomerId == Guid.Empty)
            {
                otherReservedMap.TryGetValue(
                    (orderLine.MaterialId, warehouseId),
                    out otherReserved);
            }

            // House ATP = onHand - other reservations; multi-owner grain ignores house reservations.
            var available = onHand - otherReserved;
            if (postedLine.Quantity > available)
            {
                throw Validation(
                    "AAA-DELIVERY-0005",
                    $"ATP insuficiente para el material {orderLine.MaterialId:D} (disponible {available}).");
            }

            orderLine.ApplyDelivery(postedLine.Quantity);

            // Only consume soft reservations for the warehouse actually debited
            // (AllowFefoOverride may ship from another WH without freeing house ATP there).
            var toConsumeReservation = postedLine.Quantity;
            if (ownByLine.TryGetValue(orderLine.Id, out var reservations))
            {
                foreach (var reservation in reservations)
                {
                    if (toConsumeReservation <= 0)
                    {
                        break;
                    }

                    if (reservation.WarehouseId != warehouseId)
                    {
                        continue;
                    }

                    var take = Math.Min(
                        toConsumeReservation,
                        reservation.RemainingQuantity);
                    if (take <= 0)
                    {
                        continue;
                    }

                    reservation.Consume(take);
                    toConsumeReservation -= take;
                }
            }

            var picks = picksByLine[orderLine.Id];
            var deliveryLine = new DeliveryLine(
                delivery.Id,
                orderLine.Id,
                orderLine.MaterialId,
                warehouseId,
                postedLine.Quantity,
                unit.Code);
            _dbContext.DeliveryLines.Add(deliveryLine);

            foreach (var pick in picks)
            {
                var balance = await GetOrCreateBalanceAsync(
                    orderLine.MaterialId,
                    warehouseId,
                    cancellationToken,
                    pick.LocationId,
                    pick.LotId,
                    postedLine.OwnerCustomerId);
                balance.Apply(-pick.Quantity);
                var movement = StockMovement.Create(
                    StockMovementType.Delivery,
                    orderLine.MaterialId,
                    warehouseId,
                    pick.LocationId,
                    -pick.Quantity,
                    unit.Code,
                    "Delivery",
                    delivery.Id,
                    orderLine.Id,
                    delivery.Id,
                    _clock.UtcNow,
                    _requestContext.UserId,
                    lotId: pick.LotId,
                    ownerCustomerId: postedLine.OwnerCustomerId);
                _dbContext.StockMovements.Add(movement);
                movementIds.Add(movement.Id);
            }

            onHandMap[(orderLine.MaterialId, warehouseId, postedLine.OwnerCustomerId)] =
                onHand - postedLine.Quantity;
        }

        foreach (var (prep, qty) in prepConsumptions)
        {
            prep.Consume(qty);
        }

        order.RecalculateStatus(
            await _dbContext.SalesOrderLines
                .Where(value => value.SalesOrderId == order.Id)
                .ToListAsync(cancellationToken));
        var result =
            new PostingResult(delivery.Id, delivery.Number, movementIds);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var warehouseId in command.Lines
                     .Select(value => value.WarehouseId ?? delivery.DefaultWarehouseId)
                     .Distinct())
        {
            await NotifyStockAsync(
                warehouseId,
                $"DN {delivery.Number}",
                cancellationToken);
        }

        return result;
    }

    public async Task<PostingResult> PostTransferAsync(
        PostTransferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.SourceWarehouseId == command.DestinationWarehouseId)
        {
            throw Validation(
                "AAA-TRANSFER-0001",
                "Los almacenes de origen y destino deben ser distintos.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(PostTransferAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var materials = await LoadMaterialsAsync(
            [command.MaterialId],
            cancellationToken);
        var (_, unit) = GetMaterial(materials, command.MaterialId);
        EnsureQuantityScale(command.Quantity, unit);
        await EnsureWarehousesExistAsync(
            [command.SourceWarehouseId, command.DestinationWarehouseId],
            cancellationToken);
        var sourceLocationId = await ResolveDefaultLocationIdAsync(
            command.SourceWarehouseId,
            cancellationToken);
        var destinationLocationId = await ResolveDefaultLocationIdAsync(
            command.DestinationWarehouseId,
            cancellationToken);
        await AcquireAtpAdvisoryLockAsync(
            command.MaterialId,
            command.SourceWarehouseId,
            cancellationToken);
        await AcquireAtpAdvisoryLockAsync(
            command.MaterialId,
            command.DestinationWarehouseId,
            cancellationToken);
        await LockStockAsync(
            [
                (command.MaterialId, command.SourceWarehouseId, sourceLocationId, Guid.Empty),
                (command.MaterialId, command.DestinationWarehouseId, destinationLocationId, Guid.Empty),
            ],
            cancellationToken);

        var source = await GetOrCreateBalanceAsync(
            command.MaterialId,
            command.SourceWarehouseId,
            cancellationToken,
            sourceLocationId,
            Guid.Empty);
        var destination = await GetOrCreateBalanceAsync(
            command.MaterialId,
            command.DestinationWarehouseId,
            cancellationToken,
            destinationLocationId,
            Guid.Empty);
        source.Apply(-command.Quantity);
        destination.Apply(command.Quantity);

        var correlationId = Guid.CreateVersion7();
        var outgoing = StockMovement.Create(
            StockMovementType.TransferOut,
            command.MaterialId,
            command.SourceWarehouseId,
            sourceLocationId,
            -command.Quantity,
            unit.Code,
            "Transfer",
            correlationId,
            null,
            correlationId,
            _clock.UtcNow,
            _requestContext.UserId,
            command.Reason);
        var incoming = StockMovement.Create(
            StockMovementType.TransferIn,
            command.MaterialId,
            command.DestinationWarehouseId,
            destinationLocationId,
            command.Quantity,
            unit.Code,
            "Transfer",
            correlationId,
            null,
            correlationId,
            _clock.UtcNow,
            _requestContext.UserId,
            command.Reason);
        _dbContext.StockMovements.AddRange(outgoing, incoming);

        var result = new PostingResult(
            correlationId,
            null,
            [outgoing.Id, incoming.Id]);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyStockAsync(
            command.SourceWarehouseId,
            $"TRF {correlationId:N}",
            cancellationToken);
        await NotifyStockAsync(
            command.DestinationWarehouseId,
            $"TRF {correlationId:N}",
            cancellationToken);
        return result;
    }

    public async Task<PostingResult> PostLocationTransferAsync(
        PostLocationTransferCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Quantity <= 0)
        {
            throw Validation(
                "AAA-LOC-0001",
                "La cantidad debe ser positiva.");
        }

        if (command.SourceLocationId == command.DestinationLocationId)
        {
            throw Validation(
                "AAA-LOC-0002",
                "Origen y destino deben diferir.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(PostLocationTransferAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var materials = await LoadMaterialsAsync(
            [command.MaterialId],
            cancellationToken);
        var (_, unit) = GetMaterial(materials, command.MaterialId);
        EnsureQuantityScale(command.Quantity, unit);
        await EnsureWarehousesExistAsync(
            [command.WarehouseId],
            cancellationToken);

        var sourceExists = await _dbContext.LocationNodes.AnyAsync(
            value =>
                value.Id == command.SourceLocationId
                && value.WarehouseId == command.WarehouseId,
            cancellationToken);
        var destinationExists = await _dbContext.LocationNodes.AnyAsync(
            value =>
                value.Id == command.DestinationLocationId
                && value.WarehouseId == command.WarehouseId,
            cancellationToken);
        if (!sourceExists || !destinationExists)
        {
            throw NotFound(
                "AAA-LOC-0004",
                "Una o ambas ubicaciones no existen en el almacén.");
        }

        var lotId = command.LotId;
        await AcquireAtpAdvisoryLockAsync(
            command.MaterialId,
            command.WarehouseId,
            cancellationToken);
        await LockStockAsync(
            [
                (command.MaterialId, command.WarehouseId, command.SourceLocationId, lotId),
                (command.MaterialId, command.WarehouseId, command.DestinationLocationId, lotId),
            ],
            cancellationToken);

        var source = await GetOrCreateBalanceAsync(
            command.MaterialId,
            command.WarehouseId,
            cancellationToken,
            command.SourceLocationId,
            lotId);
        var destination = await GetOrCreateBalanceAsync(
            command.MaterialId,
            command.WarehouseId,
            cancellationToken,
            command.DestinationLocationId,
            lotId);
        source.Apply(-command.Quantity);
        destination.Apply(command.Quantity);

        var correlationId = Guid.CreateVersion7();
        var reason = string.IsNullOrWhiteSpace(command.Reason)
            ? $"Traspaso ubicación {command.SourceLocationId:N} → {command.DestinationLocationId:N}"
            : command.Reason.Trim();
        var outgoing = StockMovement.Create(
            StockMovementType.TransferOut,
            command.MaterialId,
            command.WarehouseId,
            command.SourceLocationId,
            -command.Quantity,
            unit.Code,
            "LocationTransfer",
            correlationId,
            null,
            correlationId,
            _clock.UtcNow,
            _requestContext.UserId,
            reason,
            lotId);
        var incoming = StockMovement.Create(
            StockMovementType.TransferIn,
            command.MaterialId,
            command.WarehouseId,
            command.DestinationLocationId,
            command.Quantity,
            unit.Code,
            "LocationTransfer",
            correlationId,
            null,
            correlationId,
            _clock.UtcNow,
            _requestContext.UserId,
            reason,
            lotId);
        _dbContext.StockMovements.AddRange(outgoing, incoming);

        var result = new PostingResult(
            correlationId,
            null,
            [outgoing.Id, incoming.Id]);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyStockAsync(
            command.WarehouseId,
            $"LOC {correlationId:N}",
            cancellationToken);
        return result;
    }

    public async Task<PostingResult> PostPutawayAsync(
        PostPutawayCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var defaultLocationId = await ResolveDefaultLocationIdAsync(
            command.WarehouseId,
            cancellationToken);
        if (defaultLocationId == command.DestinationLocationId)
        {
            throw Validation(
                "AAA-PUTAWAY-0001",
                "El destino de colocación debe diferir de la raíz del almacén.");
        }

        return await PostLocationTransferAsync(
            new PostLocationTransferCommand(
                command.IdempotencyKey,
                command.MaterialId,
                command.WarehouseId,
                defaultLocationId,
                command.DestinationLocationId,
                command.Quantity,
                string.IsNullOrWhiteSpace(command.Reason)
                    ? "Putaway"
                    : command.Reason,
                command.LotId),
            cancellationToken);
    }

    public Task<IReadOnlyList<PickSuggestion>> SuggestPickAsync(
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        CancellationToken cancellationToken = default) =>
        ReadWithRlsAsync(
            ct => SuggestPickAsync(
                materialId,
                warehouseId,
                quantity,
                ct,
                requiredLotId: null),
            cancellationToken);

    public async Task<IReadOnlyCollection<LotView>> ListLotsAsync(
        Guid materialId,
        CancellationToken cancellationToken = default)
    {
        if (materialId == Guid.Empty)
        {
            throw Validation(
                "AAA-LOT-0001",
                "El material es obligatorio.");
        }

        return await ReadWithRlsAsync(
            async ct =>
            {
                IReadOnlyCollection<LotView> rows = await _dbContext.Lots
                    .AsNoTracking()
                    .Where(value => value.MaterialId == materialId)
                    .OrderBy(value => value.ExpiryDate == null)
                    .ThenBy(value => value.ExpiryDate)
                    .ThenBy(value => value.Code)
                    .Select(value => new LotView(
                        value.Id,
                        value.MaterialId,
                        value.Code,
                        value.ExpiryDate,
                        value.CreatedUtc))
                    .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    public async Task<PostingResult> PostAdjustmentAsync(
        PostAdjustmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Type is not StockMovementType.Adjustment
            and not StockMovementType.Opening)
        {
            throw Validation(
                "AAA-ADJUSTMENT-0001",
                "Solo se admiten tipos Adjustment y Opening.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(PostAdjustmentAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var materials = await LoadMaterialsAsync(
            [command.MaterialId],
            cancellationToken);
        var (_, unit) = GetMaterial(materials, command.MaterialId);
        EnsureQuantityScale(decimal.Abs(command.SignedQuantity), unit);
        await EnsureWarehousesExistAsync(
            [command.WarehouseId],
            cancellationToken);
        var locationId = await ResolveDefaultLocationIdAsync(
            command.WarehouseId,
            cancellationToken);
        await AcquireAtpAdvisoryLockAsync(
            command.MaterialId,
            command.WarehouseId,
            cancellationToken);
        await LockStockAsync(
            [(command.MaterialId, command.WarehouseId, locationId, Guid.Empty)],
            cancellationToken);
        if (command.Type == StockMovementType.Opening)
        {
            // Single-use opening per material+warehouse (including a prior Opening).
            var anyMovementExists = await _dbContext.StockMovements.AnyAsync(
                value =>
                    value.MaterialId == command.MaterialId
                    && value.WarehouseId == command.WarehouseId,
                cancellationToken);
            if (anyMovementExists)
            {
                throw Conflict(
                    "AAA-ADJUSTMENT-0003",
                    "No se puede abrir stock: ya existe un movimiento de ese material en el almacén.");
            }
        }

        var balance = await GetOrCreateBalanceAsync(
            command.MaterialId,
            command.WarehouseId,
            cancellationToken,
            locationId,
            Guid.Empty);
        balance.Apply(command.SignedQuantity);

        var operationId = Guid.CreateVersion7();
        var movement = StockMovement.Create(
            command.Type,
            command.MaterialId,
            command.WarehouseId,
            locationId,
            command.SignedQuantity,
            unit.Code,
            command.Type == StockMovementType.Opening
                ? "Opening"
                : "Adjustment",
            operationId,
            null,
            null,
            _clock.UtcNow,
            _requestContext.UserId,
            command.Reason,
            Guid.Empty);
        _dbContext.StockMovements.Add(movement);

        var result =
            new PostingResult(operationId, null, [movement.Id]);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyStockAsync(
            command.WarehouseId,
            $"{command.Type} {operationId:N}",
            cancellationToken);
        return result;
    }

    public async Task<PostingResult> PostSalesReturnAsync(
        PostSalesReturnCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Type is not StockMovementType.ReturnIn
            and not StockMovementType.ReturnOut)
        {
            throw Validation(
                "AAA-RETURN-0001",
                "Solo se admiten tipos ReturnIn y ReturnOut.");
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw Validation(
                "AAA-RETURN-0002",
                "El motivo de la devolución es obligatorio.");
        }

        EnsurePostingLines(command.Lines.Count);

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(PostSalesReturnAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var order = await _dbContext.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == command.SalesOrderId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-RETURN-0003",
                "El pedido de venta no existe.");
        _ = order;

        if (command.OriginalDeliveryId is Guid deliveryId)
        {
            var deliveryExists = await _dbContext.Deliveries.AnyAsync(
                value =>
                    value.Id == deliveryId
                    && value.SalesOrderId == command.SalesOrderId,
                cancellationToken);
            if (!deliveryExists)
            {
                throw Validation(
                    "AAA-RETURN-0004",
                    "La entrega original no pertenece al pedido.");
            }
        }

        var materials = await LoadMaterialsAsync(
            command.Lines.Select(value => value.MaterialId),
            cancellationToken);
        var warehouses = command.Lines
            .Select(value => value.WarehouseId)
            .Distinct()
            .ToArray();
        await EnsureWarehousesExistAsync(warehouses, cancellationToken);

        foreach (var pair in command.Lines
                     .Select(value => (value.MaterialId, value.WarehouseId))
                     .Distinct()
                     .OrderBy(value => value.MaterialId)
                     .ThenBy(value => value.WarehouseId))
        {
            await AcquireAtpAdvisoryLockAsync(
                pair.MaterialId,
                pair.WarehouseId,
                cancellationToken);
        }

        var movementIds = new List<Guid>();
        var operationId = Guid.CreateVersion7();
        foreach (var line in command.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw Validation(
                    "AAA-RETURN-0005",
                    "La cantidad de devolución debe ser positiva.");
            }

            var (_, unit) = GetMaterial(materials, line.MaterialId);
            EnsureQuantityScale(line.Quantity, unit);
            var locationId = line.LocationId
                ?? await ResolveDefaultLocationIdAsync(
                    line.WarehouseId,
                    cancellationToken);
            await LockStockAsync(
                [(line.MaterialId, line.WarehouseId, locationId, line.LotId)],
                cancellationToken);
            var signed = command.Type == StockMovementType.ReturnIn
                ? line.Quantity
                : -line.Quantity;
            var balance = await GetOrCreateBalanceAsync(
                line.MaterialId,
                line.WarehouseId,
                cancellationToken,
                locationId,
                line.LotId);
            balance.Apply(signed);
            var movement = StockMovement.Create(
                command.Type,
                line.MaterialId,
                line.WarehouseId,
                locationId,
                signed,
                unit.Code,
                "SalesReturn",
                operationId,
                command.OriginalDeliveryId,
                null,
                _clock.UtcNow,
                _requestContext.UserId,
                command.Reason,
                line.LotId);
            _dbContext.StockMovements.Add(movement);
            movementIds.Add(movement.Id);
        }

        var result = new PostingResult(operationId, null, movementIds);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var warehouseId in command.Lines
                     .Select(value => value.WarehouseId)
                     .Distinct())
        {
            await NotifyStockAsync(
                warehouseId,
                $"RET {operationId:N}",
                cancellationToken);
        }

        return result;
    }

    public async Task<PostingResult> PostInventoryCountAsync(
        string idempotencyKey,
        Guid inventoryCountId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(PostInventoryCountAsync),
            idempotencyKey,
            new { InventoryCountId = inventoryCountId },
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var count = await _dbContext.InventoryCounts.SingleOrDefaultAsync(
            value => value.Id == inventoryCountId,
            cancellationToken)
            ?? throw NotFound("AAA-COUNT-0003", "El recuento no existe.");
        var lines = await _dbContext.InventoryCountLines
            .Where(value => value.InventoryCountId == inventoryCountId)
            .ToListAsync(cancellationToken);
        try
        {
            count.MarkPosted(_clock.UtcNow);
        }
        catch (InvalidOperationException exception)
        {
            throw Conflict("AAA-COUNT-0004", exception.Message);
        }

        var differences = lines.Where(value => value.Difference != 0).ToArray();
        var materials = await LoadMaterialsAsync(
            differences.Select(value => value.MaterialId),
            cancellationToken);
        var locationId = await ResolveDefaultLocationIdAsync(
            count.WarehouseId,
            cancellationToken);
        foreach (var materialId in differences
                     .Select(value => value.MaterialId)
                     .Distinct()
                     .OrderBy(value => value))
        {
            await AcquireAtpAdvisoryLockAsync(
                materialId,
                count.WarehouseId,
                cancellationToken);
        }

        await LockStockAsync(
            differences.Select(value =>
                (value.MaterialId, count.WarehouseId, locationId, Guid.Empty)),
            cancellationToken);

        var movementIds = new List<Guid>();
        foreach (var line in differences)
        {
            var (_, unit) = GetMaterial(materials, line.MaterialId);
            EnsureQuantityScale(decimal.Abs(line.Difference), unit);
            var balance = await GetOrCreateBalanceAsync(
                line.MaterialId,
                count.WarehouseId,
                cancellationToken,
                locationId,
                Guid.Empty);
            balance.Apply(line.Difference);

            var operationId = Guid.CreateVersion7();
            var movement = StockMovement.Create(
                StockMovementType.Adjustment,
                line.MaterialId,
                count.WarehouseId,
                locationId,
                line.Difference,
                unit.Code,
                "Adjustment",
                operationId,
                null,
                null,
                _clock.UtcNow,
                _requestContext.UserId,
                $"RECUENTO {count.Number}",
                Guid.Empty);
            _dbContext.StockMovements.Add(movement);
            movementIds.Add(movement.Id);
        }

        var result = new PostingResult(count.Id, count.Number, movementIds);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyStockAsync(
            count.WarehouseId,
            $"CNT {count.Number}",
            cancellationToken);
        return result;
    }

    public async Task<PostingResult> ReverseMovementAsync(
        ReverseMovementCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            throw Validation(
                "AAA-REVERSAL-0001",
                "El motivo de reversión es obligatorio.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<PostingResult>(
            nameof(ReverseMovementAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var seed = await _dbContext.StockMovements.SingleOrDefaultAsync(
            value => value.Id == command.MovementId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-REVERSAL-0002",
                "El movimiento no existe.");
        var originals = new List<StockMovement> { seed };
        if (seed.CorrelatedOperationId is Guid correlationId)
        {
            originals = await _dbContext.StockMovements
                .Where(value =>
                    value.CorrelatedOperationId == correlationId
                    && value.Type != StockMovementType.Reversal)
                .ToListAsync(cancellationToken);
            if (originals.Count == 0)
            {
                originals.Add(seed);
            }
        }

        foreach (var pair in originals
                     .Select(value => (value.MaterialId, value.WarehouseId))
                     .Distinct()
                     .OrderBy(value => value.MaterialId)
                     .ThenBy(value => value.WarehouseId))
        {
            await AcquireAtpAdvisoryLockAsync(
                pair.MaterialId,
                pair.WarehouseId,
                cancellationToken);
        }

        await LockStockOwnedAsync(
            originals.Select(value =>
                (value.MaterialId, value.WarehouseId, value.LocationId, value.LotId, value.OwnerCustomerId)),
            cancellationToken);
        foreach (var original in originals)
        {
            var alreadyReversed = await _dbContext.StockMovements.AnyAsync(
                value => value.ReversedMovementId == original.Id,
                cancellationToken);
            if (alreadyReversed)
            {
                throw Conflict(
                    "AAA-REVERSAL-0003",
                    "El movimiento, o la otra pata de la misma operación, ya fue revertido.");
            }
        }

        var reversalIds = new List<Guid>(originals.Count);
        foreach (var original in originals)
        {
            var balance = await GetOrCreateBalanceAsync(
                original.MaterialId,
                original.WarehouseId,
                cancellationToken,
                original.LocationId,
                original.LotId,
                original.OwnerCustomerId);
            balance.Apply(-original.SignedQuantity);
            var reversal = StockMovement.CreateReversal(
                original,
                alreadyReversed: false,
                _clock.UtcNow,
                _requestContext.UserId,
                command.Reason);
            _dbContext.StockMovements.Add(reversal);
            reversalIds.Add(reversal.Id);
            await ReverseDocumentQuantityAsync(original, cancellationToken);
        }

        var result =
            new PostingResult(reversalIds[0], null, reversalIds);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(result, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var warehouseId in originals
                     .Select(value => value.WarehouseId)
                     .Distinct())
        {
            await NotifyStockAsync(
                warehouseId,
                $"REV {reversalIds[0]:N}",
                cancellationToken);
        }

        return result;
    }

    private async Task ReverseDocumentQuantityAsync(
        StockMovement original,
        CancellationToken cancellationToken)
    {
        if (original.OriginLineId is not { } lineId)
        {
            return;
        }

        if (string.Equals(
                original.OriginDocumentType,
                "GoodsReceipt",
                StringComparison.Ordinal))
        {
            var line = await _dbContext.PurchaseOrderLines.SingleAsync(
                value => value.Id == lineId,
                cancellationToken);
            line.ReverseReceipt(decimal.Abs(original.SignedQuantity));
            var order = await _dbContext.PurchaseOrders.SingleAsync(
                value => value.Id == line.PurchaseOrderId,
                cancellationToken);
            var lines = await _dbContext.PurchaseOrderLines
                .Where(value => value.PurchaseOrderId == order.Id)
                .ToListAsync(cancellationToken);
            order.RecalculateStatus(lines);
        }
        else if (string.Equals(
                     original.OriginDocumentType,
                     "Delivery",
                     StringComparison.Ordinal))
        {
            var qty = decimal.Abs(original.SignedQuantity);
            var line = await _dbContext.SalesOrderLines.SingleAsync(
                value => value.Id == lineId,
                cancellationToken);
            line.ReverseDelivery(qty);
            var order = await _dbContext.SalesOrders.SingleAsync(
                value => value.Id == line.SalesOrderId,
                cancellationToken);
            var lines = await _dbContext.SalesOrderLines
                .Where(value => value.SalesOrderId == order.Id)
                .ToListAsync(cancellationToken);
            order.RecalculateStatus(lines);

            // Restore PrepPick by the same grain the movement debitó (not LIFO-by-Id).
            var movementLotId = original.LotId;
            var prepPicks = await _dbContext.PrepPickLines
                .Where(value =>
                    value.SalesOrderLineId == line.Id
                    && value.ConsumedQuantity > 0
                    && value.SuggestedLocationId == original.LocationId
                    && ((value.SuggestedLotId ?? Guid.Empty) == movementLotId))
                .OrderByDescending(value => value.Id)
                .ToListAsync(cancellationToken);
            var remainingPrep = qty;
            foreach (var prep in prepPicks)
            {
                if (remainingPrep <= 0)
                {
                    break;
                }

                var take = Math.Min(prep.ConsumedQuantity, remainingPrep);
                if (take <= 0)
                {
                    continue;
                }

                prep.RestoreConsume(take);
                remainingPrep -= take;
            }

            // Restore soft reservation on the warehouse that was actually reversed.
            var reservations = await _dbContext.StockReservations
                .Where(value =>
                    value.SalesOrderLineId == line.Id
                    && value.WarehouseId == original.WarehouseId
                    && value.ConsumedQuantity > 0
                    && value.ReleasedUtc == null)
                .OrderByDescending(value => value.Id)
                .ToListAsync(cancellationToken);
            var remainingReservation = qty;
            foreach (var reservation in reservations)
            {
                if (remainingReservation <= 0)
                {
                    break;
                }

                var take = Math.Min(reservation.ConsumedQuantity, remainingReservation);
                if (take <= 0)
                {
                    continue;
                }

                reservation.RestoreConsume(take);
                remainingReservation -= take;
            }

            // Full reverse demotes Delivered → Confirmed; restore Prepared if prep still covers.
            await TryRestorePreparedAfterDeliveryReverseAsync(
                order,
                lines,
                cancellationToken);
        }
    }

    private async Task TryRestorePreparedAfterDeliveryReverseAsync(
        SalesOrder order,
        IReadOnlyCollection<SalesOrderLine> lines,
        CancellationToken cancellationToken)
    {
        if (order.Status != SalesOrderStatus.Confirmed)
        {
            return;
        }

        var openLines = lines.Where(value => value.OpenQuantity > 0).ToArray();
        if (openLines.Length == 0)
        {
            return;
        }

        var picks = await _dbContext.PrepPickLines
            .Where(value => value.SalesOrderId == order.Id)
            .ToListAsync(cancellationToken);
        if (picks.Count == 0)
        {
            return;
        }

        foreach (var openLine in openLines)
        {
            var covered = picks
                .Where(value => value.SalesOrderLineId == openLine.Id)
                .Sum(value => value.RemainingConfirmed);
            if (covered < openLine.OpenQuantity)
            {
                return;
            }
        }

        order.RestorePreparedAfterReverse();
    }

    private async Task<IdempotencyStart<T>> BeginIdempotencyAsync<T>(
        string commandType,
        string key,
        object request,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
        {
            throw Validation(
                "AAA-IDEMPOTENCY-0001",
                "Idempotency-Key es obligatorio y admite hasta 200 caracteres.");
        }

        var requestJson = JsonSerializer.Serialize(
            request,
            IdempotencyJsonOptions);
        var requestHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)));
        // Scope idempotency keys per actor so one worker cannot reuse another's key.
        var scopedKey = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{_requestContext.UserId:N}:{key.Trim()}");
        if (scopedKey.Length > 200)
        {
            throw Validation(
                "AAA-IDEMPOTENCY-0001",
                "Idempotency-Key es demasiado largo tras el ámbito de usuario.");
        }

        var lockKey = $"idempotency:{commandType}:{scopedKey}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        var existing = await _dbContext.IdempotencyRecords
            .SingleOrDefaultAsync(
                value =>
                    value.CommandType == commandType
                    && value.Key == scopedKey,
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(
                    existing.RequestHash,
                    requestHash,
                    StringComparison.Ordinal))
            {
                throw Conflict(
                    "AAA-IDEMPOTENCY-0002",
                    "La clave idempotente ya se usó con otra petición.");
            }

            if (existing.Status != IdempotencyStatus.Completed
                || existing.ResultJson is null)
            {
                throw Conflict(
                    "AAA-IDEMPOTENCY-0003",
                    "La operación idempotente anterior no está completa.");
            }

            var result = JsonSerializer.Deserialize<T>(
                existing.ResultJson,
                IdempotencyJsonOptions)
                ?? throw new InvalidOperationException(
                    "El resultado idempotente guardado no es válido.");
            return new IdempotencyStart<T>(existing, result);
        }

        var record = new IdempotencyRecord(
            commandType,
            scopedKey,
            requestHash,
            _clock.UtcNow);
        _dbContext.IdempotencyRecords.Add(record);
        return new IdempotencyStart<T>(record, null);
    }

    private async Task LockStockAsync(
        IEnumerable<(Guid MaterialId, Guid WarehouseId)> stockKeys,
        CancellationToken cancellationToken)
    {
        var resolved =
            new List<(Guid MaterialId, Guid WarehouseId, Guid LocationId, Guid LotId)>();
        foreach (var key in stockKeys.Distinct())
        {
            var locationId = await ResolveDefaultLocationIdAsync(
                key.WarehouseId,
                cancellationToken);
            resolved.Add((key.MaterialId, key.WarehouseId, locationId, Guid.Empty));
        }

        await LockStockAsync(resolved, cancellationToken);
    }

    private async Task AcquireAtpAdvisoryLockAsync(
        Guid materialId,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var atpLockKey = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"atp:{materialId:N}:{warehouseId:N}");
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({atpLockKey}, 0))",
            cancellationToken);
    }

    private Task LockStockAsync(
        IEnumerable<(Guid MaterialId, Guid WarehouseId, Guid LocationId, Guid LotId)> stockKeys,
        CancellationToken cancellationToken) =>
        LockStockOwnedAsync(
            stockKeys.Select(value =>
                (value.MaterialId, value.WarehouseId, value.LocationId, value.LotId, Guid.Empty)),
            cancellationToken);

    private async Task LockStockOwnedAsync(
        IEnumerable<(Guid MaterialId, Guid WarehouseId, Guid LocationId, Guid LotId, Guid OwnerCustomerId)> stockKeys,
        CancellationToken cancellationToken)
    {
        foreach (var key in stockKeys
                     .Distinct()
                     .OrderBy(value => value.MaterialId)
                     .ThenBy(value => value.WarehouseId)
                     .ThenBy(value => value.LocationId)
                     .ThenBy(value => value.LotId)
                     .ThenBy(value => value.OwnerCustomerId))
        {
            if (key.LocationId == Guid.Empty)
            {
                throw Validation(
                    "AAA-INVENTORY-0007",
                    "La ubicación es obligatoria para bloquear stock.");
            }

            var advisoryKey =
                $"stock:{key.MaterialId:N}:{key.WarehouseId:N}:{key.LocationId:N}:{key.LotId:N}:{key.OwnerCustomerId:N}";
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({advisoryKey}, 0))",
                cancellationToken);
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM stock_balances WHERE material_id = {key.MaterialId} AND warehouse_id = {key.WarehouseId} AND location_id = {key.LocationId} AND lot_id = {key.LotId} AND owner_customer_id = {key.OwnerCustomerId} FOR UPDATE",
                cancellationToken);
        }
    }

    private async Task<StockBalance> GetOrCreateBalanceAsync(
        Guid materialId,
        Guid warehouseId,
        CancellationToken cancellationToken,
        Guid? locationId = null,
        Guid lotId = default,
        Guid ownerCustomerId = default)
    {
        var resolvedLocationId = locationId
            ?? await ResolveDefaultLocationIdAsync(warehouseId, cancellationToken);
        if (resolvedLocationId == Guid.Empty)
        {
            throw Validation(
                "AAA-INVENTORY-0007",
                "La ubicación es obligatoria para el saldo de stock.");
        }

        var balance = _dbContext.StockBalances.Local.SingleOrDefault(
            value =>
                value.MaterialId == materialId
                && value.WarehouseId == warehouseId
                && value.LocationId == resolvedLocationId
                && value.LotId == lotId
                && value.OwnerCustomerId == ownerCustomerId);
        balance ??= await _dbContext.StockBalances.SingleOrDefaultAsync(
            value =>
                value.MaterialId == materialId
                && value.WarehouseId == warehouseId
                && value.LocationId == resolvedLocationId
                && value.LotId == lotId
                && value.OwnerCustomerId == ownerCustomerId,
            cancellationToken);

        if (balance is null)
        {
            // Migrate legacy warehouse-only balance (LocationId empty) onto DEFAULT
            // only for the non-lot sentinel grain / house stock.
            if (lotId == Guid.Empty && ownerCustomerId == Guid.Empty)
            {
                var legacy = _dbContext.StockBalances.Local.SingleOrDefault(
                    value =>
                        value.MaterialId == materialId
                        && value.WarehouseId == warehouseId
                        && value.LocationId == Guid.Empty
                        && value.LotId == Guid.Empty
                        && value.OwnerCustomerId == Guid.Empty);
                legacy ??= await _dbContext.StockBalances.SingleOrDefaultAsync(
                    value =>
                        value.MaterialId == materialId
                        && value.WarehouseId == warehouseId
                        && value.LocationId == Guid.Empty
                        && value.LotId == Guid.Empty
                        && value.OwnerCustomerId == Guid.Empty,
                    cancellationToken);
                if (legacy is not null)
                {
                    legacy.AssignLocation(resolvedLocationId);
                    return legacy;
                }
            }

            balance = new StockBalance(
                materialId,
                warehouseId,
                resolvedLocationId,
                lotId,
                ownerCustomerId: ownerCustomerId);
            _dbContext.StockBalances.Add(balance);
        }

        return balance;
    }

    private async Task<Guid> ResolveReceiptLotAsync(
        Material material,
        string? lotCode,
        DateOnly? lotExpiryDate,
        CancellationToken cancellationToken)
    {
        if (!material.RequiresLot)
        {
            return Guid.Empty;
        }

        if (string.IsNullOrWhiteSpace(lotCode))
        {
            throw Validation(
                "AAA-LOT-0002",
                $"El material {material.Code} requiere código de lote en la recepción.");
        }

        var normalized = UnitOfMeasure.NormalizeCode(lotCode, 40);
        var existing = await _dbContext.Lots.SingleOrDefaultAsync(
            value =>
                value.MaterialId == material.Id
                && value.Code == normalized,
            cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var lot = new Lot(
            material.Id,
            normalized,
            lotExpiryDate,
            _clock.UtcNow);
        _dbContext.Lots.Add(lot);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return lot.Id;
    }

    private async Task<IReadOnlyList<PickSuggestion>> SuggestPickAsync(
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        CancellationToken cancellationToken,
        Guid? requiredLotId,
        Guid ownerCustomerId = default)
    {
        if (materialId == Guid.Empty || warehouseId == Guid.Empty)
        {
            throw Validation(
                "AAA-FEFO-0001",
                "El material y el almacén son obligatorios.");
        }

        if (quantity <= 0)
        {
            throw Validation(
                "AAA-FEFO-0002",
                "La cantidad a sugerir debe ser positiva.");
        }

        var balancesQuery = _dbContext.StockBalances
            .AsNoTracking()
            .Where(value =>
                value.MaterialId == materialId
                && value.WarehouseId == warehouseId
                && value.Status == StockStatus.Available
                && value.Quantity > 0
                && value.OwnerCustomerId == ownerCustomerId);
        if (requiredLotId is { } lotFilter && lotFilter != Guid.Empty)
        {
            balancesQuery = balancesQuery.Where(value => value.LotId == lotFilter);
        }

        var balances = await balancesQuery.ToListAsync(cancellationToken);
        var lotIds = balances
            .Select(value => value.LotId)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        var lots = await _dbContext.Lots
            .AsNoTracking()
            .Where(value => lotIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        var ordered = balances
            .Select(balance =>
            {
                DateOnly? expiry = null;
                if (balance.LotId != Guid.Empty
                    && lots.TryGetValue(balance.LotId, out var lot))
                {
                    expiry = lot.ExpiryDate;
                }

                return (Balance: balance, Expiry: expiry);
            })
            .OrderBy(value => value.Expiry is null) // nulls last
            .ThenBy(value => value.Expiry)
            .ThenBy(value => value.Balance.LotId)
            .ThenBy(value => value.Balance.LocationId)
            .ToArray();

        var remaining = quantity;
        var suggestions = new List<PickSuggestion>();
        foreach (var entry in ordered)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(remaining, entry.Balance.Quantity);
            suggestions.Add(
                new PickSuggestion(
                    entry.Balance.LocationId,
                    entry.Balance.LotId,
                    take,
                    entry.Expiry));
            remaining -= take;
        }

        return suggestions;
    }

    private async Task<Guid> ResolveDefaultLocationIdAsync(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var warehouse = await _dbContext.Warehouses
            .SingleOrDefaultAsync(value => value.Id == warehouseId, cancellationToken)
            ?? throw NotFound(
                "AAA-INVENTORY-0002",
                "El almacén indicado no existe.");

        var root = await _dbContext.LocationNodes
            .FirstOrDefaultAsync(
                value =>
                    value.WarehouseId == warehouseId
                    && value.Kind == LocationNodeKind.Root,
                cancellationToken);
        if (root is null)
        {
            root = await _dbContext.LocationNodes
                .FirstOrDefaultAsync(
                    value =>
                        value.WarehouseId == warehouseId
                        && value.Code == "DEFAULT",
                    cancellationToken);
        }

        if (root is not null)
        {
            await EnsureRootIdentityAsync(root, warehouse, cancellationToken);
            return root.Id;
        }

        var node = new LocationNode(
            warehouseId,
            warehouse.Code,
            warehouse.Name,
            LocationNodeKind.Root,
            null);
        _dbContext.LocationNodes.Add(node);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return node.Id;
    }

    private async Task EnsureRootIdentityAsync(
        LocationNode root,
        Warehouse warehouse,
        CancellationToken cancellationToken)
    {
        var changed = false;
        if (string.Equals(root.Code, "DEFAULT", StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await _dbContext.LocationNodes.AnyAsync(
                value =>
                    value.WarehouseId == warehouse.Id
                    && value.Id != root.Id
                    && value.Code == warehouse.Code,
                cancellationToken);
            root.Recode(
                codeTaken
                    ? TruncateCode($"R-{warehouse.Code}", 40)
                    : warehouse.Code);
            changed = true;
        }

        if (string.Equals(root.Name, "Ubicación por defecto", StringComparison.OrdinalIgnoreCase))
        {
            root.Rename("Raíz");
            changed = true;
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string TruncateCode(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private async Task<Dictionary<Guid, Guid>> ResolveDefaultLocationsAsync(
        IEnumerable<Guid> warehouseIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Guid>();
        foreach (var warehouseId in warehouseIds.Distinct())
        {
            result[warehouseId] = await ResolveDefaultLocationIdAsync(
                warehouseId,
                cancellationToken);
        }

        return result;
    }

    private async Task EnsureWarehousesExistAsync(
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken)
    {
        var distinct = warehouseIds.Distinct().ToArray();
        var count = await _dbContext.Warehouses.CountAsync(
            value => distinct.Contains(value.Id),
            cancellationToken);
        if (count != distinct.Length)
        {
            throw NotFound(
                "AAA-INVENTORY-0005",
                "Uno o más almacenes no existen.");
        }
    }

    private static void EnsurePostingLines(int count)
    {
        if (count == 0)
        {
            throw Validation(
                "AAA-INVENTORY-0006",
                "La contabilización debe contener líneas.");
        }
    }

    private sealed record IdempotencyStart<T>(
        IdempotencyRecord Record,
        T? Result)
        where T : class;
}
