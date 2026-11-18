using Carpathians.Reviews.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.WebApi.Metrics;

/// <summary>Знімок стану колекцій для gauge-метрик.</summary>
public sealed record ReviewsStatistics(IReadOnlyDictionary<int, long> ReviewsByRating, long Discussions);

/// <summary>
/// Останній знімок стану MongoDB. Callback gauge синхронний і викликається під час збирання метрик,
/// тому значення готує фоновий <see cref="ReviewsStatisticsRefresher"/>.
/// </summary>
public sealed class ReviewsStatisticsSnapshot
{
    private volatile ReviewsStatistics? _current;

    public ReviewsStatistics? Current => _current;

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void Update(ReviewsStatistics statistics)
    {
        _current = statistics;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// Оновлює знімок кожні <see cref="Interval"/>: агрегація $group за полем rating (5 груп) і оцінка кількості
/// обговорень за метаданими колекції (estimatedDocumentCount — без сканування). Недоступна MongoDB не зупиняє сервіс.
/// </summary>
public sealed class ReviewsStatisticsRefresher(
    MongoDbContext context,
    ReviewsStatisticsSnapshot snapshot,
    ILogger<ReviewsStatisticsRefresher> logger) : BackgroundService
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
            var groups = await context.ReviewDocuments.Aggregate()
                .Group(new BsonDocument
                {
                    { "_id", "$rating" },
                    { "count", new BsonDocument("$sum", 1) }
                })
                .ToListAsync(ct);

            var byRating = groups
                .Where(g => g["_id"].IsNumeric)
                .ToDictionary(g => g["_id"].ToInt32(), g => g["count"].ToInt64());
            var discussions = await context.Discussions.EstimatedDocumentCountAsync(cancellationToken: ct);

            snapshot.Update(new ReviewsStatistics(byRating, discussions));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Reviews statistics for gauge metrics were not refreshed, keeping snapshot from {UpdatedAt}",
                snapshot.UpdatedAt);
        }
    }
}
