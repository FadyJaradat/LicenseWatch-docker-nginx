using System.Security.Claims;
using System.Text;
using LicenseWatch.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Web.Security;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Core.Entities;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.UsersView)]
[Route("admin/users")]
public class UsersController : Controller
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<UsersController> _logger;
    private const int DefaultPageSize = 20;

    public UsersController(
        UserManager<IdentityUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IAuditLogger auditLogger,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search = null, int page = 1, int pageSize = DefaultPageSize)
    {
        var query = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.Email!.Contains(search) || u.UserName!.Contains(search));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<UserListItemViewModel>();
        var currentUserId = _userManager.GetUserId(User);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var deleteCheck = await GetDeleteEligibilityAsync(user, roles, currentUserId);
            items.Add(new UserListItemViewModel
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? "(unknown)",
                Roles = roles.ToList(),
                IsLocked = await IsLockedAsync(user),
                LastLoginDisplay = "—",
                CanDelete = deleteCheck.CanDelete,
                DeleteDisabledReason = deleteCheck.Reason
            });
        }

        var vm = new UsersListViewModel
        {
            Users = items,
            Search = search,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return View(vm);
    }

    [HttpGet("create")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost("create")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(string.Empty, "Email is required.");
            return View();
        }

        var tempPassword = GenerateTempPassword();
        var user = new IdentityUser
        {
            Email = email,
            UserName = email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View();
        }

        TempData["TempPassword"] = tempPassword;
        TempData["AlertMessage"] = "User created. Provide the temporary password securely.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Details), new { id = user.Id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var allRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        var deleteCheck = await GetDeleteEligibilityAsync(user, roles, _userManager.GetUserId(User));

        var vm = new UserDetailViewModel
        {
            Id = user.Id,
            Email = user.Email ?? user.UserName ?? "(unknown)",
            Roles = roles.ToList(),
            AllRoles = allRoles,
            IsLocked = await IsLockedAsync(user),
            LastLoginDisplay = "—",
            TempPassword = TempData["TempPassword"] as string,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            AlertDetails = TempData["AlertDetails"] as string,
            CanDelete = deleteCheck.CanDelete,
            DeleteDisabledReason = deleteCheck.Reason
        };

        return View(vm);
    }

    [HttpPost("{id}/assign-role")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignRole(string id, string roleName)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return RedirectToAction(nameof(Index));
        }

        roleName = roleName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roleName))
        {
            SetTempAlert("Role name is required.", "danger");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            var createRoleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (!createRoleResult.Succeeded)
            {
                SetTempAlert("Failed to create role.", "danger", string.Join(", ", createRoleResult.Errors.Select(e => e.Description)));
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, roleName);
        if (!addResult.Succeeded)
        {
            SetTempAlert("Failed to assign role.", "danger", string.Join(", ", addResult.Errors.Select(e => e.Description)));
        }
        else
        {
            SetTempAlert("Role assigned.", "success");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id}/remove-role")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(string id, string roleName)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(roleName, "SystemAdmin", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(user.Id, _userManager.GetUserId(User), StringComparison.OrdinalIgnoreCase))
        {
            SetTempAlert("You cannot remove your own SystemAdmin role.", "warning");
            return RedirectToAction(nameof(Details), new { id });
        }

        var removeResult = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (!removeResult.Succeeded)
        {
            SetTempAlert("Failed to remove role.", "danger", string.Join(", ", removeResult.Errors.Select(e => e.Description)));
        }
        else
        {
            SetTempAlert("Role removed.", "success");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id}/lock")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return RedirectToAction(nameof(Index));
        }

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        SetTempAlert("User locked.", "success");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id}/unlock")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return RedirectToAction(nameof(Index));
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        SetTempAlert("User unlocked.", "success");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id}/reset-password")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var tempPassword = GenerateTempPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);
        if (!resetResult.Succeeded)
        {
            SetTempAlert("Failed to reset password.", "danger", string.Join(", ", resetResult.Errors.Select(e => e.Description)));
        }
        else
        {
            TempData["TempPassword"] = tempPassword;
            SetTempAlert("Password reset. Share the temporary password securely.", "success");
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id}/delete")]
    [Authorize(Policy = PermissionPolicies.UsersManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var deleteCheck = await GetDeleteEligibilityAsync(user, roles, _userManager.GetUserId(User));
        if (!deleteCheck.CanDelete)
        {
            await LogAuditAsync("Users.DeleteFailed", user, deleteCheck.Reason ?? "Deletion blocked by policy.");
            SetTempAlert(deleteCheck.Reason ?? "User cannot be deleted.", "warning");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (roles.Count > 0)
        {
            var removeRoles = await _userManager.RemoveFromRolesAsync(user, roles);
            if (!removeRoles.Succeeded)
            {
                SetTempAlert("Failed to remove user roles.", "danger", string.Join(", ", removeRoles.Errors.Select(e => e.Description)));
                await LogAuditAsync("Users.DeleteFailed", user, "Role removal failed.");
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            SetTempAlert("Failed to delete user.", "danger", string.Join(", ", result.Errors.Select(e => e.Description)));
            await LogAuditAsync("Users.DeleteFailed", user, "Delete operation failed.");
            return RedirectToAction(nameof(Details), new { id });
        }

        await LogAuditAsync("Users.Deleted", user, $"Deleted user {user.Email ?? user.UserName}");
        SetTempAlert("User deleted.", "success");
        return RedirectToAction(nameof(Index));
    }

    private static string GenerateTempPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string symbols = "!@$%";
        var all = $"{upper}{lower}{digits}{symbols}";
        var random = new Random();

        var chars = new List<char>
        {
            upper[random.Next(upper.Length)],
            lower[random.Next(lower.Length)],
            digits[random.Next(digits.Length)],
            symbols[random.Next(symbols.Length)]
        };

        while (chars.Count < 14)
        {
            chars.Add(all[random.Next(all.Length)]);
        }

        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }

    private async Task<bool> IsLockedAsync(IdentityUser user)
    {
        if (!await _userManager.GetLockoutEnabledAsync(user))
        {
            return false;
        }

        var end = await _userManager.GetLockoutEndDateAsync(user);
        return end.HasValue && end.Value >= DateTimeOffset.UtcNow;
    }

    private async Task<(bool CanDelete, string? Reason)> GetDeleteEligibilityAsync(
        IdentityUser user,
        IList<string> roles,
        string? currentUserId)
    {
        if (string.Equals(user.Id, currentUserId, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "You cannot delete your own account.");
        }

        if (roles.Any(role => string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            var systemAdmins = await _userManager.GetUsersInRoleAsync("SystemAdmin");
            if (systemAdmins.Count <= 1)
            {
                return (false, "Cannot delete the last SystemAdmin. Assign another SystemAdmin first.");
            }
        }

        return (true, null);
    }

    private async Task LogAuditAsync(string action, IdentityUser target, string summary)
    {
        var actorId = _userManager.GetUserId(User) ?? string.Empty;
        var actorEmail = User?.Identity?.Name ?? "System";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _auditLogger.LogAsync(new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = actorId,
            ActorEmail = actorEmail,
            Action = action,
            EntityType = "User",
            EntityId = target.Id,
            Summary = summary,
            IpAddress = ip
        });
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
