using System.Diagnostics;
using Carpathians.Bll.Interfaces;
using Carpathians.Platform.ServiceDefaults.Caching;

namespace Carpathians.Catalog.Api.Caching;

/// <summary>
/// Прогрівання кешу Каталогу: довідник регіонів, популярні складні маршрути та їхні картки
/// завантажуються в L1/L2 під час старту (дані доступні одразу) і далі періодично,
/// щоб найчастіші запити не чекали на базу після закінчення TTL.
/// <para>Налаштування: Caching:Warmup:Enabled, Caching:Warmup:IntervalMinutes.</para>
/// </summary>
public sealed class CatalogCacheWarmupService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CatalogCacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Caching:Warmup:Enabled", true))
        {
            logger.LogInformation("Catalog cache warm-up is disabled");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Caching:Warmup:IntervalMinutes", 10)));
        using var timer = new PeriodicTimer(interval);

        do
        {
            await WarmUpAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task WarmUpAsync(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        using var context = CacheCallContext.Begin(CacheCallContext.Background);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var regions = scope.ServiceProvider.GetRequiredService<IRegionService>();
            var routes = scope.ServiceProvider.GetRequiredService<IRouteService>();      // декоратор з кешем

            var regionList = await regions.GetAllAsync(ct);
            var popular = await routes.GetPopularChallengingAsync(ct);
            foreach (var route in popular)
                await routes.GetByIdAsync(route.Id, ct);                                  // картки «гарячих» маршрутів

            logger.LogInformation(
                "Catalog cache warmed up: {RegionsCount} regions, {RoutesCount} popular routes in {ElapsedMs} ms",
                regionList.Count, popular.Count, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // База ще недоступна — сервіс працює без прогрітого кешу, наступна спроба за розкладом.
            logger.LogWarning(ex, "Catalog cache warm-up failed; next attempt on schedule");
        }
    }
}
