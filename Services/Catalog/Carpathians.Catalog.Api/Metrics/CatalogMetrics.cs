using System.Diagnostics;
using System.Diagnostics.Metrics;
using Carpathians.Domain.Enums;
using Carpathians.Platform.ServiceDefaults.Metrics;

namespace Carpathians.Catalog.Api.Metrics;

/// <summary>
/// Бізнес-метрики каталогу маршрутів (Meter "Catalog.Api", OpenTelemetry -> Aspire Dashboard).
/// <list type="bullet">
/// <item>лічильники: routes.created / updated / deleted (тег route.difficulty), routes.searches (тег search.type),
/// points.created (тег point.type);</item>
/// <item>catalog.operation.duration — тривалість операцій каталогу, успішних і невдалих (operation, status, error.type);</item>
/// <item>gauge: routes.active за складністю, points.active за типом точки — знімок стану каталогу.</item>
/// </list>
/// Значення тегів — переліки (Difficulty, PointType, тип пошуку), тож кількість часових рядів обмежена.
/// </summary>
public sealed class CatalogMetrics
{
    public const string MeterName = BusinessMeters.Catalog;

    /// <summary>Значення тегу operation.</summary>
    public static class Operations
    {
        public const string RouteGet = "route.get";
        public const string RouteSearch = "route.search";
        public const string RouteCreate = "route.create";
        public const string RouteUpdate = "route.update";
        public const string RouteDelete = "route.delete";
        public const string PointGet = "point.get";
        public const string PointCreate = "point.create";
        public const string PointUpdate = "point.update";
        public const string PointDelete = "point.delete";
    }

    /// <summary>Значення тегу search.type.</summary>
    public static class SearchTypes
    {
        public const string All = "all";
        public const string Paged = "paged";
        public const string PopularChallenging = "popular_challenging";
    }

    private const string DifficultyTag = "route.difficulty";
    private const string PointTypeTag = "point.type";
    private const string SearchTypeTag = "search.type";

    private readonly Counter<long> _routesCreated;
    private readonly Counter<long> _routesUpdated;
    private readonly Counter<long> _routesDeleted;
    private readonly Counter<long> _searches;
    private readonly Counter<long> _pointsCreated;
    private readonly Histogram<double> _duration;

    public CatalogMetrics(IMeterFactory meterFactory, CatalogStatisticsSnapshot snapshot)
    {
        var meter = meterFactory.Create(MeterName);

        _routesCreated = meter.CreateCounter<long>("routes.created", "{route}", "Created hiking routes");
        _routesUpdated = meter.CreateCounter<long>("routes.updated", "{route}", "Updated hiking routes");
        _routesDeleted = meter.CreateCounter<long>("routes.deleted", "{route}", "Deleted hiking routes");
        _searches = meter.CreateCounter<long>("routes.searches", "{search}", "Route catalog searches by type");
        _pointsCreated = meter.CreateCounter<long>("points.created", "{point}", "Created map points");

        _duration = meter.CreateHistogram<double>("catalog.operation.duration", "ms",
            "Duration of catalog operations (successful and failed)");

        meter.CreateObservableGauge("routes.active", () => ObserveRoutes(snapshot), "{route}",
            "Current number of routes in the catalog by difficulty");
        meter.CreateObservableGauge("points.active", () => ObservePoints(snapshot), "{point}",
            "Current number of map points by type");
    }

    public void RouteCreated(Difficulty difficulty) => _routesCreated.Add(1, DifficultyTagValue(difficulty));

    public void RouteUpdated(Difficulty difficulty) => _routesUpdated.Add(1, DifficultyTagValue(difficulty));

    public void RouteDeleted() => _routesDeleted.Add(1);

    public void RoutesSearched(string searchType) => _searches.Add(1, new KeyValuePair<string, object?>(SearchTypeTag, searchType));

    public void PointCreated(PointType pointType) => _pointsCreated.Add(1, PointTypeTagValue(pointType));

    /// <summary>Виконує операцію й записує тривалість для успіху й для збою; виняток прокидається далі.</summary>
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await action();
            RecordDuration(operation, started, null);
            return result;
        }
        catch (Exception ex)
        {
            RecordDuration(operation, started, ex);
            throw;
        }
    }

    public Task MeasureAsync(string operation, Func<Task> action)
        => MeasureAsync(operation, async () =>
        {
            await action();
            return true;
        });

    private void RecordDuration(string operation, long started, Exception? error)
        => _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, MetricTags.ForOperation(operation, error));

    private static IEnumerable<Measurement<int>> ObserveRoutes(CatalogStatisticsSnapshot snapshot)
    {
        if (snapshot.Current is not { } stats)
            yield break;

        foreach (var difficulty in Enum.GetValues<Difficulty>())
            yield return new Measurement<int>(stats.RoutesByDifficulty.GetValueOrDefault(difficulty), DifficultyTagValue(difficulty));
    }

    private static IEnumerable<Measurement<int>> ObservePoints(CatalogStatisticsSnapshot snapshot)
    {
        if (snapshot.Current is not { } stats)
            yield break;

        foreach (var type in Enum.GetValues<PointType>())
            yield return new Measurement<int>(stats.PointsByType.GetValueOrDefault(type), PointTypeTagValue(type));
    }

    private static KeyValuePair<string, object?> DifficultyTagValue(Difficulty difficulty) => new(DifficultyTag, difficulty.ToString());

    private static KeyValuePair<string, object?> PointTypeTagValue(PointType type) => new(PointTypeTag, type.ToString());
}
