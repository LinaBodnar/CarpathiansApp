using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using MediatR;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetTopRatedRoutes;

public class GetTopRatedRoutesQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetTopRatedRoutesQuery, IReadOnlyList<RouteRatingDto>>
{
    public Task<IReadOnlyList<RouteRatingDto>> Handle(GetTopRatedRoutesQuery q, CancellationToken ct)
        => queries.GetTopRatedRoutesAsync(Math.Clamp(q.Top, 1, 50), Math.Max(q.MinReviews, 1), ct);
}
