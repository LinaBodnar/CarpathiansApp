using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Dal.Repositories;

public class RouteRepository(CatalogDbContext context)
    : GenericRepository<TrailRoute>(context), IRouteRepository
{
    /// <summary>
    /// Eager loading: ОДИН SQL-запит із Include/ThenInclude.
    /// Завантажує маршрут разом з регіоном, деталями, спорядженням та точками ланцюжка.
    /// </summary>
    public async Task<TrailRoute?> GetWithDetailsEagerAsync(long id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(r => r.Region)
            .Include(r => r.Detail)
            .Include(r => r.GearItems)
            .Include(r => r.RoutePoints.OrderBy(rp => rp.SequenceNo))
                .ThenInclude(rp => rp.Point)
                    .ThenInclude(p => p.GalleryImages)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    /// <summary>
    /// Explicit loading: ДВА SQL-запити.
    /// Спочатку вибирається базовий маршрут (1-й запит).
    /// Потім через Entry(x).Collection(...).Query().Include(...).LoadAsync(ct) завантажується зв'язана колекція (2-й запит).
    /// </summary>
    public async Task<TrailRoute?> GetWithDetailsExplicitAsync(long id, CancellationToken ct = default)
    {
        // 1-й SQL-запит: тільки сутність TrailRoute
        var route = await DbSet.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (route is null)
            return null;

        // 2-й SQL-запит: явне завантаження RoutePoints з підтягуванням Point
        await Context.Entry(route)
            .Collection(r => r.RoutePoints)
            .Query()
            .Include(rp => rp.Point)
            .OrderBy(rp => rp.SequenceNo)
            .LoadAsync(ct);

        return route;
    }

    public async Task<bool> ExistsBySlugAsync(string slug, long? excludeId = null, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return await DbSet.AnyAsync(r => r.Slug == normalized && (!excludeId.HasValue || r.Id != excludeId.Value), ct);
    }
}
