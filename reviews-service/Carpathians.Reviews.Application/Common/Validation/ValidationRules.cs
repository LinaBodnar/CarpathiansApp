using FluentValidation;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Common.Validation;

/// <summary>Спільні правила валідації, зокрема MongoDB-специфічні (ObjectId).</summary>
public static class ValidationRules
{
    public const int MaxAuthorIdLength = 64;
    public const int MaxAuthorNameLength = 100;
    public const int MaxUrlLength = 500;
    public const int MaxTagLength = 30;

    public static bool BeValidObjectId(string? value)
        => !string.IsNullOrWhiteSpace(value) && ObjectId.TryParse(value, out var id) && id != ObjectId.Empty;

    /// <summary>Рядок має бути коректним 24-символьним hex ObjectId.</summary>
    public static IRuleBuilderOptions<T, string> MustBeObjectId<T>(this IRuleBuilder<T, string> rule)
        => rule.Must(BeValidObjectId).WithMessage("'{PropertyName}' must be a valid ObjectId (24 hex characters)");

    public static IRuleBuilderOptions<T, string?> MustBeObjectIdIfPresent<T>(this IRuleBuilder<T, string?> rule)
        => rule.Must(v => v is null || BeValidObjectId(v))
            .WithMessage("'{PropertyName}' must be a valid ObjectId (24 hex characters)");

    public static void AuthorRules<T>(
        this AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, string>> authorId,
        System.Linq.Expressions.Expression<Func<T, string>> authorName)
    {
        validator.RuleFor(authorId).NotEmpty().MaximumLength(MaxAuthorIdLength);
        validator.RuleFor(authorName).NotEmpty().MaximumLength(MaxAuthorNameLength);
    }
}
