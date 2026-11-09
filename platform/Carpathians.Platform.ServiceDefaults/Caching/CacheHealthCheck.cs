using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Статистика кешу в health check сервісу: кількість і розмір записів у пам'яті, попадання/промахи.
/// Повний JSON — на /health/cache.
/// </summary>
public sealed class CacheHealthCheck(IMemoryCache memory, CacheMetrics metrics) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var stats = memory.GetCurrentStatistics();
        var data = new Dictionary<string, object>
        {
            ["l1.entries"] = stats?.CurrentEntryCount ?? 0,
            ["l1.sizeKb"] = stats?.CurrentEstimatedSize ?? 0,
            ["l1.totalHits"] = stats?.TotalHits ?? 0,
            ["l1.totalMisses"] = stats?.TotalMisses ?? 0,
            ["l1.hitRatio"] = Math.Round(metrics.LocalHitRatio, 3)
        };

        return Task.FromResult(HealthCheckResult.Healthy("In-memory cache (L1) is operational", data));
    }
}
