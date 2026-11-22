using System.Globalization;
using System.Text;
using AAALogistics.Application.Branding;
using AAALogistics.Application.Documents;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Sales;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Documents;

/// <summary>
/// Minimal PDF renderer without third-party packages (ISO 32000 text objects).
/// </summary>
public sealed class DocumentPdfService(
    ApplicationDbContext dbContext,
    ICustomerRlsSession rlsSession,
    IAppBrandingStore brandingStore) : IDocumentPdfService
{
    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly ICustomerRlsSession _rlsSession =
        rlsSession ?? throw new ArgumentNullException(nameof(rlsSession));

    private readonly IAppBrandingStore _brandingStore =
        brandingStore ?? throw new ArgumentNullException(nameof(brandingStore));

    private async Task<string?> BrandNameAsync(CancellationToken cancellationToken)
    {
        var brand = await _brandingStore.GetAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(brand.DisplayName) ? null : brand.DisplayName;
    }

    private async Task<byte[]?> BrandLogoPngAsync(CancellationToken cancellationToken)
    {
        var logo = await _brandingStore.TryReadLogoAsync(cancellationToken);
        if (logo is null || logo.Value.ContentType != "image/png")
        {
            return null;
        }

        return logo.Value.Bytes;
    }

    public async Task<byte[]> RenderPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var order = await _dbContext.PurchaseOrders.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == purchaseOrderId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-PDF-0001",
                "El pedido de compra no existe.");
        var lines = await _dbContext.PurchaseOrderLines.AsNoTracking()
            .Where(value => value.PurchaseOrderId == purchaseOrderId)
            .ToListAsync(cancellationToken);
        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == order.SupplierId, cancellationToken);
        var materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();
        var materials = await _dbContext.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var issuer = await _dbContext.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var sealPng = await TryLoadIssuerSealOrSignatureAsync(issuer, cancellationToken);
        var meta = new List<string>
        {
            $"Estado: {order.Status}",
            $"Fecha: {order.DocumentDate:yyyy-MM-dd}",
            $"Moneda: {order.Currency}",
        };
        if (order.ExpectedReceiptDate is { } expected)
        {
            meta.Add($"Recepción prevista: {expected:yyyy-MM-dd}");
        }

        var rows = lines.Select(line =>
        {
            var materialLabel = materials.TryGetValue(line.MaterialId, out var material)
                ? $"{material.Code} — {material.DisplayName}"
                : "Material (sin maestro)";
            return new CommercialLineRow(
                materialLabel,
                Fmt(line.RequestedQuantity),
                $"{Fmt(line.PurchasePrice)} {line.Currency}",
                "—",
                Fmt(line.OpenQuantity));
        }).ToArray();
        var model = CommercialPdf.Create(
            issuer,
            "PEDIDO COMPRA",
            order.Number,
            meta,
            "Proveedor",
            [
                supplier is null
                    ? "Proveedor (maestro no cargado)"
                    : $"{supplier.Name}  NIF {supplier.TaxId}",
            ],
            rows,
            [$"Líneas: {lines.Count}"],
            ["Documento comercial de compra.", sealPng is null
                ? "[Espacio para sello / firma]"
                : "Sello / firma incrustado."],
            includePayment: false,
            brandDisplayName: await BrandNameAsync(cancellationToken));
        var brandLogo = sealPng ?? await BrandLogoPngAsync(cancellationToken);
        return CommercialPdf.Render(model, brandLogo);
    }

    public async Task<byte[]> RenderSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var order = await _dbContext.SalesOrders.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == salesOrderId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-PDF-0003",
                "El pedido de venta no existe.");
        var lines = await _dbContext.SalesOrderLines.AsNoTracking()
            .Where(value => value.SalesOrderId == salesOrderId)
            .ToListAsync(cancellationToken);
        var customer = await _dbContext.Customers.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == order.CustomerId, cancellationToken);
        var materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();
        var materials = await _dbContext.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var issuer = await _dbContext.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var sealPng = await TryLoadIssuerSealOrSignatureAsync(issuer, cancellationToken);
        var meta = new List<string>
        {
            $"Estado: {order.Status}",
            $"Entrega pedida: {order.RequestedDeliveryDate:yyyy-MM-dd}",
            $"Moneda: {order.Currency}",
        };
        if (!string.IsNullOrWhiteSpace(order.Incoterms))
        {
            meta.Add($"Incoterms: {order.Incoterms}");
        }

        if (!string.IsNullOrWhiteSpace(order.CustomsReference))
        {
            meta.Add($"Ref. aduanas: {order.CustomsReference}");
        }

        var rows = lines.Select(line =>
        {
            var materialLabel = materials.TryGetValue(line.MaterialId, out var material)
                ? $"{material.Code} — {material.DisplayName}"
                : "Material (sin maestro)";
            return new CommercialLineRow(
                materialLabel,
                Fmt(line.RequestedQuantity),
                $"{Fmt(line.CatalogPriceSnapshot)} {line.Currency}",
                "—",
                Fmt(line.PostedQuantity));
        }).ToArray();
        var model = CommercialPdf.Create(
            issuer,
            "PEDIDO VENTA",
            order.Number,
            meta,
            "Cliente",
            [
                customer is null
                    ? "Cliente (maestro no cargado)"
                    : $"{customer.Name}  NIF {customer.TaxId}",
                $"{order.DeliveryAddressLine1}, {order.DeliveryPostalCode} {order.DeliveryCity}",
            ],
            rows,
            [$"Líneas: {lines.Count}"],
            ["Documento comercial de venta.", sealPng is null
                ? "[Espacio para sello / firma]"
                : "Sello / firma incrustado."],
            brandDisplayName: await BrandNameAsync(cancellationToken));
        var brandLogo = sealPng ?? await BrandLogoPngAsync(cancellationToken);
        return CommercialPdf.Render(model, brandLogo);
    }

    public async Task<byte[]> RenderDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var delivery = await _dbContext.Deliveries.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == deliveryId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-PDF-0002",
                "El albarán no existe.");
        var lines = await _dbContext.DeliveryLines.AsNoTracking()
            .Where(value => value.DeliveryId == deliveryId)
            .ToListAsync(cancellationToken);
        var materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();
        var materials = await _dbContext.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var issuer = await _dbContext.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        var salesOrderNumber = await _dbContext.SalesOrders.AsNoTracking()
            .Where(value => value.Id == delivery.SalesOrderId)
            .Select(value => value.Number)
            .FirstOrDefaultAsync(cancellationToken) ?? "—";
        var warehouseIds = lines.Select(line => line.WarehouseId).Distinct().ToArray();
        var warehouses = await _dbContext.Warehouses.AsNoTracking()
            .Where(value => warehouseIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var sealPng = await TryLoadIssuerSealOrSignatureAsync(issuer, cancellationToken);
        var rows = lines.Select(line =>
        {
            var materialLabel = materials.TryGetValue(line.MaterialId, out var material)
                ? $"{material.Code} — {material.DisplayName}"
                : "—";
            var warehouseLabel = warehouses.TryGetValue(line.WarehouseId, out var warehouse)
                ? warehouse.Code
                : "—";
            return new CommercialLineRow(
                materialLabel,
                Fmt(line.Quantity),
                "—",
                "—",
                warehouseLabel);
        }).ToArray();
        var model = CommercialPdf.Create(
            issuer,
            "ALBARÁN",
            delivery.Number,
            [
                $"Pedido: {salesOrderNumber}",
                $"Fecha: {delivery.DocumentDate:yyyy-MM-dd}",
            ],
            "Entrega",
            [delivery.DeliveryAddressSnapshot],
            rows,
            [$"Líneas: {lines.Count}"],
            ["Albarán de entrega.", sealPng is null
                ? "[Espacio para sello / firma]"
                : "Sello / firma incrustado."],
            includePayment: false,
            brandDisplayName: await BrandNameAsync(cancellationToken));
        var brandLogo = sealPng ?? await BrandLogoPngAsync(cancellationToken);
        return CommercialPdf.Render(model, brandLogo);
    }

    public async Task<byte[]> RenderGoodsReceiptAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var receipt = await _dbContext.GoodsReceipts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == goodsReceiptId, cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-PDF-0004",
                "La recepción no existe.");
        var lines = await _dbContext.GoodsReceiptLines.AsNoTracking()
            .Where(value => value.GoodsReceiptId == goodsReceiptId)
            .ToListAsync(cancellationToken);
        var purchaseOrderNumber = await _dbContext.PurchaseOrders.AsNoTracking()
            .Where(value => value.Id == receipt.PurchaseOrderId)
            .Select(value => value.Number)
            .FirstOrDefaultAsync(cancellationToken) ?? "—";
        var supplier = await _dbContext.Suppliers.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == receipt.SupplierId, cancellationToken);
        var materialIds = lines.Select(line => line.MaterialId).Distinct().ToArray();
        var warehouseIds = lines.Select(line => line.WarehouseId).Distinct().ToArray();
        var materials = await _dbContext.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var warehouses = await _dbContext.Warehouses.AsNoTracking()
            .Where(value => warehouseIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var issuer = await _dbContext.IssuerFiscalProfiles.AsNoTracking()
            .OrderBy(value => value.LegalName)
            .FirstOrDefaultAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var sealPng = await TryLoadIssuerSealOrSignatureAsync(issuer, cancellationToken);
        var rows = lines.Select(line =>
        {
            var materialLabel = materials.TryGetValue(line.MaterialId, out var material)
                ? $"{material.Code} — {material.DisplayName}"
                : "—";
            var warehouseLabel = warehouses.TryGetValue(line.WarehouseId, out var warehouse)
                ? warehouse.Code
                : "—";
            return new CommercialLineRow(
                materialLabel,
                Fmt(line.Quantity),
                line.UnitSnapshot,
                "—",
                warehouseLabel);
        }).ToArray();
        var model = CommercialPdf.Create(
            issuer,
            "RECEPCIÓN",
            receipt.Number,
            [
                $"Pedido compra: {purchaseOrderNumber}",
                $"Fecha: {receipt.DocumentDate:yyyy-MM-dd}",
                $"Contabilizado: {receipt.PostedUtc:yyyy-MM-dd HH:mm} UTC",
            ],
            "Proveedor",
            [
                supplier is null
                    ? "Proveedor —"
                    : $"{supplier.Name}  NIF {supplier.TaxId}",
            ],
            rows,
            [$"Líneas: {lines.Count}"],
            ["Recepción de mercancía (GR).", sealPng is null
                ? "[Espacio para sello / firma]"
                : "Sello / firma incrustado."],
            includePayment: false,
            brandDisplayName: await BrandNameAsync(cancellationToken));
        var brandLogo = sealPng ?? await BrandLogoPngAsync(cancellationToken);
        return CommercialPdf.Render(model, brandLogo);
    }

    public async Task<byte[]> RenderOtifReportAsync(
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
                value.Number,
                value.RequestedDeliveryDate,
            })
            .ToListAsync(cancellationToken);
        var orderIds = delivered.Select(value => value.Id).ToArray();
        var lastDeliveryByOrder = orderIds.Length == 0
            ? new Dictionary<Guid, DateOnly>()
            : await _dbContext.Deliveries
                .AsNoTracking()
                .Where(value => orderIds.Contains(value.SalesOrderId))
                .GroupBy(value => value.SalesOrderId)
                .Select(group => new
                {
                    SalesOrderId = group.Key,
                    LastDate = group.Max(value => value.DocumentDate),
                })
                .ToDictionaryAsync(
                    value => value.SalesOrderId,
                    value => value.LastDate,
                    cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var onTime = 0;
        var rows = new List<IReadOnlyList<string>>();
        foreach (var order in delivered.OrderBy(value => value.Number))
        {
            var deliveryDate = lastDeliveryByOrder.TryGetValue(order.Id, out var date)
                ? date
                : (DateOnly?)null;
            var isOnTime = deliveryDate is { } actual
                && actual <= order.RequestedDeliveryDate;
            if (isOnTime)
            {
                onTime++;
            }

            rows.Add(
            [
                order.Number,
                order.RequestedDeliveryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                deliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—",
                isOnTime ? "A tiempo" : "Fuera de plazo",
            ]);
        }

        var total = delivered.Count;
        var percent = total == 0
            ? 0m
            : Math.Round(100m * onTime / total, 2, MidpointRounding.AwayFromZero);
        return ReportPdf.Render(
            "INFORME OTIF",
            [
                "Criterio: fecha de entrega ≤ fecha pedida.",
                $"Generado: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC",
            ],
            ["Pedido", "Fecha pedida", "Entrega", "Resultado"],
            [120, 120, 120, 172],
            [false, false, false, false],
            rows,
            [$"OTIF: {onTime}/{total} ({percent.ToString("0.00", CultureInfo.InvariantCulture)}%)"]);
    }

    public async Task<byte[]> RenderOrdersByStatusReportAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var purchases = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .Select(value => new
            {
                Kind = "Pedido compra",
                value.Number,
                Status = value.Status.ToString(),
            })
            .ToListAsync(cancellationToken);
        var sales = await _dbContext.SalesOrders
            .AsNoTracking()
            .OrderByDescending(value => value.CreatedUtc)
            .Take(200)
            .Select(value => new
            {
                Kind = "Pedido venta",
                value.Number,
                Status = value.Status.ToString(),
            })
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var rows = purchases.Concat(sales)
            .Select(row => (IReadOnlyList<string>)[row.Kind, row.Number, row.Status])
            .ToArray();
        return ReportPdf.Render(
            "INFORME PEDIDOS POR ESTADO",
            [$"Generado: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC", $"Filas: {rows.Length}"],
            ["Tipo", "Número", "Estado"],
            [160, 180, 192],
            [false, false, false],
            rows);
    }

    public async Task<byte[]> RenderStockReportAsync(
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var balances = await _dbContext.StockBalances.AsNoTracking()
            .OrderByDescending(value => value.Quantity)
            .Take(200)
            .ToListAsync(cancellationToken);
        var materialIds = balances.Select(value => value.MaterialId).Distinct().ToArray();
        var warehouseIds = balances.Select(value => value.WarehouseId).Distinct().ToArray();
        var locationIds = balances
            .Where(value => value.LocationId != Guid.Empty)
            .Select(value => value.LocationId)
            .Distinct()
            .ToArray();
        var materials = await _dbContext.Materials.AsNoTracking()
            .Where(value => materialIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var warehouses = await _dbContext.Warehouses.AsNoTracking()
            .Where(value => warehouseIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, cancellationToken);
        var locations = locationIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _dbContext.LocationNodes.AsNoTracking()
                .Where(value => locationIds.Contains(value.Id))
                .ToDictionaryAsync(value => value.Id, value => value.Code, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var rows = balances.Select(balance =>
        {
            var material = materials.TryGetValue(balance.MaterialId, out var mat)
                ? mat.Code
                : "—";
            var warehouse = warehouses.TryGetValue(balance.WarehouseId, out var wh)
                ? wh.Code
                : "—";
            var location = balance.LocationId == Guid.Empty
                ? "raíz"
                : (locations.TryGetValue(balance.LocationId, out var code) ? code : "—");
            return (IReadOnlyList<string>)
            [
                material,
                warehouse,
                location,
                balance.Quantity.ToString("0.####", CultureInfo.InvariantCulture),
                balance.Status.ToString(),
            ];
        }).ToArray();

        return ReportPdf.Render(
            "INFORME DE STOCK",
            [
                $"Generado: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC",
                $"Filas: {rows.Length} (máx. 200)",
            ],
            ["Material", "Almacén", "Ubicación", "Cantidad", "Estado"],
            [120, 100, 100, 90, 122],
            [false, false, false, true, false],
            rows);
    }

    private static string Fmt(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private async Task<byte[]?> TryLoadIssuerSealOrSignatureAsync(
        Domain.Billing.IssuerFiscalProfile? issuer,
        CancellationToken cancellationToken)
    {
        foreach (var path in new[]
                 {
                     issuer?.SignaturePngRelativePath,
                     issuer?.SealPngRelativePath,
                     issuer?.LogoPngRelativePath,
                 })
        {
            var bytes = await TryLoadIssuerImageAsync(path, cancellationToken);
            if (bytes is not null)
            {
                return bytes;
            }
        }

        return null;
    }

    private static async Task<byte[]?> TryLoadIssuerImageAsync(
        string? relativePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var relative = relativePath.Replace('\\', '/').TrimStart('/');
        var contentRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var fullPath = Path.GetFullPath(Path.Combine(contentRoot, relative));
        if (!fullPath.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(fullPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }
}

public static class MinimalPdf
{
    public static byte[] FromPlainText(string text, byte[]? pngBytes = null)
    {
        var escaped = text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
        var lines = escaped.Split('\n');
        var content = new StringBuilder();
        content.AppendLine("BT /F1 10 Tf 50 750 Td 12 TL");
        foreach (var line in lines.Take(110))
        {
            content.AppendLine($"({line.TrimEnd('\r')}) '");
        }

        content.AppendLine("ET");
        return FromContentStream(content.ToString(), pngBytes, includeBoldFont: false);
    }

    public static byte[] FromContentStream(
        string contentStream,
        byte[]? pngBytes = null,
        bool includeBoldFont = false)
    {
        var content = new StringBuilder(contentStream);
        DecodedPng? seal = null;
        if (pngBytes is { Length: > 0 })
        {
            seal = TryDecodePng(pngBytes);
        }

        if (seal is not null)
        {
            var drawWidth = Math.Min(110, seal.Width);
            var drawHeight = Math.Max(
                1,
                (int)Math.Round(drawWidth * (double)seal.Height / seal.Width));
            content.AppendLine("q");
            content.AppendLine(
                CultureInfo.InvariantCulture,
                $"{drawWidth} 0 0 {drawHeight} 50 36 cm");
            content.AppendLine("/ImSeal Do");
            content.AppendLine("Q");
        }

        var stream = content.ToString();
        var fontResources = includeBoldFont
            ? "/Font << /F1 5 0 R /F2 7 0 R >>"
            : "/Font << /F1 5 0 R >>";
        var objects = new List<string>
        {
            "1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n",
            "2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n",
        };

        if (seal is null)
        {
            objects.Add(
                "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + $"/Contents 4 0 R /Resources << {fontResources} >> >>endobj\n");
            objects.Add(
                $"4 0 obj<< /Length {Encoding.ASCII.GetByteCount(stream)} >>stream\n{stream}endstream\nendobj\n");
            objects.Add(
                "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");
            if (includeBoldFont)
            {
                objects.Add(
                    "7 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>endobj\n");
            }
        }
        else
        {
            objects.Add(
                "3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
                + $"/Contents 4 0 R /Resources << {fontResources} "
                + "/XObject << /ImSeal 6 0 R >> >> >>endobj\n");
            objects.Add(
                $"4 0 obj<< /Length {Encoding.ASCII.GetByteCount(stream)} >>stream\n{stream}endstream\nendobj\n");
            objects.Add(
                "5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n");
            objects.Add(
                $"6 0 obj<< /Type /XObject /Subtype /Image /Width {seal.Width} "
                + $"/Height {seal.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 "
                + $"/Length {seal.RgbBytes.Length} >>stream\n");
            if (includeBoldFont)
            {
                // Object 7 appended after image binary in sealed path — use object 8 instead.
                // Keep bold as object 7 only when no seal; with seal bold is object 7 after image closes.
            }
        }

        if (seal is null)
        {
            return BuildAsciiPdf(objects);
        }

        // Seal path: append bold font as object 7 after image endobj is written in BuildBinaryPdf.
        return BuildBinaryPdfWithSeal(objects, seal, includeBoldFont);
    }

    private static byte[] BuildAsciiPdf(List<string> objects)
    {
        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(obj);
        }

        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append($"xref\n0 {objects.Count + 1}\n");
        builder.Append("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            builder.Append($"{offsets[i]:D10} 00000 n \n");
        }

        builder.Append(
            $"trailer<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static byte[] BuildBinaryPdfWithSeal(
        List<string> objects,
        DecodedPng seal,
        bool includeBoldFont)
    {
        var builder = new StringBuilder();
        builder.Append("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        // objects[0..5] are catalog..font; objects[5] is image stream header without end
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(objects[i]);
        }

        var asciiPrefix = Encoding.ASCII.GetBytes(builder.ToString());
        using var ms = new MemoryStream();
        ms.Write(asciiPrefix);
        ms.Write(seal.RgbBytes);
        var mid = Encoding.ASCII.GetBytes("\nendstream\nendobj\n");
        ms.Write(mid);
        if (includeBoldFont)
        {
            offsets.Add((int)ms.Length);
            var bold = Encoding.ASCII.GetBytes(
                "7 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>endobj\n");
            ms.Write(bold);
        }

        var xrefOffset = (int)ms.Length;
        var objectCount = includeBoldFont ? objects.Count + 1 : objects.Count;
        var xrefBuilder = new StringBuilder();
        xrefBuilder.Append($"xref\n0 {objectCount + 1}\n");
        xrefBuilder.Append("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            xrefBuilder.Append($"{offsets[i]:D10} 00000 n \n");
        }

        xrefBuilder.Append(
            $"trailer<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        ms.Write(Encoding.ASCII.GetBytes(xrefBuilder.ToString()));
        return ms.ToArray();
    }

    private static DecodedPng? TryDecodePng(byte[] png)
    {
        try
        {
            if (png.Length < 24
                || png[0] != 0x89
                || png[1] != 0x50
                || png[2] != 0x4E
                || png[3] != 0x47)
            {
                return null;
            }

            var offset = 8;
            int width = 0, height = 0, bitDepth = 0, colorType = -1;
            using var idat = new MemoryStream();
            while (offset + 8 <= png.Length)
            {
                var length = ReadInt32BigEndian(png, offset);
                offset += 4;
                if (offset + 4 + length + 4 > png.Length)
                {
                    return null;
                }

                var type = Encoding.ASCII.GetString(png, offset, 4);
                offset += 4;
                var dataStart = offset;
                offset += length;
                offset += 4; // CRC
                if (type == "IHDR" && length >= 13)
                {
                    width = ReadInt32BigEndian(png, dataStart);
                    height = ReadInt32BigEndian(png, dataStart + 4);
                    bitDepth = png[dataStart + 8];
                    colorType = png[dataStart + 9];
                    if (png[dataStart + 12] != 0)
                    {
                        return null; // interlaced unsupported
                    }
                }
                else if (type == "IDAT")
                {
                    idat.Write(png, dataStart, length);
                }
                else if (type == "IEND")
                {
                    break;
                }
            }

            if (width <= 0 || height <= 0 || bitDepth != 8
                || colorType is not (2 or 6)
                || width > 1024 || height > 1024)
            {
                return null;
            }

            var compressed = idat.ToArray();
            if (compressed.Length < 2)
            {
                return null;
            }

            // Skip zlib header (2 bytes) and inflate.
            using var zlib = new MemoryStream(compressed, 2, compressed.Length - 2);
            using var deflate = new System.IO.Compression.DeflateStream(
                zlib,
                System.IO.Compression.CompressionMode.Decompress);
            using var raw = new MemoryStream();
            deflate.CopyTo(raw);
            var filtered = raw.ToArray();
            var bytesPerPixel = colorType == 6 ? 4 : 3;
            var stride = 1 + width * bytesPerPixel;
            if (filtered.Length < stride * height)
            {
                return null;
            }

            var rgb = new byte[width * height * 3];
            var prev = new byte[width * bytesPerPixel];
            var curr = new byte[width * bytesPerPixel];
            for (var y = 0; y < height; y++)
            {
                var rowStart = y * stride;
                var filter = filtered[rowStart];
                var src = filtered.AsSpan(rowStart + 1, width * bytesPerPixel);
                for (var i = 0; i < curr.Length; i++)
                {
                    var left = i >= bytesPerPixel ? curr[i - bytesPerPixel] : (byte)0;
                    var up = prev[i];
                    var rawByte = src[i];
                    curr[i] = filter switch
                    {
                        0 => rawByte,
                        1 => (byte)(rawByte + left),
                        2 => (byte)(rawByte + up),
                        3 => (byte)(rawByte + ((left + up) / 2)),
                        4 => (byte)(rawByte + Paeth(left, up, i >= bytesPerPixel ? prev[i - bytesPerPixel] : (byte)0)),
                        _ => rawByte,
                    };
                }

                for (var x = 0; x < width; x++)
                {
                    var srcIndex = x * bytesPerPixel;
                    var dstIndex = (y * width + x) * 3;
                    rgb[dstIndex] = curr[srcIndex];
                    rgb[dstIndex + 1] = curr[srcIndex + 1];
                    rgb[dstIndex + 2] = curr[srcIndex + 2];
                }

                (prev, curr) = (curr, prev);
            }

            return new DecodedPng(width, height, rgb);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    private static int ReadInt32BigEndian(byte[] data, int offset) =>
        (data[offset] << 24)
        | (data[offset + 1] << 16)
        | (data[offset + 2] << 8)
        | data[offset + 3];

    private sealed record DecodedPng(int Width, int Height, byte[] RgbBytes);
}
