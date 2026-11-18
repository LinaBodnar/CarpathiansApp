using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Persistence.Repositories;

public class ReviewRepository(MongoDbContext context) : IReviewRepository
{
    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    public async Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _reviews.Find(VersionFilter.ById<Review>(id)).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(long routeId, string authorId, CancellationToken ct = default)
    {
        var filter = Builders<Review>.Filter.Eq(r => r.RouteId, routeId)
                     & Builders<Review>.Filter.Eq("author.userId", authorId);   // ux_reviews_route_author
        return _reviews.Find(filter).Limit(1).AnyAsync(ct);
    }

    public Task AddAsync(Review review, CancellationToken ct = default)
        => _reviews.InsertOneAsync(review, cancellationToken: ct);

    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        var expectedVersion = review.Version;
        var filter = VersionFilter.For<Review>(review.Id, expectedVersion);
        review.BumpVersion();

        var result = await _reviews.ReplaceOneAsync(filter, review, cancellationToken: ct);
        if (result.MatchedCount == 0)
            await ThrowNotFoundOrConflictAsync(review.Id, expectedVersion, ct);
    }

    public async Task<bool> DeleteAsync(ObjectId id, int? expectedVersion, CancellationToken ct = default)
    {
        var filter = expectedVersion is null
            ? VersionFilter.ById<Review>(id)
            : VersionFilter.For<Review>(id, expectedVersion.Value);

        var result = await _reviews.DeleteOneAsync(filter, ct);
        if (result.DeletedCount > 0)
            return true;

        if (expectedVersion is not null && await ExistsByIdAsync(id, ct))
            throw new ConflictException($"Review {id} was modified: expected version {expectedVersion}");
        return false;
    }

    public async Task<long> UpdateRouteNameAsync(long routeId, string routeName, CancellationToken ct = default)
    {
        var filter = Builders<Review>.Filter.Eq(r => r.RouteId, routeId)
                     & Builders<Review>.Filter.Ne(r => r.RouteName, routeName);
        var update = Builders<Review>.Update
            .Set(r => r.RouteName, routeName)
            .Set(r => r.UpdatedAt, DateTime.UtcNow)
            .Inc(VersionFilter.VersionField, 1);

        var result = await _reviews.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount;
    }

    private Task<bool> ExistsByIdAsync(ObjectId id, CancellationToken ct)
        => _reviews.Find(VersionFilter.ById<Review>(id)).Limit(1).AnyAsync(ct);

    private async Task ThrowNotFoundOrConflictAsync(ObjectId id, int expectedVersion, CancellationToken ct)
    {
        if (!await ExistsByIdAsync(id, ct))
            throw NotFoundException.For("Review", id);
        throw new ConflictException(
            $"Review {id} was modified by another request (expected version {expectedVersion})");
    }
}
