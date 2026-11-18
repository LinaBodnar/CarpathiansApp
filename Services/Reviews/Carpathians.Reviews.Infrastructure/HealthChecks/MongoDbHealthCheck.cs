using Carpathians.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace Carpathians.Reviews.Infrastructure.HealthChecks;

/// <summary>Перевірка доступності MongoDB командою <c>ping</c> (детально — у Лаб. 07).</summary>
public class MongoDbHealthCheck(MongoDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthContext, CancellationToken ct = default)
    {
        try
        {
            await context.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
            return HealthCheckResult.Healthy($"MongoDB '{context.Database.DatabaseNamespace.DatabaseName}' is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unreachable", ex);
        }
    }
}
