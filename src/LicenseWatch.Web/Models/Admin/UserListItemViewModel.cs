namespace LicenseWatch.Web.Models.Admin;

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsLocked { get; set; }
    public string LastLoginDisplay { get; set; } = "—";
    public bool CanDelete { get; set; }
    public string? DeleteDisabledReason { get; set; }
}

public class UsersListViewModel
{
    public IReadOnlyCollection<UserListItemViewModel> Users { get; set; } = Array.Empty<UserListItemViewModel>();
    public string? Search { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}
