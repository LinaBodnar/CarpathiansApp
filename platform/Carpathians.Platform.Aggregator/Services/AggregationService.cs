using System.Diagnostics;
using System.Text.Json;
using Carpathians.Platform.Aggregator.Clients;
using Carpathians.Platform.Aggregator.Contracts;
using Carpathians.Platform.Aggregator.Models;
using Polly;

namespace Carpathians.Platform.Aggregator.Services;

/// <summary>
/// Aggregator Pattern: паралельно опитує мікросервіси (Task.WhenAll) і збирає одну відповідь.
/// Недоступність необов'язкового сервісу не «валить» запит — дані цього сервісу відсутні,
/// а в Warnings з'являється попередження.
/// </summary>
public class AggregationService(
    BookingClient booking,
    CatalogClient catalog,
    ReviewsClient reviews,
    ILogger<AggregationService> logger)
{
    public const int LatestReviewsCount = 3;

    /// <summary>Власні spans агрегації (видно в trace разом з паралельними викликами сервісів).</summary>
    private static readonly ActivitySource Telemetry = new("Carpathians.Aggregator");

    private static readonly string[] DifficultyNames = { "Easy", "Moderate", "Hard" };

    /// <summary>Повна картка бронювання. null — бронювання не існує.</summary>
    public async Task<FullBookingDto?> GetFullBookingAsync(long bookingId, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("AggregateFullBooking");
        activity?.SetTag("booking.id", bookingId);
        var warnings = new Warnings();

        // 1. Бронювання — без нього агрегувати нічого (обов'язковий виклик).
        BookingResponse? bookingData;
        try
        {
            bookingData = await booking.GetBookingAsync(bookingId, ct);
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogError(ex, "{Service} is unavailable, booking {BookingId} cannot be aggregated",
                booking.ServiceName, bookingId);
            throw new UpstreamUnavailableException(booking.ServiceName, ex);
        }

        if (bookingData is null)
            return null;

        // 2. Клієнт і виїзди позицій бронювання (booking-api) — паралельно.
        var departureIds = bookingData.Items.Select(i => i.DepartureId).Distinct().ToList();
        var customerTask = SafeAsync(booking.ServiceName, $"customer {bookingData.CustomerId}",
            () => booking.GetCustomerAsync(bookingData.CustomerId, ct), warnings, ct);
        var departureTasks = departureIds.ToDictionary(id => id, id => SafeAsync(booking.ServiceName,
            $"departure {id}", () => booking.GetDepartureAsync(id, ct), warnings, ct));

        await Task.WhenAll(departureTasks.Values.Cast<Task>().Append(customerTask));

        // Узгодженість: беремо лише виїзди, які справді належать позиціям цього бронювання.
        var departures = departureTasks
            .Select(t => t.Value.Result)
            .Where(d => d is not null && departureIds.Contains(d.Id))
            .ToDictionary(d => d!.Id, d => d!);

        // 3. Маршрути з Каталогу + рейтинги й відгуки з Відгуків — усі виклики одночасно.
        var routeIds = departures.Values.Select(d => d.RouteId).Distinct().ToList();
        activity?.SetTag("route.ids", string.Join(",", routeIds));

        var routeTasks = routeIds.ToDictionary(id => id, id => SafeAsync(catalog.ServiceName,
            $"route {id}", () => catalog.GetRouteAsync(id, ct), warnings, ct));
        var ratingTasks = routeIds.ToDictionary(id => id, id => SafeAsync(reviews.ServiceName,
            $"rating of route {id}", () => reviews.GetRouteRatingAsync(id, ct), warnings, ct));
        var reviewTasks = routeIds.ToDictionary(id => id, id => SafeAsync(reviews.ServiceName,
            $"reviews of route {id}", () => reviews.GetLatestReviewsAsync(id, LatestReviewsCount, ct), warnings, ct));

        await Task.WhenAll(routeTasks.Values.Cast<Task>()
            .Concat(ratingTasks.Values)
            .Concat(reviewTasks.Values));

        // 4. Композиція: одна поїздка на кожну позицію бронювання.
        var trips = bookingData.Items.Select(item =>
        {
            departures.TryGetValue(item.DepartureId, out var departure);
            var routeId = departure?.RouteId;

            var route = routeId is null ? null : routeTasks[routeId.Value].Result;
            if (route is not null && route.Id != routeId)
            {
                warnings.Add($"{catalog.ServiceName} returned data for another route ({route.Id} instead of {routeId})");
                route = null;
            }

            return new TripDto(
                item.DepartureId,
                item.Seats,
                item.UnitPrice,
                item.RouteName,
                departure?.DepartureDate,
                routeId,
                route is null ? null : ToRouteInfo(route),
                routeId is null ? null : ToRating(ratingTasks[routeId.Value].Result),
                routeId is null ? null : ToSnippets(reviewTasks[routeId.Value].Result));
        }).ToList();

        var customer = customerTask.Result;
        var result = new FullBookingDto(
            new BookingSummaryDto(bookingData.Id, bookingData.Status, bookingData.BookingDate,
                bookingData.TotalAmount, bookingData.MeetingPoint, bookingData.Comment),
            customer is null ? null : new CustomerSummaryDto(customer.Id, customer.FullName, customer.Email),
            trips,
            warnings.ToList());

        activity?.SetTag("aggregation.complete", result.IsComplete);
        logger.LogInformation("Booking {BookingId} aggregated: {TripsCount} trips, complete {IsComplete}, warnings {Warnings}",
            bookingId, trips.Count, result.IsComplete, result.Warnings);
        return result;
    }

    /// <summary>Огляд маршруту: Каталог і Відгуки паралельно. null — маршруту немає ніде.</summary>
    public async Task<RouteOverviewDto?> GetRouteOverviewAsync(long routeId, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("AggregateRouteOverview");
        activity?.SetTag("route.id", routeId);
        var warnings = new Warnings();

        var routeTask = SafeAsync(catalog.ServiceName, $"route {routeId}",
            () => catalog.GetRouteAsync(routeId, ct), warnings, ct);
        var ratingTask = SafeAsync(reviews.ServiceName, $"rating of route {routeId}",
            () => reviews.GetRouteRatingAsync(routeId, ct), warnings, ct);
        var reviewsTask = SafeAsync(reviews.ServiceName, $"reviews of route {routeId}",
            () => reviews.GetLatestReviewsAsync(routeId, LatestReviewsCount, ct), warnings, ct);

        await Task.WhenAll(routeTask, ratingTask, reviewsTask);

        var route = routeTask.Result;
        var rating = ratingTask.Result;
        if (route is null && rating is null && warnings.Count == 0)
            return null;

        return new RouteOverviewDto(routeId,
            route is null ? null : ToRouteInfo(route),
            ToRating(rating),
            ToSnippets(reviewsTask.Result),
            warnings.ToList());
    }

    /// <summary>
    /// Виклик необов'язкового сервісу: тривалість логується, недоступність перетворюється
    /// на попередження (default замість винятку). Скасування клієнтом прокидається далі.
    /// </summary>
    private async Task<T?> SafeAsync<T>(string service, string resource, Func<Task<T?>> call,
        Warnings warnings, CancellationToken ct) where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await call();
        }
        catch (Exception ex) when (IsUnavailable(ex, ct))
        {
            logger.LogWarning(ex, "{Service} is unavailable for {Resource} after {ElapsedMs} ms: {Error}",
                service, resource, sw.ElapsedMilliseconds, ex.Message);
            warnings.Add($"{service} is unavailable");
            return null;
        }
    }

    /// <summary>Мережеві збої, таймаути, відмови resilience-pipeline і некоректні відповіді.</summary>
    private static bool IsUnavailable(Exception ex, CancellationToken ct)
        => !ct.IsCancellationRequested
           && ex is HttpRequestException or TaskCanceledException or TimeoutException
               or ExecutionRejectedException or JsonException;

    private static RouteInfoDto ToRouteInfo(RouteResponse r) => new(
        r.Id, r.Name, r.RegionName, DifficultyName(r.Difficulty), r.DistanceKm, r.DurationHours,
        r.ElevationGainM, r.Detail?.Season, r.Detail?.MaxAltitudeM);

    private static RatingDto? ToRating(RouteRatingResponse? rating)
        => rating is null ? null : new RatingDto(rating.AvgRating, rating.Count, rating.Distribution);

    private static IReadOnlyList<ReviewSnippetDto>? ToSnippets(IReadOnlyList<ReviewResponse>? items)
        => items?.Select(r => new ReviewSnippetDto(r.AuthorName, r.Rating, r.Text, r.CreatedAt)).ToList();

    /// <summary>Каталог серіалізує enum складності числом; підтримано й рядок.</summary>
    private static string DifficultyName(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetInt32(out var i) && i >= 0 && i < DifficultyNames.Length
            => DifficultyNames[i],
        JsonValueKind.String => value.GetString() ?? "Unknown",
        _ => "Unknown"
    };

    /// <summary>Потокобезпечний набір попереджень (паралельні виклики), без дублікатів.</summary>
    private sealed class Warnings
    {
        private readonly List<string> _items = new();

        public int Count { get { lock (_items) return _items.Count; } }

        public void Add(string warning)
        {
            lock (_items)
            {
                if (!_items.Contains(warning))
                    _items.Add(warning);
            }
        }

        public List<string> ToList() { lock (_items) return _items.ToList(); }
    }
}
