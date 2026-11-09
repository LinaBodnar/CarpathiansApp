using Carpathians.Platform.Aggregator.Contracts;

namespace Carpathians.Platform.Aggregator.Clients;

/// <summary>Typed HttpClient для reviews-api (рейтинги й відгуки маршрутів).</summary>
public class ReviewsClient(HttpClient http, ILogger<ReviewsClient> logger)
    : ServiceClientBase(http, logger, "reviews-api")
{
    /// <summary>null — маршрут ще не має відгуків (reviews-api повертає 404).</summary>
    public Task<RouteRatingResponse?> GetRouteRatingAsync(long routeId, CancellationToken ct)
        => GetOrNullAsync<RouteRatingResponse>($"/api/routes/{routeId}/rating", ct);

    public async Task<IReadOnlyList<ReviewResponse>?> GetLatestReviewsAsync(long routeId, int count, CancellationToken ct)
    {
        var page = await GetOrNullAsync<PagedResponse<ReviewResponse>>(
            $"/api/reviews?routeId={routeId}&sortBy=createdAt&sortDir=desc&page=1&pageSize={count}", ct);
        return page?.Items;
    }
}
