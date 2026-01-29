using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Compliance;
using LicenseWatch.Infrastructure.Dashboard;
using LicenseWatch.Infrastructure.Email;
using LicenseWatch.Infrastructure.Health;
using LicenseWatch.Infrastructure.Jobs;
using LicenseWatch.Infrastructure.Maintenance;
using LicenseWatch.Infrastructure.Optimization;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Infrastructure.Reports;
using LicenseWatch.Infrastructure.Storage;
using LicenseWatch.Infrastructure.Usage;
using LicenseWatch.Web;
using LicenseWatch.Web.Diagnostics;
using LicenseWatch.Web.Extensions;
using LicenseWatch.Web.Helpers;
using LicenseWatch.Web.Hangfire;
using LicenseWatch.Web.Options;
using LicenseWatch.Web.Security;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Core.Jobs;
using LicenseWatch.Core.Entities;
using LicenseWatch.Core.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Diagnostics;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId |
                                      ActivityTrackingOptions.SpanId |
                                      ActivityTrackingOptions.ParentId;
});

var configuration = builder.Configuration;
var connectionString = configuration.GetConnectionString("IdentityDb") ?? "Data Source=App_Data/licensewatch.identity.db";
var appDbConnectionString = configuration.GetConnectionString("AppDb") ?? "Data Source=App_Data/licensewatch.app.db";
var appDataPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(appDataPath);
var dataProtectionKeysPath = Path.Combine(appDataPath, "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
var bootstrapFilePath = Path.Combine(appDataPath, "bootstrap.json");
var uploadsPath = Path.Combine(appDataPath, "uploads");
Directory.CreateDirectory(uploadsPath);
var importsTempPath = Path.Combine(appDataPath, "imports", "tmp");
Directory.CreateDirectory(importsTempPath);
var backupsPath = Path.Combine(appDataPath, "backups");
Directory.CreateDirectory(backupsPath);
var brandingPath = Path.Combine(appDataPath, "branding");
Directory.CreateDirectory(brandingPath);
var hangfireDbPath = Path.Combine(appDataPath, "hangfire.db");
var identityDbPath = ResolveSqlitePath(connectionString, builder.Environment.ContentRootPath);
var appDbPath = ResolveSqlitePath(appDbConnectionString, builder.Environment.ContentRootPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("LicenseWatch");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(appDbConnectionString, sql =>
    {
        sql.MigrationsAssembly("LicenseWatch.Infrastructure");
    }));

builder.Services.Configure<BootstrapSettingsStorageOptions>(options =>
{
    options.FilePath = bootstrapFilePath;
});
builder.Services.AddSingleton<IBootstrapSettingsStore, FileBootstrapSettingsStore>();
builder.Services.Configure<AttachmentStorageOptions>(builder.Configuration.GetSection("Attachments"));
builder.Services.PostConfigure<AttachmentStorageOptions>(options => options.RootPath = uploadsPath);
builder.Services.AddSingleton<IAttachmentStorage, FileAttachmentStorage>();
builder.Services.Configure<ImportOptions>(builder.Configuration.GetSection("Imports"));
builder.Services.PostConfigure<ImportOptions>(options => options.RootPath = importsTempPath);
builder.Services.Configure<BackupOptions>(options =>
{
    options.AppDataPath = appDataPath;
    options.BackupDirectory = backupsPath;
    options.ExcludedDirectories = new[]
    {
        Path.Combine(appDataPath, "imports", "tmp"),
        backupsPath
    };
});
builder.Services.AddSingleton<IBackupService, BackupService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IDashboardQueryService, DashboardQueryService>();
builder.Services.AddScoped<IComplianceEvaluator, ComplianceEvaluator>();
builder.Services.AddScoped<IUsageAggregator, UsageAggregator>();
builder.Services.AddScoped<IReportsQueryService, ReportsQueryService>();
builder.Services.AddScoped<IOptimizationEngine, OptimizationEngine>();
builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<IEmailNotificationService, EmailNotificationService>();
builder.Services.AddScoped<IReportDeliveryService, ReportDeliveryService>();
builder.Services.AddSingleton<IReportExportService, ReportExportService>();
builder.Services.AddSingleton<IEmailTemplateRenderer, EmailTemplateRenderer>();
builder.Services.AddScoped<BackgroundJobRunner>();
builder.Services.AddScoped<IJobScheduler, JobScheduler>();
builder.Services.AddSingleton(new AppRuntimeInfo(DateTime.UtcNow));
builder.Services.AddSingleton<StartupConfigState>();
builder.Services.AddHostedService<StartupConfigValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSQLiteStorage(hangfireDbPath);
});
builder.Services.AddHangfireServer();

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredUniqueChars = 1;
        options.User.RequireUniqueEmail = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.AccessDeniedPath = "/account/access-denied";
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.Events.OnRedirectToLogin = context =>
    {
        var returnUrl = context.Request.Path + context.Request.QueryString;
        var cookieName = options.Cookie.Name ?? ".AspNetCore.Identity.Application";
        var hasCookie = context.Request.Cookies.ContainsKey(cookieName);
        var reason = hasCookie ? "expired" : null;
        var loginUrl = $"{options.LoginPath}?returnUrl={UrlEncoder.Default.Encode(returnUrl)}";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            loginUrl += $"&reason={reason}";
        }

        context.Response.Redirect(loginUrl);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdmin", policy => policy.RequireRole("SystemAdmin"));
    foreach (var permission in PermissionCatalog.All)
    {
        options.AddPolicy(PermissionPolicies.For(permission.Key),
            policy => policy.Requirements.Add(new PermissionRequirement(permission.Key)));
    }
});

