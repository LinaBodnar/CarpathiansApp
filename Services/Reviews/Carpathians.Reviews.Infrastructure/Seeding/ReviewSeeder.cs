using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.ValueObjects;
using Carpathians.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using static Carpathians.Reviews.Infrastructure.Seeding.SeedData;

namespace Carpathians.Reviews.Infrastructure.Seeding;

/// <summary>
/// Відгуки з різними наборами полів (гнучка схема): photos, comments, gpsTrack, weather, tags,
/// discussionId, isVerifiedParticipant є лише в частині документів.
/// </summary>
public class ReviewSeeder(MongoDbContext context, ILogger<ReviewSeeder> logger) : IDataSeeder
{
    public int Order => 2;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.Reviews.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
            return;                                                    // уже наповнено

        var poles = await FindDiscussionIdAsync(Route1, Discussion1Title, ct);
        var lake = await FindDiscussionIdAsync(Route2, Discussion2Title, ct);

        // 1) Повний відгук: photos + comments + discussionId + isVerifiedParticipant
        var full = new Review(Route1, Route1Name, Olena, 5, "Чудовий маршрут, з вершини відкривається неймовірна панорама.");
        full.SetPhotos(new[] { "hoverla-top-1.jpg", "hoverla-top-2.jpg" });
        full.AddComment(Andrii, "Згоден, краєвиди чудові");
        if (poles is not null) full.LinkDiscussion(poles.Value);
        full.MarkVerifiedParticipant();

        // 2) Мінімальний відгук + погода
        var windy = new Review(Route1, Route1Name, Andrii, 4, "Гарний підйом, але на вершині сильний вітер.");
        windy.SetWeather(new WeatherInfo(8, "strong", "cloudy"));
        windy.MarkVerifiedParticipant(false);

        // 3) GPS-трек + photos + tags + discussionId
        var withTrack = new Review(Route2, Route2Name, Maria, 5, "Озеро Несамовите варте окремого заходу.");
        withTrack.SetPhotos(new[] { "nesamovyte-1.jpg" });
        withTrack.SetGpsTrack(new[]
        {
            new GeoPoint(48.1660, 24.5460, 1050),
            new GeoPoint(48.1550, 24.5100, 1750),
            new GeoPoint(48.1601, 24.5004, 2061)
        });
        withTrack.SetTags(new[] { "lake", "photography" });
        if (lake is not null) withTrack.LinkDiscussion(lake.Value);

        // 4) Коментарі й теги, без фото
        var longDay = new Review(Route3, Route3Name, Olena, 3, "Дуже довгий день, взяла замало води.");
        longDay.SetTags(new[] { "long", "water" });
        longDay.AddComment(Maria, "Так, води потрібно щонайменше 2 літри");
        longDay.AddComment(Andrii, "Дякую за пораду для наступного разу");

        // 5) Проста оцінка з погодою
        var rainy = new Review(Route2, Route2Name, Andrii, 4, "Стежка до озера місцями слизька після дощу.");
        rainy.SetWeather(new WeatherInfo(11, "light", "rain"));

        var reviews = new[] { full, windy, withTrack, longDay, rainy };
        await context.Reviews.InsertManyAsync(reviews, cancellationToken: ct);    // bulk-вставка

        logger.LogInformation("Seeded {Count} reviews", reviews.Length);
    }

    private async Task<ObjectId?> FindDiscussionIdAsync(long routeId, string title, CancellationToken ct)
    {
        var discussion = await context.Discussions
            .Find(d => d.RouteId == routeId && d.Title == title)
            .FirstOrDefaultAsync(ct);
        return discussion?.Id;
    }
}
