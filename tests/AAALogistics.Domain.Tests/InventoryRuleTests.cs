using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Inventory;
using AAALogistics.Domain.Purchasing;
using AAALogistics.Domain.Sales;

namespace AAALogistics.Domain.Tests;

public sealed class InventoryRuleTests
{
    [Fact]
    public void Lot_Requires_Material_And_Code()
    {
        Assert.Throws<ArgumentException>(
            () => new Lot(Guid.Empty, "L1", null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(
            () => new Lot(Guid.CreateVersion7(), " ", null, DateTimeOffset.UtcNow));

        var lot = new Lot(
            Guid.CreateVersion7(),
            "lot-a",
            new DateOnly(2027, 1, 15),
            DateTimeOffset.UtcNow);
        Assert.Equal("LOT-A", lot.Code);
        Assert.Equal(new DateOnly(2027, 1, 15), lot.ExpiryDate);
    }

    [Fact]
    public void Material_RequiresLot_Defaults_False_And_Can_Be_Set()
    {
        var material = new Material(
            "MAT-LOT",
            "Con lote",
            Guid.CreateVersion7(),
            1m,
            "EUR");
        Assert.False(material.RequiresLot);

        material.SetRequiresLot(true);
        Assert.True(material.RequiresLot);

        var withLot = new Material(
            "MAT-LOT2",
            "Con lote",
            Guid.CreateVersion7(),
            1m,
            "EUR",
            requiresLot: true);
        Assert.True(withLot.RequiresLot);
    }

    [Fact]
    public void Purchase_Order_Open_Quantity_Is_Derived()
    {
        var line = new PurchaseOrderLine(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            10m,
            3.25m,
            "EUR",
            "EA");

        line.ApplyReceipt(4m);

        Assert.Equal(6m, line.OpenQuantity);
        Assert.Equal(4m, line.PostedQuantity);
    }

    [Fact]
    public void Stock_Reservation_Consume_And_Release()
    {
        var reservation = new StockReservation(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            10m,
            DateTimeOffset.UtcNow);

        reservation.Consume(4m);
        Assert.True(reservation.IsActive);
        Assert.Equal(6m, reservation.RemainingQuantity);

        reservation.Consume(6m);
        Assert.False(reservation.IsActive);
        Assert.Equal(0m, reservation.RemainingQuantity);

        var open = new StockReservation(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            5m,
            DateTimeOffset.UtcNow);
        open.Release(DateTimeOffset.UtcNow);
        Assert.False(open.IsActive);
        Assert.NotNull(open.ReleasedUtc);
        Assert.Equal(5m, open.RemainingQuantity);
    }

    [Fact]
    public void ReturnIn_Must_Be_Positive_And_ReturnOut_Negative()
    {
        var materialId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var locationId = Guid.CreateVersion7();
        var inbound = StockMovement.Create(
            StockMovementType.ReturnIn,
            materialId,
            warehouseId,
            locationId,
            3m,
            "EA",
            "SalesReturn",
            Guid.CreateVersion7(),
            null,
            null,
            DateTimeOffset.UtcNow,
            Guid.CreateVersion7(),
            "devolución");
        Assert.Equal(3m, inbound.SignedQuantity);

        var outbound = StockMovement.Create(
            StockMovementType.ReturnOut,
            materialId,
            warehouseId,
            locationId,
            -2m,
            "EA",
            "SalesReturn",
            Guid.CreateVersion7(),
            null,
            null,
            DateTimeOffset.UtcNow,
            Guid.CreateVersion7(),
            "salida devolución");
        Assert.Equal(-2m, outbound.SignedQuantity);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => StockMovement.Create(
                StockMovementType.ReturnIn,
                materialId,
                warehouseId,
                locationId,
                -1m,
                "EA",
                "SalesReturn",
                Guid.CreateVersion7(),
                null,
                null,
                DateTimeOffset.UtcNow,
                Guid.CreateVersion7(),
                "bad"));
    }

    [Fact]
    public void PrepPick_Confirm_Cannot_Go_Below_Consumed_And_Restore_Is_Symmetric()
    {
        var pick = new PrepPickLine(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            10m);
        pick.Confirm(10m, DateTimeOffset.UtcNow);
        pick.Consume(4m);
        Assert.Equal(6m, pick.RemainingConfirmed);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => pick.Confirm(3m, DateTimeOffset.UtcNow));

        pick.RestoreConsume(4m);
        Assert.Equal(0m, pick.ConsumedQuantity);
        Assert.Equal(10m, pick.RemainingConfirmed);
    }

    [Fact]
    public void Stock_Reservation_RestoreConsume_Reactivates_When_Not_Released()
    {
        var reservation = new StockReservation(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            8m,
            DateTimeOffset.UtcNow);
        reservation.Consume(8m);
        Assert.False(reservation.IsActive);

        reservation.RestoreConsume(3m);
        Assert.True(reservation.IsActive);
        Assert.Equal(3m, reservation.RemainingQuantity);
    }

    [Fact]
    public void Movement_Cannot_Be_Reversed_Twice()
    {
        var original = StockMovement.Create(
            StockMovementType.Receipt,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            5m,
            "EA",
            "GoodsReceipt",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            DateTimeOffset.UtcNow,
            Guid.CreateVersion7());

        var first = StockMovement.CreateReversal(
            original,
            alreadyReversed: false,
            DateTimeOffset.UtcNow,
            Guid.CreateVersion7(),
            "Corrección");

        Assert.Equal(original.Id, first.ReversedMovementId);
        Assert.Throws<InvalidOperationException>(
            () => StockMovement.CreateReversal(
                original,
                alreadyReversed: true,
                DateTimeOffset.UtcNow,
                Guid.CreateVersion7(),
                "Segundo intento"));
    }
}
