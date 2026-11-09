namespace Carpathians.Platform.Aggregator.Models;

// Агреговані DTO відображають бізнес-сценарій «картка бронювання» / «огляд маршруту»,
// а не копіюють структури окремих сервісів. Null-поля не серіалізуються.

/// <summary>Повна картка бронювання: дані booking-api, catalog-api і reviews-api в одній відповіді.</summary>
public record FullBookingDto(
    BookingSummaryDto Booking,
    CustomerSummaryDto? Customer,
    IReadOnlyList<TripDto> Trips,
    IReadOnlyList<string> Warnings)
{
    /// <summary>true — усі сервіси відповіли; false — частина даних відсутня (див. Warnings).</summary>
    public bool IsComplete => Warnings.Count == 0;
}

public record BookingSummaryDto(
    long Id, string Status, DateTime BookingDate, decimal TotalAmount, string MeetingPoint, string? Comment);

public record CustomerSummaryDto(long Id, string FullName, string Email);

/// <summary>Одна поїздка бронювання: виїзд + актуальні дані маршруту + рейтинг і свіжі відгуки.</summary>
public record TripDto(
    long DepartureId,
    int Seats,
    decimal UnitPrice,
    string BookedRouteName,          // знімок назви на момент бронювання (booking-api)
    DateTime? DepartureDate,
    long? RouteId,
    RouteInfoDto? Route,
    RatingDto? Rating,
    IReadOnlyList<ReviewSnippetDto>? LatestReviews);

/// <summary>Актуальні дані маршруту з Каталогу.</summary>
public record RouteInfoDto(
    long Id, string Name, string Region, string Difficulty, decimal DistanceKm, decimal DurationHours,
    int ElevationGainM, string? Season, int? MaxAltitudeM);

public record RatingDto(double AvgRating, int ReviewsCount, IReadOnlyDictionary<string, int>? Distribution);

public record ReviewSnippetDto(string AuthorName, int Rating, string Text, DateTime CreatedAt);

/// <summary>Огляд маршруту: Каталог + рейтинг + свіжі відгуки (два сервіси паралельно).</summary>
public record RouteOverviewDto(
    long RouteId,
    RouteInfoDto? Route,
    RatingDto? Rating,
    IReadOnlyList<ReviewSnippetDto>? LatestReviews,
    IReadOnlyList<string> Warnings)
{
    public bool IsComplete => Warnings.Count == 0;
}
