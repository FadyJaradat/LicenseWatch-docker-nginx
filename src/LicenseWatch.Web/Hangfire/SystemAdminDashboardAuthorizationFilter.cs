using Hangfire.Dashboard;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;

namespace LicenseWatch.Web.Hangfire;

public class SystemAdminDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var authorization = httpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        var result = authorization.AuthorizeAsync(httpContext.User, PermissionPolicies.For(PermissionKeys.JobsScheduleManage))
            .GetAwaiter()
            .GetResult();
        return result.Succeeded;
    }
}
