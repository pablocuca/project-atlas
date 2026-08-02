using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Atlas.Host.Modules;

internal sealed class PositionsPostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1 FROM positions.position LIMIT 0");
            await command.ExecuteNonQueryAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot reach the positions schema.", ex);
        }
    }
}
