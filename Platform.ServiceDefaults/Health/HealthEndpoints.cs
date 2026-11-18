using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Carpathians.Platform.ServiceDefaults.Health;

/// <summary>
/// Теги health checks — однакові в усіх сервісах. За ними перевірки розподіляються між endpoints
/// (Predicate у MapHealthChecks) і фільтруються в gRPC Health Checking Protocol.
/// </summary>
public static class HealthTags
{
    /// <summary>Liveness: лише сам процес, без зовнішніх залежностей (/health/live).</summary>
    public const string Live = "live";

    /// <summary>Readiness: залежності, без яких сервіс не обслуговує трафік (/health/ready).</summary>
    public const string Ready = "ready";

    /// <summary>Startup: застосунок завершив запуск (/health/startup).</summary>
    public const string Startup = "startup";

    public const string Database = "database";
    public const string Cache = "cache";
    public const string Grpc = "grpc";
    public const string Downstream = "downstream";
}

/// <summary>Шляхи health endpoints платформи.</summary>
public static class HealthEndpointPaths
{
    /// <summary>Усі перевірки (його використовує WithHttpHealthCheck в AppHost).</summary>
    public const string All = "/health";

    /// <summary>Liveness — чи живий процес. Збій -> оркестратор перезапускає процес.</summary>
    public const string Live = "/health/live";

    /// <summary>Readiness — чи доступні залежності. Збій -> сервіс тимчасово виключається з балансування.</summary>
    public const string Ready = "/health/ready";

    /// <summary>Startup — чи завершився запуск (для повільного старту: міграції, сидинг, прогрівання кешу).</summary>
    public const string Startup = "/health/startup";

    /// <summary>Liveness шаблону Aspire (лишається для сумісності, те саме, що /health/live).</summary>
    public const string Alive = "/alive";

    /// <summary>Статистика кешу (Лаб. 06).</summary>
    public const string Cache = "/health/cache";
}

/// <summary>
/// Startup-перевірка: Unhealthy, доки хост не завершив запуск (усі IHostedService стартували), далі Healthy.
/// Не звертається до зовнішніх систем, тому виконується миттєво.
/// </summary>
public sealed class StartupHealthCheck(IHostApplicationLifetime lifetime) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(lifetime.ApplicationStarted.IsCancellationRequested
            ? HealthCheckResult.Healthy("Application has started")
            : HealthCheckResult.Unhealthy("Application is still starting"));
}

/// <summary>
/// JSON-відповідь health endpoint з деталями кожної перевірки: статус, опис, тривалість, теги, помилка, дані.
/// <code>
/// { "status": "Degraded", "totalDurationMs": 12.4,
///   "checks": { "redis-cache": { "status": "Degraded", "description": "...", "durationMs": 3001.2,
///               "tags": ["cache","redis","ready"], "error": "...", "data": {} } } }
/// </code>
/// Стек викликів не публікується — лише тип і повідомлення винятку.
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new
        {
            Status = report.Status.ToString(),
            TotalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            Checks = report.Entries.ToDictionary(e => e.Key, e => new
            {
                Status = e.Value.Status.ToString(),
                e.Value.Description,
                DurationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                Tags = e.Value.Tags,
                Error = e.Value.Exception is { } ex ? $"{ex.GetType().Name}: {ex.Message}" : null,
                Data = e.Value.Data.Count > 0 ? e.Value.Data : null
            })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
