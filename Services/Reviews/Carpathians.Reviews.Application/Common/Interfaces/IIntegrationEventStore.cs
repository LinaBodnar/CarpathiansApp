namespace Carpathians.Reviews.Application.Common.Interfaces;

/// <summary>
/// Стан обробки integration events у Відгуках (колекції processedEvents і routeNameSync у MongoDB).
/// <para>MongoDB платформи — standalone-екземпляр без replica set, тож багатодокументних транзакцій немає.
/// Ідемпотентність забезпечує порядок кроків і сама операція: <c>$set routeName</c> з тим самим значенням
/// повторно нічого не змінює, тому збій між кроками безпечний — подію буде оброблено ще раз з тим самим результатом.</para>
/// </summary>
public interface IIntegrationEventStore
{
    /// <summary>Чи оброблено подію з цим EventId (колекція processedEvents, _id = EventId).</summary>
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Позначає подію обробленою; повторний виклик для того самого EventId нічого не робить.</summary>
    Task MarkProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default);

    /// <summary>
    /// Фіксує назву маршруту з часом події Каталогу (routeNameSync, _id = routeId), якщо подія не старша
    /// за вже застосовану. <c>false</c> — out-of-order: новішу назву вже застосовано, цю подію слід пропустити.
    /// </summary>
    Task<bool> TryAdvanceRouteNameAsync(long routeId, string routeName, DateTime changedAt, CancellationToken ct = default);
}
