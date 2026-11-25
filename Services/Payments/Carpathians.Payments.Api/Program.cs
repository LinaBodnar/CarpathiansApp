using Carpathians.Payments.Api.Payments;
using Carpathians.Platform.ServiceDefaults.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery (спільні налаштування платформи)
builder.AddServiceDefaults();

// Лаб. 09: перевірка JWT локально + permission-based авторизація: політика на кожен permission
// (payments:read, payments:process, payments:refund), обробник читає permissions із токена.
builder.AddPlatformAuthentication();
// Keycloak (Лаб. 09, частина 3): JWT Bearer на realm DevRealmDrivers — Authority і Audience передає AppHost
builder.AddKeycloakJwtBearer();
builder.AddPermissionAuthorization();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PaymentStore>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
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

app.UsePlatformAuthentication();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
