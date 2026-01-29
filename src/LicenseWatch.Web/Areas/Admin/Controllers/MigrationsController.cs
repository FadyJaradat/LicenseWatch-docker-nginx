using System.Globalization;
using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.MigrationsManage)]
[Route("admin/migrations")]
public class MigrationsController : Controller
{
    private readonly ApplicationDbContext _identityDb;
    private readonly AppDbContext _appDb;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<MigrationsController> _logger;
    private readonly IAuditLogger _auditLogger;

    public MigrationsController(
        ApplicationDbContext identityDb,
        AppDbContext appDb,
        IWebHostEnvironment environment,
        ILogger<MigrationsController> logger,
        IAuditLogger auditLogger)
    {
        _identityDb = identityDb;
        _appDb = appDb;
        _environment = environment;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildViewModelAsync();
        return View(vm);
    }

    [HttpPost("apply-appdb")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyAppDb()
    {
        var now = DateTime.UtcNow;
        try
        {
            await _appDb.Database.MigrateAsync();
            await _auditLogger.LogAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = now,
                ActorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                ActorEmail = User.Identity?.Name ?? string.Empty,
                Action = "Migrations.Applied",
                EntityType = "AppDb",
                EntityId = "AppDb",
                Summary = "Applied App DB migrations.",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });

            TempData["MigrationAlertMessage"] = "App DB migrations applied successfully.";
            TempData["MigrationAlertStyle"] = "success";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply App DB migrations");
            TempData["MigrationAlertMessage"] = $"Migration failed: {ex.Message}";
            TempData["MigrationAlertStyle"] = "danger";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<MigrationAssistantViewModel> BuildViewModelAsync()
    {
        var contexts = new List<MigrationContextViewModel>
        {
            await BuildEfContextAsync(
                "Identity",
                "ASP.NET Core Identity store",
                _identityDb,
                canApply: false,
                guidanceWhenEmpty: "Identity uses EnsureCreated; migrations are not configured."),
            await BuildEfContextAsync(
                "App DB",
                "Domain data schema (licenses, compliance, reports)",
                _appDb,
                canApply: true,
                guidanceWhenEmpty: "Apply migrations to initialize the application schema."),
            BuildHangfireContext()
        };

        return new MigrationAssistantViewModel
        {
            CheckedAtUtc = DateTime.UtcNow,
            Contexts = contexts,
            AlertMessage = TempData["MigrationAlertMessage"] as string,
            AlertStyle = TempData["MigrationAlertStyle"] as string ?? "info"
        };
    }

    private async Task<MigrationContextViewModel> BuildEfContextAsync(
        string name,
        string description,
        DbContext context,
        bool canApply,
        string guidanceWhenEmpty)
    {
        try
        {
            var applied = (await context.Database.GetAppliedMigrationsAsync()).ToList();
            var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
            var lastApplied = ParseMigrationTimestamp(applied.LastOrDefault());

            var status = "Up to date";
            string? guidance = null;
            if (pending.Count > 0)
            {
                status = "Pending";
                guidance = "Pending migrations detected. Take a backup before applying.";
            }
            else if (applied.Count == 0)
            {
                status = "No migrations";
                guidance = guidanceWhenEmpty;
            }

            return new MigrationContextViewModel
            {
                Name = name,
                Description = description,
                AppliedMigrations = applied,
                PendingMigrations = pending,
                LastAppliedUtc = lastApplied,
                Status = status,
                Guidance = guidance,
                CanApply = canApply
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read migrations for {ContextName}", name);
            return new MigrationContextViewModel
            {
                Name = name,
                Description = description,
                Status = "Unavailable",
                Guidance = "Unable to read migration history. Verify database connectivity.",
                CanApply = canApply
            };
        }
    }

    private MigrationContextViewModel BuildHangfireContext()
    {
        var hangfirePath = Path.Combine(_environment.ContentRootPath, "App_Data", "hangfire.db");
        var exists = System.IO.File.Exists(hangfirePath);

        return new MigrationContextViewModel
        {
            Name = "Hangfire",
            Description = "Background job storage (Hangfire-managed schema)",
            AppliedMigrations = Array.Empty<string>(),
            PendingMigrations = Array.Empty<string>(),
            LastAppliedUtc = null,
            Status = exists ? "Managed" : "Missing",
            Guidance = exists
                ? "Hangfire manages its own schema inside the storage file."
                : "Hangfire storage file not found. Start the app to initialize it.",
            CanApply = false
        };
    }

    private static DateTime? ParseMigrationTimestamp(string? migrationId)
    {
        if (string.IsNullOrWhiteSpace(migrationId) || migrationId.Length < 14)
        {
            return null;
        }

        var prefix = migrationId[..14];
        if (DateTime.TryParseExact(prefix, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
