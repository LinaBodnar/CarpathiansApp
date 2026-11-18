using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Dal.Repositories;

public class RoutePointRepository(CatalogDbContext context)
    : GenericRepository<RoutePoint>(context), IRoutePointRepository
{
    /// <summary>
    /// LINQ to Entities по M:N зв'язку через проміжну сутність RoutePoint з фільтрацією за власним полем (SequenceNo >= minSequenceNo).
    /// </summary>
    public async Task<IReadOnlyList<RoutePoint>> GetPointsByRouteIdAsync(
        long routeId, int minSequenceNo = 1, CancellationToken ct = default)
    {
        return await Context.RoutePoints
            .AsNoTracking()
            .Where(rp => rp.RouteId == routeId && rp.SequenceNo >= minSequenceNo)
            .OrderBy(rp => rp.SequenceNo)
            .Include(rp => rp.Point)
                .ThenInclude(p => p.GalleryImages)
            .ToListAsync(ct);
    }

    /// <summary>
    /// LINQ to Entities по M:N у зворотному напрямку: пошук маршрутів, що проходять через задану точку.
    /// </summary>
    public async Task<IReadOnlyList<TrailRoute>> GetRoutesPassingThroughPointAsync(
        long pointId, CancellationToken ct = default)
    {
        return await Context.RoutePoints
            .AsNoTracking()
            .Where(rp => rp.PointId == pointId)
            .OrderBy(rp => rp.SequenceNo)
            .Select(rp => rp.Route)
            .Include(r => r.Region)
            .Distinct()
            .ToListAsync(ct);
    }
}
