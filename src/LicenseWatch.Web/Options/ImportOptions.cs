namespace LicenseWatch.Web.Options;

public class ImportOptions
{
    public int MaxSizeMb { get; set; } = 5;

    public string RootPath { get; set; } = string.Empty;
}
