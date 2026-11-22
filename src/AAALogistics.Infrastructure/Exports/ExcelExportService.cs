using AAALogistics.Application.Exports;
using AAALogistics.Application.Logistics;
using ClosedXML.Excel;

namespace AAALogistics.Infrastructure.Exports;

public sealed class ExcelExportService : IExcelExportService
{
    private static readonly string[] MaterialHeaders =
    [
        "Id",
        "Código",
        "Nombre",
        "Descripción",
        "Id unidad",
        "Precio catálogo",
        "Moneda",
        "Activo",
        "Id externo",
    ];

    private static readonly string[] StockHeaders =
    [
        "Id material",
        "Código material",
        "Id almacén",
        "Código almacén",
        "Cantidad",
        "Cantidad mínima",
    ];

    public byte[] CreateMaterialsWorkbook(
        IReadOnlyCollection<MaterialView> materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Materiales");
        WriteHeaders(sheet, MaterialHeaders);
        var row = 2;
        foreach (var material in materials.OrderBy(
                     value => value.Code,
                     StringComparer.Ordinal))
        {
            WriteText(sheet.Cell(row, 1), material.Id.ToString("D"));
            WriteText(sheet.Cell(row, 2), material.Code);
            WriteText(sheet.Cell(row, 3), material.DisplayName);
            WriteText(sheet.Cell(row, 4), material.Description);
            WriteText(sheet.Cell(row, 5), material.UnitId.ToString("D"));
            sheet.Cell(row, 6).Value = material.CatalogPrice;
            WriteText(sheet.Cell(row, 7), material.Currency);
            sheet.Cell(row, 8).Value = material.IsActive;
            WriteText(sheet.Cell(row, 9), material.ExternalId);
            row++;
        }

        sheet.Columns().AdjustToContents();
        return Save(workbook);
    }

    public byte[] CreateStockWorkbook(IReadOnlyCollection<StockView> stock)
    {
        ArgumentNullException.ThrowIfNull(stock);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Stock");
        WriteHeaders(sheet, StockHeaders);
        var row = 2;
        foreach (var balance in stock.OrderBy(value => value.MaterialCode, StringComparer.Ordinal)
                     .ThenBy(value => value.WarehouseCode, StringComparer.Ordinal))
        {
            WriteText(sheet.Cell(row, 1), balance.MaterialId.ToString("D"));
            WriteText(sheet.Cell(row, 2), balance.MaterialCode);
            WriteText(sheet.Cell(row, 3), balance.WarehouseId.ToString("D"));
            WriteText(sheet.Cell(row, 4), balance.WarehouseCode);
            sheet.Cell(row, 5).Value = balance.Quantity;
            sheet.Cell(row, 6).Value = balance.MinQuantity;
            row++;
        }

        sheet.Columns().AdjustToContents();
        return Save(workbook);
    }

    private static void WriteHeaders(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            var cell = sheet.Cell(1, index + 1);
            WriteText(cell, headers[index]);
            cell.Style.Font.Bold = true;
        }
    }

    private static void WriteText(IXLCell cell, string? value)
    {
        cell.Value = ExcelCellText.Neutralize(value);
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
