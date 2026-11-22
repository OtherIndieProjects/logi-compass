using AAALogistics.Domain.Billing;

namespace AAALogistics.Domain.Tests;

public sealed class InvoiceFiscalTests
{
    [Fact]
    public void MarkRectified_rejects_second_call()
    {
        var invoice = CreateOrdinary();
        invoice.MarkRectified();
        var exception = Assert.Throws<InvalidOperationException>(invoice.MarkRectified);
        Assert.Contains("ya está rectificada", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkRectified_rejects_rectifying_kind()
    {
        var invoice = new Invoice(
            "RA",
            1,
            InvoiceKind.Rectifying,
            Guid.CreateVersion7(),
            null,
            Guid.CreateVersion7(),
            "B12345678",
            "Cliente",
            "Calle 1, 28001 Madrid",
            "EUR",
            -100m,
            21m,
            -21m,
            -121m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTimeOffset.UtcNow,
            "abc",
            null);
        var exception = Assert.Throws<InvalidOperationException>(invoice.MarkRectified);
        Assert.Contains("rectificativa", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Invoice CreateOrdinary() =>
        new(
            "A",
            1,
            InvoiceKind.Ordinary,
            Guid.CreateVersion7(),
            null,
            null,
            "B12345678",
            "Cliente",
            "Calle 1, 28001 Madrid",
            "EUR",
            100m,
            21m,
            21m,
            121m,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateTimeOffset.UtcNow,
            "sif-hash",
            null);
}
