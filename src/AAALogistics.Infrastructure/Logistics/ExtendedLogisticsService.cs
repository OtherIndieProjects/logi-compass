using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Branding;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Billing;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Documents;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Operations;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Warehouse;
using AAALogistics.Infrastructure.Documents;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace AAALogistics.Infrastructure.Logistics;

public sealed class ExtendedLogisticsService(
    ApplicationDbContext dbContext,
    IClock clock,
    IRequestContext requestContext,
    ICustomerRlsSession rlsSession,
    IHostEnvironment hostEnvironment,
    IVerifactuSubmitter verifactuSubmitter,
    IIndustrialLogisticsService industrial,
    IAppBrandingStore brandingStore,
    ILogisticsService logistics) : IExtendedLogisticsService
{
    private const int MaxSealPngBytes = 512 * 1024;

    private static readonly JsonSerializerOptions IdempotencyJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly ApplicationDbContext _db = dbContext;
    private readonly IClock _clock = clock;
    private readonly IRequestContext _requestContext = requestContext;
    private readonly ICustomerRlsSession _rlsSession = rlsSession;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IVerifactuSubmitter _verifactuSubmitter = verifactuSubmitter;
    private readonly IIndustrialLogisticsService _industrial = industrial;
    private readonly IAppBrandingStore _brandingStore = brandingStore;
    private readonly ILogisticsService _logistics = logistics;

    private async Task<string?> BrandNameAsync(CancellationToken cancellationToken)
    {
        var brand = await _brandingStore.GetAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(brand.DisplayName) ? null : brand.DisplayName;
    }

    private async Task<byte[]?> BrandLogoPngAsync(CancellationToken cancellationToken)
    {
        var logo = await _brandingStore.TryReadLogoAsync(cancellationToken);
        return logo is { ContentType: "image/png" } ? logo.Value.Bytes : null;
    }

    public async Task<LocationNodeView> CreateLocationAsync(
        CreateLocationNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await _db.Warehouses.SingleOrDefaultAsync(
            value => value.Id == command.WarehouseId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-WH-0001",
                "El almacén no existe.");
        _ = warehouse;
        if (command.ParentId is { } parentId)
        {
            var parent = await _db.LocationNodes.SingleOrDefaultAsync(
                value => value.Id == parentId && value.WarehouseId == command.WarehouseId,
                cancellationToken)
                ?? throw new LogisticsException(
                    LogisticsErrorKind.NotFound,
                    "AAA-WH-0003",
                    "La ubicación padre no existe en este almacén.");
            _ = parent;
        }

        var node = new LocationNode(
            command.WarehouseId,
            command.Code,
            command.Name,
            command.Kind,
            command.ParentId);
        _db.LocationNodes.Add(node);
        await _db.SaveChangesAsync(cancellationToken);
        return ToLocation(node);
    }

    public async Task<LocationNodeView> UpdateLocationAsync(
        UpdateLocationNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        var node = await _db.LocationNodes.SingleOrDefaultAsync(
            value => value.Id == command.LocationId
                && value.WarehouseId == command.WarehouseId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-WH-0004",
                "La ubicación no existe en este almacén.");

        if ((string.Equals(node.Code, "DEFAULT", StringComparison.OrdinalIgnoreCase)
                || node.Kind == LocationNodeKind.Root)
            && !command.IsActive)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-WH-0005",
                "La raíz del almacén no se puede desactivar.");
        }

        node.Rename(command.Name);
        if (command.IsActive)
        {
            node.Activate();
        }
        else
        {
            node.Deactivate();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToLocation(node);
    }

    public async Task DeleteLocationAsync(
        Guid warehouseId,
        Guid locationId,
        CancellationToken cancellationToken = default)
    {
        var node = await _db.LocationNodes.SingleOrDefaultAsync(
            value => value.Id == locationId && value.WarehouseId == warehouseId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-WH-0004",
                "La ubicación no existe en este almacén.");

        // Raíz ≡ almacén: borrar la raíz elimina el almacén completo (si está vacío).
        if (string.Equals(node.Code, "DEFAULT", StringComparison.OrdinalIgnoreCase)
            || node.Kind == LocationNodeKind.Root)
        {
            await _logistics.DeleteWarehouseAsync(warehouseId, cancellationToken);
            return;
        }

        if (await _db.LocationNodes.AnyAsync(
                value => value.ParentId == locationId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-WH-0006",
                "No se puede eliminar: tiene ubicaciones hijas. Elimine las hijas primero o borre el almacén.");
        }

        if (await _db.StockBalances.AnyAsync(
                value => value.LocationId == locationId && value.Quantity > 0,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-WH-0007",
                "No se puede eliminar: hay stock. Use Desactivar.");
        }

        if (await _db.StockMovements.AnyAsync(
                value => value.LocationId == locationId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-WH-0008",
                "No se puede eliminar: hay movimientos históricos. Use Desactivar.");
        }

        if (await _db.PrepPickLines.AnyAsync(
                value => value.SuggestedLocationId == locationId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-WH-0009",
                "No se puede eliminar: está referenciada en picking. Use Desactivar.");
        }

        var emptyBalances = await _db.StockBalances
            .Where(value => value.LocationId == locationId && value.Quantity == 0)
            .ToListAsync(cancellationToken);
        if (emptyBalances.Count > 0)
        {
            _db.StockBalances.RemoveRange(emptyBalances);
        }

        _db.LocationNodes.Remove(node);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LocationNodeView>> ListLocationsAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        var scopedWarehouses = await ResolveScopedWarehouseIdsAsync(cancellationToken);
        if (scopedWarehouses is not null && !scopedWarehouses.Contains(warehouseId))
        {
            return [];
        }

        await MigrateDefaultRootAsync(warehouseId, cancellationToken);

        var rows = await _db.LocationNodes.AsNoTracking()
            .Where(value => value.WarehouseId == warehouseId)
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToLocation).ToArray();
    }

    private async Task MigrateDefaultRootAsync(
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var root = await _db.LocationNodes
            .FirstOrDefaultAsync(
                value =>
                    value.WarehouseId == warehouseId
                    && (value.Kind == LocationNodeKind.Root
                        || value.Code == "DEFAULT"),
                cancellationToken);
        if (root is null)
        {
            return;
        }

        var warehouse = await _db.Warehouses
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == warehouseId, cancellationToken);
        if (warehouse is null)
        {
            return;
        }

        var changed = false;
        if (string.Equals(root.Code, "DEFAULT", StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await _db.LocationNodes.AnyAsync(
                value =>
                    value.WarehouseId == warehouseId
                    && value.Id != root.Id
                    && value.Code == warehouse.Code,
                cancellationToken);
            var target = codeTaken
                ? TruncateCode($"R-{warehouse.Code}", 40)
                : warehouse.Code;
            root.Recode(target);
            changed = true;
        }

        if (string.Equals(root.Name, "Ubicación por defecto", StringComparison.OrdinalIgnoreCase))
        {
            root.Rename("Raíz");
            changed = true;
        }

        if (changed)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string TruncateCode(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private async Task<HashSet<Guid>?> ResolveScopedWarehouseIdsAsync(
        CancellationToken cancellationToken)
    {
        if (_requestContext.UserId is not Guid userId)
        {
            return null;
        }

        var ctx = await _db.UserOrgContexts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        if (ctx is null || (ctx.PlantId is null && ctx.CompanyId is null))
        {
            return null;
        }

        IQueryable<Domain.Studio.PlantWarehouse> query = _db.PlantWarehouses.AsNoTracking();
        if (ctx.PlantId is Guid plantId)
        {
            query = query.Where(value => value.PlantId == plantId);
        }
        else if (ctx.CompanyId is Guid companyId)
        {
            var plantIds = _db.Plants.AsNoTracking()
                .Where(value => value.CompanyId == companyId)
                .Select(value => value.Id);
            query = query.Where(value => plantIds.Contains(value.PlantId));
        }

        var ids = await query.Select(value => value.WarehouseId).Distinct().ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task<IReadOnlyCollection<OrderPrepSessionView>> JoinPrepAsync(
        JoinPrepCommand command,
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await _rlsSession.ApplyAsync(cancellationToken);

        var lockKey = $"prep:{command.SalesOrderId:N}";
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);

        var order = await _db.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == command.SalesOrderId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-SALES-0005",
                "El pedido de venta no existe.");

        var active = await _db.OrderPrepSessions
            .Where(value => value.SalesOrderId == command.SalesOrderId && value.IsActive)
            .ToListAsync(cancellationToken);

        if (command.Mode == PrepLockMode.Exclusive
            && active.Any(value => value.UserId != userId))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-PREP-0001",
                "El pedido está en preparación exclusiva por otro usuario.");
        }

        if (active.Any(value => value.Mode == PrepLockMode.Exclusive
                && value.UserId != userId))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-PREP-0002",
                "Hay un bloqueo exclusivo activo.");
        }

        if (!active.Any(value => value.UserId == userId))
        {
            _db.OrderPrepSessions.Add(
                new OrderPrepSession(
                    command.SalesOrderId,
                    userId,
                    userName,
                    command.Mode,
                    _clock.UtcNow));
        }

        order.StartPreparation();
        await RegeneratePrepPicksAsync(order, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return await ListActivePrepAsync(command.SalesOrderId, cancellationToken);
    }

    public async Task LeavePrepAsync(
        Guid salesOrderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _db.OrderPrepSessions
            .Where(value =>
                value.SalesOrderId == salesOrderId
                && value.UserId == userId
                && value.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Leave(_clock.UtcNow);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPreparedAsync(
        Guid salesOrderId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);

        var order = await _db.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == salesOrderId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-SALES-0005",
                "El pedido de venta no existe.");

        var sessions = await _db.OrderPrepSessions
            .Where(value => value.SalesOrderId == salesOrderId && value.IsActive)
            .ToListAsync(cancellationToken);
        if (!sessions.Any(value => value.UserId == userId))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-PREP-0003",
                "Debe unirse a la preparación del pedido antes de marcarlo preparado.");
        }

        var picks = await _db.PrepPickLines
            .Where(value => value.SalesOrderId == salesOrderId)
            .ToListAsync(cancellationToken);
        if (picks.Count == 0
            || picks.Any(value => value.ConfirmedUtc is null || value.ConfirmedQuantity <= 0))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-PREP-0006",
                "Debe confirmar todas las líneas de picking antes de marcar preparado.");
        }

        var openLines = (await _db.SalesOrderLines
                .Where(value => value.SalesOrderId == salesOrderId)
                .ToListAsync(cancellationToken))
            .Where(value => value.OpenQuantity > 0)
            .ToArray();
        foreach (var line in openLines)
        {
            var covered = picks
                .Where(value => value.SalesOrderLineId == line.Id)
                .Sum(value => value.RemainingConfirmed);
            if (covered < line.OpenQuantity)
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Conflict,
                    "AAA-PREP-0007",
                    $"Picking confirmado insuficiente para la línea abierta {line.Id:D}.");
            }
        }

        order.MarkPrepared();
        foreach (var session in sessions)
        {
            session.Leave(_clock.UtcNow);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrderPrepSessionView>> ListActivePrepAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.OrderPrepSessions.AsNoTracking()
            .Where(value => value.SalesOrderId == salesOrderId && value.IsActive)
            .OrderBy(value => value.JoinedUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(value => new OrderPrepSessionView(
            value.Id,
            value.SalesOrderId,
            value.UserId,
            value.UserName,
            value.Mode.ToString(),
            value.JoinedUtc)).ToArray();
    }

    public async Task<IReadOnlyCollection<PrepPickLineView>> ListPrepPicksAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.PrepPickLines.AsNoTracking()
            .Where(value => value.SalesOrderId == salesOrderId)
            .OrderBy(value => value.SalesOrderLineId)
            .ThenBy(value => value.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(ToPrepPick).ToArray();
    }

    public async Task<PrepPickLineView> ConfirmPrepPickAsync(
        ConfirmPrepPickCommand command,
        CancellationToken cancellationToken = default)
    {
        var hasSession = await _db.OrderPrepSessions.AnyAsync(
            value =>
                value.SalesOrderId == command.SalesOrderId
                && value.UserId == command.UserId
                && value.IsActive,
            cancellationToken);
        if (!hasSession)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-PREP-0003",
                "Debe unirse a la preparación antes de confirmar picking.");
        }

        var pick = await _db.PrepPickLines.SingleOrDefaultAsync(
            value =>
                value.Id == command.PrepPickLineId
                && value.SalesOrderId == command.SalesOrderId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-PREP-0004",
                "La línea de picking no existe.");
        try
        {
            pick.Confirm(command.ConfirmedQuantity, _clock.UtcNow);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-PREP-0005",
                exception.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToPrepPick(pick);
    }

    public async Task<CarrierView> CreateCarrierAsync(
        CreateCarrierCommand command,
        CancellationToken cancellationToken = default)
    {
        var carrier = new Carrier(command.Code, command.Name, command.TaxId, command.Phone);
        _db.Carriers.Add(carrier);
        await _db.SaveChangesAsync(cancellationToken);
        return ToCarrier(carrier);
    }

    public async Task<IReadOnlyCollection<CarrierView>> ListCarriersAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Carriers.AsNoTracking()
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToCarrier).ToArray();
    }

    public async Task<CmrView> CreateCmrAsync(
        CreateCmrCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        _ = await _db.Carriers.SingleOrDefaultAsync(
            value => value.Id == command.CarrierId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-CMR-0001",
                "El transportista no existe.");
        var number = await NextDocumentAsync("CMR", "CMR", cancellationToken);
        var packages = command.Packages;
        var huQuery = _db.HandlingUnits.AsQueryable();
        if (command.DeliveryId is Guid deliveryId)
        {
            huQuery = huQuery.Where(value => value.DeliveryId == deliveryId);
        }
        else
        {
            huQuery = huQuery.Where(value => value.SalesOrderId == command.SalesOrderId);
        }

        var huPackages = await huQuery.SumAsync(
            value => (int?)value.PackageCount,
            cancellationToken) ?? 0;
        if (huPackages > 0)
        {
            packages = huPackages;
        }

        var cmr = new CmrDocument(
            number,
            command.SalesOrderId,
            command.DeliveryId,
            command.CarrierId,
            command.VehiclePlate,
            packages,
            command.Remarks,
            _clock.UtcNow);
        _db.CmrDocuments.Add(cmr);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CmrView(
            cmr.Id,
            cmr.Number,
            cmr.SalesOrderId,
            cmr.CarrierId,
            cmr.VehiclePlate,
            cmr.Packages,
            cmr.CreatedUtc);
    }

    public async Task<byte[]> RenderCmrPdfAsync(
        Guid cmrId,
        CancellationToken cancellationToken = default)
    {
        var cmr = await _db.CmrDocuments.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == cmrId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-CMR-0002",
                "CMR no encontrado.");
        var carrier = await _db.Carriers.AsNoTracking()
            .SingleAsync(value => value.Id == cmr.CarrierId, cancellationToken);
        var issuer = await _db.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        byte[]? sealPng = null;
        var sealNote = "Documento para sello PNG del transportista/emisor.";
        if (!string.IsNullOrWhiteSpace(issuer?.SealPngRelativePath))
        {
            var relative = issuer.SealPngRelativePath.Replace('\\', '/').TrimStart('/');
            var contentRoot = Path.GetFullPath(_hostEnvironment.ContentRootPath);
            var fullPath = Path.GetFullPath(Path.Combine(contentRoot, relative));
            if (fullPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase)
                && File.Exists(fullPath))
            {
                sealPng = await File.ReadAllBytesAsync(fullPath, cancellationToken);
                sealNote = $"Sello PNG emisor: {relative}";
            }
        }

        var salesOrderNumber = await _db.SalesOrders.AsNoTracking()
            .Where(value => value.Id == cmr.SalesOrderId)
            .Select(value => value.Number)
            .FirstOrDefaultAsync(cancellationToken) ?? "—";
        var model = CommercialPdf.Create(
            issuer,
            "CMR",
            cmr.Number,
            [
                $"Pedido: {salesOrderNumber}",
                $"Fecha: {DateTimeOffset.UtcNow:yyyy-MM-dd}",
            ],
            "Transportista",
            [
                carrier.Name,
                $"Matrícula {cmr.VehiclePlate}",
                $"Bultos {cmr.Packages}",
                string.IsNullOrWhiteSpace(cmr.Remarks) ? "Sin observaciones." : cmr.Remarks,
            ],
            [
                new CommercialLineRow(
                    "Transporte por carretera (CMR)",
                    cmr.Packages.ToString(CultureInfo.InvariantCulture),
                    "—",
                    "—",
                    "—"),
            ],
            [$"Bultos: {cmr.Packages}"],
            [sealNote, "Documento de transporte; no es factura."],
            includePayment: false,
            brandDisplayName: await BrandNameAsync(cancellationToken));
        return CommercialPdf.Render(model, sealPng ?? await BrandLogoPngAsync(cancellationToken));
    }

    public async Task<HandlingUnitView> CreateHandlingUnitAsync(
        CreateHandlingUnitCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var order = await _db.SalesOrders.SingleOrDefaultAsync(
            value => value.Id == command.SalesOrderId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-HU-0001",
                "El pedido de venta no existe.");
        _ = order;

        if (command.DeliveryId is Guid deliveryId)
        {
            var deliveryOk = await _db.Deliveries.AnyAsync(
                value =>
                    value.Id == deliveryId
                    && value.SalesOrderId == command.SalesOrderId,
                cancellationToken);
            if (!deliveryOk)
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Validation,
                    "AAA-HU-0002",
                    "La entrega no pertenece al pedido.");
            }
        }

        var sscc = string.IsNullOrWhiteSpace(command.Sscc)
            ? GenerateDidacticSscc()
            : command.Sscc.Trim();
        var unit = new HandlingUnit(
            sscc,
            command.SalesOrderId,
            command.DeliveryId,
            command.PackageCount,
            _clock.UtcNow);
        _db.HandlingUnits.Add(unit);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-HU-0003",
                "El SSCC ya existe.");
        }

        return ToHandlingUnit(unit);
    }

    public async Task<IReadOnlyCollection<HandlingUnitView>> ListHandlingUnitsByOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.HandlingUnits.AsNoTracking()
            .Where(value => value.SalesOrderId == salesOrderId)
            .OrderBy(value => value.CreatedUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(ToHandlingUnit).ToArray();
    }

    public async Task<byte[]> RenderPackingListPdfAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        var delivery = await _db.Deliveries.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == deliveryId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-HU-0004",
                "Entrega no encontrada.");
        var order = await _db.SalesOrders.AsNoTracking()
            .SingleAsync(value => value.Id == delivery.SalesOrderId, cancellationToken);
        var units = await _db.HandlingUnits.AsNoTracking()
            .Where(value =>
                value.DeliveryId == deliveryId
                || (value.DeliveryId == null
                    && value.SalesOrderId == delivery.SalesOrderId))
            .OrderBy(value => value.Sscc)
            .ToListAsync(cancellationToken);
        var lines = await _db.DeliveryLines.AsNoTracking()
            .Where(value => value.DeliveryId == deliveryId)
            .ToListAsync(cancellationToken);
        var issuer = await _db.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        var materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();
        var materials = await _db.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        var commercialLines = new List<CommercialLineRow>();
        if (units.Count == 0)
        {
            commercialLines.Add(new CommercialLineRow("(sin handling units)", "—", "—", "—", "—"));
        }
        else
        {
            commercialLines.AddRange(units.Select(unit =>
                new CommercialLineRow(
                    $"SSCC {unit.Sscc}",
                    unit.PackageCount.ToString(CultureInfo.InvariantCulture),
                    "bultos",
                    "—",
                    "—")));
        }

        foreach (var line in lines.Take(20))
        {
            var label = materials.TryGetValue(line.MaterialId, out var material)
                ? $"{material.Code} — {material.DisplayName}"
                : "Material (sin maestro)";
            commercialLines.Add(
                new CommercialLineRow(
                    label,
                    line.Quantity.ToString("0.####", CultureInfo.InvariantCulture),
                    line.UnitSnapshot,
                    "—",
                    "—"));
        }

        var customer = await _db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == delivery.CustomerId, cancellationToken);
        var customerLabel = customer is null
            ? "Cliente —"
            : $"{customer.Name}  NIF {customer.TaxId}";
        var model = CommercialPdf.Create(
            issuer,
            "PACKING LIST",
            delivery.Number,
            [
                $"Pedido: {order.Number}",
                $"Fecha: {delivery.DocumentDate:yyyy-MM-dd}",
                $"Incoterms: {order.Incoterms ?? "—"}",
                $"Aduana: {order.CustomsReference ?? "—"}",
            ],
            "Entrega",
            [delivery.DeliveryAddressSnapshot, customerLabel],
            commercialLines,
            [$"SSCC: {units.Count}", $"Líneas entrega: {lines.Count}"],
            ["Lista de empaque; no es factura."],
            includePayment: false,
            brandDisplayName: await BrandNameAsync(cancellationToken));
        return CommercialPdf.Render(model, await BrandLogoPngAsync(cancellationToken));
    }

    public async Task<InvoiceView> IssueInvoiceAsync(
        IssueInvoiceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var lines = command.Lines.ToArray();
        if (lines.Length == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-INV-0002",
                "La factura requiere líneas.");
        }

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotency = await BeginIdempotencyAsync<InvoiceView>(
            nameof(IssueInvoiceAsync),
            command.IdempotencyKey,
            command,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var customer = await _db.Customers.SingleOrDefaultAsync(
            value => value.Id == command.CustomerId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-INV-0001",
                "Cliente no encontrado.");

        var series = await LockInvoiceSeriesAsync(
            command.SeriesCode,
            cancellationToken);

        var sourceCurrency = "EUR";
        if (command.SalesOrderId is Guid salesOrderId)
        {
            var salesOrder = await _db.SalesOrders.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == salesOrderId, cancellationToken);
            if (salesOrder is not null && !string.IsNullOrWhiteSpace(salesOrder.Currency))
            {
                sourceCurrency = salesOrder.Currency;
            }
        }

        var invoiceCurrency = "EUR";
        if (_requestContext.CompanyId is Guid companyId)
        {
            var company = await _db.Companies.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == companyId, cancellationToken);
            if (company is not null && !string.IsNullOrWhiteSpace(company.Currency))
            {
                invoiceCurrency = company.Currency;
            }
        }

        var fxRate = 1m;
        if (!string.Equals(sourceCurrency, invoiceCurrency, StringComparison.OrdinalIgnoreCase))
        {
            var fx = await _industrial.ConvertFxAsync(
                sourceCurrency,
                invoiceCurrency,
                1m,
                DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
                cancellationToken);
            fxRate = fx.Rate;
        }

        var convertedLines = lines
            .Select(line =>
            {
                var unitPrice = decimal.Round(line.UnitPrice * fxRate, 6);
                var lineNet = decimal.Round(line.Quantity * unitPrice, 6);
                return (line.Description, line.Quantity, UnitPrice: unitPrice, LineNet: lineNet);
            })
            .ToArray();
        var net = convertedLines.Sum(line => line.LineNet);
        var tax = decimal.Round(net * command.TaxRate / 100m, 6);
        var total = net + tax;
        var number = series.AllocateNext();
        var previous = await LatestSifHashInSeriesAsync(series.Code, cancellationToken);
        var sif = ComputeSifHash(
            series.Code,
            number,
            customer.TaxId,
            total,
            previous);
        var invoice = new Invoice(
            series.Code,
            number,
            InvoiceKind.Ordinary,
            customer.Id,
            command.SalesOrderId,
            null,
            customer.TaxId,
            customer.Name,
            $"{customer.AddressLine1}, {customer.PostalCode} {customer.City}",
            invoiceCurrency,
            net,
            command.TaxRate,
            tax,
            total,
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
            _clock.UtcNow,
            sif,
            previous);
        _db.Invoices.Add(invoice);
        var issuedLines = new List<InvoiceLineView>(convertedLines.Length);
        foreach (var line in convertedLines)
        {
            var lineTax = decimal.Round(line.LineNet * command.TaxRate / 100m, 6);
            _db.InvoiceLines.Add(
                new InvoiceLine(
                    invoice.Id,
                    line.Description,
                    line.Quantity,
                    line.UnitPrice,
                    command.TaxRate,
                    line.LineNet,
                    lineTax));
            issuedLines.Add(new InvoiceLineView(line.Description, line.Quantity, line.UnitPrice));
        }

        var view = ToInvoice(invoice, issuedLines);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(view, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _verifactuSubmitter.SubmitAsync(invoice.Id, cancellationToken);
        return view;
    }

    public async Task<InvoiceView> RectifyInvoiceAsync(
        Guid invoiceId,
        string reason,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-INV-0005",
                "El motivo de rectificación es obligatorio.");
        }

        var normalizedReason = reason.Trim();
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var idempotencyPayload = new { invoiceId, reason = normalizedReason };
        var idempotency = await BeginIdempotencyAsync<InvoiceView>(
            nameof(RectifyInvoiceAsync),
            idempotencyKey,
            idempotencyPayload,
            cancellationToken);
        if (idempotency.Result is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return idempotency.Result;
        }

        var original = await _db.Invoices.SingleOrDefaultAsync(
            value => value.Id == invoiceId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-INV-0003",
                "Factura no encontrada.");
        if (original.Status == InvoiceStatus.Rectified)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-INV-0004",
                "La factura ya está rectificada.");
        }

        try
        {
            original.MarkRectified();
        }
        catch (InvalidOperationException exception)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-INV-0004",
                exception.Message);
        }

        var seriesCode = "R" + original.SeriesCode;
        var series = await LockInvoiceSeriesAsync(seriesCode, cancellationToken);
        var originalLines = await _db.InvoiceLines
            .Where(value => value.InvoiceId == original.Id)
            .OrderBy(value => value.Id)
            .ToListAsync(cancellationToken);

        var number = series.AllocateNext();
        var previous = await LatestSifHashInSeriesAsync(series.Code, cancellationToken);
        var sif = ComputeSifHash(
            series.Code,
            number,
            original.CustomerTaxId,
            -original.TotalAmount,
            previous);
        var rect = new Invoice(
            series.Code,
            number,
            InvoiceKind.Rectifying,
            original.CustomerId,
            original.SalesOrderId,
            original.Id,
            original.CustomerTaxId,
            original.CustomerName,
            original.CustomerAddress,
            original.Currency,
            -original.NetAmount,
            original.TaxRate,
            -original.TaxAmount,
            -original.TotalAmount,
            DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime),
            original.OperationDate,
            _clock.UtcNow,
            sif,
            previous);
        _db.Invoices.Add(rect);

        if (originalLines.Count == 0)
        {
            _db.InvoiceLines.Add(
                new InvoiceLine(
                    rect.Id,
                    $"Rectificativa: {normalizedReason}",
                    -1m,
                    original.NetAmount,
                    original.TaxRate,
                    -original.NetAmount,
                    -original.TaxAmount));
        }
        else
        {
            _db.InvoiceLines.Add(
                new InvoiceLine(
                    rect.Id,
                    $"Motivo rectificación: {normalizedReason}",
                    0m,
                    0m,
                    original.TaxRate,
                    0m,
                    0m));
            foreach (var line in originalLines)
            {
                _db.InvoiceLines.Add(
                    new InvoiceLine(
                        rect.Id,
                        line.Description,
                        -line.Quantity,
                        line.UnitPrice,
                        line.TaxRate,
                        -line.NetAmount,
                        -line.TaxAmount));
            }
        }

        IReadOnlyCollection<InvoiceLineView> rectLines = originalLines.Count == 0
            ? [new InvoiceLineView($"Rectificativa: {normalizedReason}", -1m, original.NetAmount)]
            : originalLines
                .Select(line => new InvoiceLineView(line.Description, -line.Quantity, line.UnitPrice))
                .ToArray();
        var view = ToInvoice(rect, rectLines);
        idempotency.Record.Complete(
            JsonSerializer.Serialize(view, IdempotencyJsonOptions),
            _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return view;
    }

    public async Task<IReadOnlyCollection<InvoiceView>> ListInvoicesAsync(
        CancellationToken cancellationToken = default,
        Guid? customerId = null)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var query = _db.Invoices.AsNoTracking().AsQueryable();
        if (customerId is not null)
        {
            query = query.Where(value => value.CustomerId == customerId.Value);
        }

        var rows = await query
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var invoiceIds = rows.Select(value => value.Id).ToArray();
        var lineRows = invoiceIds.Length == 0
            ? []
            : await _db.InvoiceLines.AsNoTracking()
                .Where(value => invoiceIds.Contains(value.InvoiceId))
                .OrderBy(value => value.Description)
                .ToListAsync(cancellationToken);
        var linesByInvoice = lineRows
            .GroupBy(value => value.InvoiceId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<InvoiceLineView>)group
                    .Select(line => new InvoiceLineView(
                        line.Description,
                        line.Quantity,
                        line.UnitPrice))
                    .ToArray());
        await transaction.CommitAsync(cancellationToken);
        return rows.Select(invoice => ToInvoice(
                invoice,
                linesByInvoice.TryGetValue(invoice.Id, out var lines) ? lines : []))
            .ToArray();
    }

    public async Task<byte[]> RenderInvoicePdfAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var invoice = await _db.Invoices.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == invoiceId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-INV-0003",
                "Factura no encontrada.");
        var lines = await _db.InvoiceLines.AsNoTracking()
            .Where(value => value.InvoiceId == invoiceId)
            .OrderBy(value => value.Description)
            .ToListAsync(cancellationToken);
        var issuer = await _db.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        string? salesOrderNumber = null;
        if (invoice.SalesOrderId is Guid linkedSalesOrderId)
        {
            salesOrderNumber = await _db.SalesOrders.AsNoTracking()
                .Where(value => value.Id == linkedSalesOrderId)
                .Select(value => value.Number)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var qrPayload = $"VERIFACTU:{invoice.SifHash}";
        var contentRoot = Path.GetFullPath(_hostEnvironment.ContentRootPath);
        var signaturePath = issuer?.SignaturePngRelativePath ?? "seals/issuer-signature.png";
        var footerImage = await TryLoadIssuerPngAsync(
            signaturePath,
            contentRoot,
            cancellationToken);
        var imageNote = footerImage is not null
            ? $"Firma/sello PNG: {signaturePath}"
            : null;
        if (footerImage is null)
        {
            footerImage = await TryLoadIssuerPngAsync(
                issuer?.SealPngRelativePath,
                contentRoot,
                cancellationToken);
            if (footerImage is not null)
            {
                imageNote = $"Sello PNG: {issuer!.SealPngRelativePath}";
            }
        }

        if (footerImage is null)
        {
            footerImage = await TryLoadIssuerPngAsync(
                issuer?.LogoPngRelativePath,
                contentRoot,
                cancellationToken);
            if (footerImage is not null)
            {
                imageNote = $"Logo PNG: {issuer!.LogoPngRelativePath}";
            }
        }

        imageNote ??= "[Espacio para sello / firma del emisor]";

        var irpfPercent = issuer?.IrpfPercent ?? 0m;
        var irpfAmount = irpfPercent <= 0
            ? 0m
            : Math.Round(
                invoice.NetAmount * irpfPercent / 100m,
                2,
                MidpointRounding.AwayFromZero);
        var totalDue = invoice.TotalAmount - irpfAmount;
        var dueDate = issuer is { PaymentDueDays: > 0 }
            ? invoice.IssueDate.AddDays(issuer.PaymentDueDays)
            : (DateOnly?)null;

        var meta = new List<string>
        {
            $"Tipo: {invoice.Kind}",
            $"Fecha expedición: {invoice.IssueDate:yyyy-MM-dd}",
        };
        if (invoice.OperationDate != invoice.IssueDate)
        {
            meta.Add($"Fecha operación: {invoice.OperationDate:yyyy-MM-dd}");
        }

        if (dueDate is { } vencimiento)
        {
            meta.Add($"Vencimiento: {vencimiento:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(salesOrderNumber))
        {
            meta.Add($"Pedido venta: {salesOrderNumber}");
        }

        var commercialLines = lines.Select(line =>
            new CommercialLineRow(
                line.Description,
                line.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
                line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                line.TaxRate.ToString("0.##", CultureInfo.InvariantCulture),
                line.NetAmount.ToString("0.00", CultureInfo.InvariantCulture))).ToArray();
        if (commercialLines.Length == 0)
        {
            commercialLines =
            [
                new CommercialLineRow("(Sin líneas detalladas)", "—", "—", "—", "—"),
            ];
        }

        var totals = new List<string>
        {
            $"Base imponible {invoice.NetAmount.ToString("0.00", CultureInfo.InvariantCulture)} {invoice.Currency}",
            $"IVA {invoice.TaxRate.ToString("0.##", CultureInfo.InvariantCulture)}%  {invoice.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture)} {invoice.Currency}",
        };
        if (irpfPercent > 0)
        {
            totals.Add(
                $"IRPF {irpfPercent.ToString("0.##", CultureInfo.InvariantCulture)}%  -{irpfAmount.ToString("0.00", CultureInfo.InvariantCulture)} {invoice.Currency}");
        }

        totals.Add(
            $"TOTAL {(irpfPercent > 0 ? "A PAGAR " : string.Empty)}" +
            $"{totalDue.ToString("0.00", CultureInfo.InvariantCulture)} {invoice.Currency}");

        var model = CommercialPdf.Create(
            issuer,
            "FACTURA",
            invoice.DocumentNumber,
            meta,
            "Facturar a",
            [$"{invoice.CustomerName}  CIF/NIF {invoice.CustomerTaxId}", invoice.CustomerAddress],
            commercialLines,
            totals,
            [
                issuer?.LegalFooterText ?? IssuerFiscalProfile.DefaultLegalFooter,
                $"SIF {invoice.SifHash} · Prev {invoice.PreviousSifHash ?? "—"}",
                $"Verifactu {issuer?.VerifactuMode ?? "LocalChain"} · QR escaneable",
                imageNote,
            ],
            qrPayload: qrPayload,
            brandDisplayName: await BrandNameAsync(cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return CommercialPdf.Render(model, footerImage ?? await BrandLogoPngAsync(cancellationToken));
    }

    public async Task<IssuerFiscalProfile> UpsertIssuerAsync(
        UpsertIssuerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.LegalName))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-FAC-0012",
                "La razón social / nombre de empresa es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(command.TaxId))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-FAC-0013",
                "El CIF/NIF del emisor es obligatorio.");
        }

        var previous = await _db.IssuerFiscalProfiles
            .AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);

        var existing = new IssuerFiscalProfile(
            command.LegalName,
            command.TaxId,
            command.AddressLine,
            command.City,
            command.PostalCode,
            command.CountryCode);
        existing.ApplyCompanyDetails(
            command.LegalName,
            command.TaxId,
            command.AddressLine,
            command.City,
            command.PostalCode,
            command.CountryCode,
            command.Phone,
            command.Email,
            command.Website,
            command.SignerName,
            command.SignerTitle,
            command.Iban,
            command.BankName,
            command.PaymentMethod,
            command.PaymentDueDays,
            command.LegalFooterText,
            command.IrpfPercent);

        var sealPath = await PersistIssuerPngAsync(
            command.SealPngRelativePath,
            command.SealPngBytes,
            defaultRelative: "seals/issuer-seal.png",
            previous?.SealPngRelativePath,
            cancellationToken);
        var signaturePath = await PersistIssuerPngAsync(
            command.SignaturePngRelativePath,
            command.SignaturePngBytes,
            defaultRelative: "seals/issuer-signature.png",
            previous?.SignaturePngRelativePath,
            cancellationToken);
        var logoPath = await PersistIssuerPngAsync(
            command.LogoPngRelativePath,
            command.LogoPngBytes,
            defaultRelative: "seals/issuer-logo.png",
            previous?.LogoPngRelativePath,
            cancellationToken);

        existing.SetSealPath(sealPath);
        existing.SetSignaturePath(signaturePath);
        existing.SetLogoPath(logoPath);
        if (!string.IsNullOrWhiteSpace(previous?.VerifactuMode))
        {
            existing.SetVerifactuMode(previous.VerifactuMode);
        }

        var all = await _db.IssuerFiscalProfiles.ToListAsync(cancellationToken);
        if (all.Count > 0)
        {
            _db.IssuerFiscalProfiles.RemoveRange(all);
        }

        _db.IssuerFiscalProfiles.Add(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    private async Task<string?> PersistIssuerPngAsync(
        string? relativePath,
        byte[]? pngBytes,
        string defaultRelative,
        string? previousPath,
        CancellationToken cancellationToken)
    {
        var hasPath = !string.IsNullOrWhiteSpace(relativePath);
        var hasBytes = pngBytes is { Length: > 0 };
        if (!hasPath && !hasBytes)
        {
            return previousPath;
        }

        var relative = (hasPath ? relativePath! : defaultRelative)
            .Replace('\\', '/')
            .TrimStart('/');
        if (relative.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            || !relative.StartsWith("seals/", StringComparison.OrdinalIgnoreCase)
            || relative.IndexOf('/', "seals/".Length) >= 0
            || !relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-FAC-0010",
                "Ruta de imagen PNG inválida: use seals/<nombre>.png.");
        }

        if (!hasBytes)
        {
            return relative;
        }

        if (pngBytes!.Length > MaxSealPngBytes
            || !pngBytes.AsSpan().StartsWith(PngSignature))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-FAC-0011",
                "La imagen debe ser un PNG de 512 KB como máximo.");
        }

        var contentRoot = Path.GetFullPath(_hostEnvironment.ContentRootPath);
        var fullPath = Path.GetFullPath(Path.Combine(contentRoot, relative));
        if (!fullPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-FAC-0010",
                "Ruta de imagen PNG fuera del content root.");
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, pngBytes, cancellationToken);
        return relative;
    }

    private static async Task<byte[]?> TryLoadIssuerPngAsync(
        string? relativePath,
        string contentRoot,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var relative = relativePath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(contentRoot, relative));
        if (!fullPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public async Task<MaterialDeletionRequestView> RequestMaterialDeletionAsync(
        Guid materialId,
        Guid requestedByUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _ = await _db.Materials.SingleOrDefaultAsync(
            value => value.Id == materialId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-MAT-0001",
                "Material no encontrado.");
        var hasStock = await _db.StockBalances.AnyAsync(
            value => value.MaterialId == materialId && value.Quantity > 0,
            cancellationToken);
        var request = new MaterialDeletionRequest(
            materialId,
            requestedByUserId,
            reason,
            hasStock,
            _clock.UtcNow);
        _db.MaterialDeletionRequests.Add(request);
        if (!hasStock)
        {
            var material = await _db.Materials.SingleAsync(
                value => value.Id == materialId,
                cancellationToken);
            material.Deactivate();
            var systemApprover = Guid.CreateVersion7();
            request.Approve(systemApprover, _clock.UtcNow);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToDeletion(request);
    }

    public async Task ApproveMaterialDeletionAsync(
        Guid requestId,
        Guid approvedByUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.MaterialDeletionRequests.SingleOrDefaultAsync(
            value => value.Id == requestId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-MAT-0002",
                "Solicitud no encontrada.");
        request.Approve(approvedByUserId, _clock.UtcNow);
        var material = await _db.Materials.SingleAsync(
            value => value.Id == request.MaterialId,
            cancellationToken);
        material.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectMaterialDeletionAsync(
        Guid requestId,
        Guid rejectedByUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.MaterialDeletionRequests.SingleOrDefaultAsync(
            value => value.Id == requestId,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-MAT-0002",
                "Solicitud no encontrada.");
        request.Reject(rejectedByUserId, _clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MaterialDeletionRequestView>> ListMaterialDeletionRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.MaterialDeletionRequests.AsNoTracking()
            .OrderByDescending(value => value.RequestedUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDeletion).ToArray();
    }

    public async Task<IssuerFiscalProfile?> GetIssuerAsync(
        CancellationToken cancellationToken = default) =>
        await _db.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<QuoteView> CreateQuoteAsync(
        CreateQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines.Count == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-QUOTE-0001",
                "El presupuesto debe contener al menos una línea.");
        }

        var customer = await _db.Customers.SingleOrDefaultAsync(
            value => value.Id == command.CustomerId && value.IsActive,
            cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-QUOTE-0002",
                "El cliente no existe o no está activo.");
        if (!await _db.Warehouses.AnyAsync(
                value => value.Id == command.DefaultWarehouseId,
                cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-QUOTE-0003",
                "El almacén indicado no existe.");
        }

        var materialIds = command.Lines.Select(line => line.MaterialId).Distinct().ToArray();
        var materials = await _db.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        if (materials.Count != materialIds.Length)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-QUOTE-0004",
                "Uno o más materiales no existen.");
        }

        var number = await NextDocumentAsync("QUOTE", "QT", cancellationToken);
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var quote = new Quote(
            number,
            command.CustomerId,
            command.DefaultWarehouseId,
            today,
            command.ValidUntil < today ? today.AddDays(30) : command.ValidUntil,
            string.IsNullOrWhiteSpace(command.Currency) ? "EUR" : command.Currency,
            command.TaxRate,
            _clock.UtcNow);
        quote.SetNotes(command.Notes);
        var lines = command.Lines.Select(line =>
        {
            var material = materials[line.MaterialId];
            var description = string.IsNullOrWhiteSpace(material.DisplayName)
                ? material.Code
                : $"{material.Code} — {material.DisplayName}";
            return new QuoteLine(
                quote.Id,
                material.Id,
                description,
                line.Quantity,
                line.UnitPrice,
                command.TaxRate);
        }).ToArray();
        quote.RecalculateTotals(lines);
        _db.Quotes.Add(quote);
        _db.QuoteLines.AddRange(lines);
        await _db.SaveChangesAsync(cancellationToken);
        _ = customer;
        return ToQuoteView(quote, lines);
    }

    public async Task<IReadOnlyCollection<QuoteView>> ListQuotesAsync(
        CancellationToken cancellationToken = default)
    {
        await ExpireQuotesAsync(cancellationToken);
        var quotes = await _db.Quotes.AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var ids = quotes.Select(value => value.Id).ToArray();
        var lines = await _db.QuoteLines.AsNoTracking()
            .Where(value => ids.Contains(value.QuoteId))
            .ToListAsync(cancellationToken);
        var byQuote = lines.GroupBy(value => value.QuoteId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        return quotes.Select(quote =>
            ToQuoteView(
                quote,
                byQuote.TryGetValue(quote.Id, out var rows) ? rows : [])).ToArray();
    }

    public async Task<QuoteView> SendQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var quote = await LoadQuoteAsync(quoteId, cancellationToken);
        quote.MarkSent();
        await _db.SaveChangesAsync(cancellationToken);
        var lines = await _db.QuoteLines.AsNoTracking()
            .Where(value => value.QuoteId == quote.Id)
            .ToListAsync(cancellationToken);
        return ToQuoteView(quote, lines);
    }

    public async Task<QuoteView> RejectQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var quote = await LoadQuoteAsync(quoteId, cancellationToken);
        quote.MarkRejected();
        await _db.SaveChangesAsync(cancellationToken);
        var lines = await _db.QuoteLines.AsNoTracking()
            .Where(value => value.QuoteId == quote.Id)
            .ToListAsync(cancellationToken);
        return ToQuoteView(quote, lines);
    }

    public async Task<QuoteView> AcceptQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        var quote = await LoadQuoteAsync(quoteId, cancellationToken);
        var lines = await _db.QuoteLines
            .Where(value => value.QuoteId == quote.Id)
            .ToListAsync(cancellationToken);
        if (lines.Count == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-QUOTE-0005",
                "El presupuesto no tiene líneas.");
        }

        var customer = await _db.Customers.SingleAsync(
            value => value.Id == quote.CustomerId,
            cancellationToken);
        var materials = await _db.Materials
            .Where(value => lines.Select(line => line.MaterialId).Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var unitIds = materials.Values.Select(value => value.UnitId).Distinct().ToArray();
        var units = await _db.UnitsOfMeasure.AsNoTracking()
            .Where(value => unitIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);

        var soNumber = await NextDocumentAsync("SALES_ORDER", "SO", cancellationToken);
        var order = new SalesOrder(
            soNumber,
            quote.CustomerId,
            quote.DefaultWarehouseId,
            quote.ValidUntil,
            quote.Currency,
            customer.AddressLine1,
            customer.AddressLine2,
            customer.City,
            customer.Region,
            customer.PostalCode,
            customer.CountryCode,
            _clock.UtcNow,
            quote.Number);
        var soLines = lines.Select(line =>
        {
            var material = materials[line.MaterialId];
            var unitCode = units.TryGetValue(material.UnitId, out var unit)
                ? unit.Code
                : "UN";
            return new SalesOrderLine(
                order.Id,
                material.Id,
                line.Quantity,
                line.UnitPrice,
                quote.Currency,
                unitCode);
        }).ToArray();

        _db.SalesOrders.Add(order);
        _db.SalesOrderLines.AddRange(soLines);
        quote.MarkAccepted(order.Id);
        await _db.SaveChangesAsync(cancellationToken);
        return ToQuoteView(quote, lines);
    }

    public async Task<byte[]> RenderQuotePdfAsync(
        Guid quoteId,
        CancellationToken cancellationToken = default)
    {
        await ExpireQuotesAsync(cancellationToken);
        var quote = await _db.Quotes.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == quoteId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-QUOTE-0006",
                "Presupuesto no encontrado.");
        var lines = await _db.QuoteLines.AsNoTracking()
            .Where(value => value.QuoteId == quoteId)
            .ToListAsync(cancellationToken);
        var customer = await _db.Customers.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == quote.CustomerId, cancellationToken);
        var issuer = await _db.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        var contentRoot = Path.GetFullPath(_hostEnvironment.ContentRootPath);
        var footerImage = await TryLoadIssuerPngAsync(
            issuer?.SignaturePngRelativePath ?? issuer?.SealPngRelativePath,
            contentRoot,
            cancellationToken);

        var model = CommercialPdf.Create(
            issuer,
            "PRESUPUESTO",
            quote.Number,
            [
                $"Estado: {quote.Status}",
                $"Fecha: {quote.IssueDate:yyyy-MM-dd}",
                $"Válido hasta: {quote.ValidUntil:yyyy-MM-dd}",
                $"Moneda: {quote.Currency}",
            ],
            "Cliente",
            [
                customer is null
                    ? $"Cliente Id {quote.CustomerId:D}"
                    : $"{customer.Name}  NIF {customer.TaxId}",
                customer is null
                    ? string.Empty
                    : $"{customer.AddressLine1}, {customer.PostalCode} {customer.City}",
            ],
            lines.Select(line =>
                new CommercialLineRow(
                    line.Description,
                    line.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
                    line.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture),
                    line.TaxRate.ToString("0.##", CultureInfo.InvariantCulture),
                    line.NetAmount.ToString("0.00", CultureInfo.InvariantCulture))),
            [
                $"Base {quote.NetAmount.ToString("0.00", CultureInfo.InvariantCulture)} {quote.Currency}",
                $"IVA {quote.TaxRate.ToString("0.##", CultureInfo.InvariantCulture)}%  {quote.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture)} {quote.Currency}",
                $"TOTAL {quote.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)} {quote.Currency}",
            ],
            [
                "Este presupuesto no es una factura.",
                string.IsNullOrWhiteSpace(quote.Notes) ? "Sin notas." : quote.Notes!,
                footerImage is null ? "[Espacio para sello / firma]" : "Sello / firma incrustado.",
            ],
            brandDisplayName: await BrandNameAsync(cancellationToken));
        return CommercialPdf.Render(model, footerImage ?? await BrandLogoPngAsync(cancellationToken));
    }

    private async Task ExpireQuotesAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var expired = await _db.Quotes
            .Where(value =>
                value.ValidUntil < today
                && (value.Status == QuoteStatus.Draft || value.Status == QuoteStatus.Sent))
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var quote in expired)
        {
            quote.MarkExpired();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Quote> LoadQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        await ExpireQuotesAsync(cancellationToken);
        return await _db.Quotes.SingleOrDefaultAsync(value => value.Id == quoteId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-QUOTE-0006",
                "Presupuesto no encontrado.");
    }

    private static QuoteView ToQuoteView(Quote quote, IReadOnlyCollection<QuoteLine> lines) =>
        new(
            quote.Id,
            quote.Number,
            quote.Status.ToString(),
            quote.CustomerId,
            quote.DefaultWarehouseId,
            quote.IssueDate,
            quote.ValidUntil,
            quote.Currency,
            quote.TaxRate,
            quote.NetAmount,
            quote.TaxAmount,
            quote.TotalAmount,
            quote.SalesOrderId,
            quote.Notes,
            lines.Select(line =>
                new QuoteLineView(
                    line.Id,
                    line.MaterialId,
                    line.Description,
                    line.Quantity,
                    line.UnitPrice,
                    line.TaxRate,
                    line.NetAmount)).ToArray());

    private async Task RegeneratePrepPicksAsync(
        SalesOrder order,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PrepPickLines
            .Where(value => value.SalesOrderId == order.Id)
            .ToListAsync(cancellationToken);

        // Keep confirmed/consumed picks so re-Join/wave cannot wipe DN binding.
        var disposable = existing
            .Where(value =>
                value.ConfirmedQuantity <= 0
                && value.ConsumedQuantity <= 0)
            .ToList();
        if (disposable.Count > 0)
        {
            _db.PrepPickLines.RemoveRange(disposable);
        }

        var retainedByLine = existing
            .Where(value =>
                value.ConfirmedQuantity > 0
                || value.ConsumedQuantity > 0)
            .GroupBy(value => value.SalesOrderLineId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.RemainingConfirmed));

        var lines = await _db.SalesOrderLines
            .Where(value => value.SalesOrderId == order.Id)
            .ToListAsync(cancellationToken);
        foreach (var line in lines)
        {
            var open = line.OpenQuantity;
            if (open <= 0)
            {
                continue;
            }

            retainedByLine.TryGetValue(line.Id, out var covered);
            var need = open - covered;
            if (need <= 0)
            {
                continue;
            }

            var suggestions = await SuggestFefoAsync(
                line.MaterialId,
                order.DefaultWarehouseId,
                need,
                cancellationToken);
            foreach (var suggestion in suggestions)
            {
                _db.PrepPickLines.Add(
                    new PrepPickLine(
                        order.Id,
                        line.Id,
                        line.MaterialId,
                        suggestion.LocationId,
                        suggestion.LotId == Guid.Empty
                            ? null
                            : suggestion.LotId,
                        suggestion.Quantity));
            }
        }
    }

    private async Task<IReadOnlyList<(Guid LocationId, Guid LotId, decimal Quantity)>> SuggestFefoAsync(
        Guid materialId,
        Guid warehouseId,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        // House stock only — same owner grain DN/Confirm use by default.
        var balances = await _db.StockBalances
            .AsNoTracking()
            .Where(value =>
                value.MaterialId == materialId
                && value.WarehouseId == warehouseId
                && value.Status == StockStatus.Available
                && value.Quantity > 0
                && value.OwnerCustomerId == Guid.Empty)
            .ToListAsync(cancellationToken);
        var lotIds = balances
            .Select(value => value.LotId)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        var lots = await _db.Lots
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
            .OrderBy(value => value.Expiry is null)
            .ThenBy(value => value.Expiry)
            .ThenBy(value => value.Balance.LotId)
            .ThenBy(value => value.Balance.LocationId)
            .ToArray();

        var remaining = quantity;
        var result = new List<(Guid LocationId, Guid LotId, decimal Quantity)>();
        foreach (var entry in ordered)
        {
            if (remaining <= 0)
            {
                break;
            }

            var take = Math.Min(remaining, entry.Balance.Quantity);
            result.Add(
                (entry.Balance.LocationId, entry.Balance.LotId, take));
            remaining -= take;
        }

        return result;
    }

    private static PrepPickLineView ToPrepPick(PrepPickLine pick) =>
        new(
            pick.Id,
            pick.SalesOrderId,
            pick.SalesOrderLineId,
            pick.MaterialId,
            pick.SuggestedLocationId,
            pick.SuggestedLotId,
            pick.Quantity,
            pick.ConfirmedQuantity,
            pick.ConfirmedUtc);

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

    private async Task<InvoiceSeries> LockInvoiceSeriesAsync(
        string seriesCode,
        CancellationToken cancellationToken)
    {
        var normalized = seriesCode.Trim().ToUpperInvariant();
        // Row lock first; then load via EF (works if the series is already tracked).
        var locked = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM invoice_series WHERE code = {normalized} FOR UPDATE",
            cancellationToken);
        _ = locked;
        var series = await _db.InvoiceSeries
            .SingleOrDefaultAsync(value => value.Code == normalized, cancellationToken);
        if (series is null)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-INV-SERIES",
                "La serie de facturación no existe; debe crearse antes de emitir.");
        }

        return series;
    }

    private async Task<string?> LatestSifHashInSeriesAsync(
        string seriesCode,
        CancellationToken cancellationToken) =>
        await _db.Invoices.AsNoTracking()
            .Where(value => value.SeriesCode == seriesCode)
            .OrderByDescending(value => value.Number)
            .ThenByDescending(value => value.CreatedUtc)
            .Select(value => value.SifHash)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<IdempotencyStart<T>> BeginIdempotencyAsync<T>(
        string commandType,
        string key,
        object request,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IDEMPOTENCY-0001",
                "Idempotency-Key es obligatorio y admite hasta 200 caracteres.");
        }

        var requestJson = JsonSerializer.Serialize(request, IdempotencyJsonOptions);
        var requestHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)));
        var scopedKey = string.Create(
            CultureInfo.InvariantCulture,
            $"{_requestContext.UserId:N}:{key.Trim()}");
        if (scopedKey.Length > 200)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IDEMPOTENCY-0001",
                "Idempotency-Key es demasiado largo tras el ámbito de usuario.");
        }

        var lockKey = $"idempotency:{commandType}:{scopedKey}";
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);
        var existing = await _db.IdempotencyRecords
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
                throw new LogisticsException(
                    LogisticsErrorKind.Conflict,
                    "AAA-IDEMPOTENCY-0002",
                    "La clave idempotente ya se usó con otra petición.");
            }

            if (existing.Status != IdempotencyStatus.Completed
                || existing.ResultJson is null)
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Conflict,
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
        _db.IdempotencyRecords.Add(record);
        return new IdempotencyStart<T>(record, null);
    }

    private static string ComputeSifHash(
        string series,
        int number,
        string taxId,
        decimal total,
        string? previous)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{series}|{number}|{taxId}|{total:0.000000}|{previous}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    private sealed record IdempotencyStart<T>(
        IdempotencyRecord Record,
        T? Result)
        where T : class;

    private static LocationNodeView ToLocation(LocationNode node) =>
        new(
            node.Id,
            node.WarehouseId,
            node.ParentId,
            node.Code,
            node.Name,
            node.Kind.ToString(),
            node.IsActive);

    private static CarrierView ToCarrier(Carrier carrier) =>
        new(
            carrier.Id,
            carrier.Code,
            carrier.Name,
            carrier.TaxId,
            carrier.Phone,
            carrier.IsActive);

    private static HandlingUnitView ToHandlingUnit(HandlingUnit unit) =>
        new(
            unit.Id,
            unit.Sscc,
            unit.SalesOrderId,
            unit.DeliveryId,
            unit.PackageCount,
            unit.CreatedUtc);

    private static string GenerateDidacticSscc()
    {
        // 18-digit didactic SSCC (not a real GS1 check digit).
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            .ToString(CultureInfo.InvariantCulture);
        var suffix = RandomNumberGenerator.GetInt32(100000, 999999)
            .ToString(CultureInfo.InvariantCulture);
        var raw = ("003" + ticks + suffix).PadRight(18, '0');
        return raw[..18];
    }

    private static InvoiceView ToInvoice(
        Invoice invoice,
        IReadOnlyCollection<InvoiceLineView>? lines = null) =>
        new(
            invoice.Id,
            invoice.DocumentNumber,
            invoice.Kind.ToString(),
            invoice.Status.ToString(),
            invoice.CustomerId,
            invoice.SalesOrderId,
            invoice.NetAmount,
            invoice.TaxRate,
            invoice.TaxAmount,
            invoice.TotalAmount,
            invoice.SifHash,
            invoice.PreviousSifHash,
            invoice.IssueDate,
            lines ?? []);

    private static MaterialDeletionRequestView ToDeletion(MaterialDeletionRequest request) =>
        new(
            request.Id,
            request.MaterialId,
            request.RequestedByUserId,
            request.Reason,
            request.HasStock,
            request.Status.ToString(),
            request.RequestedUtc);
}
