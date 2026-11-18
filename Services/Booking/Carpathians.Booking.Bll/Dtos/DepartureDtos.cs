using System.ComponentModel.DataAnnotations;
namespace Carpathians.Bll.Dtos;

public record DepartureDto(long Id, long RouteId, string? RouteName, DateTime DepartureDate,
    decimal Price, int SeatsTotal, int SeatsAvailable);

public class CreateDepartureDto
{
    [Range(1, long.MaxValue)] public long RouteId { get; set; }
    [Required] public DateTime DepartureDate { get; set; }
    [Range(0.01, 1_000_000)] public decimal Price { get; set; }
    [Range(1, 1000)] public int SeatsTotal { get; set; }
}

public class UpdateDepartureDto
{
    [Required] public DateTime DepartureDate { get; set; }
    [Range(0.01, 1_000_000)] public decimal Price { get; set; }
}