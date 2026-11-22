using System.Security.Cryptography;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Catalog;
using AAALogistics.Domain.Sales;
using AAALogistics.Domain.Security;
using AAALogistics.Infrastructure.Configuration;
using AAALogistics.Infrastructure.Identity;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Logistics;

public sealed class RegistrationService(
    ApplicationDbContext dbContext,
    IClock clock,
    ICustomerRlsSession rlsSession,
    IRequestContext requestContext,
    UserManager<ApplicationUser> userManager,
    IOptions<LegalOptions> legalOptions) : IRegistrationService
{
    private readonly ApplicationDbContext _dbContext =
        dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    private readonly ICustomerRlsSession _rlsSession =
        rlsSession ?? throw new ArgumentNullException(nameof(rlsSession));

    private readonly IRequestContext _requestContext =
        requestContext ?? throw new ArgumentNullException(nameof(requestContext));

    private readonly UserManager<ApplicationUser> _userManager =
        userManager ?? throw new ArgumentNullException(nameof(userManager));

    private readonly LegalOptions _legalOptions =
        legalOptions?.Value ?? throw new ArgumentNullException(nameof(legalOptions));

    public async Task<RegistrationRequestView> SubmitAsync(
        SubmitRegistrationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.AcceptTerms)
        {
            throw Validation(
                "AAA-PORTAL-0004",
                "Hay que aceptar los textos legales para enviar la solicitud.");
        }

        if (_legalOptions.RequireLegalTexts
            && string.IsNullOrWhiteSpace(_legalOptions.CurrentVersion))
        {
            throw Validation(
                "AAA-PORTAL-0003",
                "El registro está cerrado: no hay una versión legal configurada.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var request = new RegistrationRequest(
            command.CompanyName,
            command.TaxId,
            command.ContactName,
            command.Email,
            command.UserName,
            command.AddressLine1,
            command.City,
            command.PostalCode,
            command.CountryCode,
            _clock.UtcNow);
        if (!string.IsNullOrWhiteSpace(_legalOptions.CurrentVersion))
        {
            request.RecordLegalVersion(_legalOptions.CurrentVersion);
        }

        var pendingExists = await _dbContext.RegistrationRequests.AnyAsync(
            value =>
                value.TaxId == request.TaxId
                && value.Status == RegistrationRequestStatus.Pending,
            cancellationToken);
        if (pendingExists)
        {
            throw PendingConflict();
        }

        _dbContext.RegistrationRequests.Add(request);
        var legalVersion = string.IsNullOrWhiteSpace(_legalOptions.CurrentVersion)
            ? "unversioned"
            : _legalOptions.CurrentVersion.Trim();
        if (string.IsNullOrWhiteSpace(request.LegalVersion))
        {
            request.RecordLegalVersion(legalVersion);
        }

        _dbContext.LegalAcceptances.Add(
            new LegalAcceptance(
                legalVersion,
                _clock.UtcNow,
                _requestContext.IpAddress,
                request.Id,
                customerId: null));
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw PendingConflict();
        }

        return new RegistrationRequestView(
            request.Id,
            request.Status.ToString(),
            request.CreatedUtc);
    }

    public async Task<IReadOnlyCollection<RegistrationRequestDetails>> ListAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        RegistrationRequestStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<RegistrationRequestStatus>(
                    status,
                    ignoreCase: true,
                    out var value))
            {
                throw Validation(
                    "AAA-PORTAL-0006",
                    "El estado de la solicitud no es válido.");
            }

            parsed = value;
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var query = _dbContext.RegistrationRequests.AsNoTracking();
        if (parsed is RegistrationRequestStatus selected)
        {
            query = query.Where(value => value.Status == selected);
        }

        var rows = await query
            .OrderBy(value => value.CreatedUtc)
            .Select(value => new RegistrationRequestDetails(
                value.Id,
                value.CompanyName,
                value.TaxId,
                value.ContactName,
                value.Email,
                value.UserName,
                value.Status.ToString(),
                value.CreatedUtc,
                value.ReviewedUtc,
                value.CustomerId,
                value.RejectionReason))
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows;
    }

    public async Task<RegistrationDecision> ApproveAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var request = await _dbContext.RegistrationRequests
            .SingleOrDefaultAsync(value => value.Id == requestId, cancellationToken)
            ?? throw NotFound(
                "AAA-PORTAL-0010",
                "La solicitud de registro no existe.");
        if (request.Status != RegistrationRequestStatus.Pending)
        {
            throw Conflict(
                "AAA-PORTAL-0007",
                "La solicitud ya no está pendiente.");
        }

        if (await _dbContext.Customers.IgnoreQueryFilters().AnyAsync(
                value => value.TaxId == request.TaxId,
                cancellationToken))
        {
            throw Conflict(
                "AAA-PORTAL-0008",
                "Ya existe un cliente con ese identificador fiscal.");
        }

        if (await _userManager.FindByNameAsync(request.UserName) is not null)
        {
            throw Conflict(
                "AAA-PORTAL-0005",
                "Ya existe un usuario con ese nombre.");
        }

        var customer = new Customer(
            request.CompanyName,
            request.TaxId,
            request.AddressLine1,
            request.City,
            request.PostalCode,
            request.CountryCode);
        _dbContext.Customers.Add(customer);

        var temporaryPassword = CreateTemporaryPassword();
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.UserName,
            Email = request.Email,
            CustomerId = customer.Id,
            LockoutEnabled = true,
        };
        var created = await _userManager.CreateAsync(user, temporaryPassword);
        if (!created.Succeeded)
        {
            throw Conflict(
                "AAA-PORTAL-0005",
                "No se pudo crear el usuario del portal: "
                + string.Join(
                    "; ",
                    created.Errors.Select(error => error.Description)));
        }

        var role = await _userManager.AddToRoleAsync(user, AppRoles.Customer);
        if (!role.Succeeded)
        {
            throw Conflict(
                "AAA-PORTAL-0005",
                "No se pudo asignar el rol de cliente.");
        }

        request.Approve(customer.Id, _clock.UtcNow);
        // Link the submitter's prior legal acceptance to the new customer.
        // Do not overwrite IP/time — those belong to the solicitante.
        var acceptances = await _dbContext.LegalAcceptances
            .Where(value => value.RegistrationRequestId == request.Id)
            .ToListAsync(cancellationToken);
        foreach (var acceptance in acceptances)
        {
            acceptance.AssignCustomer(customer.Id);
        }

        if (acceptances.Count == 0)
        {
            var version = string.IsNullOrWhiteSpace(request.LegalVersion)
                ? (string.IsNullOrWhiteSpace(_legalOptions.CurrentVersion)
                    ? "unversioned"
                    : _legalOptions.CurrentVersion.Trim())
                : request.LegalVersion;
            _dbContext.LegalAcceptances.Add(
                new LegalAcceptance(
                    version,
                    request.CreatedUtc,
                    ip: null,
                    request.Id,
                    customer.Id));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RegistrationDecision(
            request.Id,
            request.Status.ToString(),
            customer.Id,
            user.Id,
            temporaryPassword);
    }

    public async Task<RegistrationDecision> RejectAsync(
        Guid requestId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Validation(
                "AAA-PORTAL-0009",
                "El motivo de rechazo es obligatorio.");
        }

        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var request = await _dbContext.RegistrationRequests
            .SingleOrDefaultAsync(value => value.Id == requestId, cancellationToken)
            ?? throw NotFound(
                "AAA-PORTAL-0010",
                "La solicitud de registro no existe.");
        request.Reject(reason, _clock.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RegistrationDecision(
            request.Id,
            request.Status.ToString(),
            null,
            null,
            null);
    }

    private static string CreateTemporaryPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private static LogisticsException PendingConflict() =>
        new(
            LogisticsErrorKind.Conflict,
            "AAA-PORTAL-0002",
            "Ya existe una solicitud pendiente con ese identificador fiscal.");

    private static LogisticsException Validation(string code, string message) =>
        new(LogisticsErrorKind.Validation, code, message);

    private static LogisticsException NotFound(string code, string message) =>
        new(LogisticsErrorKind.NotFound, code, message);

    private static LogisticsException Conflict(string code, string message) =>
        new(LogisticsErrorKind.Conflict, code, message);
}
