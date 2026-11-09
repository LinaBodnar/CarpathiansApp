using Carpathians.Domain.Enums;
using Carpathians.Domain.Exceptions;

namespace Carpathians.Domain.Entities;

public class TrailRoute
{
    public long Id { get; private set; }
    public int RegionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public Difficulty Difficulty { get; private set; }
    public decimal DistanceKm { get; private set; }
    public decimal DurationHours { get; private set; }
    public int ElevationGainM { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public Region Region { get; private set; } = null!;
    public RouteDetail? Detail { get; private set; }
    public List<GearItem> GearItems { get; } = new();
    public List<RoutePoint> RoutePoints { get; } = new();

    private TrailRoute() { }

    public TrailRoute(int regionId, string name, string slug, Difficulty difficulty,
                      decimal distanceKm, decimal durationHours, int elevationGainM = 0,
                      string? createdBy = null)
    {
        if (regionId <= 0)
            throw new DomainException("Region ID must be positive");
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Slug is required");

        RegionId = regionId;
        Slug = slug.Trim().ToLowerInvariant();
        CreatedBy = createdBy;
        Apply(name, difficulty, distanceKm, durationHours, elevationGainM);
    }

    public void Update(string name, Difficulty difficulty, decimal distanceKm,
                       decimal durationHours, int elevationGainM, string? updatedBy = null)
    {
        Apply(name, difficulty, distanceKm, durationHours, elevationGainM);
        Touch(updatedBy);
    }

    public void SetDetail(string description, string? season, int? maxAltitudeM,
                          string? startPoint, string? requirements)
    {
        if (Detail is null)
            Detail = new RouteDetail(description, season, maxAltitudeM, startPoint, requirements);
        else
            Detail.Update(description, season, maxAltitudeM, startPoint, requirements);
    }

    public void AddGearItem(string name, bool isRequired = true, string? notes = null)
    {
        if (GearItems.Any(g => g.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"Gear item '{name}' already exists on this route");

        GearItems.Add(new GearItem(name, isRequired, notes));
    }

    public void RemoveGearItem(long gearItemId)
    {
        var item = GearItems.FirstOrDefault(g => g.Id == gearItemId);
        if (item is null)
            throw new DomainException($"Gear item with ID {gearItemId} not found on this route");

        GearItems.Remove(item);
    }

    public void AddPoint(MapPoint point, int sequenceNo)
    {
        if (sequenceNo <= 0)
            throw new DomainException("Sequence number must be positive");
        if (RoutePoints.Any(rp => ReferenceEquals(rp.Point, point) || (rp.PointId != 0 && rp.PointId == point.Id)))
            throw new DomainException($"Point '{point.Name}' is already on this route");
        if (RoutePoints.Any(rp => rp.SequenceNo == sequenceNo))
            throw new DomainException($"Sequence number {sequenceNo} is already used");

        RoutePoints.Add(new RoutePoint(this, point, sequenceNo));
    }

    public void RemovePoint(long pointId)
    {
        var rp = RoutePoints.FirstOrDefault(p => p.PointId == pointId);
        if (rp is null)
            throw new DomainException($"Point with ID {pointId} is not linked to this route");

        RoutePoints.Remove(rp);
    }

    public void MarkDeleted(string? updatedBy = null)
    {
        IsDeleted = true;
        Touch(updatedBy);
    }

    public void Restore(string? updatedBy = null)
    {
        IsDeleted = false;
        Touch(updatedBy);
    }

    private void Apply(string name, Difficulty difficulty, decimal distanceKm,
                       decimal durationHours, int elevationGainM)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Route name is required");
        if (distanceKm <= 0)
            throw new DomainException("Distance must be greater than zero");
        if (durationHours <= 0)
            throw new DomainException("Duration must be greater than zero");
        if (elevationGainM < 0)
            throw new DomainException("Elevation gain cannot be negative");

        Name = name.Trim();
        Difficulty = difficulty;
        DistanceKm = distanceKm;
        DurationHours = durationHours;
        ElevationGainM = elevationGainM;
    }

    private void Touch(string? by)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = by;
    }
}
