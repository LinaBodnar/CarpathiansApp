namespace Carpathians.Platform.EventBus.Topology;

/// <summary>
/// Черга сервісу-споживача з усім, що їй потрібно для надійної обробки:
/// <list type="bullet">
/// <item><c>Name</c> — основна черга (durable), прив'язана до exchange шаблонами <c>BindingKeys</c>;</item>
/// <item><c>Name.retry</c> — черга затримки повторів: повідомлення лежить у ній <c>RetryDelay</c>,
/// після чого RabbitMQ повертає його в основну чергу (x-message-ttl + x-dead-letter-exchange);</item>
/// <item><c>Name.dlq</c> — Dead Letter Queue: сюди потрапляє повідомлення, відхилене через
/// BasicNackAsync(requeue: false), — постійний збій або вичерпані спроби.</item>
/// </list>
/// </summary>
/// <param name="Name">Описова назва у форматі &lt;сервіс&gt;.&lt;призначення&gt;, напр. booking.catalog-sync.</param>
/// <param name="Exchange">Exchange, з якого черга отримує події.</param>
/// <param name="BindingKeys">Шаблони routing key: <c>*</c> — рівно одне слово, <c>#</c> — будь-яка кількість слів.</param>
public sealed record ConsumerQueue(string Name, ExchangeDefinition Exchange, IReadOnlyList<string> BindingKeys)
{
    /// <summary>Скільки разів повідомлення повторюється після тимчасового збою, перш ніж піти в DLQ.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Затримка перед повторною обробкою (TTL черги повторів).</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Скільки непідтверджених повідомлень споживач може отримати одночасно (BasicQos).</summary>
    public ushort PrefetchCount { get; init; } = 10;

    public string RetryQueue => $"{Name}.retry";

    public string DeadLetterQueue => $"{Name}.dlq";
}

/// <summary>Durable topic exchange подій сервісу-власника і його dead letter exchange.</summary>
/// <param name="Name">Напр. catalog-events.</param>
public sealed record ExchangeDefinition(string Name)
{
    /// <summary>
    /// Dead letter exchange (тип direct). Ключ маршрутизації = назва черги-джерела
    /// (x-dead-letter-routing-key), тож відхилене повідомлення потрапляє лише в DLQ своєї черги,
    /// а не в DLQ усіх споживачів exchange.
    /// </summary>
    public string DeadLetterExchange => $"{Name}.dlx";
}
