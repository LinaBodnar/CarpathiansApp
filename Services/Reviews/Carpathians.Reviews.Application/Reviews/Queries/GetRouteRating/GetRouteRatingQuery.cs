using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetRouteRating;

public record GetRouteRatingQuery(long RouteId) : IQuery<RouteRatingDto>;
