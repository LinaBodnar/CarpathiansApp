using System.Text.Json;
using Carpathians.Platform.EventBus.Events;
using Carpathians.Platform.EventBus.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Carpathians.Platform.EventBus.Consuming;

/// <summary>
/// Маршрутизація повідомлень черги до обробників за типом події (BasicProperties.Type):
/// тип -> .NET-тип record-а для десеріалізації -> обробник із DI-scope повідомлення.
/// </summary>
public sealed class EventHandlerRegistry
{
    private readonly Dictionary<string, EventRegistration> _registrations = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> EventTypes => _registrations.Keys;

    /// <summary>
    /// Підписка на тип події. <paramref name="eventType"/> — ім'я типу в повідомленні (за замовчуванням ім'я record-а);
    /// <paramref name="maxSupportedVersion"/> — найвища версія схеми, яку розуміє обробник.
    /// </summary>
    public EventHandlerRegistry On<TEvent, THandler>(string? eventType = null, int maxSupportedVersion = 1)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        var name = eventType ?? typeof(TEvent).Name;
        _registrations[name] = new EventRegistration(
            name,
            typeof(TEvent),
            typeof(THandler),
            maxSupportedVersion,
            (services, @event, ct) => services.GetRequiredService<THandler>().HandleAsync((TEvent)@event, ct));
        return this;
    }

    internal IEnumerable<Type> HandlerTypes => _registrations.Values.Select(r => r.HandlerType);

    internal bool TryGet(string? eventType, out EventRegistration registration)
    {
        if (eventType is not null && _registrations.TryGetValue(eventType, out var found))
        {
            registration = found;
            return true;
        }

        registration = null!;
        return false;
    }
}

internal sealed record EventRegistration(
    string EventType,
    Type EventClrType,
    Type HandlerType,
    int MaxSupportedVersion,
    Func<IServiceProvider, IntegrationEvent, CancellationToken, Task> Invoke)
{
    /// <summary>Десеріалізація тіла; порожнє тіло, некоректний JSON або null — <see cref="JsonException"/>.</summary>
    public IntegrationEvent Deserialize(ReadOnlyMemory<byte> body)
        => JsonSerializer.Deserialize(body.Span, EventClrType, EventJson.Options) as IntegrationEvent
           ?? throw new JsonException($"Message body is not a valid {EventType}");
}
