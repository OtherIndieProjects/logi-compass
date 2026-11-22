using AAALogistics.Domain.Industrial;

namespace AAALogistics.Domain.Tests;

public sealed class IndustrialPlatformTests
{
    [Fact]
    public void Credit_Blocks_When_Receivables_Exceed_Limit()
    {
        var terms = new CustomerCommercialTerms(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            paymentTermDays: 30,
            creditLimit: 1000m,
            currency: "EUR");
        terms.SetOpenReceivables(1000m);
        Assert.False(terms.IsCreditBlocked); // igual al límite aún no bloquea
        Assert.True(terms.WouldExceedCredit(1m));
        terms.SetOpenReceivables(1001m);
        Assert.True(terms.IsCreditBlocked);
    }

    [Fact]
    public void ExchangeRate_Converts_With_AwayFromZero()
    {
        var rate = new ExchangeRate("USD", "EUR", 1.105m, DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Equal(110.50m, rate.Convert(100m));
    }
}
