namespace LicenseWatch.Core.Entities;

public class RolePermission
{
    public Guid Id { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string PermissionKey { get; set; } = string.Empty;

    public DateTime GrantedAtUtc { get; set; }

    public string GrantedByUserId { get; set; } = string.Empty;
}
