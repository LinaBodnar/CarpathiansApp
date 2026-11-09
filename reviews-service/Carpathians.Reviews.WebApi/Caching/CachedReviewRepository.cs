using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;

namespace Carpathians.Reviews.WebApi.Caching;

/// <summary>
/// Декоратор репозиторію відгуків: кожна успішна зміна (створення, оновлення, коментар, видалення,
/// синхронізація назви маршруту) інвалідує рейтинг і списки відгуків цього маршруту та топ маршрутів.
/// Інвалідація — на рівні запису в MongoDB, тож її не можна «забути» в жодному обробнику команд.
/// </summary>
public sealed class CachedReviewRepository(ReviewRepository inner, ICacheService cache) : IReviewRepository
{
    public Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);

    public Task<bool> ExistsAsync(long routeId, string authorId, CancellationToken ct = default)
        => inner.ExistsAsync(routeId, authorId, ct);

    public async Task AddAsync(Review review, CancellationToken ct = default)
    {
        await inner.AddAsync(review, ct);
        await InvalidateRouteAsync(review.RouteId, "ReviewCreated", ct);
    }

    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        await inner.UpdateAsync(review, ct);
        await InvalidateRouteAsync(review.RouteId, "ReviewUpdated", ct);
    }

    public async Task<bool> DeleteAsync(ObjectId id, int? expectedVersion, CancellationToken ct = default)
    {
        // Маршрут відгуку потрібен для інвалідації — читаємо до видалення.
        var review = await inner.GetByIdAsync(id, ct);
        var deleted = await inner.DeleteAsync(id, expectedVersion, ct);
        if (deleted && review is not null)
            await InvalidateRouteAsync(review.RouteId, "ReviewDeleted", ct);
        return deleted;
    }

    public async Task<long> UpdateRouteNameAsync(long routeId, string routeName, CancellationToken ct = default)
    {
        var updated = await inner.UpdateRouteNameAsync(routeId, routeName, ct);
        await InvalidateRouteAsync(routeId, "RouteNameSynced", ct);
        return updated;
    }

    private Task InvalidateRouteAsync(long routeId, string reason, CancellationToken ct)
        => cache.InvalidateAsync(reason, ct,
            ReviewsCacheKeys.RouteRating(routeId),
            ReviewsCacheKeys.RouteVersion(routeId),
            ReviewsCacheKeys.TopRatedVersion);
}
