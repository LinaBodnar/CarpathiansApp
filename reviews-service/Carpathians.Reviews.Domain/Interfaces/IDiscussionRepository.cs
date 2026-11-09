using Carpathians.Reviews.Domain.Entities;
using MongoDB.Bson;

namespace Carpathians.Reviews.Domain.Interfaces;

/// <summary>Репозиторій обговорень і їхніх повідомлень (гібридна модель).</summary>
public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    Task<DiscussionMessage?> GetMessageByIdAsync(ObjectId messageId, CancellationToken ct = default);

    Task AddAsync(Discussion discussion, CancellationToken ct = default);

    /// <summary>
    /// Зберігає нове повідомлення (discussionMessages) і оновлений документ обговорення
    /// (messageCount, lastMessages) з перевіркою версії.
    /// </summary>
    Task AddMessageAsync(Discussion discussion, DiscussionMessage message, CancellationToken ct = default);
}
