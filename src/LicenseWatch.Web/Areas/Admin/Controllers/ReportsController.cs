using System.Security.Claims;
using System.Text.Json;
using Cronos;
using Hangfire;
using Hangfire.Storage;
using LicenseWatch.Core.Entities;
using LicenseWatch.Core.Jobs;
using LicenseWatch.Core.Reports;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Infrastructure.Reports;
using LicenseWatch.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Web.Security;
using LicenseWatch.Infrastructure.Jobs;
using LicenseWatch.Core.Services;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[Route("admin/reports")]
public class ReportsController : Controller
{
    private const int PageSize = 25;
    private static readonly JsonSerializerOptions PresetJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<CronPresetViewModel> CronPresets = new List<CronPresetViewModel>
    {
        new() { Label = "Daily at 06:00 UTC", Expression = "0 6 * * *" },
        new() { Label = "Weekly (Mon 06:00 UTC)", Expression = "0 6 * * 1" },
        new() { Label = "Monthly (1st 06:00 UTC)", Expression = "0 6 1 * *" }
    };

    private readonly AppDbContext _dbContext;
    private readonly IReportsQueryService _reports;
    private readonly IReportExportService _exportService;
    private readonly IJobScheduler _scheduler;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        AppDbContext dbContext,
        IReportsQueryService reports,
        IReportExportService exportService,
        IJobScheduler scheduler,
        IBackgroundJobClient backgroundJobs,
        IPermissionService permissionService,
        ILogger<ReportsController> logger)
    {
        _dbContext = dbContext;
        _reports = reports;
        _exportService = exportService;
        _scheduler = scheduler;
        _backgroundJobs = backgroundJobs;
        _permissionService = permissionService;
        _logger = logger;
    }

    [HttpGet("")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public IActionResult Index()
    {
        var vm = new ReportsLandingViewModel
        {
            Reports = new List<ReportCardViewModel>
            {
                new()
                {
                    Title = "License inventory",
                    Description = "Complete list of licenses with ownership and seat data.",
                    Url = "/admin/reports/licenses",
                    Icon = "bi-archive"
                },
                new()
                {
                    Title = "Expirations",
                    Description = "Track upcoming renewals and days remaining.",
                    Url = "/admin/reports/expirations",
                    Icon = "bi-calendar-event"
                },
                new()
                {
                    Title = "Compliance violations",
                    Description = "Monitor open compliance risks and severity.",
                    Url = "/admin/reports/compliance",
                    Icon = "bi-shield-exclamation"
                },
                new()
                {
                    Title = "Usage summary",
                    Description = "Aggregate usage peaks and averages by license.",
                    Url = "/admin/reports/usage",
                    Icon = "bi-activity"
                },
                new()
                {
                    Title = "Delivery schedules",
                    Description = "Send reports to stakeholders on a recurring cadence.",
                    Url = "/admin/reports/schedules",
                    Icon = "bi-envelope-paper"
                }
            }
        };

        return View(vm);
    }

    [HttpGet("schedules")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> Schedules()
    {
        var definitions = await _dbContext.ScheduledJobs.AsNoTracking()
            .Where(job => job.JobType == JobKeys.ReportDelivery)
            .OrderBy(job => job.Name)
            .ToListAsync();

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
            .Where(log => definitions.Select(d => d.Key).Contains(log.JobKey))
            .OrderByDescending(log => log.StartedAtUtc)
            .ToListAsync();

        var latestByJob = history
            .GroupBy(log => log.JobKey)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(log => log.StartedAtUtc).First());

        var schedules = definitions.Select(definition =>
        {
            var payload = ParseSchedulePayload(definition.ParametersJson);
            latestByJob.TryGetValue(definition.Key, out var lastRun);
            var recurring = recurringJobs.FirstOrDefault(r => r.Id == definition.Key);

            return new ReportScheduleItemViewModel
            {
                Key = definition.Key,
                Name = definition.Name,
                ReportName = ResolveReportName(payload?.ReportKey),
                Format = payload?.Format ?? "csv",
                Recipients = payload?.Recipients ?? string.Empty,
                CronExpression = definition.CronExpression,
                ScheduleLabel = BuildScheduleLabel(definition.CronExpression),
                IsEnabled = definition.IsEnabled,
                LastRunUtc = lastRun?.StartedAtUtc,
                LastStatus = lastRun?.Status,
                NextRunUtc = recurring?.NextExecution,
                PresetId = payload?.PresetId
            };
        }).ToList();

        var recentDeliveryLogs = await _dbContext.NotificationLogs.AsNoTracking()
            .Where(log => log.Type == "ReportDelivery")
            .OrderByDescending(log => log.CreatedAtUtc)
            .Take(25)
            .ToListAsync();

        var vm = new ReportScheduleListViewModel
        {
            Schedules = schedules,
            RecentDeliveries = recentDeliveryLogs.Select(log => new ReportDeliveryLogViewModel
            {
                CreatedAtUtc = log.CreatedAtUtc,
                Status = log.Status,
                Subject = log.Subject,
                ToEmail = log.ToEmail
            }).ToList(),
            AlertMessage = TempData["ReportsAlertMessage"] as string,
            AlertStyle = TempData["ReportsAlertStyle"] as string ?? "info",
            CanManage = await _permissionService.HasPermissionAsync(User, PermissionKeys.JobsScheduleManage),
            CanRun = await _permissionService.HasPermissionAsync(User, PermissionKeys.JobsRun)
        };

        return View(vm);
    }

    [HttpGet("schedules/create")]
    [Authorize(Policy = PermissionPolicies.JobsScheduleManage)]
    public async Task<IActionResult> CreateSchedule()
    {
        var vm = await BuildScheduleEditorViewModel(new ReportScheduleEditorViewModel
        {
            IsEnabled = true,
            CronExpression = CronPresets[0].Expression,
            Format = "excel"
        });

        return View("ScheduleEdit", vm);
    }

    [HttpGet("schedules/edit/{jobKey}")]
    [Authorize(Policy = PermissionPolicies.JobsScheduleManage)]
    public async Task<IActionResult> EditSchedule(string jobKey)
    {
        var definition = await _dbContext.ScheduledJobs.AsNoTracking()
            .FirstOrDefaultAsync(job => job.Key == jobKey && job.JobType == JobKeys.ReportDelivery);

        if (definition is null)
        {
            TempData["ReportsAlertMessage"] = "Schedule not found.";
            TempData["ReportsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Schedules));
        }

        var payload = ParseSchedulePayload(definition.ParametersJson);
        var vm = await BuildScheduleEditorViewModel(new ReportScheduleEditorViewModel
        {
            Key = definition.Key,
            Name = definition.Name,
            ReportKey = payload?.ReportKey ?? ReportKeys.LicenseInventory,
            PresetId = payload?.PresetId,
            Format = payload?.Format ?? "csv",
            Recipients = payload?.Recipients ?? string.Empty,
            CronExpression = definition.CronExpression,
            IsEnabled = definition.IsEnabled
        });

        return View("ScheduleEdit", vm);
    }

    [HttpPost("schedules/save")]
    [Authorize(Policy = PermissionPolicies.JobsScheduleManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSchedule(ReportScheduleEditorViewModel input)
    {
        var errors = ValidateSchedule(input).ToList();
        if (errors.Count > 0)
        {
            var vmInvalid = await BuildScheduleEditorViewModel(input);
            vmInvalid.AlertMessage = "Please fix the highlighted issues.";
            vmInvalid.AlertStyle = "danger";
            vmInvalid.AlertDetails = string.Join(Environment.NewLine, errors);
            return View("ScheduleEdit", vmInvalid);
        }

        var key = string.IsNullOrWhiteSpace(input.Key)
            ? $"report-{Guid.NewGuid():N}"
            : input.Key.Trim();

        var filtersJson = await ResolveFiltersJsonAsync(input.ReportKey, input.PresetId);
        var parametersJson = JsonSerializer.Serialize(new ReportSchedulePayload(
            input.ReportKey,
            input.Format,
            input.Recipients,
            input.PresetId,
            filtersJson), PresetJsonOptions);

        await _scheduler.SaveAsync(new ScheduledJobDefinition
        {
            Key = key,
            Name = input.Name.Trim(),
            Description = $"Scheduled report: {ResolveReportName(input.ReportKey)}",
            JobType = JobKeys.ReportDelivery,
            CronExpression = input.CronExpression,
            ParametersJson = parametersJson,
            IsEnabled = input.IsEnabled
        }, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);

        await _scheduler.SyncAsync();

        TempData["ReportsAlertMessage"] = string.IsNullOrWhiteSpace(input.Key)
            ? "Schedule created."
            : "Schedule updated.";
        TempData["ReportsAlertStyle"] = "success";
        return RedirectToAction(nameof(Schedules));
    }

    [HttpPost("schedules/run/{jobKey}")]
    [Authorize(Policy = PermissionPolicies.JobsRun)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunSchedule(string jobKey)
    {
        var exists = await _dbContext.ScheduledJobs.AsNoTracking()
            .AnyAsync(job => job.Key == jobKey && job.JobType == JobKeys.ReportDelivery);

        if (!exists)
        {
            TempData["ReportsAlertMessage"] = "Schedule not found.";
            TempData["ReportsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Schedules));
        }

        var correlationId = HttpContext.Items["CorrelationId"]?.ToString();
        _backgroundJobs.Enqueue<BackgroundJobRunner>(job => job.RunScheduledJobAsync(jobKey, correlationId));
        TempData["ReportsAlertMessage"] = "Report delivery queued.";
        TempData["ReportsAlertStyle"] = "success";
        return RedirectToAction(nameof(Schedules));
    }

    [HttpPost("schedules/toggle/{jobKey}")]
    [Authorize(Policy = PermissionPolicies.JobsScheduleManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSchedule(string jobKey, bool enable)
    {
        await _scheduler.ToggleAsync(jobKey, enable, User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        await _scheduler.SyncAsync();

        TempData["ReportsAlertMessage"] = enable ? "Schedule enabled." : "Schedule paused.";
        TempData["ReportsAlertStyle"] = "info";
        return RedirectToAction(nameof(Schedules));
    }

    [HttpGet("licenses")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> Licenses(Guid? categoryId, string? vendor, string? status, DateTime? expiresFrom, DateTime? expiresTo, int page = 1)
    {
        page = ClampPage(page);
        NormalizeDateRange(ref expiresFrom, ref expiresTo);
        var filter = new LicenseReportFilter(
            categoryId,
            vendor,
            status,
            ToDateOnly(expiresFrom),
            ToDateOnly(expiresTo));

        var results = await _reports.GetLicenseInventoryAsync(filter, page, PageSize);
        var categories = await LoadCategories();
        var presets = await LoadPresets(ReportKeys.LicenseInventory);

        var vm = new LicenseReportViewModel
        {
            CategoryId = categoryId,
            Vendor = vendor,
            Status = status,
            ExpiresFrom = expiresFrom,
            ExpiresTo = expiresTo,
            Categories = categories,
            StatusOptions = new[] { "Good", "Warning", "Critical", "Expired", "Unknown" },
            Results = results,
            LastRefreshedUtc = DateTime.UtcNow,
            Presets = presets,
            AlertMessage = TempData["ReportsAlertMessage"] as string,
            AlertStyle = TempData["ReportsAlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("licenses/export")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> LicensesExport(Guid? categoryId, string? vendor, string? status, DateTime? expiresFrom, DateTime? expiresTo, string format = "csv")
    {
        NormalizeDateRange(ref expiresFrom, ref expiresTo);
        var filter = new LicenseReportFilter(
            categoryId,
            vendor,
            status,
            ToDateOnly(expiresFrom),
            ToDateOnly(expiresTo));

        try
        {
            _logger.LogInformation("License inventory export requested. Format={Format}", format);
            var rows = await _reports.GetLicenseInventoryExportAsync(filter);
            var result = format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                ? _exportService.ExportLicenseInventoryExcel(rows, BuildFileName("license-inventory", "xlsx"))
                : _exportService.ExportLicenseInventoryCsv(rows, BuildFileName("license-inventory", "csv"));

            _logger.LogInformation("License inventory export completed. Rows={RowCount}", rows.Count);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License inventory export failed.");
            TempData["ReportsAlertMessage"] = "Export failed. Please try again or check logs.";
            TempData["ReportsAlertStyle"] = "danger";
            return RedirectToAction(nameof(Licenses), new { categoryId, vendor, status, expiresFrom, expiresTo });
        }
    }

    [HttpGet("expirations")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> Expirations(Guid? categoryId, int? expiringDays, DateTime? expiresFrom, DateTime? expiresTo, int page = 1)
    {
        page = ClampPage(page);
        NormalizeDateRange(ref expiresFrom, ref expiresTo);
        expiringDays = NormalizeExpiringDays(expiringDays);
        var filter = new ExpirationReportFilter(
            categoryId,
            expiringDays,
            ToDateOnly(expiresFrom),
            ToDateOnly(expiresTo));

        var results = await _reports.GetExpirationReportAsync(filter, page, PageSize);
        var categories = await LoadCategories();
        var presets = await LoadPresets(ReportKeys.Expirations);

        var vm = new ExpirationReportViewModel
        {
            CategoryId = categoryId,
            ExpiringDays = expiringDays,
            ExpiresFrom = expiresFrom,
            ExpiresTo = expiresTo,
            Categories = categories,
            Results = results,
            LastRefreshedUtc = DateTime.UtcNow,
            Presets = presets,
            AlertMessage = TempData["ReportsAlertMessage"] as string,
            AlertStyle = TempData["ReportsAlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("expirations/export")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> ExpirationsExport(Guid? categoryId, int? expiringDays, DateTime? expiresFrom, DateTime? expiresTo, string format = "csv")
    {
        NormalizeDateRange(ref expiresFrom, ref expiresTo);
        expiringDays = NormalizeExpiringDays(expiringDays);
        var filter = new ExpirationReportFilter(
            categoryId,
            expiringDays,
            ToDateOnly(expiresFrom),
            ToDateOnly(expiresTo));

        try
        {
            _logger.LogInformation("Expiration report export requested. Format={Format}", format);
            var rows = await _reports.GetExpirationReportExportAsync(filter);
            var result = format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                ? _exportService.ExportExpirationExcel(rows, BuildFileName("expirations", "xlsx"))
                : _exportService.ExportExpirationCsv(rows, BuildFileName("expirations", "csv"));

            _logger.LogInformation("Expiration report export completed. Rows={RowCount}", rows.Count);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Expiration report export failed.");
            TempData["ReportsAlertMessage"] = "Export failed. Please try again or check logs.";
            TempData["ReportsAlertStyle"] = "danger";
            return RedirectToAction(nameof(Expirations), new { categoryId, expiringDays, expiresFrom, expiresTo });
        }
    }

    [HttpGet("compliance")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> Compliance(string? status, string? severity, string? rule, int page = 1)
    {
        page = ClampPage(page);
        var filter = new ComplianceReportFilter(status, severity, rule);
        var results = await _reports.GetComplianceReportAsync(filter, page, PageSize);
        var presets = await LoadPresets(ReportKeys.Compliance);

        var ruleOptions = await _dbContext.ComplianceViolations.AsNoTracking()
            .Select(v => v.RuleKey)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync();

        var vm = new ComplianceReportViewModel
        {
            Status = status,
            Severity = severity,
            Rule = rule,
            StatusOptions = new[] { "Open", "Acknowledged", "Resolved" },
            SeverityOptions = new[] { "Critical", "Warning", "Info" },
            RuleOptions = ruleOptions,
            Results = results,
            LastRefreshedUtc = DateTime.UtcNow,
            Presets = presets,
            AlertMessage = TempData["ReportsAlertMessage"] as string,
            AlertStyle = TempData["ReportsAlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("compliance/export")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> ComplianceExport(string? status, string? severity, string? rule, string format = "csv")
    {
        var filter = new ComplianceReportFilter(status, severity, rule);
        try
        {
            _logger.LogInformation("Compliance report export requested. Format={Format}", format);
            var rows = await _reports.GetComplianceReportExportAsync(filter);
            var result = format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                ? _exportService.ExportComplianceExcel(rows, BuildFileName("compliance", "xlsx"))
                : _exportService.ExportComplianceCsv(rows, BuildFileName("compliance", "csv"));

            _logger.LogInformation("Compliance report export completed. Rows={RowCount}", rows.Count);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compliance report export failed.");
            TempData["ReportsAlertMessage"] = "Export failed. Please try again or check logs.";
            TempData["ReportsAlertStyle"] = "danger";
            return RedirectToAction(nameof(Compliance), new { status, severity, rule });
        }
    }

    [HttpGet("usage")]
    [HttpGet("/admin/usage")]
    [HttpGet("/admin/analytics")]
    [Authorize(Policy = PermissionPolicies.UsageView)]
    public async Task<IActionResult> Usage(Guid? categoryId, Guid? licenseId, DateTime? from, DateTime? to, int page = 1)
    {
        page = ClampPage(page);
        NormalizeDateRange(ref from, ref to);
        var filter = new UsageReportFilter(
            categoryId,
            licenseId,
            ToDateOnly(from),
            ToDateOnly(to));

        var results = await _reports.GetUsageReportAsync(filter, page, PageSize);
        var categories = await LoadCategories();
        var licenses = await LoadLicenses();
        var presets = await LoadPresets(ReportKeys.Usage);

        var vm = new UsageReportViewModel
        {
            CategoryId = categoryId,
            LicenseId = licenseId,
            From = from,
            To = to,
            Categories = categories,
            Licenses = licenses,
            Results = results,
            LastRefreshedUtc = DateTime.UtcNow,
            Presets = presets,
            AlertMessage = TempData["ReportsAlertMessage"] as string,
            AlertStyle = TempData["ReportsAlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("usage/export")]
    [Authorize(Policy = PermissionPolicies.UsageView)]
    public async Task<IActionResult> UsageExport(Guid? categoryId, Guid? licenseId, DateTime? from, DateTime? to, string format = "csv")
    {
        NormalizeDateRange(ref from, ref to);
        var filter = new UsageReportFilter(
            categoryId,
            licenseId,
            ToDateOnly(from),
            ToDateOnly(to));

        try
        {
            _logger.LogInformation("Usage report export requested. Format={Format}", format);
            var rows = await _reports.GetUsageReportExportAsync(filter);
            var result = format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                ? _exportService.ExportUsageExcel(rows, BuildFileName("usage", "xlsx"))
                : _exportService.ExportUsageCsv(rows, BuildFileName("usage", "csv"));

            _logger.LogInformation("Usage report export completed. Rows={RowCount}", rows.Count);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Usage report export failed.");
            TempData["ReportsAlertMessage"] = "Export failed. Please try again or check logs.";
            TempData["ReportsAlertStyle"] = "danger";
            return RedirectToAction(nameof(Usage), new { categoryId, licenseId, from, to });
        }
    }

    [HttpGet("presets/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    public async Task<IActionResult> ApplyPreset(Guid id)
    {
        var preset = await _dbContext.ReportPresets.FindAsync(id);
        if (preset is null)
        {
            TempData["ReportsAlertMessage"] = "Preset not found.";
            TempData["ReportsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        preset.LastUsedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        try
        {
            return preset.ReportKey switch
            {
                ReportKeys.LicenseInventory => RedirectToAction(nameof(Licenses), BuildRouteValues(JsonSerializer.Deserialize<LicensePresetPayload>(preset.FiltersJson, PresetJsonOptions))),
                ReportKeys.Expirations => RedirectToAction(nameof(Expirations), BuildRouteValues(JsonSerializer.Deserialize<ExpirationPresetPayload>(preset.FiltersJson, PresetJsonOptions))),
                ReportKeys.Compliance => RedirectToAction(nameof(Compliance), BuildRouteValues(JsonSerializer.Deserialize<CompliancePresetPayload>(preset.FiltersJson, PresetJsonOptions))),
                ReportKeys.Usage => RedirectToAction(nameof(Usage), BuildRouteValues(JsonSerializer.Deserialize<UsagePresetPayload>(preset.FiltersJson, PresetJsonOptions))),
                _ => RedirectToAction(nameof(Index))
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply report preset {PresetId}", id);
            TempData["ReportsAlertMessage"] = "Failed to apply preset. Try saving it again.";
            TempData["ReportsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("presets/save")]
    [Authorize(Policy = PermissionPolicies.ReportsView)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePreset(ReportPresetInputModel input)
    {
        if (string.IsNullOrWhiteSpace(input.ReportKey) || string.IsNullOrWhiteSpace(input.Name))
        {
            TempData["ReportsAlertMessage"] = "Preset name is required.";
            TempData["ReportsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var payload = BuildPresetPayload(input.ReportKey, input);
        if (payload is null)
        {
            TempData["ReportsAlertMessage"] = "Invalid report preset.";
            TempData["ReportsAlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        var now = DateTime.UtcNow;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var normalizedName = input.Name.Trim();
        var filtersJson = JsonSerializer.Serialize(payload, PresetJsonOptions);

        var preset = await _dbContext.ReportPresets
            .FirstOrDefaultAsync(p => p.ReportKey == input.ReportKey && p.Name == normalizedName);

        if (preset is null)
        {
            preset = new ReportPreset
            {
                Id = Guid.NewGuid(),
                ReportKey = input.ReportKey,
                Name = normalizedName,
                FiltersJson = filtersJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = userId,
                UpdatedByUserId = userId
            };
            _dbContext.ReportPresets.Add(preset);
        }
        else
        {
            preset.FiltersJson = filtersJson;
            preset.UpdatedAtUtc = now;
            preset.UpdatedByUserId = userId;
        }

        await _dbContext.SaveChangesAsync();

        TempData["ReportsAlertMessage"] = "Preset saved.";
        TempData["ReportsAlertStyle"] = "success";
        return RedirectToReport(input.ReportKey, input);
    }

    private static object? BuildPresetPayload(string reportKey, ReportPresetInputModel input)
    {
        return reportKey switch
        {
            ReportKeys.LicenseInventory => new LicensePresetPayload(
                input.CategoryId,
                input.Vendor,
                input.Status,
                input.ExpiresFrom,
                input.ExpiresTo),
            ReportKeys.Expirations => new ExpirationPresetPayload(
                input.CategoryId,
                input.ExpiringDays,
                input.ExpiresFrom,
                input.ExpiresTo),
            ReportKeys.Compliance => new CompliancePresetPayload(
                input.Status,
                input.Severity,
                input.Rule),
            ReportKeys.Usage => new UsagePresetPayload(
                input.CategoryId,
                input.LicenseId,
                input.From,
                input.To),
            _ => null
        };
    }

    private IActionResult RedirectToReport(string reportKey, ReportPresetInputModel input)
    {
        return reportKey switch
        {
            ReportKeys.LicenseInventory => RedirectToAction(nameof(Licenses), new
            {
                categoryId = input.CategoryId,
                vendor = input.Vendor,
                status = input.Status,
                expiresFrom = ToDateString(input.ExpiresFrom),
                expiresTo = ToDateString(input.ExpiresTo)
            }),
            ReportKeys.Expirations => RedirectToAction(nameof(Expirations), new
            {
                categoryId = input.CategoryId,
                expiringDays = input.ExpiringDays,
                expiresFrom = ToDateString(input.ExpiresFrom),
                expiresTo = ToDateString(input.ExpiresTo)
            }),
            ReportKeys.Compliance => RedirectToAction(nameof(Compliance), new
            {
                status = input.Status,
                severity = input.Severity,
                rule = input.Rule
            }),
            ReportKeys.Usage => RedirectToAction(nameof(Usage), new
            {
                categoryId = input.CategoryId,
                licenseId = input.LicenseId,
                from = ToDateString(input.From),
                to = ToDateString(input.To)
            }),
            _ => RedirectToAction(nameof(Index))
        };
    }

    private static DateOnly? ToDateOnly(DateTime? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private static string? ToDateString(DateTime? value)
        => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : null;

    private static object BuildRouteValues(LicensePresetPayload? payload)
        => payload is null
            ? new { }
            : new
            {
                categoryId = payload.CategoryId,
                vendor = payload.Vendor,
                status = payload.Status,
                expiresFrom = ToDateString(payload.ExpiresFrom),
                expiresTo = ToDateString(payload.ExpiresTo)
            };

    private static object BuildRouteValues(ExpirationPresetPayload? payload)
        => payload is null
            ? new { }
            : new
            {
                categoryId = payload.CategoryId,
                expiringDays = payload.ExpiringDays,
                expiresFrom = ToDateString(payload.ExpiresFrom),
                expiresTo = ToDateString(payload.ExpiresTo)
            };

    private static object BuildRouteValues(CompliancePresetPayload? payload)
        => payload is null
            ? new { }
            : new
            {
                status = payload.Status,
                severity = payload.Severity,
                rule = payload.Rule
            };

    private static object BuildRouteValues(UsagePresetPayload? payload)
        => payload is null
            ? new { }
            : new
            {
                categoryId = payload.CategoryId,
                licenseId = payload.LicenseId,
                from = ToDateString(payload.From),
                to = ToDateString(payload.To)
            };

    private static string BuildFileName(string reportKey, string extension)
        => $"report-{reportKey}-v{BuildInfo.Version}-{DateTime.UtcNow:yyyyMMdd-HHmm}.{extension}";

    private static int ClampPage(int page)
        => Math.Clamp(page, 1, 200);

    private static void NormalizeDateRange(ref DateTime? from, ref DateTime? to)
    {
        if (!from.HasValue || !to.HasValue)
        {
            return;
        }

        if (from > to)
        {
            (from, to) = (to, from);
        }
    }

    private static int? NormalizeExpiringDays(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value is > 0 and <= 365 ? value : null;
    }

    private async Task<ReportScheduleEditorViewModel> BuildScheduleEditorViewModel(ReportScheduleEditorViewModel input)
    {
        var presets = await LoadPresets(input.ReportKey);
        var reportOptions = new List<ReportOptionViewModel>
        {
            new() { Key = ReportKeys.LicenseInventory, Name = "License inventory" },
            new() { Key = ReportKeys.Expirations, Name = "Expirations" },
            new() { Key = ReportKeys.Compliance, Name = "Compliance violations" },
            new() { Key = ReportKeys.Usage, Name = "Usage summary" }
        };

        return new ReportScheduleEditorViewModel
        {
            Key = input.Key,
            Name = input.Name,
            ReportKey = string.IsNullOrWhiteSpace(input.ReportKey) ? ReportKeys.LicenseInventory : input.ReportKey,
            PresetId = input.PresetId,
            Format = string.IsNullOrWhiteSpace(input.Format) ? "csv" : input.Format,
            Recipients = input.Recipients,
            CronExpression = input.CronExpression,
            IsEnabled = input.IsEnabled,
            Presets = presets,
            Reports = reportOptions,
            CronPresets = CronPresets,
            NextRuns = BuildNextRuns(input.CronExpression),
            AlertMessage = input.AlertMessage,
            AlertStyle = input.AlertStyle,
            AlertDetails = input.AlertDetails
        };
    }

    private static IEnumerable<string> ValidateSchedule(ReportScheduleEditorViewModel input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            yield return "Schedule name is required.";
        }

        if (string.IsNullOrWhiteSpace(input.ReportKey))
        {
            yield return "Select a report to deliver.";
        }

        if (string.IsNullOrWhiteSpace(input.Recipients))
        {
            yield return "Enter at least one recipient.";
        }

        if (string.IsNullOrWhiteSpace(input.CronExpression) || !TryParseCron(input.CronExpression, out _))
        {
            yield return "Cron expression is invalid.";
        }
    }

    private async Task<string?> ResolveFiltersJsonAsync(string reportKey, Guid? presetId)
    {
        if (!presetId.HasValue)
        {
            return null;
        }

        var preset = await _dbContext.ReportPresets.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == presetId.Value && p.ReportKey == reportKey);

        return preset?.FiltersJson;
    }

    private static ReportSchedulePayload? ParseSchedulePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReportSchedulePayload>(json, PresetJsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveReportName(string? reportKey)
        => reportKey switch
        {
            ReportKeys.LicenseInventory => "License inventory",
            ReportKeys.Expirations => "Expirations",
            ReportKeys.Compliance => "Compliance violations",
            ReportKeys.Usage => "Usage summary",
            _ => reportKey ?? "Report"
        };

    private static string BuildScheduleLabel(string cron)
        => cron switch
        {
            "0 6 * * *" => "Daily at 06:00 UTC",
            "0 6 * * 1" => "Weekly (Mon 06:00 UTC)",
            "0 6 1 * *" => "Monthly (1st 06:00 UTC)",
            _ => $"{cron} (UTC)"
        };

    private static IReadOnlyList<string> BuildNextRuns(string cron)
    {
        if (!TryParseCron(cron, out var expression))
        {
            return Array.Empty<string>();
        }

        var now = DateTime.UtcNow;
        var nextRuns = new List<string>();
        for (var i = 0; i < 4; i++)
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

    private sealed record ReportSchedulePayload(
        string ReportKey,
        string Format,
        string? Recipients,
        Guid? PresetId,
        string? FiltersJson);

    private async Task<IReadOnlyCollection<CategoryOption>> LoadCategories()
    {
        var categories = await _dbContext.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return categories.Select(c => new CategoryOption { Id = c.Id, Name = c.Name }).ToList();
    }

    private async Task<IReadOnlyCollection<LicenseOption>> LoadLicenses()
    {
        var licenses = await _dbContext.Licenses.AsNoTracking()
            .OrderBy(l => l.Name)
            .ToListAsync();

        return licenses.Select(l => new LicenseOption { Id = l.Id, Name = l.Name }).ToList();
    }

    private async Task<IReadOnlyList<ReportPresetViewModel>> LoadPresets(string reportKey)
    {
        var presets = await _dbContext.ReportPresets.AsNoTracking()
            .Where(p => p.ReportKey == reportKey)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return presets.Select(p => new ReportPresetViewModel
        {
            Id = p.Id,
            Name = p.Name,
            LastUsedAtUtc = p.LastUsedAtUtc
        }).ToList();
    }

    private sealed record LicensePresetPayload(
        Guid? CategoryId,
        string? Vendor,
        string? Status,
        DateTime? ExpiresFrom,
        DateTime? ExpiresTo);

    private sealed record ExpirationPresetPayload(
        Guid? CategoryId,
        int? ExpiringDays,
        DateTime? ExpiresFrom,
        DateTime? ExpiresTo);

    private sealed record CompliancePresetPayload(
        string? Status,
        string? Severity,
        string? Rule);

    private sealed record UsagePresetPayload(
        Guid? CategoryId,
        Guid? LicenseId,
        DateTime? From,
        DateTime? To);
}

public class ReportPresetInputModel
{
    public string ReportKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public string? Vendor { get; set; }
    public string? Status { get; set; }
    public DateTime? ExpiresFrom { get; set; }
    public DateTime? ExpiresTo { get; set; }
    public int? ExpiringDays { get; set; }
    public string? Severity { get; set; }
    public string? Rule { get; set; }
    public Guid? LicenseId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
