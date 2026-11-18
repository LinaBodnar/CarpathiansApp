using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using MediatR;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewsFeed;

public class GetReviewsFeedQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsFeedQuery, CursorPage<ReviewDto>>
{
    public Task<CursorPage<ReviewDto>> Handle(GetReviewsFeedQuery q, CancellationToken ct)
    {
        ObjectId? after = string.IsNullOrWhiteSpace(q.After) ? null : ObjectId.Parse(q.After);
        return queries.GetFeedAsync(q.RouteId, after, Math.Clamp(q.Limit, 1, GetReviewsFeedQuery.MaxLimit), ct);
    }
}
