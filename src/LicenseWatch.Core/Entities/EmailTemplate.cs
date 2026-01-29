namespace LicenseWatch.Core.Entities;

public class EmailTemplate
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string SubjectTemplate { get; set; } = string.Empty;

    public string BodyHtmlTemplate { get; set; } = string.Empty;

    public string? BodyTextTemplate { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; }

    public string UpdatedByUserId { get; set; } = string.Empty;
}
