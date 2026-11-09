using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>Рівні кешу — значення тегу cache.level у метриках і властивості CacheLevel у логах.</summary>
public static class CacheLevel
{
    /// <summary>IMemoryCache — пам'ять процесу.</summary>
    public const string L1 = "l1";

    /// <summary>Redis — розподілений кеш, спільний для всіх екземплярів.</summary>
    public const string L2 = "l2";

    /// <summary>Джерело даних (база даних) — для тривалості завантаження при промаху.</summary>
    public const string Source = "source";
}

/// <summary>
/// Власні метрики ефективності кешу (Meter "Carpathians.Cache", експорт через OpenTelemetry в Aspire Dashboard):
/// попадання/промахи окремо для L1 і L2 (та окремо для HTTP і gRPC — тег cache.transport),
/// частка попадань, розмір кешу в пам'яті, доступність Redis, витіснення, інвалідації, тривалість операцій.
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Carpathians.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Counter<long> _evictions;
    private readonly Histogram<double> _duration;

    private readonly LevelStats _l1 = new();
    private readonly LevelStats _l2 = new();

    public CacheMetrics(IMeterFactory meterFactory, IMemoryCache memory, DistributedCacheCircuit circuit)
    {
        var meter = meterFactory.Create(MeterName);

        _hits = meter.CreateCounter<long>("cache.hits", "{hit}", "Cache lookups served from the cache level");
        _misses = meter.CreateCounter<long>("cache.misses", "{miss}", "Cache lookups that went to the next level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", "{key}", "Keys removed after data changes");
        _evictions = meter.CreateCounter<long>("cache.evictions", "{entry}", "Entries evicted by TTL or size limit");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", "ms", "Duration of cache operations");

        meter.CreateObservableGauge("cache.hit_ratio", ObserveHitRatio, "1", "Share of lookups served by the cache level");
        meter.CreateObservableGauge("cache.memory.entries",
            () => memory.GetCurrentStatistics()?.CurrentEntryCount ?? 0, "{entry}", "Entries in the in-memory cache (L1)");
        meter.CreateObservableGauge("cache.memory.size",
            () => memory.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0, "KiBy", "Estimated size of the in-memory cache (L1)");
        meter.CreateObservableGauge("cache.distributed.available",
            () => circuit.IsBroken ? 0 : 1, "1", "1 — Redis (L2) is reachable, 0 — degraded to L1 and the database");
    }

    public void Hit(string level)
    {
        Stats(level)?.Hit();
        _hits.Add(1, Level(level), Transport);
    }

    public void Miss(string level)
    {
        Stats(level)?.Miss();
        _misses.Add(1, Level(level), Transport);
    }

    public void Invalidated(int keys, string reason)
        => _invalidations.Add(keys, new KeyValuePair<string, object?>("cache.reason", reason));

    public void Evicted(string level, string reason)
        => _evictions.Add(1, Level(level), new KeyValuePair<string, object?>("cache.reason", reason));

    public void RecordDuration(string level, string operation, TimeSpan elapsed)
        => _duration.Record(elapsed.TotalMilliseconds, Level(level),
            new KeyValuePair<string, object?>("cache.operation", operation));

    /// <summary>Частка попадань рівня від запуску процесу (для health check).</summary>
    public double HitRatio(string level) => Stats(level)?.Ratio ?? 0;

    private IEnumerable<Measurement<double>> ObserveHitRatio()
    {
        if (_l1.HasData)
            yield return new Measurement<double>(_l1.Ratio, Level(CacheLevel.L1));
        if (_l2.HasData)
            yield return new Measurement<double>(_l2.Ratio, Level(CacheLevel.L2));
    }

    private LevelStats? Stats(string level) => level switch
    {
        CacheLevel.L1 => _l1,
        CacheLevel.L2 => _l2,
        _ => null
    };

    private static KeyValuePair<string, object?> Level(string level) => new("cache.level", level);

    /// <summary>Звідки прийшов запит: http, grpc або background (прогрівання).</summary>
    private static KeyValuePair<string, object?> Transport => new("cache.transport", CacheCallContext.Transport);

    private sealed class LevelStats
    {
        private long _hits;
        private long _misses;

        public void Hit() => Interlocked.Increment(ref _hits);
        public void Miss() => Interlocked.Increment(ref _misses);

        public bool HasData => Interlocked.Read(ref _hits) + Interlocked.Read(ref _misses) > 0;

        public double Ratio
        {
            get
            {
                var hits = Interlocked.Read(ref _hits);
                var total = hits + Interlocked.Read(ref _misses);
                return total == 0 ? 0 : (double)hits / total;
            }
        }
    }
}
