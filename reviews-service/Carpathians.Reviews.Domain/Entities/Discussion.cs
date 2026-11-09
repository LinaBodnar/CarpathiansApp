using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.ValueObjects;

namespace Carpathians.Reviews.Domain.Entities;

/// <summary>
/// Обговорення маршруту — документ колекції <c>discussions</c>.
/// Гібридний підхід: кілька останніх повідомлень вбудовано в <see cref="LastMessages"/>
/// для швидкого читання, а повна (необмежена) гілка зберігається окремими документами
/// <see cref="DiscussionMessage"/> з посиланням <c>discussionId</c>.
/// </summary>
public class Discussion : BaseEntity
{
    public const int MaxTitleLength = 200;
    public const int LastMessagesLimit = 3;

    private readonly List<MessagePreview> _lastMessages = new();

    public long RouteId { get; private set; }
    public string RouteName { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public Author CreatedBy { get; private set; } = null!;
    public DateTime LastActivityAt { get; private set; }
    public int MessageCount { get; private set; }

    public IReadOnlyCollection<MessagePreview> LastMessages => _lastMessages.AsReadOnly();

    private Discussion() { }                                // для десеріалізації

    public Discussion(long routeId, string routeName, string title, Author createdBy)
    {
        if (routeId <= 0)
            throw new DomainException("RouteId must be positive");
        if (string.IsNullOrWhiteSpace(routeName))
            throw new DomainException("RouteName is required");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Discussion title is required");
        if (title.Length > MaxTitleLength)
            throw new DomainException($"Discussion title must not exceed {MaxTitleLength} characters");

        RouteId = routeId;
        RouteName = routeName.Trim();
        Title = title.Trim();
        CreatedBy = createdBy ?? throw new DomainException("Discussion author is required");
        LastActivityAt = CreatedAt;
    }

    /// <summary>
    /// Створює нове повідомлення в обговоренні й оновлює лічильник та вбудовані
    /// останні повідомлення. Саме повідомлення зберігається окремим документом.
    /// </summary>
    public DiscussionMessage PostMessage(Author author, string text, DiscussionMessage? replyTo = null)
    {
        if (replyTo is not null && replyTo.DiscussionId != Id)
            throw new DomainException("Reply must belong to the same discussion");

        var message = new DiscussionMessage(Id, author, text, replyTo?.Id);

        MessageCount++;
        LastActivityAt = message.CreatedAt;
        _lastMessages.Add(new MessagePreview(message.Author, message.Text, message.CreatedAt));
        if (_lastMessages.Count > LastMessagesLimit)
            _lastMessages.RemoveRange(0, _lastMessages.Count - LastMessagesLimit);

        Touch();
        return message;
    }

    /// <summary>Оновлення денормалізованої назви маршруту за подією з Каталогу.</summary>
    public void RenameRoute(string routeName)
    {
        if (string.IsNullOrWhiteSpace(routeName))
            throw new DomainException("RouteName is required");
        RouteName = routeName.Trim();
        Touch();
    }
}
