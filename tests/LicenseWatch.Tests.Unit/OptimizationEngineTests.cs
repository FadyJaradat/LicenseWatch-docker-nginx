using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Optimization;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LicenseWatch.Tests.Unit;

public class OptimizationEngineTests
{
    [Fact]
    public async Task GenerateInsightsAsync_CreatesUnderutilizedAndUnassignedInsights()
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
            Name = "Productivity",
            CreatedAtUtc = DateTime.UtcNow
        };

        var underutilized = new License
        {
            Id = Guid.NewGuid(),
            Name = "Suite Pro",
            CategoryId = category.Id,
            SeatsPurchased = 100,
            SeatsAssigned = 15,
            CreatedAtUtc = DateTime.UtcNow
        };

        var unassigned = new License
        {
            Id = Guid.NewGuid(),
            Name = "Analytics Pro",
            CategoryId = category.Id,
            SeatsPurchased = 50,
            SeatsAssigned = 30,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.Categories.Add(category);
        dbContext.Licenses.AddRange(underutilized, unassigned);
        dbContext.UsageDailySummaries.Add(new UsageDailySummary
        {
            Id = Guid.NewGuid(),
            LicenseId = underutilized.Id,
            UsageDateUtc = DateTime.UtcNow.Date,
            MaxSeatsUsed = 10
        });

        await dbContext.SaveChangesAsync();

        var engine = new OptimizationEngine(dbContext, NullLogger<OptimizationEngine>.Instance);
        await engine.GenerateInsightsAsync(30);

        var insights = await dbContext.OptimizationInsights.AsNoTracking().ToListAsync();

        Assert.Contains(insights, i => i.Key == "UnderutilizedSeats" && i.Severity == "Critical");
        Assert.Contains(insights, i => i.Key == "UnassignedSeats" && i.Severity == "Warning");
    }
}
