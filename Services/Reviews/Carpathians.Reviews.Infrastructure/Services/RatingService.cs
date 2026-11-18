using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Infrastructure.Persistence;
using Carpathians.Reviews.Infrastructure.Persistence.Queries;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Services;

/// <summary>
/// Перераховує похідні дані маршруту в колекції <c>ratings</c> aggregation pipeline зі стадією
/// <c>$merge</c> (upsert за унікальним індексом ux_ratings_route). Якщо відгуків не лишилося —
/// документ рейтингу видаляється.
/// </summary>
public class RatingService(MongoDbContext context) : IRatingService
{
    public async Task RecalculateAsync(long routeId, CancellationToken ct = default)
    {
        var routeFilter = Builders<BsonDocument>.Filter.Eq("routeId", routeId);

        if (await context.ReviewDocuments.CountDocumentsAsync(routeFilter, cancellationToken: ct) == 0)
        {
            await context.Ratings.DeleteOneAsync(routeFilter, ct);
            return;
        }

        var project = RatingPipeline.ProjectRating();
        project["$project"].AsBsonDocument.Add("updatedAt", "$$NOW");

        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("routeId", routeId)),
            RatingPipeline.GroupByRoute(),
            project,
            new BsonDocument("$merge", new BsonDocument
            {
                { "into", MongoDbContext.RatingsCollection },
                { "on", "routeId" },
                { "whenMatched", "replace" },
                { "whenNotMatched", "insert" }
            })
        };

        await context.ReviewDocuments.AggregateToCollectionAsync(
            PipelineDefinition<BsonDocument, BsonDocument>.Create(pipeline), cancellationToken: ct);
    }
}
