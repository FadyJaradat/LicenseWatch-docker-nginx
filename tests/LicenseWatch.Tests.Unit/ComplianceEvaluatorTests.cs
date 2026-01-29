using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Compliance;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LicenseWatch.Tests.Unit;

public class ComplianceEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_CreatesOveruseAndExpiredViolations()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Core Apps",
            CreatedAtUtc = DateTime.UtcNow
        };

        var license = new License
        {
            Id = Guid.NewGuid(),
            Name = "Example Suite",
            CategoryId = category.Id,
            SeatsPurchased = 10,
            ExpiresOnUtc = DateTime.UtcNow.Date.AddDays(-2),
            Status = "Expired",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Categories.Add(category);
        dbContext.Licenses.Add(license);
        dbContext.UsageDailySummaries.Add(new UsageDailySummary
        {
            Id = Guid.NewGuid(),
            LicenseId = license.Id,
            UsageDateUtc = DateTime.UtcNow.Date,
            MaxSeatsUsed = 15
        });

        await dbContext.SaveChangesAsync();

        var evaluator = new ComplianceEvaluator(dbContext, NullLogger<ComplianceEvaluator>.Instance);
        await evaluator.EvaluateAsync(DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date));

        var violations = await dbContext.ComplianceViolations.AsNoTracking().ToListAsync();

        Assert.Contains(violations, v => v.RuleKey == "Overuse" && v.Severity == "Critical");
        Assert.Contains(violations, v => v.RuleKey == "Expired" && v.Severity == "Critical");
    }
}
