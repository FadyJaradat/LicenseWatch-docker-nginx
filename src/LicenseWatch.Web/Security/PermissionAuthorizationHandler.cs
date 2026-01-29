using Microsoft.AspNetCore.Authorization;

namespace LicenseWatch.Web.Security;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var hasPermission = await _permissionService.HasPermissionAsync(context.User, requirement.PermissionKey);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
