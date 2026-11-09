using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Дворівневий кеш за схемою cache-aside з запасним шляхом: L1 (IMemoryCache) -> L2 (Redis) -> база даних.
/// Кожен рівень заповнюється на зворотному шляху: значення з Redis кладеться в пам'ять,
/// значення з бази — і в Redis, і в пам'ять.
/// <list type="bullet">
/// <item>L1 — пам'ять процесу: найшвидший, з SizeLimit (Size запису = КБ JSON) і витісненням за пріоритетами.</item>
/// <item>L2 — Redis через IDistributedCache: спільний для всіх екземплярів, DTO серіалізуються System.Text.Json,
/// великі значення (понад 16 КБ) стискаються GZip.</item>
/// <item>Недоступність Redis не валить сервіс: запобіжник (<see cref="DistributedCacheCircuit"/>)
/// на 30 с переводить кеш у режим L1 + база.</item>
/// </list>
/// Без Redis (немає ConnectionStrings:redis) працює лише L1.
/// </summary>
public sealed class CacheService : ICacheService
{
    /// <summary>Значення, більші за цей розмір, стискаються перед записом у Redis.</summary>
    private const int CompressionThresholdBytes = 16 * 1024;

    private const byte FormatJson = 0;
    private const byte FormatGzipJson = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache? _distributed;
    private readonly DistributedCacheCircuit _circuit;
    private readonly CacheMetrics _metrics;
    private readonly ILogger<CacheService> _logger;
    private readonly KeyedLock _locks = new();

    public CacheService(
        IMemoryCache memory,
        CacheMetrics metrics,
        DistributedCacheCircuit circuit,
        ILogger<CacheService> logger,
        IDistributedCache? distributed = null)
    {
        _memory = memory;
        _metrics = metrics;
        _circuit = circuit;
        _logger = logger;
        // MemoryDistributedCache — це теж пам'ять процесу, а не спільний L2.
        _distributed = distributed is MemoryDistributedCache ? null : distributed;
    }

