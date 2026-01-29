using LicenseWatch.Core.Entities;

namespace LicenseWatch.Infrastructure.Email;

public class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public EmailRenderResult Render(EmailTemplate template, IReadOnlyDictionary<string, string?> tokens)
    {
        var subject = ReplaceTokens(template.SubjectTemplate, tokens);
        var html = ReplaceTokens(template.BodyHtmlTemplate, tokens);
        var text = ReplaceTokens(template.BodyTextTemplate ?? string.Empty, tokens);
        return new EmailRenderResult(subject, html, text);
    }

    private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string?> tokens)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var output = template;
        foreach (var token in tokens)
        {
            var placeholder = $"{{{{{token.Key}}}}}";
            output = output.Replace(placeholder, token.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return output;
    }
}
