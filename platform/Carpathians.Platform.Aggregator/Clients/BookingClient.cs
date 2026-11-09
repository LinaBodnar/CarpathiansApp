using Carpathians.Platform.Aggregator.Contracts;

namespace Carpathians.Platform.Aggregator.Clients;

/// <summary>Typed HttpClient для booking-api (бронювання, виїзди, клієнти).</summary>
public class BookingClient(HttpClient http, ILogger<BookingClient> logger)
    : ServiceClientBase(http, logger, "booking-api")
{
    public Task<BookingResponse?> GetBookingAsync(long id, CancellationToken ct)
        => GetOrNullAsync<BookingResponse>($"/api/bookings/{id}", ct);

    public Task<DepartureResponse?> GetDepartureAsync(long id, CancellationToken ct)
        => GetOrNullAsync<DepartureResponse>($"/api/departures/{id}", ct);

    public Task<CustomerResponse?> GetCustomerAsync(long id, CancellationToken ct)
        => GetOrNullAsync<CustomerResponse>($"/api/customers/{id}", ct);
}
