using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Common;

namespace AAALogistics.Domain.Sales;

public sealed class LegalAcceptance : Entity, IPersonDataEntity
{
    private LegalAcceptance()
    {
    }

    public LegalAcceptance(
        string version,
        DateTimeOffset acceptedUtc,
        string? ip,
        Guid? registrationRequestId,
        Guid? customerId)
    {
        Version = UnitOfMeasure.Required(version, nameof(version), 64);
        AcceptedUtc = acceptedUtc;
        if (string.IsNullOrWhiteSpace(ip))
        {
            Ip = null;
        }
        else
        {
            var normalized = ip.Trim();
            if (normalized.Length > 64)
            {
                throw new ArgumentException(
                    "La dirección IP supera 64 caracteres.",
                    nameof(ip));
            }

            Ip = normalized;
        }

        RegistrationRequestId = registrationRequestId;
        CustomerId = customerId;
    }

    public string Version { get; private set; } = string.Empty;

    public DateTimeOffset AcceptedUtc { get; private set; }

    public string? Ip { get; private set; }

    public Guid? RegistrationRequestId { get; private set; }

    public Guid? CustomerId { get; private set; }

    Guid IPersonDataEntity.PersonId =>
        CustomerId ?? RegistrationRequestId ?? Id;

    public void AssignCustomer(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException(
                "El identificador de cliente no es válido.",
                nameof(customerId));
        }

        if (CustomerId is { } existing && existing != customerId)
        {
            throw new InvalidOperationException(
                "La aceptación legal ya está vinculada a otro cliente.");
        }

        CustomerId = customerId;
    }
}
