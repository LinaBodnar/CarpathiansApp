using Carpathians.Dal.Interfaces;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    private readonly string _table;  

    protected BaseDapperRepository(SqlConnection c, Func<SqlTransaction?> tx, string table)
        : base(c, tx) => _table = table;

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<T>(
            Cmd($"SELECT * FROM {_table} WHERE Id = @Id AND IsDeleted = 0", new { Id = id }, ct: ct));
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.QueryAsync<T>(
            Cmd($"SELECT * FROM {_table} WHERE IsDeleted = 0", ct: ct));
        return rows.AsList();
    }

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Connection.ExecuteAsync(Cmd(
            $"UPDATE {_table} SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0",
            new { Id = id }, ct: ct));
        return rows > 0;
    }

    public abstract Task<long> AddAsync(T entity, CancellationToken ct = default);
}