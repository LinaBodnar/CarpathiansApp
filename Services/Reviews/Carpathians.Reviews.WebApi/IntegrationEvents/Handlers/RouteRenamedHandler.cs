using Carpathians.Platform.EventBus.Consuming;
using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Reviews.Commands.SyncRouteName;
using Carpathians.Reviews.WebApi.IntegrationEvents.Events;
using FluentValidation;
using MediatR;

namespace Carpathians.Reviews.WebApi.IntegrationEvents.Handlers;

/// <summary>
/// route.renamed — денормалізована назва маршруту в усіх відгуках і обговореннях (reviews.routeName,
/// discussions.routeName) за політикою дублювання з Лаб. 01. Бізнес-дію виконує наявна CQRS-команда
/// <see cref="SyncRouteNameCommand"/> (валідація, логування, метрики; кеш маршруту інвалідує CachedReviewRepository).
/// <list type="number">
/// <item>EventId уже в processedEvents — дублікат, пропуск;</item>
/// <item>подія старша за застосовану назву — out-of-order, позначити обробленою без змін;</item>
/// <item>UpdateMany routeName -> позначити EventId обробленим.</item>
/// </list>
/// </summary>
public sealed class RouteRenamedHandler(
    IIntegrationEventStore store,
    ISender mediator,
    ILogger<RouteRenamedHandler> logger) : IIntegrationEventHandler<RouteRenamedIntegrationEvent>
{
    public async Task HandleAsync(RouteRenamedIntegrationEvent e, CancellationToken ct)
    {
        const string eventType = nameof(RouteRenamedIntegrationEvent);

        if (await store.IsProcessedAsync(e.EventId, ct))
        {
            logger.LogInformation("Duplicate {EventType} {EventId} for route {RouteId} skipped, CorrelationId {CorrelationId}",
                eventType, e.EventId, e.RouteId, e.CorrelationId);
            return;
        }

        if (!await store.TryAdvanceRouteNameAsync(e.RouteId, e.NewName, e.OccurredAt, ct))
        {
            logger.LogWarning(
                "Out-of-order {EventType} {EventId} for route {RouteId} (occurred {OccurredAt:O}) ignored: newer name already applied",
                eventType, e.EventId, e.RouteId, e.OccurredAt);
            await store.MarkProcessedAsync(e.EventId, eventType, ct);
            return;
        }

        long updated;
        try
        {
            updated = await mediator.Send(new SyncRouteNameCommand(e.RouteId, e.NewName), ct);
        }
        catch (ValidationException ex)
        {
            // некоректні дані події від повтору не виправляться — одразу в DLQ
            throw new PermanentEventException($"{eventType} {e.EventId} is invalid: {ex.Message}", ex);
        }

        await store.MarkProcessedAsync(e.EventId, eventType, ct);

        logger.LogInformation(
            "Route {RouteId} renamed '{OldName}' -> '{NewName}': {UpdatedDocuments} reviews/discussions updated, EventId {EventId}, CorrelationId {CorrelationId}",
            e.RouteId, e.OldName, e.NewName, updated, e.EventId, e.CorrelationId);
    }
}
