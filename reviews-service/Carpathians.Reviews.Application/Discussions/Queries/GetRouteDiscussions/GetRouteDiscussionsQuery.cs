using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetRouteDiscussions;

public record GetRouteDiscussionsQuery(long RouteId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<DiscussionDto>>;
