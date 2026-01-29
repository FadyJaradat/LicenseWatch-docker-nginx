using LicenseWatch.Core.Diagnostics;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Auditing;

public class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(AppDbContext dbContext, ILogger<AuditLogger> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        try
        {
            if (entry.Id == Guid.Empty)
            {
                entry.Id = Guid.NewGuid();
            }

            if (string.IsNullOrWhiteSpace(entry.CorrelationId))
            {
                entry.CorrelationId = CorrelationContext.Current;
            }

            if (string.IsNullOrWhiteSpace(entry.ActorDisplay))
            {
                entry.ActorDisplay = entry.ActorEmail;
            }

            if (string.IsNullOrWhiteSpace(entry.ImpersonatedDisplay) && !string.IsNullOrWhiteSpace(entry.ImpersonatedUserId))
            {
                entry.ImpersonatedDisplay = entry.ImpersonatedUserId;
            }

            _dbContext.AuditLogs.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log {Action} for {EntityType}", entry.Action, entry.EntityType);
        }
    }
}
