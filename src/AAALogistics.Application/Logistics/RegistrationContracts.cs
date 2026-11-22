namespace AAALogistics.Application.Logistics;

public sealed record SubmitRegistrationCommand(
    string CompanyName,
    string TaxId,
    string ContactName,
    string Email,
    string UserName,
    string AddressLine1,
    string City,
    string PostalCode,
    string CountryCode,
    bool AcceptTerms);

public sealed record RegistrationRequestView(
    Guid Id,
    string Status,
    DateTimeOffset CreatedUtc);

public sealed record RegistrationRequestDetails(
    Guid Id,
    string CompanyName,
    string TaxId,
    string ContactName,
    string Email,
    string UserName,
    string Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ReviewedUtc,
    Guid? CustomerId,
    string? RejectionReason);

public sealed record RegistrationDecision(
    Guid RequestId,
    string Status,
    Guid? CustomerId,
    Guid? UserId,
    string? TemporaryPassword);

public interface IRegistrationService
{
    Task<RegistrationRequestView> SubmitAsync(
        SubmitRegistrationCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<RegistrationRequestDetails>> ListAsync(
        string? status,
        CancellationToken cancellationToken = default);

    Task<RegistrationDecision> ApproveAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<RegistrationDecision> RejectAsync(
        Guid requestId,
        string reason,
        CancellationToken cancellationToken = default);
}
