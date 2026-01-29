using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Core.Services;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Email;
using LicenseWatch.Infrastructure.Jobs;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Infrastructure.Storage;
using LicenseWatch.Web.Models.Admin;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Web.Security;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.LicensesView)]
[Route("admin/licenses")]
public class LicensesController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IAttachmentStorage _attachmentStorage;
    private readonly IAuditLogger _auditLogger;
    private readonly IEmailNotificationService _notifications;
    private readonly IBootstrapSettingsStore _settingsStore;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<LicensesController> _logger;

    public LicensesController(
        AppDbContext dbContext,
        IAttachmentStorage attachmentStorage,
        IAuditLogger auditLogger,
        IEmailNotificationService notifications,
        IBootstrapSettingsStore settingsStore,
        IBackgroundJobClient backgroundJobs,
        ILogger<LicensesController> logger)
    {
        _dbContext = dbContext;
        _attachmentStorage = attachmentStorage;
        _auditLogger = auditLogger;
        _notifications = notifications;
        _settingsStore = settingsStore;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search = null,
        Guid? categoryId = null,
        string? status = null,
        int? expiringDays = null,
        bool overAllocated = false,
        string? vendor = null,
        DateTime? expiresFrom = null,
        DateTime? expiresTo = null,
        bool overuseRisk = false,
        bool criticalRisk = false)
    {
        var thresholds = await GetSystemThresholdsAsync();
        var query = _dbContext.Licenses.Include(l => l.Category).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            if (search.Length > 100)
            {
                search = search[..100];
            }

            query = query.Where(l => l.Name.Contains(search) || (l.Vendor != null && l.Vendor.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(vendor))
        {
            vendor = vendor.Trim();
            if (vendor.Length > 100)
            {
                vendor = vendor[..100];
            }

            query = query.Where(l => l.Vendor != null && l.Vendor.Contains(vendor));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(l => l.CategoryId == categoryId.Value);
        }

        var statusFilter = !criticalRisk && !string.IsNullOrWhiteSpace(status)
            ? status.Trim()
            : null;

        if (expiringDays.HasValue)
        {
            expiringDays = expiringDays.Value is > 0 and <= 365 ? expiringDays : null;
        }

        if (expiringDays.HasValue)
        {
            var now = DateTime.UtcNow.Date;
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date > now && l.ExpiresOnUtc.Value.Date <= now.AddDays(expiringDays.Value));
        }

        if (expiresFrom.HasValue && expiresTo.HasValue && expiresFrom.Value.Date > expiresTo.Value.Date)
        {
            (expiresFrom, expiresTo) = (expiresTo, expiresFrom);
        }

        if (expiresFrom.HasValue)
        {
            var from = expiresFrom.Value.Date;
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date >= from);
        }

        if (expiresTo.HasValue)
        {
            var to = expiresTo.Value.Date;
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date <= to);
        }

        if (overAllocated)
        {
            query = query.Where(l => l.SeatsAssigned.HasValue && l.SeatsPurchased.HasValue && l.SeatsAssigned.Value > l.SeatsPurchased.Value);
        }

        if (!criticalRisk && overuseRisk)
        {
            var windowStart = DateTime.UtcNow.Date.AddDays(-30);

            var overuseIds = _dbContext.UsageDailySummaries.AsNoTracking()
                .Where(u => u.UsageDateUtc >= windowStart)
                .GroupBy(u => u.LicenseId)
                .Select(g => new { LicenseId = g.Key, Peak = g.Max(x => x.MaxSeatsUsed) })
                .Join(
                    _dbContext.Licenses.AsNoTracking().Where(l => l.SeatsPurchased.HasValue && l.SeatsPurchased.Value > 0),
                    usage => usage.LicenseId,
                    license => license.Id,
                    (usage, license) => new { usage.LicenseId, usage.Peak, license.SeatsPurchased })
                .Where(x => x.Peak > x.SeatsPurchased!.Value)
                .Select(x => x.LicenseId);

            query = query.Where(l => overuseIds.Contains(l.Id));
        }

        if (criticalRisk)
        {
            var now = DateTime.UtcNow.Date;
            var windowStart = now.AddDays(-30);

            var overuseIds = _dbContext.UsageDailySummaries.AsNoTracking()
                .Where(u => u.UsageDateUtc >= windowStart)
                .GroupBy(u => u.LicenseId)
                .Select(g => new { LicenseId = g.Key, Peak = g.Max(x => x.MaxSeatsUsed) })
                .Join(
                    _dbContext.Licenses.AsNoTracking().Where(l => l.SeatsPurchased.HasValue && l.SeatsPurchased.Value > 0),
                    usage => usage.LicenseId,
                    license => license.Id,
                    (usage, license) => new { usage.LicenseId, usage.Peak, license.SeatsPurchased })
                .Where(x => x.Peak > x.SeatsPurchased!.Value)
                .Select(x => x.LicenseId);

            query = query.Where(l =>
                (l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date <= now)
                || overuseIds.Contains(l.Id));
        }

        var licenses = await query.OrderBy(l => l.Name).ToListAsync();
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var filtered = new List<LicenseWatch.Core.Entities.License>();
            foreach (var license in licenses)
            {
                var resolved = ResolveThresholds(license.UseCustomThresholds, license.CriticalThresholdDays, license.WarningThresholdDays, thresholds);
                var computedStatus = LicenseStatusCalculator.ComputeStatus(license.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays);
                if (string.Equals(computedStatus, statusFilter, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(license);
                }
            }

            licenses = filtered;
        }
        var categories = await _dbContext.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();

        var vm = new LicenseListViewModel
        {
            Search = search,
            CategoryId = categoryId,
            Status = status,
            ExpiringDays = expiringDays,
            OverAllocated = overAllocated,
            Categories = categories.Select(c => new CategoryOption { Id = c.Id, Name = c.Name }).ToList(),
            Licenses = licenses.Select(l =>
            {
                var resolved = ResolveThresholds(l.UseCustomThresholds, l.CriticalThresholdDays, l.WarningThresholdDays, thresholds);
                return new LicenseListItemViewModel
                {
                    Id = l.Id,
                    Name = l.Name,
                    Vendor = l.Vendor,
                    CategoryName = l.Category?.Name ?? "Unassigned",
                    ExpiresOnUtc = l.ExpiresOnUtc,
                    Status = LicenseStatusCalculator.ComputeStatus(l.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays)
                };
            }).ToList(),
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("create")]
    [Authorize(Policy = PermissionPolicies.LicensesManage)]
    public async Task<IActionResult> Create()
    {
        var categories = await _dbContext.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        return View(new LicenseFormViewModel
        {
            Categories = categories.Select(c => new CategoryOption { Id = c.Id, Name = c.Name }).ToList(),
            Currency = "USD"
        });
    }

    [HttpPost("create")]
    [Authorize(Policy = PermissionPolicies.LicensesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LicenseFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (model.CategoryId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Category is required.");
        }

        if (model.UseCustomThresholds)
        {
            if (!model.CriticalThresholdDays.HasValue || model.CriticalThresholdDays.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.CriticalThresholdDays), "Critical days must be greater than 0.");
            }

            if (!model.WarningThresholdDays.HasValue || model.WarningThresholdDays.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.WarningThresholdDays), "Warning days must be greater than 0.");
            }

            if (model.CriticalThresholdDays.HasValue && model.WarningThresholdDays.HasValue
                && model.WarningThresholdDays.Value <= model.CriticalThresholdDays.Value)
            {
                ModelState.AddModelError(nameof(model.WarningThresholdDays), "Warning days must be greater than critical days.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.Categories = await LoadCategoryOptions();
            return View(model);
        }

        var thresholds = await GetSystemThresholdsAsync();
        var resolved = ResolveThresholds(model.UseCustomThresholds, model.CriticalThresholdDays, model.WarningThresholdDays, thresholds);

        var license = new License
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Vendor = model.Vendor?.Trim(),
            CategoryId = model.CategoryId,
            SeatsPurchased = model.SeatsPurchased,
            SeatsAssigned = model.SeatsAssigned,
            CostPerSeatMonthly = model.CostPerSeatMonthly,
            Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant(),
            ExpiresOnUtc = model.ExpiresOnUtc?.Date,
            Status = LicenseStatusCalculator.ComputeStatus(model.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays),
            UseCustomThresholds = model.UseCustomThresholds,
            CriticalThresholdDays = model.UseCustomThresholds ? model.CriticalThresholdDays : null,
            WarningThresholdDays = model.UseCustomThresholds ? model.WarningThresholdDays : null,
            Notes = model.Notes?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Licenses.Add(license);
        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("License.Created", "License", license.Id.ToString(), $"Created license {license.Name}");

        await _notifications.NotifyAsync("License.Created", new EmailNotificationContext(
            license.Id,
            "License",
            license.Name,
            $"License created: {license.Name}",
            license.Vendor,
            license.ExpiresOnUtc,
            null,
            null,
            BuildDashboardUrl(),
            User.Identity?.Name), HttpContext.RequestAborted);

        TriggerComplianceEvaluation();
        TempData["AlertMessage"] = "License created. Compliance evaluation triggered.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Details), new { id = license.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var license = await _dbContext.Licenses
            .Include(l => l.Category)
            .Include(l => l.Attachments)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (license is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var audits = await _dbContext.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == id.ToString())
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(10)
            .ToListAsync();

        var lastEvaluatedAt = await _dbContext.ComplianceViolations.AsNoTracking()
            .Where(v => v.LicenseId == id)
            .MaxAsync(v => (DateTime?)v.LastEvaluatedAtUtc);

        var thresholds = await GetSystemThresholdsAsync();
        var resolved = ResolveThresholds(license.UseCustomThresholds, license.CriticalThresholdDays, license.WarningThresholdDays, thresholds);
        var thresholdLabel = license.UseCustomThresholds
            ? $"Custom: {resolved.CriticalDays} / {resolved.WarningDays} days"
            : $"System: {thresholds.CriticalDays} / {thresholds.WarningDays} days";

        var vm = new LicenseDetailViewModel
        {
            Id = license.Id,
            Name = license.Name,
            Vendor = license.Vendor,
            CategoryName = license.Category?.Name ?? "Unassigned",
            ExpiresOnUtc = license.ExpiresOnUtc,
            Status = LicenseStatusCalculator.ComputeStatus(license.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays),
            SeatsPurchased = license.SeatsPurchased,
            SeatsAssigned = license.SeatsAssigned,
            CostPerSeatMonthly = license.CostPerSeatMonthly,
            Currency = license.Currency,
            Notes = license.Notes,
            UseCustomThresholds = license.UseCustomThresholds,
            CriticalThresholdDays = license.CriticalThresholdDays,
            WarningThresholdDays = license.WarningThresholdDays,
            LastEvaluatedAtUtc = lastEvaluatedAt,
            ThresholdLabel = thresholdLabel,
            Attachments = license.Attachments.Select(a => new AttachmentItemViewModel
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                UploadedAtUtc = a.UploadedAtUtc
            }).ToList(),
            AuditLogs = audits.Select(a => new AuditLogItemViewModel
            {
                OccurredAtUtc = a.OccurredAtUtc,
                ActorEmail = a.ActorEmail,
                ActorDisplay = string.IsNullOrWhiteSpace(a.ActorDisplay) ? a.ActorEmail : a.ActorDisplay,
                ActingAs = a.ImpersonatedDisplay,
                IsImpersonated = !string.IsNullOrWhiteSpace(a.ImpersonatedDisplay),
                Action = a.Action,
                Summary = a.Summary,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                EntityIdDisplay = a.EntityId,
                IpAddress = a.IpAddress,
                IpAddressDisplay = string.IsNullOrWhiteSpace(a.IpAddress) ? "—" : a.IpAddress,
                CorrelationId = a.CorrelationId
            }).ToList(),
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            AlertDetails = TempData["AlertDetails"] as string
        };

        return View(vm);
    }

    [HttpGet("{id:guid}/edit")]
    [Authorize(Policy = PermissionPolicies.LicensesManage)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var license = await _dbContext.Licenses.FindAsync(id);
        if (license is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var vm = new LicenseFormViewModel
        {
            Id = license.Id,
            Name = license.Name,
            Vendor = license.Vendor,
            CategoryId = license.CategoryId,
            SeatsPurchased = license.SeatsPurchased,
            SeatsAssigned = license.SeatsAssigned,
            CostPerSeatMonthly = license.CostPerSeatMonthly,
            Currency = string.IsNullOrWhiteSpace(license.Currency) ? "USD" : license.Currency,
            ExpiresOnUtc = license.ExpiresOnUtc,
            Notes = license.Notes,
            UseCustomThresholds = license.UseCustomThresholds,
            CriticalThresholdDays = license.CriticalThresholdDays,
            WarningThresholdDays = license.WarningThresholdDays,
            Categories = await LoadCategoryOptions()
        };

        return View(vm);
    }

    [HttpPost("{id:guid}/edit")]
    [Authorize(Policy = PermissionPolicies.LicensesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, LicenseFormViewModel model)
    {
        var license = await _dbContext.Licenses.FindAsync(id);
        if (license is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
        }

        if (model.CategoryId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "Category is required.");
        }

        if (model.UseCustomThresholds)
        {
            if (!model.CriticalThresholdDays.HasValue || model.CriticalThresholdDays.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.CriticalThresholdDays), "Critical days must be greater than 0.");
            }

            if (!model.WarningThresholdDays.HasValue || model.WarningThresholdDays.Value <= 0)
            {
                ModelState.AddModelError(nameof(model.WarningThresholdDays), "Warning days must be greater than 0.");
            }

            if (model.CriticalThresholdDays.HasValue && model.WarningThresholdDays.HasValue
                && model.WarningThresholdDays.Value <= model.CriticalThresholdDays.Value)
            {
                ModelState.AddModelError(nameof(model.WarningThresholdDays), "Warning days must be greater than critical days.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.Categories = await LoadCategoryOptions();
            return View(model);
        }

        var thresholds = await GetSystemThresholdsAsync();
        var resolved = ResolveThresholds(model.UseCustomThresholds, model.CriticalThresholdDays, model.WarningThresholdDays, thresholds);

        license.Name = model.Name.Trim();
        license.Vendor = model.Vendor?.Trim();
        license.CategoryId = model.CategoryId;
        license.SeatsPurchased = model.SeatsPurchased;
        license.SeatsAssigned = model.SeatsAssigned;
        license.CostPerSeatMonthly = model.CostPerSeatMonthly;
        license.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant();
        license.ExpiresOnUtc = model.ExpiresOnUtc?.Date;
        license.Status = LicenseStatusCalculator.ComputeStatus(model.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays);
        license.UseCustomThresholds = model.UseCustomThresholds;
        license.CriticalThresholdDays = model.UseCustomThresholds ? model.CriticalThresholdDays : null;
        license.WarningThresholdDays = model.UseCustomThresholds ? model.WarningThresholdDays : null;
        license.Notes = model.Notes?.Trim();
        license.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("License.Updated", "License", license.Id.ToString(), $"Updated license {license.Name}");

        await _notifications.NotifyAsync("License.Updated", new EmailNotificationContext(
            license.Id,
            "License",
            license.Name,
            $"License updated: {license.Name}",
            license.Vendor,
            license.ExpiresOnUtc,
            null,
            null,
            BuildDashboardUrl(),
            User.Identity?.Name), HttpContext.RequestAborted);

        TriggerComplianceEvaluation();
        TempData["AlertMessage"] = "License updated. Compliance evaluation triggered.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Details), new { id = license.Id });
    }

    [HttpPost("{id:guid}/delete")]
    [Authorize(Policy = PermissionPolicies.LicensesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var license = await _dbContext.Licenses.Include(l => l.Attachments).FirstOrDefaultAsync(l => l.Id == id);
        if (license is null)
        {
            return RedirectToAction(nameof(Index));
        }

        foreach (var attachment in license.Attachments)
        {
            await _attachmentStorage.DeleteAsync(attachment.StoredFileName);
        }

        _dbContext.Licenses.Remove(license);
        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("License.Deleted", "License", license.Id.ToString(), $"Deleted license {license.Name}");

        await _notifications.NotifyAsync("License.Deleted", new EmailNotificationContext(
            license.Id,
            "License",
            license.Name,
            $"License deleted: {license.Name}",
            license.Vendor,
            license.ExpiresOnUtc,
            null,
            null,
            BuildDashboardUrl(),
            User.Identity?.Name), HttpContext.RequestAborted);

        TempData["AlertMessage"] = "License deleted.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/attachments")]
    [Authorize(Policy = PermissionPolicies.LicensesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file)
    {
        var license = await _dbContext.Licenses.FindAsync(id);
        if (license is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (file is null)
        {
            SetTempAlert("Please select a file to upload.", "warning");
            return RedirectToAction(nameof(Details), new { id });
        }

        var saveResult = await _attachmentStorage.SaveAsync(file);
        if (!saveResult.Success || saveResult.StoredFileName is null)
        {
            SetTempAlert(saveResult.ErrorMessage ?? "Attachment upload failed.", "danger");
            return RedirectToAction(nameof(Details), new { id });
        }

        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            LicenseId = id,
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = saveResult.StoredFileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            UploadedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            UploadedAtUtc = DateTime.UtcNow
        };

        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("Attachment.Uploaded", "Attachment", attachment.Id.ToString(), $"Uploaded {attachment.OriginalFileName}");

        TempData["AlertMessage"] = "Attachment uploaded.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Policy = PermissionPolicies.LicensesManage)]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId)
    {
        var attachment = await _dbContext.Attachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.LicenseId == id);
        if (attachment is null)
        {
            return NotFound();
        }

        var path = _attachmentStorage.GetFilePath(attachment.StoredFileName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        var stream = System.IO.File.OpenRead(path);
        return File(stream, attachment.ContentType, attachment.OriginalFileName);
    }

    private async Task<IReadOnlyCollection<CategoryOption>> LoadCategoryOptions()
    {
        var categories = await _dbContext.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        return categories.Select(c => new CategoryOption { Id = c.Id, Name = c.Name }).ToList();
    }

    private async Task LogAuditAsync(string action, string entityType, string entityId, string summary)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = User.Identity?.Name ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = userId,
            ActorEmail = email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            IpAddress = ip
        });
    }

    private void SetTempAlert(string message, string style)
    {
        TempData["AlertMessage"] = message;
        TempData["AlertStyle"] = style;
    }

    private string BuildDashboardUrl()
        => $"{Request.Scheme}://{Request.Host}/admin";

    private async Task<(int CriticalDays, int WarningDays)> GetSystemThresholdsAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        return LicenseStatusCalculator.NormalizeThresholds(
            settings.Compliance.CriticalDays,
            settings.Compliance.WarningDays);
    }

    private static (int CriticalDays, int WarningDays) ResolveThresholds(
        bool useCustom,
        int? criticalOverride,
        int? warningOverride,
        (int CriticalDays, int WarningDays) systemThresholds)
    {
        if (!useCustom)
        {
            return systemThresholds;
        }

        return LicenseStatusCalculator.NormalizeThresholds(
            criticalOverride ?? systemThresholds.CriticalDays,
            warningOverride ?? systemThresholds.WarningDays);
    }

    private void TriggerComplianceEvaluation()
    {
        try
        {
            var correlationId = HttpContext.Items["CorrelationId"]?.ToString();
            _backgroundJobs.Enqueue<BackgroundJobRunner>(job => job.RunComplianceEvaluationAsync(correlationId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue compliance evaluation.");
        }
    }
}
