using System.Diagnostics;
using System.Diagnostics.Metrics;
using Carpathians.Platform.ServiceDefaults.Metrics;

namespace Carpathians.Reviews.WebApi.Metrics;

/// <summary>
/// Бізнес-метрики сервісу відгуків (Meter "Reviews.Api", OpenTelemetry -> Aspire Dashboard).
/// <list type="bullet">
/// <item>лічильники: reviews.created (тег review.rating 1–5), reviews.updated, reviews.deleted, reviews.comments.added,
/// discussions.created, discussions.messages.posted;</item>
/// <item>reviews.operation.duration — тривалість кожної команди й запиту CQRS, успішних і невдалих
/// (теги operation, operation.kind = command | query, status, error.type);</item>
/// <item>gauge: reviews.current за оцінкою, discussions.current — знімок стану MongoDB.</item>
/// </list>
/// Значення тегу operation — назви типів команд і запитів (фіксований перелік), оцінка — лише 1–5.
/// </summary>
public sealed class ReviewsMetrics
{
    public const string MeterName = BusinessMeters.Reviews;

    public const string OperationKindTag = "operation.kind";
    private const string RatingTag = "review.rating";

    private readonly Counter<long> _reviewsCreated;
    private readonly Counter<long> _reviewsUpdated;
    private readonly Counter<long> _reviewsDeleted;
    private readonly Counter<long> _commentsAdded;
    private readonly Counter<long> _discussionsCreated;
    private readonly Counter<long> _messagesPosted;
    private readonly Histogram<double> _duration;

    public ReviewsMetrics(IMeterFactory meterFactory, ReviewsStatisticsSnapshot snapshot)
    {
        var meter = meterFactory.Create(MeterName);

        _reviewsCreated = meter.CreateCounter<long>("reviews.created", "{review}", "Created reviews by rating");
        _reviewsUpdated = meter.CreateCounter<long>("reviews.updated", "{review}", "Updated reviews");
        _reviewsDeleted = meter.CreateCounter<long>("reviews.deleted", "{review}", "Deleted reviews");
        _commentsAdded = meter.CreateCounter<long>("reviews.comments.added", "{comment}", "Comments added to reviews");
        _discussionsCreated = meter.CreateCounter<long>("discussions.created", "{discussion}", "Created route discussions");
        _messagesPosted = meter.CreateCounter<long>("discussions.messages.posted", "{message}", "Messages posted to discussions");

        _duration = meter.CreateHistogram<double>("reviews.operation.duration", "ms",
            "Duration of review commands and queries (successful and failed)");

        meter.CreateObservableGauge("reviews.current", () => ObserveReviews(snapshot), "{review}",
            "Current number of reviews by rating");
        meter.CreateObservableGauge("discussions.current", () => ObserveDiscussions(snapshot), "{discussion}",
            "Current number of route discussions");
    }

    public void ReviewCreated(int rating) => _reviewsCreated.Add(1, Rating(rating));

    public void ReviewUpdated() => _reviewsUpdated.Add(1);

    public void ReviewDeleted() => _reviewsDeleted.Add(1);

    public void CommentAdded() => _commentsAdded.Add(1);

    public void DiscussionCreated() => _discussionsCreated.Add(1);

    public void DiscussionMessagePosted() => _messagesPosted.Add(1);

    /// <summary>Виконує команду чи запит і записує тривалість для успіху й для збою; виняток прокидається далі.</summary>
    public async Task<T> MeasureAsync<T>(string operation, string operationKind, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await action();
            RecordDuration(operation, operationKind, started, null);
            return result;
        }
        catch (Exception ex)
        {
            RecordDuration(operation, operationKind, started, ex);
            throw;
        }
    }

    private void RecordDuration(string operation, string operationKind, long started, Exception? error)
    {
        var tags = MetricTags.ForOperation(operation, error);
        tags.Add(OperationKindTag, operationKind);
        _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
    }

    private static IEnumerable<Measurement<long>> ObserveReviews(ReviewsStatisticsSnapshot snapshot)
    {
        if (snapshot.Current is not { } stats)
            yield break;

        for (var rating = 1; rating <= 5; rating++)
            yield return new Measurement<long>(stats.ReviewsByRating.GetValueOrDefault(rating), Rating(rating));
    }

    private static IEnumerable<Measurement<long>> ObserveDiscussions(ReviewsStatisticsSnapshot snapshot)
    {
        if (snapshot.Current is { } stats)
            yield return new Measurement<long>(stats.Discussions);
    }

    /// <summary>Оцінка як тег: лише "1"–"5" (домен гарантує діапазон; інше — "other").</summary>
    private static KeyValuePair<string, object?> Rating(int rating)
        => new(RatingTag, rating is >= 1 and <= 5 ? rating.ToString() : "other");
}
