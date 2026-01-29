namespace LicenseWatch.Infrastructure.Usage;

public interface IUsageAggregator
{
    Task<UsageAggregationResult> AggregateAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
}

public record UsageAggregationResult(DateOnly WindowStart, DateOnly WindowEnd, int SummariesUpdated, int LicensesProcessed);
