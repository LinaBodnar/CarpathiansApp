using Carpathians.Reviews.Domain.Common;
using Carpathians.Reviews.Domain.Exceptions;

namespace Carpathians.Reviews.Domain.ValueObjects;

/// <summary>Точка GPS-треку маршруту: <c>{ lat, lng, ele }</c>.</summary>
public sealed class GeoPoint : ValueObject
{
    public double Lat { get; }
    public double Lng { get; }

    /// <summary>Висота над рівнем моря, м (необов'язкова).</summary>
    public double? Ele { get; }

    public GeoPoint(double lat, double lng, double? ele = null)
    {
        if (lat is < -90 or > 90)
            throw new DomainException("Latitude must be between -90 and 90");
        if (lng is < -180 or > 180)
            throw new DomainException("Longitude must be between -180 and 180");
        if (ele is < -500 or > 9000)
            throw new DomainException("Elevation must be between -500 and 9000 meters");

        Lat = lat;
        Lng = lng;
        Ele = ele;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Lat;
        yield return Lng;
        yield return Ele;
    }
}
