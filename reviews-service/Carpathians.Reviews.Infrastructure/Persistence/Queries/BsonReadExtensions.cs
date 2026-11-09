using MongoDB.Bson;

namespace Carpathians.Reviews.Infrastructure.Persistence.Queries;

/// <summary>Безпечне читання полів BsonDocument з урахуванням гнучкої схеми (поле може бути відсутнє).</summary>
internal static class BsonReadExtensions
{
    public static string Str(this BsonDocument d, string name, string fallback = "")
        => d.TryGetValue(name, out var v) && v.IsString ? v.AsString : fallback;

    public static string? StrOrNull(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsString ? v.AsString : null;

    public static long Int64(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsNumeric ? v.ToInt64() : 0;

    public static int Int32(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsNumeric ? v.ToInt32() : 0;

    public static double Double(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsNumeric ? v.ToDouble() : 0;

    public static double? DoubleOrNull(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsNumeric ? v.ToDouble() : null;

    public static bool? BoolOrNull(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsBoolean ? v.AsBoolean : null;

    public static DateTime Date(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsValidDateTime ? v.ToUniversalTime() : DateTime.MinValue;

    public static DateTime? DateOrNull(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsValidDateTime ? v.ToUniversalTime() : null;

    public static string? IdOrNull(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsObjectId ? v.AsObjectId.ToString() : null;

    public static BsonDocument Doc(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsBsonDocument ? v.AsBsonDocument : new BsonDocument();

    public static BsonDocument? DocOrNull(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsBsonDocument ? v.AsBsonDocument : null;

    public static IEnumerable<BsonDocument> Docs(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsBsonArray
            ? v.AsBsonArray.Where(x => x.IsBsonDocument).Select(x => x.AsBsonDocument)
            : Enumerable.Empty<BsonDocument>();

    public static IReadOnlyList<string> Strings(this BsonDocument d, string name)
        => d.TryGetValue(name, out var v) && v.IsBsonArray
            ? v.AsBsonArray.Where(x => x.IsString).Select(x => x.AsString).ToList()
            : Array.Empty<string>();
}
