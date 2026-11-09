using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Bll.Services;
using Carpathians.Platform.ServiceDefaults.Caching;

namespace Carpathians.Api.Caching;

// Декоратори BLL-сервісів Лаб. 02 з кешуванням (cache-aside L1 -> L2 -> SQL Server).
// Самі сервіси (ADO.NET + Dapper) не змінюються; REST-контролери й gRPC-сервіс працюють через інтерфейси.
// Після кожної зміни відповідні ключі видаляються синхронно — до відповіді клієнту.

/// <summary>Виїзди: кешуються картка й список; зміна виїзду інвалідує обидва.</summary>
public sealed class CachedDepartureService(DepartureService inner, ICacheService cache) : IDepartureService
{
    public async Task<DepartureDto> GetByIdAsync(long id, CancellationToken ct)
        => (await cache.GetOrCreateAsync(
            BookingCacheKeys.Departure(id),
            async token => await inner.GetByIdAsync(id, token),
            BookingCachePolicies.Departure, ct))!;

    public async Task<IReadOnlyList<DepartureDto>> GetAllAsync(CancellationToken ct)
        => (await cache.GetOrCreateAsync(
            BookingCacheKeys.DeparturesAll,
            async token => await inner.GetAllAsync(token),
            BookingCachePolicies.DeparturesAll, ct))!;

    public async Task<DepartureDto> CreateAsync(CreateDepartureDto input, CancellationToken ct)
    {
        var created = await inner.CreateAsync(input, ct);
        await cache.InvalidateAsync("DepartureCreated", ct, BookingCacheKeys.DeparturesAll);
        return created;
    }

    public async Task<DepartureDto> UpdateAsync(long id, UpdateDepartureDto input, CancellationToken ct)
    {
        var updated = await inner.UpdateAsync(id, input, ct);
        await cache.InvalidateAsync("DepartureUpdated", ct, BookingCacheKeys.Departure(id), BookingCacheKeys.DeparturesAll);
        return updated;
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await inner.DeleteAsync(id, ct);
        await cache.InvalidateAsync("DepartureDeleted", ct, BookingCacheKeys.Departure(id), BookingCacheKeys.DeparturesAll);
    }
}

/// <summary>Клієнти: кешується лише картка з коротким TTL (персональні дані); списки клієнтів не кешуються.</summary>
public sealed class CachedCustomerService(CustomerService inner, ICacheService cache) : ICustomerService
{
    public async Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct)
        => (await cache.GetOrCreateAsync(
            BookingCacheKeys.Customer(id),
            async token => await inner.GetByIdAsync(id, token),
            BookingCachePolicies.Customer, ct))!;

    public Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct) => inner.GetAllAsync(ct);

    public Task<CustomerDto> CreateAsync(CustomerInputDto input, CancellationToken ct) => inner.CreateAsync(input, ct);

    public async Task<CustomerDto> UpdateAsync(long id, CustomerInputDto input, CancellationToken ct)
    {
        var updated = await inner.UpdateAsync(id, input, ct);
        await cache.InvalidateAsync("CustomerUpdated", ct, BookingCacheKeys.Customer(id));
        return updated;
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await inner.DeleteAsync(id, ct);
        await cache.InvalidateAsync("CustomerDeleted", ct, BookingCacheKeys.Customer(id));
    }
}

/// <summary>
/// Бронювання: картка кешується коротко; зміна статусу інвалідує її, а створення й скасування —
/// ще й виїзди позицій (змінюється кількість вільних місць).
/// </summary>
public sealed class CachedBookingService(BookingService inner, ICacheService cache) : IBookingService
{
    public async Task<BookingDto> GetByIdAsync(long id, CancellationToken ct)
        => (await cache.GetOrCreateAsync(
            BookingCacheKeys.Booking(id),
            async token => await inner.GetByIdAsync(id, token),
            BookingCachePolicies.Booking, ct))!;

    public async Task<BookingDto> CreateAsync(CreateBookingDto input, CancellationToken ct)
    {
        var created = await inner.CreateAsync(input, ct);
        await cache.InvalidateAsync(SeatKeys(created), "BookingCreated", ct);
        return created;
    }

    public async Task ConfirmAsync(long id, CancellationToken ct)
    {
        await inner.ConfirmAsync(id, ct);
        await cache.InvalidateAsync("BookingConfirmed", ct, BookingCacheKeys.Booking(id));
    }

    public async Task<PaymentResultDto> PayAsync(long id, PayBookingDto input, CancellationToken ct)
    {
        var result = await inner.PayAsync(id, input, ct);
        await cache.InvalidateAsync("BookingPaid", ct, BookingCacheKeys.Booking(id));
        return result;
    }

    public async Task CancelAsync(long id, CancellationToken ct)
    {
        // Позиції читаються до скасування: після нього місця повертаються у виїзди.
        var booking = await inner.GetByIdAsync(id, ct);
        await inner.CancelAsync(id, ct);

        var keys = SeatKeys(booking);
        keys.Add(BookingCacheKeys.Booking(id));
        await cache.InvalidateAsync(keys, "BookingCancelled", ct);
    }

    /// <summary>Виїзди позицій бронювання + список виїздів (вільні місця змінилися).</summary>
    private static List<string> SeatKeys(BookingDto booking)
    {
        var keys = booking.Items.Select(i => BookingCacheKeys.Departure(i.DepartureId)).Distinct().ToList();
        keys.Add(BookingCacheKeys.DeparturesAll);
        return keys;
    }
}
