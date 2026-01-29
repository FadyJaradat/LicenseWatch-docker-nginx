namespace LicenseWatch.Web.Models.Admin;

public class RoleListItem
{
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
}

public class RoleListViewModel
{
    public IReadOnlyCollection<RoleListItem> Roles { get; set; } = Array.Empty<RoleListItem>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}
