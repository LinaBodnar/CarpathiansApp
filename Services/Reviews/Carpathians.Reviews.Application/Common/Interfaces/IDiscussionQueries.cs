using Carpathians.Reviews.Application.Common.Models;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Common.Interfaces;

/// <summary>Читальна сторона CQRS для обговорень.</summary>
public interface IDiscussionQueries
{
    Task<DiscussionDto?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>Обговорення маршруту, найактивніші першими (ix_discussions_route_activity).</summary>
    Task<PagedResult<DiscussionDto>> GetByRouteAsync(
        long routeId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Повна гілка повідомлень з пагінацією (ix_messages_discussion_created).</summary>
    Task<PagedResult<DiscussionMessageDto>> GetMessagesAsync(
        ObjectId discussionId, int page, int pageSize, CancellationToken ct = default);
}
