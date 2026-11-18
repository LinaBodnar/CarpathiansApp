using Carpathians.Dal.Data;
using Carpathians.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Catalog.Api.Metrics;

/// <summary>Знімок стану каталогу для gauge-метрик.</summary>
public sealed record CatalogStatistics(
    IReadOnlyDictionary<Difficulty, int> RoutesByDifficulty,
    IReadOnlyDictionary<PointType, int> PointsByType);

/// <summary>
/// Останній знімок стану каталогу. Callback gauge синхронний і викликається під час збирання метрик,
/// тому звертатися з нього до БД не можна — значення готує <see cref="CatalogStatisticsRefresher"/>.
/// </summary>
public sealed class CatalogStatisticsSnapshot
{
    private volatile CatalogStatistics? _current;

    public CatalogStatistics? Current => _current;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(CatalogStatistics statistics)
    {
        _current = statistics;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Оновлює знімок кожні <see cref="Interval"/> двома агрегатними запитами EF Core (GROUP BY за індексованими
/// колонками, без відстеження; глобальний фільтр IsDeleted діє). Збій БД не зупиняє сервіс — лишається попередній знімок.
/// </summary>
public sealed class CatalogStatisticsRefresher(
    IServiceScopeFactory scopeFactory,
    CatalogStatisticsSnapshot snapshot,
    ILogger<CatalogStatisticsRefresher> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

            var routes = await db.Routes.AsNoTracking()
                .GroupBy(r => r.Difficulty)
                .Select(g => new { Difficulty = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Difficulty, x => x.Count, ct);
            var points = await db.Points.AsNoTracking()
                .GroupBy(p => p.PointType)
                .Select(g => new { PointType = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PointType, x => x.Count, ct);

            snapshot.Update(new CatalogStatistics(routes, points));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Catalog statistics for gauge metrics were not refreshed, keeping snapshot from {UpdatedAt}",
                snapshot.UpdatedAt);
        }
    }
}
