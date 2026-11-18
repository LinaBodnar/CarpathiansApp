using Carpathians.Platform.EventBus.Events;

namespace Carpathians.Api.IntegrationEvents.Events;

// Власні копії контрактів подій Каталогу (як в eShop): Бронювання залежить лише від JSON-формату,
// а не від збірки Каталогу. Імена типів збігаються з BasicProperties.Type, який ставить Каталог.
// Нові поля в майбутніх версіях подій ігноруються (EventJson), відсутні — лишаються default.

public sealed record RouteCreatedIntegrationEvent(
    long RouteId,
    string Name,
    string Slug,
    int RegionId,
    string Difficulty) : IntegrationEvent;

public sealed record RouteRenamedIntegrationEvent(
    long RouteId,
    string OldName,
    string NewName) : IntegrationEvent;

public sealed record RouteDeletedIntegrationEvent(
    long RouteId,
    string Name) : IntegrationEvent;
