using Carpathians.Platform.ServiceDefaults.Security;
using System.Reflection;
using Carpathians.Reviews.Application;
using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Infrastructure;
using Carpathians.Reviews.Infrastructure.Persistence.Queries;
using Carpathians.Reviews.Infrastructure.Persistence.Repositories;
using Carpathians.Platform.EventBus;
using Carpathians.Platform.EventBus.Topology;
using Carpathians.Platform.ServiceDefaults.Grpc;
using Carpathians.Reviews.WebApi.Caching;
using Carpathians.Reviews.WebApi.Grpc;
using Carpathians.Reviews.WebApi.HealthChecks;
using Carpathians.Reviews.WebApi.IntegrationEvents.Events;
using Carpathians.Reviews.WebApi.IntegrationEvents.Handlers;
using Carpathians.Reviews.WebApi.Mappings;
using Carpathians.Reviews.WebApi.Metrics;
using Carpathians.Reviews.WebApi.Middleware;
using MongoDB.Driver;
using Serilog;
using PlatformCorrelation = Carpathians.Platform.ServiceDefaults.Correlation.CorrelationIdMiddleware;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи: Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Лаб. 09: перевірка JWT (issuer, audience, підпис, термін дії) локально — без звернення до Auth Service
builder.AddPlatformAuthentication();
// Лаб. 09: політики permissions і scopes (gRPC для Aggregator — scope reviews.read)
builder.AddPermissionAuthorization();

// Aspire передає рядок підключення як ConnectionStrings:ReviewsDb (з іменем бази в URL);
// без Aspire використовується секція MongoDb з appsettings.json.
if (builder.Configuration.GetConnectionString("ReviewsDb") is { Length: > 0 } aspireMongo)
{
    builder.Configuration["MongoDb:ConnectionString"] = aspireMongo;
    if (MongoUrl.Create(aspireMongo).DatabaseName is { Length: > 0 } databaseName)
        builder.Configuration["MongoDb:DatabaseName"] = databaseName;
}

// Шари Clean Architecture: кожен шар надає власний метод розширення
// (+ збірка WebApi з ApiMappingProfile: контракти API -> команди)
// MetricsBehavior (Лаб. 07) — найзовнішній behavior MediatR: тривалість і бізнес-лічильники кожної команди/запиту.
builder.Services
    .AddApplication(builder.Configuration,                                      // MediatR, Behaviors, валідатори, AutoMapper
        cfg => cfg.AddOpenBehavior(typeof(MetricsBehavior<,>)),
        typeof(ApiMappingProfile).Assembly)
    .AddInfrastructure(builder.Configuration);                                  // MongoDB: мапінг, контекст, репозиторії, запити

// Бізнес-метрики (Лаб. 07): Meter "Reviews.Api" (Singleton), знімок стану MongoDB для gauge, що оновлюється фоново.
builder.Services.AddSingleton<ReviewsStatisticsSnapshot>();
builder.Services.AddSingleton<ReviewsMetrics>();
builder.Services.AddHostedService<ReviewsStatisticsRefresher>();

// Кеш (Лаб. 06)
// L2: IDistributedCache поверх Redis (ConnectionStrings:redis від AppHost, Aspire-інтеграція).
// Власний health check Redis вимкнено: недоступний кеш — це Degraded (CacheHealthCheck), а не відмова сервісу.
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
{
    builder.AddRedisDistributedCache("redis",
        settings => settings.DisableHealthChecks = true,
        options =>
        {
            options.AbortOnConnectFail = false; // старт сервісу не залежить від Redis
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 1000;        // недоступний Redis не затримує запит надовго
        });
}
// L1: IMemoryCache з SizeLimit і політиками витіснення + ICacheService (L1 -> L2 -> MongoDB), метрики, health check
builder.AddPlatformCaching();

// Декоратори з кешем поверх реалізацій Infrastructure (пізніша реєстрація інтерфейсу перекриває попередню):
// читання (IReviewQueries) кешується, запис (IReviewRepository) інвалідує кеш маршруту.
builder.Services.AddScoped<ReviewQueries>();
builder.Services.AddScoped<ReviewRepository>();
builder.Services.AddScoped<IReviewQueries, CachedReviewQueries>();
builder.Services.AddScoped<IReviewRepository, CachedReviewRepository>();

// Централізована обробка помилок: винятки -> ProblemDetails з коректним статусом
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    if (ctx.HttpContext.Request.Headers.TryGetValue(PlatformCorrelation.HeaderName, out var correlationId))
        ctx.ProblemDetails.Extensions["correlationId"] = correlationId.ToString();
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Health checks (Лаб. 07): reviews-db (MongoDB ping, Unhealthy), redis-cache (Degraded), gRPC Health Checking Protocol.
// Endpoints /health, /health/live, /health/ready, /health/startup публікує MapDefaultEndpoints (ServiceDefaults).
builder.AddReviewsHealthChecks();

// Брокер повідомлень (Лаб. 08): IConnection і health check через Aspire.RabbitMQ.Client (ConnectionStrings:messaging).
// Відгуки — споживач подій Каталогу: черга reviews.catalog-sync (route.renamed) з чергою повторів і DLQ.
// Обробник оновлює routeName в усіх відгуках і обговореннях маршруту (SyncRouteNameCommand),
// ідемпотентно — через колекцію processedEvents.
builder.AddEventBus()
    .AddConsumer(CatalogTopology.ReviewsCatalogSync, handlers => handlers
        .On<RouteRenamedIntegrationEvent, RouteRenamedHandler>());

// gRPC-сервер (Лаб. 06): HTTP/2 через HTTPS-endpoint (Http1AndHttp2, ALPN), interceptor-и:
// логування/трасування викликів і перетворення доменних винятків на коди статусу gRPC.
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerLoggingInterceptor>();
    options.Interceptors.Add<ReviewsGrpcExceptionInterceptor>();
});
builder.Services.AddGrpcReflection();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
        o.IncludeXmlComments(xml);
});

var app = builder.Build();

app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UsePlatformAuthentication();
app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();          // gRPC: carpathians.reviews.v1.ReviewsGrpc
app.MapGrpcHealthChecksService();                  // gRPC: grpc.health.v1.Health (Check/Watch) — для Aggregator
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();               // опис сервісів для Postman/grpcurl
app.MapDefaultEndpoints();                          // /health, /health/live, /health/ready, /health/startup, /alive

app.Run();

/// <summary>Точка входу (public — для інтеграційних тестів через WebApplicationFactory).</summary>
public partial class Program
{
}
