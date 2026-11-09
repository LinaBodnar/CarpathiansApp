using Serilog.Context;

namespace Carpathians.Reviews.WebApi.Middleware;

/// <summary>
/// Кореляційний ідентифікатор запиту: береться із заголовка X-Correlation-Id або генерується,
/// повертається у відповіді й додається до всіх записів логу (разом із RequestPath і UserId).
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[CorrelationHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value ?? "/"))
        using (LogContext.PushProperty("UserId", context.User.Identity?.Name ?? "anonymous"))
        {
            await next(context);
        }
    }
}
