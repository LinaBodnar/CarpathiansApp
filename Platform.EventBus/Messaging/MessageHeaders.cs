namespace Carpathians.Platform.EventBus.Messaging;

/// <summary>
/// Заголовки повідомлення (метадані поза тілом події). Тип події передається у BasicProperties.Type,
/// EventId — у BasicProperties.MessageId, час — у BasicProperties.Timestamp.
/// </summary>
public static class MessageHeaders
{
    /// <summary>Бізнес-трасування (X-Correlation-Id HTTP-запиту, що спричинив подію).</summary>
    public const string CorrelationId = "x-correlation-id";

    /// <summary>Версія схеми події — споживач відхиляє несумісну версію в DLQ.</summary>
    public const string EventVersion = "x-event-version";

    /// <summary>Сервіс, що опублікував подію.</summary>
    public const string Source = "x-source";

    /// <summary>Номер повторної спроби обробки (0 — перша доставка).</summary>
    public const string RetryCount = "x-retry-count";

    /// <summary>Причина останнього збою обробки (для розслідування в DLQ).</summary>
    public const string LastError = "x-last-error";

    /// <summary>Історія dead-lettering, яку додає RabbitMQ (причина, черга, кількість).</summary>
    public const string Death = "x-death";

    /// <summary>W3C Trace Context (OpenTelemetry propagator).</summary>
    public const string TraceParent = "traceparent";
}
