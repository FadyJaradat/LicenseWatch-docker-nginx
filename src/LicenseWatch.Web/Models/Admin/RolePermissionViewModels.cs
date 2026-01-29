namespace LicenseWatch.Web.Models.Admin;

public class RoleDetailViewModel
{
    public string RoleName { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
    public int UserCount { get; set; }
    public IReadOnlyList<RolePermissionGroupViewModel> Groups { get; set; } = Array.Empty<RolePermissionGroupViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class RolePermissionGroupViewModel
{
    public string GroupName { get; set; } = string.Empty;
    public IReadOnlyList<RolePermissionItemViewModel> Permissions { get; set; } = Array.Empty<RolePermissionItemViewModel>();
    public bool AllSelected { get; set; }
}

public class RolePermissionItemViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsGranted { get; set; }
    public bool IsManage { get; set; }
}

