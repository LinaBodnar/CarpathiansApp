using Carpathians.Catalog.Grpc;
using Carpathians.Platform.ServiceDefaults.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Carpathians.Catalog.Api.HealthChecks;

/// <summary>
/// Health checks каталогу маршрутів (Лаб. 07):
/// <list type="bullet">
/// <item>catalog-db — SQL Server (EF Core), найпростіший запит SELECT 1, таймаут 5 с; недоступна БД -> Unhealthy (сервіс не готовий);</item>
/// <item>redis-cache — PING до Redis, таймаут 3 с; недоступний кеш -> Degraded: сервіс відповідає з L1 і БД (Частина 1.7);</item>
/// <item>gRPC Health Checking Protocol (grpc.health.v1.Health) — відповідає на основі readiness-перевірок;
/// ним Aggregator перевіряє catalog-api.</item>
/// </list>
/// Усі залежності мають тег ready -> /health/ready; liveness (/health/live) їх не виконує.
/// </summary>
public static class CatalogHealthChecks
{
    public const string DatabaseCheck = "catalog-db";
    public const string RedisCheck = "redis-cache";

    public static IHostApplicationBuilder AddCatalogHealthChecks(this IHostApplicationBuilder builder, string connectionString)
    {
        var checks = builder.Services.AddHealthChecks()
            .AddSqlServer(
                connectionString: connectionString,
                healthQuery: "SELECT 1;",
                name: DatabaseCheck,
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthTags.Database, "sql", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(5));

        // Redis підключається лише під AppHost (ConnectionStrings:redis); IConnectionMultiplexer реєструє
        // Aspire-інтеграція AddRedisDistributedCache — перевірка використовує те саме з'єднання.
        if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
        {
            checks.AddRedis(
                sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                name: RedisCheck,
                failureStatus: HealthStatus.Degraded,            // кеш необов'язковий
                tags: [HealthTags.Cache, "redis", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(3));
        }

        // gRPC Health Checking Protocol: загальний стан ("") і стан сервісу carpathians.catalog.v1.CatalogGrpc —
        // за readiness-перевірками. Degraded (Redis) -> SERVING, Unhealthy (БД) -> NOT_SERVING.
        builder.Services.AddGrpcHealthChecks(options =>
        {
            options.Services.MapService("", registration => registration.Tags.Contains(HealthTags.Ready));
            options.Services.MapService(CatalogGrpc.Descriptor.FullName, registration => registration.Tags.Contains(HealthTags.Ready));
        });

        return builder;
    }
}
