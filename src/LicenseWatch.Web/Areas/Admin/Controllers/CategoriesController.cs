using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Web.Security;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.CategoriesManage)]
[Route("admin/categories")]
public class CategoriesController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogger _auditLogger;

    public CategoriesController(AppDbContext dbContext, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _auditLogger = auditLogger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search = null)
    {
        var query = _dbContext.Categories.Include(c => c.Licenses).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search));
        }

        var categories = await query.OrderBy(c => c.Name).ToListAsync();
        var vm = new CategoryListViewModel
        {
            Search = search,
            Categories = categories.Select(c => new CategoryListItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                LicenseCount = c.Licenses.Count,
                CreatedAtUtc = c.CreatedAtUtc
            }).ToList(),
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            AlertDetails = TempData["AlertDetails"] as string
        };

        return View(vm);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        return View(new CategoryFormViewModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
            return View(model);
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            _dbContext.Categories.Add(category);
            await _dbContext.SaveChangesAsync();
            await LogAuditAsync("Category.Created", "Category", category.Id.ToString(), $"Created category {category.Name}");

            TempData["AlertMessage"] = "Category created.";
            TempData["AlertStyle"] = "success";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
            return View(model);
        }
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var category = await _dbContext.Categories.FindAsync(id);
        if (category is null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View(new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description
        });
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CategoryFormViewModel model)
    {
        var category = await _dbContext.Categories.FindAsync(id);
        if (category is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "Name is required.");
            return View(model);
        }

        try
        {
            category.Name = model.Name.Trim();
            category.Description = model.Description?.Trim();
            await _dbContext.SaveChangesAsync();
            await LogAuditAsync("Category.Updated", "Category", category.Id.ToString(), $"Updated category {category.Name}");

            TempData["AlertMessage"] = "Category updated.";
            TempData["AlertStyle"] = "success";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
            return View(model);
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var category = await _dbContext.Categories.Include(c => c.Licenses).FirstOrDefaultAsync(c => c.Id == id);
        if (category is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (category.Licenses.Any())
        {
            TempData["AlertMessage"] = "Category has licenses and cannot be deleted.";
            TempData["AlertStyle"] = "warning";
            return RedirectToAction(nameof(Index));
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("Category.Deleted", "Category", category.Id.ToString(), $"Deleted category {category.Name}");

        TempData["AlertMessage"] = "Category deleted.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Index));
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
}
