using Carpathians.Reviews.Domain.Entities;
using MongoDB.Bson;

namespace Carpathians.Reviews.Domain.Interfaces;

/// <summary>Репозиторій агрегата <see cref="Review"/> (операції запису). Реалізація — в Infrastructure.</summary>
public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>Чи залишав користувач відгук на цей маршрут (один відгук від автора на маршрут).</summary>
    Task<bool> ExistsAsync(long routeId, string authorId, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);

    /// <summary>
    /// Оновлює документ лише якщо версія в БД дорівнює <see cref="Review.Version"/>;
    /// інакше кидає ConflictException (оптимістична конкурентність).
    /// </summary>
    Task UpdateAsync(Review review, CancellationToken ct = default);

    /// <summary>Видаляє відгук з перевіркою очікуваної версії; false — документ не знайдено.</summary>
    Task<bool> DeleteAsync(ObjectId id, int? expectedVersion, CancellationToken ct = default);

    /// <summary>Оновлення денормалізованої назви маршруту в усіх відгуках (подія з Каталогу).</summary>
    Task<long> UpdateRouteNameAsync(long routeId, string routeName, CancellationToken ct = default);
}
