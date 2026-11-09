using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>Рівні кешу — значення тегу cache.level у метриках і властивості CacheLevel у логах.</summary>
public static class CacheLevel
{
    /// <summary>IMemoryCache — пам'ять процесу.</summary>
    public const string L1 = "l1";

    /// <summary>Джерело даних (база даних) — для тривалості завантаження при промаху.</summary>
    public const string Source = "source";
}

/// <summary>
/// Власні метрики ефективності кешу (Meter "Carpathians.Cache", експорт через OpenTelemetry в Aspire Dashboard):
/// попадання/промахи, частка попадань, розмір кешу, витіснення, інвалідації, тривалість операцій.
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Carpathians.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Counter<long> _evictions;
    private readonly Histogram<double> _duration;

    private long _l1Hits;
    private long _l1Misses;

    public CacheMetrics(IMeterFactory meterFactory, IMemoryCache memory)
    {
        var meter = meterFactory.Create(MeterName);

        _hits = meter.CreateCounter<long>("cache.hits", "{hit}", "Cache lookups served from the cache");
        _misses = meter.CreateCounter<long>("cache.misses", "{miss}", "Cache lookups that went to the next level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", "{key}", "Keys removed after data changes");
        _evictions = meter.CreateCounter<long>("cache.evictions", "{entry}", "Entries evicted by TTL or size limit");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", "ms", "Duration of cache operations");

        meter.CreateObservableGauge("cache.hit_ratio", ObserveHitRatio, "1", "Share of lookups served by the cache level");
        meter.CreateObservableGauge("cache.memory.entries",
            () => memory.GetCurrentStatistics()?.CurrentEntryCount ?? 0, "{entry}", "Entries in the in-memory cache");
        meter.CreateObservableGauge("cache.memory.size",
            () => memory.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0, "KiBy", "Estimated size of the in-memory cache");
    }

    public void Hit(string level)
    {
        if (level == CacheLevel.L1)
            Interlocked.Increment(ref _l1Hits);
        _hits.Add(1, Level(level));
    }

    public void Miss(string level)
    {
        if (level == CacheLevel.L1)
            Interlocked.Increment(ref _l1Misses);
        _misses.Add(1, Level(level));
    }

    public void Invalidated(int keys, string reason)
        => _invalidations.Add(keys, new KeyValuePair<string, object?>("cache.reason", reason));

    public void Evicted(string level, string reason)
        => _evictions.Add(1, Level(level), new KeyValuePair<string, object?>("cache.reason", reason));

    public void RecordDuration(string level, string operation, TimeSpan elapsed)
        => _duration.Record(elapsed.TotalMilliseconds, Level(level),
            new KeyValuePair<string, object?>("cache.operation", operation));

    /// <summary>Частка попадань у L1 від запуску процесу (для health check).</summary>
    public double LocalHitRatio => Ratio(Interlocked.Read(ref _l1Hits), Interlocked.Read(ref _l1Misses));

    private IEnumerable<Measurement<double>> ObserveHitRatio()
    {
        var hits = Interlocked.Read(ref _l1Hits);
        var misses = Interlocked.Read(ref _l1Misses);
        if (hits + misses > 0)
            yield return new Measurement<double>(Ratio(hits, misses), Level(CacheLevel.L1));
    }

    private static double Ratio(long hits, long misses) => hits + misses == 0 ? 0 : (double)hits / (hits + misses);

    private static KeyValuePair<string, object?> Level(string level) => new("cache.level", level);
}
