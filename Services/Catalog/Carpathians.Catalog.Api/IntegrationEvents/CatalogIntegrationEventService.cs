using System.Diagnostics;
using System.Text;
using Carpathians.Bll.Interfaces;
using Carpathians.Catalog.Api.IntegrationEvents.Events;
using Carpathians.Dal.Data;
using Carpathians.Dal.Outbox;
using Carpathians.Domain.Entities;
using Carpathians.Platform.EventBus.Events;
using Carpathians.Platform.EventBus.Messaging;
using Carpathians.Platform.EventBus.Publishing;
using Carpathians.Platform.EventBus.Topology;

namespace Carpathians.Catalog.Api.IntegrationEvents;

/// <summary>
/// Integration events Каталогу через transactional outbox (scoped, той самий CatalogDbContext, що й UnitOfWork):
/// <list type="number">
/// <item><c>Add*</c> — створює подію з CorrelationId поточного запиту і додає запис у IntegrationEventLog;
/// запис потрапляє в БД разом зі змінами маршруту під час Commit (одна транзакція);</item>
/// <item><see cref="PublishPendingAsync"/> — після коміту публікує ці події в catalog-events і позначає Published.
/// Збій брокера не скасовує збережену зміну: запис отримує стан PublishFailed, повтор — у CatalogOutboxPublisher.</item>
/// </list>
/// </summary>
public sealed class CatalogIntegrationEventService(
    CatalogDbContext db,
    IEventPublisher publisher,
    CorrelationContext correlation,
    ILogger<CatalogIntegrationEventService> logger) : ICatalogIntegrationEventService
{
    private readonly List<IntegrationEventLogEntry> _pending = [];

    public Task AddRouteCreatedAsync(TrailRoute route, CancellationToken ct = default)
        => AddAsync(new RouteCreatedIntegrationEvent(route.Id, route.Name, route.Slug, route.RegionId,
                route.Difficulty.ToString()) { CorrelationId = correlation.Current },
            CatalogTopology.RoutingKeys.RouteCreated, route.Id, ct);

    public Task AddRouteRenamedAsync(TrailRoute route, string oldName, CancellationToken ct = default)
        => AddAsync(new RouteRenamedIntegrationEvent(route.Id, oldName, route.Name)
                { CorrelationId = correlation.Current },
            CatalogTopology.RoutingKeys.RouteRenamed, route.Id, ct);

    public Task AddRouteDeletedAsync(TrailRoute route, CancellationToken ct = default)
        => AddAsync(new RouteDeletedIntegrationEvent(route.Id, route.Name) { CorrelationId = correlation.Current },
            CatalogTopology.RoutingKeys.RouteDeleted, route.Id, ct);

    public async Task PublishPendingAsync(CancellationToken ct = default)
    {
        foreach (var entry in _pending)
            await PublishEntryAsync(db, publisher, entry, logger, ct);

        _pending.Clear();
        await db.SaveChangesAsync(CancellationToken.None);       // стани Published / PublishFailed
    }

    private async Task AddAsync<T>(T @event, string routingKey, long routeId, CancellationToken ct)
        where T : IntegrationEvent
    {
        var entry = new IntegrationEventLogEntry(
            @event.EventId,
            typeof(T).Name,
            @event.SchemaVersion,
            CatalogTopology.Exchange.Name,
            routingKey,
            Encoding.UTF8.GetString(EventJson.Serialize(@event)),
            @event.OccurredAt,
            @event.CorrelationId,
            Activity.Current?.Id);                                   // W3C traceparent запиту

        await db.IntegrationEventLog.AddAsync(entry, ct);
        _pending.Add(entry);

        logger.LogInformation(
            "Integration event {EventType} {EventId} for route {RouteId} saved to outbox, CorrelationId {CorrelationId}",
            entry.EventType, entry.EventId, routeId, entry.CorrelationId);
    }

    /// <summary>Публікація одного запису outbox і фіксація результату (без SaveChanges — його робить викликач).</summary>
    internal static async Task PublishEntryAsync(CatalogDbContext db, IEventPublisher publisher,
        IntegrationEventLogEntry entry, ILogger logger, CancellationToken ct)
    {
        var message = new OutgoingMessage(
            entry.EventId,
            entry.EventType,
            entry.SchemaVersion,
            entry.OccurredAt,
            entry.CorrelationId,
            entry.Exchange,
            entry.RoutingKey,
            Encoding.UTF8.GetBytes(entry.Content))
        {
            // у фоновому процесі Activity.Current немає — продовжуємо trace запиту, що зберіг подію
            ParentContext = Activity.Current is null
                            && ActivityContext.TryParse(entry.TraceParent, null, out var parent)
                ? parent
                : default
        };

        try
        {
            await publisher.PublishAsync(message, ct);
            entry.MarkPublished(DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            entry.MarkFailed($"{ex.GetType().Name}: {ex.Message}");
            logger.LogWarning(ex,
                "Integration event {EventType} {EventId} stays in outbox (attempt {Attempt}), CorrelationId {CorrelationId}",
                entry.EventType, entry.EventId, entry.TimesSent, entry.CorrelationId);
        }

        db.IntegrationEventLog.Update(entry);
    }
}
