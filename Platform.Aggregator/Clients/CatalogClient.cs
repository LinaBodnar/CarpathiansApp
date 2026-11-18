using Carpathians.Platform.Aggregator.Contracts;

namespace Carpathians.Platform.Aggregator.Clients;

/// <summary>Typed HttpClient для catalog-api (актуальні дані маршрутів).</summary>
public class CatalogClient(HttpClient http, ILogger<CatalogClient> logger)
    : ServiceClientBase(http, logger, "catalog-api")
{
    public Task<RouteResponse?> GetRouteAsync(long routeId, CancellationToken ct)
        => GetOrNullAsync<RouteResponse>($"/api/routes/{routeId}", ct);
}
