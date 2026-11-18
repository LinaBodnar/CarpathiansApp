using System.Data;
namespace Carpathians.Dal.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    ICustomerRepository Customers { get; }
    IDepartureRepository Departures { get; }
    IBookingRepository Bookings { get; }
    IRouteRefRepository RouteRefs { get; }              // копія довідника маршрутів Каталогу (Лаб. 08)
    IProcessedEventRepository ProcessedEvents { get; }  // ідемпотентність обробників подій (Лаб. 08)
    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}