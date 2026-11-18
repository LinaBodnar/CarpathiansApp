using Carpathians.Platform.EventBus.Events;

namespace Carpathians.Platform.EventBus.Consuming;

/// <summary>
/// Обробник одного типу integration event. Окремий клас на кожен тип події; створюється в DI-scope
/// конкретного повідомлення (репозиторії, Unit of Work, кеш — scoped-залежності цього повідомлення).
/// <para>Обробник ОБОВ'ЯЗКОВО ідемпотентний: RabbitMQ гарантує доставку «щонайменше один раз»,
/// тож та сама подія може прийти повторно — повторна обробка не повинна змінити стан удруге.</para>
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}

/// <summary>
/// Постійний збій: повтор не допоможе (некоректні бізнес-дані, порушення інваріанта).
/// Consumer одразу відхиляє повідомлення в DLQ без повторних спроб.
/// </summary>
public sealed class PermanentEventException(string message, Exception? inner = null) : Exception(message, inner);
