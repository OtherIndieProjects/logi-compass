using System.Globalization;
using System.Text.Json;
using AAALogistics.Application.Abstractions;
using AAALogistics.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AAALogistics.Infrastructure.Email;

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class FileEmailSender(
    IOptions<EmailOptions> options,
    IClock clock) : IEmailSender
{
    private readonly string _directory = Path.GetFullPath(
        options?.Value.FileDirectory
        ?? throw new ArgumentNullException(nameof(options)));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        Directory.CreateDirectory(_directory);

        var createdAtUtc = _clock.UtcNow;
        var timestamp = createdAtUtc.ToString(
            "yyyyMMdd'T'HHmmss.fff'Z'",
            CultureInfo.InvariantCulture);
        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"{timestamp}-{Guid.CreateVersion7():N}.json");
        var path = Path.Combine(_directory, fileName);

        await using var stream = new FileStream(
            path,
            new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            });

        await JsonSerializer.SerializeAsync(
            stream,
            new StoredEmail(
                createdAtUtc,
                message.Recipient,
                message.Subject,
                message.TextBody),
            cancellationToken: cancellationToken);
    }

    private sealed record StoredEmail(
        DateTimeOffset CreatedAtUtc,
        string Recipient,
        string Subject,
        string TextBody);
}

/// <summary>
/// SMTP sender for industrial / staging (ADR 0025). Uses System.Net.Mail.
/// </summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(_options.SmtpHost)
            || string.IsNullOrWhiteSpace(_options.SmtpFrom))
        {
            throw new InvalidOperationException(
                "Email:SmtpHost y Email:SmtpFrom son obligatorios en modo Smtp.");
        }

        using var client = new System.Net.Mail.SmtpClient(
            _options.SmtpHost,
            _options.SmtpPort)
        {
            EnableSsl = _options.SmtpUseStartTls,
            DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
        };
        if (!string.IsNullOrWhiteSpace(_options.SmtpUserName))
        {
            client.Credentials = new System.Net.NetworkCredential(
                _options.SmtpUserName,
                _options.SmtpPassword ?? string.Empty);
        }

        using var mail = new System.Net.Mail.MailMessage(
            _options.SmtpFrom,
            message.Recipient,
            message.Subject,
            message.TextBody);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail, cancellationToken);
    }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
