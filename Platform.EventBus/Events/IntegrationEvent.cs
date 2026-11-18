using System.Text.Json.Serialization;

namespace Carpathians.Platform.EventBus.Events;

/// <summary>
/// Базовий тип integration event — бізнес-факту, що вже стався в сервісі-власнику
/// (назва в минулому часі: RouteRenamed, а не RenameRoute).
/// <list type="bullet">
/// <item><c>record</c> з init-властивостями — незмінний, порівнюється за значенням;</item>
/// <item><see cref="EventId"/> — ключ ідемпотентності: споживач обробляє подію з тим самим EventId лише раз;</item>
/// <item><see cref="CorrelationId"/> — бізнес-трасування від HTTP-запиту (X-Correlation-Id) до обробки в іншому сервісі;</item>
/// <item>подія самодостатня: містить усі дані для обробки, споживач не звертається до власника по деталі.</item>
/// </list>
/// <para>Контракт — JSON (camelCase), а не .NET-тип: кожен сервіс має власну копію record-а
/// (як в eShop), тож сервіси не ділять збірку з контрактами й можуть бути написані будь-якою мовою.</para>
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public string? CorrelationId { get; init; }

    /// <summary>
    /// Версія схеми події (заголовок x-event-version). Зворотно сумісні зміни — лише нові поля,
    /// наявні не перейменовуються й не видаляються. Несумісна зміна = нова мажорна версія.
    /// </summary>
    [JsonIgnore]
    public virtual int SchemaVersion => 1;
}
