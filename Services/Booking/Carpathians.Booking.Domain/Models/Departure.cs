namespace Carpathians.Domain.Models;

public class Departure
{
    public long Id { get; set; }
    public long RouteId { get; set; }
    public DateTime DepartureDate { get; set; }
    public decimal Price { get; set; }
    public int SeatsTotal { get; set; }
    public int SeatsAvailable { get; set; }
    public string? RouteName { get; set; }   
}