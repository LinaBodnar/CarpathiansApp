using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetTopRatedRoutes;

public record GetTopRatedRoutesQuery(int Top = 5, int MinReviews = 1)
    : IQuery<IReadOnlyList<RouteRatingDto>>;
