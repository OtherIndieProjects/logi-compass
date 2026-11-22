using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Logistics;
using AAALogistics.Domain.Industrial;
using AAALogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AAALogistics.Infrastructure.Logistics;

public sealed class IndustrialLogisticsService(
    ApplicationDbContext dbContext,
    ICustomerRlsSession rlsSession) : IIndustrialLogisticsService
{
    private readonly ApplicationDbContext _db = dbContext;
    private readonly ICustomerRlsSession _rlsSession = rlsSession;

    public async Task<CompanyView> CreateCompanyAsync(
        CreateCompanyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = Domain.Catalog.UnitOfMeasure.NormalizeCode(command.Code, 30);
        if (await _db.Companies.AnyAsync(value => value.Code == code, cancellationToken))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Conflict,
                "AAA-CO-0001",
                "Ya existe una empresa con ese código.");
        }

        var company = new Company(
            command.Code,
            command.LegalName,
            command.TaxId,
            command.Currency);
        _db.Companies.Add(company);
        await _db.SaveChangesAsync(cancellationToken);
        return ToCompany(company);
    }

    public async Task<IReadOnlyCollection<CompanyView>> ListCompaniesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Companies.AsNoTracking()
            .OrderBy(value => value.Code)
            .ToListAsync(cancellationToken);
        return rows.Select(ToCompany).ToArray();
    }

    public async Task<ExchangeRateView> UpsertExchangeRateAsync(
        UpsertExchangeRateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var from = Domain.Catalog.UnitOfMeasure.NormalizeCode(command.FromCurrency, 3);
        var to = Domain.Catalog.UnitOfMeasure.NormalizeCode(command.ToCurrency, 3);
        var existing = await _db.ExchangeRates.SingleOrDefaultAsync(
            value =>
                value.FromCurrency == from
                && value.ToCurrency == to
                && value.ValidOn == command.ValidOn,
            cancellationToken);
        if (existing is null)
        {
            existing = new ExchangeRate(from, to, command.Rate, command.ValidOn);
            _db.ExchangeRates.Add(existing);
        }
        else
        {
            existing.UpdateRate(command.Rate);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToFx(existing);
    }

    public async Task<IReadOnlyCollection<ExchangeRateView>> ListExchangeRatesAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.ExchangeRates.AsNoTracking()
            .OrderByDescending(value => value.ValidOn)
            .ThenBy(value => value.FromCurrency)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToFx).ToArray();
    }

    public async Task<FxConvertResult> ConvertFxAsync(
        string fromCurrency,
        string toCurrency,
        decimal amount,
        DateOnly? onDate = null,
        CancellationToken cancellationToken = default)
    {
        var from = Domain.Catalog.UnitOfMeasure.NormalizeCode(fromCurrency, 3);
        var to = Domain.Catalog.UnitOfMeasure.NormalizeCode(toCurrency, 3);
        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return new FxConvertResult(from, to, amount, amount, 1m, onDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
        }

        var day = onDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rate = await _db.ExchangeRates.AsNoTracking()
            .Where(value =>
                value.FromCurrency == from
                && value.ToCurrency == to
                && value.ValidOn <= day)
            .OrderByDescending(value => value.ValidOn)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-FX-0001",
                "No hay tipo de cambio para el par/fecha.");

        return new FxConvertResult(
            from,
            to,
            amount,
            rate.Convert(amount),
            rate.Rate,
            rate.ValidOn);
    }

    public async Task<CustomerCommercialTermsView> UpsertCommercialTermsAsync(
        UpsertCustomerCommercialTermsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);
        await _rlsSession.ApplyAsync(cancellationToken);
        var customerOk = await _db.Customers.AnyAsync(
            value => value.Id == command.CustomerId,
            cancellationToken);
        var companyOk = await _db.Companies.AnyAsync(
            value => value.Id == command.CompanyId,
            cancellationToken);
        if (!customerOk || !companyOk)
        {
            throw new LogisticsException(
                LogisticsErrorKind.NotFound,
                "AAA-CREDIT-0001",
                "Cliente o empresa no encontrados.");
        }

        var terms = await _db.CustomerCommercialTerms.SingleOrDefaultAsync(
            value =>
                value.CustomerId == command.CustomerId
                && value.CompanyId == command.CompanyId,
            cancellationToken);
        if (terms is null)
        {
            terms = new CustomerCommercialTerms(
                command.CustomerId,
                command.CompanyId,
                command.PaymentTermDays,
                command.CreditLimit,
                command.Currency);
            _db.CustomerCommercialTerms.Add(terms);
        }
        else
        {
            terms.Update(
                command.PaymentTermDays,
                command.CreditLimit,
                command.Currency);
        }

        terms.SetOpenReceivables(command.OpenReceivables);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToTerms(terms);
    }

    public async Task<CustomerCommercialTermsView?> GetCommercialTermsAsync(
        Guid customerId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var terms = await _db.CustomerCommercialTerms.AsNoTracking()
            .SingleOrDefaultAsync(
                value =>
                    value.CustomerId == customerId
                    && value.CompanyId == companyId,
                cancellationToken);
        return terms is null ? null : ToTerms(terms);
    }

    public async Task EnsureCreditAllowsConfirmAsync(
        Guid customerId,
        Guid? companyId,
        decimal orderAmount,
        CancellationToken cancellationToken = default)
    {
        if (orderAmount <= 0)
        {
            return;
        }

        // With company claim: enforce that company's terms.
        // Without claim: if any company has terms for the customer, enforce the
        // strictest match so credit cannot be bypassed by missing JWT company_id.
        var query = _db.CustomerCommercialTerms.AsNoTracking()
            .Where(value => value.CustomerId == customerId);
        if (companyId is Guid scopedCompanyId)
        {
            query = query.Where(value => value.CompanyId == scopedCompanyId);
        }

        var termsList = await query.ToListAsync(cancellationToken);
        if (termsList.Count == 0)
        {
            return;
        }

        foreach (var terms in termsList)
        {
            if (terms.IsCreditBlocked || terms.WouldExceedCredit(orderAmount))
            {
                throw new LogisticsException(
                    LogisticsErrorKind.Conflict,
                    "AAA-CREDIT-0002",
                    "Confirmación bloqueada por límite de crédito o bloqueo comercial.");
            }
        }
    }

    public async Task<RfScanResult> ResolveRfScanAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new LogisticsException(
                LogisticsErrorKind.Validation,
                "AAA-RF-0001",
                "Código de escaneo vacío.");
        }

        var trimmed = code.Trim();
        var now = DateTimeOffset.UtcNow;

        var hu = await _db.HandlingUnits.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Sscc == trimmed, cancellationToken);
        if (hu is not null)
        {
            return new RfScanResult(
                trimmed,
                "SSCC",
                now,
                hu.Id,
                $"HU {hu.Sscc} · SO {hu.SalesOrderId:D}");
        }

        var material = await _db.Materials.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Code == trimmed, cancellationToken);
        if (material is not null)
        {
            return new RfScanResult(
                trimmed,
                "Barcode",
                now,
                material.Id,
                $"{material.Code} · {material.DisplayName}");
        }

        throw new LogisticsException(
            LogisticsErrorKind.NotFound,
            "AAA-RF-0002",
            "Código no resuelto a HU/SSCC ni material.");
    }

    private static CompanyView ToCompany(Company company) =>
        new(
            company.Id,
            company.Code,
            company.LegalName,
            company.TaxId,
            company.Currency,
            company.IsActive);

    private static ExchangeRateView ToFx(ExchangeRate rate) =>
        new(rate.Id, rate.FromCurrency, rate.ToCurrency, rate.Rate, rate.ValidOn);

    private static CustomerCommercialTermsView ToTerms(CustomerCommercialTerms terms) =>
        new(
            terms.Id,
            terms.CustomerId,
            terms.CompanyId,
            terms.PaymentTermDays,
            terms.CreditLimit,
            terms.OpenReceivables,
            terms.Currency,
            terms.IsCreditBlocked);
}
