using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Carpathians.Platform.Aggregator.Resilience;

/// <summary>Транспорт клієнта: від нього залежить, чи можна повторювати POST.</summary>
public enum DownstreamTransport
{
    /// <summary>REST (typed HttpClient, Лаб. 05): повторюються лише безпечні методи (GET).</summary>
    Http,

    /// <summary>gRPC (Лаб. 06): усі виклики — POST, але унарні RPC Aggregator лише читають дані (ідемпотентні).</summary>
    Grpc
}

/// <summary>
/// Власні політики resilience для клієнтів Aggregator.
/// <para>ServiceDefaults додає Standard Resilience Handler до всіх HttpClient (ConfigureHttpClientDefaults).
/// Щоб обробники не нашаровувалися, клієнт спершу прибирає стандартний (RemoveAllResilienceHandlers),
/// а потім додає власний з параметрами <see cref="DownstreamPolicy"/>.</para>
/// <para>gRPC повертає помилки (UNAVAILABLE, DEADLINE_EXCEEDED...) статусом у трейлерах при HTTP 200,
/// тож HTTP-обробник їх не бачить і реагує лише на мережеві збої, 5xx і таймаути. Повтори за gRPC-статусами —
/// вбудована retry-політика каналу (<see cref="AddGrpcRetryPolicy"/>). Обидва рівні обмежені deadline виклику,
/// а RetryThrottling каналу припиняє повтори при масових збоях.</para>
/// </summary>
public static class DownstreamResilience
{
    private const string LoggerCategory = "Carpathians.Aggregator.Resilience";

    /// <summary>Замінює стандартний resilience-обробник клієнта на налаштований під сервіс.</summary>
    public static IHttpClientBuilder AddDownstreamResilience(
        this IHttpClientBuilder builder, DownstreamPolicy policy, DownstreamTransport transport)
    {
        policy.Validate();

#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers позначено як experimental у Microsoft.Extensions.Http.Resilience
        builder.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        builder.AddStandardResilienceHandler().Configure((options, services) =>
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
            var client = $"{policy.Service} ({transport})";

            // Retry: експоненційна затримка з jitter — клієнти не повторюють запити одночасно (thundering herd).
            options.Retry.MaxRetryAttempts = policy.MaxRetryAttempts;
            options.Retry.Delay = policy.RetryDelay;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            if (transport == DownstreamTransport.Http)
                options.Retry.DisableForUnsafeHttpMethods();    // POST/PUT/DELETE не повторюються: без дублювання змін

            // Таймаути: спроба і загальний час разом з повторами.
            options.AttemptTimeout.Timeout = policy.AttemptTimeout;
            options.TotalRequestTimeout.Timeout = policy.TotalTimeout;

            // Circuit breaker: fail-fast при систематичних збоях; переходи станів — у логах.
            options.CircuitBreaker.FailureRatio = policy.FailureRatio;
            options.CircuitBreaker.MinimumThroughput = policy.MinimumThroughput;
            options.CircuitBreaker.SamplingDuration = policy.SamplingDuration;
            options.CircuitBreaker.BreakDuration = policy.BreakDuration;
            options.CircuitBreaker.OnOpened = args =>
            {
                logger.LogError("Circuit breaker for {Client} opened for {BreakSeconds} s after {Outcome}; calls are rejected without waiting",
                    client, args.BreakDuration.TotalSeconds, Describe(args.Outcome));
                return ValueTask.CompletedTask;
            };
            options.CircuitBreaker.OnHalfOpened = _ =>
            {
                logger.LogWarning("Circuit breaker for {Client} is half-open: probing with a trial request", client);
                return ValueTask.CompletedTask;
            };
            options.CircuitBreaker.OnClosed = _ =>
            {
                logger.LogInformation("Circuit breaker for {Client} closed: {Service} is responding again", client, policy.Service);
                return ValueTask.CompletedTask;
            };
        });

        return builder;
    }

    /// <summary>
    /// Retry-політика gRPC-каналу за gRPC-статусами: UNAVAILABLE (сервіс тимчасово недоступний)
    /// і RESOURCE_EXHAUSTED (перевантаження). RetryThrottling (token bucket) вимикає повтори,
    /// коли збоїв забагато, щоб не створювати «шторм» повторів.
    /// </summary>
    public static IHttpClientBuilder AddGrpcRetryPolicy(this IHttpClientBuilder builder, DownstreamPolicy policy)
        => builder.ConfigureChannel(channel => channel.ServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = policy.GrpcMaxAttempts,
                        InitialBackoff = policy.GrpcInitialBackoff,
                        MaxBackoff = policy.GrpcMaxBackoff,
                        BackoffMultiplier = 2,
                        RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted }
                    }
                }
            },
            RetryThrottling = new RetryThrottlingPolicy
            {
                MaxTokens = 10,
                TokenRatio = 0.1
            }
        });

    private static string Describe(Outcome<HttpResponseMessage> outcome)
        => outcome.Exception is { } ex
            ? ex.GetType().Name
            : $"HTTP {(int?)outcome.Result?.StatusCode}";
}
