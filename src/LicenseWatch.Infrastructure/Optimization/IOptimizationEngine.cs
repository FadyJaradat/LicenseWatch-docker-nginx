namespace LicenseWatch.Infrastructure.Optimization;

public interface IOptimizationEngine
{
    Task<OptimizationResult> GenerateInsightsAsync(int windowDays = 30, CancellationToken cancellationToken = default);
}

public record OptimizationResult(int Created, int Updated, int Deactivated);
