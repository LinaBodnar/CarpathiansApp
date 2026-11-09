using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpathians.Platform.ApiGateway.Logging;

/// <summary>
/// Метрики трафіку Gateway (експортуються OpenTelemetry через AddMeter("Carpathians.*")):
/// кількість запитів, тривалість і помилки в розрізі маршруту й кластера.
/// У Dashboard (Metrics) з них видно запити за секунду, середній час відповіді й частку помилок.
/// </summary>
public sealed class GatewayMetrics
{
    public const string MeterName = "Carpathians.Gateway";

    private readonly Counter<long> _requests;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _duration;

    public GatewayMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _requests = meter.CreateCounter<long>("gateway.requests", description: "Proxied requests");
        _errors = meter.CreateCounter<long>("gateway.errors", description: "Failed proxied requests (5xx or proxy error)");
        _duration = meter.CreateHistogram<double>("gateway.request.duration", unit: "ms",
            description: "Proxied request duration");
    }

    public void Record(string route, string cluster, int statusCode, double elapsedMs, bool failed)
    {
        var tags = new TagList
        {
            { "route", route },
            { "cluster", cluster },
            { "status_code", statusCode }
        };

        _requests.Add(1, tags);
        _duration.Record(elapsedMs, tags);
        if (failed)
            _errors.Add(1, tags);
    }
}
