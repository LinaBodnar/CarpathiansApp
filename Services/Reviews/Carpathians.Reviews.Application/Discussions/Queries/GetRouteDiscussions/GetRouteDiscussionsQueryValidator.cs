using FluentValidation;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetRouteDiscussions;

public class GetRouteDiscussionsQueryValidator : AbstractValidator<GetRouteDiscussionsQuery>
{
    public GetRouteDiscussionsQueryValidator()
    {
        RuleFor(x => x.RouteId).GreaterThan(0);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
