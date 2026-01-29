using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Maintenance;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.MaintenanceView)]
[Route("admin/maintenance")]
public class MaintenanceController : Controller
{
    private readonly IBackupService _backupService;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IBackupService backupService, IAuditLogger auditLogger, ILogger<MaintenanceController> logger)
    {
        _backupService = backupService;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var vm = BuildViewModel();
        return View(vm);
    }

    [HttpPost("backup")]
    [Authorize(Policy = PermissionPolicies.MaintenanceManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Backup(CancellationToken cancellationToken)
    {
        try
        {
            var backup = await _backupService.CreateBackupAsync(cancellationToken);
            await LogAuditAsync("Maintenance.BackupCreated", "Backup", backup.FileName, $"Created backup {backup.FileName}.");

            TempData["AlertMessage"] = $"Backup created: {backup.FileName}";
            TempData["AlertStyle"] = "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup.");
            TempData["AlertMessage"] = "Backup failed. Check logs and try again.";
            TempData["AlertStyle"] = "danger";
            TempData["AlertDetails"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("download/{fileName}")]
    [Authorize(Policy = PermissionPolicies.MaintenanceManage)]
    public IActionResult Download(string fileName)
    {
        var path = _backupService.ResolveBackupPath(fileName);
        if (path is null)
        {
            return NotFound();
        }

        var safeName = Path.GetFileName(path);
        return PhysicalFile(path, "application/zip", safeName);
    }

    private MaintenanceViewModel BuildViewModel()
    {
        var backups = _backupService.ListBackups(20)
            .Select(info => new BackupItemViewModel
            {
                FileName = info.FileName,
                CreatedAtUtc = info.CreatedAtUtc,
                SizeLabel = FormatBytes(info.SizeBytes)
            })
            .ToList();

        return new MaintenanceViewModel
        {
            AppDataPath = _backupService.AppDataPath,
            BackupsPath = _backupService.BackupDirectory,
            Backups = backups,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            AlertDetails = TempData["AlertDetails"] as string
        };
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

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return $"{kb:F1} KB";
        }

        var mb = kb / 1024d;
        return $"{mb:F1} MB";
    }
}
