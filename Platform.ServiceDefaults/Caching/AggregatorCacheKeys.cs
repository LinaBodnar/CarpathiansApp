namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Ключі агрегованих відповідей Aggregator у спільному Redis ("aggregator:{сценарій}:{id}").
/// Спільні для платформи: Aggregator кешує за ними відповіді, а мікросервіси видаляють їх
/// при зміні вихідних даних (бронювання, маршруту, відгуків), не чекаючи закінчення TTL.
/// </summary>
public static class AggregatorCacheKeys
{
    /// <summary>Повна картка бронювання (booking-api + catalog-api + reviews-api).</summary>
    public static string FullBooking(long bookingId) => $"aggregator:booking:{bookingId}:full";

    /// <summary>Огляд маршруту (catalog-api + reviews-api).</summary>
    public static string RouteOverview(long routeId) => $"aggregator:route:{routeId}:overview";
}