var securityPolicy = new SecurityPolicyOptions();
builder.Services.AddSingleton(securityPolicy);
builder.Services.AddSingleton<ISecurityEventStore, SecurityEventStore>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var httpContext = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers["Retry-After"] = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            httpContext.Items["RateLimitRetryAfter"] = retryAfter;
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        var path = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value : string.Empty;
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var userEmail = httpContext.User.Identity?.Name;
        var summary = $"Rate limit triggered for {path}.";

        var eventStore = httpContext.RequestServices.GetService<ISecurityEventStore>();
        eventStore?.Add(new SecurityEvent(DateTime.UtcNow, "RateLimit", summary, path, ip, userEmail));

        var auditLogger = httpContext.RequestServices.GetService<IAuditLogger>();
        if (auditLogger is not null)
        {
            await auditLogger.LogAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                ActorUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                ActorEmail = userEmail ?? string.Empty,
                Action = "Security.RateLimitTriggered",
                EntityType = "Security",
                EntityId = path ?? string.Empty,
                Summary = summary,
                IpAddress = ip
            }, token);
        }

        var accept = httpContext.Request.Headers.Accept.ToString();
        var wantsJson = accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                        && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);

        if (wantsJson)
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync("{\"error\":\"Too many requests\"}", token);
        }
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (path.StartsWith("/health") || path.StartsWith("/error"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") || path.StartsWith("/images") || path.StartsWith("/favicon"))
        {
            return RateLimitPartition.GetNoLimiter("static");
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (path == "/account/login" && HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"login:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = securityPolicy.LoginPermitLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        }

        if ((path.StartsWith("/admin/import/upload") && HttpMethods.IsPost(context.Request.Method))
            || (path.Contains("/attachments") && HttpMethods.IsPost(context.Request.Method)))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"upload:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = securityPolicy.UploadPermitLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        }

        if (path.StartsWith("/admin"))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"admin:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = securityPolicy.AdminPermitLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
        }

        return RateLimitPartition.GetNoLimiter($"public:{ip}");
    });
});

builder.Services.AddHealthChecks()
    .AddCheck("identity-db", new SqliteFileHealthCheck(identityDbPath, "Identity database"), tags: new[] { "ready" })
    .AddCheck("app-db", new SqliteFileHealthCheck(appDbPath, "Application database"), tags: new[] { "ready" })
    .AddCheck("hangfire-db", new SqliteFileHealthCheck(hangfireDbPath, "Hangfire storage"), tags: new[] { "ready" })
    .AddCheck("app-data-writable", new WritableDirectoryHealthCheck(appDataPath, "App_Data volume"), tags: new[] { "ready" })
    .AddCheck("dp-keys-writable", new WritableDirectoryHealthCheck(dataProtectionKeysPath, "Data Protection keys"), tags: new[] { "ready" })
    .AddCheck<BootstrapSettingsHealthCheck>("bootstrap-settings", tags: new[] { "ready" })
    .AddCheck<EmailConfigurationHealthCheck>("email-settings", tags: new[] { "ready" })
    .AddCheck<StartupConfigHealthCheck>("startup-config", tags: new[] { "ready" });

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler("/error");
app.UseStatusCodePagesWithReExecute("/error/{0}");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var correlationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Items["CorrelationId"] = correlationId;
    CorrelationContext.Current = correlationId;

    using var scope = app.Logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId
    });

    try
    {
        await next();
    }
    finally
    {
        CorrelationContext.Current = null;
    }
});

app.Use(async (context, next) =>
{
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items["CspNonce"] = nonce;

    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

        var scriptSrc = $"script-src 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net";
        if (context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            scriptSrc += " http://localhost:8080";
        }

        var csp = string.Join("; ",
            "default-src 'self'",
            scriptSrc,
            "style-src 'self' https://cdn.jsdelivr.net",
            "img-src 'self' data:",
            "font-src 'self' https://cdn.jsdelivr.net",
            "connect-src 'self'",
            "frame-src 'self'",
            "object-src 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "frame-ancestors 'none'");

        headers["Content-Security-Policy"] = csp;
        return Task.CompletedTask;
    });

    await next();
});

app.UseRouting();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(brandingPath),
    RequestPath = "/app-data/branding"
});
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health", () => Results.Text("OK", "text/plain"));
app.MapGet("/health/live", () => Results.Text("OK", "text/plain"));
app.MapGet("/version", () => Results.Text(AppInfo.InformationalVersion, "text/plain"));
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = entry => entry.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalAsync
});
app.MapStaticAssets();
app.UseHangfireDashboard("/admin/hangfire", new DashboardOptions
{
    Authorization = new[] { new SystemAdminDashboardAuthorizationFilter() }
});

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "privacy",
    pattern: "privacy",
    defaults: new { controller = "Home", action = "Privacy" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

if (app.Environment.IsDevelopment())
{
    await app.ApplyAppDbMigrationsAsync();
}

await app.SeedIdentityAsync();
await app.SeedEmailTemplatesAsync();
await app.SeedEmailNotificationRulesAsync();
await app.SeedPermissionsAsync();
await app.SeedScheduledJobsAsync();

app.Run();

static string ResolveSqlitePath(string connectionString, string contentRoot)
{
    var builder = new SqliteConnectionStringBuilder(connectionString);
    var path = builder.DataSource ?? string.Empty;
    if (string.IsNullOrWhiteSpace(path))
    {
        return string.Empty;
    }

    return Path.IsPathRooted(path) ? path : Path.Combine(contentRoot, path);
}

public partial class Program { }
