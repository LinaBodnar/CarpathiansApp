using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Services;
using Carpathians.Dal.Interfaces;
using Carpathians.Platform.ServiceDefaults.Caching;

namespace Carpathians.Catalog.Api.Caching;

/// <summary>
/// Декоратор <see cref="IRegionService"/> з кешуванням довідника регіонів (absolute expiration).
/// Назва регіону входить у картки маршрутів (RouteDto.RegionName), тому її зміна інвалідує
/// й картки маршрутів цього регіону.
/// </summary>
public sealed class CachedRegionService(RegionService inner, ICacheService cache, IUnitOfWork uow) : IRegionService
{
    /// <summary>Верхня межа кількості маршрутів регіону при пошуку карток для інвалідації.</summary>
    private const int MaxRoutesPerRegion = 1000;

    public async Task<IReadOnlyList<RegionDto>> GetAllAsync(CancellationToken ct = default)
        => (await cache.GetOrCreateAsync(
            CatalogCacheKeys.RegionsAll,
            async token => await inner.GetAllAsync(token),
            CatalogCachePolicies.Regions, ct))!;

    public async Task<RegionDto> GetByIdAsync(int id, CancellationToken ct = default)
        => (await cache.GetOrCreateAsync(
            CatalogCacheKeys.Region(id),
            async token => await inner.GetByIdAsync(id, token),
            CatalogCachePolicies.Regions, ct))!;

    public async Task<RegionDto> CreateAsync(CreateRegionDto input, CancellationToken ct = default)
    {
        var created = await inner.CreateAsync(input, ct);
        await cache.InvalidateAsync("RegionCreated", ct, CatalogCacheKeys.RegionsAll);
        return created;
    }

    public async Task<RegionDto> UpdateAsync(int id, UpdateRegionDto input, CancellationToken ct = default)
    {
        var updated = await inner.UpdateAsync(id, input, ct);

        var (routes, _) = await uow.Routes.GetPagedAsync(1, MaxRoutesPerRegion, regionId: id, ct: ct);
        var keys = new List<string> { CatalogCacheKeys.RegionsAll, CatalogCacheKeys.Region(id), CatalogCacheKeys.RoutesVersion };
        keys.AddRange(routes.Select(r => CatalogCacheKeys.Route(r.Id)));

        await cache.InvalidateAsync(keys, "RegionUpdated", ct);
        return updated;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        // Регіон з маршрутами видалити не можна (ConflictException у RegionService) — картки не зачіпаються.
        await inner.DeleteAsync(id, ct);
        await cache.InvalidateAsync("RegionDeleted", ct, CatalogCacheKeys.RegionsAll, CatalogCacheKeys.Region(id));
    }
}
