using Carpathians.Api.Caching;
using Carpathians.Api.Middleware;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Mapping;
using Carpathians.Bll.Services;
using Carpathians.Dal.Interfaces;
using Carpathians.Dal.Repositories;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи: Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

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
// L1: IMemoryCache з SizeLimit і політиками витіснення + ICacheService (L1 -> L2 -> БД), метрики, health check
builder.AddPlatformCaching();

// BLL-сервіси Лаб. 02 + декоратори з кешуванням (контролери й gRPC отримують I*Service з кешем)
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<DepartureService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ICustomerService, CachedCustomerService>();
builder.Services.AddScoped<IDepartureService, CachedDepartureService>();
builder.Services.AddScoped<IBookingService, CachedBookingService>();
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<BookingProfile>();
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
app.MapDefaultEndpoints();
app.Run();