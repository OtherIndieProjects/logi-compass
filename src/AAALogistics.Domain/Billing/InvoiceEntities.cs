using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Billing;

public enum InvoiceKind
{
    Ordinary = 0,
    Rectifying = 1,
}

public enum InvoiceStatus
{
    Issued = 0,
    Rectified = 1,
}

public sealed class InvoiceSeries : Entity
{
    private InvoiceSeries()
    {
    }

    public InvoiceSeries(string code, string name, int nextNumber = 1)
    {
        Code = UnitOfMeasure.NormalizeCode(code, 16);
        Name = UnitOfMeasure.Required(name, nameof(name), 100);
        NextNumber = nextNumber < 1 ? 1 : nextNumber;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int NextNumber { get; private set; }

    public int AllocateNext()
    {
        var current = NextNumber;
        NextNumber++;
        return current;
    }
}

public sealed class Invoice : Entity
{
    private Invoice()
    {
    }

    public Invoice(
        string seriesCode,
        int number,
        InvoiceKind kind,
        Guid customerId,
        Guid? salesOrderId,
        Guid? rectifiesInvoiceId,
        string customerTaxId,
        string customerName,
        string customerAddress,
        string currency,
        decimal netAmount,
        decimal taxRate,
        decimal taxAmount,
        decimal totalAmount,
        DateOnly issueDate,
        DateOnly operationDate,
        DateTimeOffset createdUtc,
        string sifHash,
        string? previousSifHash)
    {
        SeriesCode = UnitOfMeasure.NormalizeCode(seriesCode, 16);
        Number = number;
        Kind = kind;
        Status = InvoiceStatus.Issued;
        CustomerId = customerId;
        SalesOrderId = salesOrderId;
        RectifiesInvoiceId = rectifiesInvoiceId;
        CustomerTaxId = UnitOfMeasure.NormalizeCode(customerTaxId, 40);
        CustomerName = UnitOfMeasure.Required(customerName, nameof(customerName), 200);
        CustomerAddress = UnitOfMeasure.Required(customerAddress, nameof(customerAddress), 300);
        Currency = UnitOfMeasure.NormalizeCode(currency, 3);
        NetAmount = netAmount;
        TaxRate = taxRate;
        TaxAmount = taxAmount;
        TotalAmount = totalAmount;
        IssueDate = issueDate;
        OperationDate = operationDate;
        CreatedUtc = createdUtc;
        SifHash = UnitOfMeasure.Required(sifHash, nameof(sifHash), 128);
        PreviousSifHash = previousSifHash;
        DocumentNumber = $"{SeriesCode}-{Number:D8}";
    }

    public string SeriesCode { get; private set; } = string.Empty;

    public int Number { get; private set; }

    public string DocumentNumber { get; private set; } = string.Empty;

