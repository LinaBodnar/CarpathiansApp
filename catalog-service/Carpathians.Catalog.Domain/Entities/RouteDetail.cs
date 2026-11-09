using Carpathians.Domain.Exceptions;

namespace Carpathians.Domain.Entities;

public class RouteDetail
{
    public long RouteId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? Season { get; private set; }
    public int? MaxAltitudeM { get; private set; }
    public string? StartPoint { get; private set; }
    public string? Requirements { get; private set; }

    private RouteDetail() { }

    internal RouteDetail(string description, string? season, int? maxAltitudeM,
                         string? startPoint, string? requirements)
    {
        Update(description, season, maxAltitudeM, startPoint, requirements);
    }

    public void Update(string description, string? season, int? maxAltitudeM,
                       string? startPoint, string? requirements)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Description is required");
        if (maxAltitudeM is <= 0)
            throw new DomainException("Max altitude must be positive");

        Description = description.Trim();
        Season = season?.Trim();
        MaxAltitudeM = maxAltitudeM;
        StartPoint = startPoint?.Trim();
        Requirements = requirements?.Trim();
    }
}
