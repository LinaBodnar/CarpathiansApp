using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.ValueObjects;
using MongoDB.Bson;

namespace Carpathians.Reviews.Domain.Entities;

/// <summary>
/// Відгук про маршрут — корінь агрегата, документ колекції <c>reviews</c>.
/// <list type="bullet">
/// <item>embedding: <see cref="Author"/>, <see cref="Comments"/>, <see cref="Weather"/>, <see cref="GpsTrack"/>;</item>
/// <item>referencing: <see cref="RouteId"/> (Каталог, без FK), <see cref="DiscussionId"/> (колекція discussions);</item>
/// <item>денормалізація: <see cref="RouteName"/> — копія назви маршруту з Каталогу для читання;</item>
/// <item>гнучка схема: photos, tags, gpsTrack, weather, isVerifiedParticipant є лише в частині документів.</item>
/// </list>
/// </summary>
public class Review : BaseEntity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;
    public const int MaxTextLength = 5000;
    public const int MaxRouteNameLength = 200;
    public const int MaxPhotos = 20;
    public const int MaxTags = 10;
    public const int MaxGpsPoints = 5000;

    // поля не readonly: драйвер MongoDB заповнює їх під час десеріалізації (BsonClassMap.MapField)
    private List<Comment> _comments = new();
    private List<string> _photos = new();
    private List<string> _tags = new();
    private List<GeoPoint> _gpsTrack = new();

    public long RouteId { get; private set; }
    public string RouteName { get; private set; } = string.Empty;
    public Author Author { get; private set; } = null!;
    public int Rating { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public WeatherInfo? Weather { get; private set; }
    public ObjectId? DiscussionId { get; private set; }
    public bool? IsVerifiedParticipant { get; private set; }

    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();
    public IReadOnlyCollection<string> Photos => _photos.AsReadOnly();
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();
    public IReadOnlyCollection<GeoPoint> GpsTrack => _gpsTrack.AsReadOnly();

    private Review() { }                                    // для десеріалізації

    public Review(long routeId, string routeName, Author author, int rating, string text)
    {
        if (routeId <= 0)
            throw new DomainException("RouteId must be positive");

        Author = author ?? throw new DomainException("Review author is required");
        RouteId = routeId;
        SetRouteName(routeName);
        SetRating(rating);
        SetText(text);
    }

    /// <summary>Редагування відгуку автором: нова оцінка й текст.</summary>
    public void Update(int rating, string text)
    {
        SetRating(rating);
        SetText(text);
        Touch();
    }

    public void AddComment(Author author, string text)
    {
        _comments.Add(new Comment(author, text, DateTime.UtcNow));
        Touch();
    }

    public void SetPhotos(IEnumerable<string>? photos)
    {
        var items = Normalize(photos);
        if (items.Count > MaxPhotos)
            throw new DomainException($"A review can contain at most {MaxPhotos} photos");
        Replace(_photos, items);
        Touch();
    }

    public void SetTags(IEnumerable<string>? tags)
    {
        var items = Normalize(tags).Select(t => t.ToLowerInvariant()).Distinct().ToList();
        if (items.Count > MaxTags)
            throw new DomainException($"A review can contain at most {MaxTags} tags");
        Replace(_tags, items);
        Touch();
    }

    public void SetGpsTrack(IEnumerable<GeoPoint>? track)
    {
        var points = track?.ToList() ?? new List<GeoPoint>();
        if (points.Count == 1)
            throw new DomainException("GPS track must contain at least 2 points");
        if (points.Count > MaxGpsPoints)
            throw new DomainException($"GPS track must not exceed {MaxGpsPoints} points");
        Replace(_gpsTrack, points);
        Touch();
    }

    public void SetWeather(WeatherInfo? weather)
    {
        Weather = weather;
        Touch();
    }

    /// <summary>Посилання на обговорення (reference на колекцію discussions).</summary>
    public void LinkDiscussion(ObjectId discussionId)
    {
        if (discussionId == ObjectId.Empty)
            throw new DomainException("DiscussionId must be a valid ObjectId");
        DiscussionId = discussionId;
        Touch();
    }

    /// <summary>Позначка «перевірений учасник» — оновлюється подією про завершення поїздки з Бронювання.</summary>
    public void MarkVerifiedParticipant(bool verified = true)
    {
        IsVerifiedParticipant = verified;
        Touch();
    }

    /// <summary>Оновлення денормалізованої назви маршруту за подією з Каталогу.</summary>
    public void RenameRoute(string routeName)
    {
        SetRouteName(routeName);
        Touch();
    }

    private void SetRouteName(string routeName)
    {
        if (string.IsNullOrWhiteSpace(routeName))
            throw new DomainException("RouteName is required");
        if (routeName.Length > MaxRouteNameLength)
            throw new DomainException($"RouteName must not exceed {MaxRouteNameLength} characters");
        RouteName = routeName.Trim();
    }

    private void SetRating(int rating)
    {
        if (rating is < MinRating or > MaxRating)
            throw new DomainException($"Rating must be between {MinRating} and {MaxRating}");
        Rating = rating;
    }

    private void SetText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Review text is required");
        if (text.Length > MaxTextLength)
            throw new DomainException($"Review text must not exceed {MaxTextLength} characters");
        Text = text.Trim();
    }

    private static List<string> Normalize(IEnumerable<string>? values)
        => values?
               .Where(v => !string.IsNullOrWhiteSpace(v))
               .Select(v => v.Trim())
               .ToList()
           ?? new List<string>();

    private static void Replace<T>(List<T> target, IEnumerable<T> source)
    {
        target.Clear();
        target.AddRange(source);
    }
}
