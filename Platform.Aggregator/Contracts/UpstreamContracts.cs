using System.Text.Json;

namespace Carpathians.Platform.Aggregator.Contracts;

// Відповіді мікросервісів у тому вигляді, в якому їх повертають API (JSON camelCase).
// Aggregator читає лише потрібні йому поля.

// booking-api (Лаб. 02)
public record BookingItemResponse(long DepartureId, string RouteName, decimal UnitPrice, int Seats);

public record BookingResponse(
    long Id, long CustomerId, string Status, DateTime BookingDate, decimal TotalAmount,
    string ContactPhone, string MeetingPoint, string? Comment, IReadOnlyList<BookingItemResponse> Items);

public record DepartureResponse(
    long Id, long RouteId, string? RouteName, DateTime DepartureDate, decimal Price, int SeatsTotal, int SeatsAvailable);

public record CustomerResponse(long Id, string Email, string FullName, string? Phone);

// catalog-api (Лаб. 03)
public record RouteDetailResponse(string Description, string? Season, int? MaxAltitudeM, string? StartPoint, string? Requirements);

public record RouteResponse(
    long Id, string RegionName, string Name, JsonElement Difficulty, decimal DistanceKm, decimal DurationHours,
    int ElevationGainM, RouteDetailResponse? Detail);

// reviews-api (Лаб. 04)
public record RouteRatingResponse(long RouteId, string? RouteName, double AvgRating, int Count,
    IReadOnlyDictionary<string, int>? Distribution);

public record ReviewResponse(string Id, long RouteId, string AuthorName, int Rating, string Text,
    int CommentsCount, DateTime CreatedAt);

public record PagedResponse<T>(IReadOnlyList<T> Items, long TotalCount);
