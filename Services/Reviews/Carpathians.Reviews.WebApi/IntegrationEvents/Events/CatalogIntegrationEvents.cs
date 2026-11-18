using Carpathians.Platform.EventBus.Events;

namespace Carpathians.Reviews.WebApi.IntegrationEvents.Events;

/// <summary>
/// Власна копія контракту події Каталогу (як в eShop): Відгуки залежать лише від JSON-формату.
/// Черга reviews.catalog-sync прив'язана тільки до route.renamed — іншим подіям Каталогу сюди не потрапити.
/// </summary>
public sealed record RouteRenamedIntegrationEvent(
    long RouteId,
    string OldName,
    string NewName) : IntegrationEvent;
