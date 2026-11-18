using System.Diagnostics;
using System.Diagnostics.Metrics;
using Carpathians.Bll.Dtos;
using Carpathians.Domain.Constants;
using Carpathians.Platform.ServiceDefaults.Metrics;

namespace Carpathians.Api.Metrics;

/// <summary>
/// Бізнес-метрики сервісу бронювань (Meter "Booking.Api", OpenTelemetry -> Aspire Dashboard, вкладка Metrics).
/// <list type="bullet">
/// <item>лічильники бізнес-подій: bookings.created / confirmed / paid / cancelled, заброньовані місця, сума бронювань;</item>
/// <item>bookings.operation.duration — тривалість кожної операції, і успішної, і невдалої (теги operation, status, error.type);</item>
/// <item>gauge: bookings.current — бронювання за статусами, departures.seats.available — вільні місця майбутніх виїздів.</item>
/// </list>
/// Теги мають фіксовані множини значень (статуси, способи оплати, операції) — без Id бронювань і клієнтів.
/// Реєструється як Singleton: Meter живе весь час роботи застосунку.
/// </summary>
public sealed class BookingMetrics
{
    public const string MeterName = BusinessMeters.Booking;

    /// <summary>Значення тегу operation.</summary>
    public static class Operations
    {
        public const string Get = "get";
        public const string Create = "create";
        public const string Confirm = "confirm";
        public const string Pay = "pay";
        public const string Cancel = "cancel";
        public const string List = "list";
        public const string Update = "update";
        public const string Delete = "delete";
    }

    private const string StatusTag = "booking.status";
    private const string PaymentMethodTag = "payment.method";

    private static readonly string[] Statuses =
    [
        BookingStatus.Submitted, BookingStatus.AwaitingValidation, BookingStatus.SeatsConfirmed,
        BookingStatus.Paid, BookingStatus.Completed, BookingStatus.Cancelled
    ];

    private static readonly string[] PaymentMethods = ["Card", "Cash", "Transfer"];

    private readonly Counter<long> _created;
    private readonly Counter<long> _confirmed;
    private readonly Counter<long> _paid;
    private readonly Counter<long> _cancelled;
    private readonly Counter<long> _seatsReserved;
    private readonly Counter<double> _amount;
    private readonly Histogram<double> _duration;

    public BookingMetrics(IMeterFactory meterFactory, BookingStatisticsSnapshot snapshot)
    {
        var meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>("bookings.created", "{booking}", "Number of created bookings");
        _confirmed = meter.CreateCounter<long>("bookings.confirmed", "{booking}", "Bookings with confirmed seats");
        _paid = meter.CreateCounter<long>("bookings.paid", "{booking}", "Paid bookings by payment method");
        _cancelled = meter.CreateCounter<long>("bookings.cancelled", "{booking}", "Cancelled bookings");
        _seatsReserved = meter.CreateCounter<long>("bookings.seats.reserved", "{seat}", "Seats reserved by created bookings");
        _amount = meter.CreateCounter<double>("bookings.amount", "UAH", "Total amount of created bookings");

        _duration = meter.CreateHistogram<double>("bookings.operation.duration", "ms",
            "Duration of booking operations (successful and failed)");

        // Значення читаються callback-ом у момент збирання метрик зі знімка, який оновлює BookingStatisticsRefresher.
        meter.CreateObservableGauge("bookings.current", () => ObserveByStatus(snapshot), "{booking}",
            "Current number of bookings by status");
        meter.CreateObservableGauge("departures.seats.available", () => ObserveSeats(snapshot), "{seat}",
            "Free seats in upcoming departures");
    }

    public void BookingCreated(BookingDto booking)
    {
        _created.Add(1, new KeyValuePair<string, object?>(StatusTag, KnownStatus(booking.Status)));
        _seatsReserved.Add(booking.Items.Sum(i => i.Seats));
        _amount.Add((double)booking.TotalAmount);
    }

    public void BookingConfirmed() => _confirmed.Add(1);

    public void BookingPaid(string method) => _paid.Add(1, new KeyValuePair<string, object?>(PaymentMethodTag,
        Array.Find(PaymentMethods, m => string.Equals(m, method, StringComparison.OrdinalIgnoreCase)) ?? "other"));

    public void BookingCancelled() => _cancelled.Add(1);

    /// <summary>
    /// Виконує операцію й записує її тривалість — і для успіху, і для збою (з категорією помилки),
    /// інакше картина продуктивності викривлена. Виняток прокидається далі без змін.
    /// </summary>
    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await action();
            RecordDuration(operation, started, null);
            return result;
        }
        catch (Exception ex)
        {
            RecordDuration(operation, started, ex);
            throw;
        }
    }

    public Task MeasureAsync(string operation, Func<Task> action)
        => MeasureAsync(operation, async () =>
        {
            await action();
            return true;
        });

    private void RecordDuration(string operation, long started, Exception? error)
        => _duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, MetricTags.ForOperation(operation, error));

    private static IEnumerable<Measurement<int>> ObserveByStatus(BookingStatisticsSnapshot snapshot)
    {
        if (snapshot.Current is not { } stats)
            yield break;                                   // знімка ще немає — не показуємо хибні нулі

        foreach (var status in Statuses)                   // фіксований перелік: невідомі значення з БД не створюють рядів
            yield return new Measurement<int>(stats.CountByStatus.GetValueOrDefault(status),
                new KeyValuePair<string, object?>(StatusTag, status));
    }

    private static IEnumerable<Measurement<int>> ObserveSeats(BookingStatisticsSnapshot snapshot)
    {
        if (snapshot.Current is { } stats)
            yield return new Measurement<int>(stats.UpcomingSeatsAvailable);
    }

    private static string KnownStatus(string status) => Array.IndexOf(Statuses, status) >= 0 ? status : "other";
}
