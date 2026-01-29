namespace LicenseWatch.Web.Models.Admin;

public class CategoryListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int LicenseCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CategoryListViewModel
{
    public IReadOnlyCollection<CategoryListItemViewModel> Categories { get; set; } = Array.Empty<CategoryListItemViewModel>();
    public string? Search { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class CategoryFormViewModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
