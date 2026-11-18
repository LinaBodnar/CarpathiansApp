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
}