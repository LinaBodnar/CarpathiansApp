using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Services;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Enums;
using Carpathians.Platform.ServiceDefaults.Caching;

namespace Carpathians.Catalog.Api.Caching;

/// <summary>
/// Декоратор <see cref="IPointService"/>: точки (вершини, притулки) самі не кешуються, але входять
/// у картки маршрутів. Зміна точки інвалідує картки всіх маршрутів, що через неї проходять
/// (M:N через RoutePoint, Лаб. 03).
/// </summary>
public sealed class CachedPointService(PointService inner, ICacheService cache, IUnitOfWork uow) : IPointService
{
    public Task<IReadOnlyList<MapPointDto>> GetAllAsync(CancellationToken ct = default) => inner.GetAllAsync(ct);

    public Task<MapPointDto> GetByIdAsync(long id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<MapPointDto>> GetByTypeAsync(PointType pointType, CancellationToken ct = default)
        => inner.GetByTypeAsync(pointType, ct);

    public Task<MapPointDto> CreateAsync(CreatePointDto input, CancellationToken ct = default)
        => inner.CreateAsync(input, ct);                     // нова точка ще не входить у жоден маршрут

    public async Task<MapPointDto> UpdateAsync(long id, UpdatePointDto input, CancellationToken ct = default)
    {
        var affected = await AffectedRouteKeysAsync(id, ct);
        var updated = await inner.UpdateAsync(id, input, ct);
        await InvalidateAsync(affected, "PointUpdated", ct);
        return updated;
    }

    public async Task<MapPointDto> AddImageAsync(long id, AddGalleryImageDto input, CancellationToken ct = default)
    {
        var affected = await AffectedRouteKeysAsync(id, ct);
        var updated = await inner.AddImageAsync(id, input, ct);
        await InvalidateAsync(affected, "PointGalleryChanged", ct);
        return updated;
    }

    public async Task<MapPointDto> RemoveImageAsync(long id, long imageId, CancellationToken ct = default)
    {
        var affected = await AffectedRouteKeysAsync(id, ct);
        var updated = await inner.RemoveImageAsync(id, imageId, ct);
        await InvalidateAsync(affected, "PointGalleryChanged", ct);
        return updated;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        // Маршрути шукаємо ДО видалення: після soft delete зв'язки з точкою вже не повертаються.
        var affected = await AffectedRouteKeysAsync(id, ct);
        await inner.DeleteAsync(id, ct);
        await InvalidateAsync(affected, "PointDeleted", ct);
    }

    /// <summary>Картки маршрутів, що проходять через точку, + версія списків маршрутів.</summary>
    private async Task<List<string>> AffectedRouteKeysAsync(long pointId, CancellationToken ct)
    {
        var routes = await uow.RoutePoints.GetRoutesPassingThroughPointAsync(pointId, ct);
        var keys = routes.Select(r => CatalogCacheKeys.Route(r.Id)).ToList();
        if (keys.Count > 0)
            keys.Add(CatalogCacheKeys.RoutesVersion);
        return keys;
    }

    private Task InvalidateAsync(List<string> keys, string reason, CancellationToken ct)
        => keys.Count == 0 ? Task.CompletedTask : cache.InvalidateAsync(keys, reason, ct);
}
