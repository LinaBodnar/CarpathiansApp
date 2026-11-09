using System.Reflection;
using Carpathians.Reviews.Application;
using Carpathians.Reviews.Infrastructure;
using Carpathians.Reviews.Infrastructure.HealthChecks;
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
app.MapDefaultEndpoints();                          // /health (у т. ч. MongoDB) і /alive

app.Run();

/// <summary>Точка входу (public — для інтеграційних тестів через WebApplicationFactory).</summary>
public partial class Program
{
}
