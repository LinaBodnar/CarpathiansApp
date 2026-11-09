using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Статистика кешу в health check сервісу: записи й розмір L1, частка попадань L1/L2, стан Redis.
/// Недоступний Redis — Degraded, а не Unhealthy: кеш необов'язковий, сервіс відповідає з L1 і бази.
/// Повний JSON — на /health/cache.
/// </summary>
public sealed class CacheHealthCheck(
    IMemoryCache memory,
    CacheMetrics metrics,
    DistributedCacheCircuit circuit,
    ICacheService cache) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var stats = memory.GetCurrentStatistics();
        var hasRedis = cache is CacheService { HasDistributedLevel: true };

        var data = new Dictionary<string, object>
        {
            ["l1.entries"] = stats?.CurrentEntryCount ?? 0,
            ["l1.sizeKb"] = stats?.CurrentEstimatedSize ?? 0,
            ["l1.totalHits"] = stats?.TotalHits ?? 0,
            ["l1.totalMisses"] = stats?.TotalMisses ?? 0,
            ["l1.hitRatio"] = Math.Round(metrics.HitRatio(CacheLevel.L1), 3),
            ["l2.configured"] = hasRedis,
            ["l2.available"] = hasRedis && !circuit.IsBroken,
            ["l2.hitRatio"] = Math.Round(metrics.HitRatio(CacheLevel.L2), 3)
        };

        var result = hasRedis && circuit.IsBroken
            ? HealthCheckResult.Degraded("Redis (L2) is unavailable; serving from L1 and the database", data: data)
            : HealthCheckResult.Healthy(hasRedis ? "Two-level cache (L1 memory + L2 Redis) is operational"
                                                 : "In-memory cache (L1) is operational", data);

        return Task.FromResult(result);
    }
}
