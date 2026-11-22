using System.Globalization;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Purchasing;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Warehouse;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Logistics;

public sealed partial class LogisticsService(
    ApplicationDbContext dbContext,
    IClock clock,
    IRequestContext requestContext,
    ICustomerRlsSession rlsSession,
    IOptions<DemoOptions> demoOptions,
    IIndustrialLogisticsService industrialLogistics,
    IOperationsRealtime? operationsRealtime = null) : ILogisticsService
{
    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    private readonly ICustomerRlsSession _rlsSession =
        rlsSession ?? throw new ArgumentNullException(nameof(rlsSession));

    private readonly DemoOptions _demoOptions =
        demoOptions?.Value ?? throw new ArgumentNullException(nameof(demoOptions));

    private readonly IIndustrialLogisticsService _industrial =
        industrialLogistics
        ?? throw new ArgumentNullException(nameof(industrialLogistics));

    private readonly IOperationsRealtime _operationsRealtime =
        operationsRealtime ?? new NullOperationsRealtime();

    private sealed class NullOperationsRealtime : IOperationsRealtime
    {
        public Task NotifyStockChangedAsync(
            Guid warehouseId,
            string message,
            CancellationToken cancellationToken = default)
        {
            _ = warehouseId;
            _ = message;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private Task NotifyStockAsync(
        Guid warehouseId,
        string message,
        CancellationToken cancellationToken) =>
        _operationsRealtime.NotifyStockChangedAsync(
            warehouseId,
            message,
            cancellationToken);

    private async Task<T> ReadWithRlsAsync<T>(
        Func<CancellationToken, Task<T>> query,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var result = await query(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<UnitOfMeasureView> CreateUnitOfMeasureAsync(
        CreateUnitOfMeasureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var unit = new UnitOfMeasure(
            command.Code,
            command.Name,
            command.DecimalScale);
        if (await _dbContext.UnitsOfMeasure.AnyAsync(
                value => value.Code == unit.Code,
                cancellationToken))
        {
            throw Conflict(
                "AAA-CATALOG-0001",
                "Ya existe una unidad con ese código.");
        }

        _dbContext.UnitsOfMeasure.Add(unit);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(unit);
    }

    public async Task<UnitOfMeasureView> UpdateUnitOfMeasureAsync(
        UpdateUnitOfMeasureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var unit = await _dbContext.UnitsOfMeasure.SingleOrDefaultAsync(
            value => value.Id == command.UnitId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-CATALOG-0002",
                "La unidad indicada no existe.");
        unit.Update(command.Name, command.DecimalScale);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(unit);
    }

    public async Task<EnsureDefaultUnitsResult> EnsureDefaultUnitsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);

        var defaults = new (string Code, string Name, int Scale)[]
        {
            ("UN", "Unidad", 0),
            ("KG", "Kilogramo", 3),
            ("L", "Litro", 3),
            ("PCS", "Piezas", 0),
        };

        var existing = await _dbContext.UnitsOfMeasure
            .Select(value => value.Code)
            .ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var created = new List<string>();
        foreach (var (code, name, scale) in defaults)
        {
            if (existingSet.Contains(code))
            {
                continue;
            }

            _dbContext.UnitsOfMeasure.Add(new UnitOfMeasure(code, name, scale));
            created.Add(code);
        }

        if (created.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new EnsureDefaultUnitsResult(created.Count, created);
    }

    public async Task<IReadOnlyCollection<UnitOfMeasureView>> ListUnitsAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadWithRlsAsync(
            async ct =>
            {
                IReadOnlyCollection<UnitOfMeasureView> rows =
                    await _dbContext.UnitsOfMeasure
                        .AsNoTracking()
                        .OrderBy(value => value.Code)
                        .Select(value => new UnitOfMeasureView(
                            value.Id,
                            value.Code,
                            value.Name,
                            value.DecimalScale))
                        .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    public async Task<MaterialView> CreateMaterialAsync(
        CreateMaterialCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        if (!await _dbContext.UnitsOfMeasure.AnyAsync(
                value => value.Id == command.UnitId,
                cancellationToken))
        {
            throw NotFound(
                "AAA-CATALOG-0002",
                "La unidad indicada no existe.");
        }

        var material = new Material(
            command.Code,
            command.DisplayName,
            command.UnitId,
            command.CatalogPrice,
            command.Currency,
            command.ExternalId,
            command.RequiresLot,
            command.Description);
        if (await _dbContext.Materials.AnyAsync(
                value => value.Code == material.Code,
                cancellationToken))
        {
            throw Conflict(
                "AAA-CATALOG-0003",
                "Ya existe un material con ese código.");
        }

        _dbContext.Materials.Add(material);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(material);
    }

    public async Task<MaterialView> UpdateMaterialAsync(
        UpdateMaterialCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var material = await _dbContext.Materials.SingleOrDefaultAsync(
            value => value.Id == command.MaterialId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-CATALOG-0004",
                "El material indicado no existe.");
        material.Update(
            command.DisplayName,
            command.CatalogPrice,
            command.Currency,
            command.IsActive,
            command.ExternalId,
            command.Description);
        material.SetRequiresLot(command.RequiresLot);
        if (!command.IsActive)
        {
            material.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(material);
    }

    public async Task<IReadOnlyCollection<MaterialView>> ListMaterialsAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadWithRlsAsync(
            async ct =>
            {
                IReadOnlyCollection<MaterialView> rows = await _dbContext.Materials
                    .AsNoTracking()
                    .OrderBy(value => value.Code)
                    .Select(value => new MaterialView(
                        value.Id,
                        value.Code,
                        value.DisplayName,
                        value.Description,
                        value.UnitId,
                        value.CatalogPrice,
                        value.Currency,
                        value.IsActive,
                        value.ExternalId,
                        value.RequiresLot))
                    .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    public async Task<WarehouseView> CreateWarehouseAsync(
        CreateWarehouseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var siteKind = ParseSiteKind(command.SiteKind);
        var warehouse = new Warehouse(command.Code, command.Name, siteKind);
        if (await _dbContext.Warehouses.AnyAsync(
                value => value.Code == warehouse.Code,
                cancellationToken))
        {
            throw Conflict(
                "AAA-INVENTORY-0001",
                "Ya existe un almacén con ese código.");
        }

        _dbContext.Warehouses.Add(warehouse);
        _dbContext.LocationNodes.Add(
            new LocationNode(
                warehouse.Id,
                warehouse.Code,
                warehouse.Name,
                LocationNodeKind.Root,
                null));
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(warehouse);
    }

    public async Task DeleteWarehouseAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var warehouse = await _dbContext.Warehouses.SingleOrDefaultAsync(
            value => value.Id == warehouseId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-INVENTORY-0002",
                "El almacén indicado no existe.");

        if (await _dbContext.StockBalances.AnyAsync(
                value => value.WarehouseId == warehouseId && value.Quantity > 0,
                cancellationToken))
        {
            throw Conflict(
                "AAA-WH-0011",
                "No se puede eliminar: hay stock. Traslade o ajuste a cero primero.");
        }

        if (await _dbContext.StockMovements.AnyAsync(
                value => value.WarehouseId == warehouseId,
                cancellationToken))
        {
            throw Conflict(
                "AAA-WH-0012",
                "No se puede eliminar: hay movimientos históricos. Use wipe-catalog en vacío local o desactive.");
        }

        if (await _dbContext.StockReservations.AnyAsync(
                value => value.WarehouseId == warehouseId,
                cancellationToken))
        {
            throw Conflict(
                "AAA-WH-0013",
                "No se puede eliminar: hay reservas de stock activas.");
        }

        var locationIds = await _dbContext.LocationNodes
            .Where(value => value.WarehouseId == warehouseId)
            .Select(value => value.Id)
            .ToListAsync(cancellationToken);
        if (locationIds.Count > 0
            && await _dbContext.PrepPickLines.AnyAsync(
                value => locationIds.Contains(value.SuggestedLocationId),
                cancellationToken))
        {
            throw Conflict(
                "AAA-WH-0014",
                "No se puede eliminar: está referenciado en picking.");
        }

        if (await _dbContext.PurchaseOrders.AnyAsync(
                value => value.DefaultWarehouseId == warehouseId,
                cancellationToken)
            || await _dbContext.SalesOrders.AnyAsync(
                value => value.DefaultWarehouseId == warehouseId,
                cancellationToken)
            || await _dbContext.GoodsReceipts.AnyAsync(
                value => value.DefaultWarehouseId == warehouseId,
                cancellationToken)
            || await _dbContext.Deliveries.AnyAsync(
                value => value.DefaultWarehouseId == warehouseId,
                cancellationToken)
            || await _dbContext.InventoryCounts.AnyAsync(
                value => value.WarehouseId == warehouseId,
                cancellationToken)
            || await _dbContext.PurchaseRequisitions.AnyAsync(
                value => value.WarehouseId == warehouseId,
                cancellationToken))
        {
            throw Conflict(
                "AAA-WH-0015",
                "No se puede eliminar: hay documentos (pedidos, recepciones, entregas, recuentos) que lo referencian.");
        }

        // Vínculos planta↔almacén: se quitan al borrar (no bloquean).
        var plantLinks = await _dbContext.PlantWarehouses
            .Where(value => value.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        if (plantLinks.Count > 0)
        {
            _dbContext.PlantWarehouses.RemoveRange(plantLinks);
        }

        var emptyBalances = await _dbContext.StockBalances
            .Where(value => value.WarehouseId == warehouseId && value.Quantity == 0)
            .ToListAsync(cancellationToken);
        if (emptyBalances.Count > 0)
        {
            _dbContext.StockBalances.RemoveRange(emptyBalances);
        }

        // Cascada: raíz, zonas y huecos vacíos del almacén.
        var nodes = await _dbContext.LocationNodes
            .Where(value => value.WarehouseId == warehouseId)
            .ToListAsync(cancellationToken);
        if (nodes.Count > 0)
        {
            _dbContext.LocationNodes.RemoveRange(nodes);
        }

        _dbContext.Warehouses.Remove(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<WarehouseView> UpdateWarehouseAsync(
        UpdateWarehouseCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var warehouse = await _dbContext.Warehouses.SingleOrDefaultAsync(
            value => value.Id == command.WarehouseId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-INVENTORY-0002",
                "El almacén indicado no existe.");
        var siteKind = string.IsNullOrWhiteSpace(command.SiteKind)
            ? (WarehouseSiteKind?)null
            : ParseSiteKind(command.SiteKind);
        warehouse.Update(command.Name, siteKind);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(warehouse);
    }

    public async Task<IReadOnlyCollection<WarehouseView>> ListWarehousesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadWithRlsAsync(
            async ct =>
            {
                IReadOnlyCollection<WarehouseView> rows = await _dbContext.Warehouses
                    .AsNoTracking()
                    .OrderBy(value => value.Code)
                    .Select(value => new WarehouseView(
                        value.Id,
                        value.Code,
                        value.Name,
                        value.SiteKind.ToString()))
                    .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    public async Task<PartyView> CreateSupplierAsync(
        CreatePartyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        if (await _dbContext.Suppliers.AnyAsync(
                value => value.TaxId == command.TaxId.ToUpper(),
                cancellationToken))
        {
            throw Conflict(
                "AAA-PURCHASE-0001",
                "Ya existe un proveedor con ese identificador fiscal.");
        }

        var supplier = new Supplier(
            command.Name,
            command.TaxId,
            command.AddressLine1,
            command.City,
            command.PostalCode,
            command.CountryCode,
            command.AddressLine2,
            command.Region,
            command.ExternalId,
            command.LeadTimeDays);
        _dbContext.Suppliers.Add(supplier);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(supplier);
    }

    public async Task<PartyView> UpdateSupplierAsync(
        UpdatePartyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var supplier = await _dbContext.Suppliers.SingleOrDefaultAsync(
            value => value.Id == command.PartyId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-PURCHASE-0002",
                "El proveedor indicado no existe.");
        supplier.Update(
            command.Name,
            command.AddressLine1,
            command.City,
            command.PostalCode,
            command.CountryCode,
            command.IsActive,
            command.AddressLine2,
            command.Region,
            command.ExternalId,
            command.LeadTimeDays);
        if (!command.IsActive)
        {
            supplier.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(supplier);
    }

    public async Task<IReadOnlyCollection<PartyView>> ListSuppliersAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadWithRlsAsync(
            async ct =>
            {
                IReadOnlyCollection<PartyView> rows = await _dbContext.Suppliers
                    .AsNoTracking()
                    .OrderBy(value => value.Name)
                    .Select(value => new PartyView(
                        value.Id,
                        value.Name,
                        value.TaxId,
                        value.City,
                        value.CountryCode,
                        value.IsActive,
                        value.ExternalId,
                        value.AddressLine1,
                        value.AddressLine2,
                        value.Region,
                        value.PostalCode,
                        value.LeadTimeDays))
                    .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    public async Task<PartyView> CreateCustomerAsync(
        CreatePartyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        if (await _dbContext.Customers.IgnoreQueryFilters().AnyAsync(
                value => value.TaxId == command.TaxId.ToUpper(),
                cancellationToken))
        {
            throw Conflict(
                "AAA-SALES-0001",
                "Ya existe un cliente con ese identificador fiscal.");
        }

        var customer = new Customer(
            command.Name,
            command.TaxId,
            command.AddressLine1,
            command.City,
            command.PostalCode,
            command.CountryCode,
            command.AddressLine2,
            command.Region,
            command.ExternalId);
        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(customer);
    }

    public async Task<PartyView> UpdateCustomerAsync(
        UpdatePartyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var customer = await _dbContext.Customers.SingleOrDefaultAsync(
            value => value.Id == command.PartyId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-SALES-0002",
                "El cliente indicado no existe.");
        customer.Update(
            command.Name,
            command.AddressLine1,
            command.City,
            command.PostalCode,
            command.CountryCode,
            command.IsActive,
            command.AddressLine2,
            command.Region,
            command.ExternalId);
        if (!command.IsActive)
        {
            customer.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(customer);
    }

    public async Task<IReadOnlyCollection<PartyView>> ListCustomersAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadWithRlsAsync(
            async ct =>
            {
                IReadOnlyCollection<PartyView> rows = await _dbContext.Customers
                    .AsNoTracking()
                    .OrderBy(value => value.Name)
                    .Select(value => new PartyView(
                        value.Id,
                        value.Name,
                        value.TaxId,
                        value.City,
                        value.CountryCode,
                        value.IsActive,
                        value.ExternalId,
                        value.AddressLine1,
                        value.AddressLine2,
                        value.Region,
                        value.PostalCode))
                    .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    public async Task<PurchaseOrderView> CreatePurchaseOrderAsync(
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw Validation(
                "AAA-PURCHASE-0002",
                "El pedido de compra debe contener líneas.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);

        var supplier = await _dbContext.Suppliers.SingleOrDefaultAsync(
            value => value.Id == command.SupplierId && value.IsActive,
            cancellationToken)
            ?? throw NotFound(
                "AAA-PURCHASE-0003",
                "El proveedor no existe o no está activo.");
        _ = supplier;

        if (!await _dbContext.Warehouses.AnyAsync(
                value => value.Id == command.DefaultWarehouseId,
                cancellationToken))
        {
            throw NotFound(
                "AAA-PURCHASE-0004",
                "El almacén indicado no existe.");
        }

        var materials = await LoadMaterialsAsync(
            command.Lines.Select(value => value.MaterialId),
            cancellationToken);
        var number = await TakeDocumentNumberAsync(
            "PURCHASE_ORDER",
            "PO",
            cancellationToken);
        var order = new PurchaseOrder(
            number,
            command.SupplierId,
            command.DocumentDate,
            command.ExpectedReceiptDate,
            command.DefaultWarehouseId,
            command.Currency,
            _clock.UtcNow,
            command.ExternalId);
        var lines = command.Lines.Select(line =>
        {
            var (material, unit) = GetMaterial(materials, line.MaterialId);
            EnsureQuantityScale(line.Quantity, unit);
            return new PurchaseOrderLine(
                order.Id,
                material.Id,
                line.Quantity,
                line.PurchasePrice,
                command.Currency,
                unit.Code);
        }).ToArray();

        _dbContext.PurchaseOrders.Add(order);
        _dbContext.PurchaseOrderLines.AddRange(lines);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<SalesOrderView> CreateSalesOrderAsync(
        CreateSalesOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw Validation(
                "AAA-SALES-0002",
                "El pedido de venta debe contener líneas.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var customer = await _dbContext.Customers.SingleOrDefaultAsync(
            value => value.Id == command.CustomerId && value.IsActive,
            cancellationToken)
            ?? throw NotFound(
                "AAA-SALES-0003",
                "El cliente no existe o no está activo.");
        if (!await _dbContext.Warehouses.AnyAsync(
                value => value.Id == command.DefaultWarehouseId,
                cancellationToken))
        {
            throw NotFound(
                "AAA-SALES-0004",
                "El almacén indicado no existe.");
        }

        var materials = await LoadMaterialsAsync(
            command.Lines.Select(value => value.MaterialId),
            cancellationToken);
        var number = await TakeDocumentNumberAsync(
            "SALES_ORDER",
            "SO",
            cancellationToken);
        var order = new SalesOrder(
            number,
            customer.Id,
            command.DefaultWarehouseId,
            command.RequestedDeliveryDate,
            command.Currency,
            customer.AddressLine1,
            customer.AddressLine2,
            customer.City,
            customer.Region,
            customer.PostalCode,
            customer.CountryCode,
            _clock.UtcNow,
            command.ExternalId);
        order.SetTradeTerms(command.Incoterms, command.CustomsReference);
        var lines = command.Lines.Select(line =>
        {
            var (material, unit) = GetMaterial(materials, line.MaterialId);
            EnsureQuantityScale(line.Quantity, unit);
            return new SalesOrderLine(
                order.Id,
                material.Id,
                line.Quantity,
                material.CatalogPrice,
                command.Currency,
                unit.Code);
        }).ToArray();

        _dbContext.SalesOrders.Add(order);
        _dbContext.SalesOrderLines.AddRange(lines);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<SalesOrderView> UpdateSalesOrderTradeTermsAsync(
        UpdateSalesOrderTradeTermsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var order = await _dbContext.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == command.SalesOrderId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-SALES-0005",
                "El pedido de venta no existe.");
        if (order.Status is SalesOrderStatus.Cancelled
            or SalesOrderStatus.Closed
            or SalesOrderStatus.Rejected)
        {
            throw Validation(
                "AAA-SALES-0006",
                "El pedido no admite actualización de Incoterms.");
        }

        order.SetTradeTerms(command.Incoterms, command.CustomsReference);
        var lines = await _dbContext.SalesOrderLines
            .Where(value => value.SalesOrderId == order.Id)
            .ToListAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<IReadOnlyCollection<PurchaseOrderView>> ListPurchaseOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var orders = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        if (orders.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        var ids = orders.Select(value => value.Id).ToArray();
        var lines = await _dbContext.PurchaseOrderLines
            .AsNoTracking()
            .Where(value => ids.Contains(value.PurchaseOrderId))
            .ToListAsync(cancellationToken);
        var linesByOrder = lines
            .GroupBy(value => value.PurchaseOrderId)
            .ToDictionary(
                group => group.Key,
                group => (IEnumerable<PurchaseOrderLine>)group.ToArray());
        var result = orders
            .Select(order => ToView(
                order,
                linesByOrder.TryGetValue(order.Id, out var orderLines)
                    ? orderLines
                    : []))
            .ToArray();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<PurchaseOrderView> GetPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var order = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == purchaseOrderId, cancellationToken)
            ?? throw NotFound(
                "AAA-PURCHASE-0005",
                "El pedido de compra no existe.");
        var lines = await _dbContext.PurchaseOrderLines
            .AsNoTracking()
            .Where(value => value.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToView(order, lines);
    }

    public async Task<IReadOnlyCollection<SalesOrderView>> ListSalesOrdersAsync(
        CancellationToken cancellationToken = default,
        Guid? customerId = null)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var query = _dbContext.SalesOrders.AsNoTracking().AsQueryable();
        if (customerId is not null)
        {
            query = query.Where(value => value.CustomerId == customerId.Value);
        }

        var orders = await query
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        if (orders.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return [];
        }

        var ids = orders.Select(value => value.Id).ToArray();
        var lines = await _dbContext.SalesOrderLines
            .AsNoTracking()
            .Where(value => ids.Contains(value.SalesOrderId))
            .ToListAsync(cancellationToken);
        var linesByOrder = lines
            .GroupBy(value => value.SalesOrderId)
            .ToDictionary(
                group => group.Key,
                group => (IEnumerable<SalesOrderLine>)group.ToArray());
        var result = orders
            .Select(order => ToView(
                order,
                linesByOrder.TryGetValue(order.Id, out var orderLines)
                    ? orderLines
                    : []))
            .ToArray();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyCollection<DeliverySummaryView>> ListDeliveriesAsync(
        Guid? salesOrderId = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var query = _dbContext.Deliveries.AsNoTracking().AsQueryable();
        if (salesOrderId is Guid orderId)
        {
            query = query.Where(value => value.SalesOrderId == orderId);
        }

        var deliveries = await query
            .OrderByDescending(value => value.PostedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var orderIds = deliveries.Select(value => value.SalesOrderId).Distinct().ToArray();
        var orderNumbers = orderIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.SalesOrders.AsNoTracking()
                .Where(value => orderIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, value => value.Number, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return deliveries.Select(delivery => new DeliverySummaryView(
            delivery.Id,
            delivery.Number,
            delivery.SalesOrderId,
            orderNumbers.TryGetValue(delivery.SalesOrderId, out var number) ? number : null,
            delivery.DocumentDate,
            delivery.CustomerId)).ToArray();
    }

    public async Task<IReadOnlyCollection<GoodsReceiptSummaryView>> ListGoodsReceiptsAsync(
        Guid? purchaseOrderId = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var query = _dbContext.GoodsReceipts.AsNoTracking().AsQueryable();
        if (purchaseOrderId is Guid poId)
        {
            query = query.Where(value => value.PurchaseOrderId == poId);
        }

        var receipts = await query
            .OrderByDescending(value => value.PostedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var poIds = receipts.Select(value => value.PurchaseOrderId).Distinct().ToArray();
        var poNumbers = poIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.PurchaseOrders.AsNoTracking()
                .Where(value => poIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, value => value.Number, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipts.Select(receipt => new GoodsReceiptSummaryView(
            receipt.Id,
            receipt.Number,
            receipt.PurchaseOrderId,
            poNumbers.TryGetValue(receipt.PurchaseOrderId, out var number) ? number : null,
            receipt.DocumentDate,
            receipt.SupplierId)).ToArray();
    }

    public async Task<IReadOnlyCollection<DocumentNumberRangeView>> ListDocumentNumberRangesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var rows = await _dbContext.DocumentNumberRanges.AsNoTracking()
            .OrderBy(value => value.DocumentType)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(row => new DocumentNumberRangeView(
            row.DocumentType,
            row.Prefix,
            row.NextValue)).ToArray();
    }

    public async Task<DocumentNumberRangeView> UpsertDocumentNumberRangeAsync(
        string documentType,
        string prefix,
        long nextValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentType) || string.IsNullOrWhiteSpace(prefix))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-NUM-0001",
                "Tipo de documento y prefijo son obligatorios.");
        }

        if (nextValue < 1)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-NUM-0002",
                "El próximo número debe ser ≥ 1.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var type = documentType.Trim();
        var range = await _dbContext.DocumentNumberRanges
            .SingleOrDefaultAsync(value => value.DocumentType == type, cancellationToken);
        if (range is null)
        {
            range = new DocumentNumberRange(type, prefix.Trim());
            _dbContext.DocumentNumberRanges.Add(range);
        }

        try
        {
            range.Reconfigure(prefix.Trim(), nextValue);
        }
        catch (InvalidOperationException exception)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-NUM-0003",
                exception.Message);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DocumentNumberRangeView(range.DocumentType, range.Prefix, range.NextValue);
    }

    public async Task<ConfirmSalesOrderResult> ConfirmSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var order = await _dbContext.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == salesOrderId,
            cancellationToken)
            ?? throw NotFound(
                "AAA-SALES-0005",
                "El pedido de venta no existe.");
        var lines = await _dbContext.SalesOrderLines
            .Where(value => value.SalesOrderId == salesOrderId)
            .ToListAsync(cancellationToken);

        var materialIds = lines.Select(value => value.MaterialId).Distinct().ToArray();
        var materialsForCredit = await _dbContext.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var estimatedAmount = lines.Sum(line =>
        {
            materialsForCredit.TryGetValue(line.MaterialId, out var material);
            var price = material?.CatalogPrice ?? 0m;
            return price * line.RequestedQuantity;
        });
        await _industrial.EnsureCreditAllowsConfirmAsync(
            order.CustomerId,
            _requestContext.CompanyId,
            estimatedAmount,
            cancellationToken);

        // Global ATP advisory per material+warehouse before grain snapshot,
        // so concurrent GR/putaway cannot inflate unlocked locations.
        foreach (var materialId in materialIds.OrderBy(value => value))
        {
            var atpLockKey = string.Create(
                CultureInfo.InvariantCulture,
                $"atp:{materialId:N}:{order.DefaultWarehouseId:N}");
            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({atpLockKey}, 0))",
                cancellationToken);
        }

        // House stock only (OwnerCustomerId == Empty) for standard ATP confirm.
        var availableGrains = await _dbContext.StockBalances
            .Where(value =>
                materialIds.Contains(value.MaterialId)
                && value.WarehouseId == order.DefaultWarehouseId
                && value.Status == StockStatus.Available
                && value.OwnerCustomerId == Guid.Empty)
            .Select(value => new
            {
                value.MaterialId,
                value.WarehouseId,
                value.LocationId,
                value.LotId,
            })
            .ToListAsync(cancellationToken);
        await LockStockAsync(
            availableGrains.Select(value =>
                (value.MaterialId, value.WarehouseId, value.LocationId, value.LotId)),
            cancellationToken);

        var onHandByMaterial = await _dbContext.StockBalances
            .Where(value =>
                materialIds.Contains(value.MaterialId)
                && value.WarehouseId == order.DefaultWarehouseId
                && value.Status == StockStatus.Available
                && value.OwnerCustomerId == Guid.Empty)
            .GroupBy(value => value.MaterialId)
            .Select(group => new
            {
                MaterialId = group.Key,
                Quantity = group.Sum(value => value.Quantity),
            })
            .ToDictionaryAsync(
                value => value.MaterialId,
                value => value.Quantity,
                cancellationToken);

        var reservedByOthers = await _dbContext.StockReservations
            .Where(value =>
                value.IsActive
                && value.WarehouseId == order.DefaultWarehouseId
                && materialIds.Contains(value.MaterialId)
                && value.SalesOrderId != order.Id)
            .GroupBy(value => value.MaterialId)
            .Select(group => new
            {
                MaterialId = group.Key,
                Quantity = group.Sum(
                    value => value.Quantity - value.ConsumedQuantity),
            })
            .ToDictionaryAsync(
                value => value.MaterialId,
                value => value.Quantity,
                cancellationToken);

        var remainingAtp = new Dictionary<Guid, decimal>();
        foreach (var materialId in materialIds)
        {
            onHandByMaterial.TryGetValue(materialId, out var onHand);
            reservedByOthers.TryGetValue(materialId, out var reserved);
            remainingAtp[materialId] = Math.Max(0m, onHand - reserved);
        }

        var warnings = new List<string>();
        var now = _clock.UtcNow;
        foreach (var line in lines)
        {
            var openQty = line.RequestedQuantity;
            remainingAtp.TryGetValue(line.MaterialId, out var atp);
            var reserveQty = Math.Min(openQty, atp);
            if (reserveQty > 0)
            {
                _dbContext.StockReservations.Add(
                    new StockReservation(
                        order.Id,
                        line.Id,
                        line.MaterialId,
                        order.DefaultWarehouseId,
                        reserveQty,
                        now));
                remainingAtp[line.MaterialId] = atp - reserveQty;
            }

            if (reserveQty < openQty)
            {
                var uncovered = openQty - reserveQty;
                warnings.Add(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Material {line.MaterialId:D}: sin cubrir {uncovered}."));
            }
        }

        order.Confirm(lines, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ConfirmSalesOrderResult(
            order.Id,
            order.Status.ToString(),
            warnings);
    }

    public async Task<AtpView> GetAtpAsync(
        Guid materialId,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        if (materialId == Guid.Empty || warehouseId == Guid.Empty)
        {
            throw Validation(
                "AAA-ATP-0001",
                "El material y el almacén son obligatorios.");
        }

        return await ReadWithRlsAsync(
            async ct =>
            {
                var onHand = await _dbContext.StockBalances
                    .AsNoTracking()
                    .Where(value =>
                        value.MaterialId == materialId
                        && value.WarehouseId == warehouseId
                        && value.Status == StockStatus.Available
                        && value.OwnerCustomerId == Guid.Empty)
                    .SumAsync(value => value.Quantity, ct);
                var reserved = await _dbContext.StockReservations
                    .AsNoTracking()
                    .Where(value =>
                        value.IsActive
                        && value.MaterialId == materialId
                        && value.WarehouseId == warehouseId)
                    .SumAsync(
                        value => value.Quantity - value.ConsumedQuantity,
                        ct);
                return new AtpView(
                    materialId,
                    warehouseId,
                    Math.Max(0m, onHand - reserved));
            },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockView>> GetStockAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadWithRlsAsync(
            async ct =>
            {
                var scopedWarehouses = await ResolveScopedWarehouseIdsAsync(ct);
                var balances = _dbContext.StockBalances.AsNoTracking().AsQueryable();
                if (scopedWarehouses is not null)
                {
                    balances = balances.Where(value => scopedWarehouses.Contains(value.WarehouseId));
                }

                IReadOnlyCollection<StockView> rows = await (
                    from balance in balances
                    join material in _dbContext.Materials.AsNoTracking()
                        on balance.MaterialId equals material.Id
                    join warehouse in _dbContext.Warehouses.AsNoTracking()
                        on balance.WarehouseId equals warehouse.Id
                    join location in _dbContext.LocationNodes.AsNoTracking()
                        on balance.LocationId equals location.Id into locations
                    from location in locations.DefaultIfEmpty()
                    orderby material.Code, warehouse.Code, location.Code
                    select new StockView(
                        balance.MaterialId,
                        material.Code,
                        balance.WarehouseId,
                        warehouse.Code,
                        balance.Quantity,
                        balance.MinQuantity,
                        balance.LocationId,
                        location != null ? location.Code : null,
                        balance.LotId,
                        balance.Status.ToString()))
                    .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    private async Task<HashSet<Guid>?> ResolveScopedWarehouseIdsAsync(
        CancellationToken cancellationToken)
    {
        if (_requestContext.UserId is not Guid userId)
        {
            return null;
        }

        var ctx = await _dbContext.UserOrgContexts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        if (ctx is null || (ctx.PlantId is null && ctx.CompanyId is null))
        {
            return null;
        }

        IQueryable<Domain.Studio.PlantWarehouse> query = _dbContext.PlantWarehouses.AsNoTracking();
        if (ctx.PlantId is Guid plantId)
        {
            query = query.Where(value => value.PlantId == plantId);
        }
        else if (ctx.CompanyId is Guid companyId)
        {
            var plantIds = _dbContext.Plants.AsNoTracking()
                .Where(value => value.CompanyId == companyId)
                .Select(value => value.Id);
            query = query.Where(value => plantIds.Contains(value.PlantId));
        }

        var ids = await query.Select(value => value.WarehouseId).Distinct().ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlyCollection<MovementView>> GetMovementsAsync(
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1_000)
        {
            throw Validation(
                "AAA-INVENTORY-0002",
                "El límite debe estar entre 1 y 1000.");
        }

        return await ReadWithRlsAsync(
            async ct =>
            {
                IReadOnlyCollection<MovementView> rows = await _dbContext.StockMovements
            .AsNoTracking()
            .OrderByDescending(value => value.PostedUtc)
            .ThenByDescending(value => value.Id)
            .Take(limit)
            .Select(value => new MovementView(
                value.Id,
                value.Type,
                value.MaterialId,
                value.WarehouseId,
                value.SignedQuantity,
                value.UnitSnapshot,
                value.OriginDocumentType,
                value.OriginDocumentId,
                value.OriginLineId,
                value.CorrelatedOperationId,
                value.ReversedMovementId,
                value.PostedUtc,
                value.Reason))
            .ToListAsync(ct);
                return rows;
            },
            cancellationToken);
    }

    public async Task<ReconciliationResult> ReconcileStockAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var balances = await _dbContext.StockBalances
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var ledger = await _dbContext.StockMovements
            .AsNoTracking()
            .GroupBy(value => new { value.MaterialId, value.WarehouseId })
            .Select(group => new
            {
                group.Key.MaterialId,
                group.Key.WarehouseId,
                Quantity = group.Sum(value => value.SignedQuantity),
            })
            .ToListAsync(cancellationToken);

        var balanceMap = balances
            .GroupBy(value => (value.MaterialId, value.WarehouseId))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.Quantity));
        var ledgerMap = ledger.ToDictionary(
            value => (value.MaterialId, value.WarehouseId),
            value => value.Quantity);
        var allKeys = balanceMap.Keys
            .Union(ledgerMap.Keys)
            .OrderBy(value => value.MaterialId)
            .ThenBy(value => value.WarehouseId);
        var differences = allKeys
            .Select(key =>
            {
                balanceMap.TryGetValue(key, out var balance);
                ledgerMap.TryGetValue(key, out var movementTotal);
                return new ReconciliationItem(
                    key.MaterialId,
                    key.WarehouseId,
                    balance,
                    movementTotal,
                    balance - movementTotal);
            })
            .Where(value => value.Difference != 0)
            .ToArray();
        await transaction.CommitAsync(cancellationToken);
        return new ReconciliationResult(differences.Length == 0, differences);
    }

    public async Task<DemoSeedResult> SeedDemoAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_demoOptions.AllowSeed)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Forbidden,
                "AAA-DEMO-0001",
                "La carga de demostración está desactivada.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);

        // Fail closed if the database already holds non-demo operational data.
        var hasForeignMaterials = await _dbContext.Materials.AnyAsync(
            value => value.Code != "DEMO-BOX" && value.Code != "DEMO-PALLET",
            cancellationToken);
        var hasForeignOrders = await _dbContext.PurchaseOrders.AnyAsync(cancellationToken)
            || await _dbContext.SalesOrders.IgnoreQueryFilters().AnyAsync(cancellationToken);
        if (hasForeignMaterials || hasForeignOrders)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-DEMO-0002",
                "seed-demo se niega: la base ya contiene datos que no son de demostración.");
        }

        var created = false;
        var unit = await _dbContext.UnitsOfMeasure
            .SingleOrDefaultAsync(value => value.Code == "EA", cancellationToken);
        if (unit is null)
        {
            unit = new UnitOfMeasure("EA", "Unidad", 0);
            _dbContext.UnitsOfMeasure.Add(unit);
            created = true;
        }

        var warehouse = await _dbContext.Warehouses
            .SingleOrDefaultAsync(value => value.Code == "MAIN", cancellationToken);
        if (warehouse is null)
        {
            warehouse = new Warehouse("MAIN", "Almacén principal");
            _dbContext.Warehouses.Add(warehouse);
            created = true;
        }

        var materialOne = await _dbContext.Materials
            .SingleOrDefaultAsync(
                value => value.Code == "DEMO-BOX",
                cancellationToken);
        if (materialOne is null)
        {
            materialOne = new Material(
                "DEMO-BOX",
                "Caja de demostración",
                unit.Id,
                12.50m,
                "EUR");
            _dbContext.Materials.Add(materialOne);
            created = true;
        }

        var materialTwo = await _dbContext.Materials
            .SingleOrDefaultAsync(
                value => value.Code == "DEMO-PALLET",
                cancellationToken);
        if (materialTwo is null)
        {
            materialTwo = new Material(
                "DEMO-PALLET",
                "Palé de demostración",
                unit.Id,
                85m,
                "EUR");
            _dbContext.Materials.Add(materialTwo);
            created = true;
        }

        if (!await _dbContext.Suppliers.AnyAsync(
                value => value.TaxId == "DEMO-SUPPLIER",
                cancellationToken))
        {
            _dbContext.Suppliers.Add(
                new Supplier(
                    "Proveedor de demostración",
                    "DEMO-SUPPLIER",
                    "Calle Prueba 1",
                    "Madrid",
                    "28001",
                    "ES"));
            created = true;
        }

        if (!await _dbContext.Customers.IgnoreQueryFilters().AnyAsync(
                value => value.TaxId == "DEMO-CUSTOMER",
                cancellationToken))
        {
            _dbContext.Customers.Add(
                new Customer(
                    "Cliente de demostración",
                    "DEMO-CUSTOMER",
                    "Avenida Ejemplo 2",
                    "Madrid",
                    "28002",
                    "ES"));
            created = true;
        }

        if (created)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        var movements = new List<Guid>();
        foreach (var (material, quantity) in new[]
                 {
                     (materialOne, 100m),
                     (materialTwo, 20m),
                 })
        {
            var result = await PostAdjustmentAsync(
                new PostAdjustmentCommand(
                    $"seed-demo-opening-{material.Id:N}",
                    material.Id,
                    warehouse.Id,
                    quantity,
                    StockMovementType.Opening,
                    "Stock inicial de demostración"),
                cancellationToken);
            movements.AddRange(result.MovementIds);
        }

        return new DemoSeedResult(
            created,
            movements,
            created
                ? "Datos de demostración creados."
                : "Los datos de demostración ya existían; se verificó la apertura.");
    }

    private async Task<Dictionary<Guid, (Material Material, UnitOfMeasure Unit)>>
        LoadMaterialsAsync(
            IEnumerable<Guid> materialIds,
            CancellationToken cancellationToken)
    {
        var ids = materialIds.Distinct().ToArray();
        var values = await (
            from material in _dbContext.Materials
            join unit in _dbContext.UnitsOfMeasure
                on material.UnitId equals unit.Id
            where ids.Contains(material.Id) && material.IsActive
            select new { Material = material, Unit = unit })
            .ToListAsync(cancellationToken);
        if (values.Count != ids.Length)
        {
            throw NotFound(
                "AAA-CATALOG-0004",
                "Uno o más materiales no existen o no están activos.");
        }

        return values.ToDictionary(
            value => value.Material.Id,
            value => (value.Material, value.Unit));
    }

    private static (Material Material, UnitOfMeasure Unit) GetMaterial(
        IReadOnlyDictionary<Guid, (Material Material, UnitOfMeasure Unit)> values,
        Guid materialId) =>
        values.TryGetValue(materialId, out var value)
            ? value
            : throw NotFound(
                "AAA-CATALOG-0005",
                "El material indicado no existe.");

    private static void EnsureQuantityScale(
        decimal quantity,
        UnitOfMeasure unit)
    {
        if (quantity <= 0)
        {
            throw Validation(
                "AAA-INVENTORY-0003",
                "La cantidad debe ser positiva.");
        }

        if (decimal.Round(quantity, unit.DecimalScale) != quantity)
        {
            throw Validation(
                "AAA-INVENTORY-0004",
                $"La cantidad supera la escala permitida por {unit.Code}.");
        }
    }

    private async Task<string> TakeDocumentNumberAsync(
        string documentType,
        string prefix,
        CancellationToken cancellationToken)
    {
        var lockKey = $"document-number:{documentType}";
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        var range = await _dbContext.DocumentNumberRanges
            .SingleOrDefaultAsync(
                value => value.DocumentType == documentType,
                cancellationToken);
        if (range is null)
        {
            range = new DocumentNumberRange(documentType, prefix);
            _dbContext.DocumentNumberRanges.Add(range);
        }

        return range.TakeNext();
    }

    private static UnitOfMeasureView ToView(UnitOfMeasure value) =>
        new(value.Id, value.Code, value.Name, value.DecimalScale);

    private static MaterialView ToView(Material value) =>
        new(
            value.Id,
            value.Code,
            value.DisplayName,
            value.Description,
            value.UnitId,
            value.CatalogPrice,
            value.Currency,
            value.IsActive,
            value.ExternalId,
            value.RequiresLot);

    private static WarehouseView ToView(Warehouse value) =>
        new(value.Id, value.Code, value.Name, value.SiteKind.ToString());

    private static WarehouseSiteKind ParseSiteKind(string? siteKind)
    {
        if (Enum.TryParse<WarehouseSiteKind>(siteKind, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return WarehouseSiteKind.Generic;
    }

    private static PartyView ToView(Supplier value) =>
        new(
            value.Id,
            value.Name,
            value.TaxId,
            value.City,
            value.CountryCode,
            value.IsActive,
            value.ExternalId,
            value.AddressLine1,
            value.AddressLine2,
            value.Region,
            value.PostalCode,
            value.LeadTimeDays);

    private static PartyView ToView(Customer value) =>
        new(
            value.Id,
            value.Name,
            value.TaxId,
            value.City,
            value.CountryCode,
            value.IsActive,
            value.ExternalId,
            value.AddressLine1,
            value.AddressLine2,
            value.Region,
            value.PostalCode);

    private static PurchaseOrderView ToView(
        PurchaseOrder order,
        IEnumerable<PurchaseOrderLine> lines) =>
        new(
            order.Id,
            order.Number,
            order.Status.ToString(),
            lines.Select(value => new OrderLineView(
                value.Id,
                value.MaterialId,
                value.RequestedQuantity,
                value.ConfirmedQuantity,
                value.PostedQuantity,
                value.CancelledQuantity,
                value.OpenQuantity)).ToArray(),
            order.SupplierId);

    private static SalesOrderView ToView(
        SalesOrder order,
        IEnumerable<SalesOrderLine> lines) =>
        new(
            order.Id,
            order.Number,
            order.Status.ToString(),
            lines.Select(value => new OrderLineView(
                value.Id,
                value.MaterialId,
                value.RequestedQuantity,
                value.ConfirmedQuantity,
                value.PostedQuantity,
                value.CancelledQuantity,
                value.OpenQuantity)).ToArray(),
            order.CustomerId,
            order.Incoterms,
            order.CustomsReference);

    private static LogisticsException Validation(
        string code,
        string message) =>
        new(LogisticsErrorKind.Validation, code, message);

    private static LogisticsException NotFound(
        string code,
        string message) =>
        new(LogisticsErrorKind.NotFound, code, message);

    private static LogisticsException Conflict(
        string code,
        string message) =>
        new(LogisticsErrorKind.Conflict, code, message);
}
