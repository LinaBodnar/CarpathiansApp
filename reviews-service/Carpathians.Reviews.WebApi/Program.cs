using System.Reflection;
using Carpathians.Reviews.Application;
using Carpathians.Reviews.Infrastructure;
using Carpathians.Reviews.Infrastructure.HealthChecks;
using Carpathians.Reviews.WebApi.Mappings;
using Carpathians.Reviews.WebApi.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

// Шари Clean Architecture: кожен шар надає власний метод розширення
// (+ збірка WebApi з ApiMappingProfile: контракти API -> команди)
builder.Services
    .AddApplication(builder.Configuration, typeof(ApiMappingProfile).Assembly)  // MediatR, Behaviors, валідатори, AutoMapper
    .AddInfrastructure(builder.Configuration);                                  // MongoDB: мапінг, контекст, репозиторії, запити

// Централізована обробка помилок: винятки -> ProblemDetails з коректним статусом
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
    if (ctx.HttpContext.Response.Headers.TryGetValue(CorrelationIdMiddleware.CorrelationHeader, out var correlationId))
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
app.MapHealthChecks("/health");

app.Run();

/// <summary>Точка входу (public — для інтеграційних тестів через WebApplicationFactory).</summary>
public partial class Program
{
}
