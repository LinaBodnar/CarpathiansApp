using System.Data;

namespace Carpathians.Dal.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    IRouteRepository Routes { get; }
    IPointRepository Points { get; }
    IRoutePointRepository RoutePoints { get; }
    IRegionRepository Regions { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
