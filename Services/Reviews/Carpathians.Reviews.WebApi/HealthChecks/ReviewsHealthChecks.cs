using Carpathians.Platform.ServiceDefaults.Health;
using Carpathians.Reviews.Grpc;
using Carpathians.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Carpathians.Reviews.WebApi.HealthChecks;

/// <summary>
/// Health checks сервісу відгуків (Лаб. 07):
/// <list type="bullet">
/// <item>reviews-db — MongoDB, команда ping до бази відгуків, таймаут 5 с; недоступна база -> Unhealthy;</item>
/// <item>redis-cache — PING до Redis, таймаут 3 с; недоступний кеш -> Degraded: читання йдуть у MongoDB (Частина 1.7);</item>
/// <item>gRPC Health Checking Protocol (grpc.health.v1.Health) — відповідає на основі readiness-перевірок;
/// ним Aggregator перевіряє reviews-api.</item>
/// </list>
/// Усі залежності мають тег ready -> /health/ready; liveness (/health/live) їх не виконує.
/// </summary>
public static class ReviewsHealthChecks
{
    public const string DatabaseCheck = "reviews-db";
    public const string RedisCheck = "redis-cache";

    public static IHostApplicationBuilder AddReviewsHealthChecks(this IHostApplicationBuilder builder)
    {
        // Той самий IMongoClient і та сама база, що й у репозиторіях (MongoDbContext): ping, без читання колекцій.
        var checks = builder.Services.AddHealthChecks()
            .AddMongoDb(
                sp => sp.GetRequiredService<MongoDbContext>().Client,
                sp => sp.GetRequiredService<MongoDbContext>().Database.DatabaseNamespace.DatabaseName,
                name: DatabaseCheck,
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthTags.Database, "mongodb", HealthTags.Ready],
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

        // gRPC Health Checking Protocol: загальний стан ("") і стан сервісу carpathians.reviews.v1.ReviewsGrpc —
        // за readiness-перевірками. Degraded (Redis) -> SERVING, Unhealthy (MongoDB) -> NOT_SERVING.
        builder.Services.AddGrpcHealthChecks(options =>
        {
            options.Services.MapService("", registration => registration.Tags.Contains(HealthTags.Ready));
            options.Services.MapService(ReviewsGrpc.Descriptor.FullName, registration => registration.Tags.Contains(HealthTags.Ready));
        });

        return builder;
    }
}
