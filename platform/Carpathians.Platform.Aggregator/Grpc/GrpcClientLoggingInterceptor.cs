using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Carpathians.Platform.Aggregator.Grpc;

/// <summary>
/// Клієнтський interceptor gRPC в Aggregator: окремий span клієнта для кожного виклику
/// (ActivityKind.Client, атрибути rpc.*), структуровані логи (сервіс, метод, статус, тривалість)
/// і результат кешу сервісу з trailer "x-cache".
/// <para>Span HTTP/2-запиту (AddHttpClientInstrumentation) стає дочірнім до цього span, а span сервера
/// (AddAspNetCoreInstrumentation у мікросервісі) — дочірнім до HTTP-запиту: увесь виклик видно
/// в одному trace з правильною ієрархією. CorrelationId потрапляє в метадані виклику автоматично —
/// CorrelationIdHandler з ServiceDefaults додає заголовок до HTTP/2-запиту, що несе gRPC-виклик.</para>
/// </summary>
public sealed class GrpcClientLoggingInterceptor(ILogger<GrpcClientLoggingInterceptor> logger) : Interceptor
{
    public const string ActivitySourceName = "Carpathians.Aggregator.Grpc";
    private const string CacheTrailer = "x-cache";

    private static readonly ActivitySource Telemetry = new(ActivitySourceName);

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var service = context.Method.ServiceName;
        var method = context.Method.Name;

        var parent = Activity.Current;
        var activity = Telemetry.StartActivity($"{service}/{method}", ActivityKind.Client);
        activity?.SetTag("rpc.system", "grpc");
        activity?.SetTag("rpc.service", service);
        activity?.SetTag("rpc.method", method);
        activity?.SetTag("server.address", context.Host);

        var started = Stopwatch.GetTimestamp();
        AsyncUnaryCall<TResponse> call;
        try
        {
            call = continuation(request, context);               // HTTP/2-запит стартує всередині span клієнта
        }
        finally
        {
            Activity.Current = parent;                           // span завершиться разом з відповіддю
        }

        return new AsyncUnaryCall<TResponse>(
            CompleteAsync(call, activity, service, method, started),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> CompleteAsync<TResponse>(
        AsyncUnaryCall<TResponse> call, Activity? activity, string service, string method, long started)
    {
        try
        {
            var response = await call.ResponseAsync;
            var cache = call.GetTrailers().GetValue(CacheTrailer) ?? "none";

            activity?.SetTag("rpc.grpc.status_code", (int)StatusCode.OK);
            activity?.SetTag("cache.result", cache);
            logger.LogInformation("gRPC call {GrpcService}/{GrpcMethod}: {GrpcStatus} in {ElapsedMs} ms, service cache {CacheResult}",
                service, method, StatusCode.OK, Elapsed(started), cache);
            return response;
        }
        catch (RpcException ex)
        {
            activity?.SetTag("rpc.grpc.status_code", (int)ex.StatusCode);
            if (ex.StatusCode != StatusCode.NotFound)
                activity?.SetStatus(ActivityStatusCode.Error, ex.Status.Detail);

            logger.Log(ex.StatusCode == StatusCode.NotFound ? LogLevel.Information : LogLevel.Warning,
                "gRPC call {GrpcService}/{GrpcMethod}: {GrpcStatus} in {ElapsedMs} ms — {GrpcDetail}",
                service, method, ex.StatusCode, Elapsed(started), ex.Status.Detail);
            throw;
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}
