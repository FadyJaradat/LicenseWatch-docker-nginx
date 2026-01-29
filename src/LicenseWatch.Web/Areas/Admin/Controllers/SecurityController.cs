using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Options;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.SecurityView)]
[Route("admin/security")]
public class SecurityController : Controller
{
    private readonly IOptions<IdentityOptions> _identityOptions;
    private readonly IOptionsMonitor<CookieAuthenticationOptions> _cookieOptions;
    private readonly ISecurityEventStore _eventStore;
    private readonly SecurityPolicyOptions _policy;
    private readonly IHostEnvironment _environment;

    public SecurityController(
        IOptions<IdentityOptions> identityOptions,
        IOptionsMonitor<CookieAuthenticationOptions> cookieOptions,
        ISecurityEventStore eventStore,
        SecurityPolicyOptions policy,
        IHostEnvironment environment)
    {
        _identityOptions = identityOptions;
        _cookieOptions = cookieOptions;
        _eventStore = eventStore;
        _policy = policy;
        _environment = environment;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var identity = _identityOptions.Value;
        var cookie = _cookieOptions.Get(IdentityConstants.ApplicationScheme);

        var vm = new SecurityDashboardViewModel
        {
            EnvironmentName = _environment.EnvironmentName,
            AppVersion = AppInfo.DisplayVersion,
            GeneratedAtUtc = DateTime.UtcNow,
            Posture = new SecurityPostureViewModel
            {
                CspEnabled = true,
                RateLimitingEnabled = true,
                HstsEnabled = !_environment.IsDevelopment(),
                CookieSummary = $"HttpOnly: {(cookie.Cookie.HttpOnly ? "Yes" : "No")} • SameSite: {cookie.Cookie.SameSite} • Secure: {cookie.Cookie.SecurePolicy} • Sliding: {(cookie.SlidingExpiration ? "On" : "Off")}",
                PasswordSummary = $"Length: {identity.Password.RequiredLength} • Upper: {BoolLabel(identity.Password.RequireUppercase)} • Lower: {BoolLabel(identity.Password.RequireLowercase)} • Digit: {BoolLabel(identity.Password.RequireDigit)} • Symbol: {BoolLabel(identity.Password.RequireNonAlphanumeric)}",
                LockoutSummary = $"Max attempts: {identity.Lockout.MaxFailedAccessAttempts} • Lockout: {identity.Lockout.DefaultLockoutTimeSpan.TotalMinutes:0} min",
                RateLimitSummary = $"Login: {_policy.LoginPermitLimitPerMinute}/min • Admin: {_policy.AdminPermitLimitPerMinute}/min • Upload: {_policy.UploadPermitLimitPerMinute}/min"
            },
            Events = _eventStore.GetRecent(50).Select(e => new SecurityEventViewModel
            {
                OccurredAtUtc = e.OccurredAtUtc,
                EventType = e.EventType,
                Summary = e.Summary,
                Path = e.Path,
                IpAddress = e.IpAddress,
                UserEmail = e.UserEmail
            }).ToList()
        };

        return View(vm);
    }

    private static string BoolLabel(bool value) => value ? "Required" : "Off";
}
