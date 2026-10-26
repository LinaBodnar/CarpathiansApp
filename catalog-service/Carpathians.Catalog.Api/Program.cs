using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Mapping;
using Carpathians.Bll.Services;
using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Dal.Repositories;
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

// AutoMapper (with LicenseKey as in Orders / Booking)
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddProfile<CatalogProfile>();
});

// BLL Services
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IPointService, PointService>();
builder.Services.AddScoped<IRegionService, RegionService>();

builder.Services.AddControllers();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.MapControllers();
app.Run();
