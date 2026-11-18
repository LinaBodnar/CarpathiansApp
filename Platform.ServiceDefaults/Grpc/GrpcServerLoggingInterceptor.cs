using System.Diagnostics;
using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Platform.ServiceDefaults.Correlation;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace Carpathians.Platform.ServiceDefaults.Grpc;

/// <summary>
/// Серверний interceptor gRPC (спільний для всіх сервісів платформи):
/// <list type="bullet">
/// <item>структуровані логи кожного виклику: метод, адреса клієнта (peer), статус, тривалість,
/// CorrelationId з метаданих і результат кешу — окремо від HTTP-логів (Protocol = grpc);</item>
/// <item>атрибути span сервера (rpc.system, rpc.service, rpc.method, rpc.grpc.status_code, correlation.id),
/// який створює AddAspNetCoreInstrumentation — span клієнта в Aggregator стає його батьком;</item>
/// <item>trailer "x-cache" у відповіді: на якому рівні кешу знайдено дані (l1 / l2 / source);</item>
/// <item>помилки: RpcException логуються з кодом статусу, неочікувані винятки — з stack trace і
/// перетворюються на INTERNAL без деталей реалізації.</item>
/// </list>
/// Реєстрація: <c>builder.Services.AddGrpc(o => o.Interceptors.Add&lt;GrpcServerLoggingInterceptor&gt;())</c> — першим.
/// </summary>
public sealed class GrpcServerLoggingInterceptor(ILogger<GrpcServerLoggingInterceptor> logger) : Interceptor
{
    /// <summary>Trailer з ознакою попадання в кеш.</summary>
    public const string CacheTrailer = "x-cache";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var (service, method) = SplitMethod(context.Method);
        var correlationId = context.RequestHeaders.GetValue(CorrelationIdMiddleware.HeaderName.ToLowerInvariant());

        var activity = Activity.Current;                       // server span ASP.NET Core
        activity?.SetTag("rpc.system", "grpc");
        activity?.SetTag("rpc.service", service);
        activity?.SetTag("rpc.method", method);
        if (correlationId is not null)
            activity?.SetTag(CorrelationIdMiddleware.ActivityTag, correlationId);

        using var cacheScope = CacheCallContext.Begin(CacheCallContext.Grpc);
        using var logScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Protocol"] = "grpc",
            ["GrpcMethod"] = context.Method
        });

        logger.LogInformation("gRPC {GrpcMethod} started from {Peer}, correlation {CorrelationId}",
            context.Method, context.Peer, correlationId);
        var started = Stopwatch.GetTimestamp();

        try
        {
            var response = await continuation(request, context);

            var cache = CacheCallContext.Summary ?? "none";
            context.ResponseTrailers.Add(CacheTrailer, cache);
            activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.OK);
            activity?.SetTag("cache.result", cache);

            logger.LogInformation("gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms, cache {CacheResult}",
                context.Method, context.Peer, StatusCode.OK, Elapsed(started), cache);
            return response;
        }
        catch (RpcException ex)
        {
            activity?.SetTag("rpc.grpc.status_code", (int)ex.StatusCode);
            activity?.SetStatus(ex.StatusCode is StatusCode.NotFound or StatusCode.InvalidArgument
                ? ActivityStatusCode.Unset                     // очікувана відповідь клієнту, не збій сервера
                : ActivityStatusCode.Error, ex.Status.Detail);

            logger.LogWarning(ex, "gRPC {GrpcMethod} from {Peer}: {GrpcStatus} in {ElapsedMs} ms — {GrpcDetail}",
                context.Method, context.Peer, ex.StatusCode, Elapsed(started), ex.Status.Detail);
            throw;
        }
        catch (Exception ex) when (ex is OperationCanceledException && context.CancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.Cancelled);
            logger.LogInformation("gRPC {GrpcMethod} from {Peer}: cancelled by the client after {ElapsedMs} ms",
                context.Method, context.Peer, Elapsed(started));
            throw new RpcException(new Status(StatusCode.Cancelled, "The call was cancelled by the client"));
        }
        catch (Exception ex)
        {
            activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.Internal);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "gRPC {GrpcMethod} from {Peer} failed after {ElapsedMs} ms",
                context.Method, context.Peer, Elapsed(started));
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred"));
        }
    }

    /// <summary>"/carpathians.catalog.v1.CatalogGrpc/GetRoute" -> ("carpathians.catalog.v1.CatalogGrpc", "GetRoute").</summary>
    private static (string Service, string Method) SplitMethod(string fullMethod)
    {
        var parts = fullMethod.Trim('/').Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (fullMethod, fullMethod);
    }

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
