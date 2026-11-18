using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Exceptions;

namespace Carpathians.Reviews.Domain.ValueObjects;

/// <summary>
/// Автор відгуку, коментаря чи повідомлення. Вбудовується в документ як піддокумент
/// <c>{ userId, displayName }</c>.
/// <para><c>UserId</c> — логічне посилання на Customers.Id у БД Бронювання (без FK);
/// <c>DisplayName</c> — знімок імені на момент створення (див. політику дублювання).</para>
/// </summary>
public sealed class Author : ValueObject
{
    public const int MaxDisplayNameLength = 100;

    public string UserId { get; }
    public string DisplayName { get; }

    public Author(string userId, string displayName)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("Author userId is required");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new DomainException("Author displayName is required");
        if (displayName.Length > MaxDisplayNameLength)
            throw new DomainException($"Author displayName must not exceed {MaxDisplayNameLength} characters");

        UserId = userId.Trim();
        DisplayName = displayName.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return DisplayName;
    }

    public override string ToString() => $"{DisplayName} ({UserId})";
}
