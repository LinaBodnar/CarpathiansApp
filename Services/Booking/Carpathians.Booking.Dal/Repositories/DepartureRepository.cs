using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Exceptions;
using Carpathians.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public sealed class DepartureRepository : BaseDapperRepository<Departure>, IDepartureRepository
{
    public DepartureRepository(SqlConnection c, Func<SqlTransaction?> tx)
        : base(c, tx, "dbo.Departures") { }

    public override async Task<long> AddAsync(Departure d, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT dbo.Departures (RouteId, DepartureDate, Price, SeatsTotal, SeatsAvailable)
            VALUES (@RouteId, @DepartureDate, @Price, @SeatsTotal, @SeatsTotal);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
        await EnsureOpenAsync(ct);
        return await GuardAsync(() => Connection.ExecuteScalarAsync<long>(
            Cmd(sql, new { d.RouteId, d.DepartureDate, d.Price, d.SeatsTotal }, ct: ct)));
    }

    public async Task<bool> UpdateAsync(Departure d, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.Departures
            SET DepartureDate = @DepartureDate, Price = @Price, SeatsTotal = @SeatsTotal,
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        var rows = await GuardAsync(() => Connection.ExecuteAsync(
            Cmd(sql, new { d.Id, d.DepartureDate, d.Price, d.SeatsTotal }, ct: ct)));
        return rows > 0;
    }

    public async Task<Departure?> GetWithRouteNameAsync(long id, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT d.Id, d.RouteId, d.DepartureDate, d.Price, d.SeatsTotal, d.SeatsAvailable,
                   r.Name AS RouteName
            FROM dbo.Departures d
            JOIN dbo.RouteRefs r ON r.Id = d.RouteId
            WHERE d.Id = @Id AND d.IsDeleted = 0";
        await EnsureOpenAsync(ct);
        return await Connection.QuerySingleOrDefaultAsync<Departure>(Cmd(sql, new { Id = id }, ct: ct));
    }

    public async Task DecreaseSeatsAsync(long departureId, int seats, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.Departures
            SET SeatsAvailable = SeatsAvailable - @Seats, UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @Id AND IsDeleted = 0 AND SeatsAvailable >= @Seats";
        await EnsureOpenAsync(ct);
        var rows = await Connection.ExecuteAsync(Cmd(sql, new { Id = departureId, Seats = seats }, ct: ct));
        if (rows > 0) return;

        if (await GetByIdAsync(departureId, ct) is null)
            throw new NotFoundException($"Departure {departureId} not found");
        throw new BusinessConflictException("Not enough free seats");
    }
}