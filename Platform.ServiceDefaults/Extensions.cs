using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Platform.ServiceDefaults.Correlation;
using Carpathians.Platform.ServiceDefaults.Health;
using Carpathians.Platform.ServiceDefaults.Metrics;
using Carpathians.Platform.ServiceDefaults.Resilience;
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
    /// <summary>gRPC Health Checking Protocol (grpc.health.v1.Health/Check) — службові виклики, не трасуються.</summary>
    private const string GrpcHealthPath = "/grpc.health.v1.Health";

    /// <summary>ActivitySource драйвера MongoDB (пакет MongoDB.Driver.Core.Extensions.DiagnosticSources).</summary>
    public const string MongoDbActivitySource = "MongoDB.Driver.Core.Extensions.DiagnosticSources";

    /// <summary>
    /// ActivitySource клієнта gRPC (Grpc.Net.Client): span "Grpc.Net.Client.GrpcOut" з grpc.method і grpc.status_code.
    /// Без підписки він створюється, але не експортується — і span HTTP/2-запиту втратив би батька в trace.
    /// </summary>
    public const string GrpcClientActivitySource = "Grpc.Net.Client";

    /// <summary>Meter Polly: resilience.polly.strategy.events (OnRetry, OnCircuitOpened...), тривалість спроб і викликів.</summary>
    public const string ResilienceMeter = "Polly";

    /// <summary>
    /// ActivitySource асинхронного обміну повідомленнями (Лаб. 08, Platform.EventBus): span публікації (Producer)
    /// і обробки (Consumer). Контекст передається через брокер у заголовку traceparent — trace
    /// HTTP-запит -> публікація -> обробка в іншому сервісі -> БД стає наскрізним.
    /// </summary>
    public const string MessagingActivitySource = "Platform.Messaging";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddSerilogLogging();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        builder.Services.AddSingleton<FaultInjectionState>();     // імітація збоїв (лише Development)

        // Standard Resilience Handler (Polly): rate limiter -> загальний таймаут -> retry -> circuit breaker
        // -> таймаут спроби — для ВСІХ HttpClient, у т. ч. gRPC-клієнтів (вони створюються тією самою фабрикою).
        // Клієнт з іншими вимогами спершу прибирає цей обробник (RemoveAllResilienceHandlers), а потім
        // додає власний — обробники не нашаровуються (див. Aggregator, Resilience/DownstreamResilience.cs).
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
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
                .MinimumLevel.Override("Polly", LogEventLevel.Information)   // повтори, переходи circuit breaker
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
                .AddMeter(MessagingActivitySource)                    // RabbitMQ: оброблені повідомлення, глибина черг і DLQ (Лаб. 08)
                .AddMeter(ResilienceMeter)                            // Polly: OnRetry, OnCircuitOpened, ... (Лаб. 07)
                .AddMeter(BusinessMeters.All)                         // бізнес-метрики: Booking.Api, Catalog.Api, Reviews.Api, Aggregator.Api (Лаб. 07)
                .AddMeter("Carpathians.*")                          // інші власні метрики (Gateway)
                // межі кошиків гістограм тривалості бізнес-операцій (*.duration) — до 10 с, щоб бачити повтори й таймаути
                .AddView(instrument => OperationDurationBuckets.Applies(instrument.Meter.Name, instrument.Name)
                    ? new ExplicitBucketHistogramConfiguration { Boundaries = OperationDurationBuckets.Boundaries }
                    : null))
            .WithTracing(tracing => tracing
                .SetSampler(CreateSampler(builder))
                .AddSource(builder.Environment.ApplicationName)       // власні spans сервісу (Activity API)
                .AddSource("Carpathians.*")
                .AddSource(MongoDbActivitySource)                     // MongoDB (Лаб. 04)
                .AddSource(MessagingActivitySource)                   // RabbitMQ: publish / process (Лаб. 08)
                .AddSource(GrpcClientActivitySource)                  // gRPC-клієнт (Лаб. 06): span виклику між клієнтом і HTTP/2-запитом
                // службові перевірки стану (/health/*, /alive, gRPC health) не засмічують traces
                .AddAspNetCoreInstrumentation(o => o.Filter = context =>
                    !context.Request.Path.StartsWithSegments(HealthEndpointPaths.All)
                    && !context.Request.Path.StartsWithSegments(HealthEndpointPaths.Alive)
                    && !context.Request.Path.StartsWithSegments(GrpcHealthPath))
                .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage = request =>
                    request.RequestUri?.AbsolutePath.StartsWith(GrpcHealthPath, StringComparison.Ordinal) != true)
                                                                      // вихідні виклики (Aggregator, YARP)
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

    /// <summary>
    /// Базові перевірки кожного сервісу: "self" (liveness — процес відповідає) і "startup" (запуск завершено).
    /// Перевірки залежностей (БД, Redis, gRPC) сервіси додають самі — у папці HealthChecks/ свого .Api;
    /// усі вони накопичуються в одній колекції, порядок відносно AddServiceDefaults() не важливий.
    /// </summary>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is responding"), [HealthTags.Live])
            .AddCheck<StartupHealthCheck>("startup", tags: [HealthTags.Startup]);

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
        // Development: керована імітація збоїв /dev/faults для перевірки resilience (Лаб. 07)
        app.UseFaultInjection();

        // Health endpoints (Лаб. 07). Перевірки розподіляються за тегами; коди відповіді стандартні:
        // Healthy і Degraded -> 200, Unhealthy -> 503. Відповідь — JSON з деталями кожної перевірки.
        //   /health         — усі перевірки (WithHttpHealthCheck в AppHost, стан ресурсу в Dashboard);
        //   /health/live    — liveness: лише процес ("self", тег live), без зовнішніх залежностей — миттєво;
        //   /health/ready   — readiness: БД, Redis, gRPC-залежності (тег ready);
        //   /health/startup — startup: застосунок завершив запуск (тег startup);
        //   /alive          — liveness шаблону Aspire (сумісність), те саме, що /health/live.
        MapHealthEndpoint(app, HealthEndpointPaths.All, _ => true);
        MapHealthEndpoint(app, HealthEndpointPaths.Live, r => r.Tags.Contains(HealthTags.Live));
        MapHealthEndpoint(app, HealthEndpointPaths.Ready, r => r.Tags.Contains(HealthTags.Ready));
        MapHealthEndpoint(app, HealthEndpointPaths.Startup, r => r.Tags.Contains(HealthTags.Startup));
        MapHealthEndpoint(app, HealthEndpointPaths.Alive, r => r.Tags.Contains(HealthTags.Live));

        // /health/cache — статистика кешу (Лаб. 06): записи, розмір, попадання/промахи, стан Redis
        MapHealthEndpoint(app, HealthEndpointPaths.Cache, r => r.Tags.Contains(HealthTags.Cache));

        return app;
    }

    private static void MapHealthEndpoint(WebApplication app, string path, Func<HealthCheckRegistration, bool> predicate)
        => app.MapHealthChecks(path, new HealthCheckOptions
        {
            Predicate = predicate,
            ResponseWriter = HealthResponseWriter.WriteAsync
        });
}
