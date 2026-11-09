using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Persistence.Queries;

/// <summary>
/// Читальна сторона CQRS для відгуків: фільтрація ($match), сортування, пагінація й агрегація
/// виконуються в MongoDB; з БД вибираються лише потрібні поля (проєкція одразу у форму DTO).
/// </summary>
public class ReviewQueries(MongoDbContext context) : IReviewQueries
{
    private readonly IMongoCollection<BsonDocument> _reviews = context.ReviewDocuments;
    private static readonly FilterDefinitionBuilder<BsonDocument> Filter = Builders<BsonDocument>.Filter;
    private static readonly SortDefinitionBuilder<BsonDocument> Sort = Builders<BsonDocument>.Sort;

    public async Task<ReviewDetailsDto?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
    {
        var document = await _reviews.Find(Filter.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return document is null ? null : ReviewReadMapper.ToReviewDetailsDto(document);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(
        ReviewFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        var match = BuildFilter(filter);

        var totalTask = _reviews.CountDocumentsAsync(match, cancellationToken: ct);
        var itemsTask = _reviews.Aggregate()
            .Match(match)                                            // $match (першою стадією — для $text)
            .Sort(BuildSort(filter.SortBy, filter.SortDir))          // підкріплено ix_reviews_route_created
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project(new BsonDocumentProjectionDefinition<BsonDocument, BsonDocument>(
                ReviewReadMapper.ListProjection()))
            .ToListAsync(ct);

        await Task.WhenAll(totalTask, itemsTask);

        var items = itemsTask.Result.Select(ReviewReadMapper.ToReviewDto).ToList();
        return new PagedResult<ReviewDto>(items, totalTask.Result, page, pageSize);
    }

    public async Task<CursorPage<ReviewDto>> GetFeedAsync(
        long? routeId, ObjectId? after, int limit, CancellationToken ct = default)
    {
        var filter = Filter.Empty;
        if (routeId is not null)
            filter &= Filter.Eq("routeId", routeId.Value);
        if (after is not null)
            filter &= Filter.Lt("_id", after.Value);                 // наступні після курсора

        // ObjectId зростає разом із часом створення, тому сортування за _id = за датою
        var documents = await _reviews.Find(filter)
            .Sort(Sort.Descending("_id"))
            .Limit(limit + 1)                                        // +1 — щоб знати, чи є ще сторінка
            .Project(new BsonDocumentProjectionDefinition<BsonDocument, BsonDocument>(
                ReviewReadMapper.ListProjection()))
            .ToListAsync(ct);

        var hasMore = documents.Count > limit;
        var items = documents.Take(limit).Select(ReviewReadMapper.ToReviewDto).ToList();
        return new CursorPage<ReviewDto>(items, hasMore ? items[^1].Id : null, hasMore);
    }

    public async Task<RouteRatingDto?> GetRouteRatingAsync(long routeId, CancellationToken ct = default)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("routeId", routeId)),
            RatingPipeline.GroupByRoute(),
            RatingPipeline.ProjectRating()
        };

        using var cursor = await _reviews.AggregateAsync(
            PipelineDefinition<BsonDocument, BsonDocument>.Create(pipeline), cancellationToken: ct);
        var document = await cursor.FirstOrDefaultAsync(ct);

        return document is null ? null : ReviewReadMapper.ToRouteRatingDto(document);
    }

    public async Task<IReadOnlyList<RouteRatingDto>> GetTopRatedRoutesAsync(
        int top, int minReviews, CancellationToken ct = default)
    {
        var pipeline = new[]
        {
            RatingPipeline.GroupByRoute(),
            new BsonDocument("$match", new BsonDocument("count", new BsonDocument("$gte", minReviews))),
            new BsonDocument("$sort", new BsonDocument { { "avgRating", -1 }, { "count", -1 } }),
            new BsonDocument("$limit", top),
            RatingPipeline.ProjectRating()
        };

        using var cursor = await _reviews.AggregateAsync(
            PipelineDefinition<BsonDocument, BsonDocument>.Create(pipeline), cancellationToken: ct);
        var documents = await cursor.ToListAsync(ct);

        return documents.Select(ReviewReadMapper.ToRouteRatingDto).ToList();
    }

    private static FilterDefinition<BsonDocument> BuildFilter(ReviewFilter f)
    {
        var filter = Filter.Empty;
        if (f.RouteId is not null)
            filter &= Filter.Eq("routeId", f.RouteId.Value);
        if (!string.IsNullOrWhiteSpace(f.AuthorId))
            filter &= Filter.Eq("author.userId", f.AuthorId);
        if (f.MinRating is not null)
            filter &= Filter.Gte("rating", f.MinRating.Value);
        if (!string.IsNullOrWhiteSpace(f.Tag))
            filter &= Filter.Eq("tags", f.Tag.ToLowerInvariant());   // елемент масиву
        if (!string.IsNullOrWhiteSpace(f.Search))
            filter &= Filter.Text(f.Search);                         // tx_reviews_text
        return filter;
    }

    private static SortDefinition<BsonDocument> BuildSort(string sortBy, string sortDir)
    {
        var field = sortBy == "rating" ? "rating" : "createdAt";
        return sortDir == "asc"
            ? Sort.Combine(Sort.Ascending(field), Sort.Ascending("_id"))
            : Sort.Combine(Sort.Descending(field), Sort.Descending("_id"));
    }
}
