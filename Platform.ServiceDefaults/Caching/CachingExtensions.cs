using Carpathians.Platform.ServiceDefaults.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

public static class CachingExtensions
{
    /// <summary>Розмір кешу в пам'яті за замовчуванням: 50 МБ (одиниця Size = 1 КБ).</summary>
    private const long DefaultMemorySizeLimitKb = 50 * 1024;

    /// <summary>
    /// Кеш платформи: IMemoryCache (L1) з обмеженням розміру й політиками витіснення,
    /// <see cref="ICacheService"/> (cache-aside L1 -> L2 -> база), запобіжник для Redis, метрики й health check.
    /// <para>Рівень L2 — IDistributedCache, зареєстрований сервісом до виклику
    /// (<c>builder.AddRedisDistributedCache("redis")</c>). Без нього кеш працює лише в пам'яті.</para>
    /// <para>Ліміт пам'яті: Caching:Memory:SizeLimitKb в appsettings.json.</para>
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

        builder.Services.TryAddSingleton<DistributedCacheCircuit>();
        builder.Services.TryAddSingleton<CacheMetrics>();
        builder.Services.TryAddSingleton<ICacheService>(sp => new CacheService(
            sp.GetRequiredService<IMemoryCache>(),
            sp.GetRequiredService<CacheMetrics>(),
            sp.GetRequiredService<DistributedCacheCircuit>(),
            sp.GetRequiredService<ILogger<CacheService>>(),
            sp.GetService<IDistributedCache>()));                     // Redis (L2), якщо підключено

        builder.Services.AddHealthChecks()
            .AddCheck<CacheHealthCheck>("cache", tags: new[] { "cache" });

        return builder;
    }
}
