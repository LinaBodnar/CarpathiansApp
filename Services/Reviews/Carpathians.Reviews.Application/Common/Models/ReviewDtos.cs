namespace Carpathians.Reviews.Application.Common.Models;

/// <summary>Короткий відгук для списків.</summary>
public record ReviewDto(
    string Id,
    long RouteId,
    string RouteName,
    string AuthorId,
    string AuthorName,
    int Rating,
    string Text,
    int CommentsCount,
    IReadOnlyList<string> Tags,
    bool? IsVerifiedParticipant,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version);

public record CommentDto(string AuthorId, string AuthorName, string Text, DateTime CreatedAt);

public record GeoPointDto(double Lat, double Lng, double? Ele);

public record WeatherDto(double? TempC, string? Wind, string? Conditions);

/// <summary>Повний відгук з усіма вбудованими даними.</summary>
public record ReviewDetailsDto(
    string Id,
    long RouteId,
    string RouteName,
    string AuthorId,
    string AuthorName,
    int Rating,
    string Text,
    IReadOnlyList<string> Photos,
    IReadOnlyList<string> Tags,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<GeoPointDto> GpsTrack,
    WeatherDto? Weather,
    string? DiscussionId,
    bool? IsVerifiedParticipant,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int Version);

/// <summary>Зведений рейтинг маршруту (колекція ratings / aggregation pipeline).</summary>
public record RouteRatingDto(
    long RouteId,
    string? RouteName,
    double AvgRating,
    int Count,
    IReadOnlyDictionary<string, int> Distribution);
