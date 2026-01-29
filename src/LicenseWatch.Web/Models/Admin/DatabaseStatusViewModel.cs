namespace LicenseWatch.Web.Models.Admin;

public class DatabaseStatusViewModel
{
    public string Environment { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string MaskedConnectionString { get; set; } = string.Empty;
    public bool CanConnect { get; set; }
    public int AppliedMigrationsCount { get; set; }
    public IEnumerable<string> AppliedMigrations { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> PendingMigrations { get; set; } = Enumerable.Empty<string>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}
