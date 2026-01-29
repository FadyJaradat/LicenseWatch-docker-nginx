namespace LicenseWatch.Web.Security;

public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
    {
        new(PermissionKeys.DashboardView, "Dashboard", "Overview", "View executive dashboard and key signals."),

        new(PermissionKeys.LicensesView, "Licenses (View)", "Portfolio", "View license inventory and details."),
        new(PermissionKeys.LicensesManage, "Licenses (Manage)", "Portfolio", "Create, edit, and delete license records.", true),
        new(PermissionKeys.CategoriesManage, "Categories", "Portfolio", "Create and manage license categories.", true),
        new(PermissionKeys.ImportManage, "CSV Import", "Portfolio", "Upload and commit CSV imports.", true),
        new(PermissionKeys.UsageView, "Usage", "Portfolio", "View usage summaries and analytics."),
        new(PermissionKeys.ComplianceView, "Compliance (View)", "Portfolio", "View compliance breaches and status."),
        new(PermissionKeys.ComplianceManage, "Compliance (Manage)", "Portfolio", "Acknowledge or resolve compliance issues.", true),
        new(PermissionKeys.ReportsView, "Reports", "Portfolio", "View and export reports."),

        new(PermissionKeys.EmailView, "Email (View)", "Operations", "View email settings, templates, and logs."),
        new(PermissionKeys.EmailManage, "Email (Manage)", "Operations", "Edit email settings and templates.", true),
        new(PermissionKeys.JobsView, "Jobs (View)", "Operations", "View job schedules and history."),
        new(PermissionKeys.JobsRun, "Jobs (Run)", "Operations", "Run jobs on demand.", true),
        new(PermissionKeys.JobsScheduleManage, "Jobs (Schedule)", "Operations", "Create and manage recurring job schedules.", true),
        new(PermissionKeys.JobsCustomManage, "Jobs (Custom)", "Operations", "Create and manage custom jobs.", true),
        new(PermissionKeys.OptimizationView, "Optimization (View)", "Operations", "View optimization insights and recommendations."),
        new(PermissionKeys.OptimizationManage, "Optimization (Manage)", "Operations", "Run analysis and manage recommendations.", true),

        new(PermissionKeys.AuditView, "Audit Log", "System", "View audit activity."),
        new(PermissionKeys.SecurityView, "Security", "System", "View security posture and events."),
        new(PermissionKeys.SystemView, "System Status", "System", "View system readiness and runtime health."),
        new(PermissionKeys.UsersView, "Users (View)", "System", "View user accounts."),
        new(PermissionKeys.UsersManage, "Users (Manage)", "System", "Create, edit, and lock users.", true),
        new(PermissionKeys.RolesView, "Roles (View)", "System", "View roles and assigned permissions."),
        new(PermissionKeys.RolesManage, "Roles (Manage)", "System", "Manage role permissions.", true),
        new(PermissionKeys.SettingsManage, "Settings", "System", "Manage bootstrap settings.", true),
        new(PermissionKeys.DatabaseManage, "Database", "System", "Test connections and view database status.", true),
        new(PermissionKeys.MigrationsManage, "Migrations", "System", "Apply database migrations.", true),
        new(PermissionKeys.MaintenanceView, "Maintenance (View)", "System", "View backups and maintenance tools."),
        new(PermissionKeys.MaintenanceManage, "Maintenance (Manage)", "System", "Create and download backups.", true)
    };

    private static readonly Dictionary<string, string[]> ImpliedBy = new(StringComparer.OrdinalIgnoreCase)
    {
        [PermissionKeys.LicensesView] = new[] { PermissionKeys.LicensesManage },
        [PermissionKeys.ComplianceView] = new[] { PermissionKeys.ComplianceManage },
        [PermissionKeys.EmailView] = new[] { PermissionKeys.EmailManage },
        [PermissionKeys.OptimizationView] = new[] { PermissionKeys.OptimizationManage },
        [PermissionKeys.UsersView] = new[] { PermissionKeys.UsersManage },
        [PermissionKeys.RolesView] = new[] { PermissionKeys.RolesManage },
        [PermissionKeys.MaintenanceView] = new[] { PermissionKeys.MaintenanceManage },
        [PermissionKeys.JobsView] = new[] { PermissionKeys.JobsRun, PermissionKeys.JobsScheduleManage },
        [PermissionKeys.JobsRun] = new[] { PermissionKeys.JobsScheduleManage }
    };

    public static IReadOnlyList<PermissionDefinition> GetByGroup(string group)
        => All.Where(def => string.Equals(def.Group, group, StringComparison.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyDictionary<string, IReadOnlyList<PermissionDefinition>> Grouped()
        => All.GroupBy(def => def.Group)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PermissionDefinition>)group.ToList());

    public static IReadOnlyList<string> GetImpliedPermissions(string requiredKey)
    {
        if (ImpliedBy.TryGetValue(requiredKey, out var implied))
        {
            return implied;
        }

        return Array.Empty<string>();
    }
}

