using Carpathians.Api.IntegrationEvents.Events;
using Carpathians.Platform.EventBus.Consuming;

namespace Carpathians.Api.IntegrationEvents.Handlers;

// Окремий обробник на кожен тип події Каталогу (черга booking.catalog-sync, binding route.*).
// Створюються в DI-scope повідомлення разом зі своїм UnitOfWork.

/// <summary>route.created — новий маршрут з'являється в довіднику RouteRefs.</summary>
public sealed class RouteCreatedHandler(RouteRefsSynchronizer sync)
    : IIntegrationEventHandler<RouteCreatedIntegrationEvent>
{
    public Task HandleAsync(RouteCreatedIntegrationEvent e, CancellationToken ct)
        => sync.ApplyAsync(e, nameof(RouteCreatedIntegrationEvent), e.RouteId, e.Name, isActive: true, ct);
}

/// <summary>route.renamed — нова назва в RouteRefs; назви в оформлених бронюваннях не змінюються.</summary>
public sealed class RouteRenamedHandler(RouteRefsSynchronizer sync)
    : IIntegrationEventHandler<RouteRenamedIntegrationEvent>
{
    public Task HandleAsync(RouteRenamedIntegrationEvent e, CancellationToken ct)
        => sync.ApplyAsync(e, nameof(RouteRenamedIntegrationEvent), e.RouteId, e.NewName, isActive: true, ct);
}

/// <summary>route.deleted — копія лишається (на неї посилаються виїзди), але позначається неактивною.</summary>
public sealed class RouteDeletedHandler(RouteRefsSynchronizer sync)
    : IIntegrationEventHandler<RouteDeletedIntegrationEvent>
{
    public Task HandleAsync(RouteDeletedIntegrationEvent e, CancellationToken ct)
        => sync.ApplyAsync(e, nameof(RouteDeletedIntegrationEvent), e.RouteId, e.Name, isActive: false, ct);
}
