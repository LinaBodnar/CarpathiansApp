using System.Reflection;
using Carpathians.Reviews.Application;
using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Infrastructure;
using Carpathians.Reviews.Infrastructure.HealthChecks;
using Carpathians.Reviews.Infrastructure.Persistence.Queries;
using Carpathians.Reviews.Infrastructure.Persistence.Repositories;
using Carpathians.Platform.ServiceDefaults.Grpc;
using Carpathians.Reviews.WebApi.Caching;
using Carpathians.Reviews.WebApi.Grpc;
using Carpathians.Reviews.WebApi.Mappings;
using Carpathians.Reviews.WebApi.Middleware;
using MongoDB.Driver;
using Serilog;
using PlatformCorrelation = Carpathians.Platform.ServiceDefaults.Correlation.CorrelationIdMiddleware;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи: Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

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
builder.Services
    .AddApplication(builder.Configuration, typeof(ApiMappingProfile).Assembly)  // MediatR, Behaviors, валідатори, AutoMapper
    .AddInfrastructure(builder.Configuration);                                  // MongoDB: мапінг, контекст, репозиторії, запити

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

builder.Services.AddHealthChecks()
    .AddCheck<MongoDbHealthCheck>("mongodb", tags: new[] { "db", "ready" });

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

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();          // gRPC: carpathians.reviews.v1.ReviewsGrpc
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();               // опис сервісів для Postman/grpcurl
app.MapDefaultEndpoints();                          // /health (у т. ч. MongoDB) і /alive

app.Run();

/// <summary>Точка входу (public — для інтеграційних тестів через WebApplicationFactory).</summary>
public partial class Program
{
}
