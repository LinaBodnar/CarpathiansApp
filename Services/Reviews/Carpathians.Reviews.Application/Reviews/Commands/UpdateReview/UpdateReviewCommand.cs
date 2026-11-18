using Carpathians.Reviews.Application.Common.Messaging;

namespace Carpathians.Reviews.Application.Reviews.Commands.UpdateReview;

/// <summary>
/// Редагування відгуку. <paramref name="ExpectedVersion"/> — версія, яку бачив клієнт
/// (заголовок If-Match); якщо документ уже змінено — конфлікт 409. Повертає нову версію.
/// </summary>
public record UpdateReviewCommand(
    string Id,
    int Rating,
    string Text,
    IReadOnlyList<string>? Tags = null,
    int? ExpectedVersion = null) : ICommand<int>;
