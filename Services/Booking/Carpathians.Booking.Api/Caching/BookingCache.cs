using Carpathians.Platform.ServiceDefaults.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Carpathians.Api.Caching;

/// <summary>
/// Схема ключів кешу сервісу бронювань: "booking:{сутність}:{id}".
/// Префікс "booking:" ізолює ключі від інших сервісів у спільному Redis.
/// </summary>
public static class BookingCacheKeys
{
    public static string Departure(long id) => $"booking:departure:{id}";

    public const string DeparturesAll = "booking:departures:all";

    public static string Customer(long id) => $"booking:customer:{id}";

    public static string Booking(long id) => $"booking:booking:{id}";
}

/// <summary>
/// TTL даних сервісу бронювань. На відміну від Каталогу, дані тут змінюються часто
/// (вільні місця при кожному бронюванні), а клієнти й бронювання містять персональні дані
/// (email, телефон) — тому лише короткі TTL, а зміни інвалідують записи одразу.
/// </summary>
public static class BookingCachePolicies
{
    /// <summary>Виїзд: ціна й дата стабільні, вільні місця змінюються — інвалідація при бронюванні/скасуванні.</summary>
    public static readonly CachePolicy Departure = new()
    {
        LocalTtl = TimeSpan.FromSeconds(30),
        DistributedTtl = TimeSpan.FromMinutes(5)
    };

    public static readonly CachePolicy DeparturesAll = new()
    {
        LocalTtl = TimeSpan.FromSeconds(30),
        DistributedTtl = TimeSpan.FromMinutes(2),
        Priority = CacheItemPriority.Low
    };

    /// <summary>Клієнт — персональні дані: короткий TTL на обох рівнях.</summary>
    public static readonly CachePolicy Customer = new()
    {
        LocalTtl = TimeSpan.FromSeconds(30),
        DistributedTtl = TimeSpan.FromMinutes(2)
    };

    /// <summary>Бронювання — персональні дані й статус, що змінюється: найкоротший TTL.</summary>
    public static readonly CachePolicy Booking = new()
    {
        LocalTtl = TimeSpan.FromSeconds(15),
        DistributedTtl = TimeSpan.FromMinutes(1)
    };
}