    public InvoiceKind Kind { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid? SalesOrderId { get; private set; }

    public Guid? RectifiesInvoiceId { get; private set; }

    public string CustomerTaxId { get; private set; } = string.Empty;

    public string CustomerName { get; private set; } = string.Empty;

    public string CustomerAddress { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public decimal NetAmount { get; private set; }

    public decimal TaxRate { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public DateOnly OperationDate { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public string SifHash { get; private set; } = string.Empty;

    public string? PreviousSifHash { get; private set; }

    public void MarkRectified()
    {
        if (Status == InvoiceStatus.Rectified)
        {
            throw new InvalidOperationException(
                "La factura ya está rectificada.");
        }

        if (Kind == InvoiceKind.Rectifying)
        {
            throw new InvalidOperationException(
                "Una factura rectificativa no se puede rectificar.");
        }

        Status = InvoiceStatus.Rectified;
    }
}

public sealed class InvoiceLine : Entity
{
    private InvoiceLine()
    {
    }

    public InvoiceLine(
        Guid invoiceId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        decimal netAmount,
        decimal taxAmount)
    {
        InvoiceId = invoiceId;
        Description = UnitOfMeasure.Required(description, nameof(description), 250);
        Quantity = quantity;
        UnitPrice = unitPrice;
        TaxRate = taxRate;
        NetAmount = netAmount;
        TaxAmount = taxAmount;
    }

    public Guid InvoiceId { get; private set; }

    public Invoice Invoice { get; private set; } = null!;

    public string Description { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal TaxRate { get; private set; }

    public decimal NetAmount { get; private set; }

    public decimal TaxAmount { get; private set; }
}

public sealed class IssuerFiscalProfile : Entity
{
    public const string DefaultLegalFooter =
        "Operación sujeta y no exenta de IVA. Factura emitida conforme al RD 1619/2012.";

    private IssuerFiscalProfile()
    {
    }

    public IssuerFiscalProfile(
        string legalName,
        string taxId,
        string addressLine,
        string city,
        string postalCode,
        string countryCode)
    {
        ApplyIdentity(legalName, taxId, addressLine, city, postalCode, countryCode);
        VerifactuMode = "LocalChain";
        LegalFooterText = DefaultLegalFooter;
        SignerTitle = "Administrador";
        PaymentDueDays = 30;
        IrpfPercent = 0m;
    }

    public string LegalName { get; private set; } = string.Empty;

    public string TaxId { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    public string? Email { get; private set; }

    public string? Website { get; private set; }

    public string? SignerName { get; private set; }

    public string? SignerTitle { get; private set; }

    public string? Iban { get; private set; }

    public string? BankName { get; private set; }

    public string? PaymentMethod { get; private set; }

    public int PaymentDueDays { get; private set; }

    public string? LegalFooterText { get; private set; }

    public decimal IrpfPercent { get; private set; }

    public string? SealPngRelativePath { get; private set; }

    public string? SignaturePngRelativePath { get; private set; }

    public string? LogoPngRelativePath { get; private set; }

    public string VerifactuMode { get; private set; } = "LocalChain";

    public void ApplyCompanyDetails(
        string legalName,
        string taxId,
        string addressLine,
        string city,
        string postalCode,
        string countryCode,
        string? phone,
        string? email,
        string? website,
        string? signerName,
        string? signerTitle,
        string? iban,
        string? bankName,
        string? paymentMethod,
        int paymentDueDays,
        string? legalFooterText,
        decimal irpfPercent)
    {
        ApplyIdentity(legalName, taxId, addressLine, city, postalCode, countryCode);
        Phone = Optional(phone, 40);
        Email = Optional(email, 120);
        Website = Optional(website, 120);
        SignerName = Optional(signerName, 120);
        SignerTitle = string.IsNullOrWhiteSpace(signerTitle)
            ? "Administrador"
            : Optional(signerTitle, 80);
        Iban = Optional(iban, 40);
        BankName = Optional(bankName, 120);
        PaymentMethod = Optional(paymentMethod, 120);
        PaymentDueDays = paymentDueDays < 0 ? 0 : paymentDueDays;
        LegalFooterText = string.IsNullOrWhiteSpace(legalFooterText)
            ? DefaultLegalFooter
            : UnitOfMeasure.Required(legalFooterText, nameof(legalFooterText), 800);
        if (irpfPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(irpfPercent),
                irpfPercent,
                "El IRPF debe estar entre 0 y 100.");
        }

        IrpfPercent = irpfPercent;
    }

    public void SetSealPath(string? relativePath) =>
        SealPngRelativePath = NormalizeSealPath(relativePath);

    public void SetSignaturePath(string? relativePath) =>
        SignaturePngRelativePath = NormalizeSealPath(relativePath);

    public void SetLogoPath(string? relativePath) =>
        LogoPngRelativePath = NormalizeSealPath(relativePath);

    public void SetVerifactuMode(string mode) =>
        VerifactuMode = UnitOfMeasure.Required(mode, nameof(mode), 40);

    private void ApplyIdentity(
        string legalName,
        string taxId,
        string addressLine,
        string city,
        string postalCode,
        string countryCode)
    {
        LegalName = UnitOfMeasure.Required(legalName, nameof(legalName), 200);
        TaxId = UnitOfMeasure.NormalizeCode(taxId, 40);
        AddressLine = UnitOfMeasure.Required(addressLine, nameof(addressLine), 200);
        City = UnitOfMeasure.Required(city, nameof(city), 100);
        PostalCode = UnitOfMeasure.Required(postalCode, nameof(postalCode), 20);
        CountryCode = UnitOfMeasure.NormalizeCode(countryCode, 2);
    }

    private static string? NormalizeSealPath(string? relativePath) =>
        string.IsNullOrWhiteSpace(relativePath) ? null : relativePath.Trim();

    private static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentException(
                $"El valor supera {maxLength} caracteres.",
                nameof(value));
    }
}
