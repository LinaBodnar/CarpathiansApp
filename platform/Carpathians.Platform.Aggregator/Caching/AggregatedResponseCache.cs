using System.Diagnostics;
using System.Text.Json;
using Carpathians.Platform.ServiceDefaults.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace Carpathians.Platform.Aggregator.Caching;

/// <summary>
/// Кеш агрегованих відповідей у Redis (IDistributedCache): складена картка зберігається цілком,
/// і повторний запит не робить жодного gRPC-виклику.
/// <list type="bullet">
/// <item>ключі — з параметрів запиту (<see cref="AggregatorCacheKeys"/>), мікросервіси видаляють їх при змінах;</item>
/// <item>короткий TTL (30 с) — коротший за кеші окремих сервісів, щоб агрегована відповідь
/// не виглядала «свіжішою», ніж дані, з яких її зібрано;</item>
/// <item>часткові відповіді (з Warnings) не кешуються — інакше збій одного сервісу «закріпився» б на весь TTL;</item>
/// <item>недоступний Redis не валить Aggregator: запобіжник переводить його в режим без кешу.</item>
/// </list>
/// </summary>
public sealed class AggregatedResponseCache(
    DistributedCacheCircuit circuit,
    CacheMetrics metrics,
    ILogger<AggregatedResponseCache> logger,
    IDistributedCache? redis = null)
{
    /// <summary>TTL агрегованих відповідей.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache? _redis = redis is MemoryDistributedCache ? null : redis;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        if (_redis is null || !circuit.IsAvailable)
            return null;

        var started = Stopwatch.GetTimestamp();
        try
        {
            var payload = await _redis.GetAsync(key, ct);
            circuit.RecordSuccess();

            if (payload is null)
            {
                metrics.Miss(CacheLevel.L2);
                logger.LogDebug("Aggregated response cache miss {CacheKey}", key);
                return null;
            }

            var value = JsonSerializer.Deserialize<T>(payload, Json);
            metrics.Hit(CacheLevel.L2);
            logger.LogInformation("Aggregated response {CacheKey} served from {CacheLevel} ({SizeBytes} B), no gRPC calls",
                key, CacheLevel.L2, payload.Length);
            return value;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Aggregated response {CacheKey} cannot be deserialized; treating as a miss", key);
            return null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            circuit.RecordFailure(ex);
            return null;
        }
        finally
        {
            metrics.RecordDuration(CacheLevel.L2, "get", Stopwatch.GetElapsedTime(started));
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct) where T : class
    {
        if (_redis is null || !circuit.IsAvailable)
            return;

        var started = Stopwatch.GetTimestamp();
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(value, Json);
            await _redis.SetAsync(key, payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl }, ct);
            circuit.RecordSuccess();
            logger.LogDebug("Aggregated response {CacheKey} cached at {CacheLevel} for {TtlSeconds} s ({SizeBytes} B)",
                key, CacheLevel.L2, (int)Ttl.TotalSeconds, payload.Length);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            circuit.RecordFailure(ex);
        }
        finally
        {
            metrics.RecordDuration(CacheLevel.L2, "set", Stopwatch.GetElapsedTime(started));
        }
    }
}
