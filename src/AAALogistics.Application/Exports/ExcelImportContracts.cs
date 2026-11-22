namespace AAALogistics.Application.Exports;

public sealed record ExcelImportPreviewRow(
    int RowNumber,
    string Code,
    string Description,
    string? UnitCode,
    decimal? Quantity,
    IReadOnlyCollection<string> Errors);

public sealed record ExcelImportPreview(
    string Kind,
    int TotalRows,
    int ValidRows,
    IReadOnlyCollection<ExcelImportPreviewRow> Rows);

public sealed record ExcelImportCommitResult(
    int CreatedOrUpdated,
    IReadOnlyCollection<string> Messages);

public interface IExcelImportService
{
    Task<ExcelImportPreview> PreviewMaterialsAsync(
        Stream workbook,
        CancellationToken cancellationToken = default);

    Task<ExcelImportCommitResult> CommitMaterialsAsync(
        Stream workbook,
        CancellationToken cancellationToken = default);

    Task<ExcelImportPreview> PreviewStockOpeningAsync(
        Stream workbook,
        CancellationToken cancellationToken = default);

    Task<ExcelImportCommitResult> CommitStockOpeningAsync(
        Stream workbook,
        string idempotencyPrefix,
        CancellationToken cancellationToken = default);
}
