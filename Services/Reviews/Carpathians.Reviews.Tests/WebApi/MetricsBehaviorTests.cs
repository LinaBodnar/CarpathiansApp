using System.Diagnostics.Metrics;
using Carpathians.Reviews.Application.Reviews.Commands.CreateReview;
using Carpathians.Reviews.Application.Reviews.Commands.DeleteReview;
using Carpathians.Reviews.Application.Reviews.Queries.GetReviewById;
using Carpathians.Reviews.Application.Common.Models;
using Carpathians.Reviews.WebApi.Metrics;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Carpathians.Reviews.Tests.WebApi;

/// <summary>MetricsBehavior: тривалість кожної команди/запиту (і невдалої), бізнес-лічильник лише після успіху.</summary>
[TestFixture]
public class MetricsBehaviorTests
{
    private ServiceProvider _services = null!;
    private ReviewsMetrics _metrics = null!;
    private MeterListener _listener = null!;
    private List<(string Name, double Value, Dictionary<string, object?> Tags)> _measurements = null!;

    [SetUp]
    public void SetUp()
    {
        _services = new ServiceCollection()
            .AddMetrics()
            .AddSingleton<ReviewsStatisticsSnapshot>()
            .AddSingleton<ReviewsMetrics>()
            .BuildServiceProvider();
        _metrics = _services.GetRequiredService<ReviewsMetrics>();
        _measurements = [];

        var factory = _services.GetRequiredService<IMeterFactory>();
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ReviewsMetrics.MeterName && instrument.Meter.Scope == factory)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((i, v, t, _) => Add(i, v, t));
        _listener.SetMeasurementEventCallback<double>((i, v, t, _) => Add(i, v, t));
        _listener.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _listener.Dispose();
        _services.Dispose();
    }

    [Test]
    public async Task Successful_command_records_duration_and_counts_review_by_rating()
    {
        var behavior = new MetricsBehavior<CreateReviewCommand, string>(_metrics);

        await behavior.Handle(new CreateReviewCommand(1, "Hoverla", "u1", "Olena", 5, "Great view"),
            _ => Task.FromResult("id"), CancellationToken.None);

        var duration = _measurements.Single(m => m.Name == "reviews.operation.duration");
        Assert.That(duration.Tags["operation"], Is.EqualTo("create_review"));
        Assert.That(duration.Tags["operation.kind"], Is.EqualTo("command"));
        Assert.That(duration.Tags["status"], Is.EqualTo("success"));

        var created = _measurements.Single(m => m.Name == "reviews.created");
        Assert.That(created.Tags["review.rating"], Is.EqualTo("5"));
    }

    [Test]
    public async Task Query_is_measured_as_query_without_business_counter()
    {
        var behavior = new MetricsBehavior<GetReviewByIdQuery, ReviewDetailsDto>(_metrics);

        await behavior.Handle(new GetReviewByIdQuery("id"), _ => Task.FromResult<ReviewDetailsDto>(null!), CancellationToken.None);

        var duration = _measurements.Single();
        Assert.That(duration.Name, Is.EqualTo("reviews.operation.duration"));
        Assert.That(duration.Tags["operation"], Is.EqualTo("get_review_by_id"));
        Assert.That(duration.Tags["operation.kind"], Is.EqualTo("query"));
    }

    [Test]
    public void Failed_command_records_failure_and_is_not_counted()
    {
        var behavior = new MetricsBehavior<DeleteReviewCommand, Unit>(_metrics);

        Assert.ThrowsAsync<FluentValidation.ValidationException>(() => behavior.Handle(new DeleteReviewCommand("bad"),
            _ => throw new FluentValidation.ValidationException("invalid id"), CancellationToken.None));

        var duration = _measurements.Single();
        Assert.That(duration.Tags["status"], Is.EqualTo("failure"));
        Assert.That(duration.Tags["error.type"], Is.EqualTo("validation"));
        Assert.That(_measurements.Any(m => m.Name == "reviews.deleted"), Is.False);
    }

    private void Add<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags) where T : struct
        => _measurements.Add((instrument.Name, Convert.ToDouble(value), new Dictionary<string, object?>(tags.ToArray())));
}
