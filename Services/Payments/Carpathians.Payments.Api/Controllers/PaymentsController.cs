using Carpathians.Payments.Api.Payments;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Payments.Api.Controllers;

/// <summary>
/// Платежі (таблиця 3.3.3): кожен endpoint вимагає свій permission, і всі вони є лише в ролі admin.
/// manager, user і guest отримують 403 на будь-який endpoint — платежі найчутливіший ресурс системи.
/// </summary>
[ApiController]
[Route("api/payments")]
[Authorize]
[Produces("application/json")]
public class PaymentsController(PaymentStore payments, ILogger<PaymentsController> logger) : ControllerBase
{
    /// <summary>Список платежів (опційно — одного бронювання).</summary>
    [HttpGet]
    [RequirePermission(Permissions.Payments.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentDto>), StatusCodes.Status200OK)]
    public IActionResult GetAll([FromQuery] long? bookingId)
        => Ok(payments.List(bookingId));

    /// <summary>Проведення платежу.</summary>
    [HttpPost("process")]
    [RequirePermission(Permissions.Payments.Process)]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult Process(ProcessPaymentRequest request)
    {
        var payment = payments.Process(request, User.Identity?.Name);
        logger.LogInformation("Payment {PaymentId} for booking {BookingId} processed by {UserName}: {Amount} {Currency}",
            payment.Id, payment.BookingId, payment.ProcessedBy, payment.Amount, payment.Currency);
        return Created($"/api/payments?bookingId={payment.BookingId}", payment);
    }

    /// <summary>Повернення коштів.</summary>
    [HttpPost("refund")]
    [RequirePermission(Permissions.Payments.Refund)]
    [ProducesResponseType(typeof(PaymentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult Refund(RefundPaymentRequest request)
    {
        try
        {
            var refunded = payments.Refund(request.PaymentId, request.Reason, User.Identity?.Name);
            if (refunded is null)
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Payment not found");

            logger.LogInformation("Payment {PaymentId} refunded by {UserName}: {Reason}",
                refunded.Id, refunded.RefundedBy, refunded.RefundReason);
            return Ok(refunded);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Payment is already refunded", detail: ex.Message);
        }
    }
}
