using Ardalis.Specification;
using Carpathians.Bll.Dtos;
using Carpathians.Domain.Entities;

namespace Carpathians.Bll.Specifications;

public class RouteFilterCountSpec : Specification<TrailRoute>
{
    public RouteFilterCountSpec(RouteQueryParameters q)
    {
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var search = q.Search.Trim();
            Query.Where(r => r.Name.Contains(search) || r.Slug.Contains(search));
        }

        if (q.RegionId.HasValue)
        {
            Query.Where(r => r.RegionId == q.RegionId.Value);
        }

        if (q.Difficulty.HasValue)
        {
            Query.Where(r => r.Difficulty == q.Difficulty.Value);
        }

        if (q.MaxDistanceKm.HasValue)
        {
            Query.Where(r => r.DistanceKm <= q.MaxDistanceKm.Value);
        }
    }
}
