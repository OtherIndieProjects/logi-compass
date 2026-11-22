using System.Net.Http.Json;
using AAALogistics.Application.Logistics;
using AAALogistics.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Billing;

/// <summary>
/// Lab HTTP bridge for Verifactu (ADR 0025). Posts a generic JSON envelope with
/// invoice id to <c>Verifactu:AeatBaseUrl</c>. Not a real SIF/AEAT payload;
/// client certificate path is reserved and unused until mutual TLS is wired.
/// Keep RF-FAC-010 as Parcial until a real SIF body is implemented.
/// </summary>
public sealed class AeatHttpVerifactuSubmitter(
    HttpClient httpClient,
    IOptions<VerifactuOptions> options,
    ILogger<AeatHttpVerifactuSubmitter> logger) : IVerifactuSubmitter
{
    private readonly HttpClient _httpClient =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    private readonly VerifactuOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    private readonly ILogger<AeatHttpVerifactuSubmitter> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task SubmitAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.AeatBaseUrl))
        {
            throw new InvalidOperationException(
                "Verifactu:AeatBaseUrl es obligatorio en modo AeatHttp.");
        }

        var baseUri = _options.AeatBaseUrl.TrimEnd('/');
        var url = $"{baseUri}/verifactu/submissions";
        _logger.LogInformation(
            "VerifactuSubmit AEAT HTTP for invoice {InvoiceId} → {Url} (cert={Cert})",
            invoiceId,
            url,
            string.IsNullOrWhiteSpace(_options.ClientCertificatePath)
                ? "(none)"
                : _options.ClientCertificatePath);

        using var response = await _httpClient.PostAsJsonAsync(
            url,
            new { invoiceId, mode = "AeatHttp" },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"AEAT Verifactu HTTP {(int)response.StatusCode}: {body}");
        }
    }
}
