using Carpathians.Domain.Models;
namespace Carpathians.Dal.Interfaces;

public interface IBookingRepository : IGenericRepository<Booking>
{
    Task<Booking?> GetWithItemsAsync(long bookingId, CancellationToken ct = default);
    Task AddItemAsync(BookingItem item, CancellationToken ct = default);
    Task AddHistoryAsync(long bookingId, string? oldStatus, string newStatus,
        string? changedBy, CancellationToken ct = default);
    
    Task ChangeStatusAsync(long bookingId, string newStatus, string? changedBy, CancellationToken ct = default);
    Task<long> PayAsync(long bookingId, string method, string? paidBy, CancellationToken ct = default);
    Task CancelAsync(long bookingId, string? changedBy, CancellationToken ct = default);

    /// <summary>Сторінка бронювань з позиціями (Лаб. 09); customerEmail = null — усі бронювання.</summary>
    Task<IReadOnlyList<Booking>> ListAsync(string? customerEmail, int skip, int take, CancellationToken ct = default);

    Task<int> CountAsync(string? customerEmail, CancellationToken ct = default);

    /// <summary>Оновлення контактних даних бронювання (BookingDetails); false — бронювання не існує.</summary>
    Task<bool> UpdateDetailsAsync(long bookingId, string contactPhone, string meetingPoint, string? comment,
        string? updatedBy, CancellationToken ct = default);

    /// <summary>Знімок стану для gauge-метрик (Лаб. 07): кількість бронювань за статусами й вільні місця майбутніх виїздів.</summary>
    Task<BookingStatistics> GetStatisticsAsync(CancellationToken ct = default);
}

/// <param name="CountByStatus">Кількість активних (не видалених) бронювань за статусом.</param>
/// <param name="UpcomingSeatsAvailable">Сума вільних місць у виїздах із датою від сьогодні.</param>
public sealed record BookingStatistics(IReadOnlyDictionary<string, int> CountByStatus, int UpcomingSeatsAvailable);