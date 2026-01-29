using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using LicenseWatch.Core.Services;

namespace LicenseWatch.Infrastructure.Reports;

public interface IReportExportService
{
    ReportExportResult ExportLicenseInventoryCsv(IReadOnlyList<LicenseReportRow> rows, string fileName);
    ReportExportResult ExportLicenseInventoryExcel(IReadOnlyList<LicenseReportRow> rows, string fileName);
    ReportExportResult ExportExpirationCsv(IReadOnlyList<ExpirationReportRow> rows, string fileName);
    ReportExportResult ExportExpirationExcel(IReadOnlyList<ExpirationReportRow> rows, string fileName);
    ReportExportResult ExportComplianceCsv(IReadOnlyList<ComplianceReportRow> rows, string fileName);
    ReportExportResult ExportComplianceExcel(IReadOnlyList<ComplianceReportRow> rows, string fileName);
    ReportExportResult ExportUsageCsv(IReadOnlyList<UsageReportRow> rows, string fileName);
    ReportExportResult ExportUsageExcel(IReadOnlyList<UsageReportRow> rows, string fileName);
}

public record ReportExportResult(string ContentType, string FileName, byte[] Content);

public class ReportExportService : IReportExportService
{
    private static readonly UTF8Encoding Utf8WithBom = new(true);

    public ReportExportResult ExportLicenseInventoryCsv(IReadOnlyList<LicenseReportRow> rows, string fileName)
    {
        var bytes = BuildCsv(writer =>
        {
            writer.WriteField("Name");
            writer.WriteField("Vendor");
            writer.WriteField("Category");
            writer.WriteField("SeatsPurchased");
            writer.WriteField("SeatsAssigned");
            writer.WriteField("ExpiresOn");
            writer.WriteField("Status");
            writer.NextRecord();

            foreach (var row in rows)
            {
                writer.WriteField(row.Name);
                writer.WriteField(row.Vendor ?? string.Empty);
                writer.WriteField(row.Category);
                writer.WriteField(row.SeatsPurchased?.ToString() ?? string.Empty);
                writer.WriteField(row.SeatsAssigned?.ToString() ?? string.Empty);
                writer.WriteField(row.ExpiresOnUtc?.ToString("yyyy-MM-dd") ?? string.Empty);
                writer.WriteField(row.Status);
                writer.NextRecord();
            }
        });

        return new ReportExportResult("text/csv; charset=utf-8", fileName, bytes);
    }

    public ReportExportResult ExportLicenseInventoryExcel(IReadOnlyList<LicenseReportRow> rows, string fileName)
    {
        using var workbook = new XLWorkbook();
        ApplyMetadata(workbook, "License inventory");
        var sheet = workbook.Worksheets.Add("Licenses");
        WriteHeader(sheet, "Name", "Vendor", "Category", "SeatsPurchased", "SeatsAssigned", "ExpiresOn", "Status");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Name;
            sheet.Cell(rowIndex, 2).Value = row.Vendor ?? string.Empty;
            sheet.Cell(rowIndex, 3).Value = row.Category;
            sheet.Cell(rowIndex, 4).Value = row.SeatsPurchased;
            sheet.Cell(rowIndex, 5).Value = row.SeatsAssigned;
            sheet.Cell(rowIndex, 6).Value = row.ExpiresOnUtc?.ToLocalTime().Date;
            sheet.Cell(rowIndex, 7).Value = row.Status;
            rowIndex++;
        }

        sheet.Column(6).Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        return new ReportExportResult("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName, SaveWorkbook(workbook));
    }

    public ReportExportResult ExportExpirationCsv(IReadOnlyList<ExpirationReportRow> rows, string fileName)
    {
        var bytes = BuildCsv(writer =>
        {
            writer.WriteField("License");
            writer.WriteField("Vendor");
            writer.WriteField("ExpiresOn");
            writer.WriteField("DaysRemaining");
            writer.WriteField("Status");
            writer.NextRecord();

            foreach (var row in rows)
            {
                writer.WriteField(row.LicenseName);
                writer.WriteField(row.Vendor ?? string.Empty);
                writer.WriteField(row.ExpiresOnUtc.ToString("yyyy-MM-dd"));
                writer.WriteField(row.DaysRemaining);
                writer.WriteField(row.Status);
                writer.NextRecord();
            }
        });

        return new ReportExportResult("text/csv; charset=utf-8", fileName, bytes);
    }

    public ReportExportResult ExportExpirationExcel(IReadOnlyList<ExpirationReportRow> rows, string fileName)
    {
        using var workbook = new XLWorkbook();
        ApplyMetadata(workbook, "Expirations");
        var sheet = workbook.Worksheets.Add("Expirations");
        WriteHeader(sheet, "License", "Vendor", "ExpiresOn", "DaysRemaining", "Status");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.LicenseName;
            sheet.Cell(rowIndex, 2).Value = row.Vendor ?? string.Empty;
            sheet.Cell(rowIndex, 3).Value = row.ExpiresOnUtc.ToLocalTime().Date;
            sheet.Cell(rowIndex, 4).Value = row.DaysRemaining;
            sheet.Cell(rowIndex, 5).Value = row.Status;
            rowIndex++;
        }

