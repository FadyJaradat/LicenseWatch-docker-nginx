using LicenseWatch.Core.Jobs;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Diagnostics;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.SystemView)]
[Route("admin/system")]
public class SystemController : Controller
{
    private static readonly IReadOnlyList<JobDefinition> JobDefinitions = new[]
    {
        new JobDefinition(JobKeys.UsageAggregation, "Usage aggregation"),
        new JobDefinition(JobKeys.ComplianceEvaluation, "Compliance evaluation"),
        new JobDefinition(JobKeys.Notifications, "Notifications")
    };

    private readonly AppDbContext _dbContext;
    private readonly HealthCheckService _healthChecks;
    private readonly AppRuntimeInfo _runtimeInfo;
    private readonly IWebHostEnvironment _environment;

    public SystemController(
        AppDbContext dbContext,
        HealthCheckService healthChecks,
        AppRuntimeInfo runtimeInfo,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _healthChecks = healthChecks;
        _runtimeInfo = runtimeInfo;
        _environment = environment;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildViewModelAsync();
        return View(vm);
    }

    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run()
    {
        var vm = await BuildViewModelAsync();
        vm.AlertMessage = "Readiness checks completed.";
        vm.AlertStyle = "success";
        return View("Index", vm);
    }

    private async Task<SystemStatusViewModel> BuildViewModelAsync()
    {
        var report = await _healthChecks.CheckHealthAsync(entry => entry.Tags.Contains("ready"));
        var checks = report.Entries.Select(entry => new SystemHealthCheckViewModel
        {
            Name = FormatCheckName(entry.Key),
            Status = entry.Value.Status.ToString(),
            Description = entry.Value.Description ?? string.Empty,
            Guidance = BuildGuidance(entry.Key, entry.Value.Status),
            Duration = entry.Value.Duration,
            Details = entry.Value.Exception?.Message
        }).OrderBy(c => c.Name).ToList();

        var history = await _dbContext.JobExecutionLogs.AsNoTracking()
            .OrderByDescending(log => log.StartedAtUtc)
            .ToListAsync();

        var latestByJob = history
            .GroupBy(log => log.JobKey)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(log => log.StartedAtUtc).First());

        var jobs = JobDefinitions.Select(definition =>
        {
            latestByJob.TryGetValue(definition.Key, out var lastRun);
            return new SystemJobSummaryViewModel
            {
                Key = definition.Key,
                Name = definition.Name,
                LastRunUtc = lastRun?.StartedAtUtc,
                Status = lastRun?.Status ?? "Unknown",
                Summary = lastRun?.Summary
            };
        }).ToList();

        var uptime = DateTime.UtcNow - _runtimeInfo.StartedAtUtc;

        return new SystemStatusViewModel
        {
            Version = AppInfo.DisplayVersion,
            Commit = AppInfo.Commit,
            CommitShort = AppInfo.CommitShort,
            BuildTimestamp = AppInfo.BuildTimestampDisplay,
            EnvironmentName = _environment.EnvironmentName,
            StartedAtUtc = _runtimeInfo.StartedAtUtc,
            Uptime = FormatUptime(uptime),
            Jobs = jobs,
            Checks = checks,
            CheckedAtUtc = DateTime.UtcNow
        };
    }

    private static string FormatCheckName(string key)
        => key switch
        {
            "identity-db" => "Identity database",
            "app-db" => "Application database",
            "hangfire-db" => "Hangfire storage",
            "app-data-writable" => "App_Data writable",
            "dp-keys-writable" => "Data Protection keys writable",
            "bootstrap-settings" => "Bootstrap settings",
            _ => key
        };

    private static string? BuildGuidance(string key, HealthStatus status)
    {
        if (status == HealthStatus.Healthy)
        {
            return null;
        }

        return key switch
        {
            "identity-db" => "Identity DB missing or unavailable. Ensure App_Data volume is mounted and migrations are applied.",
            "app-db" => "App DB missing or unavailable. Apply migrations from /admin/database.",
            "hangfire-db" => "Hangfire storage is missing. Verify /app/App_Data is writable.",
            "app-data-writable" => "App_Data volume not writable. Check Docker volume mount and permissions.",
            "dp-keys-writable" => "Data Protection keys directory is not writable. Verify /app/App_Data/keys.",
            "bootstrap-settings" => "Bootstrap settings cannot be decrypted. Ensure Data Protection keys persist across restarts.",
            _ => "Review logs for more details."
        };
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{uptime.Hours}h {uptime.Minutes}m";
        }

        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private sealed record JobDefinition(string Key, string Name);
}
