using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Services;
using Carpathians.Platform.ServiceDefaults.Caching;

namespace Carpathians.Catalog.Api.Caching;

/// <summary>
/// Декоратор <see cref="IRouteService"/> з кешуванням (cache-aside). Бізнес-логіка Лаб. 03 (RouteService)
/// не змінюється; REST-контролер і gRPC-сервіс працюють через IRouteService, тому кеш діє для обох протоколів.
/// <para>Читання: кеш -> у разі промаху RouteService (EF Core) -> запис у кеш.
/// Запис: після SaveChanges видаляються картка маршруту й версія списків — синхронно, до відповіді клієнту.</para>
/// <para><see cref="GetWithDetailsExplicitAsync"/> навмисно не кешується: ендпоінт демонструє explicit loading (Лаб. 03).</para>
/// </summary>
public sealed class CachedRouteService(RouteService inner, ICacheService cache) : IRouteService
{
    // ---------- Читання ----------

    public async Task<RouteDto> GetByIdAsync(long id, CancellationToken ct = default)
        => (await cache.GetOrCreateAsync(
            CatalogCacheKeys.Route(id),
            async token => await inner.GetByIdAsync(id, token),
            CatalogCachePolicies.RouteCard, ct))!;

    public async Task<IReadOnlyList<RouteDto>> GetAllAsync(CancellationToken ct = default)
    {
        var version = await ListsVersionAsync(ct);
        return (await cache.GetOrCreateAsync(
            CatalogCacheKeys.RoutesAll(version),
            async token => await inner.GetAllAsync(token),
            CatalogCachePolicies.RouteLists, ct))!;
    }

    public async Task<PagedResult<RouteDto>> GetPagedAsync(RouteQueryParameters queryParams, CancellationToken ct = default)
    {
        var version = await ListsVersionAsync(ct);
        return (await cache.GetOrCreateAsync(
            CatalogCacheKeys.RoutesPage(version, queryParams),
            async token => await inner.GetPagedAsync(queryParams, token),
            CatalogCachePolicies.RouteLists, ct))!;
    }

    public async Task<IReadOnlyList<RouteDto>> GetPopularChallengingAsync(CancellationToken ct = default)
    {
        var version = await ListsVersionAsync(ct);
        return (await cache.GetOrCreateAsync(
            CatalogCacheKeys.RoutesPopular(version),
            async token => await inner.GetPopularChallengingAsync(token),
            CatalogCachePolicies.RouteLists, ct))!;
    }

    public Task<RouteDto> GetWithDetailsExplicitAsync(long id, CancellationToken ct = default)
        => inner.GetWithDetailsExplicitAsync(id, ct);

    // ---------- Запис: зміна даних -> інвалідація ----------

    public async Task<RouteDto> CreateAsync(CreateRouteDto input, CancellationToken ct = default)
    {
        var created = await inner.CreateAsync(input, ct);
        await cache.InvalidateAsync("RouteCreated", ct, CatalogCacheKeys.RoutesVersion);
        return created;
    }

    public async Task<RouteDto> UpdateAsync(long id, UpdateRouteDto input, CancellationToken ct = default)
    {
        var updated = await inner.UpdateAsync(id, input, ct);
        await InvalidateRouteAsync(id, "RouteUpdated", ct);
        return updated;
    }

    public async Task<RouteDto> SetDetailAsync(long id, SetRouteDetailDto input, CancellationToken ct = default)
    {
        var updated = await inner.SetDetailAsync(id, input, ct);
        await InvalidateRouteAsync(id, "RouteDetailChanged", ct);
        return updated;
    }

    public async Task<RouteDto> AddGearItemAsync(long id, AddGearItemDto input, CancellationToken ct = default)
    {
        var updated = await inner.AddGearItemAsync(id, input, ct);
        await InvalidateRouteAsync(id, "RouteGearChanged", ct);
        return updated;
    }

    public async Task<RouteDto> RemoveGearItemAsync(long id, long gearItemId, CancellationToken ct = default)
    {
        var updated = await inner.RemoveGearItemAsync(id, gearItemId, ct);
        await InvalidateRouteAsync(id, "RouteGearChanged", ct);
        return updated;
    }

    public async Task<RouteDto> AddPointAsync(long id, AddRoutePointDto input, CancellationToken ct = default)
    {
        // RouteService після зміни читає маршрут через GetByIdAsync — уже без кешу (inner).
        var updated = await inner.AddPointAsync(id, input, ct);
        await InvalidateRouteAsync(id, "RoutePointsChanged", ct);
        return updated;
    }

    public async Task<RouteDto> RemovePointAsync(long id, long pointId, CancellationToken ct = default)
    {
        var updated = await inner.RemovePointAsync(id, pointId, ct);
        await InvalidateRouteAsync(id, "RoutePointsChanged", ct);
        return updated;
    }

    public async Task DeleteAsync(long id, string? deletedBy = null, CancellationToken ct = default)
    {
        await inner.DeleteAsync(id, deletedBy, ct);
        await InvalidateRouteAsync(id, "RouteDeleted", ct);
    }

    // ---------- Допоміжне ----------

    private Task<string> ListsVersionAsync(CancellationToken ct)
        => cache.GetVersionAsync(CatalogCacheKeys.RoutesVersion, CatalogCachePolicies.ListVersion, ct);

    /// <summary>Картка маршруту + усі списки (маршрут може входити в будь-яку сторінку).</summary>
    private Task InvalidateRouteAsync(long id, string reason, CancellationToken ct)
        => cache.InvalidateAsync(reason, ct, CatalogCacheKeys.Route(id), CatalogCacheKeys.RoutesVersion);
}
