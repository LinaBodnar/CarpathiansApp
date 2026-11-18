using RabbitMQ.Client;

namespace Carpathians.Platform.EventBus.Topology;

/// <summary>
/// Декларативне оголошення топології під час старту застосунку.
/// Повторне оголошення з тими самими параметрами безпечне (ідемпотентне), тому його виконують
/// і publisher, і кожен consumer — незалежно від того, хто стартує першим.
/// <para>Параметри вже створених черг не можна змінювати ні в коді, ні вручну через Management UI:
/// RabbitMQ відповість PRECONDITION_FAILED під час наступного оголошення.</para>
/// </summary>
public static class RabbitMqTopology
{
    /// <summary>Exchange подій (topic, durable) і його dead letter exchange (direct, durable).</summary>
    public static async Task DeclareExchangeAsync(IChannel channel, ExchangeDefinition exchange, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(exchange.Name, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: ct);
        await channel.ExchangeDeclareAsync(exchange.DeadLetterExchange, ExchangeType.Direct, durable: true,
            autoDelete: false, cancellationToken: ct);
    }

    /// <summary>
    /// Черга споживача: основна (з bindings і DLX), черга повторів із затримкою і DLQ.
    /// <code>
    /// catalog-events --route.*--> booking.catalog-sync --nack(requeue:false)--> catalog-events.dlx --> booking.catalog-sync.dlq
    ///                                  ^                         |
    ///                                  |   (TTL 5 с)             | тимчасовий збій: публікація з x-retry-count + 1
    ///                                  +-- booking.catalog-sync.retry <--+
    /// </code>
    /// </summary>
    public static async Task DeclareConsumerQueueAsync(IChannel channel, ConsumerQueue queue, CancellationToken ct)
    {
        await DeclareExchangeAsync(channel, queue.Exchange, ct);

        // Основна черга: відхилені повідомлення -> DLX з ключем = назва черги -> лише її DLQ
        await channel.QueueDeclareAsync(queue.Name, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = queue.Exchange.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queue.Name
            },
            cancellationToken: ct);

        foreach (var bindingKey in queue.BindingKeys)
            await channel.QueueBindAsync(queue.Name, queue.Exchange.Name, bindingKey, cancellationToken: ct);

        // DLQ: повідомлення лишаються тут для ручного розслідування (заголовок x-death — причина й історія)
        await channel.QueueDeclareAsync(queue.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false,
            cancellationToken: ct);
        await channel.QueueBindAsync(queue.DeadLetterQueue, queue.Exchange.DeadLetterExchange, queue.Name,
            cancellationToken: ct);

        // Черга повторів без споживачів: після x-message-ttl RabbitMQ перекладає повідомлення
        // через default exchange ("") назад в основну чергу — затримка без блокування споживача.
        await channel.QueueDeclareAsync(queue.RetryQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = (int)queue.RetryDelay.TotalMilliseconds,
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = queue.Name
            },
            cancellationToken: ct);
    }
}
