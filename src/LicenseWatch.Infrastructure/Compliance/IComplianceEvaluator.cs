namespace LicenseWatch.Infrastructure.Compliance;

public interface IComplianceEvaluator
{
    Task<ComplianceEvaluationResult> EvaluateAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
}

public record ComplianceEvaluationResult(
    DateOnly WindowStart,
    DateOnly WindowEnd,
    int Opened,
    int Resolved,
    int Updated,
    int TotalOpen,
    int TotalAcknowledged,
    int TotalResolved);
