namespace LicenseWatch.Web.Models.Admin;

public class JobsViewModel
{
    public IReadOnlyList<JobScheduleViewModel> Jobs { get; set; } = Array.Empty<JobScheduleViewModel>();
    public IReadOnlyList<JobHistoryItemViewModel> History { get; set; } = Array.Empty<JobHistoryItemViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public bool CanRun { get; set; }
    public bool CanManageSchedules { get; set; }
    public bool CanManageCustomJobs { get; set; }
}

public class JobScheduleViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string ScheduleLabel { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsCustom { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public string? LastStatus { get; set; }
    public string? LastSummary { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRunUtc { get; set; }
}

public class JobHistoryItemViewModel
{
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Error { get; set; }
    public string DurationLabel { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public class JobEditorViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsCustom { get; set; }
    public IReadOnlyList<JobParameterInputModel> Parameters { get; set; } = Array.Empty<JobParameterInputModel>();
    public IReadOnlyList<JobTypeOptionViewModel> JobTypes { get; set; } = Array.Empty<JobTypeOptionViewModel>();
    public IReadOnlyList<CronPresetViewModel> CronPresets { get; set; } = Array.Empty<CronPresetViewModel>();
    public IReadOnlyList<string> NextRuns { get; set; } = Array.Empty<string>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class JobEditorInputModel
{
    public string? Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsCustom { get; set; }
    public List<JobParameterInputModel> Parameters { get; set; } = new();
}

public class JobParameterInputModel
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class JobTypeOptionViewModel
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CronPresetViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}
