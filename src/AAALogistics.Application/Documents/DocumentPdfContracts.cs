namespace AAALogistics.Application.Documents;

public interface IDocumentPdfService
{
    Task<byte[]> RenderPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderDeliveryAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderGoodsReceiptAsync(
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderOtifReportAsync(
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderOrdersByStatusReportAsync(
        CancellationToken cancellationToken = default);

    Task<byte[]> RenderStockReportAsync(
        CancellationToken cancellationToken = default);
}
