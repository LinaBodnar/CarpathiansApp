using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Carpathians.Platform.ServiceDefaults.Correlation;

/// <summary>
/// Наскрізний ідентифікатор запиту. Береться із заголовка X-Correlation-Id або генерується
/// (на вході в систему — у Gateway; у сервісах — якщо до них звернулися в обхід Gateway),
/// записується в Request.Headers (його побачать YARP і CorrelationIdHandler), повертається
/// клієнту, додається в LogContext і як тег поточного span.
/// На початку й у кінці запиту логується межа запиту (boundary logging).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogProperty = "CorrelationId";
    public const string ActivityTag = "correlation.id";

    public async Task InvokeAsync(HttpContext context)
    {
        var generated = false;
        string correlationId = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = correlationId;     // далі його побачать YARP і HttpClient
            generated = true;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag(ActivityTag, correlationId);

        using (LogContext.PushProperty(LogProperty, correlationId))
        {
            if (generated)
                logger.LogInformation("Generated new CorrelationId {CorrelationId} for {Method} {Path}",
                    correlationId, context.Request.Method, context.Request.Path);

            var sw = Stopwatch.StartNew();
            logger.LogInformation("Request started {Method} {Path} from {ClientIp}",
                context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress?.ToString());

            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation("Request finished {Method} {Path} with {StatusCode} in {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}
