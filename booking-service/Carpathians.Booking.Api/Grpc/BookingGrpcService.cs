using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Booking.Grpc;
using Carpathians.Domain.Exceptions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Carpathians.Api.Grpc;

/// <summary>
/// gRPC-API сервісу бронювань (контракт Protos/booking.proto) для внутрішніх викликів (Aggregator).
/// Тонкий адаптер над BLL Лаб. 02 через I*Service — тобто через декоратори з кешем.
/// <para>Помилки: некоректний запит — INVALID_ARGUMENT тут; доменні винятки (NotFoundException -> NOT_FOUND)
/// перетворює <see cref="BookingGrpcExceptionInterceptor"/>.</para>
/// </summary>
public sealed class BookingGrpcService(
    IBookingService bookings,
    ICustomerService customers,
    IDepartureService departures) : BookingGrpc.BookingGrpcBase
{
    public const int MaxBatchSize = 100;

    public override async Task<BookingReply> GetBooking(GetBookingRequest request, ServerCallContext context)
    {
        EnsurePositive(request.Id, "Booking id");
        var booking = await bookings.GetByIdAsync(request.Id, context.CancellationToken);

        var reply = new BookingReply
        {
            Id = booking.Id,
            CustomerId = booking.CustomerId,
            Status = booking.Status,
            BookingDate = ToTimestamp(booking.BookingDate),
            TotalAmount = booking.TotalAmount,
            ContactPhone = booking.ContactPhone,
            MeetingPoint = booking.MeetingPoint,
            Comment = booking.Comment
        };
        reply.Items.AddRange(booking.Items.Select(i => new BookingItemReply
        {
            DepartureId = i.DepartureId,
            RouteName = i.RouteName,
            UnitPrice = i.UnitPrice,
            Seats = i.Seats
        }));
        return reply;
    }

    public override async Task<CustomerReply> GetCustomer(GetCustomerRequest request, ServerCallContext context)
    {
        EnsurePositive(request.Id, "Customer id");
        var customer = await customers.GetByIdAsync(request.Id, context.CancellationToken);

        return new CustomerReply
        {
            Id = customer.Id,
            Email = customer.Email,
            FullName = customer.FullName,
            Phone = customer.Phone
        };
    }

    public override async Task<DeparturesReply> GetDeparturesByIds(GetDeparturesByIdsRequest request, ServerCallContext context)
    {
        if (request.Ids.Count == 0 || request.Ids.Count > MaxBatchSize)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"From 1 to {MaxBatchSize} departure ids are expected"));
        foreach (var id in request.Ids)
            EnsurePositive(id, "Departure id");

        var reply = new DeparturesReply();
        // Послідовно: один UnitOfWork (з'єднання SQL) на запит; більшість виїздів береться з кешу.
        foreach (var id in request.Ids.Distinct())
        {
            try
            {
                reply.Departures.Add(ToReply(await departures.GetByIdAsync(id, context.CancellationToken)));
            }
            catch (NotFoundException)
            {
                // відсутні виїзди пропускаються
            }
        }

        return reply;
    }

    private static DepartureReply ToReply(DepartureDto d) => new()
    {
        Id = d.Id,
        RouteId = d.RouteId,
        RouteName = d.RouteName,
        DepartureDate = ToTimestamp(d.DepartureDate),
        Price = d.Price,
        SeatsTotal = d.SeatsTotal,
        SeatsAvailable = d.SeatsAvailable
    };

    private static void EnsurePositive(long id, string name)
    {
        if (id <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{name} must be positive"));
    }

    /// <summary>Дати в БД зберігаються в UTC без зазначення Kind; Timestamp вимагає UTC.</summary>
    private static Timestamp ToTimestamp(DateTime value) => Timestamp.FromDateTime(value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    });
}
