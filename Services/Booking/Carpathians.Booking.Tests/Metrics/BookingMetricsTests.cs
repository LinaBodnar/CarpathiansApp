using System.Diagnostics.Metrics;
using Carpathians.Api.Metrics;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Constants;
using Carpathians.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Carpathians.Tests.Metrics;

/// <summary>Бізнес-метрики бронювань: лічильники після успіху, тривалість для успіху й збою, gauge зі знімка.</summary>
[TestFixture]
public class BookingMetricsTests
{
    private ServiceProvider _services = null!;
    private BookingStatisticsSnapshot _snapshot = null!;
    private BookingMetrics _metrics = null!;
    private MeterListener _listener = null!;
    private List<(string Name, double Value, Dictionary<string, object?> Tags)> _measurements = null!;

    [SetUp]
    public void SetUp()
    {
        _services = new ServiceCollection()
            .AddMetrics()
            .AddSingleton<BookingStatisticsSnapshot>()
            .AddSingleton<BookingMetrics>()
            .BuildServiceProvider();
        _snapshot = _services.GetRequiredService<BookingStatisticsSnapshot>();
        _metrics = _services.GetRequiredService<BookingMetrics>();
        _measurements = [];

        // Слухаємо лише Meter цього тесту (Scope = фабрика цього контейнера) — тести не заважають один одному.
        var factory = _services.GetRequiredService<IMeterFactory>();
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == BookingMetrics.MeterName && instrument.Meter.Scope == factory)
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((i, v, t, _) => Add(i, v, t));
        _listener.SetMeasurementEventCallback<int>((i, v, t, _) => Add(i, v, t));
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
    public async Task Create_counts_booking_seats_and_amount_and_records_duration()
    {
        var inner = Substitute.For<IBookingService>();
        inner.CreateAsync(Arg.Any<CreateBookingDto>(), Arg.Any<CancellationToken>()).Returns(new BookingDto(
            1, 1, BookingStatus.Submitted, DateTime.UtcNow, 1500m, "+380000000", "Vorokhta", null,
            [new BookingItemDto(1, "Hoverla", 500m, 2), new BookingItemDto(2, "Petros", 250m, 2)]));

        await new MeteredBookingService(inner, _metrics).CreateAsync(new CreateBookingDto(), CancellationToken.None);

        var created = Single("bookings.created");
        Assert.That(created.Value, Is.EqualTo(1));
        Assert.That(created.Tags["booking.status"], Is.EqualTo(BookingStatus.Submitted));
        Assert.That(Single("bookings.seats.reserved").Value, Is.EqualTo(4));
        Assert.That(Single("bookings.amount").Value, Is.EqualTo(1500));

        var duration = Single("bookings.operation.duration");
        Assert.That(duration.Tags["operation"], Is.EqualTo("create"));
        Assert.That(duration.Tags["status"], Is.EqualTo("success"));
        Assert.That(duration.Tags.ContainsKey("error.type"), Is.False);
    }

    [Test]
    public void Failed_operation_records_duration_with_error_type_and_does_not_count_event()
    {
        var inner = Substitute.For<IBookingService>();
        inner.ConfirmAsync(42, Arg.Any<CancellationToken>()).ThrowsAsync(new BusinessConflictException("already paid"));

        Assert.ThrowsAsync<BusinessConflictException>(() => new MeteredBookingService(inner, _metrics).ConfirmAsync(42, CancellationToken.None));

        var duration = Single("bookings.operation.duration");
        Assert.That(duration.Tags["operation"], Is.EqualTo("confirm"));
        Assert.That(duration.Tags["status"], Is.EqualTo("failure"));
        Assert.That(duration.Tags["error.type"], Is.EqualTo("conflict"));
        Assert.That(_measurements.Any(m => m.Name == "bookings.confirmed"), Is.False);
    }

    [Test]
    public async Task Payment_method_tag_is_normalized_to_known_values()
    {
        var inner = Substitute.For<IBookingService>();
        inner.PayAsync(Arg.Any<long>(), Arg.Any<PayBookingDto>(), Arg.Any<CancellationToken>()).Returns(new PaymentResultDto(7));

        await new MeteredBookingService(inner, _metrics).PayAsync(1, new PayBookingDto { Method = "card" }, CancellationToken.None);

        Assert.That(Single("bookings.paid").Tags["payment.method"], Is.EqualTo("Card"));
    }

    [Test]
    public void Gauges_report_nothing_until_the_first_snapshot_then_every_known_status()
    {
        _listener.RecordObservableInstruments();
        Assert.That(_measurements, Is.Empty);

        _snapshot.Update(new BookingStatistics(
            new Dictionary<string, int> { [BookingStatus.Paid] = 3, ["Unexpected"] = 9 }, UpcomingSeatsAvailable: 42));
        _listener.RecordObservableInstruments();

        var byStatus = _measurements.Where(m => m.Name == "bookings.current").ToList();
        Assert.That(byStatus, Has.Count.EqualTo(6), "one series per known status, unknown values are ignored");
        Assert.That(byStatus.Single(m => (string?)m.Tags["booking.status"] == BookingStatus.Paid).Value, Is.EqualTo(3));
        Assert.That(Single("departures.seats.available").Value, Is.EqualTo(42));
    }

    private (string Name, double Value, Dictionary<string, object?> Tags) Single(string name)
        => _measurements.Single(m => m.Name == name);

    private void Add<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags) where T : struct
        => _measurements.Add((instrument.Name, Convert.ToDouble(value), new Dictionary<string, object?>(tags.ToArray())));
}
