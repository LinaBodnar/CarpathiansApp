using Carpathians.Platform.ServiceDefaults.Security;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Carpathians.Booking.Grpc;
using Carpathians.Catalog.Grpc;
using Carpathians.Platform.Aggregator.Caching;
using Carpathians.Platform.Aggregator.Clients;
using Carpathians.Platform.Aggregator.Grpc;
using Carpathians.Platform.Aggregator.HealthChecks;
using Carpathians.Platform.Aggregator.Metrics;
using Carpathians.Platform.Aggregator.Resilience;
using Carpathians.Platform.Aggregator.Services;
using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Reviews.Grpc;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery; CorrelationIdHandler і resilience
// підключено до всіх HttpClient у ServiceDefaults — і до gRPC-клієнтів, що працюють поверх HttpClientFactory.
builder.AddServiceDefaults();

// Лаб. 09: перевірка JWT (issuer, audience, підпис, термін дії) локально — без звернення до Auth Service
builder.AddPlatformAuthentication();
// Permission-based авторизація: політика на кожен permission, рішення приймається локально за claims токена
builder.AddPermissionAuthorization();

// ---------- gRPC-клієнти мікросервісів (Лаб. 06) ----------
// Адреса — логічне ім'я ресурсу AppHost: service discovery працює так само, як для HttpClient у Лаб. 05.
// "https": gRPC потребує HTTP/2, який Kestrel мікросервісу узгоджує через TLS (ALPN).
// Фабрика клієнтів повторно використовує HTTP/2-з'єднання: паралельні виклики мультиплексуються в ньому.
//
// Resilience (Лаб. 07): стандартний обробник ServiceDefaults замінено політикою сервісу (без нашарування):
// критичні catalog-api і booking-api — більше повторів і чутливіший circuit breaker,
// некритичний reviews-api — один повтор і короткі таймаути (див. Resilience/DownstreamPolicies.cs).
// Повтори за gRPC-статусами (UNAVAILABLE, RESOURCE_EXHAUSTED) — retry-політика каналу.
builder.Services.AddSingleton<GrpcClientLoggingInterceptor>();

builder.Services.AddGrpcClient<BookingGrpc.BookingGrpcClient>(o => o.Address = new Uri("https://booking-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHandler(builder.Environment))
    .AddDownstreamResilience(DownstreamPolicies.Booking, DownstreamTransport.Grpc)
    .AddGrpcRetryPolicy(DownstreamPolicies.Booking);
builder.Services.AddGrpcClient<CatalogGrpc.CatalogGrpcClient>(o => o.Address = new Uri("https://catalog-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHandler(builder.Environment))
    .AddDownstreamResilience(DownstreamPolicies.Catalog, DownstreamTransport.Grpc)
    .AddGrpcRetryPolicy(DownstreamPolicies.Catalog);
builder.Services.AddGrpcClient<ReviewsGrpc.ReviewsGrpcClient>(o => o.Address = new Uri("https://reviews-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHandler(builder.Environment))
    .AddDownstreamResilience(DownstreamPolicies.Reviews, DownstreamTransport.Grpc)
    .AddGrpcRetryPolicy(DownstreamPolicies.Reviews);

// Typed HttpClients Лаб. 05 — для порівняння затримок HTTP і gRPC (HttpAggregationService); ті самі політики,
// повтори лише для безпечних методів.
builder.Services.AddHttpClient<BookingClient>(c => c.BaseAddress = new Uri("https+http://booking-api"))
    .AddHttpMessageHandler<ForwardAuthorizationHandler>()     // access token користувача -> мікросервіс (Лаб. 09)
    .AddDownstreamResilience(DownstreamPolicies.Booking, DownstreamTransport.Http);
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new Uri("https+http://catalog-api"))
    .AddHttpMessageHandler<ForwardAuthorizationHandler>()
    .AddDownstreamResilience(DownstreamPolicies.Catalog, DownstreamTransport.Http);
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new Uri("https+http://reviews-api"))
    .AddHttpMessageHandler<ForwardAuthorizationHandler>()
    .AddDownstreamResilience(DownstreamPolicies.Reviews, DownstreamTransport.Http);

// ---------- Кеш агрегованих відповідей у Redis (Лаб. 06) ----------
// ConnectionStrings:redis передає AppHost; без нього Aggregator працює без кешу.
if (!string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
{
    builder.AddRedisDistributedCache("redis",
        settings => settings.DisableHealthChecks = true,  // недоступний кеш — Degraded, а не відмова
        options =>
        {
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 2000;
            options.AsyncTimeout = 1000;
        });
}
builder.AddPlatformCaching();                               // метрики кешу, запобіжник Redis, /health/cache
builder.Services.AddSingleton(sp => new AggregatedResponseCache(
    sp.GetRequiredService<DistributedCacheCircuit>(),
    sp.GetRequiredService<CacheMetrics>(),
    sp.GetRequiredService<ILogger<AggregatedResponseCache>>(),
    sp.GetService<IDistributedCache>()));

// Бізнес-метрики (Лаб. 07): Meter "Aggregator.Api" — результати агрегації, збої мікросервісів, стан circuit breaker
builder.Services.AddSingleton<CircuitBreakerStates>();
builder.Services.AddSingleton<AggregatorMetrics>();

// Health checks (Лаб. 07): booking-api, catalog-api, reviews-api — стандартним gRPC Health Checking Protocol
// (deadline 2 с; критичні -> Unhealthy, reviews-api -> Degraded), redis-cache -> Degraded.
builder.AddAggregatorHealthChecks(() => CreateGrpcHandler(builder.Environment));

builder.Services.AddScoped<AggregationService>();          // gRPC — основна агрегація
builder.Services.AddScoped<HttpAggregationService>();      // HTTP (Лаб. 05) — для порівняння
builder.Services.AddScoped<TransportBenchmarkService>();   // вимірювання HTTP vs gRPC

// JSON у camelCase, null-значення не серіалізуються
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UsePlatformAuthentication();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// HTTP/2-обробник gRPC-каналу: кілька HTTP/2-з'єднань при великій кількості паралельних викликів.
// У Development довіряємо також сертифікату розробника ASP.NET Core (dotnet dev-certs), яким користуються
// мікросервіси під Aspire, навіть якщо він не доданий у довірені на цій машині. В інших середовищах — лише стандартна перевірка.
static SocketsHttpHandler CreateGrpcHandler(IHostEnvironment environment)
{
    var handler = new SocketsHttpHandler
    {
        EnableMultipleHttp2Connections = true,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
    };

    if (environment.IsDevelopment())
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
            errors == SslPolicyErrors.None || IsAspNetCoreDevelopmentCertificate(certificate);

    return handler;
}

// OID розширення, яким позначено сертифікат розробника ASP.NET Core.
static bool IsAspNetCoreDevelopmentCertificate(X509Certificate? certificate)
    => certificate is not null
       && new X509Certificate2(certificate).Extensions.Any(e => e.Oid?.Value == "1.3.6.1.4.1.311.84.1.1");
