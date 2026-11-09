using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.ValueObjects;

namespace Carpathians.Reviews.Domain.Entities;

/// <summary>
/// Коментар до відгуку. Існує лише всередині агрегата <see cref="Review"/> і зберігається
/// як елемент вбудованого масиву <c>reviews.comments</c> (embedding).
/// </summary>
public class Comment
{
    public const int MaxTextLength = 2000;

    public Author Author { get; private set; } = null!;
    public string Text { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private Comment() { }                                   // для десеріалізації

    internal Comment(Author author, string text, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Comment text is required");
        if (text.Length > MaxTextLength)
            throw new DomainException($"Comment text must not exceed {MaxTextLength} characters");

        Author = author ?? throw new DomainException("Comment author is required");
        Text = text.Trim();
        CreatedAt = createdAt;
    }
}
