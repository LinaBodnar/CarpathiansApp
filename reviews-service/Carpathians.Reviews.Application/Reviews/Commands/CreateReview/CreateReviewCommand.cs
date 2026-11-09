using Carpathians.Reviews.Application.Common.Messaging;

namespace Carpathians.Reviews.Application.Reviews.Commands.CreateReview;

public record GeoPointInput(double Lat, double Lng, double? Ele);

public record WeatherInput(double? TempC, string? Wind, string? Conditions);

/// <summary>
/// Створення відгуку. Необов'язкові поля (photos, tags, gpsTrack, weather) демонструють
/// гнучку схему: у документ потрапляють лише ті, що передані.
/// </summary>
public record CreateReviewCommand(
    long RouteId,
    string RouteName,
    string AuthorId,
    string AuthorName,
    int Rating,
    string Text,
    IReadOnlyList<string>? Photos = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<GeoPointInput>? GpsTrack = null,
    WeatherInput? Weather = null) : ICommand<string>;
