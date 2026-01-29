using System.Net;
using System.Net.Mail;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Email;

public class EmailSender : IEmailSender
{
    private readonly IBootstrapSettingsStore _settingsStore;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IBootstrapSettingsStore settingsStore, AppDbContext dbContext, ILogger<EmailSender> logger)
    {
        _settingsStore = settingsStore;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? textBody,
        string type,
        string? correlationId,
        string? triggerEntityType,
        string? triggerEntityId,
        IReadOnlyList<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Email send requested {Type} to {ToEmail}", type, toEmail);

        if (!IsValidEmail(toEmail))
        {
            return await LogResultAsync("Failed", "Recipient email is invalid.", toEmail, subject, type, correlationId, triggerEntityType, triggerEntityId, cancellationToken);
        }

        var settings = await _settingsStore.LoadAsync(cancellationToken);
        var emailSettings = settings.Email;
        if (!HasRequiredSettings(emailSettings))
        {
            return await LogResultAsync("Failed", "SMTP settings are incomplete.", toEmail, subject, type, correlationId, triggerEntityType, triggerEntityId, cancellationToken);
        }

        if (string.Equals(type, "TestEmail", StringComparison.OrdinalIgnoreCase))
        {
            var suppressionMinutes = emailSettings.SuppressionMinutes <= 0 ? 60 : emailSettings.SuppressionMinutes;
            var windowStart = DateTime.UtcNow.AddMinutes(-suppressionMinutes);
            var recent = await _dbContext.NotificationLogs.AsNoTracking()
                .AnyAsync(n => n.Type == "TestEmail" && n.ToEmail == toEmail && n.CreatedAtUtc >= windowStart, cancellationToken);
            if (recent)
            {
                return await LogResultAsync("Suppressed", "Test email suppressed to avoid repeat sends.", toEmail, subject, type, correlationId, triggerEntityType, triggerEntityId, cancellationToken);
            }
        }

        var originalCallback = ServicePointManager.ServerCertificateValidationCallback;
        if (emailSettings.IgnoreTlsErrors)
        {
            ServicePointManager.ServerCertificateValidationCallback = (_, _, _, _) => true;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(emailSettings.FromEmail, emailSettings.FromName),
                Subject = subject,
                Body = string.IsNullOrWhiteSpace(htmlBody) ? (textBody ?? string.Empty) : htmlBody,
                IsBodyHtml = !string.IsNullOrWhiteSpace(htmlBody)
            };
            message.To.Add(toEmail);

            if (!string.IsNullOrWhiteSpace(textBody) && !string.IsNullOrWhiteSpace(htmlBody))
            {
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, "text/plain"));
            }

            if (attachments is not null)
            {
                foreach (var attachment in attachments)
                {
                    if (attachment.Content.Length == 0 || string.IsNullOrWhiteSpace(attachment.FileName))
                    {
                        continue;
                    }

                    var stream = new MemoryStream(attachment.Content);
                    var mailAttachment = new System.Net.Mail.Attachment(stream, attachment.FileName, attachment.ContentType);
                    message.Attachments.Add(mailAttachment);
                }
            }

            using var smtp = new SmtpClient(emailSettings.SmtpHost, emailSettings.SmtpPort)
            {
                EnableSsl = emailSettings.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(emailSettings.Username) && !string.IsNullOrWhiteSpace(emailSettings.Password))
            {
                smtp.Credentials = new NetworkCredential(emailSettings.Username, emailSettings.Password);
            }

            await smtp.SendMailAsync(message);

            return await LogResultAsync("Sent", null, toEmail, subject, type, correlationId, triggerEntityType, triggerEntityId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email ({Type}) to {ToEmail}", type, toEmail);
            var error = SanitizeError(ex);
            return await LogResultAsync("Failed", error, toEmail, subject, type, correlationId, triggerEntityType, triggerEntityId, cancellationToken);
        }
        finally
        {
            if (emailSettings.IgnoreTlsErrors)
            {
                ServicePointManager.ServerCertificateValidationCallback = originalCallback;
            }
        }
    }

    private async Task<EmailSendResult> LogResultAsync(
        string status,
        string? error,
        string toEmail,
        string subject,
        string type,
        string? correlationId,
        string? triggerEntityType,
        string? triggerEntityId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email send {Status} ({Type}) to {ToEmail}", status, type, toEmail);

        var log = new NotificationLog
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Type = type,
            ToEmail = toEmail,
            Subject = subject,
            Status = status,
            Error = string.IsNullOrWhiteSpace(error) ? null : TrimToLength(error, 1000),
            CorrelationId = correlationId,
            TriggerEntityType = triggerEntityType,
            TriggerEntityId = triggerEntityId
        };

        _dbContext.NotificationLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new EmailSendResult(status, error);
    }

    private static bool HasRequiredSettings(LicenseWatch.Core.Models.EmailSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.SmtpHost)
               && settings.SmtpPort > 0
               && !string.IsNullOrWhiteSpace(settings.FromName)
               && !string.IsNullOrWhiteSpace(settings.FromEmail);
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeError(Exception ex)
    {
        var message = ex.Message.Replace(Environment.NewLine, " ").Replace("\r", " ").Replace("\n", " ");
        if (ex.InnerException is not null)
        {
            message = $"{message} | {ex.InnerException.Message}";
        }

        return message;
    }

    private static string TrimToLength(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
