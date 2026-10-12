using System.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Models;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public sealed class CustomerRepository : RepositoryBase, ICustomerRepository
{
    public CustomerRepository(SqlConnection c, Func<SqlTransaction?> tx) : base(c, tx) { }

    private const string SelectColumns = "Id, Email, FullName, Phone, CreatedAt";

    private static Customer Map(SqlDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Email = r.GetString(1),
        FullName = r.GetString(2),
        Phone = r.IsDBNull(3) ? null : r.GetString(3),
        CreatedAt = r.GetDateTime(4)
    };

    public async Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM dbo.Customers WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM dbo.Customers WHERE IsDeleted = 0 ORDER BY Id";
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var list = new List<Customer>();
        while (await reader.ReadAsync(ct)) list.Add(Map(reader));
        return list;
    }

    public async Task<long> AddAsync(Customer c, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT dbo.Customers (Email, FullName, Phone) VALUES (@Email, @FullName, @Phone);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        await EnsureOpenAsync(ct);
        return await GuardAsync(async () =>
        {
            await using var command = new SqlCommand(sql, Connection, Transaction);
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = c.Email;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = c.FullName;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)c.Phone ?? DBNull.Value;
            return (long)(await command.ExecuteScalarAsync(ct))!;
        });
    }

    public async Task<bool> UpdateAsync(Customer c, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.Customers
            SET Email = @Email, FullName = @FullName, Phone = @Phone, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        return await GuardAsync(async () =>
        {
            await using var command = new SqlCommand(sql, Connection, Transaction);
            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = c.Id;
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = c.Email;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = c.FullName;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)c.Phone ?? DBNull.Value;
            return await command.ExecuteNonQueryAsync(ct) > 0;
        });
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        const string sql = "UPDATE dbo.Customers SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        await using var command = new SqlCommand(sql, Connection, Transaction);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }
}