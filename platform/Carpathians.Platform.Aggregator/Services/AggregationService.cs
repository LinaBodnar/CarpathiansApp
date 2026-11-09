using System.Diagnostics;
using Carpathians.Booking.Grpc;
using Carpathians.Catalog.Grpc;
using Carpathians.Grpc.Common;
using Carpathians.Platform.Aggregator.Caching;
using Carpathians.Platform.Aggregator.Models;
using Carpathians.Platform.ServiceDefaults.Caching;
using Carpathians.Reviews.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Carpathians.Platform.Aggregator.Services;

/// <summary>
/// Aggregator Pattern поверх gRPC (Лаб. 06): типізовані gRPC-клієнти мікросервісів,
/// незалежні виклики — паралельно (Task.WhenAll), мультиплексовані в одному HTTP/2-з'єднанні з кожним сервісом.
/// <list type="bullet">
/// <item>кеш агрегованих відповідей у Redis перевіряється ДО паралельних викликів; повні відповіді кешуються на 30 с;</item>
/// <item>часткові відмови: недоступний необов'язковий сервіс (RpcException UNAVAILABLE, DEADLINE_EXCEEDED...)
/// дає попередження у Warnings, а не помилку всього запиту; NOT_FOUND — це «даних немає», не відмова;</item>
/// <item>CancellationToken і deadline передаються в кожен виклик.</item>
/// </list>
/// Відповіді gRPC перетворюються на ті самі агреговані DTO, що й у Лаб. 05 (HTTP) — контракт для клієнтів не змінився.
/// </summary>
public class AggregationService(
    BookingGrpc.BookingGrpcClient booking,
    CatalogGrpc.CatalogGrpcClient catalog,
    ReviewsGrpc.ReviewsGrpcClient reviews,
    AggregatedResponseCache cache,
    ILogger<AggregationService> logger)
{
    public const int LatestReviewsCount = 3;

    /// <summary>Deadline одного gRPC-виклику: повільний сервіс не затримує всю агрегацію.</summary>
    public static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    private const string BookingService = "booking-api";
    private const string CatalogService = "catalog-api";
    private const string ReviewsService = "reviews-api";

    /// <summary>Власні spans агрегації (батьківські для spans gRPC-клієнтів).</summary>
    private static readonly ActivitySource Telemetry = new("Carpathians.Aggregator");

    // ---------- Повна картка бронювання ----------

    /// <summary>Повна картка бронювання. Value = null — бронювання не існує; FromCache — відповідь з Redis.</summary>
    public async Task<(FullBookingDto? Value, bool FromCache)> GetFullBookingAsync(long bookingId, CancellationToken ct)
    {
        var key = AggregatorCacheKeys.FullBooking(bookingId);
        if (await cache.GetAsync<FullBookingDto>(key, ct) is { } cached)
            return (cached, true);

        var result = await AggregateFullBookingAsync(bookingId, ct);
        await CacheIfCompleteAsync(key, result, result?.Warnings, ct);
        return (result, false);
    }

    /// <summary>
    /// Збирання картки бронювання через gRPC без кешу агрегованих відповідей
    /// (використовується й для вимірювання затримок HTTP vs gRPC). null — бронювання не існує.
    /// </summary>
    public async Task<FullBookingDto?> AggregateFullBookingAsync(long bookingId, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("AggregateFullBooking");
        activity?.SetTag("booking.id", bookingId);
        activity?.SetTag("aggregator.transport", "grpc");
        var warnings = new AggregationWarnings();

        // 1. Бронювання — обов'язкове: без нього агрегувати нічого.
        BookingReply bookingData;
        try
        {
            bookingData = await booking.GetBookingAsync(new GetBookingRequest { Id = bookingId }, Options(ct));
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "{Service} is unavailable ({GrpcStatus}), booking {BookingId} cannot be aggregated",
                BookingService, ex.StatusCode, bookingId);
            throw new UpstreamUnavailableException(BookingService, ex);
        }

        // 2. Клієнт і виїзди позицій (booking-api) — паралельно; виїзди — одним пакетним викликом.
        var departureIds = bookingData.Items.Select(i => i.DepartureId).Distinct().ToList();
        var customerTask = SafeAsync(BookingService, $"customer {bookingData.CustomerId}",
            token => booking.GetCustomerAsync(new GetCustomerRequest { Id = bookingData.CustomerId }, Options(token)).ResponseAsync,
            warnings, ct);
        var departuresTask = departureIds.Count == 0
            ? Task.FromResult<DeparturesReply?>(new DeparturesReply())
            : SafeAsync(BookingService, "departures",
                token => booking.GetDeparturesByIdsAsync(new GetDeparturesByIdsRequest { Ids = { departureIds } }, Options(token)).ResponseAsync,
                warnings, ct);

        await Task.WhenAll(customerTask, departuresTask);
        var customer = await customerTask;                           // задачі вже завершені — await без очікування
        var departures = (await departuresTask)?.Departures
            .Where(d => departureIds.Contains(d.Id))                 // узгодженість: лише виїзди цього бронювання
            .ToDictionary(d => d.Id) ?? new Dictionary<long, DepartureReply>();

        // 3. Маршрути (catalog-api), рейтинги й свіжі відгуки (reviews-api) — усі виклики одночасно.
        var routeIds = departures.Values.Select(d => d.RouteId).Distinct().ToList();
        activity?.SetTag("route.ids", string.Join(",", routeIds));

        var routesTask = routeIds.Count == 0
            ? Task.FromResult<RoutesReply?>(new RoutesReply())
            : SafeAsync(CatalogService, "routes",
                token => catalog.GetRoutesByIdsAsync(new GetRoutesByIdsRequest { Ids = { routeIds } }, Options(token)).ResponseAsync,
                warnings, ct);
        var ratingsTask = routeIds.Count == 0
            ? Task.FromResult<RouteRatingsReply?>(new RouteRatingsReply())
            : SafeAsync(ReviewsService, "ratings",
                token => reviews.GetRatingsForRoutesAsync(new GetRatingsForRoutesRequest { RouteIds = { routeIds } }, Options(token)).ResponseAsync,
                warnings, ct);
        var latestTasks = routeIds.ToDictionary(id => id, id => SafeAsync(ReviewsService, $"reviews of route {id}",
            token => reviews.GetLatestReviewsAsync(new GetLatestReviewsRequest { RouteId = id, Count = LatestReviewsCount }, Options(token)).ResponseAsync,
            warnings, ct));

        await Task.WhenAll(latestTasks.Values.Cast<Task>().Append(routesTask).Append(ratingsTask));

        var routesReply = await routesTask;
        var ratingsReply = await ratingsTask;
        var routes = routesReply?.Routes.ToDictionary(r => r.Id);
        var ratings = ratingsReply?.Ratings.ToDictionary(r => r.RouteId);
        var latest = new Dictionary<long, ReviewsReply?>(latestTasks.Count);
        foreach (var (routeId, task) in latestTasks)
            latest[routeId] = await task;

        // 4. Композиція: одна поїздка на кожну позицію бронювання.
        var trips = bookingData.Items.Select(item =>
        {
            departures.TryGetValue(item.DepartureId, out var departure);
            var routeId = departure?.RouteId;

            RouteReply? route = null;
            RouteRatingReply? rating = null;
            ReviewsReply? routeReviews = null;
            if (routeId is { } id)
            {
                routes?.TryGetValue(id, out route);
                ratings?.TryGetValue(id, out rating);
                latest.TryGetValue(id, out routeReviews);
            }

            return new TripDto(
                item.DepartureId,
                item.Seats,
                ToDecimal(item.UnitPrice),
                item.RouteName,
                departure?.DepartureDate?.ToDateTime(),
                routeId,
                route is null ? null : ToRouteInfo(route),
                rating is null ? null : ToRating(rating),
                routeId is null || routeReviews is null ? null : ToSnippets(routeReviews));
        }).ToList();

        var result = new FullBookingDto(
            new BookingSummaryDto(bookingData.Id, bookingData.Status, ToDateTime(bookingData.BookingDate),
                ToDecimal(bookingData.TotalAmount), bookingData.MeetingPoint, bookingData.Comment),
            customer is null ? null : new CustomerSummaryDto(customer.Id, customer.FullName, customer.Email),
            trips,
            warnings.ToList());

        activity?.SetTag("aggregation.complete", result.IsComplete);
        logger.LogInformation("Booking {BookingId} aggregated via gRPC: {TripsCount} trips, complete {IsComplete}, warnings {Warnings}",
            bookingId, trips.Count, result.IsComplete, result.Warnings);
        return result;
    }

    // ---------- Огляд маршруту ----------

    /// <summary>Огляд маршруту. Value = null — маршруту немає ніде; FromCache — відповідь з Redis.</summary>
    public async Task<(RouteOverviewDto? Value, bool FromCache)> GetRouteOverviewAsync(long routeId, CancellationToken ct)
    {
        var key = AggregatorCacheKeys.RouteOverview(routeId);
        if (await cache.GetAsync<RouteOverviewDto>(key, ct) is { } cached)
            return (cached, true);

        var result = await AggregateRouteOverviewAsync(routeId, ct);
        await CacheIfCompleteAsync(key, result, result?.Warnings, ct);
        return (result, false);
    }

    private async Task<RouteOverviewDto?> AggregateRouteOverviewAsync(long routeId, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("AggregateRouteOverview");
        activity?.SetTag("route.id", routeId);
        activity?.SetTag("aggregator.transport", "grpc");
        var warnings = new AggregationWarnings();

        var routeTask = SafeAsync(CatalogService, $"route {routeId}",
            token => catalog.GetRouteAsync(new GetRouteRequest { Id = routeId }, Options(token)).ResponseAsync, warnings, ct);
        var ratingTask = SafeAsync(ReviewsService, $"rating of route {routeId}",
            token => reviews.GetRouteRatingAsync(new GetRouteRatingRequest { RouteId = routeId }, Options(token)).ResponseAsync, warnings, ct);
        var reviewsTask = SafeAsync(ReviewsService, $"reviews of route {routeId}",
            token => reviews.GetLatestReviewsAsync(new GetLatestReviewsRequest { RouteId = routeId, Count = LatestReviewsCount }, Options(token)).ResponseAsync,
            warnings, ct);

        await Task.WhenAll(routeTask, ratingTask, reviewsTask);

        var route = await routeTask;
        var rating = await ratingTask;
        var latest = await reviewsTask;
        if (route is null && rating is null && warnings.Count == 0)
            return null;

        return new RouteOverviewDto(routeId,
            route is null ? null : ToRouteInfo(route),
            rating is null ? null : ToRating(rating),
            latest is null ? null : ToSnippets(latest),
            warnings.ToList());
    }

    // ---------- Допоміжне ----------

    private async Task CacheIfCompleteAsync<T>(string key, T? result, IReadOnlyList<string>? warnings, CancellationToken ct)
        where T : class
    {
        if (result is null)
            return;

        if (warnings is { Count: > 0 })
        {
            logger.LogInformation("Partial aggregated response {CacheKey} is not cached: {Warnings}", key, warnings);
            return;
        }

        await cache.SetAsync(key, result, ct);
    }

    /// <summary>Deadline і скасування — у кожен виклик.</summary>
    private static CallOptions Options(CancellationToken ct)
        => new(deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

    /// <summary>
    /// Виклик необов'язкового сервісу: NOT_FOUND -> null (даних немає), інші RpcException
    /// (UNAVAILABLE, DEADLINE_EXCEEDED, INTERNAL...) -> попередження й null замість помилки всього запиту.
    /// Скасування клієнтом прокидається далі.
    /// </summary>
    private async Task<T?> SafeAsync<T>(string service, string resource, Func<CancellationToken, Task<T>> call,
        AggregationWarnings warnings, CancellationToken ct) where T : class
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await call(ct);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Service} is unavailable for {Resource} after {ElapsedMs} ms: {GrpcStatus} {GrpcDetail}",
                service, resource, (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, ex.StatusCode, ex.Status.Detail);
            warnings.Add($"{service} is unavailable");
            return null;
        }
    }

    // ---------- Повідомлення gRPC -> агреговані DTO ----------

    private static RouteInfoDto ToRouteInfo(RouteReply r) => new(
        r.Id, r.Name, r.RegionName, DifficultyName(r.Difficulty), ToDecimal(r.DistanceKm), ToDecimal(r.DurationHours),
        r.ElevationGainM, r.Detail?.Season, r.Detail?.MaxAltitudeM);

    private static RatingDto ToRating(RouteRatingReply r)
        => new(r.AvgRating, r.ReviewsCount, r.Distribution.ToDictionary(d => d.Key, d => d.Value));

    private static IReadOnlyList<ReviewSnippetDto> ToSnippets(ReviewsReply reply)
        => reply.Reviews.Select(r => new ReviewSnippetDto(r.AuthorName, r.Rating, r.Text, ToDateTime(r.CreatedAt))).ToList();

    private static string DifficultyName(RouteDifficulty difficulty)
        => difficulty == RouteDifficulty.Unspecified ? "Unknown" : difficulty.ToString();

    private static decimal ToDecimal(DecimalValue? value) => value?.ToDecimal() ?? 0m;

    private static DateTime ToDateTime(Timestamp? value) => value?.ToDateTime() ?? default;
}
