using Carpathians.Domain.Entities;

namespace Carpathians.Dal.Interfaces;

public interface IRoutePointRepository : IGenericRepository<RoutePoint>
{
    /// <summary>
    /// LINQ to Entities по M:N зв'язку через проміжну сутність RoutePoint із фільтрацією/сортуванням за власним полем (SequenceNo).
    /// Повертає список точок маршруту з порядковим номером у ланцюжку.
    /// </summary>
    Task<IReadOnlyList<RoutePoint>> GetPointsByRouteIdAsync(long routeId, int minSequenceNo = 1, CancellationToken ct = default);

    /// <summary>
    /// LINQ to Entities по M:N у зворотному напрямку: пошук маршрутів, що проходять через конкретну точку (наприклад, вершину чи притулок).
    /// </summary>
    Task<IReadOnlyList<TrailRoute>> GetRoutesPassingThroughPointAsync(long pointId, CancellationToken ct = default);
}
