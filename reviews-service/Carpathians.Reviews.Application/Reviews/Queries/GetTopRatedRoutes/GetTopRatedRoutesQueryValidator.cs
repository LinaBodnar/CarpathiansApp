using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetTopRatedRoutes;

public class GetTopRatedRoutesQueryValidator : AbstractValidator<GetTopRatedRoutesQuery>
{
    public GetTopRatedRoutesQueryValidator()
    {
        RuleFor(x => x.Top).InclusiveBetween(1, 50);
        RuleFor(x => x.MinReviews).GreaterThanOrEqualTo(1);
    }
}
