using System.Globalization;
using Carpathians.Bll.Dtos;

namespace Carpathians.Catalog.Api.Caching;

/// <summary>
/// Схема ключів кешу Каталогу: "catalog:{сутність}:{id}". Префікс "catalog:" ізолює ключі
/// від інших сервісів у спільному Redis. Списки маршрутів містять версію групи
/// ("catalog:routes:v{версія}:..."), тож одна інвалідація ключа-версії скидає всі списки.
/// </summary>
public static class CatalogCacheKeys
{
    /// <summary>Картка маршруту (деталі, спорядження, точки).</summary>
    public static string Route(long id) => $"catalog:route:{id}";

    /// <summary>Ключ-версія всіх списків маршрутів (масова інвалідація).</summary>
    public const string RoutesVersion = "catalog:routes:version";

    public static string RoutesAll(string version) => $"catalog:routes:v{version}:all";

    public static string RoutesPopular(string version) => $"catalog:routes:v{version}:popular-challenging";

    /// <summary>Сторінка списку: усі параметри запиту входять у ключ.</summary>
    public static string RoutesPage(string version, RouteQueryParameters q) => string.Join(':',
        $"catalog:routes:v{version}:page",
        q.Page.ToString(CultureInfo.InvariantCulture),
        q.PageSize.ToString(CultureInfo.InvariantCulture),
        q.Search?.Trim().ToLowerInvariant() ?? "",
        q.RegionId?.ToString(CultureInfo.InvariantCulture) ?? "",
        q.Difficulty?.ToString() ?? "",
        q.MaxDistanceKm?.ToString(CultureInfo.InvariantCulture) ?? "",
        q.SortBy?.Trim().ToLowerInvariant() ?? "",
        q.SortDir?.Trim().ToLowerInvariant() ?? "");

    /// <summary>Довідник регіонів.</summary>
    public const string RegionsAll = "catalog:regions:all";

    public static string Region(int id) => $"catalog:region:{id}";
}
