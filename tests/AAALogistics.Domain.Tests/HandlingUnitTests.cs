using AAALogistics.Domain.Documents;

namespace AAALogistics.Domain.Tests;

public sealed class HandlingUnitTests
{
    [Fact]
    public void Creates_With_Unique_Sscc_And_Package_Count()
    {
        var orderId = Guid.CreateVersion7();
        var unit = new HandlingUnit(
            "003123456789012345",
            orderId,
            null,
            4,
            DateTimeOffset.UtcNow);

        Assert.Equal("003123456789012345", unit.Sscc);
        Assert.Equal(orderId, unit.SalesOrderId);
        Assert.Null(unit.DeliveryId);
        Assert.Equal(4, unit.PackageCount);
    }

    [Fact]
    public void Rejects_Empty_Order_Or_Zero_Packages()
    {
        Assert.Throws<ArgumentException>(
            () => new HandlingUnit(
                "003123456789012345",
                Guid.Empty,
                null,
                1,
                DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HandlingUnit(
                "003123456789012345",
                Guid.CreateVersion7(),
                null,
                0,
                DateTimeOffset.UtcNow));
    }
}
