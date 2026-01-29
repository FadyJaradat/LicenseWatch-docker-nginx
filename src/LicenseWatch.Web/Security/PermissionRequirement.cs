using Microsoft.AspNetCore.Authorization;

namespace LicenseWatch.Web.Security;

public sealed class PermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
