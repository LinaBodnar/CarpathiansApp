using System.Diagnostics;
using Carpathians.Platform.EventBus.Events;

namespace Carpathians.Platform.EventBus.Publishing;

/// <summary>
/// Публікація integration events у брокер. Publisher надсилає подію в exchange з routing key
/// і нічого не знає про споживачів — розподіл по чергах виконує exchange (publish-subscribe).
/// <para>Викликається лише ПІСЛЯ успішного збереження змін у БД сервісу-власника.</para>
/// </summary>
public interface IEventPublisher
{
    /// <summary>Серіалізує подію в JSON і публікує її (persistent, з підтвердженням брокера).</summary>
    Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct = default)
        where T : IntegrationEvent;

    /// <summary>
    /// Публікує вже серіалізовану подію (напр. з таблиці outbox). <see cref="OutgoingMessage.ParentContext"/>
    /// відновлює trace HTTP-запиту, у якому подію було збережено.
    /// </summary>
    Task PublishAsync(OutgoingMessage message, CancellationToken ct = default);
}

/// <summary>Серіалізована подія з метаданими для публікації.</summary>
public sealed record OutgoingMessage(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTime OccurredAt,
    string? CorrelationId,
    string Exchange,
    string RoutingKey,
    ReadOnlyMemory<byte> Body)
{
    /// <summary>Контекст трасування, у якому подію створено (default — поточний Activity).</summary>
    public ActivityContext ParentContext { get; init; }
}
