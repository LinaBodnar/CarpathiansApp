using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;

namespace Carpathians.Api.Metrics;

/// <summary>
/// Декоратор бізнес-операцій бронювання з метриками: тривалість кожного виклику (разом із кешем і БД)
/// і лічильник бізнес-події одразу після успішної операції. Охоплює і REST-контролери, і gRPC-сервіс,
/// бо обидва працюють через <see cref="IBookingService"/>. Ланцюжок: метрики -> кеш -> BookingService (ADO.NET + Dapper).
/// </summary>
public sealed class MeteredBookingService(IBookingService inner, BookingMetrics metrics) : IBookingService
{
    public Task<BookingDto> GetByIdAsync(long id, CancellationToken ct)
        => metrics.MeasureAsync(BookingMetrics.Operations.Get, () => inner.GetByIdAsync(id, ct));

    public async Task<BookingDto> CreateAsync(CreateBookingDto input, CancellationToken ct)
    {
        var created = await metrics.MeasureAsync(BookingMetrics.Operations.Create, () => inner.CreateAsync(input, ct));
        metrics.BookingCreated(created);
        return created;
    }

    public async Task ConfirmAsync(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync(BookingMetrics.Operations.Confirm, () => inner.ConfirmAsync(id, ct));
        metrics.BookingConfirmed();
    }

    public async Task<PaymentResultDto> PayAsync(long id, PayBookingDto input, CancellationToken ct)
    {
        var result = await metrics.MeasureAsync(BookingMetrics.Operations.Pay, () => inner.PayAsync(id, input, ct));
        metrics.BookingPaid(input.Method);
        return result;
    }

    public async Task CancelAsync(long id, CancellationToken ct)
    {
        await metrics.MeasureAsync(BookingMetrics.Operations.Cancel, () => inner.CancelAsync(id, ct));
        metrics.BookingCancelled();
    }

    public Task<BookingPageDto> GetListAsync(BookingListQuery query, CancellationToken ct)
        => metrics.MeasureAsync(BookingMetrics.Operations.List, () => inner.GetListAsync(query, ct));

    public Task<BookingDto> UpdateAsync(long id, UpdateBookingDto input, CancellationToken ct)
        => metrics.MeasureAsync(BookingMetrics.Operations.Update, () => inner.UpdateAsync(id, input, ct));

    public Task DeleteAsync(long id, CancellationToken ct)
        => metrics.MeasureAsync(BookingMetrics.Operations.Delete, () => inner.DeleteAsync(id, ct));
}
