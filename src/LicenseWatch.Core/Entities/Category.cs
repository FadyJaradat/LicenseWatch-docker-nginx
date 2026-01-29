namespace LicenseWatch.Core.Entities;

public class Category
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<License> Licenses { get; set; } = new List<License>();
}
