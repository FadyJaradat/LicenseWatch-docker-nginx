using LicenseWatch.Core.Entities;

namespace LicenseWatch.Infrastructure.Email;

public interface IEmailTemplateRenderer
{
    EmailRenderResult Render(EmailTemplate template, IReadOnlyDictionary<string, string?> tokens);
}

public record EmailRenderResult(string Subject, string HtmlBody, string TextBody);
