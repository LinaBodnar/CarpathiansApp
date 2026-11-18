namespace Carpathians.Reviews.Application.Common.Interfaces;

/// <summary>
/// Перерахунок похідних даних у колекції <c>ratings</c> після зміни відгуків
/// (політика дублювання: avgRating і count перераховуються агрегацією).
/// </summary>
public interface IRatingService
{
    Task RecalculateAsync(long routeId, CancellationToken ct = default);
}
