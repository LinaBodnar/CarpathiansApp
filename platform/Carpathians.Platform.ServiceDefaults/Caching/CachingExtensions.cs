using Carpathians.Platform.ServiceDefaults.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting;

public static class CachingExtensions
{
    /// <summary>Розмір кешу в пам'яті за замовчуванням: 50 МБ (одиниця Size = 1 КБ).</summary>
    private const long DefaultMemorySizeLimitKb = 50 * 1024;

    /// <summary>
    /// Кеш платформи: IMemoryCache з обмеженням розміру й політиками витіснення,
    /// <see cref="ICacheService"/> (cache-aside), метрики й health check кешу.
    /// <para>Ліміт: Caching:Memory:SizeLimitKb в appsettings.json.</para>
    /// </summary>
    public static TBuilder AddPlatformCaching<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var sizeLimitKb = builder.Configuration.GetValue("Caching:Memory:SizeLimitKb", DefaultMemorySizeLimitKb);

        builder.Services.AddMemoryCache(options =>
        {
            options.SizeLimit = sizeLimitKb;                          // кеш не «з'їсть» усю пам'ять процесу
            options.CompactionPercentage = 0.25;                      // при переповненні звільняється 25 %
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(1);
            options.TrackStatistics = true;                           // статистика для метрик і health check
        });

        builder.Services.TryAddSingleton<CacheMetrics>();
        builder.Services.TryAddSingleton<ICacheService, CacheService>();

        builder.Services.AddHealthChecks()
            .AddCheck<CacheHealthCheck>("cache", tags: new[] { "cache" });

        return builder;
    }
}
