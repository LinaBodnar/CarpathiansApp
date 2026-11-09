using Carpathians.Reviews.Application.Common.Validation;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Commands.DeleteReview;

public class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
