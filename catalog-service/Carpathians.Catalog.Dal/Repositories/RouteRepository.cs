using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Dal.Repositories;

public class RouteRepository(CatalogDbContext context)
    : GenericRepository<TrailRoute>(context), IRouteRepository
{
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

    public async Task<TrailRoute?> GetWithDetailsExplicitAsync(long id, CancellationToken ct = default)
    {
        var route = await DbSet.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (route is null)
            return null;

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

    public async Task<(IReadOnlyList<TrailRoute> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        int? regionId = null,
        Difficulty? difficulty = null,
        decimal? maxDistanceKm = null,
        string? sortBy = null,
        string? sortDir = null,
        CancellationToken ct = default)
    {
        IQueryable<TrailRoute> query = DbSet
            .AsNoTracking()
            .Include(r => r.Region)
            .Include(r => r.Detail);

        // 1. Пошук (назва або slug)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(r => EF.Functions.Like(r.Name, pattern) || EF.Functions.Like(r.Slug, pattern));
        }

        // 2. Фільтри предметної області
        if (regionId.HasValue)
        {
            query = query.Where(r => r.RegionId == regionId.Value);
        }

        if (difficulty.HasValue)
        {
            query = query.Where(r => r.Difficulty == difficulty.Value);
        }

        if (maxDistanceKm.HasValue)
        {
            query = query.Where(r => r.DistanceKm <= maxDistanceKm.Value);
        }

        // 3. Підрахунок загальної кількості в SQL ДО Skip/Take
        var totalCount = await query.CountAsync(ct);

        // 4. Сортування за білим списком (whitelist)
        var isDescending = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);

        query = (sortBy?.ToLowerInvariant(), isDescending) switch
        {
            ("name", false) => query.OrderBy(r => r.Name),
            ("name", true) => query.OrderByDescending(r => r.Name),
            ("distance", false) => query.OrderBy(r => r.DistanceKm),
            ("distance", true) => query.OrderByDescending(r => r.DistanceKm),
            ("duration", false) => query.OrderBy(r => r.DurationHours),
            ("duration", true) => query.OrderByDescending(r => r.DurationHours),
            ("elevation", false) => query.OrderBy(r => r.ElevationGainM),
            ("elevation", true) => query.OrderByDescending(r => r.ElevationGainM),
            ("createdat", false) => query.OrderBy(r => r.CreatedAt),
            ("createdat", true) => query.OrderByDescending(r => r.CreatedAt),
            _ => isDescending ? query.OrderByDescending(r => r.Id) : query.OrderBy(r => r.Id)
        };

        // 5. Пагінація на рівні SQL (OFFSET ... FETCH NEXT)
        var skip = (page - 1) * pageSize;
        var items = await query.Skip(skip).Take(pageSize).ToListAsync(ct);

        return (items, totalCount);
    }
}
