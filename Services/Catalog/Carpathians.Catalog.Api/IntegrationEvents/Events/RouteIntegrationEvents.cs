using Carpathians.Platform.EventBus.Events;

namespace Carpathians.Catalog.Api.IntegrationEvents.Events;

// Integration events Каталогу — бізнес-факти про маршрути (назви в минулому часі).
// Незмінні record-и; кожна подія самодостатня: споживач не звертається до Каталогу по додаткові дані.
// Routing keys — CatalogTopology.RoutingKeys (route.created / route.renamed / route.deleted).

/// <summary>Маршрут створено: Бронювання додає його в довідник RouteRefs.</summary>
public sealed record RouteCreatedIntegrationEvent(
    long RouteId,
    string Name,
    string Slug,
    int RegionId,
    string Difficulty) : IntegrationEvent;

/// <summary>
/// Маршрут перейменовано: Бронювання оновлює RouteRefs.Name, Відгуки — routeName в усіх відгуках
/// і обговореннях маршруту. Знімки назви в оформлених бронюваннях (BookingItems.RouteName) не змінюються.
/// </summary>
public sealed record RouteRenamedIntegrationEvent(
    long RouteId,
    string OldName,
    string NewName) : IntegrationEvent;

/// <summary>Маршрут видалено (soft delete): Бронювання позначає копію в RouteRefs неактивною.</summary>
public sealed record RouteDeletedIntegrationEvent(
    long RouteId,
    string Name) : IntegrationEvent;
