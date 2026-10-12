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
}