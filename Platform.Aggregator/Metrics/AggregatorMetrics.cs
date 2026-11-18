using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Carpathians.Platform.Aggregator.Resilience;
using Carpathians.Platform.Aggregator.Services;
using Carpathians.Platform.ServiceDefaults.Metrics;
using Grpc.Core;

namespace Carpathians.Platform.Aggregator.Metrics;

/// <summary>
/// Бізнес-метрики Aggregator (Meter "Aggregator.Api", OpenTelemetry -> Aspire Dashboard).
/// <list type="bullet">
/// <item>aggregator.requests — агреговані відповіді за типом (operation), результатом (aggregation.result:
/// complete | partial | not_found | unavailable | error) і джерелом (cache.result: hit | miss);</item>
/// <item>aggregator.request.duration — тривалість агрегації, успішної й невдалої (ті самі теги + status, error.type);</item>
/// <item>aggregator.downstream.failures — невдалі виклики мікросервісів (downstream.service, rpc.grpc.status);</item>
/// <item>gauge aggregator.circuit.state — стан circuit breaker кожного клієнта: 0 closed, 1 half-open, 2 open
/// (downstream.service, transport).</item>
/// </list>
/// </summary>
public sealed class AggregatorMetrics
{
    public const string MeterName = BusinessMeters.Aggregator;

    /// <summary>Значення тегу operation.</summary>
    public static class Aggregations
    {
        public const string FullBooking = "full_booking";
        public const string RouteOverview = "route_overview";
    }

    /// <summary>Значення тегу aggregation.result.</summary>
    public static class Results
    {
        public const string Complete = "complete";
        public const string Partial = "partial";
        public const string NotFound = "not_found";
        public const string Unavailable = "unavailable";
        public const string Error = "error";
    }

    private const string ResultTag = "aggregation.result";
    private const string CacheTag = "cache.result";
    private const string ServiceTag = "downstream.service";
    private const string GrpcStatusTag = "rpc.grpc.status";
    private const string TransportTag = "transport";

    private readonly Counter<long> _requests;
    private readonly Counter<long> _downstreamFailures;
    private readonly Histogram<double> _duration;

    public AggregatorMetrics(IMeterFactory meterFactory, CircuitBreakerStates circuits)
    {
        var meter = meterFactory.Create(MeterName);

        _requests = meter.CreateCounter<long>("aggregator.requests", "{request}",
            "Aggregated responses by type, result and cache source");
        _downstreamFailures = meter.CreateCounter<long>("aggregator.downstream.failures", "{call}",
            "Failed calls to downstream services (after resilience retries)");
        _duration = meter.CreateHistogram<double>("aggregator.request.duration", "ms",
            "Duration of aggregation requests (successful and failed)");

        meter.CreateObservableGauge("aggregator.circuit.state", () => ObserveCircuits(circuits), "{state}",
            "Circuit breaker state per downstream client: 0 closed, 1 half-open, 2 open");
    }

    /// <summary>
    /// Виконує агрегацію, класифікує результат і записує лічильник та тривалість — і для успіху, і для збою.
    /// Value = null — даних немає (404); часткова відповідь — дані є, але частина сервісів недоступна.
    /// </summary>
    public async Task<(T? Value, bool FromCache)> MeasureAsync<T>(string aggregation,
        Func<Task<(T? Value, bool FromCache)>> action, Func<T, bool> isPartial) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var response = await action();
            var result = response.Value is null ? Results.NotFound
                : isPartial(response.Value) ? Results.Partial
                : Results.Complete;
            Record(aggregation, result, response.FromCache ? "hit" : "miss", started, null);
            return response;
        }
        catch (Exception ex)
        {
            var result = ex is UpstreamUnavailableException ? Results.Unavailable : Results.Error;
            Record(aggregation, result, "miss", started, ex);
            throw;
        }
    }

    /// <summary>Невдалий виклик мікросервісу (після всіх повторів): назва сервісу й gRPC-статус — обмежені переліки.</summary>
    public void DownstreamFailed(string service, StatusCode status)
        => _downstreamFailures.Add(1,
            new KeyValuePair<string, object?>(ServiceTag, service),
            new KeyValuePair<string, object?>(GrpcStatusTag, status.ToString()));

    private void Record(string aggregation, string result, string cache, long started, Exception? error)
    {
        var tags = MetricTags.ForOperation(aggregation, error);
        tags.Add(ResultTag, result);
        tags.Add(CacheTag, cache);

        _requests.Add(1, tags);
        _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
    }

    private static IEnumerable<Measurement<int>> ObserveCircuits(CircuitBreakerStates circuits)
    {
        foreach (var ((service, transport), state) in circuits.Current)
            yield return new Measurement<int>((int)state,
                new KeyValuePair<string, object?>(ServiceTag, service),
                new KeyValuePair<string, object?>(TransportTag, transport.ToString().ToLowerInvariant()));
    }
}

/// <summary>Стан circuit breaker (значення gauge aggregator.circuit.state).</summary>
public enum DownstreamCircuitState
{
    Closed = 0,
    HalfOpen = 1,
    Open = 2
}

/// <summary>
/// Поточний стан circuit breaker кожного клієнта Aggregator (сервіс × транспорт). Оновлюється з колбеків
/// OnOpened / OnHalfOpened / OnClosed resilience-політики; від старту всі ланцюги замкнені.
/// </summary>
public sealed class CircuitBreakerStates
{
    private readonly ConcurrentDictionary<(string Service, DownstreamTransport Transport), DownstreamCircuitState> _states = new();

    public CircuitBreakerStates()
    {
        foreach (var policy in DownstreamPolicies.All)
            foreach (var transport in Enum.GetValues<DownstreamTransport>())
                _states[(policy.Service, transport)] = DownstreamCircuitState.Closed;
    }

    public IEnumerable<KeyValuePair<(string Service, DownstreamTransport Transport), DownstreamCircuitState>> Current => _states;

    public DownstreamCircuitState Get(string service, DownstreamTransport transport)
        => _states.GetValueOrDefault((service, transport), DownstreamCircuitState.Closed);

    public void Set(string service, DownstreamTransport transport, DownstreamCircuitState state) => _states[(service, transport)] = state;
}
