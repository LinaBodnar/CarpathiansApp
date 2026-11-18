using Carpathians.Reviews.Application.Common.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Persistence;

/// <summary>
/// Реалізація <see cref="IIntegrationEventStore"/> на MongoDB:
/// <list type="bullet">
/// <item><c>processedEvents</c> — { _id: EventId, eventType, processedAt }; унікальність _id = ідемпотентність;</item>
/// <item><c>routeNameSync</c> — { _id: routeId, routeName, changedAt }: час останньої застосованої події Каталогу
/// для кожного маршруту (захист від out-of-order).</item>
/// </list>
/// </summary>
public sealed class IntegrationEventStore(MongoDbContext context) : IIntegrationEventStore
{
    public const string ProcessedEventsCollection = "processedEvents";
    public const string RouteNameSyncCollection = "routeNameSync";

    private const int DuplicateKey = 11000;

    private IMongoCollection<BsonDocument> ProcessedEvents
        => context.Database.GetCollection<BsonDocument>(ProcessedEventsCollection);

    private IMongoCollection<BsonDocument> RouteNameSync
        => context.Database.GetCollection<BsonDocument>(RouteNameSyncCollection);

    public Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default)
        => ProcessedEvents.Find(new BsonDocument("_id", eventId.ToString())).Limit(1).AnyAsync(ct);

    public async Task MarkProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        try
        {
            await ProcessedEvents.InsertOneAsync(new BsonDocument
            {
                { "_id", eventId.ToString() },
                { "eventType", eventType },
                { "processedAt", DateTime.UtcNow }
            }, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == DuplicateKey)
        {
            // паралельна обробка тієї самої події вже позначила її — результат той самий
        }
    }

    public async Task<bool> TryAdvanceRouteNameAsync(long routeId, string routeName, DateTime changedAt,
        CancellationToken ct = default)
    {
        // $lte, а не $lt: повторна обробка тієї самої події (збій після цього кроку) має пройти далі
        var filter = new BsonDocument
        {
            { "_id", routeId },
            { "changedAt", new BsonDocument("$lte", changedAt) }
        };
        var update = new BsonDocument("$set", new BsonDocument
        {
            { "routeName", routeName },
            { "changedAt", changedAt }
        });

        try
        {
            await RouteNameSync.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Code == DuplicateKey)
        {
            // документ маршруту є, але з новішим changedAt — фільтр не збігся, upsert вставляв дублікат _id
            return false;
        }
    }
}
