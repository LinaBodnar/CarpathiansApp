using System.ComponentModel.DataAnnotations;

namespace Carpathians.Reviews.WebApi.Contracts;

// Контракти API з базовою модельною валідацією (DataAnnotations). Бізнес-валідація —
// у валідаторах FluentValidation через ValidationBehavior шару Application.

public class GeoPointRequest
{
    [Range(-90, 90)] public double Lat { get; set; }
    [Range(-180, 180)] public double Lng { get; set; }
    public double? Ele { get; set; }
}

public class WeatherRequest
{
    [Range(-60, 60)] public double? TempC { get; set; }
    [StringLength(30)] public string? Wind { get; set; }
    [StringLength(50)] public string? Conditions { get; set; }
}

/// <summary>Новий відгук про маршрут.</summary>
public class CreateReviewRequest
{
    /// <summary>Id маршруту в Каталозі (логічне посилання без FK).</summary>
    [Range(1, long.MaxValue)] public long RouteId { get; set; }

    /// <summary>Назва маршруту — денормалізована копія з Каталогу.</summary>
    [Required, StringLength(200)] public string RouteName { get; set; } = string.Empty;

    /// <summary>Id користувача з БД Бронювання.</summary>
    [Required, StringLength(64)] public string AuthorId { get; set; } = string.Empty;

    [Required, StringLength(100)] public string AuthorName { get; set; } = string.Empty;

    /// <summary>Оцінка від 1 до 5 (діапазон перевіряє FluentValidation).</summary>
    public int Rating { get; set; }

    [Required] public string Text { get; set; } = string.Empty;

    public List<string>? Photos { get; set; }
    public List<string>? Tags { get; set; }
    public List<GeoPointRequest>? GpsTrack { get; set; }
    public WeatherRequest? Weather { get; set; }
}

/// <summary>Редагування відгуку. Очікувану версію передавайте в заголовку If-Match.</summary>
public class UpdateReviewRequest
{
    public int Rating { get; set; }
    [Required] public string Text { get; set; } = string.Empty;
    public List<string>? Tags { get; set; }
}

public class AddCommentRequest
{
    [Required, StringLength(64)] public string AuthorId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string AuthorName { get; set; } = string.Empty;
    [Required] public string Text { get; set; } = string.Empty;
}

/// <summary>Нова назва маршруту — імітація події «маршрут перейменовано» з Каталогу.</summary>
public class SyncRouteNameRequest
{
    [Required, StringLength(200)] public string RouteName { get; set; } = string.Empty;
}

/// <summary>Відповідь на створення ресурсу: рядковий ObjectId.</summary>
public record CreatedResponse(string Id);

/// <summary>Кількість документів, оновлених операцією.</summary>
public record UpdatedCountResponse(long Updated);
