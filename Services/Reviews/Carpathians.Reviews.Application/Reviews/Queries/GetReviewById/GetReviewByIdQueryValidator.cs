using Carpathians.Reviews.Application.Common.Validation;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewById;

public class GetReviewByIdQueryValidator : AbstractValidator<GetReviewByIdQuery>
{
    public GetReviewByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
