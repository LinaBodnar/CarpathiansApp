using Carpathians.Platform.ServiceDefaults.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Catalog.Api.Caching;

/// <summary>
/// Час життя даних Каталогу в кеші залежно від того, як часто їх читають і змінюють.
/// Дані Каталогу — довідникові (Лаб. 03): читаються постійно, змінюються рідко, без персональних даних.
/// L1 (пам'ять) завжди коротший за L2 (Redis): зміна, видима іншим екземплярам у Redis,
/// не може «застрягти» в пам'яті одного екземпляра довше за L1 TTL.
/// </summary>
public static class CatalogCachePolicies
{
    /// <summary>
    /// Картка маршруту — найчастіший запит (сайт, Aggregator). Sliding 1 хв тримає «гарячі» маршрути
    /// в пам'яті, absolute 2 хв обмежує вік запису в L1; у Redis — 30 хв. Зміни маршруту інвалідують запис одразу.
    /// </summary>
    public static readonly CachePolicy RouteCard = new()
    {
        LocalTtl = TimeSpan.FromMinutes(2),
        LocalSlidingTtl = TimeSpan.FromMinutes(1),
        DistributedTtl = TimeSpan.FromMinutes(30),
        Priority = CacheItemPriority.High
    };

    /// <summary>
    /// Списки й сторінки маршрутів: залежать від будь-якої зміни маршрутів, тому живуть коротко
    /// й при витісненні йдуть першими.
    /// </summary>
    public static readonly CachePolicy RouteLists = new()
    {
        LocalTtl = TimeSpan.FromMinutes(1),
        DistributedTtl = TimeSpan.FromMinutes(5),
        Priority = CacheItemPriority.Low
    };

    /// <summary>Довідник регіонів змінюється дуже рідко — лише absolute expiration.</summary>
    public static readonly CachePolicy Regions = new()
    {
        LocalTtl = TimeSpan.FromMinutes(5),
        DistributedTtl = TimeSpan.FromHours(2),
        Priority = CacheItemPriority.High
    };

    /// <summary>
    /// Ключ-версія списків: спільна для всіх екземплярів (Redis), у пам'яті — лише 15 с,
    /// щоб нова версія після зміни швидко ставала видимою кожному екземпляру.
    /// </summary>
    public static readonly CachePolicy ListVersion = new()
    {
        LocalTtl = TimeSpan.FromSeconds(15),
        DistributedTtl = TimeSpan.FromDays(1),
        Priority = CacheItemPriority.High
    };
}
