using Carpathians.Reviews.Application.Common.Messaging;
using Carpathians.Reviews.Application.Common.Models;

namespace Carpathians.Reviews.Application.Reviews.Queries.GetReviewsFeed;

/// <summary>
/// Стрічка відгуків з курсорною пагінацією: <paramref name="After"/> — Id останнього
/// відгуку попередньої сторінки (ObjectId зростає разом із часом створення).
/// </summary>
public record GetReviewsFeedQuery(long? RouteId = null, string? After = null, int Limit = 20)
    : IQuery<CursorPage<ReviewDto>>
{
    public const int MaxLimit = 100;
}
