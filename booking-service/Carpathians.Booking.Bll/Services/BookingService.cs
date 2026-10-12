using System.Data;
using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Constants;
using Carpathians.Domain.Exceptions;
using Carpathians.Domain.Models;
namespace Carpathians.Bll.Services;

public sealed class BookingService(IUnitOfWork uow, IMapper mapper) : IBookingService
{
    private const string Actor = "booking-api";

    public async Task<BookingDto> GetByIdAsync(long id, CancellationToken ct)
        => mapper.Map<BookingDto>(await uow.Bookings.GetWithItemsAsync(id, ct)
            ?? throw new NotFoundException($"Booking {id} not found"));
    
    public async Task<BookingDto> CreateAsync(CreateBookingDto input, CancellationToken ct)
    {
        if (input.Items.GroupBy(i => i.DepartureId).Any(g => g.Count() > 1))
            throw new ValidationException("The same departure is listed more than once");

        long bookingId;
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            _ = await uow.Customers.GetByIdAsync(input.CustomerId, ct)
                ?? throw new NotFoundException($"Customer {input.CustomerId} not found");

            var items = new List<BookingItem>();
            foreach (var line in input.Items)
            {
                var departure = await uow.Departures.GetWithRouteNameAsync(line.DepartureId, ct)
                    ?? throw new NotFoundException($"Departure {line.DepartureId} not found");
                if (departure.DepartureDate.Date < DateTime.UtcNow.Date)
                    throw new ValidationException("Departure date is in the past");
                
                await uow.Departures.DecreaseSeatsAsync(departure.Id, line.Seats, ct);

                items.Add(new BookingItem   
                {
                    DepartureId = departure.Id,
                    RouteName = departure.RouteName!,
                    UnitPrice = departure.Price,
                    Seats = line.Seats
                });
            }

            bookingId = await uow.Bookings.AddAsync(new Booking
            {
                CustomerId = input.CustomerId,
                Status = BookingStatus.Submitted,
                TotalAmount = items.Sum(i => i.UnitPrice * i.Seats),
                ContactPhone = input.ContactPhone,
                MeetingPoint = input.MeetingPoint,
                Comment = input.Comment
            }, ct);

            foreach (var item in items)
            {
                item.BookingId = bookingId;
                await uow.Bookings.AddItemAsync(item, ct);
            }
            await uow.Bookings.AddHistoryAsync(bookingId, null, BookingStatus.Submitted, Actor, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
        return await GetByIdAsync(bookingId, ct);
    }
    
    public async Task ConfirmAsync(long id, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var booking = await uow.Bookings.GetWithItemsAsync(id, ct)
                ?? throw new NotFoundException($"Booking {id} not found");
            if (booking.Items.Count == 0)
                throw new BusinessConflictException($"Booking {id} has no items");

            if (booking.Status == BookingStatus.Submitted)
                await uow.Bookings.ChangeStatusAsync(id, BookingStatus.AwaitingValidation, Actor, ct);
            // повторне підтвердження / оплачене / скасоване -> THROW 50002 -> 409
            await uow.Bookings.ChangeStatusAsync(id, BookingStatus.SeatsConfirmed, Actor, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
    }
    
    public async Task<PaymentResultDto> PayAsync(long id, PayBookingDto input, CancellationToken ct)
        => new(await uow.Bookings.PayAsync(id, input.Method, Actor, ct));

    public Task CancelAsync(long id, CancellationToken ct)
        => uow.Bookings.CancelAsync(id, Actor, ct);
}