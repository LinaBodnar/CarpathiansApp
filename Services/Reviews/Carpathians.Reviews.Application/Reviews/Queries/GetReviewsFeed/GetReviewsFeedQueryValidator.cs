using Carpathians.Reviews.Application.Common.Validation;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewsFeed;

public class GetReviewsFeedQueryValidator : AbstractValidator<GetReviewsFeedQuery>
{
    public GetReviewsFeedQueryValidator()
    {
        RuleFor(x => x.After).MustBeObjectIdIfPresent();
        RuleFor(x => x.Limit).InclusiveBetween(1, GetReviewsFeedQuery.MaxLimit);
        RuleFor(x => x.RouteId).GreaterThan(0).When(x => x.RouteId is not null);
    }
}
