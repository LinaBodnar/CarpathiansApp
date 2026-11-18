using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpathians.Platform.EventBus.Diagnostics;

/// <summary>
/// Метрики асинхронної обробки (Meter "Platform.Messaging", підключено в ServiceDefaults):
/// <list type="bullet">
/// <item><c>messaging.process.messages</c> — оброблені повідомлення з тегом <c>outcome</c>:
/// ack / retry / dead_letter;</item>
/// <item><c>messaging.process.duration</c> — тривалість обробки, с;</item>
/// <item><c>messaging.queue.depth</c> — глибина черг споживача, зокрема DLQ (gauge, Лаб. 07):
/// асинхронні збої «тихі», тож ріст DLQ — головний сигнал для розслідування.</item>
/// </list>
/// </summary>
public sealed class MessagingMetrics : IDisposable
{
    public const string MeterName = "Platform.Messaging";

    private readonly Meter _meter;
    private readonly Counter<long> _processed;
    private readonly Histogram<double> _duration;
    private readonly Dictionary<string, long> _queueDepths = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public MessagingMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        _processed = _meter.CreateCounter<long>("messaging.process.messages", "{message}",
            "Messages processed by consumers, by outcome");
        _duration = _meter.CreateHistogram<double>("messaging.process.duration", "s",
            "Duration of message processing");
        _meter.CreateObservableGauge("messaging.queue.depth", ObserveDepths, "{message}",
            "Messages waiting in consumer queues (main, retry, DLQ)");
    }

    public void Processed(string queue, string? eventType, string outcome, TimeSpan elapsed)
    {
        var tags = new TagList
        {
            { "messaging.destination.name", queue },
            { "messaging.message.type", eventType ?? "unknown" },
            { "outcome", outcome }
        };
        _processed.Add(1, tags);
        _duration.Record(elapsed.TotalSeconds, tags);
    }

    public void SetQueueDepth(string queue, long depth)
    {
        lock (_sync)
            _queueDepths[queue] = depth;
    }

    private IEnumerable<Measurement<long>> ObserveDepths()
    {
        lock (_sync)
            return _queueDepths
                .Select(d => new Measurement<long>(d.Value, new KeyValuePair<string, object?>("messaging.destination.name", d.Key)))
                .ToList();
    }

    public void Dispose() => _meter.Dispose();
}

/// <summary>Результати обробки повідомлення (тег <c>outcome</c>).</summary>
public static class MessageOutcome
{
    public const string Ack = "ack";
    public const string Retry = "retry";
    public const string DeadLetter = "dead_letter";
}
