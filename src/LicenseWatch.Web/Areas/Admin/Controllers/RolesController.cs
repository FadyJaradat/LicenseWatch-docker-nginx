using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.RolesView)]
[Route("admin/roles")]
public class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<RolesController> _logger;

    public RolesController(
        RoleManager<IdentityRole> roleManager,
        UserManager<IdentityUser> userManager,
        AppDbContext dbContext,
        IAuditLogger auditLogger,
        ILogger<RolesController> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _dbContext = dbContext;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
        var items = new List<RoleListItem>();
        foreach (var role in roles)
        {
            var count = await _userManager.GetUsersInRoleAsync(role.Name!);
            items.Add(new RoleListItem { Name = role.Name ?? string.Empty, UserCount = count.Count });
        }

        var vm = new RoleListViewModel
        {
            Roles = items,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            AlertDetails = TempData["AlertDetails"] as string
        };

        return View(vm);
    }

    [HttpGet("{roleName}")]
    public async Task<IActionResult> Details(string roleName)
    {
        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
        {
            SetTempAlert("Role name is required.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            SetTempAlert("Role not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var isSystemRole = string.Equals(role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase);
        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);

        var grantedPermissions = await _dbContext.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleName == role.Name)
            .Select(rp => rp.PermissionKey)
            .ToListAsync();

        var grantedSet = new HashSet<string>(
            isSystemRole ? PermissionCatalog.All.Select(p => p.Key) : grantedPermissions,
            StringComparer.OrdinalIgnoreCase);

        var groups = PermissionCatalog.Grouped()
            .Select(group =>
            {
                var items = group.Value.Select(permission => new RolePermissionItemViewModel
                {
                    Key = permission.Key,
                    Label = permission.Label,
                    Description = permission.Description,
                    IsGranted = grantedSet.Contains(permission.Key),
                    IsManage = permission.IsManage
                }).ToList();

                return new RolePermissionGroupViewModel
                {
                    GroupName = group.Key,
                    Permissions = items,
                    AllSelected = items.All(item => item.IsGranted)
                };
            })
            .ToList();

        var vm = new RoleDetailViewModel
        {
            RoleName = role.Name ?? string.Empty,
            IsSystemRole = isSystemRole,
            UserCount = usersInRole.Count,
            Groups = groups,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            AlertDetails = TempData["AlertDetails"] as string
        };

        return View(vm);
    }

    [HttpPost("create")]
    [Authorize(Policy = PermissionPolicies.RolesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string roleName)
    {
        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
        {
            SetTempAlert("Role name is required.", "danger");
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(roleName, "SystemAdmin", StringComparison.OrdinalIgnoreCase))
        {
            SetTempAlert("SystemAdmin already exists and cannot be recreated.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (await _roleManager.RoleExistsAsync(roleName))
        {
            SetTempAlert("Role already exists.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            SetTempAlert("Failed to create role.", "danger", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        else
        {
            SetTempAlert("Role created.", "success");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{roleName}/permissions")]
    [Authorize(Policy = PermissionPolicies.RolesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePermissions(string roleName, string[] selectedPermissions)
    {
        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
        {
            SetTempAlert("Role name is required.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            SetTempAlert("Role not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase))
        {
            SetTempAlert("SystemAdmin always has every permission and cannot be edited.", "warning");
            return RedirectToAction(nameof(Details), new { roleName = role.Name });
        }

        var allowed = PermissionCatalog.All.Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selected = (selectedPermissions ?? Array.Empty<string>())
            .Where(key => allowed.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _dbContext.RolePermissions
            .Where(rp => rp.RoleName == role.Name)
            .ToListAsync();

        if (existing.Count > 0)
        {
            _dbContext.RolePermissions.RemoveRange(existing);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        foreach (var permissionKey in selected)
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = role.Name!,
                PermissionKey = permissionKey,
                GrantedAtUtc = DateTime.UtcNow,
                GrantedByUserId = userId
            });
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Role permissions updated for {RoleName}. Count: {Count}", role.Name, selected.Count);

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = userId,
            ActorEmail = User.Identity?.Name ?? string.Empty,
            Action = "Roles.PermissionsUpdated",
            EntityType = "Role",
            EntityId = role.Name ?? string.Empty,
            Summary = $"Updated {selected.Count} permissions for {role.Name}.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        SetTempAlert("Permissions updated.", "success");
        return RedirectToAction(nameof(Details), new { roleName = role.Name });
    }

    [HttpPost("{roleName}/delete")]
    [Authorize(Policy = PermissionPolicies.RolesManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string roleName)
    {
        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
        {
            SetTempAlert("Role name is required.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            SetTempAlert("Role not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = User.Identity?.Name ?? string.Empty;

        if (string.Equals(role.Name, "SystemAdmin", StringComparison.OrdinalIgnoreCase))
        {
            await _auditLogger.LogAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                ActorUserId = userId,
                ActorEmail = email,
                Action = "Roles.DeleteBlocked",
                EntityType = "Role",
                EntityId = role.Name ?? string.Empty,
                Summary = "Attempted to delete SystemAdmin role.",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });

            _logger.LogWarning("Delete blocked for SystemAdmin role.");
            SetTempAlert("SystemAdmin cannot be deleted.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count > 0)
        {
            await _auditLogger.LogAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                ActorUserId = userId,
                ActorEmail = email,
                Action = "Roles.DeleteBlocked",
                EntityType = "Role",
                EntityId = role.Name ?? string.Empty,
                Summary = $"Attempted to delete role with {usersInRole.Count} assigned users.",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });

            _logger.LogWarning("Delete blocked for role {RoleName} with {UserCount} users.", role.Name, usersInRole.Count);
            SetTempAlert($"Role has {usersInRole.Count} users assigned. Remove them first.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var permissions = await _dbContext.RolePermissions
            .Where(rp => rp.RoleName == role.Name)
            .ToListAsync();

        if (permissions.Count > 0)
        {
            _dbContext.RolePermissions.RemoveRange(permissions);
            await _dbContext.SaveChangesAsync();
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            SetTempAlert("Failed to delete role.", "danger", string.Join(", ", result.Errors.Select(e => e.Description)));
            return RedirectToAction(nameof(Index));
        }

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = userId,
            ActorEmail = email,
            Action = "Roles.Deleted",
            EntityType = "Role",
            EntityId = roleName,
            Summary = $"Deleted role {roleName}.",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        _logger.LogInformation("Role deleted {RoleName}.", roleName);
        SetTempAlert("Role deleted.", "success");
        return RedirectToAction(nameof(Index));
    }

    private void SetTempAlert(string message, string style, string? details = null)
    {
        TempData["AlertMessage"] = message;
        TempData["AlertStyle"] = style;
        if (!string.IsNullOrWhiteSpace(details))
        {
            TempData["AlertDetails"] = details;
        }
    }
}
