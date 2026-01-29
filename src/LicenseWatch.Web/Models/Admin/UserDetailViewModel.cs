namespace LicenseWatch.Web.Models.Admin;

public class UserDetailViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsLocked { get; set; }
    public string LastLoginDisplay { get; set; } = "—";
    public IReadOnlyCollection<string> AllRoles { get; set; } = Array.Empty<string>();
    public string? TempPassword { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
    public bool CanDelete { get; set; }
    public string? DeleteDisabledReason { get; set; }
}
