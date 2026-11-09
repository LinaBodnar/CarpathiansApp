using Carpathians.Api.Filters;
using Carpathians.Catalog.Api.Caching;
using Carpathians.Api.Middleware;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Mapping;
using Carpathians.Bll.Services;
using Carpathians.Bll.Validators;
using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Dal.Repositories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Спільні налаштування платформи: Serilog, OpenTelemetry, health checks, service discovery
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
                       ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

// Database
builder.Services.AddDbContext<CatalogDbContext>(o => o
    .UseSqlServer(connectionString)
    .ConfigureWarnings(w => w.Ignore(
        CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

// Repositories & Unit of Work
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IPointRepository, PointRepository>();
builder.Services.AddScoped<IRoutePointRepository, RoutePointRepository>();
builder.Services.AddScoped<IRegionRepository, RegionRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// AutoMapper
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<CatalogProfile>();
});

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateRouteDtoValidator>();

// Кеш (Лаб. 06)
// L2: IDistributedCache поверх Redis. Рядок підключення ConnectionStrings:redis передає AppHost;
// Aspire-інтеграція додає повторні підключення, логування й трасування Redis.
// Власний health check Redis вимкнено: недоступний кеш — це Degraded (див. CacheHealthCheck), а не відмова сервісу.
// Без AppHost (немає рядка підключення) сервіс працює лише з кешем у пам'яті.
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
// Прогрівання кешу: регіони й популярні маршрути — під час старту й періодично
builder.Services.AddHostedService<CatalogCacheWarmupService>();

// BLL Services (Лаб. 03) + декоратори з кешуванням: контролери отримують I*Service з кешем,
// а самі RouteService/RegionService/PointService лишаються без змін.
builder.Services.AddScoped<RouteService>();
builder.Services.AddScoped<PointService>();
builder.Services.AddScoped<RegionService>();
builder.Services.AddScoped<IRouteService, CachedRouteService>();
builder.Services.AddScoped<IPointService, CachedPointService>();
builder.Services.AddScoped<IRegionService, CachedRegionService>();

// ProblemDetails & Exception Handler
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Controllers & Swagger
builder.Services.AddControllers(o =>
{
    o.Filters.Add<ValidationFilter>();
    // Disable MVC's built-in model validation to avoid InvalidOperationException
    // on record types with [property:] DataAnnotation attributes.
    // Our ValidationFilter handles DataAnnotations + FluentValidation explicitly.
    o.ModelValidatorProviders.Clear();
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
{
    o.SuppressModelStateInvalidFilter = true;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Database migration & Seeding (only in non-production environments)
if (!app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying EF Core migrations...");
        await db.Database.MigrateAsync();
        await DbInitializer.SeedAsync(db, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
        throw;
    }
}

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
