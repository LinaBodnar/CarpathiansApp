using Carpathians.Domain.Models;
namespace Carpathians.Dal.Interfaces;

public interface IDepartureRepository : IGenericRepository<Departure>
{
    Task<bool> UpdateAsync(Departure departure, CancellationToken ct = default);
    Task<Departure?> GetWithRouteNameAsync(long id, CancellationToken ct = default);
    Task DecreaseSeatsAsync(long departureId, int seats, CancellationToken ct = default);
}