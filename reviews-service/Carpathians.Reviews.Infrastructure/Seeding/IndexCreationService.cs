using Carpathians.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Seeding;

/// <summary>
/// Створює індекси всіх колекцій під час старту застосунку (до сідінгу).
/// Назви й параметри збігаються з reviews-db/init.js (Лаб. 01), тому повторне створення безпечне:
/// MongoDB не дублює наявний індекс. Кожен індекс створено під конкретний запит.
/// </summary>
public class IndexCreationService(MongoDbContext context, ILogger<IndexCreationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await CreateReviewIndexesAsync(ct);
            await CreateDiscussionIndexesAsync(ct);
            await CreateRatingIndexesAsync(ct);
            logger.LogInformation("MongoDB indexes are ensured for database {Database}",
                context.Database.DatabaseNamespace.DatabaseName);
        }
        catch (MongoCommandException ex) when (ex.Code is 85 or 86)   // IndexOptionsConflict / IndexKeySpecsConflict
        {
            logger.LogWarning("Index with the same keys already exists with different options: {Message}", ex.Message);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            // База недоступна — сервіс стартує, стан видно на /health; індекси створяться при наступному старті.
            logger.LogError(ex, "Failed to create MongoDB indexes");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private Task CreateReviewIndexesAsync(CancellationToken ct)
    {
        var reviews = context.ReviewDocuments;
        return reviews.Indexes.CreateManyAsync(new[]
        {
            // «останні відгуки маршруту»: GET /api/reviews?routeId=..&sortBy=createdAt
            Index(new BsonDocument { { "routeId", 1 }, { "createdAt", -1 } }, "ix_reviews_route_created"),

            // один відгук від автора на маршрут (409 при дублікаті) + ExistsAsync у валідаторі
            Index(new BsonDocument { { "routeId", 1 }, { "author.userId", 1 } }, "ux_reviews_route_author",
                unique: true),

            // відгуки користувача: GET /api/reviews?authorId=..
            Index(new BsonDocument("author.userId", 1), "ix_reviews_author"),

            // reference на обговорення — лише для документів, де поле є (частковий індекс)
            new CreateIndexModel<BsonDocument>(
                new BsonDocument("discussionId", 1),
                new CreateIndexOptions<BsonDocument>
                {
                    Name = "ix_reviews_discussion",
                    PartialFilterExpression = Builders<BsonDocument>.Filter.Exists("discussionId")
                }),

            // повнотекстовий пошук: GET /api/reviews?search=..
            new CreateIndexModel<BsonDocument>(
                new BsonDocument("text", "text"),
                new CreateIndexOptions { Name = "tx_reviews_text", DefaultLanguage = "none" }),

            // фільтр за тегом (multikey-індекс по масиву): GET /api/reviews?tag=..
            Index(new BsonDocument("tags", 1), "ix_reviews_tags"),

            // сортування за оцінкою: GET /api/reviews?sortBy=rating
            Index(new BsonDocument { { "rating", -1 }, { "_id", -1 } }, "ix_reviews_rating")
        }, ct);
    }

    private async Task CreateDiscussionIndexesAsync(CancellationToken ct)
    {
        // обговорення маршруту, найактивніші першими: GET /api/routes/{id}/discussions
        await context.DiscussionDocuments.Indexes.CreateOneAsync(
            Index(new BsonDocument { { "routeId", 1 }, { "lastActivityAt", -1 } }, "ix_discussions_route_activity"),
            cancellationToken: ct);

        // гілка повідомлень у хронологічному порядку: GET /api/discussions/{id}/messages
        await context.DiscussionMessageDocuments.Indexes.CreateOneAsync(
            Index(new BsonDocument { { "discussionId", 1 }, { "createdAt", 1 } }, "ix_messages_discussion_created"),
            cancellationToken: ct);
    }

    private Task CreateRatingIndexesAsync(CancellationToken ct)
        // один документ рейтингу на маршрут; потрібен для $merge on: routeId
        => context.Ratings.Indexes.CreateOneAsync(
            Index(new BsonDocument("routeId", 1), "ux_ratings_route", unique: true),
            cancellationToken: ct);

    private static CreateIndexModel<BsonDocument> Index(BsonDocument keys, string name, bool unique = false)
        => new(keys, new CreateIndexOptions { Name = name, Unique = unique ? true : null });
}
