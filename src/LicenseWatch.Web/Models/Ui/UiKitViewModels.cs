using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Razor;

namespace LicenseWatch.Web.Models.Ui;

public class PageHeaderViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? UpdatedLabel { get; set; }
    public Func<object, HelperResult>? Actions { get; set; }
}

public class SectionCardViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public Func<object, HelperResult>? Actions { get; set; }
    public Func<object, HelperResult>? Body { get; set; }
    public Func<object, HelperResult>? Footer { get; set; }
}

public class KpiCardViewModel
{
    public string Icon { get; set; } = "bi-graph-up";
    public string Value { get; set; } = "0";
    public string Label { get; set; } = string.Empty;
    public string? Subtext { get; set; }
    public string Severity { get; set; } = "neutral";
    public string? Href { get; set; }
    public bool IsZero { get; set; }
    public int? NumericValue { get; set; }
}

public class InsightCardViewModel
{
    public string Severity { get; set; } = "neutral";
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyCollection<string> Evidence { get; set; } = Array.Empty<string>();
    public CtaViewModel? PrimaryCta { get; set; }
    public CtaViewModel? SecondaryCta { get; set; }
}

public class PriorityListViewModel
{
    public string? Title { get; set; }
    public IReadOnlyCollection<PriorityListItemViewModel> Items { get; set; } = Array.Empty<PriorityListItemViewModel>();
    public string EmptyMessage { get; set; } = "No items found.";
    public string? ViewAllUrl { get; set; }
    public string? EmptyIcon { get; set; }
    public string? EmptyCtaLabel { get; set; }
    public string? EmptyCtaUrl { get; set; }
}

public class PriorityListItemViewModel
{
    public string Severity { get; set; } = "Warning";
    public string Summary { get; set; } = string.Empty;
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public string? Why { get; set; }
}

public class ActivityFeedViewModel
{
    public string? Title { get; set; }
    public IReadOnlyCollection<ActivityFeedItemViewModel> Items { get; set; } = Array.Empty<ActivityFeedItemViewModel>();
    public string EmptyMessage { get; set; } = "No recent activity yet.";
    public string? EmptyIcon { get; set; }
    public string? EmptyCtaLabel { get; set; }
    public string? EmptyCtaUrl { get; set; }
}

public class ActivityFeedItemViewModel
{
    public string Icon { get; set; } = "bi-activity";
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string? TargetUrl { get; set; }
}

public class FilterBarViewModel
{
    public string Label { get; set; } = "Filter";
    public IReadOnlyCollection<FilterPillViewModel> Filters { get; set; } = Array.Empty<FilterPillViewModel>();
    public string? ClearUrl { get; set; }
}

public class FilterPillViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = "#";
    public bool IsActive { get; set; }
}

public class EmptyStateViewModel
{
    public string Icon { get; set; } = "bi-stars";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public CtaViewModel? PrimaryCta { get; set; }
    public CtaViewModel? SecondaryCta { get; set; }
}

public class CtaViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = "#";
    public string? Icon { get; set; }
    public string Style { get; set; } = "primary";
}
