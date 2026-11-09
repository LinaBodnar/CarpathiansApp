using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using MediatR;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewsList;

public class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    public Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
    {
        var filter = new ReviewFilter(q.RouteId, q.AuthorId, q.MinRating, q.Tag, q.Search,
            q.SortBy.ToLowerInvariant(), q.SortDir.ToLowerInvariant());

        return queries.GetListAsync(
            filter,
            Math.Max(q.Page, 1),
            Math.Clamp(q.PageSize, 1, GetReviewsListQuery.MaxPageSize),
            ct);
    }
}
