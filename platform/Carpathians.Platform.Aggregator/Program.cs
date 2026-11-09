using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using Carpathians.Booking.Grpc;
using Carpathians.Catalog.Grpc;
using Carpathians.Platform.Aggregator.Caching;
using Carpathians.Platform.Aggregator.Clients;
using Carpathians.Platform.Aggregator.Grpc;
using Carpathians.Platform.Aggregator.Services;
using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Reviews.Grpc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery; CorrelationIdHandler і resilience
// підключено до всіх HttpClient у ServiceDefaults — і до gRPC-клієнтів, що працюють поверх HttpClientFactory.
builder.AddServiceDefaults();

// ---------- gRPC-клієнти мікросервісів (Лаб. 06) ----------
// Адреса — логічне ім'я ресурсу AppHost: service discovery працює так само, як для HttpClient у Лаб. 05.
// "https": gRPC потребує HTTP/2, який Kestrel мікросервісу узгоджує через TLS (ALPN).
// Фабрика клієнтів повторно використовує HTTP/2-з'єднання: паралельні виклики мультиплексуються в ньому.
builder.Services.AddSingleton<GrpcClientLoggingInterceptor>();

builder.Services.AddGrpcClient<BookingGrpc.BookingGrpcClient>(o => o.Address = new Uri("https://booking-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHandler(builder.Environment));
builder.Services.AddGrpcClient<CatalogGrpc.CatalogGrpcClient>(o => o.Address = new Uri("https://catalog-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHandler(builder.Environment));
builder.Services.AddGrpcClient<ReviewsGrpc.ReviewsGrpcClient>(o => o.Address = new Uri("https://reviews-api"))
    .AddInterceptor<GrpcClientLoggingInterceptor>()
    .ConfigurePrimaryHttpMessageHandler(() => CreateGrpcHandler(builder.Environment));

// Typed HttpClients Лаб. 05 — лише для порівняння затримок HTTP і gRPC (HttpAggregationService).
builder.Services.AddHttpClient<BookingClient>(c => c.BaseAddress = new Uri("https+http://booking-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new Uri("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new Uri("https+http://reviews-api"));

// Агрегатор не має чекати недоступний сервіс десятки секунд: одна повторна спроба й короткі таймаути
// (стандартний resilience-обробник з ServiceDefaults; діє і для HTTP, і для gRPC-клієнтів).
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(o =>
{
    o.Retry.MaxRetryAttempts = 1;
    o.Retry.Delay = TimeSpan.FromMilliseconds(200);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
});

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
