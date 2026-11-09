using System.Text.Json;
using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Platform.ServiceDefaults.Correlation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Спільні налаштування для всіх сервісів платформи Carpathians: Serilog (структуроване логування),
/// OpenTelemetry (трасування HTTP, SQL Server, MongoDB; метрики), CorrelationId, health checks,
/// service discovery і resilience для HttpClient.
/// <para>У сервісі: <c>builder.AddServiceDefaults()</c> на початку конфігурації,
/// <c>app.UseCorrelationId()</c> одразу після <c>Build()</c> і <c>app.MapDefaultEndpoints()</c>.</para>
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string CacheHealthEndpointPath = "/health/cache";

    /// <summary>ActivitySource драйвера MongoDB (пакет MongoDB.Driver.Core.Extensions.DiagnosticSources).</summary>
    public const string MongoDbActivitySource = "MongoDB.Driver.Core.Extensions.DiagnosticSources";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddSerilogLogging();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();                      // детально — Лаб. 07
            http.AddServiceDiscovery();                               // http://booking-api -> реальна адреса
            http.AddHttpMessageHandler<CorrelationIdHandler>();       // CorrelationId у вихідні запити
        });

        return builder;
    }

    /// <summary>Логічне ім'я сервісу: OTEL_SERVICE_NAME від Aspire (booking-api) або ім'я збірки.</summary>
    public static string GetServiceName(this IHostApplicationBuilder builder)
        => builder.Configuration["OTEL_SERVICE_NAME"] is { Length: > 0 } name
            ? name
            : builder.Environment.ApplicationName;

    /// <summary>
    /// Serilog — основний провайдер логування. Кожен запис — об'єкт з полями (CompactJson у консолі).
    /// writeToProviders: true передає записи далі провайдеру OpenTelemetry, тож вони потрапляють
    /// у вкладку Structured logs Aspire Dashboard (OTLP) з TraceId/SpanId.
    /// </summary>
    public static TBuilder AddSerilogLogging<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.GetServiceName();
        var minimumLevel = builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information;

        builder.Services.AddSerilog((_, cfg) => cfg
                .MinimumLevel.Is(minimumLevel)
                // фільтрація шумних логів інфраструктури
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .MinimumLevel.Override("Polly", LogEventLevel.Warning)
                .MinimumLevel.Override("Yarp", LogEventLevel.Information)
                .Enrich.FromLogContext()                              // CorrelationId з middleware
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console(new CompactJsonFormatter()),         // JSON у Console logs Dashboard
            writeToProviders: true);                                  // -> OpenTelemetry -> Structured logs

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Залишаємо лише провайдер OpenTelemetry: консоль пише Serilog, без дублювання.
        builder.Logging.ClearProviders();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var serviceName = builder.GetServiceName();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", builder.Environment.EnvironmentName),
                new("service.logical_name", serviceName)
            }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(CacheMetrics.MeterName)                     // кеш: cache.hits, cache.misses, ... (Лаб. 06)
                .AddMeter("Carpathians.*"))                         // інші власні метрики (Gateway)
            .WithTracing(tracing => tracing
                .SetSampler(CreateSampler(builder))
                .AddSource(builder.Environment.ApplicationName)       // власні spans сервісу (Activity API)
                .AddSource("Carpathians.*")
                .AddSource(MongoDbActivitySource)                     // MongoDB (Лаб. 04)
                .AddAspNetCoreInstrumentation(o => o.Filter = context =>
                    !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                    && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                .AddHttpClientInstrumentation()                       // вихідні виклики (Aggregator, YARP)
                .AddSqlClientInstrumentation());                      // Dapper (Лаб. 02) і EF Core (Лаб. 03)

        builder.AddOpenTelemetryExporters();
        return builder;
    }

    /// <summary>100 % трасувань у Development; у Production — частка з OpenTelemetry:TracesSamplingRatio.</summary>
    private static Sampler CreateSampler(IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
            return new AlwaysOnSampler();

        var ratio = builder.Configuration.GetValue("OpenTelemetry:TracesSamplingRatio", 0.25);
        return new ParentBasedSampler(new TraceIdRatioBasedSampler(Math.Clamp(ratio, 0, 1)));
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Адресу OTLP-колектора (Aspire Dashboard) передає AppHost; експорт батчевий і не блокує запити.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), new[] { "live" });

        return builder;
    }

    /// <summary>CorrelationId middleware — реєструвати одразу після Build(), до інших middleware.</summary>
    public static WebApplication UseCorrelationId(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // /health — усі перевірки (готовність, у т. ч. БД); /alive — лише «процес живий»
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        // /health/cache — статистика кешу (Лаб. 06) у JSON: записи, розмір, попадання/промахи
        app.MapHealthChecks(CacheHealthEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("cache"),
            ResponseWriter = WriteHealthDetailsAsync
        });

        return app;
    }

    private static Task WriteHealthDetailsAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true }));
    }
}
