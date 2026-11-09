using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Cache-aside поверх <see cref="IMemoryCache"/> (рівень L1 — пам'ять процесу).
/// <para>Кожен запис має Size (у КБ серіалізованого JSON), тож кеш не перевищить SizeLimit:
/// при заповненні IMemoryCache витісняє записи (спершу з нижчим пріоритетом).
/// Попадання, промахи, витіснення й інвалідації логуються структуровано й рахуються в метриках.</para>
/// <para>Кеш у пам'яті належить одному екземпляру сервісу й не спільний між репліками.</para>
/// </summary>
public sealed class CacheService(
    IMemoryCache memory,
    CacheMetrics metrics,
    ILogger<CacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CacheMetrics _metrics = metrics;
    private readonly ILogger<CacheService> _logger = logger;
    private readonly KeyedLock _locks = new();

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CachePolicy policy,
        CancellationToken ct = default) where T : class
    {
        if (TryGetLocal(key, out T? cached))
            return cached;

        // Лише один запит на ключ іде до бази; паралельні запити того самого ключа чекають тут
        // і після звільнення блокування беруть уже заповнений кеш.
        using (await _locks.AcquireAsync(key, ct))
        {
            if (TryGetLocal(key, out cached))
                return cached;

            _metrics.Miss(CacheLevel.L1);
            _logger.LogDebug("Cache miss {CacheKey} at {CacheLevel}, loading from source", key, CacheLevel.L1);

            var value = await LoadFromSourceAsync(key, factory, ct);
            if (value is not null)
                SetLocal(key, value, policy);

            return value;
        }
    }

    public Task InvalidateAsync(IReadOnlyCollection<string> keys, string reason, CancellationToken ct = default)
    {
        foreach (var key in keys)
            memory.Remove(key);

        _metrics.Invalidated(keys.Count, reason);
        _logger.LogInformation("Cache invalidated {CacheKeys}, reason {Reason}", keys, reason);
        return Task.CompletedTask;
    }

    private bool TryGetLocal<T>(string key, out T? value) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        var found = memory.TryGetValue(key, out LocalEntry? entry) && entry?.Value is T;
        _metrics.RecordDuration(CacheLevel.L1, "get", Stopwatch.GetElapsedTime(started));

        if (!found)
        {
            value = null;
            return false;
        }

        _metrics.Hit(CacheLevel.L1);
        _logger.LogDebug("Cache hit {CacheKey} at {CacheLevel}: expires in {ExpiresInSeconds} s, size {SizeBytes} B",
            key, CacheLevel.L1, (int)(entry!.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, entry.SizeBytes);

        value = (T)entry.Value;
        return true;
    }

    private async Task<T?> LoadFromSourceAsync<T>(
        string key, Func<CancellationToken, Task<T?>> factory, CancellationToken ct) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        var value = await factory(ct);
        var elapsed = Stopwatch.GetElapsedTime(started);

        _metrics.RecordDuration(CacheLevel.Source, "load", elapsed);
        _logger.LogDebug("Loaded {CacheKey} from source in {ElapsedMs} ms", key, (long)elapsed.TotalMilliseconds);
        return value;
    }

    private void SetLocal<T>(string key, T value, CachePolicy policy) where T : class
    {
        policy.Validate();

        // Розмір запису — довжина JSON; одиниця Size = 1 КБ (SizeLimit задано в КБ).
        var sizeBytes = JsonSerializer.SerializeToUtf8Bytes(value, Json).Length;
        var entry = new LocalEntry(value, sizeBytes, DateTimeOffset.UtcNow + policy.LocalTtl);

        var options = new MemoryCacheEntryOptions
        {
            Size = Math.Max(1, (sizeBytes + 1023) / 1024),
            AbsoluteExpirationRelativeToNow = policy.LocalTtl,
            SlidingExpiration = policy.LocalSlidingTtl,
            Priority = policy.Priority
        };
        options.RegisterPostEvictionCallback(OnLocalEviction, this);

        memory.Set(key, entry, options);
        _logger.LogDebug("Cache set {CacheKey} at {CacheLevel}: TTL {TtlSeconds} s, sliding {SlidingSeconds} s, size {SizeBytes} B",
            key, CacheLevel.L1, (int)policy.LocalTtl.TotalSeconds, (int?)policy.LocalSlidingTtl?.TotalSeconds, sizeBytes);
    }

    private static void OnLocalEviction(object key, object? value, EvictionReason reason, object? state)
    {
        // Removed — інвалідація (вже врахована), Replaced — перезапис новим значенням.
        if (reason is EvictionReason.Removed or EvictionReason.Replaced || state is not CacheService self)
            return;

        self._metrics.Evicted(CacheLevel.L1, reason.ToString());
        self._logger.LogDebug("Cache entry {CacheKey} evicted from {CacheLevel}: {EvictionReason}",
            key, CacheLevel.L1, reason);
    }

    /// <summary>Запис L1: значення + розмір і час закінчення (для логування попадань).</summary>
    private sealed record LocalEntry(object Value, int SizeBytes, DateTimeOffset ExpiresAt);
}
