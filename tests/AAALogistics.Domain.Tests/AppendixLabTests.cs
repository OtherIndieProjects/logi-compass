using AAALogistics.Domain.Appendix;

namespace AAALogistics.Domain.Tests;

public sealed class AppendixLabTests
{
    [Fact]
    public void UnitConversion_Rounds_AwayFromZero_At_Target_Scale()
    {
        var from = Guid.CreateVersion7();
        var to = Guid.CreateVersion7();
        var conversion = new UnitConversion(from, to, 2.5m);
        var result = conversion.Convert(1.25m, targetScale: 2);
        Assert.Equal(3.13m, result);
    }

    [Fact]
    public void TransportRoute_Computes_Base_Plus_Distance()
    {
        var route = new TransportRoute("R1", "Norte", null, 100m, 50m, 0.8m);
        Assert.Equal(130m, route.ComputeTariff());
    }

    [Fact]
    public void Wave_Release_Only_From_Draft()
    {
        var wave = new Wave("WV-1", 1, DateTimeOffset.UtcNow);
        wave.Release();
        Assert.Equal(WaveStatus.Released, wave.Status);
        Assert.Throws<InvalidOperationException>(() => wave.Release());
    }

    [Fact]
    public void PurchaseRequisition_Cancel_From_Open()
    {
        var pr = new PurchaseRequisition(
            "PR-1",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            5m,
            PurchaseRequisitionSource.Minimum,
            DateTimeOffset.UtcNow);
        pr.Cancel();
        Assert.Equal(PurchaseRequisitionStatus.Cancelled, pr.Status);
    }
}
