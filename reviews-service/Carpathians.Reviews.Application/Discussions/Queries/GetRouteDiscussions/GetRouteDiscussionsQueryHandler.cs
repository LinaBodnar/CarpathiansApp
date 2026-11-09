using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using MediatR;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetRouteDiscussions;

public class GetRouteDiscussionsQueryHandler(IDiscussionQueries queries)
    : IRequestHandler<GetRouteDiscussionsQuery, PagedResult<DiscussionDto>>
{
    public Task<PagedResult<DiscussionDto>> Handle(GetRouteDiscussionsQuery q, CancellationToken ct)
        => queries.GetByRouteAsync(q.RouteId, Math.Max(q.Page, 1), Math.Clamp(q.PageSize, 1, 100), ct);
}
