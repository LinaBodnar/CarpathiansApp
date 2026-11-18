using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Persistence.Queries;

/// <summary>Читальна сторона CQRS для обговорень (гібридна модель discussions + discussionMessages).</summary>
public class DiscussionQueries(MongoDbContext context) : IDiscussionQueries
{
    private readonly IMongoCollection<BsonDocument> _discussions = context.DiscussionDocuments;
    private readonly IMongoCollection<BsonDocument> _messages = context.DiscussionMessageDocuments;
    private static readonly FilterDefinitionBuilder<BsonDocument> Filter = Builders<BsonDocument>.Filter;
    private static readonly SortDefinitionBuilder<BsonDocument> Sort = Builders<BsonDocument>.Sort;

    public async Task<DiscussionDto?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
    {
        var document = await _discussions.Find(Filter.Eq("_id", id)).FirstOrDefaultAsync(ct);
        return document is null ? null : ReviewReadMapper.ToDiscussionDto(document);
    }

    /// <summary>Короткі дані з lastMessages — без звернення до колекції discussionMessages.</summary>
    public async Task<PagedResult<DiscussionDto>> GetByRouteAsync(
        long routeId, int page, int pageSize, CancellationToken ct = default)
    {
        var filter = Filter.Eq("routeId", routeId);
        var total = await _discussions.CountDocumentsAsync(filter, cancellationToken: ct);
        var documents = await _discussions.Find(filter)
            .Sort(Sort.Descending("lastActivityAt"))                 // ix_discussions_route_activity
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DiscussionDto>(
            documents.Select(ReviewReadMapper.ToDiscussionDto).ToList(), total, page, pageSize);
    }

    /// <summary>Повна гілка повідомлень у хронологічному порядку.</summary>
    public async Task<PagedResult<DiscussionMessageDto>> GetMessagesAsync(
        ObjectId discussionId, int page, int pageSize, CancellationToken ct = default)
    {
        var filter = Filter.Eq("discussionId", discussionId);
        var total = await _messages.CountDocumentsAsync(filter, cancellationToken: ct);
        var documents = await _messages.Find(filter)
            .Sort(Sort.Combine(Sort.Ascending("createdAt"), Sort.Ascending("_id")))   // ix_messages_discussion_created
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return new PagedResult<DiscussionMessageDto>(
            documents.Select(ReviewReadMapper.ToDiscussionMessageDto).ToList(), total, page, pageSize);
    }
}
