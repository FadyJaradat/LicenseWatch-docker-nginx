using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LicenseWatch.Web.Helpers;

public static class HealthCheckResponseWriter
{
    public static Task WriteMinimalAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        var payload = report.Status switch
        {
            HealthStatus.Healthy => "Healthy",
            HealthStatus.Degraded => "Degraded",
            _ => "Unhealthy"
        };
        return context.Response.WriteAsync(payload);
    }
}
