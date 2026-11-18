using Carpathians.Domain.Entities;

namespace Carpathians.Bll.Interfaces;

/// <summary>
/// Integration events Каталогу з погляду бізнес-логіки. BLL не знає про брокер: вона лише фіксує бізнес-факт
/// (маршрут створено / перейменовано / видалено) у межах своєї транзакції і після коміту просить опублікувати.
/// <para>Порядок у RouteService: зміни -> <c>Add*</c> (запис в outbox, та сама транзакція) -> Commit ->
/// <see cref="PublishPendingAsync"/>. Якщо транзакція не вдалася — подія не існує і не публікується.</para>
/// Реалізація — Catalog.Api/IntegrationEvents/CatalogIntegrationEventService.
/// </summary>
public interface ICatalogIntegrationEventService
{
    /// <summary>Маршрут створено (route.created). Викликати після SaveChanges, коли відомий Id.</summary>
    Task AddRouteCreatedAsync(TrailRoute route, CancellationToken ct = default);

    /// <summary>Назву маршруту змінено (route.renamed).</summary>
    Task AddRouteRenamedAsync(TrailRoute route, string oldName, CancellationToken ct = default);

    /// <summary>Маршрут видалено (route.deleted).</summary>
    Task AddRouteDeletedAsync(TrailRoute route, CancellationToken ct = default);

    /// <summary>Публікує події, додані в цьому scope, — лише після успішного коміту транзакції.</summary>
    Task PublishPendingAsync(CancellationToken ct = default);
}
