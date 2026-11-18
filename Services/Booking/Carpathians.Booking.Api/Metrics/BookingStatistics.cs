using Carpathians.Dal.Interfaces;

namespace Carpathians.Api.Metrics;

/// <summary>
/// Останній знімок стану бронювань для gauge-метрик. Callback gauge не може звертатися до БД
/// (він синхронний і викликається під час збирання метрик), тому значення готуються заздалегідь.
/// </summary>
public sealed class BookingStatisticsSnapshot
{
    private volatile BookingStatistics? _current;

    /// <summary>null — знімок ще не отримано (наприклад, БД недоступна від старту).</summary>
    public BookingStatistics? Current => _current;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(BookingStatistics statistics)
    {
        _current = statistics;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Фоново оновлює знімок кожні <see cref="Interval"/>: два легкі агрегатні запити до SQL Server
/// замість запиту на кожен збір метрик. Недоступна БД не зупиняє сервіс — лишається попередній знімок.
/// </summary>
public sealed class BookingStatisticsRefresher(
    IServiceScopeFactory scopeFactory,
    BookingStatisticsSnapshot snapshot,
    ILogger<BookingStatisticsRefresher> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RefreshAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            snapshot.Update(await uow.Bookings.GetStatisticsAsync(ct));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Booking statistics for gauge metrics were not refreshed, keeping snapshot from {UpdatedAt}",
                snapshot.UpdatedAt);
        }
    }
}
