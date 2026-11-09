using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Domain.Exceptions;
using MediatR;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetRouteRating;

public class GetRouteRatingQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetRouteRatingQuery, RouteRatingDto>
{
    public async Task<RouteRatingDto> Handle(GetRouteRatingQuery q, CancellationToken ct)
        => await queries.GetRouteRatingAsync(q.RouteId, ct)
           ?? throw new NotFoundException($"Route {q.RouteId} has no reviews yet");
}
