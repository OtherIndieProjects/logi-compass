using AAALogistics.Application.Logistics;

namespace AAALogistics.Application.Exports;

public interface IExcelExportService
{
    byte[] CreateMaterialsWorkbook(
        IReadOnlyCollection<MaterialView> materials);

    byte[] CreateStockWorkbook(IReadOnlyCollection<StockView> stock);
}
