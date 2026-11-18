using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;

namespace Carpathians.Dal.Interfaces;

public interface IPointRepository : IGenericRepository<MapPoint>
{
    /// <summary>
    /// Eager loading точки з її галереєю.
    /// </summary>
    Task<MapPoint?> GetWithGalleryAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Перевірка унікальності назви точки.
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, long? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Точки певного типу.
    /// </summary>
    Task<IReadOnlyList<MapPoint>> GetByTypeAsync(PointType pointType, CancellationToken ct = default);
}