    /// <summary>Чи підключено Redis (L2).</summary>
    public bool HasDistributedLevel => _distributed is not null;

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CachePolicy policy,
        CancellationToken ct = default) where T : class
    {
        policy.Validate();

        // L1
        if (TryGetLocal(key, out T? cached))
            return cached;

        // Лише один запит на ключ іде далі (Redis/база); паралельні запити того самого ключа чекають тут
        // і після звільнення блокування беруть уже заповнений кеш (захист від cache stampede).
        using (await _locks.AcquireAsync(key, ct))
        {
            if (TryGetLocal(key, out cached))
                return cached;

            _metrics.Miss(CacheLevel.L1);

            // L2
            var useDistributed = _distributed is not null && policy.DistributedTtl is not null;
            if (useDistributed)
            {
                var (reachable, payload) = await TryGetDistributedAsync(key, ct);
                if (payload is not null && TryDeserialize(key, payload, out T? fromRedis))
                {
                    _metrics.Hit(CacheLevel.L2);
                    CacheCallContext.Record(CacheLevel.L2);
                    _logger.LogDebug("Cache hit {CacheKey} at {CacheLevel}, size {SizeBytes} B",
                        key, CacheLevel.L2, payload.Length);

                    SetLocal(key, fromRedis!, policy, payload.Length);           // заповнення L1 на зворотному шляху
                    return fromRedis;
                }

                if (reachable)                                                    // недоступний Redis — не промах, а пропуск рівня
                    _metrics.Miss(CacheLevel.L2);
            }

            // База даних
            _logger.LogDebug("Cache miss {CacheKey}, loading from source", key);
            var value = await LoadFromSourceAsync(key, factory, ct);
            if (value is null)
                return null;

            var json = JsonSerializer.SerializeToUtf8Bytes(value, Json);
            if (useDistributed)
                await TrySetDistributedAsync(key, json, policy.DistributedTtl!.Value, ct);
            SetLocal(key, value, policy, json.Length);

            return value;
        }
    }

    public async Task InvalidateAsync(IReadOnlyCollection<string> keys, string reason, CancellationToken ct = default)
    {
        foreach (var key in keys)
            _memory.Remove(key);

        if (_distributed is not null)
        {
            foreach (var key in keys)
                await TryRemoveDistributedAsync(key, ct);
        }

        _metrics.Invalidated(keys.Count, reason);
        _logger.LogInformation("Cache invalidated {CacheKeys}, reason {Reason}", keys, reason);
    }

    // ---------- L1: IMemoryCache ----------

    private bool TryGetLocal<T>(string key, out T? value) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        var found = _memory.TryGetValue(key, out LocalEntry? entry) && entry?.Value is T;
        _metrics.RecordDuration(CacheLevel.L1, "get", Stopwatch.GetElapsedTime(started));

        if (!found)
        {
            value = null;
            return false;
        }

        _metrics.Hit(CacheLevel.L1);
        CacheCallContext.Record(CacheLevel.L1);
        _logger.LogDebug("Cache hit {CacheKey} at {CacheLevel}: expires in {ExpiresInSeconds} s, size {SizeBytes} B",
            key, CacheLevel.L1, (int)(entry!.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds, entry.SizeBytes);

        value = (T)entry.Value;
        return true;
    }

    private void SetLocal(string key, object value, CachePolicy policy, int sizeBytes)
    {
        var entry = new LocalEntry(value, sizeBytes, DateTimeOffset.UtcNow + policy.LocalTtl);

        var options = new MemoryCacheEntryOptions
        {
            Size = Math.Max(1, (sizeBytes + 1023) / 1024),                   // одиниця Size = 1 КБ
            AbsoluteExpirationRelativeToNow = policy.LocalTtl,
            SlidingExpiration = policy.LocalSlidingTtl,
            Priority = policy.Priority
        };
        options.RegisterPostEvictionCallback(OnLocalEviction, this);

        _memory.Set(key, entry, options);
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

    // ---------- L2: Redis (IDistributedCache) ----------

    /// <summary>Reachable = false — Redis недоступний (запобіжник відкритий або помилка звернення).</summary>
    private async Task<(bool Reachable, byte[]? Payload)> TryGetDistributedAsync(string key, CancellationToken ct)
    {
        if (!_circuit.IsAvailable)
            return (false, null);

        var started = Stopwatch.GetTimestamp();
        try
        {
            var payload = await _distributed!.GetAsync(key, ct);
            _circuit.RecordSuccess();
            return (true, payload);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _circuit.RecordFailure(ex);
            return (false, null);
        }
        finally
        {
            _metrics.RecordDuration(CacheLevel.L2, "get", Stopwatch.GetElapsedTime(started));
        }
    }

    private async Task TrySetDistributedAsync(string key, byte[] json, TimeSpan ttl, CancellationToken ct)
    {
        if (!_circuit.IsAvailable)
            return;

        var payload = Encode(json);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await _distributed!.SetAsync(key, payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
            _circuit.RecordSuccess();
            _logger.LogDebug("Cache set {CacheKey} at {CacheLevel}: TTL {TtlSeconds} s, size {SizeBytes} B (stored {StoredBytes} B)",
                key, CacheLevel.L2, (int)ttl.TotalSeconds, json.Length, payload.Length);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _circuit.RecordFailure(ex);
        }
        finally
        {
            _metrics.RecordDuration(CacheLevel.L2, "set", Stopwatch.GetElapsedTime(started));
        }
    }

    private async Task TryRemoveDistributedAsync(string key, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            // Видалення пробуємо навіть при відкритому запобіжнику: застарілий запис у Redis гірший за зайву спробу.
            await _distributed!.RemoveAsync(key, ct);
            _circuit.RecordSuccess();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _circuit.RecordFailure(ex);
            _logger.LogWarning("Cache key {CacheKey} was not removed from {CacheLevel}: Redis is unavailable; the entry expires by TTL",
                key, CacheLevel.L2);
        }
        finally
        {
            _metrics.RecordDuration(CacheLevel.L2, "remove", Stopwatch.GetElapsedTime(started));
        }
    }

    // ---------- Джерело даних і серіалізація ----------

    private async Task<T?> LoadFromSourceAsync<T>(
        string key, Func<CancellationToken, Task<T?>> factory, CancellationToken ct) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        var value = await factory(ct);
        var elapsed = Stopwatch.GetElapsedTime(started);

        CacheCallContext.Record(CacheLevel.Source);
        _metrics.RecordDuration(CacheLevel.Source, "load", elapsed);
        _logger.LogDebug("Loaded {CacheKey} from source in {ElapsedMs} ms", key, (long)elapsed.TotalMilliseconds);
        return value;
    }

    /// <summary>Перший байт — формат (0 — JSON, 1 — GZip JSON), далі дані.</summary>
    private static byte[] Encode(byte[] json)
    {
        if (json.Length <= CompressionThresholdBytes)
        {
            var raw = new byte[json.Length + 1];
            raw[0] = FormatJson;
            json.CopyTo(raw, 1);
            return raw;
        }

        using var output = new MemoryStream();
        output.WriteByte(FormatGzipJson);
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(json);
        return output.ToArray();
    }

    private bool TryDeserialize<T>(string key, byte[] payload, out T? value) where T : class
    {
        try
        {
            if (payload.Length == 0)
            {
                value = null;
                return false;
            }

            if (payload[0] == FormatGzipJson)
            {
                using var gzip = new GZipStream(new MemoryStream(payload, 1, payload.Length - 1), CompressionMode.Decompress);
                value = JsonSerializer.Deserialize<T>(gzip, Json);
            }
            else
            {
                value = JsonSerializer.Deserialize<T>(payload.AsSpan(1), Json);
            }

            return value is not null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            // Формат DTO змінився після оновлення сервісу — запис вважається промахом і буде перезаписаний.
            _logger.LogWarning(ex, "Cache entry {CacheKey} at {CacheLevel} cannot be deserialized; treating as a miss",
                key, CacheLevel.L2);
            value = null;
            return false;
        }
    }

    /// <summary>Запис L1: значення + розмір і час закінчення (для логування попадань).</summary>
    private sealed record LocalEntry(object Value, int SizeBytes, DateTimeOffset ExpiresAt);
}
