using System.ComponentModel.DataAnnotations;
namespace Carpathians.Bll.Dtos;

public record BookingItemDto(long DepartureId, string RouteName, decimal UnitPrice, int Seats);

public record BookingDto(long Id, long CustomerId, string Status, DateTime BookingDate,
    decimal TotalAmount, string ContactPhone, string MeetingPoint,
    string? Comment, IReadOnlyList<BookingItemDto> Items);

public class CreateBookingDto
{
    [Range(1, long.MaxValue)] public long CustomerId { get; set; }
    [Required, StringLength(30, MinimumLength = 5)] public string ContactPhone { get; set; } = "";
    [Required, StringLength(400)] public string MeetingPoint { get; set; } = "";
    [StringLength(1000)] public string? Comment { get; set; }
    [Required, MinLength(1)] public List<CreateBookingItemDto> Items { get; set; } = new();
}

public class CreateBookingItemDto
{
    [Range(1, long.MaxValue)] public long DepartureId { get; set; }
    [Range(1, 100)] public int Seats { get; set; }
}

public class PayBookingDto
{
    [Required, RegularExpression("^(Card|Cash|Transfer)$")]
    public string Method { get; set; } = "";
}

public record PaymentResultDto(long PaymentId);