using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Політика зберігання запису в кеші: скільки він живе в пам'яті процесу (L1).
/// <para>Absolute expiration — гарантована межа «свіжості» (довідники, що рідко змінюються).
/// Sliding expiration — запис живе, доки його часто читають (картки, які запитують постійно);
/// разом з absolute не дає «вічного» запису.</para>
/// </summary>
public sealed record CachePolicy
{
    /// <summary>Максимальний час життя запису в пам'яті (absolute expiration).</summary>
    public required TimeSpan LocalTtl { get; init; }

    /// <summary>Sliding expiration у пам'яті: запис видаляється, якщо його не читали стільки часу.</summary>
    public TimeSpan? LocalSlidingTtl { get; init; }

    /// <summary>Пріоритет при витісненні, коли кеш досяг SizeLimit (Low витісняється першим).</summary>
    public CacheItemPriority Priority { get; init; } = CacheItemPriority.Normal;

    /// <summary>Перевірка узгодженості TTL (викликається під час запису).</summary>
    public void Validate()
    {
        if (LocalTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(LocalTtl), "Local TTL must be positive");
        if (LocalSlidingTtl is { } sliding && (sliding <= TimeSpan.Zero || sliding > LocalTtl))
            throw new ArgumentOutOfRangeException(nameof(LocalSlidingTtl), "Sliding TTL must be positive and not longer than local TTL");
    }
}
