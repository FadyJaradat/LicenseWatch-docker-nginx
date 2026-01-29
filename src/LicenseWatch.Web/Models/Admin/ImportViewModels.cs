namespace LicenseWatch.Web.Models.Admin;

public class ImportLandingViewModel
{
    public int MaxSizeMb { get; set; }

    public string AllowedExtensions { get; set; } = ".csv";

    public string? AlertMessage { get; set; }

    public string? AlertDetails { get; set; }

    public string AlertStyle { get; set; } = "info";
}

public class ImportPreviewViewModel
{
    public Guid SessionId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string Status { get; set; } = "Pending";

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    public int NewLicenses { get; set; }

    public int UpdatedLicenses { get; set; }

    public int NewCategories { get; set; }

    public string Filter { get; set; } = "All";

    public bool CanCommit { get; set; }

    public string? AlertMessage { get; set; }

    public string? AlertDetails { get; set; }

    public string AlertStyle { get; set; } = "info";

    public IReadOnlyList<ImportRowViewModel> Rows { get; set; } = Array.Empty<ImportRowViewModel>();
}

public class ImportRowViewModel
{
    public int RowNumber { get; set; }

    public string LicenseName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? Vendor { get; set; }

    public int? SeatsPurchased { get; set; }

    public int? SeatsAssigned { get; set; }

    public DateTime? ExpiresOnUtc { get; set; }

    public string? Notes { get; set; }

    public bool IsValid { get; set; }

    public string Action { get; set; } = "Invalid";

    public string? ErrorMessage { get; set; }
}

public class ImportResultViewModel
{
    public Guid SessionId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime? CompletedAtUtc { get; set; }

    public int TotalRows { get; set; }

    public int NewLicenses { get; set; }

    public int UpdatedLicenses { get; set; }

    public int NewCategories { get; set; }
}
