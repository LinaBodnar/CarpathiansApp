using Carpathians.Domain.Exceptions;

namespace Carpathians.Domain.Entities;

public class RoutePoint
{
    public long RouteId { get; private set; }
    public TrailRoute Route { get; private set; } = null!;
    public long PointId { get; private set; }
    public MapPoint Point { get; private set; } = null!;
    public int SequenceNo { get; private set; }
    public DateTime AddedAt { get; private set; }

    private RoutePoint() { }

    internal RoutePoint(TrailRoute route, MapPoint point, int sequenceNo)
    {
        if (sequenceNo <= 0)
            throw new DomainException("Sequence number must be positive");

        Route = route;
        RouteId = route.Id;
        Point = point;
        PointId = point.Id;
        SequenceNo = sequenceNo;
    }

    public void UpdateSequence(int newSequenceNo)
    {
        if (newSequenceNo <= 0)
            throw new DomainException("Sequence number must be positive");

        SequenceNo = newSequenceNo;
    }
}
