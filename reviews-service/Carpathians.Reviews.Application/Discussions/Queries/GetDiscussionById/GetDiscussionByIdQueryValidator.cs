using Carpathians.Reviews.Application.Common.Validation;
using FluentValidation;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionById;

public class GetDiscussionByIdQueryValidator : AbstractValidator<GetDiscussionByIdQuery>
{
    public GetDiscussionByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}
