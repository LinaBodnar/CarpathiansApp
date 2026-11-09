using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.ValueObjects;
using Carpathians.Reviews.Infrastructure.Persistence.Mappings;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Carpathians.Reviews.Tests.Infrastructure;

/// <summary>Перевірка BsonClassMap без підключення до MongoDB: серіалізація ↔ десеріалізація.</summary>
[TestFixture]
public class MongoMappingsTests
{
    [OneTimeSetUp]
    public void RegisterMappings() => MongoMappings.Register();

    [Test]
    public void Review_SerializesToCamelCaseDocumentWithEmbeddedAuthorAndComments()
    {
        var review = new Review(1, "Заросляк – Говерла", new Author("1", "Олена"), 5, "Чудовий маршрут");
        review.AddComment(new Author("2", "Андрій"), "Згоден");

        var doc = review.ToBsonDocument();

        Assert.Multiple(() =>
        {
            Assert.That(doc["_id"].IsObjectId, Is.True);
            Assert.That(doc["routeId"].ToInt64(), Is.EqualTo(1));
            Assert.That(doc["routeName"].AsString, Is.EqualTo("Заросляк – Говерла"));
            Assert.That(doc["author"]["userId"].AsString, Is.EqualTo("1"));
            Assert.That(doc["author"]["displayName"].AsString, Is.EqualTo("Олена"));
            Assert.That(doc["rating"].BsonType, Is.EqualTo(BsonType.Int32));
            Assert.That(doc["comments"].AsBsonArray, Has.Count.EqualTo(1));
            Assert.That(doc["comments"][0]["author"]["displayName"].AsString, Is.EqualTo("Андрій"));
            Assert.That(doc["createdAt"].BsonType, Is.EqualTo(BsonType.DateTime));
            Assert.That(doc["version"].AsInt32, Is.EqualTo(0));
        });
    }

    [Test]
    public void Review_DoesNotLeakPrivateFieldNamesOrEmptyOptionalFields()
    {
        var review = new Review(1, "Route", new Author("1", "Олена"), 4, "Текст");

        var names = review.ToBsonDocument().Names.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(names.Any(n => n.StartsWith('_') && n != "_id"), Is.False, "приватні поля .NET у документі");
            Assert.That(names, Does.Not.Contain("photos"));
            Assert.That(names, Does.Not.Contain("tags"));
            Assert.That(names, Does.Not.Contain("gpsTrack"));
            Assert.That(names, Does.Not.Contain("comments"));
            Assert.That(names, Does.Not.Contain("weather"));
            Assert.That(names, Does.Not.Contain("discussionId"));
            Assert.That(names, Does.Not.Contain("updatedAt"));
        });
    }

    [Test]
    public void Review_RoundTrip_RestoresValueObjectsAndCollections()
    {
        var review = new Review(2, "Заросляк – Несамовите – Говерла", new Author("3", "Марія"), 5, "Озеро");
        review.SetTags(new[] { "lake" });
        review.SetGpsTrack(new[] { new GeoPoint(48.166, 24.546, 1050), new GeoPoint(48.160, 24.500, 2061) });
        review.SetWeather(new WeatherInfo(11, "light", "rain"));

        var restored = BsonSerializer.Deserialize<Review>(review.ToBsonDocument());

        Assert.Multiple(() =>
        {
            Assert.That(restored.Id, Is.EqualTo(review.Id));
            Assert.That(restored.Author, Is.EqualTo(review.Author));
            Assert.That(restored.Tags, Is.EqualTo(new[] { "lake" }));
            Assert.That(restored.GpsTrack, Is.EqualTo(review.GpsTrack));
            Assert.That(restored.Weather, Is.EqualTo(review.Weather));
        });
    }

    [Test]
    public void Review_DeserializesDocumentFromLab01SeedScript()
    {
        // документ у форматі reviews-db/seed.sql: routeId як NumberInt, без version/updatedAt
        var seedDocument = new BsonDocument
        {
            { "_id", new ObjectId("66f0c2000000000000000002") },
            { "routeId", new BsonInt32(1) },
            { "routeName", "Заросляк – Говерла" },
            { "author", new BsonDocument { { "userId", "2" }, { "displayName", "Андрій" } } },
            { "rating", 4 },
            { "text", "Гарний підйом, але на вершині сильний вітер." },
            { "weather", new BsonDocument { { "tempC", 8.0 }, { "wind", "strong" }, { "conditions", "cloudy" } } },
            { "isVerifiedParticipant", false },
            { "createdAt", new DateTime(2026, 9, 5, 12, 15, 0, DateTimeKind.Utc) }
        };

        var review = BsonSerializer.Deserialize<Review>(seedDocument);

        Assert.Multiple(() =>
        {
            Assert.That(review.RouteId, Is.EqualTo(1));
            Assert.That(review.Version, Is.EqualTo(0));
            Assert.That(review.Weather!.Wind, Is.EqualTo("strong"));
            Assert.That(review.Comments, Is.Empty);
            Assert.That(review.IsVerifiedParticipant, Is.False);
            Assert.That(review.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Discussion_SerializesLastMessagesAndRoundTrips()
    {
        var author = new Author("1", "Олена");
        var discussion = new Discussion(1, "Заросляк – Говерла", "Чи потрібні палиці?", author);
        var message = discussion.PostMessage(author, "Перше повідомлення");

        var doc = discussion.ToBsonDocument();
        var restored = BsonSerializer.Deserialize<Discussion>(doc);
        var messageDoc = message.ToBsonDocument();

        Assert.Multiple(() =>
        {
            Assert.That(doc["lastMessages"].AsBsonArray, Has.Count.EqualTo(1));
            Assert.That(doc["messageCount"].AsInt32, Is.EqualTo(1));
            Assert.That(doc["createdBy"]["displayName"].AsString, Is.EqualTo("Олена"));
            Assert.That(restored.LastMessages.Single().Text, Is.EqualTo("Перше повідомлення"));
            Assert.That(messageDoc["discussionId"].AsObjectId, Is.EqualTo(discussion.Id));
            Assert.That(messageDoc.Contains("replyTo"), Is.False);
            Assert.That(messageDoc.Contains("attachments"), Is.False);
        });
    }
}
