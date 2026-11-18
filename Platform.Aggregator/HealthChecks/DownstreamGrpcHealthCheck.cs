using Carpathians.Platform.Aggregator.Metrics;
using Carpathians.Platform.Aggregator.Resilience;
using Carpathians.Platform.ServiceDefaults.Health;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Carpathians.Platform.Aggregator.HealthChecks;

/// <summary>
/// Перевірка мікросервісу, від якого залежить Aggregator, стандартним gRPC Health Checking Protocol:
/// унарний виклик grpc.health.v1.Health/Check з коротким deadline (2 с) — сервіс відповідає за власними
/// readiness-перевірками (БД, Redis). Виклик ідемпотентний і не змінює стан сервісу.
/// <para>Статус збою — з політики сервісу (<see cref="DownstreamPolicy.IsCritical"/>): недоступні catalog-api
/// чи booking-api -> Unhealthy (картку не зібрати), reviews-api -> Degraded (Aggregator віддає часткову відповідь).</para>
/// <para>У даних перевірки — gRPC-статус і поточний стан circuit breaker клієнтів сервісу.</para>
/// </summary>
public sealed class DownstreamGrpcHealthCheck(
    GrpcClientFactory clients,
    CircuitBreakerStates circuits,
    ILogger<DownstreamGrpcHealthCheck> logger) : IHealthCheck
{
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Ім'я реєстрації = логічне ім'я сервісу (booking-api, catalog-api, reviews-api) = ім'я gRPC-клієнта Health.
        var service = context.Registration.Name;
        var failureStatus = context.Registration.FailureStatus;
        var data = new Dictionary<string, object>
        {
            ["critical"] = failureStatus == HealthStatus.Unhealthy,
            ["circuit.grpc"] = circuits.Get(service, DownstreamTransport.Grpc).ToString(),
            ["circuit.http"] = circuits.Get(service, DownstreamTransport.Http).ToString()
        };

        try
        {
            var client = clients.CreateClient<Health.HealthClient>(service);
            var reply = await client.CheckAsync(new HealthCheckRequest(),
                deadline: DateTime.UtcNow.Add(Deadline), cancellationToken: cancellationToken);
            data["grpc.serving_status"] = reply.Status.ToString();

            if (reply.Status == HealthCheckResponse.Types.ServingStatus.Serving)
                return HealthCheckResult.Healthy($"{service} is serving", data);

            logger.LogWarning("{Service} health check: serving status {ServingStatus}", service, reply.Status);
            return new HealthCheckResult(failureStatus, $"{service} status: {reply.Status}", data: data);
        }
        catch (RpcException ex)
        {
            data["grpc.status"] = ex.StatusCode.ToString();
            logger.LogWarning(ex, "{Service} health check failed: {GrpcStatus}", service, ex.StatusCode);
            return new HealthCheckResult(failureStatus, $"{service} is unavailable ({ex.StatusCode})", ex, data);
        }
    }
}

/// <summary>Реєстрація перевірок залежностей Aggregator.</summary>
public static class AggregatorHealthChecks
{
    public const string RedisCheck = "redis-cache";

    /// <param name="createHandler">HTTP/2-обробник каналу (той самий, що в робочих gRPC-клієнтів).</param>
    public static IHostApplicationBuilder AddAggregatorHealthChecks(
        this IHostApplicationBuilder builder, Func<HttpMessageHandler> createHandler)
    {
        var checks = builder.Services.AddHealthChecks();

        foreach (var policy in DownstreamPolicies.All)
        {
            // Окремий іменований клієнт grpc.health.v1.Health для кожного сервісу. Без resilience-обробника:
            // перевірка стану не повторюється й не впливає на circuit breaker робочих клієнтів — лише deadline 2 с.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers позначено як experimental у Microsoft.Extensions.Http.Resilience
            builder.Services.AddGrpcClient<Health.HealthClient>(policy.Service,
                    o => o.Address = new Uri($"https://{policy.Service}"))
                .ConfigurePrimaryHttpMessageHandler(createHandler)
                .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

            checks.AddCheck<DownstreamGrpcHealthCheck>(
                policy.Service,
                failureStatus: policy.IsCritical ? HealthStatus.Unhealthy : HealthStatus.Degraded,
                tags: [HealthTags.Grpc, HealthTags.Downstream, HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(3));
        }

        // Redis — кеш агрегованих відповідей: недоступний -> Degraded (Aggregator збирає відповіді заново).
        if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
        {
            checks.AddRedis(
                sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                name: RedisCheck,
                failureStatus: HealthStatus.Degraded,
                tags: [HealthTags.Cache, "redis", HealthTags.Ready],
                timeout: TimeSpan.FromSeconds(3));
        }

        return builder;
    }
}
