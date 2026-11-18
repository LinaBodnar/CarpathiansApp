using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public abstract class RepositoryBase
{
    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }
    
    protected CommandDefinition Cmd(string sql, object? param = null,
        CommandType? type = null, CancellationToken ct = default)
        => new(sql, param, Transaction, commandType: type, cancellationToken: ct);
    
    protected static async Task<TResult> GuardAsync<TResult>(Func<Task<TResult>> action)
    {
        try { return await action(); }
        catch (SqlException ex) when (SqlExceptionTranslator.TryTranslate(ex, out var domain))
        { throw domain; }
    }

    protected static async Task GuardAsync(Func<Task> action)
    {
        try { await action(); }
        catch (SqlException ex) when (SqlExceptionTranslator.TryTranslate(ex, out var domain))
        { throw domain; }
    }
}