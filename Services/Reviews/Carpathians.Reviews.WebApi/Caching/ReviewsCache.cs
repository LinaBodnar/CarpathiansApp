using System.Globalization;
using Carpathians.Platform.ServiceDefaults.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Reviews.WebApi.Caching;

/// <summary>
/// Схема ключів кешу сервісу відгуків: "reviews:{сутність}:{id}". Списки відгуків маршруту
/// й топ маршрутів версіонуються: інвалідація ключа-версії скидає всі сторінки одразу.
/// </summary>
public static class ReviewsCacheKeys
{
    /// <summary>Зведений рейтинг маршруту ($group по reviews).</summary>
    public static string RouteRating(long routeId) => $"reviews:route:{routeId}:rating";

    /// <summary>Версія списків відгуків маршруту.</summary>
    public static string RouteVersion(long routeId) => $"reviews:route:{routeId}:version";

    public static string RouteReviews(long routeId, string version, string sortBy, string sortDir, int page, int pageSize)
        => string.Join(':', $"reviews:route:{routeId}:v{version}:list",
            sortBy.ToLowerInvariant(), sortDir.ToLowerInvariant(),
            page.ToString(CultureInfo.InvariantCulture), pageSize.ToString(CultureInfo.InvariantCulture));

    /// <summary>Версія рейтингу «найкращі маршрути» (залежить від будь-якого відгуку).</summary>
    public const string TopRatedVersion = "reviews:routes:top-rated:version";

    public static string TopRated(string version, int top, int minReviews)
        => $"reviews:routes:top-rated:v{version}:{top}:{minReviews}";
}

/// <summary>
/// TTL даних відгуків: нові відгуки з'являються частіше, ніж змінюється Каталог,
/// тому TTL коротші; кожна зміна відгуку інвалідує рейтинг і списки маршруту одразу.
/// </summary>
public static class ReviewsCachePolicies
{
    public static readonly CachePolicy RouteRating = new()
    {
        LocalTtl = TimeSpan.FromMinutes(1),
        DistributedTtl = TimeSpan.FromMinutes(10)
    };

    public static readonly CachePolicy RouteReviews = new()
    {
        LocalTtl = TimeSpan.FromSeconds(30),
        DistributedTtl = TimeSpan.FromMinutes(3),
        Priority = CacheItemPriority.Low
    };

    public static readonly CachePolicy TopRated = new()
    {
        LocalTtl = TimeSpan.FromMinutes(1),
        DistributedTtl = TimeSpan.FromMinutes(10)
    };

    public static readonly CachePolicy Version = new()
    {
        LocalTtl = TimeSpan.FromSeconds(10),
        DistributedTtl = TimeSpan.FromDays(1),
        Priority = CacheItemPriority.High
    };
}
