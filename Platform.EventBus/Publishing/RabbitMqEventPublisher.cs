using System.Diagnostics;
using Carpathians.Platform.EventBus.Diagnostics;
using Carpathians.Platform.EventBus.Events;
using Carpathians.Platform.EventBus.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Carpathians.Platform.EventBus.Publishing;

/// <summary>
/// Publisher на RabbitMQ.Client 7.x. IConnection — singleton (Aspire), канал створюється на кожну публікацію:
/// IChannel не потокобезпечний, а публікації йдуть паралельно з різних запитів.
/// <list type="bullet">
/// <item>канал з publisher confirms: BasicPublishAsync завершується, лише коли брокер підтвердив прийом
/// (persistent-повідомлення записане на диск); nack брокера — виняток, подія лишається неопублікованою;</item>
/// <item>BasicProperties: Persistent, ContentType, MessageId = EventId, Type = тип події, Timestamp,
/// заголовки x-correlation-id, x-event-version, x-source і traceparent.</item>
/// </list>
/// </summary>
public sealed class RabbitMqEventPublisher(
    IConnection connection,
    IHostEnvironment environment,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    private static readonly CreateChannelOptions ConfirmedChannel =
        new(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);

    public Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent
        => PublishAsync(new OutgoingMessage(
            @event.EventId,
            typeof(T).Name,
            @event.SchemaVersion,
            @event.OccurredAt,
            @event.CorrelationId,
            exchange,
            routingKey,
            EventJson.Serialize(@event)), ct);

    public async Task PublishAsync(OutgoingMessage message, CancellationToken ct = default)
    {
        using var activity = TracePropagation.StartPublishActivity(
            message.Exchange, message.RoutingKey, message.EventType, message.EventId, message.ParentContext);
        activity?.SetTag("correlation.id", message.CorrelationId);

        var headers = new Dictionary<string, object?>
        {
            [MessageHeaders.CorrelationId] = message.CorrelationId,
            [MessageHeaders.EventVersion] = message.SchemaVersion,
            [MessageHeaders.Source] = environment.ApplicationName
        };
        TracePropagation.Inject(activity ?? Activity.Current, headers);

        var properties = new BasicProperties
        {
            Persistent = true,                                   // DeliveryMode = 2: переживає перезапуск брокера
            ContentType = EventJson.ContentType,
            ContentEncoding = "utf-8",
            MessageId = message.EventId.ToString(),
            CorrelationId = message.CorrelationId,
            Type = message.EventType,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(message.OccurredAt, TimeSpan.Zero).ToUnixTimeSeconds()),
            AppId = environment.ApplicationName,
            Headers = headers
        };

        try
        {
            await using var channel = await connection.CreateChannelAsync(ConfirmedChannel, ct);
            await channel.BasicPublishAsync(message.Exchange, message.RoutingKey, mandatory: false, properties,
                message.Body, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex,
                "Failed to publish {EventType} {EventId} to {Exchange}/{RoutingKey}, CorrelationId {CorrelationId}",
                message.EventType, message.EventId, message.Exchange, message.RoutingKey, message.CorrelationId);
            throw;
        }

        logger.LogInformation(
            "Published {EventType} {EventId} (v{EventVersion}) to {Exchange}/{RoutingKey}, CorrelationId {CorrelationId}",
            message.EventType, message.EventId, message.SchemaVersion, message.Exchange, message.RoutingKey,
            message.CorrelationId);
    }
}
