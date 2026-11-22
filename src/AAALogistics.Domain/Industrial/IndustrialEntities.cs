using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Industrial;

public sealed class Company : Entity
{
    private Company()
    {
    }

    public Company(string code, string legalName, string taxId, string currency)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 30);
        LegalName = UnitOfMeasure.Required(legalName, nameof(legalName), 200);
        TaxId = UnitOfMeasure.NormalizeCode(taxId, 40);
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string LegalName { get; private set; } = string.Empty;

    public string TaxId { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void Update(string legalName, string currency)
    {
        LegalName = UnitOfMeasure.Required(legalName, nameof(legalName), 200);
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
    }
}

public sealed class ExchangeRate : Entity
{
    private ExchangeRate()
    {
    }

    public ExchangeRate(
        string fromCurrency,
        string toCurrency,
        decimal rate,
        DateOnly validOn)
    {
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate));
        }

        FromCurrency = UnitOfMeasure.NormalizeCode(fromCurrency, 3);
        ToCurrency = UnitOfMeasure.NormalizeCode(toCurrency, 3);
        Rate = rate;
        ValidOn = validOn;
    }

    public string FromCurrency { get; private set; } = string.Empty;

    public string ToCurrency { get; private set; } = string.Empty;

    public decimal Rate { get; private set; }

    public DateOnly ValidOn { get; private set; }

    public decimal Convert(decimal amount, int scale = 2) =>
        Math.Round(amount * Rate, scale, MidpointRounding.AwayFromZero);

    public void UpdateRate(decimal rate)
    {
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate));
        }

        Rate = rate;
    }
}

public sealed class CustomerCommercialTerms : Entity
{
    private CustomerCommercialTerms()
    {
    }

    public CustomerCommercialTerms(
        Guid customerId,
        Guid companyId,
        int paymentTermDays,
        decimal creditLimit,
        string currency)
    {
        CustomerId = customerId;
        CompanyId = companyId;
        PaymentTermDays = paymentTermDays < 0
            ? throw new ArgumentOutOfRangeException(nameof(paymentTermDays))
            : paymentTermDays;
        CreditLimit = creditLimit < 0
            ? throw new ArgumentOutOfRangeException(nameof(creditLimit))
            : creditLimit;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        OpenReceivables = 0m;
        IsCreditBlocked = false;
    }

    public Guid CustomerId { get; private set; }

    public Guid CompanyId { get; private set; }

    public int PaymentTermDays { get; private set; }

    public decimal CreditLimit { get; private set; }

    public decimal OpenReceivables { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public bool IsCreditBlocked { get; private set; }

    public void Update(int paymentTermDays, decimal creditLimit, string currency)
    {
        PaymentTermDays = paymentTermDays < 0
            ? throw new ArgumentOutOfRangeException(nameof(paymentTermDays))
            : paymentTermDays;
        CreditLimit = creditLimit < 0
            ? throw new ArgumentOutOfRangeException(nameof(creditLimit))
            : creditLimit;
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        RecalculateBlock();
    }

    public void SetOpenReceivables(decimal amount)
    {
        OpenReceivables = amount < 0
            ? throw new ArgumentOutOfRangeException(nameof(amount))
            : amount;
        RecalculateBlock();
    }

    public bool WouldExceedCredit(decimal additionalAmount) =>
        CreditLimit > 0 && (OpenReceivables + additionalAmount) > CreditLimit;

    private void RecalculateBlock() =>
        IsCreditBlocked = CreditLimit > 0 && OpenReceivables > CreditLimit;
}
