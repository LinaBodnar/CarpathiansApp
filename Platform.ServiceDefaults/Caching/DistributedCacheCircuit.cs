using Microsoft.Extensions.Logging;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Запобіжник для Redis (L2). Після помилки звернення до Redis на <see cref="BreakDuration"/>
/// припиняються: сервіс працює далі на L1 + базі даних (повільніше), а не чекає таймаутів
/// на кожному запиті й не падає. Після паузи наступна операція перевіряє, чи Redis повернувся.
/// </summary>
public sealed class DistributedCacheCircuit(ILogger<DistributedCacheCircuit> logger, TimeProvider? timeProvider = null)
{
    public static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private long _openUntilTicks;          // 0 — запобіжник закритий (Redis доступний)

    /// <summary>Чи можна зараз звертатися до Redis.</summary>
    public bool IsAvailable => _time.GetUtcNow().UtcTicks >= Interlocked.Read(ref _openUntilTicks);

    /// <summary>Чи був Redis недоступний під час останньої перевірки.</summary>
    public bool IsBroken => Interlocked.Read(ref _openUntilTicks) != 0;

    public void RecordFailure(Exception ex)
    {
        var openUntil = (_time.GetUtcNow() + BreakDuration).UtcTicks;
        if (Interlocked.Exchange(ref _openUntilTicks, openUntil) == 0)
            logger.LogWarning(ex, "Distributed cache (Redis, L2) is unavailable; serving from L1 and the database for {BreakSeconds} s",
                (int)BreakDuration.TotalSeconds);
    }

    public void RecordSuccess()
    {
        if (Interlocked.Exchange(ref _openUntilTicks, 0) != 0)
            logger.LogInformation("Distributed cache (Redis, L2) is available again");
    }
}
