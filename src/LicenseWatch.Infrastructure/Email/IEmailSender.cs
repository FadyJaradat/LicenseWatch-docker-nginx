namespace LicenseWatch.Infrastructure.Email;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody,
        string type,
        string? correlationId,
        string? triggerEntityType,
        string? triggerEntityId,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default);
}

public record EmailSendResult(string Status, string? ErrorMessage = null);

public record EmailAttachment(string FileName, string ContentType, byte[] Content);
