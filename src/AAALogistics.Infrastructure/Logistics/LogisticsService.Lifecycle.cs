using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Purchasing;
using AAALogistics.Domain.Sales;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Logistics;

public sealed partial class LogisticsService
{
    public async Task<PurchaseOrderView> CancelPurchaseOrderOpenAsync(
        Guid purchaseOrderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Validation(
                "AAA-PURCHASE-0012",
                "El motivo de cancelación es obligatorio.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var order = await LoadPurchaseOrderAsync(purchaseOrderId, cancellationToken);
        var lines = await _dbContext.PurchaseOrderLines
            .Where(value => value.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);
        try
        {
            order.CancelOpen(lines);
        }
        catch (InvalidOperationException exception)
        {
            throw Conflict("AAA-PURCHASE-0010", exception.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<PurchaseOrderView> ClosePurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var order = await LoadPurchaseOrderAsync(purchaseOrderId, cancellationToken);
        var lines = await _dbContext.PurchaseOrderLines
            .Where(value => value.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);
        try
        {
            order.Close(lines);
        }
        catch (InvalidOperationException exception)
        {
            throw Conflict("AAA-PURCHASE-0011", exception.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<SalesOrderView> CancelSalesOrderAsync(
        Guid salesOrderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Validation(
                "AAA-SALES-0013",
                "El motivo de cancelación es obligatorio.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var (order, lines) = await LoadSalesOrderAsync(salesOrderId, cancellationToken);
        try
        {
            order.Cancel(lines);
        }
        catch (InvalidOperationException exception)
        {
            throw Conflict("AAA-SALES-0010", exception.Message);
        }

        await ReleaseSalesOrderReservationsAsync(salesOrderId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<SalesOrderView> CloseSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var (order, lines) = await LoadSalesOrderAsync(salesOrderId, cancellationToken);
        try
        {
            order.Close(lines);
        }
        catch (InvalidOperationException exception)
        {
            throw Conflict("AAA-SALES-0011", exception.Message);
        }

        await ReleaseSalesOrderReservationsAsync(salesOrderId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    private async Task ReleaseSalesOrderReservationsAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        var reservations = await _dbContext.StockReservations
            .Where(value => value.SalesOrderId == salesOrderId && value.IsActive)
            .ToListAsync(cancellationToken);
        var utc = _clock.UtcNow;
        foreach (var reservation in reservations)
        {
            reservation.Release(utc);
        }
    }

    public async Task<SalesOrderView> RejectSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var (order, lines) = await LoadSalesOrderAsync(salesOrderId, cancellationToken);
        try
        {
            order.Reject();
        }
        catch (InvalidOperationException exception)
        {
            throw Conflict("AAA-SALES-0012", exception.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<StockView> SetMinQuantityAsync(
        Guid materialId,
        Guid warehouseId,
        decimal minQuantity,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var balance = await GetOrCreateBalanceAsync(
            materialId,
            warehouseId,
            cancellationToken);

        try
        {
            balance.SetMinQuantity(minQuantity);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw Validation("AAA-STOCK-0010", exception.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var stock = await GetStockAsync(cancellationToken);
        return stock.Single(value =>
            value.MaterialId == materialId
            && value.WarehouseId == warehouseId
            && value.LocationId == balance.LocationId
            && value.LotId == balance.LotId);
    }

    public async Task<StockView> SetStockStatusAsync(
        SetStockStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.LocationId == Guid.Empty)
        {
            throw Validation(
                "AAA-STOCK-0011",
                "La ubicación es obligatoria para cambiar el estado de stock.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var balance = await GetOrCreateBalanceAsync(
            command.MaterialId,
            command.WarehouseId,
            cancellationToken,
            command.LocationId,
            command.LotId);
        balance.SetStatus(command.Status);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var stock = await GetStockAsync(cancellationToken);
        return stock.Single(value =>
            value.MaterialId == command.MaterialId
            && value.WarehouseId == command.WarehouseId
            && value.LocationId == balance.LocationId
            && value.LotId == balance.LotId);
    }

    public async Task<IReadOnlyCollection<StockView>> ListBelowMinimumAsync(
        CancellationToken cancellationToken = default)
    {
        var stock = await GetStockAsync(cancellationToken);
        return stock
            .Where(value => value.Quantity < value.MinQuantity)
            .ToArray();
    }

    public async Task<InventoryCountView> CreateInventoryCountAsync(
        CreateInventoryCountCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw Validation(
                "AAA-COUNT-0001",
                "El recuento debe incluir al menos una línea.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        if (!await _dbContext.Warehouses.AnyAsync(
                value => value.Id == command.WarehouseId,
                cancellationToken))
        {
            throw NotFound("AAA-COUNT-0002", "El almacén no existe.");
        }

        var number = await TakeDocumentNumberAsync(
            "INVENTORY_COUNT",
            "IC",
            cancellationToken);
        var count = new InventoryCount(
            number,
            command.WarehouseId,
            _clock.UtcNow,
            _requestContext.UserId);
        _dbContext.InventoryCounts.Add(count);
        var lines = new List<InventoryCountLine>();
        foreach (var line in command.Lines)
        {
            var system = await _dbContext.StockBalances
                .AsNoTracking()
                .Where(value =>
                    value.MaterialId == line.MaterialId
                    && value.WarehouseId == command.WarehouseId)
                .SumAsync(value => value.Quantity, cancellationToken);
            var entity = new InventoryCountLine(
                count.Id,
                line.MaterialId,
                system,
                line.CountedQuantity);
            _dbContext.InventoryCountLines.Add(entity);
            lines.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToCountView(count, lines);
    }

    public async Task<IReadOnlyCollection<OrderStatusReportRow>> ReportOrdersByStatusAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var purchases = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .Select(value => new OrderStatusReportRow(
                "PurchaseOrder",
                value.Id,
                value.Number,
                value.Status.ToString(),
                value.SupplierId))
            .ToListAsync(cancellationToken);
        var sales = await _dbContext.SalesOrders
            .AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .Select(value => new OrderStatusReportRow(
                "SalesOrder",
                value.Id,
                value.Number,
                value.Status.ToString(),
                value.CustomerId))
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return purchases.Concat(sales).ToArray();
    }

    public async Task<OtifReportRow> ReportOtifAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var delivered = await _dbContext.SalesOrders
            .AsNoTracking()
            .Where(value =>
                value.Status == SalesOrderStatus.Delivered
                || value.Status == SalesOrderStatus.Closed)
            .Select(value => new
            {
                value.Id,
                value.RequestedDeliveryDate,
            })
            .ToListAsync(cancellationToken);
        if (delivered.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new OtifReportRow(0, 0, 0m);
        }

        var orderIds = delivered.Select(value => value.Id).ToArray();
        var lastDeliveryByOrder = await _dbContext.Deliveries
            .AsNoTracking()
            .Where(value => orderIds.Contains(value.SalesOrderId))
            .GroupBy(value => value.SalesOrderId)
            .Select(group => new
            {
                SalesOrderId = group.Key,
                LastDocumentDate = group.Max(value => value.DocumentDate),
            })
            .ToListAsync(cancellationToken);
        var lastMap = lastDeliveryByOrder.ToDictionary(
            value => value.SalesOrderId,
            value => value.LastDocumentDate);
        var onTime = 0;
        foreach (var order in delivered)
        {
            if (!lastMap.TryGetValue(order.Id, out var lastDate))
            {
                continue;
            }

            if (lastDate <= order.RequestedDeliveryDate)
            {
                onTime++;
            }
        }

        var percent = delivered.Count == 0
            ? 0m
            : Math.Round(
                100m * onTime / delivered.Count,
                2,
                MidpointRounding.AwayFromZero);
        await transaction.CommitAsync(cancellationToken);
        return new OtifReportRow(delivered.Count, onTime, percent);
    }

    private async Task<PurchaseOrder> LoadPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken) =>
        await _dbContext.PurchaseOrders.SingleOrDefaultAsync(
            value => value.Id == purchaseOrderId,
            cancellationToken)
        ?? throw NotFound("AAA-PURCHASE-0005", "El pedido de compra no existe.");

    private async Task<(SalesOrder Order, List<SalesOrderLine> Lines)> LoadSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == salesOrderId,
            cancellationToken)
            ?? throw NotFound("AAA-SALES-0005", "El pedido de venta no existe.");
        var lines = await _dbContext.SalesOrderLines
            .Where(value => value.SalesOrderId == salesOrderId)
            .ToListAsync(cancellationToken);
        return (order, lines);
    }

    private static InventoryCountView ToCountView(
        InventoryCount count,
        IEnumerable<InventoryCountLine> lines) =>
        new(
            count.Id,
            count.Number,
            count.WarehouseId,
            count.Status.ToString(),
            lines.Select(line => new InventoryCountLineView(
                line.MaterialId,
                line.SystemQuantity,
                line.CountedQuantity,
                line.Difference)).ToArray());
}
