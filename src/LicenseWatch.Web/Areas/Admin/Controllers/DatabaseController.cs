using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.DatabaseManage)]
[Route("admin/database")]
public class DatabaseController : Controller
{
    private readonly AppDbContext _appDbContext;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DatabaseController> _logger;

    public DatabaseController(AppDbContext appDbContext, IWebHostEnvironment env, ILogger<DatabaseController> logger)
    {
        _appDbContext = appDbContext;
        _env = env;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var vm = await BuildViewModel();
        return View(vm);
    }

    [HttpPost("test-connection")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection()
    {
        var vm = await BuildViewModel();
        try
        {
            vm.CanConnect = await _appDbContext.Database.CanConnectAsync();
            vm.AlertMessage = vm.CanConnect ? "Connection succeeded." : "Connection failed.";
            vm.AlertStyle = vm.CanConnect ? "success" : "danger";
        }
        catch (Exception ex)
        {
            vm.CanConnect = false;
            vm.AlertMessage = $"Connection failed: {ex.Message}";
            vm.AlertStyle = "danger";
            _logger.LogError(ex, "Error testing AppDb connection");
        }

        return View("Index", vm);
    }

    [HttpPost("apply-migrations")]
    [Authorize(Policy = PermissionPolicies.MigrationsManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyMigrations()
    {
        string message;
        string style;
        try
        {
            await _appDbContext.Database.MigrateAsync();
            message = "Migrations applied successfully.";
            style = "success";
            _logger.LogInformation("Applied AppDb migrations");
        }
        catch (Exception ex)
        {
            message = $"Migration failed: {ex.Message}";
            style = "danger";
            _logger.LogError(ex, "Failed to apply AppDb migrations");
        }

        var vm = await BuildViewModel();
        vm.AlertMessage = message;
        vm.AlertStyle = style;
        return View("Index", vm);
    }

    private async Task<DatabaseStatusViewModel> BuildViewModel()
    {
        var connectionString = _appDbContext.Database.GetConnectionString() ?? string.Empty;
        var applied = await _appDbContext.Database.GetAppliedMigrationsAsync();
        var pending = await _appDbContext.Database.GetPendingMigrationsAsync();

        return new DatabaseStatusViewModel
        {
            Environment = _env.EnvironmentName,
            ConnectionString = connectionString,
            MaskedConnectionString = MaskConnectionString(connectionString),
            CanConnect = false,
            AppliedMigrationsCount = applied.Count(),
            AppliedMigrations = applied.ToList(),
            PendingMigrations = pending.ToList(),
            AlertMessage = TempData["DatabaseAlertMessage"] as string,
            AlertStyle = TempData["DatabaseAlertStyle"] as string ?? "info"
        };
    }

    private static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "Not configured";
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (!string.IsNullOrWhiteSpace(builder.DataSource))
            {
                return $"Data Source={Path.GetFileName(builder.DataSource)}";
            }
        }
        catch
        {
            return "Configured";
        }

        return "Configured";
    }
}
