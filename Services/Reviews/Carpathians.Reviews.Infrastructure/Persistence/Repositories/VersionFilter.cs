using Carpathians.Reviews.Domain.Common;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Carpathians.Reviews.Infrastructure.Persistence.Repositories;

/// <summary>Фільтри для оптимістичної конкурентності за полем <c>version</c>.</summary>
internal static class VersionFilter
{
    public const string VersionField = "version";

    /// <summary>
    /// Документ із заданим Id і очікуваною версією. Документи, створені seed-скриптом Лаб. 01,
    /// не мають поля version — для них очікувана версія 0 означає «поле відсутнє».
    /// </summary>
    public static FilterDefinition<T> For<T>(ObjectId id, int expectedVersion) where T : BaseEntity
    {
        var f = Builders<T>.Filter;
        var versionMatches = expectedVersion == 0
            ? f.Or(f.Eq(VersionField, 0), f.Exists(VersionField, false))
            : f.Eq(VersionField, expectedVersion);
        return f.And(ById<T>(id), versionMatches);
    }

    public static FilterDefinition<T> ById<T>(ObjectId id) where T : BaseEntity
        => Builders<T>.Filter.Eq("_id", id);
}
