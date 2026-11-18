using System.Text;
using Carpathians.Dal.Data;
using Carpathians.Platform.EventBus.Publishing;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Catalog.Api.IntegrationEvents;

/// <summary>
/// Development-ендпоінти для перевірки подієвої інтеграції (Лаб. 08):
/// <list type="bullet">
/// <item><c>GET /dev/outbox</c> — останні записи IntegrationEventLog (EventId, стан, кількість відправлень);</item>
/// <item><c>POST /dev/outbox/{eventId}/republish</c> — повторна публікація ТІЄЇ САМОЇ події (той самий EventId):
/// сценарій «дублікат» — споживачі мають пропустити її як уже оброблену.</item>
/// </list>
/// </summary>
public static class OutboxDevEndpoints
{
    public static WebApplication MapOutboxDevEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/dev/outbox").WithTags("Dev: outbox");

        group.MapGet("/", async (CatalogDbContext db, CancellationToken ct) =>
            await db.IntegrationEventLog
                .OrderByDescending(e => e.OccurredAt)
                .Take(20)
                .Select(e => new
                {
                    e.EventId, e.EventType, e.RoutingKey, State = e.State.ToString(), e.TimesSent,
                    e.OccurredAt, e.PublishedAt, e.CorrelationId, e.LastError, e.Content
                })
                .ToListAsync(ct));

        group.MapPost("/{eventId:guid}/republish", async (Guid eventId, CatalogDbContext db, IEventPublisher publisher,
            CancellationToken ct) =>
        {
            var entry = await db.IntegrationEventLog.FirstOrDefaultAsync(e => e.EventId == eventId, ct);
            if (entry is null)
                return Results.NotFound();

            await publisher.PublishAsync(new OutgoingMessage(entry.EventId, entry.EventType, entry.SchemaVersion,
                entry.OccurredAt, entry.CorrelationId, entry.Exchange, entry.RoutingKey,
                Encoding.UTF8.GetBytes(entry.Content)), ct);

            return Results.Accepted(value: new { entry.EventId, entry.EventType, Republished = true });
        });

        return app;
    }
}
