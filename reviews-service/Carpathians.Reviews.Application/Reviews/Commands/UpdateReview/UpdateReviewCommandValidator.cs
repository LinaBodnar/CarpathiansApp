using Carpathians.Reviews.Application.Common.Validation;
using Carpathians.Reviews.Domain.Entities;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Commands.UpdateReview;

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Review.MaxTextLength);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
        RuleFor(x => x.Tags!)
            .Must(t => t.Count <= Review.MaxTags)
            .WithMessage($"A review can contain at most {Review.MaxTags} tags")
            .When(x => x.Tags is not null);
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(ValidationRules.MaxTagLength);
    }
}
