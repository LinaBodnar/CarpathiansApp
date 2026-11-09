namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Кеш платформи за схемою cache-aside: перевірити кеш -> у разі промаху прочитати джерело (БД)
/// -> записати в кеш. Ключі мають передбачувану схему "{сервіс}:{сутність}:{id}" (catalog:route:42),
/// щоб їх легко було інвалідувати.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Значення з кешу або з <paramref name="factory"/> (джерело даних), якщо в кеші його немає.
    /// null від джерела не кешується. Винятки джерела (наприклад, «не знайдено») прокидаються далі.
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        CachePolicy policy,
        CancellationToken ct = default) where T : class;

    /// <summary>
    /// Видалити записи після зміни даних. Викликається синхронно з операцією запису
    /// (після SaveChanges, до відповіді клієнту), інакше клієнт може отримати застарілі дані.
    /// </summary>
    Task InvalidateAsync(IReadOnlyCollection<string> keys, string reason, CancellationToken ct = default);
}

/// <summary>Версія групи ключів (наприклад, усіх списків маршрутів) для масової інвалідації.</summary>
public sealed record CacheVersion(string Value);

public static class CacheServiceExtensions
{
    /// <summary>
    /// Поточна версія групи ключів. Ключі списків містять версію ("catalog:routes:v{версія}:page:1"),
    /// тож інвалідація ключа-версії робить застарілими одразу всі списки: старі записи більше
    /// ніхто не читає, і вони зникають за своїм TTL.
    /// </summary>
    public static async Task<string> GetVersionAsync(
        this ICacheService cache, string versionKey, CachePolicy policy, CancellationToken ct = default)
    {
        var version = await cache.GetOrCreateAsync(versionKey,
            _ => Task.FromResult<CacheVersion?>(new CacheVersion(Guid.NewGuid().ToString("N")[..8])),
            policy, ct);
        return version!.Value;
    }

    public static Task InvalidateAsync(this ICacheService cache, string reason, CancellationToken ct, params string[] keys)
        => cache.InvalidateAsync(keys, reason, ct);
}
