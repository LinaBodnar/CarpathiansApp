using Carpathians.Dal.Data;
using Carpathians.Dal.Outbox;
using Carpathians.Platform.EventBus.Publishing;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Catalog.Api.IntegrationEvents;

/// <summary>
/// Фонова частина transactional outbox: періодично публікує події, що лишилися неопублікованими
/// (застосунок упав між комітом і публікацією або брокер був недоступний).
/// <para>Вибираються лише записи, старші за <see cref="GracePeriod"/>, — свіжі події публікує сам запит
/// одразу після коміту. Якщо подію все ж буде опубліковано двічі, споживачі відкинуть дублікат за EventId.</para>
/// </summary>
public sealed class CatalogOutboxPublisher(
    IServiceScopeFactory scopeFactory,
    ILogger<CatalogOutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(15);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox scan failed, next attempt in {IntervalSeconds} s", Interval.TotalSeconds);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false) is true);
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var threshold = DateTime.UtcNow - GracePeriod;
        var entries = await db.IntegrationEventLog
            .Where(e => e.State != IntegrationEventState.Published && e.OccurredAt < threshold)
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (entries.Count == 0)
            return;

        logger.LogInformation("Outbox: publishing {Count} pending integration events", entries.Count);

        foreach (var entry in entries)
            await CatalogIntegrationEventService.PublishEntryAsync(db, publisher, entry, logger, ct);

        await db.SaveChangesAsync(ct);
    }
}
