using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

namespace Carpathians.Payments.Api.Payments;

public static class PaymentStatus
{
    public const string Processed = "Processed";
    public const string Refunded = "Refunded";
}

public sealed record PaymentDto(
    Guid Id,
    long BookingId,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    DateTime ProcessedAt,
    string? ProcessedBy,
    DateTime? RefundedAt,
    string? RefundedBy,
    string? RefundReason);

public sealed record ProcessPaymentRequest(
    [Range(1, long.MaxValue)] long BookingId,
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Amount,
    [Required, RegularExpression("^(Card|Cash|Transfer)$")] string Method,
    [StringLength(3, MinimumLength = 3)] string Currency = "UAH");

public sealed record RefundPaymentRequest(
    Guid PaymentId,
    [Required, StringLength(200)] string Reason);

/// <summary>
/// Сховище платежів у пам'яті: сервіс — заглушка для демонстрації доступу (лише admin), без реальної платіжної системи.
/// </summary>
public sealed class PaymentStore(TimeProvider time)
{
    private readonly ConcurrentDictionary<Guid, PaymentDto> _payments = new();

    public IReadOnlyList<PaymentDto> List(long? bookingId)
        => _payments.Values
            .Where(p => bookingId is null || p.BookingId == bookingId)
            .OrderByDescending(p => p.ProcessedAt)
            .ToList();

    public PaymentDto Process(ProcessPaymentRequest request, string? processedBy)
    {
        var payment = new PaymentDto(Guid.NewGuid(), request.BookingId, request.Amount, request.Currency.ToUpperInvariant(),
            request.Method, PaymentStatus.Processed, time.GetUtcNow().UtcDateTime, processedBy, null, null, null);
        _payments[payment.Id] = payment;
        return payment;
    }

    /// <summary>Повернення коштів: null — платежу немає; виняток — уже повернено.</summary>
    public PaymentDto? Refund(Guid paymentId, string reason, string? refundedBy)
    {
        while (true)
        {
            if (!_payments.TryGetValue(paymentId, out var current))
                return null;
            if (current.Status == PaymentStatus.Refunded)
                throw new InvalidOperationException($"Payment {paymentId} is already refunded");

            var refunded = current with
            {
                Status = PaymentStatus.Refunded,
                RefundedAt = time.GetUtcNow().UtcDateTime,
                RefundedBy = refundedBy,
                RefundReason = reason
            };
            if (_payments.TryUpdate(paymentId, refunded, current))
                return refunded;
        }
    }
}
