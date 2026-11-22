using AAALogistics.Application.Logistics;
using Microsoft.Extensions.Logging;

namespace AAALogistics.Infrastructure.Billing;

/// <summary>
/// Didactic Verifactu stub: LocalChain mode logs only; never calls AEAT.
/// </summary>
public sealed class LocalChainVerifactuSubmitter(
    ILogger<LocalChainVerifactuSubmitter> logger) : IVerifactuSubmitter
{
    private readonly ILogger<LocalChainVerifactuSubmitter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public Task SubmitAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "VerifactuSubmit LocalChain no-op for invoice {InvoiceId}",
            invoiceId);
        return Task.CompletedTask;
    }
}
