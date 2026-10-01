using Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace API.Health;

/// <summary>Readiness: the database answers a trivial query within the check timeout.</summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly ISystemDbContextFactory _factory;
    public DatabaseHealthCheck(ISystemDbContextFactory factory) => _factory = factory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = _factory.Create();
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database unreachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
