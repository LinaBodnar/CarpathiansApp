using Microsoft.OpenApi.Models;
using Carpathians.Platform.ServiceDefaults.Security;
using Carpathians.Api.Caching;
using Carpathians.Api.Grpc;
using Carpathians.Api.HealthChecks;
using Carpathians.Api.IntegrationEvents;
using Carpathians.Api.IntegrationEvents.Events;
using Carpathians.Api.IntegrationEvents.Handlers;
using Carpathians.Api.Metrics;
using Carpathians.Api.Middleware;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Mapping;
using Carpathians.Bll.Services;
using Carpathians.Dal.Interfaces;
using Carpathians.Dal.Migrations;
using Carpathians.Dal.Repositories;
using Carpathians.Platform.EventBus;
using Carpathians.Platform.EventBus.Topology;
using Carpathians.Platform.ServiceDefaults.Grpc;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи: Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

// Лаб. 09: перевірка JWT (issuer, audience, підпис, термін дії) локально — без звернення до Auth Service
builder.AddPlatformAuthentication();
// Keycloak (Лаб. 09, частина 3): JWT Bearer на realm DevRealmDrivers — Authority і Audience передає AppHost
builder.AddKeycloakJwtBearer();
// Permission-based авторизація: політика на кожен permission, рішення приймається локально за claims токена
builder.AddPermissionAuthorization();

var connectionString = builder.Configuration.GetConnectionString("BookingDb")
                       ?? throw new InvalidOperationException("Connection string 'BookingDb' is missing");

builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

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
// Health checks (Лаб. 07): booking-db (SQL Server, Unhealthy), redis-cache (Degraded), gRPC Health Checking Protocol.
// Endpoints /health, /health/live, /health/ready, /health/startup публікує MapDefaultEndpoints (ServiceDefaults).
builder.AddBookingHealthChecks(connectionString);

// Брокер повідомлень (Лаб. 08): IConnection і health check через Aspire.RabbitMQ.Client (ConnectionStrings:messaging).
// Бронювання — споживач подій Каталогу: черга booking.catalog-sync (route.*) з чергою повторів і DLQ.
// Обробники оновлюють копію довідника маршрутів dbo.RouteRefs (політика дублювання з Лаб. 01),
// ідемпотентно — через dbo.ProcessedEvents у тій самій транзакції.
builder.Services.AddScoped<RouteRefsSynchronizer>();
builder.AddEventBus()
    .AddConsumer(CatalogTopology.BookingCatalogSync, handlers => handlers
        .On<RouteCreatedIntegrationEvent, RouteCreatedHandler>()
        .On<RouteRenamedIntegrationEvent, RouteRenamedHandler>()
        .On<RouteDeletedIntegrationEvent, RouteDeletedHandler>());

// L1: IMemoryCache з SizeLimit і політиками витіснення + ICacheService (L1 -> L2 -> БД), метрики, health check
builder.AddPlatformCaching();

// BLL-сервіси Лаб. 02 + декоратори з кешуванням (контролери й gRPC отримують I*Service з кешем)
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<DepartureService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ICustomerService, CachedCustomerService>();
builder.Services.AddScoped<IDepartureService, CachedDepartureService>();
builder.Services.AddScoped<CachedBookingService>();

// Бізнес-метрики (Лаб. 07): Meter "Booking.Api" (Singleton на весь час роботи), декоратор операцій бронювання
// поверх кешу (лічильники подій + тривалість), знімок стану для gauge, що оновлюється фоново.
builder.Services.AddSingleton<BookingStatisticsSnapshot>();
builder.Services.AddSingleton<BookingMetrics>();
builder.Services.AddHostedService<BookingStatisticsRefresher>();
builder.Services.AddScoped<IBookingService>(sp => new MeteredBookingService(
    sp.GetRequiredService<CachedBookingService>(), sp.GetRequiredService<BookingMetrics>()));
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<BookingProfile>();
});

// gRPC-сервер (Лаб. 06): HTTP/2 через HTTPS-endpoint (Http1AndHttp2, ALPN), interceptor-и:
// логування/трасування викликів і перетворення доменних винятків на коди статусу gRPC.
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.Interceptors.Add<GrpcServerLoggingInterceptor>();
    options.Interceptors.Add<BookingGrpcExceptionInterceptor>();
});
builder.Services.AddGrpcReflection();

builder.Services.AddControllers();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
// Лаб. 09: Swagger UI з входом через IdentityServer — Authorization Code + PKCE (публічний клієнт "swagger", без секрету)
var identityServerAuthority = builder.Configuration["IdentityServer:Authority"]?.TrimEnd('/');
builder.Services.AddSwaggerGen(options =>
{
    if (string.IsNullOrEmpty(identityServerAuthority))
        return;

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{identityServerAuthority}/connect/authorize"),
                TokenUrl = new Uri($"{identityServerAuthority}/connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    ["openid"] = "Ідентифікатор користувача",
                    ["profile"] = "Ім'я",
                    ["roles"] = "Ролі",
                    [ScopePermissionMap.BookingsRead] = "Перегляд бронювань",
                    [ScopePermissionMap.BookingsWrite] = "Створення, зміна, видалення, підтвердження бронювань"
                }
            }
        }
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" } }] =
            ["openid", "profile", "roles", ScopePermissionMap.BookingsRead, ScopePermissionMap.BookingsWrite]
    });
});

var app = builder.Build();

// SQL-міграції Лаб. 08 (ProcessedEvents, RouteRefs.CatalogChangedAt/IsActive) — ідемпотентні, поза Production
if (!app.Environment.IsProduction())
{
    var applied = await BookingSchemaMigrator.ApplyAsync(connectionString);
    app.Logger.LogInformation("Booking schema migrations applied: {Migrations}", applied);
}

app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();          

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.OAuthClientId("swagger");
        options.OAuthUsePkce();                       // code_challenge (S256) замість client_secret
        options.OAuthScopes("openid", "profile", "roles", ScopePermissionMap.BookingsRead, ScopePermissionMap.BookingsWrite);
    });
}

app.UsePlatformAuthentication();
app.MapControllers();
app.MapGrpcService<BookingGrpcService>();          // gRPC: carpathians.booking.v1.BookingGrpc
app.MapGrpcHealthChecksService();                  // gRPC: grpc.health.v1.Health (Check/Watch) — для Aggregator
if (app.Environment.IsDevelopment())
    app.MapGrpcReflectionService();               // опис сервісів для Postman/grpcurl
app.MapDefaultEndpoints();
app.Run();