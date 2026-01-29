using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LicenseWatch.Infrastructure.Health;

public sealed class SqliteFileHealthCheck : IHealthCheck
{
    private readonly string _filePath;
    private readonly string _displayName;

    public SqliteFileHealthCheck(string filePath, string displayName)
    {
        _filePath = filePath;
        _displayName = displayName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            return HealthCheckResult.Unhealthy($"{_displayName} path is not configured.");
        }

        if (!File.Exists(_filePath))
        {
            return HealthCheckResult.Unhealthy($"{_displayName} file not found.");
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = _filePath,
                Mode = SqliteOpenMode.ReadWrite
            };

            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy($"{_displayName} reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{_displayName} unavailable.", ex);
        }
    }
}
