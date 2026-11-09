using Carpathians.Reviews.Application.Common.Models;
using MongoDB.Bson;

namespace Carpathians.Reviews.Application.Common.Interfaces;

/// <summary>Параметри фільтрації списку відгуків.</summary>
public record ReviewFilter(
    long? RouteId = null,
    string? AuthorId = null,
    int? MinRating = null,
    string? Tag = null,
    string? Search = null,
    string SortBy = "createdAt",
    string SortDir = "desc");

/// <summary>
/// Читальна сторона CQRS для відгуків. Обробники запитів звертаються до цього інтерфейсу,
/// а реалізація в Infrastructure використовує проєкції та aggregation pipeline MongoDB.
/// </summary>
public interface IReviewQueries
{
    Task<ReviewDetailsDto?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>Фільтрація, сортування й пагінація skip/limit виконуються засобами MongoDB.</summary>
    Task<PagedResult<ReviewDto>> GetListAsync(
        ReviewFilter filter, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Курсорна пагінація: відгуки, створені раніше за документ <paramref name="after"/>.</summary>
    Task<CursorPage<ReviewDto>> GetFeedAsync(
        long? routeId, ObjectId? after, int limit, CancellationToken ct = default);

    /// <summary>Рейтинг маршруту ($group по reviews).</summary>
    Task<RouteRatingDto?> GetRouteRatingAsync(long routeId, CancellationToken ct = default);

    /// <summary>Найкраще оцінені маршрути з мінімальною кількістю відгуків.</summary>
    Task<IReadOnlyList<RouteRatingDto>> GetTopRatedRoutesAsync(
        int top, int minReviews, CancellationToken ct = default);
}
