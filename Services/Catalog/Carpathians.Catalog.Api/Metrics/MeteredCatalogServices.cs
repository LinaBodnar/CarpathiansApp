using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Domain.Enums;
using Ops = Carpathians.Catalog.Api.Metrics.CatalogMetrics.Operations;

namespace Carpathians.Catalog.Api.Metrics;

/// <summary>
/// Декоратор маршрутів з метриками: тривалість кожної операції (разом із кешем і EF Core) і бізнес-лічильник
/// одразу після успіху. REST-контролер і gRPC-сервіс працюють через <see cref="IRouteService"/>, тож метрики
/// охоплюють обидва протоколи. Ланцюжок: метрики -> кеш -> RouteService.
/// </summary>
public sealed class MeteredRouteService(IRouteService inner, CatalogMetrics metrics) : IRouteService
{
    public async Task<IReadOnlyList<RouteDto>> GetAllAsync(CancellationToken ct = default)
    {
        var result = await metrics.MeasureAsync(Ops.RouteSearch, () => inner.GetAllAsync(ct));
        metrics.RoutesSearched(CatalogMetrics.SearchTypes.All);
        return result;
    }

    public async Task<PagedResult<RouteDto>> GetPagedAsync(RouteQueryParameters queryParams, CancellationToken ct = default)
    {
        var result = await metrics.MeasureAsync(Ops.RouteSearch, () => inner.GetPagedAsync(queryParams, ct));
        metrics.RoutesSearched(CatalogMetrics.SearchTypes.Paged);
        return result;
    }

    public async Task<IReadOnlyList<RouteDto>> GetPopularChallengingAsync(CancellationToken ct = default)
    {
        var result = await metrics.MeasureAsync(Ops.RouteSearch, () => inner.GetPopularChallengingAsync(ct));
        metrics.RoutesSearched(CatalogMetrics.SearchTypes.PopularChallenging);
        return result;
    }

    public Task<RouteDto> GetByIdAsync(long id, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.RouteGet, () => inner.GetByIdAsync(id, ct));

    public Task<RouteDto> GetWithDetailsExplicitAsync(long id, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.RouteGet, () => inner.GetWithDetailsExplicitAsync(id, ct));

    public async Task<RouteDto> CreateAsync(CreateRouteDto input, CancellationToken ct = default)
    {
        var created = await metrics.MeasureAsync(Ops.RouteCreate, () => inner.CreateAsync(input, ct));
        metrics.RouteCreated(created.Difficulty);
        return created;
    }

    public Task<RouteDto> UpdateAsync(long id, UpdateRouteDto input, CancellationToken ct = default)
        => Updated(() => inner.UpdateAsync(id, input, ct));

    public Task<RouteDto> SetDetailAsync(long id, SetRouteDetailDto input, CancellationToken ct = default)
        => Updated(() => inner.SetDetailAsync(id, input, ct));

    public Task<RouteDto> AddGearItemAsync(long id, AddGearItemDto input, CancellationToken ct = default)
        => Updated(() => inner.AddGearItemAsync(id, input, ct));

    public Task<RouteDto> RemoveGearItemAsync(long id, long gearItemId, CancellationToken ct = default)
        => Updated(() => inner.RemoveGearItemAsync(id, gearItemId, ct));

    public Task<RouteDto> AddPointAsync(long id, AddRoutePointDto input, CancellationToken ct = default)
        => Updated(() => inner.AddPointAsync(id, input, ct));

    public Task<RouteDto> RemovePointAsync(long id, long pointId, CancellationToken ct = default)
        => Updated(() => inner.RemovePointAsync(id, pointId, ct));

    public async Task DeleteAsync(long id, string? deletedBy = null, CancellationToken ct = default)
    {
        await metrics.MeasureAsync(Ops.RouteDelete, () => inner.DeleteAsync(id, deletedBy, ct));
        metrics.RouteDeleted();
    }

    public Task<RouteDto> PublishAsync(long id, string? publishedBy = null, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.RoutePublish, () => inner.PublishAsync(id, publishedBy, ct));

    /// <summary>Будь-яка зміна маршруту (поля, деталі, спорядження, точки) — одна операція route.update.</summary>
    private async Task<RouteDto> Updated(Func<Task<RouteDto>> update)
    {
        var updated = await metrics.MeasureAsync(Ops.RouteUpdate, update);
        metrics.RouteUpdated(updated.Difficulty);
        return updated;
    }
}

/// <summary>Декоратор точок мапи з метриками (ланцюжок: метрики -> кеш -> PointService).</summary>
public sealed class MeteredPointService(IPointService inner, CatalogMetrics metrics) : IPointService
{
    public Task<IReadOnlyList<MapPointDto>> GetAllAsync(CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.PointGet, () => inner.GetAllAsync(ct));

    public Task<MapPointDto> GetByIdAsync(long id, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.PointGet, () => inner.GetByIdAsync(id, ct));

    public Task<IReadOnlyList<MapPointDto>> GetByTypeAsync(PointType pointType, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.PointGet, () => inner.GetByTypeAsync(pointType, ct));

    public async Task<MapPointDto> CreateAsync(CreatePointDto input, CancellationToken ct = default)
    {
        var created = await metrics.MeasureAsync(Ops.PointCreate, () => inner.CreateAsync(input, ct));
        metrics.PointCreated(created.PointType);
        return created;
    }

    public Task<MapPointDto> UpdateAsync(long id, UpdatePointDto input, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.PointUpdate, () => inner.UpdateAsync(id, input, ct));

    public Task<MapPointDto> AddImageAsync(long id, AddGalleryImageDto input, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.PointUpdate, () => inner.AddImageAsync(id, input, ct));

    public Task<MapPointDto> RemoveImageAsync(long id, long imageId, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.PointUpdate, () => inner.RemoveImageAsync(id, imageId, ct));

    public Task DeleteAsync(long id, CancellationToken ct = default)
        => metrics.MeasureAsync(Ops.PointDelete, () => inner.DeleteAsync(id, ct));
}
