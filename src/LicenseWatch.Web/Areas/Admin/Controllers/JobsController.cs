using System.Security.Claims;
using System.Text.Json;
using Cronos;
using Hangfire;
using Hangfire.Storage;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Jobs;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.JobsView)]
[Route("admin/jobs")]
public class JobsController : Controller
{
    private static readonly IReadOnlyList<CronPresetViewModel> CronPresets = new List<CronPresetViewModel>
    {
        new() { Label = "Daily at 02:00 UTC", Expression = "0 2 * * *" },
        new() { Label = "Every 6 hours", Expression = "0 */6 * * *" },
        new() { Label = "Every hour", Expression = "0 * * * *" },
        new() { Label = "Weekly (Mon 02:00 UTC)", Expression = "0 2 * * 1" }
    };

    private readonly AppDbContext _dbContext;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IJobScheduler _scheduler;
    private readonly IAuditLogger _auditLogger;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        AppDbContext dbContext,
        IBackgroundJobClient backgroundJobs,
        IJobScheduler scheduler,
        IAuditLogger auditLogger,
        IPermissionService permissionService,
        ILogger<JobsController> logger)
    {
        _dbContext = dbContext;
        _backgroundJobs = backgroundJobs;
        _scheduler = scheduler;
        _auditLogger = auditLogger;
        _permissionService = permissionService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildViewModelAsync();
        return View(vm);
    }

    [HttpGet("create")]
    [Authorize(Policy = PermissionPolicies.JobsCustomManage)]
    public IActionResult Create()
    {
        var vm = BuildEditorViewModel(new JobEditorInputModel
        {
            IsCustom = true,
            IsEnabled = true,
            CronExpression = CronPresets[0].Expression,
            JobType = JobCatalog.BuiltIn.First().JobType
        });
        return View("Edit", vm);
    }

    [HttpGet("edit/{jobKey}")]
    [Authorize(Policy = PermissionPolicies.JobsScheduleManage)]
    public async Task<IActionResult> Edit(string jobKey)
    {
        var definition = await _dbContext.ScheduledJobs.AsNoTracking()
            .FirstOrDefaultAsync(job => job.Key == jobKey);

        if (definition is null)
        {
            TempData["JobsAlertMessage"] = "Job definition not found.";
            TempData["JobsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var isCustom = !JobCatalog.BuiltIn.Any(def => string.Equals(def.Key, definition.Key, StringComparison.OrdinalIgnoreCase));
        if (isCustom && !await _permissionService.HasPermissionAsync(User, PermissionKeys.JobsCustomManage))
        {
            TempData["JobsAlertMessage"] = "You do not have permission to edit custom jobs.";
            TempData["JobsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var input = new JobEditorInputModel
        {
            Key = definition.Key,
            Name = definition.Name,
            Description = definition.Description,
            JobType = definition.JobType,
            CronExpression = definition.CronExpression,
            IsEnabled = definition.IsEnabled,
            IsCustom = isCustom,
            Parameters = ParseParameters(definition.ParametersJson)
        };

        var vm = BuildEditorViewModel(input);
        return View("Edit", vm);
    }

    [HttpPost("save")]
    [Authorize(Policy = PermissionPolicies.JobsScheduleManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(JobEditorInputModel input)
    {
        var errors = ValidateInput(input).ToList();
        if (errors.Count > 0)
        {
            var vmInvalid = BuildEditorViewModel(input);
            vmInvalid.AlertMessage = "Please correct the highlighted fields.";
            vmInvalid.AlertStyle = "danger";
            vmInvalid.AlertDetails = string.Join(Environment.NewLine, errors);
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View("Edit", vmInvalid);
        }

        var isCustom = input.IsCustom || string.IsNullOrWhiteSpace(input.Key);
        if (isCustom && !await _permissionService.HasPermissionAsync(User, PermissionKeys.JobsCustomManage))
        {
            TempData["JobsAlertMessage"] = "You do not have permission to manage custom jobs.";
            TempData["JobsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }
        var key = string.IsNullOrWhiteSpace(input.Key)
            ? $"custom-{Guid.NewGuid():N}"
            : input.Key.Trim();

        var parametersJson = SerializeParameters(input.Parameters);

        if (!isCustom)
        {
            var existing = await _dbContext.ScheduledJobs.AsNoTracking()
                .FirstOrDefaultAsync(job => job.Key == key);

            if (existing is not null)
            {
                input.Name = existing.Name;
                input.Description = existing.Description;
                input.JobType = existing.JobType;
            }
        }

        await _scheduler.SaveAsync(new ScheduledJobDefinition
        {
            Key = key,
            Name = input.Name,
            Description = input.Description ?? string.Empty,
            JobType = input.JobType,
            CronExpression = input.CronExpression,
            ParametersJson = parametersJson,
            IsEnabled = input.IsEnabled
        }, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        await _scheduler.SyncAsync();

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            ActorEmail = User.Identity?.Name ?? string.Empty,
            Action = "Jobs.ScheduleUpdated",
            EntityType = "ScheduledJob",
            EntityId = key,
            Summary = $"{(isCustom ? "Created" : "Updated")} schedule for {input.Name}.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("Job schedule saved for {JobKey} by {Actor}.", key, User.Identity?.Name ?? "unknown");

        TempData["JobsAlertMessage"] = "Schedule saved successfully.";
        TempData["JobsAlertStyle"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("run/{jobKey}")]
    [Authorize(Policy = PermissionPolicies.JobsRun)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(string jobKey)
    {
        var exists = await _dbContext.ScheduledJobs.AsNoTracking()
            .AnyAsync(job => job.Key == jobKey);

        if (!exists)
        {
            TempData["JobsAlertMessage"] = "Unknown job. Please refresh and try again.";
            TempData["JobsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString();
        _backgroundJobs.Enqueue<BackgroundJobRunner>(job => job.RunScheduledJobAsync(jobKey, correlationId));
        TempData["JobsAlertMessage"] = "Job queued. Check history for completion.";
        TempData["JobsAlertStyle"] = "success";

        _logger.LogInformation("Job run queued for {JobKey} by {Actor}.", jobKey, User.Identity?.Name ?? "unknown");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("toggle/{jobKey}")]
    [Authorize(Policy = PermissionPolicies.JobsScheduleManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string jobKey, bool enable)
    {
        await _scheduler.ToggleAsync(jobKey, enable, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        await _scheduler.SyncAsync();

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            ActorEmail = User.Identity?.Name ?? string.Empty,
            Action = "Jobs.ScheduleUpdated",
            EntityType = "ScheduledJob",
            EntityId = jobKey,
            Summary = enable ? "Resumed scheduled job." : "Paused scheduled job.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("Job schedule {Action} for {JobKey} by {Actor}.",
            enable ? "enabled" : "disabled",
            jobKey,
            User.Identity?.Name ?? "unknown");

        TempData["JobsAlertMessage"] = enable ? "Job schedule resumed." : "Job schedule paused.";
        TempData["JobsAlertStyle"] = "info";
        return RedirectToAction(nameof(Index));
    }

    private async Task<JobsViewModel> BuildViewModelAsync()
    {
        List<RecurringJobDto> recurringJobs = new();
        try
        {
            using var connection = JobStorage.Current.GetConnection();
            recurringJobs = connection.GetRecurringJobs();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load recurring jobs from Hangfire storage.");
        }

        var history = await _dbContext.JobExecutionLogs.AsNoTracking()
            .OrderByDescending(log => log.StartedAtUtc)
            .Take(50)
            .ToListAsync();

        var latestByJob = history
            .GroupBy(log => log.JobKey)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(log => log.StartedAtUtc).First());

        var definitions = await _scheduler.GetDefinitionsAsync();
        var builtInKeys = JobCatalog.BuiltIn.Select(job => job.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var jobs = definitions.Select(definition =>
        {
            latestByJob.TryGetValue(definition.Key, out var lastRun);
            var recurring = recurringJobs.FirstOrDefault(r => r.Id == definition.Key);
            var scheduleLabel = BuildScheduleLabel(definition.CronExpression);

            return new JobScheduleViewModel
            {
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                JobType = definition.JobType,
                CronExpression = definition.CronExpression,
                ScheduleLabel = scheduleLabel,
                IsEnabled = definition.IsEnabled,
                IsCustom = !builtInKeys.Contains(definition.Key),
                LastRunUtc = lastRun?.StartedAtUtc,
                LastStatus = lastRun?.Status,
                LastSummary = lastRun?.Summary,
                LastError = lastRun?.Error,
                NextRunUtc = recurring?.NextExecution
            };
        })
        .OrderBy(job => job.IsCustom)
        .ThenBy(job => job.Name)
        .ToList();

        var historyItems = history.Select(log => new JobHistoryItemViewModel
        {
            JobKey = log.JobKey,
            JobName = jobs.FirstOrDefault(j => j.Key == log.JobKey)?.Name ?? log.JobKey,
            StartedAtUtc = log.StartedAtUtc,
            FinishedAtUtc = log.FinishedAtUtc,
            Status = log.Status,
            Summary = log.Summary,
            Error = log.Error,
            DurationLabel = BuildDurationLabel(log.StartedAtUtc, log.FinishedAtUtc),
            CorrelationId = log.CorrelationId
        }).ToList();

        return new JobsViewModel
        {
            Jobs = jobs,
            History = historyItems,
            AlertMessage = TempData["JobsAlertMessage"] as string,
            AlertStyle = TempData["JobsAlertStyle"] as string ?? "info",
            CanRun = await _permissionService.HasPermissionAsync(User, PermissionKeys.JobsRun),
            CanManageSchedules = await _permissionService.HasPermissionAsync(User, PermissionKeys.JobsScheduleManage),
            CanManageCustomJobs = await _permissionService.HasPermissionAsync(User, PermissionKeys.JobsCustomManage)
        };
    }

    private static string BuildDurationLabel(DateTime startedUtc, DateTime? finishedUtc)
    {
        if (!finishedUtc.HasValue)
        {
            return "Running";
        }

        var duration = finishedUtc.Value - startedUtc;
        if (duration.TotalMinutes >= 1)
        {
            return $"{duration.TotalMinutes:F1} min";
        }

        return $"{Math.Max(duration.TotalSeconds, 0):F0} sec";
    }

    private static string BuildScheduleLabel(string cron)
        => cron switch
        {
            "0 2 * * *" => "Daily at 02:00 UTC",
            "0 */6 * * *" => "Every 6 hours (UTC)",
            "0 * * * *" => "Hourly (UTC)",
            "0 2 * * 1" => "Weekly (Mon 02:00 UTC)",
            _ => $"{cron} (UTC)"
        };

    private JobEditorViewModel BuildEditorViewModel(JobEditorInputModel input)
    {
        var jobTypes = JobCatalog.Supported.Select(job => new JobTypeOptionViewModel
        {
            Value = job.JobType,
            Label = job.Name,
            Description = job.Description
        }).ToList();

        var parameters = input.Parameters.Any()
            ? input.Parameters
            : new List<JobParameterInputModel>
            {
                new(),
                new(),
                new()
            };

        return new JobEditorViewModel
        {
            Key = input.Key ?? string.Empty,
            Name = input.Name,
            Description = input.Description,
            JobType = input.JobType,
            CronExpression = input.CronExpression,
            IsEnabled = input.IsEnabled,
            IsCustom = input.IsCustom,
            Parameters = parameters,
            JobTypes = jobTypes,
            CronPresets = CronPresets,
            NextRuns = BuildNextRuns(input.CronExpression)
        };
    }

    private static IEnumerable<string> ValidateInput(JobEditorInputModel input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            yield return "Job name is required.";
        }

        if (string.IsNullOrWhiteSpace(input.JobType) || !JobCatalog.IsSupportedJobType(input.JobType))
        {
            yield return "Select a valid job type.";
        }

        if (string.IsNullOrWhiteSpace(input.CronExpression))
        {
            yield return "Cron expression is required.";
        }
        else if (!TryParseCron(input.CronExpression, out _))
        {
            yield return "Cron expression is invalid. Use standard 5-part cron.";
        }
    }

    private static List<JobParameterInputModel> ParseParameters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<JobParameterInputModel>();
        }

        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict is null)
            {
                return new List<JobParameterInputModel>();
            }

            return dict.Select(entry => new JobParameterInputModel
            {
                Key = entry.Key,
                Value = entry.Value
            }).ToList();
        }
        catch
        {
            return new List<JobParameterInputModel>();
        }
    }

    private static string? SerializeParameters(IEnumerable<JobParameterInputModel> parameters)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var param in parameters)
        {
            if (string.IsNullOrWhiteSpace(param.Key))
            {
                continue;
            }

            dict[param.Key.Trim()] = param.Value?.Trim() ?? string.Empty;
        }

        return dict.Count > 0 ? JsonSerializer.Serialize(dict) : null;
    }

    private static IReadOnlyList<string> BuildNextRuns(string cron)
    {
        if (!TryParseCron(cron, out var expression))
        {
            return Array.Empty<string>();
        }

        var now = DateTime.UtcNow;
        var nextRuns = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var next = expression.GetNextOccurrence(now, TimeZoneInfo.Utc);
            if (!next.HasValue)
            {
                break;
            }

            nextRuns.Add(next.Value.ToLocalTime().ToString("f"));
            now = next.Value.AddMinutes(1);
        }

        return nextRuns;
    }

    private static bool TryParseCron(string cron, out CronExpression? expression)
    {
        expression = null;
        try
        {
            expression = CronExpression.Parse(cron, CronFormat.Standard);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
