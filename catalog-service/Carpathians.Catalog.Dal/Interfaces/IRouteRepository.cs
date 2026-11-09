using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;

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

    /// <summary>
    /// Пагінація, фільтрація та сортування на рівні IQueryable (SQL Server) ДО ToListAsync.
    /// </summary>
    Task<(IReadOnlyList<TrailRoute> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        int? regionId = null,
        Difficulty? difficulty = null,
        decimal? maxDistanceKm = null,
        string? sortBy = null,
        string? sortDir = null,
        CancellationToken ct = default);
}
