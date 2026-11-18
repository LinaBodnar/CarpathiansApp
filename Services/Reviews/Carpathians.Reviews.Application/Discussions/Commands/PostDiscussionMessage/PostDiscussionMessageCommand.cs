using Carpathians.Reviews.Application.Common.Messaging;

namespace Carpathians.Reviews.Application.Discussions.Commands.PostDiscussionMessage;

/// <summary>Нове повідомлення в обговоренні (необов'язково — відповідь на інше повідомлення).</summary>
public record PostDiscussionMessageCommand(
    string DiscussionId,
    string AuthorId,
    string AuthorName,
    string Text,
    string? ReplyTo = null,
    IReadOnlyList<string>? Attachments = null) : ICommand<string>;
