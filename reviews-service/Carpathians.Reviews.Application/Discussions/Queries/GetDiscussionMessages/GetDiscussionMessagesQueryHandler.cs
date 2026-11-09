using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Domain.Exceptions;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionMessages;

public class GetDiscussionMessagesQueryHandler(IDiscussionQueries queries)
    : IRequestHandler<GetDiscussionMessagesQuery, PagedResult<DiscussionMessageDto>>
{
    public async Task<PagedResult<DiscussionMessageDto>> Handle(
        GetDiscussionMessagesQuery q, CancellationToken ct)
    {
        var discussionId = ObjectId.Parse(q.DiscussionId);
        if (await queries.GetByIdAsync(discussionId, ct) is null)
            throw NotFoundException.For("Discussion", q.DiscussionId);

        return await queries.GetMessagesAsync(
            discussionId, Math.Max(q.Page, 1), Math.Clamp(q.PageSize, 1, 100), ct);
    }
}
