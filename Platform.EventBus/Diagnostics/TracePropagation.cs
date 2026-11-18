using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Carpathians.Platform.EventBus.Diagnostics;

/// <summary>
/// Передача контексту трасування через брокер (реалізовано вручну, без RabbitMQ.Client.OpenTelemetry,
/// щоб було видно, як контекст переходить між сервісами):
/// <list type="number">
/// <item>publisher створює span типу Producer і записує його контекст у заголовки повідомлення
/// у форматі W3C (<c>traceparent</c>, <c>tracestate</c>, <c>baggage</c>) через пропагатор OpenTelemetry;</item>
/// <item>consumer витягує контекст із заголовків (значення приходять як byte[]) і створює span типу Consumer
/// як нащадка — HTTP-запит, публікація й обробка в іншому сервісі опиняються в одному trace.</item>
/// </list>
/// ActivitySource "Platform.Messaging" підключено в ServiceDefaults (AddSource).
/// Теги — за OpenTelemetry messaging semantic conventions.
/// </summary>
public static class TracePropagation
{
    public const string ActivitySourceName = "Platform.Messaging";

    public static readonly ActivitySource Source = new(ActivitySourceName);

    private static TextMapPropagator Propagator => Propagators.DefaultTextMapPropagator;

    /// <summary>Span публікації: "{exchange} publish". <paramref name="parent"/> — для фонової публікації з outbox.</summary>
    public static Activity? StartPublishActivity(string exchange, string routingKey, string eventType, Guid eventId,
        ActivityContext parent = default)
    {
        var activity = Source.StartActivity($"{exchange} publish", ActivityKind.Producer, parent);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation.type", "send");
        activity.SetTag("messaging.destination.name", exchange);
        activity.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity.SetTag("messaging.message.id", eventId.ToString());
        activity.SetTag("messaging.message.type", eventType);
        return activity;
    }

    /// <summary>Записує контекст поточного span (W3C traceparent) і baggage у заголовки повідомлення.</summary>
    public static void Inject(Activity? activity, IDictionary<string, object?> headers)
    {
        if (activity is null)
            return;

        Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), headers,
            static (carrier, key, value) => carrier[key] = value);
    }

    /// <summary>Відновлює контекст трасування із заголовків отриманого повідомлення.</summary>
    public static PropagationContext Extract(IDictionary<string, object?>? headers)
        => Propagator.Extract(default, headers, static (carrier, key) =>
            carrier is not null && carrier.TryGetValue(key, out var value) && ReadHeader(value) is { } text
                ? [text]
                : []);

    /// <summary>
    /// Span обробки: "{queue} process", тип Consumer, батько — контекст із заголовків.
    /// Логи (Serilog -> OpenTelemetry) отримують TraceId/SpanId цього span автоматично.
    /// </summary>
    public static Activity? StartConsumerActivity(IDictionary<string, object?>? headers, string queue, string? messageId,
        string? eventType, int retryCount)
    {
        var parent = Extract(headers);
        Baggage.Current = parent.Baggage;

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        if (activity is null)
            return null;

        activity.SetTag("messaging.system", "rabbitmq");
        activity.SetTag("messaging.operation.type", "process");
        activity.SetTag("messaging.destination.name", queue);
        activity.SetTag("messaging.message.id", messageId);
        activity.SetTag("messaging.message.type", eventType);
        activity.SetTag("messaging.rabbitmq.retry_count", retryCount);
        return activity;
    }

    /// <summary>RabbitMQ повертає рядкові заголовки як byte[] (AMQP longstr).</summary>
    public static string? ReadHeader(object? value) => value switch
    {
        null => null,
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string text => text,
        _ => value.ToString()
    };
}
