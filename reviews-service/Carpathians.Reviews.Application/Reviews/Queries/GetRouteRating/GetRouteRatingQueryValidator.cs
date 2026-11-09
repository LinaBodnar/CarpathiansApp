using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetRouteRating;

public class GetRouteRatingQueryValidator : AbstractValidator<GetRouteRatingQuery>
{
    public GetRouteRatingQueryValidator() => RuleFor(x => x.RouteId).GreaterThan(0);
}
