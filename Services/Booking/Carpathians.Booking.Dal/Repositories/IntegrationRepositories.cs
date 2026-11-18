using Carpathians.Dal.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public sealed class RouteRefRepository(SqlConnection connection, Func<SqlTransaction?> transaction)
    : RepositoryBase(connection, transaction), IRouteRefRepository
{
    public async Task<bool> ApplyAsync(long routeId, string name, bool isActive, DateTime changedAt,
        CancellationToken ct = default)
    {
        // HOLDLOCK: upsert атомарний навіть при паралельній обробці подій одного маршруту.
        // Рядок, змінений новішою подією, не оновлюється (CatalogChangedAt >= @ChangedAt) — 0 рядків.
        const string sql = @"
            MERGE dbo.RouteRefs WITH (HOLDLOCK) AS t
            USING (SELECT @Id AS Id) AS s ON t.Id = s.Id
            WHEN MATCHED AND (t.CatalogChangedAt IS NULL OR t.CatalogChangedAt < @ChangedAt) THEN
                UPDATE SET Name = @Name, IsActive = @IsActive, CatalogChangedAt = @ChangedAt,
                           UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Id, Name, IsActive, CatalogChangedAt)
                VALUES (@Id, @Name, @IsActive, @ChangedAt);";

        await EnsureOpenAsync(ct);
        var rows = await Connection.ExecuteAsync(Cmd(sql,
            new { Id = routeId, Name = name, IsActive = isActive, ChangedAt = changedAt }, ct: ct));
        return rows > 0;
    }
}

public sealed class ProcessedEventRepository(SqlConnection connection, Func<SqlTransaction?> transaction)
    : RepositoryBase(connection, transaction), IProcessedEventRepository
{
    private const int PrimaryKeyViolation = 2627;
    private const int UniqueIndexViolation = 2601;

    public async Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        const string sql = "INSERT dbo.ProcessedEvents (EventId, EventType) VALUES (@EventId, @EventType);";

        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(Cmd(sql, new { EventId = eventId, EventType = eventType }, ct: ct));
            return true;
        }
        catch (SqlException ex) when (ex.Number is PrimaryKeyViolation or UniqueIndexViolation)
        {
            return false;                                       // уже оброблено
        }
    }
}
