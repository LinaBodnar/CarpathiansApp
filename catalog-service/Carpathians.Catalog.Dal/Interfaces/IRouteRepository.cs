using Carpathians.Domain.Entities;

namespace Carpathians.Dal.Interfaces;

public interface IRouteRepository : IGenericRepository<TrailRoute>
{
    /// <summary>
    /// Eager loading: ОДИН SQL-запит із Include/ThenInclude для повної картки маршруту.
    /// </summary>
    Task<TrailRoute?> GetWithDetailsEagerAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Explicit loading: ДВА SQL-запити — спочатку базовий маршрут, потім явне завантаження колекцій.
    /// </summary>
    Task<TrailRoute?> GetWithDetailsExplicitAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Перевірка унікальності slug для створення/оновлення.
    /// </summary>
    Task<bool> ExistsBySlugAsync(string slug, long? excludeId = null, CancellationToken ct = default);
}
