using LicenseWatch.Infrastructure.Reports;

namespace LicenseWatch.Web.Models.Admin;

public class ReportsLandingViewModel
{
    public IReadOnlyList<ReportCardViewModel> Reports { get; set; } = Array.Empty<ReportCardViewModel>();
}

public class ReportCardViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-graph-up";
}

public class ReportPresetViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime? LastUsedAtUtc { get; set; }
}

public class LicenseReportViewModel
{
    public Guid? CategoryId { get; set; }
    public string? Vendor { get; set; }
    public string? Status { get; set; }
    public DateTime? ExpiresFrom { get; set; }
    public DateTime? ExpiresTo { get; set; }
    public IReadOnlyCollection<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public IReadOnlyList<string> StatusOptions { get; set; } = Array.Empty<string>();
    public PagedResult<LicenseReportRow>? Results { get; set; }
    public DateTime LastRefreshedUtc { get; set; }
    public IReadOnlyList<ReportPresetViewModel> Presets { get; set; } = Array.Empty<ReportPresetViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class ExpirationReportViewModel
{
    public Guid? CategoryId { get; set; }
    public int? ExpiringDays { get; set; }
    public DateTime? ExpiresFrom { get; set; }
    public DateTime? ExpiresTo { get; set; }
    public IReadOnlyCollection<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public PagedResult<ExpirationReportRow>? Results { get; set; }
    public DateTime LastRefreshedUtc { get; set; }
    public IReadOnlyList<ReportPresetViewModel> Presets { get; set; } = Array.Empty<ReportPresetViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class ComplianceReportViewModel
{
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? Rule { get; set; }
    public IReadOnlyList<string> StatusOptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> SeverityOptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> RuleOptions { get; set; } = Array.Empty<string>();
    public PagedResult<ComplianceReportRow>? Results { get; set; }
    public DateTime LastRefreshedUtc { get; set; }
    public IReadOnlyList<ReportPresetViewModel> Presets { get; set; } = Array.Empty<ReportPresetViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class UsageReportViewModel
{
    public Guid? CategoryId { get; set; }
    public Guid? LicenseId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public IReadOnlyCollection<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public IReadOnlyCollection<LicenseOption> Licenses { get; set; } = Array.Empty<LicenseOption>();
    public PagedResult<UsageReportRow>? Results { get; set; }
    public DateTime LastRefreshedUtc { get; set; }
    public IReadOnlyList<ReportPresetViewModel> Presets { get; set; } = Array.Empty<ReportPresetViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class ReportScheduleListViewModel
{
    public IReadOnlyList<ReportScheduleItemViewModel> Schedules { get; set; } = Array.Empty<ReportScheduleItemViewModel>();
    public IReadOnlyList<ReportDeliveryLogViewModel> RecentDeliveries { get; set; } = Array.Empty<ReportDeliveryLogViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public bool CanManage { get; set; }
    public bool CanRun { get; set; }
}

public class ReportScheduleItemViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public string Format { get; set; } = "csv";
    public string Recipients { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string ScheduleLabel { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public string? LastStatus { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public Guid? PresetId { get; set; }
}

public class ReportDeliveryLogViewModel
{
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
}

public class ReportScheduleEditorViewModel
{
    public string? Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportKey { get; set; } = string.Empty;
    public Guid? PresetId { get; set; }
    public string Format { get; set; } = "csv";
    public string Recipients { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public IReadOnlyList<ReportPresetViewModel> Presets { get; set; } = Array.Empty<ReportPresetViewModel>();
    public IReadOnlyList<ReportOptionViewModel> Reports { get; set; } = Array.Empty<ReportOptionViewModel>();
    public IReadOnlyList<CronPresetViewModel> CronPresets { get; set; } = Array.Empty<CronPresetViewModel>();
    public IReadOnlyList<string> NextRuns { get; set; } = Array.Empty<string>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class ReportOptionViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class LicenseOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
