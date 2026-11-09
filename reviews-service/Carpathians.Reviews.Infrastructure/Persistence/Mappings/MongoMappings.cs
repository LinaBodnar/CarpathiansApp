using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Carpathians.Reviews.Infrastructure.Persistence.Mappings;

/// <summary>
/// BSON-мапінг доменної моделі без атрибутів у доменних класах. Структура документів
/// відповідає Лаб. 01 (reviews-db/init.js): camelCase, вкладені author/comments/weather/gpsTrack.
/// Реєструється один раз під час старту, до першого звернення до БД.
/// </summary>
public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;

            RegisterConventions();
            RegisterSerializers();
            RegisterValueObjects();
            RegisterEntities();

            _registered = true;
        }
    }

    private static void RegisterConventions()
    {
        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),                 // RouteName -> routeName
            new IgnoreExtraElementsConvention(true),              // старі/зайві поля не ламають читання
            new EnumRepresentationConvention(BsonType.String)     // enum зберігаються рядками
        };
        ConventionRegistry.Register("carpathians-reviews", pack,
            type => type.Namespace?.StartsWith("Carpathians.Reviews.Domain") == true);
    }

    private static void RegisterSerializers()
    {
        // усі дати — UTC (BSON date), як у seed-скрипті Лаб. 01
        BsonSerializer.TryRegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));
    }

    private static void RegisterValueObjects()
    {
        // Незмінні Value Objects: властивості лише для читання, створення — через конструктор.
        BsonClassMap.TryRegisterClassMap<Author>(cm =>
        {
            cm.MapProperty(a => a.UserId).SetElementName("userId");
            cm.MapProperty(a => a.DisplayName).SetElementName("displayName");
            cm.MapCreator(a => new Author(a.UserId, a.DisplayName));
        });

        BsonClassMap.TryRegisterClassMap<GeoPoint>(cm =>
        {
            cm.MapProperty(p => p.Lat).SetElementName("lat");
            cm.MapProperty(p => p.Lng).SetElementName("lng");
            cm.MapProperty(p => p.Ele).SetElementName("ele").SetIgnoreIfNull(true);
            cm.MapCreator(p => new GeoPoint(p.Lat, p.Lng, p.Ele));
        });

        BsonClassMap.TryRegisterClassMap<WeatherInfo>(cm =>
        {
            cm.MapProperty(w => w.TempC).SetElementName("tempC").SetIgnoreIfNull(true);
            cm.MapProperty(w => w.Wind).SetElementName("wind").SetIgnoreIfNull(true);
            cm.MapProperty(w => w.Conditions).SetElementName("conditions").SetIgnoreIfNull(true);
            cm.MapCreator(w => new WeatherInfo(w.TempC, w.Wind, w.Conditions));
        });

        BsonClassMap.TryRegisterClassMap<MessagePreview>(cm =>
        {
            cm.MapProperty(m => m.Author).SetElementName("author");
            cm.MapProperty(m => m.Text).SetElementName("text");
            cm.MapProperty(m => m.CreatedAt).SetElementName("createdAt");
            cm.MapCreator(m => new MessagePreview(m.Author, m.Text, m.CreatedAt));
        });
    }

    private static void RegisterEntities()
    {
        BsonClassMap.TryRegisterClassMap<BaseEntity>(cm =>
        {
            cm.AutoMap();
            cm.MapIdMember(e => e.Id);                                   // _id: ObjectId
            cm.GetMemberMap(e => e.UpdatedAt).SetIgnoreIfNull(true);
        });

        BsonClassMap.TryRegisterClassMap<Comment>(cm => cm.AutoMap());

        BsonClassMap.TryRegisterClassMap<Review>(cm =>
        {
            cm.AutoMap();
            cm.GetMemberMap(r => r.Weather).SetIgnoreIfNull(true);
            cm.GetMemberMap(r => r.DiscussionId).SetIgnoreIfNull(true);
            cm.GetMemberMap(r => r.IsVerifiedParticipant).SetIgnoreIfNull(true);

            // Приватні поля-колекції. Порожні масиви не записуються — гнучка схема:
            // у документі є лише ті поля, що реально заповнені.
            MapCollectionField(cm, "_comments", "comments", r => r.Comments.Count > 0);
            MapCollectionField(cm, "_photos", "photos", r => r.Photos.Count > 0);
            MapCollectionField(cm, "_tags", "tags", r => r.Tags.Count > 0);
            MapCollectionField(cm, "_gpsTrack", "gpsTrack", r => r.GpsTrack.Count > 0);
        });

        BsonClassMap.TryRegisterClassMap<Discussion>(cm =>
        {
            cm.AutoMap();
            cm.MapField("_lastMessages").SetElementName("lastMessages");   // обов'язкове поле схеми
        });

        BsonClassMap.TryRegisterClassMap<DiscussionMessage>(cm =>
        {
            cm.AutoMap();
            cm.GetMemberMap(m => m.ReplyTo).SetIgnoreIfNull(true);
            MapCollectionField(cm, "_attachments", "attachments", m => m.Attachments.Count > 0);
        });
    }

    private static void MapCollectionField<T>(
        BsonClassMap<T> cm, string fieldName, string elementName, Func<T, bool> shouldSerialize)
    {
        cm.MapField(fieldName)
            .SetElementName(elementName)
            .SetShouldSerializeMethod(obj => shouldSerialize((T)obj));
    }
}
