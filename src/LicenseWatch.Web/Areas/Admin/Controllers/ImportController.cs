using System.Globalization;
using System.Security.Claims;
using CsvHelper;
using CsvHelper.Configuration;
using LicenseWatch.Core.Entities;
using LicenseWatch.Core.Services;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using LicenseWatch.Web.Security;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.ImportManage)]
[Route("admin/import")]
public class ImportController : Controller
{
    private static readonly string[] RequiredColumns = { "LicenseName", "CategoryName" };
    private static readonly string[] AllowedContentTypes = { "text/csv", "application/vnd.ms-excel", "text/plain" };
    private readonly AppDbContext _dbContext;
    private readonly ImportOptions _options;
    private readonly IBootstrapSettingsStore _settingsStore;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        AppDbContext dbContext,
        IOptions<ImportOptions> options,
        IBootstrapSettingsStore settingsStore,
        ILogger<ImportController> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var vm = new ImportLandingViewModel
        {
            MaxSizeMb = _options.MaxSizeMb,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertDetails = TempData["AlertDetails"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("upload")]
    public IActionResult Upload()
    {
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            SetTempAlert("Please choose a CSV file to upload.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            SetTempAlert("Only .csv files are supported.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            SetTempAlert("CSV content type is not supported.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var maxBytes = _options.MaxSizeMb * 1024L * 1024L;
        if (file.Length > maxBytes)
        {
            SetTempAlert($"File exceeds {_options.MaxSizeMb} MB limit.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var storedFileName = $"{Guid.NewGuid():N}.csv";
        var storedPath = GetStoredFilePath(storedFileName);
        try
        {
            await using var stream = System.IO.File.Create(storedPath);
            await file.CopyToAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store import file");
            SetTempAlert("Failed to save the CSV file. Please try again.", "danger");
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var originalFileName = TrimToLength(Path.GetFileName(file.FileName), 255) ?? "import.csv";

        try
        {
            var parseResult = await ParseCsvAsync(storedPath, originalFileName, storedFileName, userId, cancellationToken);
            if (parseResult.Session is null)
            {
                TryDeleteTempFile(storedFileName);
                SetTempAlert(parseResult.ErrorMessage ?? "CSV parsing failed.", "danger", parseResult.ErrorDetails);
                return RedirectToAction(nameof(Index));
            }

            _dbContext.ImportSessions.Add(parseResult.Session);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(Preview), new { sessionId = parseResult.Session.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV import failed for {FileName}", originalFileName);
            TryDeleteTempFile(storedFileName);
            SetTempAlert("Import failed while validating the file.", "danger", ex.Message);
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("preview/{sessionId:guid}")]
    public async Task<IActionResult> Preview(Guid sessionId, string? filter = null)
    {
        var session = await _dbContext.ImportSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
        {
            SetTempAlert("Import session not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var normalizedFilter = string.IsNullOrWhiteSpace(filter) ? "All" : filter;
        var rowsQuery = _dbContext.ImportRows.AsNoTracking().Where(r => r.ImportSessionId == sessionId);
        rowsQuery = normalizedFilter switch
        {
            "Valid" => rowsQuery.Where(r => r.IsValid),
            "Invalid" => rowsQuery.Where(r => !r.IsValid),
            "New" => rowsQuery.Where(r => r.Action == "New"),
            "Update" => rowsQuery.Where(r => r.Action == "Update"),
            _ => rowsQuery
        };

        var rows = await rowsQuery.OrderBy(r => r.RowNumber).ToListAsync();

        var vm = new ImportPreviewViewModel
        {
            SessionId = session.Id,
            OriginalFileName = session.OriginalFileName,
            CreatedAtUtc = session.CreatedAtUtc,
            Status = session.Status,
            TotalRows = session.TotalRows,
            ValidRows = session.ValidRows,
            InvalidRows = session.InvalidRows,
            NewLicenses = session.NewLicenses,
            UpdatedLicenses = session.UpdatedLicenses,
            NewCategories = session.NewCategories,
            Filter = normalizedFilter,
            CanCommit = session.Status == "Pending" && session.InvalidRows == 0 && session.ValidRows > 0,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertDetails = TempData["AlertDetails"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            Rows = rows.Select(r => new ImportRowViewModel
            {
                RowNumber = r.RowNumber,
                LicenseName = r.LicenseName,
                CategoryName = r.CategoryName,
                Vendor = r.Vendor,
                SeatsPurchased = r.SeatsPurchased,
                SeatsAssigned = r.SeatsAssigned,
                ExpiresOnUtc = r.ExpiresOnUtc,
                Notes = r.Notes,
                IsValid = r.IsValid,
                Action = r.Action,
                ErrorMessage = r.ErrorMessage
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost("commit/{sessionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Commit(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.ImportSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            SetTempAlert("Import session not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (session.Status != "Pending")
        {
            SetTempAlert("This import session is no longer pending.", "warning");
            return RedirectToAction(nameof(Preview), new { sessionId });
        }

        if (session.InvalidRows > 0)
        {
            SetTempAlert("Fix invalid rows before committing the import.", "warning");
            return RedirectToAction(nameof(Preview), new { sessionId });
        }

        if (session.ValidRows == 0)
        {
            SetTempAlert("No valid rows available to commit.", "warning");
            return RedirectToAction(nameof(Preview), new { sessionId });
        }

        var rows = await _dbContext.ImportRows.Where(r => r.ImportSessionId == sessionId).ToListAsync(cancellationToken);
        if (!rows.Any())
        {
            SetTempAlert("No import rows found for this session.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userEmail = User.Identity?.Name ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var now = DateTime.UtcNow;
        var thresholds = await GetSystemThresholdsAsync(cancellationToken);

        var newCategoryCount = 0;
        var newLicenseCount = 0;
        var updatedLicenseCount = 0;
        var auditEntries = new List<AuditLog>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var categories = await _dbContext.Categories.ToListAsync(cancellationToken);
            var categoryByName = categories.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

            var licenses = await _dbContext.Licenses.Include(l => l.Category).ToListAsync(cancellationToken);
            var licenseById = licenses.ToDictionary(l => l.Id);
            var licenseByKey = new Dictionary<string, License>(StringComparer.OrdinalIgnoreCase);
            foreach (var license in licenses)
            {
                var key = BuildLicenseKey(license.Name, license.Vendor, license.Category?.Name ?? string.Empty);
                licenseByKey.TryAdd(key, license);
            }

            foreach (var row in rows.Where(r => r.IsValid))
            {
                if (!categoryByName.TryGetValue(row.CategoryName, out var category))
                {
                    category = new Category
                    {
                        Id = Guid.NewGuid(),
                        Name = row.CategoryName.Trim(),
                        CreatedAtUtc = now
                    };
                    _dbContext.Categories.Add(category);
                    categoryByName[category.Name] = category;
                    newCategoryCount++;

                    auditEntries.Add(BuildAudit(userId, userEmail, ip, "Category.Created", "Category", category.Id.ToString(),
                        $"Created category {category.Name} via import"));
                }

                License? license = null;
                var hasLicenseId = row.LicenseId.HasValue;
                if (hasLicenseId && licenseById.TryGetValue(row.LicenseId!.Value, out var existingById))
                {
                    license = existingById;
                }
                else if (!hasLicenseId)
                {
                    var key = BuildLicenseKey(row.LicenseName, row.Vendor, row.CategoryName);
                    licenseByKey.TryGetValue(key, out license);
                }

                var isNewLicense = false;
                if (license is null)
                {
                    license = new License
                    {
                        Id = row.LicenseId ?? Guid.NewGuid(),
                        CreatedAtUtc = now
                    };
                    _dbContext.Licenses.Add(license);
                    newLicenseCount++;
                    licenseById[license.Id] = license;
                    licenseByKey[BuildLicenseKey(row.LicenseName, row.Vendor, row.CategoryName)] = license;
                    isNewLicense = true;

                    auditEntries.Add(BuildAudit(userId, userEmail, ip, "License.Created", "License", license.Id.ToString(),
                        $"Created license {row.LicenseName} via import"));
                }
                else
                {
                    updatedLicenseCount++;
                    auditEntries.Add(BuildAudit(userId, userEmail, ip, "License.Updated", "License", license.Id.ToString(),
                        $"Updated license {row.LicenseName} via import"));
                }

                license.Name = row.LicenseName.Trim();
                license.Vendor = row.Vendor?.Trim();
                license.CategoryId = category.Id;
                license.SeatsPurchased = row.SeatsPurchased;
                license.SeatsAssigned = row.SeatsAssigned;
                license.ExpiresOnUtc = row.ExpiresOnUtc?.Date;
                license.Status = LicenseStatusCalculator.ComputeStatus(row.ExpiresOnUtc, thresholds.CriticalDays, thresholds.WarningDays);
                license.Notes = row.Notes?.Trim();
                license.UpdatedAtUtc = isNewLicense ? null : now;
            }

            session.Status = "Committed";
            session.CompletedAtUtc = now;
            session.NewCategories = newCategoryCount;
            session.NewLicenses = newLicenseCount;
            session.UpdatedLicenses = updatedLicenseCount;

            auditEntries.Add(BuildAudit(userId, userEmail, ip, "Import.Committed", "ImportSession", session.Id.ToString(),
                $"Committed import: {newLicenseCount} new, {updatedLicenseCount} updated, {newCategoryCount} categories"));

            _dbContext.AuditLogs.AddRange(auditEntries);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Import session {SessionId} committed with {NewLicenses} new and {UpdatedLicenses} updated licenses",
                session.Id, newLicenseCount, updatedLicenseCount);

            TryDeleteTempFile(session.StoredFileName);
            return RedirectToAction(nameof(Result), new { sessionId });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to commit import session {SessionId}", sessionId);
            SetTempAlert("Import commit failed. No changes were applied.", "danger", ex.Message);
            return RedirectToAction(nameof(Preview), new { sessionId });
        }
    }

    [HttpPost("cancel/{sessionId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.ImportSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            SetTempAlert("Import session not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (session.Status == "Pending")
        {
            session.Status = "Cancelled";
            session.CompletedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            TryDeleteTempFile(session.StoredFileName);
        }

        SetTempAlert("Import session cancelled.", "info");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("result/{sessionId:guid}")]
    public async Task<IActionResult> Result(Guid sessionId)
    {
        var session = await _dbContext.ImportSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null)
        {
            SetTempAlert("Import session not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var vm = new ImportResultViewModel
        {
            SessionId = session.Id,
            OriginalFileName = session.OriginalFileName,
            CompletedAtUtc = session.CompletedAtUtc,
            TotalRows = session.TotalRows,
            NewLicenses = session.NewLicenses,
            UpdatedLicenses = session.UpdatedLicenses,
            NewCategories = session.NewCategories
        };

        return View(vm);
    }

    [HttpGet("sample")]
    public IActionResult Sample()
    {
        var csv = string.Join(Environment.NewLine, new[]
        {
            "LicenseId,LicenseName,CategoryName,Vendor,SeatsPurchased,SeatsAssigned,ExpiresOn,Notes",
            ",Acme Suite,Productivity,Acme Corp,120,80,2026-06-30,Annual renewal",
            ",Cloud Shield,Security,BlueSec,50,50,2026-03-15,Core security tooling"
        });

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "licensewatch-sample.csv");
    }

    [HttpGet("errors/{sessionId:guid}")]
    public async Task<IActionResult> DownloadErrors(Guid sessionId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.ImportRows.AsNoTracking()
            .Where(r => r.ImportSessionId == sessionId && !r.IsValid)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(cancellationToken);

        if (!rows.Any())
        {
            SetTempAlert("No invalid rows found for this session.", "info");
            return RedirectToAction(nameof(Preview), new { sessionId });
        }

        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteField("RowNumber");
            csv.WriteField("LicenseId");
            csv.WriteField("LicenseName");
            csv.WriteField("CategoryName");
            csv.WriteField("Vendor");
            csv.WriteField("SeatsPurchased");
            csv.WriteField("SeatsAssigned");
            csv.WriteField("ExpiresOn");
            csv.WriteField("Notes");
            csv.WriteField("ErrorMessage");
            await csv.NextRecordAsync();

            foreach (var row in rows)
            {
                csv.WriteField(row.RowNumber);
                csv.WriteField(row.LicenseIdRaw);
                csv.WriteField(row.LicenseName);
                csv.WriteField(row.CategoryName);
                csv.WriteField(row.Vendor);
                csv.WriteField(row.SeatsPurchased);
                csv.WriteField(row.SeatsAssigned);
                csv.WriteField(row.ExpiresOnUtc?.ToString("yyyy-MM-dd"));
                csv.WriteField(row.Notes);
                csv.WriteField(row.ErrorMessage);
                await csv.NextRecordAsync();
            }
        }

        stream.Position = 0;
        var fileName = $"import-errors-{sessionId}.csv";
        return File(stream, "text/csv", fileName);
    }

    private async Task<(ImportSession? Session, string? ErrorMessage, string? ErrorDetails)> ParseCsvAsync(
        string filePath,
        string originalFileName,
        string storedFileName,
        string userId,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        List<ImportRow> rows = new();
        string[] headers;
        try
        {
            using var reader = new StreamReader(filePath);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim,
                MissingFieldFound = null,
                BadDataFound = null
            };

            using var csv = new CsvReader(reader, config);
            if (!await csv.ReadAsync())
            {
                return (null, "CSV header row is missing.", null);
            }

            csv.ReadHeader();
            headers = csv.HeaderRecord?.Select(h => h.Trim()).ToArray() ?? Array.Empty<string>();
            var headerSet = new HashSet<string>(headers, StringComparer.OrdinalIgnoreCase);
            var missingRequired = RequiredColumns.Where(r => !headerSet.Contains(r)).ToList();
            if (missingRequired.Count > 0)
            {
                return (null, $"Missing required columns: {string.Join(", ", missingRequired)}", null);
            }

            var seenLicenseIds = new HashSet<Guid>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rowNumber = 0;

            while (await csv.ReadAsync())
            {
                rowNumber++;
                var licenseNameRaw = GetField(csv, "LicenseName");
                var categoryNameRaw = GetField(csv, "CategoryName");
                var licenseIdRaw = GetField(csv, "LicenseId");
                var vendorRaw = GetField(csv, "Vendor");
                var seatsPurchasedRaw = GetField(csv, "SeatsPurchased");
                var seatsAssignedRaw = GetField(csv, "SeatsAssigned");
                var expiresOnRaw = GetField(csv, "ExpiresOn");
                var notesRaw = GetField(csv, "Notes");

                if (IsRowEmpty(licenseNameRaw, categoryNameRaw, licenseIdRaw, vendorRaw, seatsPurchasedRaw, seatsAssignedRaw, expiresOnRaw, notesRaw))
                {
                    continue;
                }

                var errors = new List<string>();
                var licenseName = TrimToLength(licenseNameRaw, 200);
                var categoryName = TrimToLength(categoryNameRaw, 200);
                var vendor = TrimToLength(vendorRaw, 200);
                var notes = TrimToLength(notesRaw, 2000);
                var normalizedLicenseId = TrimToLength(licenseIdRaw, 50);
                var parsedLicenseId = string.IsNullOrWhiteSpace(licenseIdRaw) || licenseIdRaw.Length > 50
                    ? null
                    : ParseLicenseId(licenseIdRaw, errors);
                var seatsPurchased = ParseInt(seatsPurchasedRaw, "SeatsPurchased", errors);
                var seatsAssigned = ParseInt(seatsAssignedRaw, "SeatsAssigned", errors);
                var expiresOnUtc = ParseDate(expiresOnRaw, errors);

                if (string.IsNullOrWhiteSpace(licenseNameRaw))
                {
                    errors.Add("LicenseName is required.");
                }
                else if (licenseNameRaw.Length > 200)
                {
                    errors.Add("LicenseName exceeds 200 characters.");
                }

                if (string.IsNullOrWhiteSpace(categoryNameRaw))
                {
                    errors.Add("CategoryName is required.");
                }
                else if (categoryNameRaw.Length > 200)
                {
                    errors.Add("CategoryName exceeds 200 characters.");
                }

                if (!string.IsNullOrWhiteSpace(vendorRaw) && vendorRaw.Length > 200)
                {
                    errors.Add("Vendor exceeds 200 characters.");
                }

                if (!string.IsNullOrWhiteSpace(notesRaw) && notesRaw.Length > 2000)
                {
                    errors.Add("Notes exceeds 2000 characters.");
                }

                if (!string.IsNullOrWhiteSpace(licenseIdRaw) && licenseIdRaw.Length > 50)
                {
                    errors.Add("LicenseId exceeds 50 characters.");
                }

                if (seatsPurchased.HasValue && seatsAssigned.HasValue && seatsAssigned > seatsPurchased)
                {
                    errors.Add("SeatsAssigned cannot exceed SeatsPurchased.");
                }

                if (!string.IsNullOrWhiteSpace(licenseName) && !string.IsNullOrWhiteSpace(categoryName))
                {
                    var key = BuildLicenseKey(licenseName, vendor, categoryName);
                    if (!seenKeys.Add(key))
                    {
                        errors.Add("Duplicate license entry found in the file.");
                    }
                }

                if (parsedLicenseId.HasValue && !seenLicenseIds.Add(parsedLicenseId.Value))
                {
                    errors.Add("Duplicate LicenseId found in the file.");
                }

                var errorMessage = errors.Count == 0 ? null : string.Join(" ", errors);
                if (!string.IsNullOrWhiteSpace(errorMessage) && errorMessage.Length > 1000)
                {
                    errorMessage = errorMessage[..1000];
                }

                var row = new ImportRow
                {
                    Id = Guid.NewGuid(),
                    ImportSessionId = sessionId,
                    RowNumber = rowNumber,
                    LicenseIdRaw = normalizedLicenseId,
                    LicenseId = parsedLicenseId,
                    LicenseName = licenseName ?? string.Empty,
                    CategoryName = categoryName ?? string.Empty,
                    Vendor = vendor,
                    SeatsPurchased = seatsPurchased,
                    SeatsAssigned = seatsAssigned,
                    ExpiresOnUtc = expiresOnUtc,
                    Notes = notes,
                    IsValid = errors.Count == 0,
                    ErrorMessage = errorMessage
                };

                rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            return (null, "Failed to parse the CSV file.", ex.Message);
        }

        if (rows.Count == 0)
        {
            return (null, "No data rows were found in the CSV.", null);
        }

        var (newLicenses, updatedLicenses, newCategories) = await UpdateRowActionsAsync(rows, cancellationToken);
        var validRows = rows.Count(r => r.IsValid);
        var invalidRows = rows.Count - validRows;

        var session = new ImportSession
        {
            Id = sessionId,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            Status = "Pending",
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            TotalRows = rows.Count,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            NewLicenses = newLicenses,
            UpdatedLicenses = updatedLicenses,
            NewCategories = newCategories,
            Rows = rows
        };

        return (session, null, null);
    }

    private async Task<(int NewLicenses, int UpdatedLicenses, int NewCategories)> UpdateRowActionsAsync(
        List<ImportRow> rows,
        CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var categoryByName = categories.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var licenses = await _dbContext.Licenses.Include(l => l.Category).AsNoTracking().ToListAsync(cancellationToken);
        var licenseById = licenses.ToDictionary(l => l.Id);
        var licenseByKey = new Dictionary<string, License>(StringComparer.OrdinalIgnoreCase);
        foreach (var license in licenses)
        {
            var key = BuildLicenseKey(license.Name, license.Vendor, license.Category?.Name ?? string.Empty);
            licenseByKey.TryAdd(key, license);
        }

        var newCategoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var newLicenses = 0;
        var updatedLicenses = 0;

        foreach (var row in rows)
        {
            if (!row.IsValid)
            {
                row.Action = "Invalid";
                continue;
            }

            if (!categoryByName.ContainsKey(row.CategoryName) && !string.IsNullOrWhiteSpace(row.CategoryName))
            {
                newCategoryNames.Add(row.CategoryName);
            }

            if (row.LicenseId.HasValue && licenseById.ContainsKey(row.LicenseId.Value))
            {
                row.Action = "Update";
                updatedLicenses++;
                continue;
            }

            if (!row.LicenseId.HasValue)
            {
                var key = BuildLicenseKey(row.LicenseName, row.Vendor, row.CategoryName);
                if (licenseByKey.TryGetValue(key, out var existing))
                {
                    row.Action = "Update";
                    row.LicenseId = existing.Id;
                    updatedLicenses++;
                    continue;
                }
            }

            row.Action = "New";
            newLicenses++;
        }

        return (newLicenses, updatedLicenses, newCategoryNames.Count);
    }

    private static string BuildLicenseKey(string licenseName, string? vendor, string categoryName)
    {
        var normalizedName = licenseName.Trim().ToLowerInvariant();
        var normalizedVendor = vendor?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedCategory = categoryName.Trim().ToLowerInvariant();
        return $"{normalizedName}|{normalizedVendor}|{normalizedCategory}";
    }

    private static string? GetField(CsvReader csv, string name)
    {
        if (csv.TryGetField(name, out string? value))
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        return null;
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static Guid? ParseLicenseId(string? raw, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (Guid.TryParse(raw, out var id))
        {
            return id;
        }

        errors.Add("LicenseId must be a valid GUID.");
        return null;
    }

    private static int? ParseInt(string? raw, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0)
        {
            return value;
        }

        errors.Add($"{fieldName} must be a non-negative whole number.");
        return null;
    }

    private static DateTime? ParseDate(string? raw, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        }

        errors.Add("ExpiresOn must be a valid date (yyyy-MM-dd).");
        return null;
    }

    private static bool IsRowEmpty(params string?[] fields)
    {
        return fields.All(string.IsNullOrWhiteSpace);
    }

    private static AuditLog BuildAudit(string userId, string email, string? ip, string action, string entityType, string entityId, string summary)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = userId,
            ActorEmail = email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            IpAddress = ip
        };
    }

    private string GetStoredFilePath(string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        return Path.Combine(_options.RootPath, safeName);
    }

    private void TryDeleteTempFile(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            return;
        }

        try
        {
            var path = GetStoredFilePath(storedFileName);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete import temp file");
        }
    }

    private void SetTempAlert(string message, string style, string? details = null)
    {
        TempData["AlertMessage"] = message;
        TempData["AlertStyle"] = style;
        if (!string.IsNullOrWhiteSpace(details))
        {
            TempData["AlertDetails"] = details;
        }
    }

    private async Task<(int CriticalDays, int WarningDays)> GetSystemThresholdsAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        return LicenseStatusCalculator.NormalizeThresholds(
            settings.Compliance.CriticalDays,
            settings.Compliance.WarningDays);
    }
}
