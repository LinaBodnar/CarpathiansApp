using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Exceptions;

namespace Carpathians.Reviews.Domain.ValueObjects;

/// <summary>
/// Коротка копія повідомлення, вбудована в масив <c>discussions.lastMessages</c>
/// (гібридний підхід: останні повідомлення — у документі обговорення, повна гілка — у discussionMessages).
/// </summary>
public sealed class MessagePreview : ValueObject
{
    public Author Author { get; }
    public string Text { get; }
    public DateTime CreatedAt { get; }

    public MessagePreview(Author author, string text, DateTime createdAt)
    {
        Author = author ?? throw new DomainException("Message author is required");
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Message text is required");
        Text = text;
        CreatedAt = createdAt;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Author;
        yield return Text;
        yield return CreatedAt;
    }
}
