namespace LicenseWatch.Web.Models.Admin;

public class EmailSettingsViewModel
{
    public EmailSettingsInputModel Input { get; set; } = new();
    public bool HasPassword { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class EmailSettingsInputModel
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public bool IgnoreTlsErrors { get; set; }
    public bool EnableDailySummary { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string? DefaultToEmail { get; set; }
    public int SuppressionMinutes { get; set; } = 60;
}

public class EmailTemplateListViewModel
{
    public IReadOnlyList<EmailTemplateListItemViewModel> Templates { get; set; } = Array.Empty<EmailTemplateListItemViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class EmailTemplateListItemViewModel
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class EmailTemplateEditViewModel
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyHtmlTemplate { get; set; } = string.Empty;
    public string? BodyTextTemplate { get; set; }
    public bool IsEnabled { get; set; }
    public string PreviewSubject { get; set; } = string.Empty;
    public string PreviewHtml { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
    public IReadOnlyList<string> AvailableTokens { get; set; } = Array.Empty<string>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class EmailNotificationSettingsViewModel
{
    public List<EmailNotificationRuleInputModel> Rules { get; set; } = new();
    public IReadOnlyList<string> Frequencies { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class EmailNotificationRuleInputModel
{
    public Guid Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Instant";
    public bool IsEnabled { get; set; }
    public List<string> SelectedRoles { get; set; } = new();
}

public class NotificationLogListViewModel
{
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Search { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public IReadOnlyList<string> Types { get; set; } = Array.Empty<string>();
    public IReadOnlyList<NotificationLogItemViewModel> Items { get; set; } = Array.Empty<NotificationLogItemViewModel>();
}

public class NotificationLogItemViewModel
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class NotificationLogDetailViewModel
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string? CorrelationId { get; set; }
    public string? TriggerEntityType { get; set; }
    public string? TriggerEntityId { get; set; }
}
