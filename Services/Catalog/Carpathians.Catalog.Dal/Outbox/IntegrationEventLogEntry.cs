namespace Carpathians.Dal.Outbox;

/// <summary>
/// Запис transactional outbox (як IntegrationEventLogEF в eShop): integration event зберігається в таблиці
/// IntegrationEventLog тієї самої БД і в ТІЙ САМІЙ транзакції, що й зміни маршруту.
/// <para>Зміна збережена — збережена й подія; транзакція відкотилась — події немає. Якщо застосунок
/// впаде між комітом і публікацією, подію опублікує фоновий процес (CatalogOutboxPublisher) — вона не загубиться.</para>
/// </summary>
public class IntegrationEventLogEntry
{
    private IntegrationEventLogEntry()
    {
    }

    public IntegrationEventLogEntry(Guid eventId, string eventType, int schemaVersion, string exchange,
        string routingKey, string content, DateTime occurredAt, string? correlationId, string? traceParent)
    {
        EventId = eventId;
        EventType = eventType;
        SchemaVersion = schemaVersion;
        Exchange = exchange;
        RoutingKey = routingKey;
        Content = content;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        TraceParent = traceParent;
        State = IntegrationEventState.NotPublished;
    }

    /// <summary>EventId події = MessageId повідомлення = ключ ідемпотентності у споживачів.</summary>
    public Guid EventId { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public int SchemaVersion { get; private set; }

    public string Exchange { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    /// <summary>Тіло події в JSON — саме те, що піде в брокер.</summary>
    public string Content { get; private set; } = string.Empty;

    public DateTime OccurredAt { get; private set; }

    public string? CorrelationId { get; private set; }

    /// <summary>W3C traceparent HTTP-запиту: фонова публікація продовжує його trace.</summary>
    public string? TraceParent { get; private set; }

    public IntegrationEventState State { get; private set; }

    public int TimesSent { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public string? LastError { get; private set; }

    public void MarkPublished(DateTime publishedAt)
    {
        State = IntegrationEventState.Published;
        TimesSent++;
        PublishedAt = publishedAt;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        State = IntegrationEventState.PublishFailed;
        TimesSent++;
        LastError = error.Length > 1000 ? error[..1000] : error;
    }
}

public enum IntegrationEventState
{
    NotPublished = 0,
    Published = 1,
    PublishFailed = 2
}
