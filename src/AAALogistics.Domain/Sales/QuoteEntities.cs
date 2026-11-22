using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Sales;

public enum QuoteStatus
{
    Draft = 0,
    Sent = 1,
    Accepted = 2,
    Rejected = 3,
    Expired = 4,
}

public sealed class Quote : Entity, IPersonDataEntity
{
    private Quote()
    {
    }

    public Quote(
        string number,
        Guid customerId,
        Guid defaultWarehouseId,
        DateOnly issueDate,
        DateOnly validUntil,
        string currency,
        decimal taxRate,
        DateTimeOffset createdUtc)
    {
        Number = UnitOfMeasure.Required(number, nameof(number), 40);
        CustomerId = customerId;
        DefaultWarehouseId = defaultWarehouseId;
        IssueDate = issueDate;
        ValidUntil = validUntil;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        TaxRate = taxRate < 0 ? 0 : taxRate;
        CreatedUtc = createdUtc;
        Status = QuoteStatus.Draft;
        RecalculateTotals([]);
    }

    public string Number { get; private set; } = string.Empty;

    public Guid CustomerId { get; private set; }

    public Guid DefaultWarehouseId { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public DateOnly ValidUntil { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal TaxRate { get; private set; }

    public decimal NetAmount { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public QuoteStatus Status { get; private set; }

    public Guid? SalesOrderId { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    Guid IPersonDataEntity.PersonId => CustomerId;

    public void SetNotes(string? notes) =>
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public void RecalculateTotals(IEnumerable<QuoteLine> lines)
    {
        var snapshot = lines.ToArray();
        NetAmount = snapshot.Sum(line => line.NetAmount);
        TaxAmount = Math.Round(NetAmount * TaxRate / 100m, 2, MidpointRounding.AwayFromZero);
        TotalAmount = NetAmount + TaxAmount;
    }

    public void MarkSent()
    {
        EnsureMutable();
        Status = QuoteStatus.Sent;
    }

    public void MarkRejected()
    {
        EnsureMutable();
        Status = QuoteStatus.Rejected;
    }

    public void MarkExpired()
    {
        if (Status is QuoteStatus.Accepted or QuoteStatus.Rejected)
        {
            return;
        }

        Status = QuoteStatus.Expired;
    }

    public void MarkAccepted(Guid salesOrderId)
    {
        EnsureMutable();
        if (salesOrderId == Guid.Empty)
        {
            throw new ArgumentException("El pedido de venta es obligatorio.", nameof(salesOrderId));
        }

        Status = QuoteStatus.Accepted;
        SalesOrderId = salesOrderId;
    }

    private void EnsureMutable()
    {
        if (Status is QuoteStatus.Accepted or QuoteStatus.Rejected or QuoteStatus.Expired)
        {
            throw new InvalidOperationException(
                "El presupuesto ya está cerrado y no admite ese cambio.");
        }
    }
}

public sealed class QuoteLine : Entity
{
    private QuoteLine()
    {
    }

    public QuoteLine(
        Guid quoteId,
        Guid materialId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal taxRate)
    {
        if (quoteId == Guid.Empty || materialId == Guid.Empty)
        {
            throw new ArgumentException("Presupuesto y material son obligatorios.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser positiva.");
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "El precio no puede ser negativo.");
        }

        QuoteId = quoteId;
        MaterialId = materialId;
        Description = UnitOfMeasure.Required(description, nameof(description), 250);
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate < 0 ? 0 : taxRate;
        NetAmount = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
    }

    public Guid QuoteId { get; private set; }

    public Quote Quote { get; private set; } = null!;

    public Guid MaterialId { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal TaxRate { get; private set; }

    public decimal NetAmount { get; private set; }
}
