using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Usage;

public class UsageAggregator : IUsageAggregator
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<UsageAggregator> _logger;

    public UsageAggregator(AppDbContext dbContext, ILogger<UsageAggregator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UsageAggregationResult> AggregateAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var windowEnd = to ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var windowStart = from ?? windowEnd.AddDays(-29);
        if (windowStart > windowEnd)
        {
            (windowStart, windowEnd) = (windowEnd, windowStart);
        }

        var licensesProcessed = await _dbContext.Licenses.AsNoTracking().CountAsync(cancellationToken);
        _logger.LogInformation("Usage aggregation placeholder executed for {Start} to {End}.", windowStart, windowEnd);

        return new UsageAggregationResult(windowStart, windowEnd, 0, licensesProcessed);
    }
}
