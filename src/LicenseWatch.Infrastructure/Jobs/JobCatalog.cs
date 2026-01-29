using LicenseWatch.Core.Jobs;

namespace LicenseWatch.Infrastructure.Jobs;

public static class JobCatalog
{
    public static readonly IReadOnlyList<JobDefinition> BuiltIn = new List<JobDefinition>
    {
        new(JobKeys.UsageAggregation,
            "Usage aggregation",
            "Aggregates usage data into daily summaries for the last 30 days.",
            JobKeys.UsageAggregation,
            "0 2 * * *"),
        new(JobKeys.ComplianceEvaluation,
            "Compliance evaluation",
            "Evaluates licenses for overuse, expiry, and missing seat rules.",
            JobKeys.ComplianceEvaluation,
            "0 2 * * *"),
        new(JobKeys.Notifications,
            "Notifications",
            "Sends digest email notifications based on configured rules.",
            JobKeys.Notifications,
            "0 2 * * *"),
        new(JobKeys.AuditRetention,
            "Audit retention cleanup",
            "Purges audit logs older than the configured retention window.",
            JobKeys.AuditRetention,
            "30 2 * * *")
    };

    public static readonly IReadOnlyList<JobDefinition> Supported = new List<JobDefinition>(BuiltIn)
    {
        new(JobKeys.ReportDelivery,
            "Report delivery",
            "Delivers scheduled reports to recipients via email.",
            JobKeys.ReportDelivery,
            "0 6 * * *")
    };

    public static bool IsSupportedJobType(string jobType)
        => Supported.Any(job => string.Equals(job.JobType, jobType, StringComparison.OrdinalIgnoreCase));
}

public record JobDefinition(
    string Key,
    string Name,
    string Description,
    string JobType,
    string DefaultCron);
