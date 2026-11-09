using System.Diagnostics;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public class BookingsController(IBookingService bookings, ILogger<BookingsController> logger) : ControllerBase
{
    /// <summary>Власні spans бізнес-операцій (підхоплюються OpenTelemetry через AddSource("Carpathians.*")).</summary>
    private static readonly ActivitySource Telemetry = new("Carpathians.Booking");

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetById(long id, CancellationToken ct)
        => Ok(await bookings.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingDto input, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("CreateBooking");
        activity?.SetTag("customer.id", input.CustomerId);

        var created = await bookings.CreateAsync(input, ct);

        activity?.SetTag("booking.id", created.Id);
        logger.LogInformation(
            "Booking {BookingId} created for customer {CustomerId}: {ItemsCount} items, amount {Amount}, status {Status}",
            created.Id, created.CustomerId, created.Items.Count, created.TotalAmount, created.Status);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(long id, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("ConfirmBooking");
        activity?.SetTag("booking.id", id);

        await bookings.ConfirmAsync(id, ct);

        logger.LogInformation("Booking {BookingId} confirmed, status {Status}", id, BookingStatus.SeatsConfirmed);
        return NoContent();
    }

    [HttpPost("{id:long}/pay")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResultDto>> Pay(
        long id, PayBookingDto input, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("PayBooking");
        activity?.SetTag("booking.id", id);

        var payment = await bookings.PayAsync(id, input, ct);

        logger.LogInformation("Booking {BookingId} paid by {PaymentMethod}, payment {PaymentId}, status {Status}",
            id, input.Method, payment.PaymentId, BookingStatus.Paid);
        return Ok(payment);
    }

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("CancelBooking");
        activity?.SetTag("booking.id", id);

        await bookings.CancelAsync(id, ct);

        logger.LogInformation("Booking {BookingId} cancelled, status {Status}", id, BookingStatus.Cancelled);
        return NoContent();
    }
}