namespace Carpathians.Platform.EventBus.Topology;

/// <summary>
/// Топологія подій Каталогу (власник маршрутів). Каталог публікує в topic exchange <c>catalog-events</c>
/// і нічого не знає про споживачів; кожен сервіс-споживач має власну чергу з потрібними bindings.
/// <para>Копії даних, які оновлюються цими подіями (політика дублювання, README розділ 9):</para>
/// <list type="bullet">
/// <item>Бронювання — довідник <c>RouteRefs (Id, Name)</c>;</item>
/// <item>Відгуки — <c>reviews.routeName</c> і <c>discussions.routeName</c>.</item>
/// </list>
/// </summary>
public static class CatalogTopology
{
    public static readonly ExchangeDefinition Exchange = new("catalog-events");

    /// <summary>Routing keys у форматі &lt;сутність&gt;.&lt;подія&gt;.</summary>
    public static class RoutingKeys
    {
        public const string RouteCreated = "route.created";
        public const string RouteRenamed = "route.renamed";
        public const string RouteDeleted = "route.deleted";
    }

    /// <summary>
    /// Бронювання тримає повний довідник маршрутів: створення, перейменування і видалення —
    /// шаблон <c>route.*</c> (рівно одне слово після "route.").
    /// </summary>
    public static readonly ConsumerQueue BookingCatalogSync =
        new("booking.catalog-sync", Exchange, ["route.*"]);

    /// <summary>
    /// Відгукам потрібна лише назва маршруту — binding тільки на перейменування.
    /// Подію route.created ця черга не отримує зовсім: маршрутизацію виконує exchange.
    /// </summary>
    public static readonly ConsumerQueue ReviewsCatalogSync =
        new("reviews.catalog-sync", Exchange, [RoutingKeys.RouteRenamed]);
}
