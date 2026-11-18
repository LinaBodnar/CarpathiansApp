using Carpathians.Dal.Interfaces;
using Carpathians.Platform.EventBus.Consuming;
using Carpathians.Platform.EventBus.Events;

namespace Carpathians.Api.IntegrationEvents;

/// <summary>
/// Оновлення копії довідника маршрутів (dbo.RouteRefs) за подією Каталогу — спільна логіка обробників.
/// <list type="number">
/// <item>одна транзакція: запис EventId у ProcessedEvents + upsert RouteRefs;</item>
/// <item>EventId уже є — дублікат (повторна доставка): відкат, нічого не змінюється;</item>
/// <item>подія старша за вже застосовану (CatalogChangedAt) — out-of-order: подію позначено обробленою,
/// але новіші дані не перезаписано.</item>
/// </list>
/// Ціни й назви в оформлених бронюваннях (BookingItems.UnitPrice, BookingItems.RouteName) — знімки на момент
/// покупки, їх подія не чіпає.
/// </summary>
public sealed class RouteRefsSynchronizer(IUnitOfWork uow, ILogger<RouteRefsSynchronizer> logger)
{
    public async Task ApplyAsync(IntegrationEvent @event, string eventType, long routeId, string name, bool isActive,
        CancellationToken ct)
    {
        if (routeId <= 0 || string.IsNullOrWhiteSpace(name))
            throw new PermanentEventException($"{eventType} {@event.EventId} has invalid route data (id {routeId})");

        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, eventType, ct))
            {
                await uow.RollbackAsync(ct);
                logger.LogInformation(
                    "Duplicate {EventType} {EventId} for route {RouteId} skipped, CorrelationId {CorrelationId}",
                    eventType, @event.EventId, routeId, @event.CorrelationId);
                return;
            }

            var applied = await uow.RouteRefs.ApplyAsync(routeId, name.Trim(), isActive, @event.OccurredAt, ct);
            await uow.CommitAsync(ct);

            if (applied)
                logger.LogInformation(
                    "RouteRefs {RouteId} updated from {EventType} {EventId}: name {RouteName}, active {IsActive}, CorrelationId {CorrelationId}",
                    routeId, eventType, @event.EventId, name, isActive, @event.CorrelationId);
            else
                logger.LogWarning(
                    "Out-of-order {EventType} {EventId} for route {RouteId} (occurred {OccurredAt:O}) ignored: newer change already applied",
                    eventType, @event.EventId, routeId, @event.OccurredAt);
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
