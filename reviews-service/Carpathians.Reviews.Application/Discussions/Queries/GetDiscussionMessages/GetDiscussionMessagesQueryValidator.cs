using Carpathians.Reviews.Application.Common.Validation;
using FluentValidation;

namespace Carpathians.Reviews.Application.Discussions.Queries.GetDiscussionMessages;

public class GetDiscussionMessagesQueryValidator : AbstractValidator<GetDiscussionMessagesQuery>
{
    public GetDiscussionMessagesQueryValidator()
    {
        RuleFor(x => x.DiscussionId).MustBeObjectId();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
