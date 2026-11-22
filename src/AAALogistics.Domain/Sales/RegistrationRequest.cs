using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Sales;

public enum RegistrationRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Expired = 3,
}

public sealed class RegistrationRequest : Entity, IPersonDataEntity
{
    private RegistrationRequest()
    {
    }

    public RegistrationRequest(
        string companyName,
        string taxId,
        string contactName,
        string email,
        string userName,
        string addressLine1,
        string city,
        string postalCode,
        string countryCode,
        DateTimeOffset createdUtc)
    {
        CompanyName = UnitOfMeasure.Required(
            companyName,
            nameof(companyName),
            200);
        TaxId = UnitOfMeasure.NormalizeCode(taxId, 40);
        ContactName = UnitOfMeasure.Required(
            contactName,
            nameof(contactName),
            150);
        Email = NormalizeEmail(email);
        UserName = NormalizeUserName(userName);
        AddressLine1 = UnitOfMeasure.Required(
            addressLine1,
            nameof(addressLine1),
            200);
        City = UnitOfMeasure.Required(city, nameof(city), 100);
        PostalCode = UnitOfMeasure.Required(
            postalCode,
            nameof(postalCode),
            30);
        CountryCode = UnitOfMeasure.NormalizeCode(countryCode, 2);
        if (CountryCode.Length != 2)
        {
            throw new ArgumentException(
                "El país debe usar un código ISO de dos letras.",
                nameof(countryCode));
        }

        CreatedUtc = createdUtc;
        Status = RegistrationRequestStatus.Pending;
    }

    public string CompanyName { get; private set; } = string.Empty;

    public string TaxId { get; private set; } = string.Empty;

    public string ContactName { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string UserName { get; private set; } = string.Empty;

    public string AddressLine1 { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string CountryCode { get; private set; } = string.Empty;

    public RegistrationRequestStatus Status { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset? ReviewedUtc { get; private set; }

    public Guid? CustomerId { get; private set; }

    public string? RejectionReason { get; private set; }

    public string? LegalVersion { get; private set; }

    Guid IPersonDataEntity.PersonId => Id;

    public void RecordLegalVersion(string version) =>
        LegalVersion = UnitOfMeasure.Required(version, nameof(version), 64);

    public void Approve(Guid customerId, DateTimeOffset reviewedUtc)
    {
        EnsurePending();
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException(
                "El cliente aprobado es obligatorio.",
                nameof(customerId));
        }

        CustomerId = customerId;
        Status = RegistrationRequestStatus.Approved;
        ReviewedUtc = reviewedUtc;
    }

    public void Reject(string reason, DateTimeOffset reviewedUtc)
    {
        EnsurePending();
        RejectionReason = UnitOfMeasure.Required(reason, nameof(reason), 500);
        Status = RegistrationRequestStatus.Rejected;
        ReviewedUtc = reviewedUtc;
    }

    public void Expire(DateTimeOffset reviewedUtc)
    {
        EnsurePending();
        Status = RegistrationRequestStatus.Expired;
        ReviewedUtc = reviewedUtc;
    }

    private void EnsurePending()
    {
        if (Status != RegistrationRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                "La solicitud ya no está pendiente.");
        }
    }

    private static string NormalizeEmail(string value)
    {
        var email = UnitOfMeasure.Required(value, nameof(value), 200);
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0
            || at != email.LastIndexOf('@')
            || at == email.Length - 1)
        {
            throw new ArgumentException(
                "El correo no es válido.",
                nameof(value));
        }

        return email;
    }

    private static string NormalizeUserName(string value)
    {
        var userName = UnitOfMeasure.Required(value, nameof(value), 64);
        if (userName.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "El usuario no puede contener espacios.",
                nameof(value));
        }

        return userName;
    }
}
