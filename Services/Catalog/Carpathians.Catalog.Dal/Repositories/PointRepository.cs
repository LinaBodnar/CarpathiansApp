using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Dal.Repositories;

public class PointRepository(CatalogDbContext context)
    : GenericRepository<MapPoint>(context), IPointRepository
{
    public async Task<MapPoint?> GetWithGalleryAsync(long id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(p => p.GalleryImages.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<bool> ExistsByNameAsync(string name, long? excludeId = null, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        return await DbSet.AnyAsync(p => p.Name == trimmed && (!excludeId.HasValue || p.Id != excludeId.Value), ct);
    }

    public async Task<IReadOnlyList<MapPoint>> GetByTypeAsync(PointType pointType, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(p => p.PointType == pointType)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }
}
