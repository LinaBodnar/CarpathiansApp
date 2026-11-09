using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Carpathians.Platform.ApiGateway.Logging;

/// <summary>
/// Логування трафіку Gateway: вхідний запит (метод, шлях, IP клієнта), рішення про маршрутизацію
/// (маршрут, кластер, адреса сервісу), відповідь (статус, тривалість, розмір) і помилки
/// недоступних сервісів (timeout, connection refused) з деталями. CorrelationId потрапляє
/// в кожен запис через LogContext (CorrelationIdMiddleware з ServiceDefaults виконується раніше).
/// </summary>
public sealed class GatewayLoggingMiddleware(
    RequestDelegate next,
    ILogger<GatewayLoggingMiddleware> logger,
    GatewayMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // службові ендпоінти не логуються як трафік
        if (context.Request.Path.StartsWithSegments("/health") || context.Request.Path.StartsWithSegments("/alive"))
        {
            await next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        var proxy = context.Features.Get<IReverseProxyFeature>();
        var routeId = proxy?.Route.Config.RouteId ?? "none";
        var clusterId = proxy?.Cluster?.Config.ClusterId ?? "none";
        var destination = proxy?.ProxiedDestination?.Model.Config.Address;
        var statusCode = context.Response.StatusCode;
        var proxyError = context.Features.Get<IForwarderErrorFeature>();

        if (proxyError is not null)
        {
            // сервіс недоступний: таймаут, відмова з'єднання тощо
            logger.LogError(proxyError.Exception,
                "{Method} {Path} -> route {RouteId}, cluster {ClusterId}: proxy error {ProxyError} after {ElapsedMs} ms, status {StatusCode}",
                context.Request.Method, context.Request.Path, routeId, clusterId,
                proxyError.Error, sw.ElapsedMilliseconds, statusCode);
        }
        else if (proxy is null)
        {
            logger.LogWarning("{Method} {Path} from {ClientIp}: no matching route, status {StatusCode}",
                context.Request.Method, context.Request.Path,
                context.Connection.RemoteIpAddress?.ToString(), statusCode);
        }
        else
        {
            logger.LogInformation(
                "{Method} {Path}{Query} from {ClientIp} -> route {RouteId}, cluster {ClusterId}, destination {Destination}: {StatusCode} in {ElapsedMs} ms, {ResponseBytes} bytes",
                context.Request.Method, context.Request.Path, context.Request.QueryString.Value,
                context.Connection.RemoteIpAddress?.ToString(), routeId, clusterId, destination,
                statusCode, sw.ElapsedMilliseconds, context.Response.ContentLength);
        }

        metrics.Record(routeId, clusterId, statusCode, sw.Elapsed.TotalMilliseconds,
            failed: proxyError is not null || statusCode >= StatusCodes.Status500InternalServerError);
    }
}
