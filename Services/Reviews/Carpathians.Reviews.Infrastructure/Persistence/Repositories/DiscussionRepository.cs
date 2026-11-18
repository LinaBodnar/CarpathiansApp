using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Persistence.Repositories;

public class DiscussionRepository(MongoDbContext context) : IDiscussionRepository
{
    private readonly IMongoCollection<Discussion> _discussions = context.Discussions;
    private readonly IMongoCollection<DiscussionMessage> _messages = context.DiscussionMessages;

    public async Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _discussions.Find(VersionFilter.ById<Discussion>(id)).FirstOrDefaultAsync(ct);

    public async Task<DiscussionMessage?> GetMessageByIdAsync(ObjectId messageId, CancellationToken ct = default)
        => await _messages.Find(VersionFilter.ById<DiscussionMessage>(messageId)).FirstOrDefaultAsync(ct);

    public Task AddAsync(Discussion discussion, CancellationToken ct = default)
        => _discussions.InsertOneAsync(discussion, cancellationToken: ct);

    /// <summary>
    /// Спершу оновлюється документ обговорення з перевіркою версії (конфлікт виявляється
    /// до запису повідомлення), потім вставляється саме повідомлення в discussionMessages.
    /// </summary>
    public async Task AddMessageAsync(Discussion discussion, DiscussionMessage message, CancellationToken ct = default)
    {
        var expectedVersion = discussion.Version;
        var filter = VersionFilter.For<Discussion>(discussion.Id, expectedVersion);
        discussion.BumpVersion();

        var result = await _discussions.ReplaceOneAsync(filter, discussion, cancellationToken: ct);
        if (result.MatchedCount == 0)
        {
            var exists = await _discussions.Find(VersionFilter.ById<Discussion>(discussion.Id)).Limit(1).AnyAsync(ct);
            if (!exists)
                throw NotFoundException.For("Discussion", discussion.Id);
            throw new ConflictException(
                $"Discussion {discussion.Id} was modified by another request (expected version {expectedVersion})");
        }

        await _messages.InsertOneAsync(message, cancellationToken: ct);
    }

    public async Task<long> UpdateRouteNameAsync(long routeId, string routeName, CancellationToken ct = default)
    {
        var filter = Builders<Discussion>.Filter.Eq(d => d.RouteId, routeId)
                     & Builders<Discussion>.Filter.Ne(d => d.RouteName, routeName);
        var update = Builders<Discussion>.Update
            .Set(d => d.RouteName, routeName)
            .Set(d => d.UpdatedAt, DateTime.UtcNow)
            .Inc(VersionFilter.VersionField, 1);

        var result = await _discussions.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount;
    }
}
