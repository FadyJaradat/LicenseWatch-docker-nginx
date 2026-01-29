using System.Security.Claims;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Web.Security;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class PermissionService : IPermissionService
{
    private const string CacheKey = "lw.permissions";
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor, ILogger<PermissionService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole("SystemAdmin"))
        {
            return true;
        }

        var permissions = await GetPermissionsAsync(user, cancellationToken);
        if (permissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        var implied = PermissionCatalog.GetImpliedPermissions(permissionKey);
        return implied.Any(key => permissions.Contains(key, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null && httpContext.Items.TryGetValue(CacheKey, out var cached)
            && cached is IReadOnlyCollection<string> cachedPermissions)
        {
            return cachedPermissions;
        }

        var roles = user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count == 0)
        {
            return Array.Empty<string>();
        }

        IReadOnlyCollection<string> permissions;
        try
        {
            permissions = await _dbContext.RolePermissions.AsNoTracking()
                .Where(rp => roles.Contains(rp.RoleName))
                .Select(rp => rp.PermissionKey)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
        catch (SqliteException ex) when (ex.Message.Contains("no such table: RolePermissions", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "RolePermissions table missing. Returning empty permission set.");
            permissions = Array.Empty<string>();
        }

        if (httpContext is not null)
        {
            httpContext.Items[CacheKey] = permissions;
        }

        return permissions;
    }
}
