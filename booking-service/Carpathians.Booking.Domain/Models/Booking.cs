using Carpathians.Domain.Constants;

namespace Carpathians.Domain.Models;

public class Booking
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = BookingStatus.Submitted;
    public DateTime BookingDate { get; set; }
    public decimal TotalAmount { get; set; }
    // BookingDetails (1:1)
    public string ContactPhone { get; set; } = "";
    public string MeetingPoint { get; set; } = "";
    public string? Comment { get; set; }
    public List<BookingItem> Items { get; set; } = new();
}

public class BookingItem
{
    public long BookingId { get; set; }
    public long DepartureId { get; set; }
    public string RouteName { get; set; } = "";   
    public decimal UnitPrice { get; set; }        
    public int Seats { get; set; }
}