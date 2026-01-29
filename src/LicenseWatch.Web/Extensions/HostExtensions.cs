using System.Data.Common;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Email;
using LicenseWatch.Infrastructure.Jobs;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Web.Extensions;

public static class HostExtensions
{
    public static async Task ApplyAppDbMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("AppDbMigrations");

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            await dbContext.Database.MigrateAsync();
            await EnsureLicenseThresholdColumnsAsync(dbContext, logger);
            await EnsureRolePermissionsTableAsync(dbContext, logger);
            logger.LogInformation("Applied App DB migrations.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply App DB migrations.");
        }
    }

    private static async Task EnsureLicenseThresholdColumnsAsync(AppDbContext dbContext, ILogger logger)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            if (!await TableExistsAsync(connection, "Licenses"))
            {
                return;
            }

            if (!await ColumnExistsAsync(connection, "Licenses", "UseCustomThresholds"))
            {
                await ExecuteNonQueryAsync(connection,
                    "ALTER TABLE Licenses ADD COLUMN UseCustomThresholds INTEGER NOT NULL DEFAULT 0;");
            }

            if (!await ColumnExistsAsync(connection, "Licenses", "CriticalThresholdDays"))
            {
                await ExecuteNonQueryAsync(connection,
                    "ALTER TABLE Licenses ADD COLUMN CriticalThresholdDays INTEGER NULL;");
            }

            if (!await ColumnExistsAsync(connection, "Licenses", "WarningThresholdDays"))
            {
                await ExecuteNonQueryAsync(connection,
                    "ALTER TABLE Licenses ADD COLUMN WarningThresholdDays INTEGER NULL;");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ensure license threshold columns.");
        }
    }

    private static async Task EnsureRolePermissionsTableAsync(AppDbContext dbContext, ILogger logger)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            await connection.OpenAsync();

            if (await TableExistsAsync(connection, "RolePermissions"))
            {
                return;
            }

            await ExecuteNonQueryAsync(connection,
                @"CREATE TABLE ""RolePermissions"" (
                    ""Id"" TEXT NOT NULL,
                    ""RoleName"" TEXT NOT NULL,
                    ""PermissionKey"" TEXT NOT NULL,
                    ""GrantedAtUtc"" TEXT NOT NULL,
                    ""GrantedByUserId"" TEXT NOT NULL,
                    CONSTRAINT ""PK_RolePermissions"" PRIMARY KEY (""Id"")
                );");

            await ExecuteNonQueryAsync(connection,
                @"CREATE INDEX ""IX_RolePermissions_RoleName"" ON ""RolePermissions"" (""RoleName"");");

            await ExecuteNonQueryAsync(connection,
                @"CREATE UNIQUE INDEX ""IX_RolePermissions_RoleName_PermissionKey""
                  ON ""RolePermissions"" (""RoleName"", ""PermissionKey"");");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to ensure RolePermissions table.");
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $name;";
        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "$table";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);
        var nameParam = command.CreateParameter();
        nameParam.ParameterName = "$name";
        nameParam.Value = columnName;
        command.Parameters.Add(nameParam);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task ExecuteNonQueryAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeding");

        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IHostEnvironment>();

            const string adminRoleName = "SystemAdmin";
            if (!await roleManager.RoleExistsAsync(adminRoleName))
            {
                await roleManager.CreateAsync(new IdentityRole(adminRoleName));
                logger.LogInformation("Created role {RoleName}", adminRoleName);
            }

            var seedEnabled = configuration.GetValue<bool?>("Security:SeedAdmin:Enabled");
            if (seedEnabled.HasValue && !seedEnabled.Value)
            {
                logger.LogInformation("Seed admin is disabled via configuration.");
                return;
            }

            if (!environment.IsDevelopment() && seedEnabled is not true)
            {
                logger.LogInformation("Skipping seed admin in non-development environment.");
                return;
            }

            var adminEmail = configuration["Security:SeedAdmin:Email"];
            var adminPassword = configuration["Security:SeedAdmin:Password"];
            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning("Seed admin credentials are not configured. Set Security:SeedAdmin:Email and Security:SeedAdmin:Password to enable admin login.");
                return;
            }

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser is null)
            {
                adminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Failed to seed admin user: {Errors}", string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return;
                }

                logger.LogInformation("Seeded admin user");
            }

            if (!await userManager.IsInRoleAsync(adminUser, adminRoleName))
            {
                await userManager.AddToRoleAsync(adminUser, adminRoleName);
                logger.LogInformation("Assigned {Email} to role {RoleName}", adminEmail, adminRoleName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while seeding identity data");
        }
    }

    public static async Task SeedEmailTemplatesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("EmailTemplateSeeding");

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var pending = await dbContext.Database.GetPendingMigrationsAsync();
            if (pending.Any())
            {
                logger.LogInformation("Skipping email template seeding; pending migrations detected.");
                return;
            }

            var existingKeys = await dbContext.EmailTemplates.Select(t => t.Key).ToListAsync();
            var now = DateTime.UtcNow;
            var systemUser = "system";

            var templates = new List<EmailTemplate>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Key = "LicenseExpiring",
                    Name = "License expiring soon",
                    SubjectTemplate = "[{{AppName}}] License expiring: {{LicenseName}}",
                    BodyHtmlTemplate = "<h2>License expiring</h2><p>{{LicenseName}} ({{Vendor}}) expires on <strong>{{ExpiresOn}}</strong>.</p><p>Review renewals in the admin dashboard: {{DashboardUrl}}</p><p class=\"small\">{{AppVersion}}</p>",
                    BodyTextTemplate = "License expiring: {{LicenseName}} ({{Vendor}}) expires on {{ExpiresOn}}. Review renewals: {{DashboardUrl}}. Version: {{AppVersion}}",
                    IsEnabled = true,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = systemUser
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Key = "LicenseExpired",
                    Name = "License expired",
                    SubjectTemplate = "[{{AppName}}] License expired: {{LicenseName}}",
                    BodyHtmlTemplate = "<h2>License expired</h2><p>{{LicenseName}} ({{Vendor}}) expired on <strong>{{ExpiresOn}}</strong>.</p><p>Open the dashboard: {{DashboardUrl}}</p><p class=\"small\">{{AppVersion}}</p>",
                    BodyTextTemplate = "License expired: {{LicenseName}} ({{Vendor}}) expired on {{ExpiresOn}}. Dashboard: {{DashboardUrl}}. Version: {{AppVersion}}",
                    IsEnabled = true,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = systemUser
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Key = "ComplianceOveruse",
                    Name = "Compliance overuse alert",
                    SubjectTemplate = "[{{AppName}}] Compliance alert: {{LicenseName}}",
                    BodyHtmlTemplate = "<h2>Compliance alert</h2><p>Overuse detected for {{LicenseName}} ({{Vendor}}). Severity: <strong>{{Severity}}</strong>.</p><p>Review details: {{DashboardUrl}}</p><p class=\"small\">{{AppVersion}}</p>",
                    BodyTextTemplate = "Overuse detected for {{LicenseName}} ({{Vendor}}). Severity: {{Severity}}. Review details: {{DashboardUrl}}. Version: {{AppVersion}}",
                    IsEnabled = true,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = systemUser
                }
            };

            var newTemplates = templates.Where(t => !existingKeys.Contains(t.Key, StringComparer.OrdinalIgnoreCase)).ToList();
            if (newTemplates.Count == 0)
            {
                return;
            }

            dbContext.EmailTemplates.AddRange(newTemplates);

            var auditEntries = newTemplates.Select(template => new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = now,
                ActorUserId = systemUser,
                ActorEmail = systemUser,
                Action = "EmailTemplate.Created",
                EntityType = "EmailTemplate",
                EntityId = template.Id.ToString(),
                Summary = $"Seeded email template: {template.Name}",
                IpAddress = null
            }).ToList();

            dbContext.AuditLogs.AddRange(auditEntries);
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} email templates.", newTemplates.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed email templates.");
        }
    }

    public static async Task SeedEmailNotificationRulesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("EmailNotificationRuleSeeding");

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var pending = await dbContext.Database.GetPendingMigrationsAsync();
            if (pending.Any())
            {
                logger.LogInformation("Skipping notification rule seeding; pending migrations detected.");
                return;
            }

            var existing = await dbContext.EmailNotificationRules.AsNoTracking()
                .Select(r => r.EventKey)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var systemUser = "system";

            var defaults = EmailNotificationCatalog.Defaults
                .Where(def => !existing.Contains(def.EventKey, StringComparer.OrdinalIgnoreCase))
                .Select(def => new EmailNotificationRule
                {
                    Id = Guid.NewGuid(),
                    EventKey = def.EventKey,
                    Name = def.Name,
                    Frequency = def.Frequency,
                    IsEnabled = true,
                    RoleRecipients = "SystemAdmin",
                    UpdatedAtUtc = now,
                    UpdatedByUserId = systemUser
                })
                .ToList();

            if (defaults.Count == 0)
            {
                return;
            }

            dbContext.EmailNotificationRules.AddRange(defaults);
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = now,
                ActorUserId = systemUser,
                ActorEmail = systemUser,
                Action = "EmailNotificationRules.Seeded",
                EntityType = "EmailNotificationRule",
                EntityId = systemUser,
                Summary = $"Seeded {defaults.Count} email notification rules.",
                IpAddress = null
            });

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} email notification rules.", defaults.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed email notification rules.");
        }
    }

    public static async Task SeedPermissionsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PermissionSeeding");

        try
        {
            var dbContext = services.GetRequiredService<AppDbContext>();
            var pending = await dbContext.Database.GetPendingMigrationsAsync();
            if (pending.Any())
            {
                logger.LogInformation("Skipping permission seeding; pending migrations detected.");
                return;
            }

            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var adminRole = await roleManager.FindByNameAsync("SystemAdmin");
            if (adminRole is null)
            {
                logger.LogWarning("SystemAdmin role not found; skipping permission seeding.");
                return;
            }

            var existing = await dbContext.RolePermissions.AsNoTracking()
                .Where(rp => rp.RoleName == adminRole.Name)
                .Select(rp => rp.PermissionKey)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var systemUser = "system";
            var missing = PermissionCatalog.All
                .Select(p => p.Key)
                .Except(existing, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missing.Count == 0)
            {
                return;
            }

            var entries = missing.Select(key => new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleName = adminRole.Name ?? "SystemAdmin",
                PermissionKey = key,
                GrantedAtUtc = now,
                GrantedByUserId = systemUser
            }).ToList();

            dbContext.RolePermissions.AddRange(entries);
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = now,
                ActorUserId = systemUser,
                ActorEmail = systemUser,
                Action = "Roles.PermissionsSeeded",
                EntityType = "RolePermission",
                EntityId = adminRole.Id,
                Summary = $"Seeded {entries.Count} permissions for SystemAdmin.",
                IpAddress = null
            });

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} permissions for SystemAdmin.", entries.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed permissions.");
        }
    }

    public static async Task SeedScheduledJobsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("JobScheduling");

        try
        {
            var scheduler = services.GetRequiredService<IJobScheduler>();
            await scheduler.EnsureDefaultsAsync();
            await scheduler.SyncAsync();
            logger.LogInformation("Scheduled job definitions synchronized.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed scheduled jobs.");
        }
    }
}
