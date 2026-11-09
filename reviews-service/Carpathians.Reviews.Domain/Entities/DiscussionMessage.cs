using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.ValueObjects;
using MongoDB.Bson;

namespace Carpathians.Reviews.Domain.Entities;

/// <summary>
/// Повідомлення в обговоренні — документ колекції <c>discussionMessages</c>.
/// Повідомлень може бути необмежено багато, тому вони не вбудовуються в обговорення,
/// а посилаються на нього через <see cref="DiscussionId"/> (referencing).
/// </summary>
public class DiscussionMessage : BaseEntity
{
    public const int MaxTextLength = 5000;
    public const int MaxAttachments = 10;

    // поля не readonly: драйвер MongoDB заповнює їх під час десеріалізації (BsonClassMap.MapField)
    private List<string> _attachments = new();

    public ObjectId DiscussionId { get; private set; }
    public Author Author { get; private set; } = null!;
    public string Text { get; private set; } = string.Empty;
    public ObjectId? ReplyTo { get; private set; }

    public IReadOnlyCollection<string> Attachments => _attachments.AsReadOnly();

    private DiscussionMessage() { }                         // для десеріалізації

    /// <summary>Створюється лише через <see cref="Discussion.PostMessage"/>.</summary>
    internal DiscussionMessage(ObjectId discussionId, Author author, string text, ObjectId? replyTo)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Message text is required");
        if (text.Length > MaxTextLength)
            throw new DomainException($"Message text must not exceed {MaxTextLength} characters");

        DiscussionId = discussionId;
        Author = author ?? throw new DomainException("Message author is required");
        Text = text.Trim();
        ReplyTo = replyTo;
    }

    public void AddAttachment(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("Attachment url is required");
        if (_attachments.Count >= MaxAttachments)
            throw new DomainException($"A message can contain at most {MaxAttachments} attachments");
        _attachments.Add(url.Trim());
    }
}
