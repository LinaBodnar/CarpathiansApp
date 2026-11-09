using Carpathians.Reviews.Application.Common.Validation;
using Carpathians.Reviews.Domain.Entities;
using FluentValidation;

namespace Carpathians.Reviews.Application.Discussions.Commands.CreateDiscussion;

public class CreateDiscussionCommandValidator : AbstractValidator<CreateDiscussionCommand>
{
    public CreateDiscussionCommandValidator()
    {
        RuleFor(x => x.RouteId).GreaterThan(0);
        RuleFor(x => x.RouteName).NotEmpty().MaximumLength(Review.MaxRouteNameLength);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Discussion.MaxTitleLength);
        this.AuthorRules(x => x.AuthorId, x => x.AuthorName);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(DiscussionMessage.MaxTextLength);
        RuleFor(x => x.ReviewId).MustBeObjectIdIfPresent();
    }
}
