using Carpathians.Reviews.Application.Common.Validation;
using Carpathians.Reviews.Domain.Entities;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Commands.AddReviewComment;

public class AddReviewCommentCommandValidator : AbstractValidator<AddReviewCommentCommand>
{
    public AddReviewCommentCommandValidator()
    {
        RuleFor(x => x.ReviewId).MustBeObjectId();
        this.AuthorRules(x => x.AuthorId, x => x.AuthorName);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Comment.MaxTextLength);
    }
}