public record PermissionDefinition(string Key, string Label, string Group, string Description, bool IsManage = false);

public static class PermissionKeys
{
    public const string DashboardView = "dashboard.view";

    public const string LicensesView = "licenses.view";
    public const string LicensesManage = "licenses.manage";
    public const string CategoriesManage = "categories.manage";
    public const string ImportManage = "import.manage";
    public const string UsageView = "usage.view";
    public const string ComplianceView = "compliance.view";
    public const string ComplianceManage = "compliance.manage";
    public const string ReportsView = "reports.view";

    public const string EmailView = "email.view";
    public const string EmailManage = "email.manage";
    public const string JobsView = "jobs.view";
    public const string JobsRun = "jobs.run";
    public const string JobsScheduleManage = "jobs.schedule.manage";
    public const string JobsCustomManage = "jobs.custom.manage";
    public const string OptimizationView = "optimization.view";
    public const string OptimizationManage = "optimization.manage";

    public const string AuditView = "audit.view";
    public const string SecurityView = "security.view";
    public const string SystemView = "system.view";
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";
    public const string SettingsManage = "settings.manage";
    public const string DatabaseManage = "database.manage";
    public const string MigrationsManage = "migrations.manage";
    public const string MaintenanceView = "maintenance.view";
    public const string MaintenanceManage = "maintenance.manage";
}

public static class PermissionPolicies
{
    public static string For(string permissionKey) => $"Permission:{permissionKey}";

    public const string DashboardView = "Permission:dashboard.view";
    public const string LicensesView = "Permission:licenses.view";
    public const string LicensesManage = "Permission:licenses.manage";
    public const string CategoriesManage = "Permission:categories.manage";
    public const string ImportManage = "Permission:import.manage";
    public const string UsageView = "Permission:usage.view";
    public const string ComplianceView = "Permission:compliance.view";
    public const string ComplianceManage = "Permission:compliance.manage";
    public const string ReportsView = "Permission:reports.view";

    public const string EmailView = "Permission:email.view";
    public const string EmailManage = "Permission:email.manage";
    public const string JobsView = "Permission:jobs.view";
    public const string JobsRun = "Permission:jobs.run";
    public const string JobsScheduleManage = "Permission:jobs.schedule.manage";
    public const string JobsCustomManage = "Permission:jobs.custom.manage";
    public const string OptimizationView = "Permission:optimization.view";
    public const string OptimizationManage = "Permission:optimization.manage";

    public const string AuditView = "Permission:audit.view";
    public const string SecurityView = "Permission:security.view";
    public const string SystemView = "Permission:system.view";
    public const string UsersView = "Permission:users.view";
    public const string UsersManage = "Permission:users.manage";
    public const string RolesView = "Permission:roles.view";
    public const string RolesManage = "Permission:roles.manage";
    public const string SettingsManage = "Permission:settings.manage";
    public const string DatabaseManage = "Permission:database.manage";
    public const string MigrationsManage = "Permission:migrations.manage";
    public const string MaintenanceView = "Permission:maintenance.view";
    public const string MaintenanceManage = "Permission:maintenance.manage";
}
