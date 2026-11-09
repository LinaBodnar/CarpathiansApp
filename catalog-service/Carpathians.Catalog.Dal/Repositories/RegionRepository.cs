using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Dal.Repositories;

public class RegionRepository(CatalogDbContext context)
    : GenericRepository<Region>(context), IRegionRepository
{
    public async Task<Region?> GetWithRoutesAsync(int id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(r => r.Routes)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken ct = default)
    {
        var trimmed = name.Trim();
        return await DbSet.AnyAsync(r => r.Name == trimmed && (!excludeId.HasValue || r.Id != excludeId.Value), ct);
    }
}
