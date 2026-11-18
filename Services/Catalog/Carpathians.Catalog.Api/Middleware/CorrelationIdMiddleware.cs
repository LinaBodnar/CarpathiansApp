using Serilog.Context;

namespace Carpathians.Api.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationHeader] = correlationId;

        var userId = context.User.Identity?.Name ?? "anonymous";

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value ?? "/"))
        using (LogContext.PushProperty("UserId", userId))
        {
            await next(context);
        }
    }
}
