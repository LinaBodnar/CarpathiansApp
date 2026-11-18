using System.Diagnostics;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Domain.Constants;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]   // Лаб. 09: лише з дійсним JWT; 401 без токена; permission — на кожному endpoint (таблиця 3.3.1)
[Produces("application/json")]
public class BookingsController(IBookingService bookings, ILogger<BookingsController> logger) : ControllerBase
{
    /// <summary>Власні spans бізнес-операцій (підхоплюються OpenTelemetry через AddSource("Carpathians.*")).</summary>
    private static readonly ActivitySource Telemetry = new("Carpathians.Booking");

    /// <summary>
    /// Список бронювань. Claims у бізнес-логіці: менеджер і адміністратор (permission bookings:approve) бачать усі,
    /// інші — лише власні: бронювання клієнта з email із токена.
    /// </summary>
    [HttpGet]
    [RequirePermission(Permissions.Bookings.Read)]
    [ProducesResponseType(typeof(BookingPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BookingPageDto>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var seesAll = User.HasPermission(Permissions.Bookings.Approve);
        var email = User.FindFirst(PlatformClaimTypes.Email)?.Value;
        if (!seesAll && string.IsNullOrWhiteSpace(email))
            return Ok(new BookingPageDto([], page, pageSize, 0));

        var result = await bookings.GetListAsync(new BookingListQuery(seesAll ? null : email, page, pageSize), ct);
        Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
        logger.LogInformation("Bookings listed by {UserName}: scope {Scope}, page {Page}, {Count} of {Total}",
            User.Identity?.Name, seesAll ? "all" : "own", result.Page, result.Items.Count, result.TotalCount);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [RequirePermission(Permissions.Bookings.Read)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> GetById(long id, CancellationToken ct)
        => Ok(await bookings.GetByIdAsync(id, ct));

    [HttpPost]
    [RequirePermission(Permissions.Bookings.Create)]
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
            "Booking {BookingId} created by {UserName} for customer {CustomerId}: {ItemsCount} items, amount {Amount}, status {Status}",
            created.Id, User.Identity?.Name, created.CustomerId, created.Items.Count, created.TotalAmount, created.Status);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Оновлення контактних даних (телефон, місце збору, коментар): manager і admin.
    /// </summary>
    [HttpPut("{id:long}")]
    [RequirePermission(Permissions.Bookings.Update)]
    [ProducesResponseType(typeof(BookingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Update(long id, UpdateBookingDto input, CancellationToken ct)
    {
        var updated = await bookings.UpdateAsync(id, input, ct);
        logger.LogInformation("Booking {BookingId} updated by {UserName}", id, User.Identity?.Name);
        return Ok(updated);
    }

    /// <summary>Видалення (лише admin): активне бронювання спершу скасовується, оплачене — 409.</summary>
    [HttpDelete("{id:long}")]
    [RequirePermission(Permissions.Bookings.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await bookings.DeleteAsync(id, ct);
        logger.LogInformation("Booking {BookingId} deleted by {UserName}", id, User.Identity?.Name);
        return NoContent();
    }

    /// <summary>Підтвердження бронювання (approve): manager і admin. /confirm — назва з Лаб. 02.</summary>
    [HttpPost("{id:long}/approve")]
    [HttpPost("{id:long}/confirm")]
    [RequirePermission(Permissions.Bookings.Approve)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(long id, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity("ConfirmBooking");
        activity?.SetTag("booking.id", id);

        await bookings.ConfirmAsync(id, ct);

        logger.LogInformation("Booking {BookingId} approved by {UserName}, status {Status}",
            id, User.Identity?.Name, BookingStatus.SeatsConfirmed);
        return NoContent();
    }

    [HttpPost("{id:long}/pay")]
    [RequirePermission(Permissions.Bookings.Update)]
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
    [RequirePermission(Permissions.Bookings.Update)]
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