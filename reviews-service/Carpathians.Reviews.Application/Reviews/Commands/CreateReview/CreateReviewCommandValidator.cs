using Carpathians.Reviews.Application.Common.Validation;
using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Interfaces;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.RouteId).GreaterThan(0);
        RuleFor(x => x.RouteName).NotEmpty().MaximumLength(Review.MaxRouteNameLength);
        this.AuthorRules(x => x.AuthorId, x => x.AuthorName);
        RuleFor(x => x.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(Review.MaxTextLength);

        RuleFor(x => x.Photos!)
            .Must(p => p.Count <= Review.MaxPhotos)
            .WithMessage($"A review can contain at most {Review.MaxPhotos} photos")
            .When(x => x.Photos is not null);
        RuleForEach(x => x.Photos).NotEmpty().MaximumLength(ValidationRules.MaxUrlLength);

        RuleFor(x => x.Tags!)
            .Must(t => t.Count <= Review.MaxTags)
            .WithMessage($"A review can contain at most {Review.MaxTags} tags")
            .When(x => x.Tags is not null);
        RuleForEach(x => x.Tags)
            .NotEmpty()
            .MaximumLength(ValidationRules.MaxTagLength)
            .Matches(@"^[\p{L}\p{N}_-]+$").WithMessage("Tag may contain only letters, digits, '-' and '_'");

        RuleFor(x => x.GpsTrack!)
            .Must(t => t.Count is 0 or >= 2 && t.Count <= Review.MaxGpsPoints)
            .WithMessage($"GPS track must contain from 2 to {Review.MaxGpsPoints} points")
            .When(x => x.GpsTrack is not null);
        RuleForEach(x => x.GpsTrack).ChildRules(point =>
        {
            point.RuleFor(p => p.Lat).InclusiveBetween(-90, 90);
            point.RuleFor(p => p.Lng).InclusiveBetween(-180, 180);
        });

        When(x => x.Weather is not null, () =>
        {
            RuleFor(x => x.Weather!.TempC).InclusiveBetween(-60, 60).When(x => x.Weather!.TempC is not null);
            RuleFor(x => x.Weather!)
                .Must(w => w.TempC is not null || !string.IsNullOrWhiteSpace(w.Wind)
                                               || !string.IsNullOrWhiteSpace(w.Conditions))
                .WithMessage("Weather must contain at least one value");
        });

        // Асинхронна валідація із запитом до MongoDB: один відгук від автора на маршрут.
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await reviews.ExistsAsync(cmd.RouteId, cmd.AuthorId, ct))
            .OverridePropertyName("AuthorId")
            .WithMessage("You have already reviewed this route")
            .When(x => x.RouteId > 0 && !string.IsNullOrWhiteSpace(x.AuthorId));
    }
}
