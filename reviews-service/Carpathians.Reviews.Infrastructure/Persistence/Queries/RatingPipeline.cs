using MongoDB.Bson;

namespace Carpathians.Reviews.Infrastructure.Persistence.Queries;

/// <summary>
/// Спільні стадії aggregation pipeline для рейтингу маршрутів:
/// $group по routeId → середня оцінка, кількість, розподіл оцінок 1–5.
/// </summary>
internal static class RatingPipeline
{
    public static BsonDocument GroupByRoute() => new("$group", new BsonDocument
    {
        { "_id", "$routeId" },
        { "routeName", new BsonDocument("$last", "$routeName") },
        { "avgRating", new BsonDocument("$avg", "$rating") },
        { "count", new BsonDocument("$sum", 1) },
        { "s1", StarCount(1) },
        { "s2", StarCount(2) },
        { "s3", StarCount(3) },
        { "s4", StarCount(4) },
        { "s5", StarCount(5) }
    });

    public static BsonDocument ProjectRating() => new("$project", new BsonDocument
    {
        { "_id", 0 },
        { "routeId", "$_id" },
        { "routeName", 1 },
        { "avgRating", new BsonDocument("$round", new BsonArray { "$avgRating", 2 }) },
        { "count", 1 },
        { "distribution", new BsonDocument
            {
                { "1", "$s1" }, { "2", "$s2" }, { "3", "$s3" }, { "4", "$s4" }, { "5", "$s5" }
            }
        }
    });

    private static BsonDocument StarCount(int stars)
        => new("$sum", new BsonDocument("$cond", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { "$rating", stars }), 1, 0
        }));
}
