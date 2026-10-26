using Carpathians.Api.Filters;
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

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
                       ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

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

// BLL Services
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IPointService, PointService>();
builder.Services.AddScoped<IRegionService, RegionService>();

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

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
