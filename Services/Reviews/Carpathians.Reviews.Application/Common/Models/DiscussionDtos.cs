namespace Carpathians.Reviews.Application.Common.Models;

public record MessagePreviewDto(string AuthorId, string AuthorName, string Text, DateTime CreatedAt);

/// <summary>Обговорення з вбудованими останніми повідомленнями (без звернення до discussionMessages).</summary>
public record DiscussionDto(
    string Id,
    long RouteId,
    string RouteName,
    string Title,
    string CreatedById,
    string CreatedByName,
    int MessageCount,
    IReadOnlyList<MessagePreviewDto> LastMessages,
    DateTime CreatedAt,
    DateTime LastActivityAt);

public record DiscussionMessageDto(
    string Id,
    string DiscussionId,
    string AuthorId,
    string AuthorName,
    string Text,
    string? ReplyTo,
    IReadOnlyList<string> Attachments,
    DateTime CreatedAt);
