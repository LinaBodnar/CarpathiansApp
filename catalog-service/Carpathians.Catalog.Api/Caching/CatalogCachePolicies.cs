using Carpathians.Platform.ServiceDefaults.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Catalog.Api.Caching;

/// <summary>
/// Час життя даних Каталогу в кеші залежно від того, як часто їх читають і змінюють.
/// Дані Каталогу — довідникові (Лаб. 03): читаються постійно, змінюються рідко, без персональних даних.
/// </summary>
public static class CatalogCachePolicies
{
    /// <summary>
    /// Картка маршруту — найчастіший запит (сайт, Aggregator). Sliding 2 хв тримає «гарячі» маршрути
    /// в пам'яті, absolute 10 хв обмежує вік запису. Зміни маршруту інвалідують запис одразу.
    /// </summary>
    public static readonly CachePolicy RouteCard = new()
    {
        LocalTtl = TimeSpan.FromMinutes(10),
        LocalSlidingTtl = TimeSpan.FromMinutes(2),
        Priority = CacheItemPriority.High
    };

    /// <summary>
    /// Списки й сторінки маршрутів: залежать від будь-якої зміни маршрутів, тому живуть коротко
    /// й при витісненні йдуть першими.
    /// </summary>
    public static readonly CachePolicy RouteLists = new()
    {
        LocalTtl = TimeSpan.FromMinutes(2),
        Priority = CacheItemPriority.Low
    };

    /// <summary>Довідник регіонів змінюється дуже рідко — лише absolute expiration.</summary>
    public static readonly CachePolicy Regions = new()
    {
        LocalTtl = TimeSpan.FromMinutes(30),
        Priority = CacheItemPriority.High
    };

    /// <summary>Ключ-версія списків: короткий TTL у пам'яті, щоб нова версія швидко ставала видимою.</summary>
    public static readonly CachePolicy ListVersion = new()
    {
        LocalTtl = TimeSpan.FromSeconds(30),
        Priority = CacheItemPriority.High
    };
}
