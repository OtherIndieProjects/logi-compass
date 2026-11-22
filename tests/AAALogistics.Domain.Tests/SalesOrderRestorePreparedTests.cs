using AAALogistics.Domain.Sales;

namespace AAALogistics.Domain.Tests;

public sealed class SalesOrderRestorePreparedTests
{
    [Fact]
    public void RestorePreparedAfterReverse_From_Confirmed_After_Full_Delivery_Reverse()
    {
        var order = new SalesOrder(
            "SO-RESTORE",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            "EUR",
            "Calle 1",
            null,
            "Madrid",
            null,
            "28001",
            "ES",
            DateTimeOffset.UtcNow,
            null);
        var line = new SalesOrderLine(
            order.Id,
            Guid.CreateVersion7(),
            5m,
            1m,
            "EUR",
            "EA");
        order.Confirm([line], DateTimeOffset.UtcNow);
        line.ApplyDelivery(5m);
        order.RecalculateStatus([line]);
        Assert.Equal(SalesOrderStatus.Delivered, order.Status);

        line.ReverseDelivery(5m);
        order.RecalculateStatus([line]);
        Assert.Equal(SalesOrderStatus.Confirmed, order.Status);

        order.RestorePreparedAfterReverse();
        Assert.Equal(SalesOrderStatus.Prepared, order.Status);

        order.RestorePreparedAfterReverse();
        Assert.Equal(SalesOrderStatus.Prepared, order.Status);
    }

    [Fact]
    public void RestorePreparedAfterReverse_Rejects_Non_Confirmed()
    {
        var order = new SalesOrder(
            "SO-BAD",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            "EUR",
            "Calle 1",
            null,
            "Madrid",
            null,
            "28001",
            "ES",
            DateTimeOffset.UtcNow,
            null);
        var line = new SalesOrderLine(
            order.Id,
            Guid.CreateVersion7(),
            2m,
            1m,
            "EUR",
            "EA");
        order.Confirm([line], DateTimeOffset.UtcNow);
        order.StartPreparation();
        Assert.Throws<InvalidOperationException>(order.RestorePreparedAfterReverse);
    }
}
