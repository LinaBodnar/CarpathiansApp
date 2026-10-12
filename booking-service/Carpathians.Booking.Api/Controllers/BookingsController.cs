using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Produces("application/json")]
public class BookingsController(IBookingService bookings) : ControllerBase
{
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
        var created = await bookings.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(long id, CancellationToken ct)
    {
        await bookings.ConfirmAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/pay")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResultDto>> Pay(
        long id, PayBookingDto input, CancellationToken ct)
        => Ok(await bookings.PayAsync(id, input, ct));

    [HttpPost("{id:long}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        await bookings.CancelAsync(id, ct);
        return NoContent();
    }
}