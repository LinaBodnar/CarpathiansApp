using Carpathians.Reviews.Domain.Entities;
using FluentValidation;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewsList;

public class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    /// <summary>Сортування дозволене лише за полями, під які є індекси.</summary>
    public static readonly string[] AllowedSortFields = { "createdAt", "rating" };
    private static readonly string[] AllowedSortDirections = { "asc", "desc" };

    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetReviewsListQuery.MaxPageSize);
        RuleFor(x => x.RouteId).GreaterThan(0).When(x => x.RouteId is not null);
        RuleFor(x => x.MinRating).InclusiveBetween(Review.MinRating, Review.MaxRating)
            .When(x => x.MinRating is not null);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Tag).MaximumLength(30);
        RuleFor(x => x.SortBy)
            .Must(s => AllowedSortFields.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", AllowedSortFields)}");
        RuleFor(x => x.SortDir)
            .Must(s => AllowedSortDirections.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortDir must be 'asc' or 'desc'");
    }
}
