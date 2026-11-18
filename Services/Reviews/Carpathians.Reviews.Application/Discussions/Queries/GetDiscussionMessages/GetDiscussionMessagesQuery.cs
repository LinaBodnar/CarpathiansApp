using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionMessages;

public record GetDiscussionMessagesQuery(string DiscussionId, int Page = 1, int PageSize = 50)
    : IQuery<PagedResult<DiscussionMessageDto>>;
