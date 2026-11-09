using Carpathians.Reviews.Infrastructure.Persistence;
using Carpathians.Reviews.Infrastructure.Persistence.Queries;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Seeding;

/// <summary>Похідна колекція ratings: перерахунок по всіх маршрутах ($group + $merge).</summary>
public class RatingSeeder(MongoDbContext context, ILogger<RatingSeeder> logger) : IDataSeeder
{
    public int Order => 3;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.Ratings.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
            return;                                                    // уже наповнено

        var project = RatingPipeline.ProjectRating();
        project["$project"].AsBsonDocument.Add("updatedAt", "$$NOW");

        var pipeline = new[]
        {
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

        logger.LogInformation("Seeded ratings: {Count} routes",
            await context.Ratings.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: ct));
    }
}