        sheet.Column(3).Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        return new ReportExportResult("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName, SaveWorkbook(workbook));
    }

    public ReportExportResult ExportComplianceCsv(IReadOnlyList<ComplianceReportRow> rows, string fileName)
    {
        var bytes = BuildCsv(writer =>
        {
            writer.WriteField("Severity");
            writer.WriteField("Title");
            writer.WriteField("License");
            writer.WriteField("Status");
            writer.WriteField("DetectedAtUtc");
            writer.WriteField("Rule");
            writer.NextRecord();

            foreach (var row in rows)
            {
                writer.WriteField(row.Severity);
                writer.WriteField(row.Title);
                writer.WriteField(row.LicenseName ?? "Unknown");
                writer.WriteField(row.Status);
                writer.WriteField(row.DetectedAtUtc.ToString("yyyy-MM-dd HH:mm"));
                writer.WriteField(row.RuleKey);
                writer.NextRecord();
            }
        });

        return new ReportExportResult("text/csv; charset=utf-8", fileName, bytes);
    }

    public ReportExportResult ExportComplianceExcel(IReadOnlyList<ComplianceReportRow> rows, string fileName)
    {
        using var workbook = new XLWorkbook();
        ApplyMetadata(workbook, "Compliance violations");
        var sheet = workbook.Worksheets.Add("Compliance");
        WriteHeader(sheet, "Severity", "Title", "License", "Status", "DetectedAtUtc", "Rule");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.Severity;
            sheet.Cell(rowIndex, 2).Value = row.Title;
            sheet.Cell(rowIndex, 3).Value = row.LicenseName ?? "Unknown";
            sheet.Cell(rowIndex, 4).Value = row.Status;
            sheet.Cell(rowIndex, 5).Value = row.DetectedAtUtc;
            sheet.Cell(rowIndex, 6).Value = row.RuleKey;
            rowIndex++;
        }

        sheet.Column(5).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        return new ReportExportResult("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName, SaveWorkbook(workbook));
    }

    public ReportExportResult ExportUsageCsv(IReadOnlyList<UsageReportRow> rows, string fileName)
    {
        var bytes = BuildCsv(writer =>
        {
            writer.WriteField("License");
            writer.WriteField("Category");
            writer.WriteField("PeakSeatsUsed");
            writer.WriteField("AvgSeatsUsed");
            writer.WriteField("EventsCount");
            writer.WriteField("Period");
            writer.NextRecord();

            foreach (var row in rows)
            {
                writer.WriteField(row.LicenseName);
                writer.WriteField(row.CategoryName);
                writer.WriteField(row.PeakSeatsUsed);
                writer.WriteField(row.AvgSeatsUsed.ToString("F1", CultureInfo.InvariantCulture));
                writer.WriteField(row.EventsCount);
                writer.WriteField($"{row.PeriodStart:yyyy-MM-dd} to {row.PeriodEnd:yyyy-MM-dd}");
                writer.NextRecord();
            }
        });

        return new ReportExportResult("text/csv; charset=utf-8", fileName, bytes);
    }

    public ReportExportResult ExportUsageExcel(IReadOnlyList<UsageReportRow> rows, string fileName)
    {
        using var workbook = new XLWorkbook();
        ApplyMetadata(workbook, "Usage summary");
        var sheet = workbook.Worksheets.Add("Usage");
        WriteHeader(sheet, "License", "Category", "PeakSeatsUsed", "AvgSeatsUsed", "EventsCount", "Period");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.LicenseName;
            sheet.Cell(rowIndex, 2).Value = row.CategoryName;
            sheet.Cell(rowIndex, 3).Value = row.PeakSeatsUsed;
            sheet.Cell(rowIndex, 4).Value = row.AvgSeatsUsed;
            sheet.Cell(rowIndex, 5).Value = row.EventsCount;
            sheet.Cell(rowIndex, 6).Value = $"{row.PeriodStart:yyyy-MM-dd} to {row.PeriodEnd:yyyy-MM-dd}";
            rowIndex++;
        }

        sheet.Column(4).Style.NumberFormat.Format = "0.0";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        return new ReportExportResult("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName, SaveWorkbook(workbook));
    }

    private static byte[] BuildCsv(Action<CsvWriter> writeAction)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Utf8WithBom, leaveOpen: true);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        writeAction(csv);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        var headerRange = sheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2FF");
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static void ApplyMetadata(XLWorkbook workbook, string reportTitle)
    {
        workbook.Properties.Title = $"{reportTitle} ({BuildInfo.DisplayVersion})";
        workbook.Properties.Subject = reportTitle;
        workbook.Properties.Comments = $"Generated by LicenseWatch {BuildInfo.DisplayVersion}.";
    }
}
