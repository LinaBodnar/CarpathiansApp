using System.Data;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public sealed class BookingRepository : BaseDapperRepository<Booking>, IBookingRepository
{
    public BookingRepository(SqlConnection c, Func<SqlTransaction?> tx)
        : base(c, tx, "dbo.Bookings") { }
    
    public override async Task<long> AddAsync(Booking b, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT dbo.Bookings (CustomerId, Status, TotalAmount)
            VALUES (@CustomerId, @Status, @TotalAmount);
            DECLARE @Id BIGINT = SCOPE_IDENTITY();
            INSERT dbo.BookingDetails (BookingId, ContactPhone, MeetingPoint, Comment)
            VALUES (@Id, @ContactPhone, @MeetingPoint, @Comment);
            SELECT @Id;";
        await EnsureOpenAsync(ct);
        return await GuardAsync(() => Connection.ExecuteScalarAsync<long>(Cmd(sql,
            new { b.CustomerId, b.Status, b.TotalAmount, b.ContactPhone, b.MeetingPoint, b.Comment }, ct: ct)));
    }

    public async Task<Booking?> GetWithItemsAsync(long bookingId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT b.Id, b.CustomerId, b.Status, b.BookingDate, b.TotalAmount,
                   bd.ContactPhone, bd.MeetingPoint, bd.Comment,
                   i.BookingId, i.DepartureId, i.RouteName, i.UnitPrice, i.Seats
            FROM dbo.Bookings b
            LEFT JOIN dbo.BookingDetails bd ON bd.BookingId = b.Id
            LEFT JOIN dbo.BookingItems i ON i.BookingId = b.Id
            WHERE b.Id = @BookingId AND b.IsDeleted = 0";
        await EnsureOpenAsync(ct);
        Booking? result = null;
        await Connection.QueryAsync<Booking, BookingItem?, Booking>(
            Cmd(sql, new { BookingId = bookingId }, ct: ct),
            (booking, item) =>
            {
                result ??= booking;
                if (item is not null) result.Items.Add(item);
                return result;
            },
            splitOn: "BookingId");
        return result;
    }

    public async Task AddItemAsync(BookingItem item, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT dbo.BookingItems (BookingId, DepartureId, RouteName, UnitPrice, Seats)
            VALUES (@BookingId, @DepartureId, @RouteName, @UnitPrice, @Seats)";
        await EnsureOpenAsync(ct);
        await GuardAsync(() => Connection.ExecuteAsync(Cmd(sql,
            new { item.BookingId, item.DepartureId, item.RouteName, item.UnitPrice, item.Seats }, ct: ct)));
    }

    public async Task AddHistoryAsync(long bookingId, string? oldStatus, string newStatus,
                                      string? changedBy, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT dbo.BookingStatusHistory (BookingId, OldStatus, NewStatus, ChangedBy)
            VALUES (@BookingId, @OldStatus, @NewStatus, @ChangedBy)";
        await EnsureOpenAsync(ct);
        await GuardAsync(() => Connection.ExecuteAsync(Cmd(sql,
            new { BookingId = bookingId, OldStatus = oldStatus, NewStatus = newStatus, ChangedBy = changedBy }, ct: ct)));
    }

    public async Task ChangeStatusAsync(long bookingId, string newStatus, string? changedBy,
                                        CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await GuardAsync(() => Connection.ExecuteAsync(Cmd("dbo.usp_ChangeBookingStatus",
            new { BookingId = bookingId, NewStatus = newStatus, ChangedBy = changedBy },
            CommandType.StoredProcedure, ct)));
    }

    public async Task<long> PayAsync(long bookingId, string method, string? paidBy,
                                     CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        return await GuardAsync(() => Connection.ExecuteScalarAsync<long>(Cmd("dbo.usp_PayBooking",
            new { BookingId = bookingId, Method = method, PaidBy = paidBy },
            CommandType.StoredProcedure, ct)));
    }

    public async Task CancelAsync(long bookingId, string? changedBy, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await GuardAsync(() => Connection.ExecuteAsync(Cmd("dbo.usp_CancelBooking",
            new { BookingId = bookingId, ChangedBy = changedBy },
            CommandType.StoredProcedure, ct)));
    }

    public async Task<IReadOnlyList<Booking>> ListAsync(string? customerEmail, int skip, int take,
                                                        CancellationToken ct = default)
    {
        // Спершу сторінка ідентифікаторів (OFFSET/FETCH), потім позиції лише цих бронювань — без дублювання рядків у пагінації.
        const string sql = @"
            WITH page AS (
                SELECT b.Id, b.BookingDate
                FROM dbo.Bookings b
                JOIN dbo.Customers c ON c.Id = b.CustomerId
                WHERE b.IsDeleted = 0 AND (@Email IS NULL OR c.Email = @Email)
                ORDER BY b.BookingDate DESC, b.Id DESC
                OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY)
            SELECT b.Id, b.CustomerId, b.Status, b.BookingDate, b.TotalAmount,
                   bd.ContactPhone, bd.MeetingPoint, bd.Comment,
                   i.BookingId, i.DepartureId, i.RouteName, i.UnitPrice, i.Seats
            FROM page p
            JOIN dbo.Bookings b ON b.Id = p.Id
            LEFT JOIN dbo.BookingDetails bd ON bd.BookingId = b.Id
            LEFT JOIN dbo.BookingItems i ON i.BookingId = b.Id
            ORDER BY p.BookingDate DESC, p.Id DESC";
        await EnsureOpenAsync(ct);
        var bookings = new Dictionary<long, Booking>();
        await Connection.QueryAsync<Booking, BookingItem?, Booking>(
            Cmd(sql, new { Email = customerEmail, Skip = skip, Take = take }, ct: ct),
            (booking, item) =>
            {
                if (!bookings.TryGetValue(booking.Id, out var current))
                    bookings[booking.Id] = current = booking;
                if (item is not null)
                    current.Items.Add(item);
                return current;
            },
            splitOn: "BookingId");
        return bookings.Values.ToList();
    }

    public async Task<int> CountAsync(string? customerEmail, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM dbo.Bookings b
            JOIN dbo.Customers c ON c.Id = b.CustomerId
            WHERE b.IsDeleted = 0 AND (@Email IS NULL OR c.Email = @Email)";
        await EnsureOpenAsync(ct);
        return await Connection.ExecuteScalarAsync<int>(Cmd(sql, new { Email = customerEmail }, ct: ct));
    }

    public async Task<bool> UpdateDetailsAsync(long bookingId, string contactPhone, string meetingPoint, string? comment,
                                               string? updatedBy, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.Bookings SET UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
            WHERE Id = @BookingId AND IsDeleted = 0;
            IF @@ROWCOUNT = 0
            BEGIN
                SELECT 0;
                RETURN;
            END
            UPDATE dbo.BookingDetails
            SET ContactPhone = @ContactPhone, MeetingPoint = @MeetingPoint, Comment = @Comment
            WHERE BookingId = @BookingId;
            SELECT 1;";
        await EnsureOpenAsync(ct);
        var updated = await GuardAsync(() => Connection.ExecuteScalarAsync<int>(Cmd(sql,
            new { BookingId = bookingId, ContactPhone = contactPhone, MeetingPoint = meetingPoint, Comment = comment, UpdatedBy = updatedBy },
            ct: ct)));
        return updated == 1;
    }

    public async Task<BookingStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        // Легкі агрегати за індексованими полями: виконуються фоново раз на кілька десятків секунд, не на кожен запит.
        const string sql = @"
            SELECT Status, COUNT(*) AS Count
            FROM dbo.Bookings
            WHERE IsDeleted = 0
            GROUP BY Status;

            SELECT ISNULL(SUM(SeatsAvailable), 0)
            FROM dbo.Departures
            WHERE IsDeleted = 0 AND DepartureDate >= CAST(SYSUTCDATETIME() AS DATE);";
        await EnsureOpenAsync(ct);
        using var grid = await Connection.QueryMultipleAsync(Cmd(sql, ct: ct));
        var byStatus = (await grid.ReadAsync<(string Status, int Count)>())
            .ToDictionary(r => r.Status, r => r.Count, StringComparer.Ordinal);
        var seats = await grid.ReadSingleAsync<int>();
        return new BookingStatistics(byStatus, seats);
    }
}