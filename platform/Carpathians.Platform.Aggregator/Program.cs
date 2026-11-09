using System.Text.Json.Serialization;
using Carpathians.Platform.Aggregator.Clients;
using Carpathians.Platform.Aggregator.Services;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Serilog, OpenTelemetry, health checks, service discovery; CorrelationIdHandler і resilience
// підключено до всіх HttpClient у ServiceDefaults.
builder.AddServiceDefaults();

// Typed HttpClients: адреса — логічне ім'я ресурсу AppHost ("https+http" — HTTPS, якщо доступний).
builder.Services.AddHttpClient<BookingClient>(c => c.BaseAddress = new Uri("https+http://booking-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new Uri("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new Uri("https+http://reviews-api"));

// Агрегатор не має чекати недоступний сервіс десятки секунд: одна повторна спроба й короткі таймаути
// (налаштування стандартного resilience-обробника; детально — Лаб. 07).
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(o =>
{
    o.Retry.MaxRetryAttempts = 1;
    o.Retry.Delay = TimeSpan.FromMilliseconds(200);
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<AggregationService>();

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
