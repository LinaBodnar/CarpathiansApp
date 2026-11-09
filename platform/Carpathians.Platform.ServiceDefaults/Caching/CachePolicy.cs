using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Політика зберігання запису на обох рівнях кешу.
/// <para>L1 (IMemoryCache, пам'ять процесу) — найшвидший, але лише для одного екземпляра сервісу.
/// L2 (Redis) — спільний для всіх екземплярів, повільніший (мережевий виклик).</para>
/// <para>L1 живе коротше за L2: інакше застарілі дані в пам'яті одного екземпляра пережили б
/// оновлення, яке інші екземпляри вже бачать у Redis.</para>
/// <para>Absolute expiration — гарантована межа «свіжості» (довідники, що рідко змінюються).
/// Sliding expiration — запис живе, доки його часто читають; разом з absolute не дає «вічного» запису.</para>
/// </summary>
public sealed record CachePolicy
{
    /// <summary>Максимальний час життя запису в пам'яті (L1, absolute expiration).</summary>
    public required TimeSpan LocalTtl { get; init; }

    /// <summary>Sliding expiration у пам'яті: запис видаляється, якщо його не читали стільки часу.</summary>
    public TimeSpan? LocalSlidingTtl { get; init; }

    /// <summary>Час життя в Redis (L2). null — дані кешуються лише в пам'яті процесу.</summary>
    public TimeSpan? DistributedTtl { get; init; }

    /// <summary>Пріоритет при витісненні з пам'яті, коли кеш досяг SizeLimit (Low витісняється першим).</summary>
    public CacheItemPriority Priority { get; init; } = CacheItemPriority.Normal;

    /// <summary>Перевірка узгодженості TTL (викликається під час запису).</summary>
    public void Validate()
    {
        if (LocalTtl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(LocalTtl), "Local TTL must be positive");
        if (LocalSlidingTtl is { } sliding && (sliding <= TimeSpan.Zero || sliding > LocalTtl))
            throw new ArgumentOutOfRangeException(nameof(LocalSlidingTtl), "Sliding TTL must be positive and not longer than local TTL");
        if (DistributedTtl is { } distributed && distributed <= LocalTtl)
            throw new ArgumentOutOfRangeException(nameof(DistributedTtl), "L1 TTL must be shorter than L2 TTL");
    }
}
