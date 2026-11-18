using Carpathians.Domain.Entities;

namespace Carpathians.Dal.Interfaces;

public interface IRegionRepository : IGenericRepository<Region>
{
    Task<Region?> GetWithRoutesAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null, CancellationToken ct = default);
}
