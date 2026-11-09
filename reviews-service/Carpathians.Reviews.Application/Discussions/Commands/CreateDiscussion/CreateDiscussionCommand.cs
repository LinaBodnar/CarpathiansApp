using Carpathians.Reviews.Application.Common.Messaging;

namespace Carpathians.Reviews.Application.Discussions.Commands.CreateDiscussion;

/// <summary>
/// Створення обговорення маршруту з першим повідомленням. Якщо передано <paramref name="ReviewId"/>,
/// відгук отримує посилання discussionId на нове обговорення.
/// </summary>
public record CreateDiscussionCommand(
    long RouteId,
    string RouteName,
    string Title,
    string AuthorId,
    string AuthorName,
    string Message,
    string? ReviewId = null) : ICommand<string>;
