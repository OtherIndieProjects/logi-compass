namespace AAALogistics.Application.Abstractions;

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string TextBody);

public interface IEmailSender
{
    Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default);
}
