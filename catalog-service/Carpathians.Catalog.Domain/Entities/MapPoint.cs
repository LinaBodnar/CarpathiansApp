using Carpathians.Domain.Enums;
using Carpathians.Domain.Exceptions;

namespace Carpathians.Domain.Entities;

public class MapPoint
{
    public long Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public int? AltitudeM { get; private set; }
    public PointType PointType { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public List<GalleryImage> GalleryImages { get; } = new();
    public List<RoutePoint> RoutePoints { get; } = new();

    private MapPoint() { }

    public MapPoint(string name, decimal latitude, decimal longitude,
                    PointType pointType, int? altitudeM = null)
    {
        ValidateAndSet(name, latitude, longitude, pointType, altitudeM);
    }

    public void Update(string name, decimal latitude, decimal longitude,
                       PointType pointType, int? altitudeM = null)
    {
        ValidateAndSet(name, latitude, longitude, pointType, altitudeM);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddImage(string fileName, string url, string? caption, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(url))
            throw new DomainException("File name and URL are required");
        if (sortOrder < 0)
            throw new DomainException("Sort order cannot be negative");
        if (GalleryImages.Any(i => i.FileName.Equals(fileName.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"Image '{fileName}' already exists for this point");

        GalleryImages.Add(new GalleryImage(fileName, url, caption, sortOrder));
    }

    public void RemoveImage(long imageId)
    {
        var image = GalleryImages.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
            throw new DomainException($"Image with ID {imageId} not found for this point");

        GalleryImages.Remove(image);
    }

    public void MarkDeleted()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        UpdatedAt = DateTime.UtcNow;
    }

    private void ValidateAndSet(string name, decimal latitude, decimal longitude,
                                PointType pointType, int? altitudeM)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Point name is required");
        if (latitude is < -90 or > 90)
            throw new DomainException("Latitude must be between -90 and 90 degrees");
        if (longitude is < -180 or > 180)
            throw new DomainException("Longitude must be between -180 and 180 degrees");
        if (altitudeM is < 0)
            throw new DomainException("Altitude cannot be negative");

        Name = name.Trim();
        Latitude = latitude;
        Longitude = longitude;
        PointType = pointType;
        AltitudeM = altitudeM;
    }
}
