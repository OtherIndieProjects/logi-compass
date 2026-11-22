using System.IO.Compression;
using AAALogistics.Application.Exports;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Inventory;
using AAALogistics.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Exports;

public sealed class ExcelImportService(
    ApplicationDbContext dbContext,
    ILogisticsService logistics,
    ICustomerRlsSession rlsSession) : IExcelImportService
{
    private const long MaxWorkbookBytes = 5L * 1024 * 1024;
    private const long MaxUncompressedBytes = 100L * 1024 * 1024;
    private const int MaxImportRows = 10_000;

    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly ILogisticsService _logistics =
        logistics ?? throw new ArgumentNullException(nameof(logistics));

    private readonly ICustomerRlsSession _rlsSession =
        rlsSession ?? throw new ArgumentNullException(nameof(rlsSession));

    public Task<ExcelImportPreview> PreviewMaterialsAsync(
        Stream workbook,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(workbook, "materials", cancellationToken);

    public async Task<ExcelImportCommitResult> CommitMaterialsAsync(
        Stream workbook,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewMaterialsAsync(workbook, cancellationToken);
        if (preview.ValidRows == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IMPORT-0001",
                "No hay filas válidas para importar.");
        }

        var created = 0;
        var messages = new List<string>();
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        foreach (var row in preview.Rows.Where(value => value.Errors.Count == 0))
        {
            if (await _dbContext.Materials.AnyAsync(
                    value => value.Code == row.Code,
                    cancellationToken))
            {
                messages.Add($"Fila {row.RowNumber}: material {row.Code} ya existe.");
                continue;
            }

            var unit = await _dbContext.UnitsOfMeasure.SingleOrDefaultAsync(
                value => value.Code == (row.UnitCode ?? "EA"),
                cancellationToken);
            if (unit is null)
            {
                messages.Add($"Fila {row.RowNumber}: unidad {row.UnitCode} no existe.");
                continue;
            }

            _dbContext.Materials.Add(
                new Material(
                    row.Code,
                    row.Description,
                    unit.Id,
                    0m,
                    "EUR"));
            created++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ExcelImportCommitResult(created, messages);
    }

    public Task<ExcelImportPreview> PreviewStockOpeningAsync(
        Stream workbook,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(workbook, "stock-opening", cancellationToken);

    public async Task<ExcelImportCommitResult> CommitStockOpeningAsync(
        Stream workbook,
        string idempotencyPrefix,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewStockOpeningAsync(workbook, cancellationToken);
        if (preview.ValidRows == 0)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IMPORT-0002",
                "No hay filas válidas de apertura.");
        }

        var created = 0;
        var messages = new List<string>();
        foreach (var row in preview.Rows.Where(value => value.Errors.Count == 0))
        {
            var material = await _dbContext.Materials.AsNoTracking()
                .SingleOrDefaultAsync(value => value.Code == row.Code, cancellationToken);
            var warehouse = await _dbContext.Warehouses.AsNoTracking()
                .SingleOrDefaultAsync(
                    value => value.Code == (row.UnitCode ?? "MAIN"),
                    cancellationToken);
            if (material is null || warehouse is null || row.Quantity is null)
            {
                messages.Add($"Fila {row.RowNumber}: material/almacén no encontrados.");
                continue;
            }

            try
            {
                await _logistics.PostAdjustmentAsync(
                    new PostAdjustmentCommand(
                        $"{idempotencyPrefix}:{row.RowNumber}",
                        material.Id,
                        warehouse.Id,
                        row.Quantity.Value,
                        StockMovementType.Opening,
                        "Importación de apertura Excel"),
                    cancellationToken);
                created++;
            }
            catch (LogisticsException exception)
            {
                messages.Add($"Fila {row.RowNumber}: {exception.Message}");
            }
        }

        return new ExcelImportCommitResult(created, messages);
    }

    private static Task<ExcelImportPreview> PreviewAsync(
        Stream workbook,
        string kind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSafeWorkbook(workbook);
        using var xl = new XLWorkbook(workbook);
        var sheet = xl.Worksheets.First();
        var rows = new List<ExcelImportPreviewRow>();
        var last = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (last - 1 > MaxImportRows)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IMPORT-0006",
                $"El archivo tiene más de {MaxImportRows:N0} filas. Divídalo en varios archivos.");
        }

        for (var rowNumber = 2; rowNumber <= last; rowNumber++)
        {
            var code = sheet.Cell(rowNumber, 1).GetString().Trim();
            var description = sheet.Cell(rowNumber, 2).GetString().Trim();
            var unitOrWarehouse = sheet.Cell(rowNumber, 3).GetString().Trim();
            decimal? quantity = null;
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add("Código obligatorio.");
            }

            if (kind == "materials" && string.IsNullOrWhiteSpace(description))
            {
                errors.Add("Descripción obligatoria.");
            }

            if (kind == "stock-opening")
            {
                if (!decimal.TryParse(
                        sheet.Cell(rowNumber, 4).GetString(),
                        out var parsed)
                    || parsed <= 0)
                {
                    errors.Add("Cantidad de apertura inválida.");
                }
                else
                {
                    quantity = parsed;
                }
            }

            rows.Add(
                new ExcelImportPreviewRow(
                    rowNumber,
                    code,
                    description,
                    string.IsNullOrWhiteSpace(unitOrWarehouse) ? null : unitOrWarehouse,
                    quantity,
                    errors));
        }

        return Task.FromResult(
            new ExcelImportPreview(
                kind,
                rows.Count,
                rows.Count(value => value.Errors.Count == 0),
                rows));
    }

    private static void EnsureSafeWorkbook(Stream workbook)
    {
        if (!workbook.CanSeek)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IMPORT-0005",
                "El archivo no es un libro Excel (.xlsx) válido.");
        }

        if (workbook.Length > MaxWorkbookBytes)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IMPORT-0003",
                "El archivo Excel supera 5 MB.");
        }

        try
        {
            using var archive = new ZipArchive(workbook, ZipArchiveMode.Read, leaveOpen: true);
            long uncompressed = 0;
            foreach (var entry in archive.Entries)
            {
                uncompressed += entry.Length;
                if (uncompressed > MaxUncompressedBytes)
                {
                    throw new LogisticsException(
                        LogisticsErrorKind.Validation,
                        "AAA-IMPORT-0004",
                        "El contenido descomprimido del Excel es demasiado grande.");
                }
            }
        }
        catch (InvalidDataException)
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-IMPORT-0005",
                "El archivo no es un libro Excel (.xlsx) válido.");
        }

        workbook.Position = 0;
    }
}
