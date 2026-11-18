using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Carpathians.Platform.ServiceDefaults.Resilience;

/// <summary>
/// Керована імітація збоїв для перевірки resilience (Лаб. 07, частина 1.10) — лише в Development.
/// Вмикається під час роботи сервісу без перезапуску:
/// <code>
/// POST   /dev/faults?failureRate=1&amp;delayMs=0   — усі запити відповідають 503 (серія збоїв → circuit breaker)
/// POST   /dev/faults?failureRate=0&amp;delayMs=3000 — кожен запит затримується на 3 с (перевищення таймауту спроби)
/// GET    /dev/faults                              — поточні налаштування
/// DELETE /dev/faults                              — вимкнути
/// </code>
/// 503 — тимчасовий збій для стандартного resilience-обробника (повтор, лічильник circuit breaker);
/// gRPC-клієнт отримує його як UNAVAILABLE. Health checks і сам /dev/faults збоям не піддаються.
/// </summary>
public sealed class FaultInjectionState
{
    private volatile Settings _settings = Settings.Disabled;

    public Settings Current => _settings;

    public void Set(double failureRate, int delayMs)
        => _settings = new Settings(Math.Clamp(failureRate, 0, 1), Math.Clamp(delayMs, 0, 60_000));

    public void Disable() => _settings = Settings.Disabled;

    public sealed record Settings(double FailureRate, int DelayMs)
    {
        public static readonly Settings Disabled = new(0, 0);

        public bool IsEnabled => FailureRate > 0 || DelayMs > 0;
    }
}

public sealed class FaultInjectionMiddleware(
    RequestDelegate next,
    FaultInjectionState state,
    ILogger<FaultInjectionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var settings = state.Current;
        if (!settings.IsEnabled || IsExcluded(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (settings.DelayMs > 0)
        {
            logger.LogWarning("Fault injection: delaying {Path} by {DelayMs} ms", context.Request.Path, settings.DelayMs);
            await Task.Delay(settings.DelayMs, context.RequestAborted);
        }

        if (settings.FailureRate > 0 && Random.Shared.NextDouble() < settings.FailureRate)
        {
            logger.LogWarning("Fault injection: failing {Path} with 503", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        await next(context);
    }

    private static bool IsExcluded(PathString path)
        => path.StartsWithSegments("/health") || path.StartsWithSegments("/alive")
           || path.StartsWithSegments(FaultInjectionExtensions.EndpointPath);
}

public static class FaultInjectionExtensions
{
    public const string EndpointPath = "/dev/faults";

    /// <summary>Middleware і ендпоінт керування збоями — лише в Development.</summary>
    public static WebApplication UseFaultInjection(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.UseMiddleware<FaultInjectionMiddleware>();

        var group = app.MapGroup(EndpointPath);
        group.MapGet("", (FaultInjectionState state) => Results.Json(state.Current, JsonOptions));
        group.MapPost("", (FaultInjectionState state, ILogger<FaultInjectionState> logger, double failureRate = 0, int delayMs = 0) =>
        {
            state.Set(failureRate, delayMs);
            logger.LogWarning("Fault injection enabled: failure rate {FailureRate}, delay {DelayMs} ms",
                state.Current.FailureRate, state.Current.DelayMs);
            return Results.Json(state.Current, JsonOptions);
        });
        group.MapDelete("", (FaultInjectionState state, ILogger<FaultInjectionState> logger) =>
        {
            state.Disable();
            logger.LogInformation("Fault injection disabled");
            return Results.NoContent();
        });

        return app;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
