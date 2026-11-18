using System.ComponentModel.DataAnnotations;

namespace Carpathians.Reviews.WebApi.Contracts;

/// <summary>Нове обговорення маршруту з першим повідомленням.</summary>
public class CreateDiscussionRequest
{
    [Range(1, long.MaxValue)] public long RouteId { get; set; }
    [Required, StringLength(200)] public string RouteName { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(64)] public string AuthorId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AuthorName { get; set; } = string.Empty;
    [Required] public string Message { get; set; } = string.Empty;

    /// <summary>Необов'язково: відгук, до якого прив'язати обговорення (reviews.discussionId).</summary>
    public string? ReviewId { get; set; }
}

public class PostMessageRequest
{
    [Required, StringLength(64)] public string AuthorId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AuthorName { get; set; } = string.Empty;
    [Required] public string Text { get; set; } = string.Empty;

    /// <summary>Необов'язково: Id повідомлення, на яке це відповідь.</summary>
    public string? ReplyTo { get; set; }

    public List<string>? Attachments { get; set; }
}
