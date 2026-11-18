namespace Carpathians.Dal.Interfaces;

/// <summary>Копія довідника маршрутів Каталогу (dbo.RouteRefs), яку оновлюють події catalog-events.</summary>
public interface IRouteRefRepository
{
    /// <summary>
    /// Застосовує стан маршруту з події (upsert). Зміна застосовується, лише якщо подія новіша за останню
    /// застосовану (<paramref name="changedAt"/> &gt; CatalogChangedAt) — захист від out-of-order доставки.
    /// </summary>
    /// <returns><c>true</c> — копію оновлено; <c>false</c> — подія застаріла, дані не змінено.</returns>
    Task<bool> ApplyAsync(long routeId, string name, bool isActive, DateTime changedAt, CancellationToken ct = default);
}

/// <summary>Оброблені integration events (dbo.ProcessedEvents) — ідемпотентність споживача.</summary>
public interface IProcessedEventRepository
{
    /// <summary>
    /// Фіксує подію як оброблену в поточній транзакції.
    /// <c>false</c> — EventId уже є (порушення PRIMARY KEY, помилка 2627): повторна доставка.
    /// </summary>
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default);
}
