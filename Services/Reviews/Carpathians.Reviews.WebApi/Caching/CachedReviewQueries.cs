using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.Infrastructure.Persistence.Queries;
using MongoDB.Bson;

namespace Carpathians.Reviews.WebApi.Caching;

/// <summary>
/// Декоратор читальної сторони CQRS (<see cref="IReviewQueries"/>) з кешуванням.
/// Обробники MediatR (Application) про кеш не знають — шар Application лишається чистим.
/// Кешуються найчастіші запити (рейтинг маршруту, свіжі відгуки маршруту, топ маршрутів);
/// довільні фільтри й курсорна стрічка йдуть одразу в MongoDB.
/// </summary>
public sealed class CachedReviewQueries(ReviewQueries inner, ICacheService cache) : IReviewQueries
{
    public Task<ReviewDetailsDto?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => inner.GetByIdAsync(id, ct);

    public async Task<PagedResult<ReviewDto>> GetListAsync(
        ReviewFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        // Кешується лише «відгуки маршруту» без додаткових фільтрів (картка маршруту, Aggregator).
        if (filter.RouteId is not { } routeId || filter.AuthorId is not null || filter.MinRating is not null
            || filter.Tag is not null || filter.Search is not null)
            return await inner.GetListAsync(filter, page, pageSize, ct);

        var version = await cache.GetVersionAsync(ReviewsCacheKeys.RouteVersion(routeId), ReviewsCachePolicies.Version, ct);
        return (await cache.GetOrCreateAsync(
            ReviewsCacheKeys.RouteReviews(routeId, version, filter.SortBy, filter.SortDir, page, pageSize),
            async token => await inner.GetListAsync(filter, page, pageSize, token),
            ReviewsCachePolicies.RouteReviews, ct))!;
    }

    public Task<CursorPage<ReviewDto>> GetFeedAsync(
        long? routeId, ObjectId? after, int limit, CancellationToken ct = default)
        => inner.GetFeedAsync(routeId, after, limit, ct);

    /// <summary>null (маршрут без відгуків) не кешується — перший відгук одразу дасть рейтинг.</summary>
    public Task<RouteRatingDto?> GetRouteRatingAsync(long routeId, CancellationToken ct = default)
        => cache.GetOrCreateAsync(
            ReviewsCacheKeys.RouteRating(routeId),
            token => inner.GetRouteRatingAsync(routeId, token),
            ReviewsCachePolicies.RouteRating, ct);

    public async Task<IReadOnlyList<RouteRatingDto>> GetTopRatedRoutesAsync(
        int top, int minReviews, CancellationToken ct = default)
    {
        var version = await cache.GetVersionAsync(ReviewsCacheKeys.TopRatedVersion, ReviewsCachePolicies.Version, ct);
        return (await cache.GetOrCreateAsync(
            ReviewsCacheKeys.TopRated(version, top, minReviews),
            async token => await inner.GetTopRatedRoutesAsync(top, minReviews, token),
            ReviewsCachePolicies.TopRated, ct))!;
    }
}
