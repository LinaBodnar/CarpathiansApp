using Ardalis.Specification;
using Carpathians.Bll.Dtos;
using Carpathians.Domain.Entities;

namespace Carpathians.Bll.Specifications;

public class RouteFilterPaginatedSpec : Specification<TrailRoute>
{
    public RouteFilterPaginatedSpec(RouteQueryParameters q)
    {
        Query.Include(r => r.Region)
             .Include(r => r.Detail);

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

        var isDesc = string.Equals(q.SortDir, "desc", StringComparison.OrdinalIgnoreCase);

        switch (q.SortBy?.ToLowerInvariant())
        {
            case "name":
                if (isDesc) Query.OrderByDescending(r => r.Name);
                else Query.OrderBy(r => r.Name);
                break;

            case "distance":
                if (isDesc) Query.OrderByDescending(r => r.DistanceKm);
                else Query.OrderBy(r => r.DistanceKm);
                break;

            case "elevation":
                if (isDesc) Query.OrderByDescending(r => r.ElevationGainM);
                else Query.OrderBy(r => r.ElevationGainM);
                break;

            case "difficulty":
                if (isDesc) Query.OrderByDescending(r => r.Difficulty);
                else Query.OrderBy(r => r.Difficulty);
                break;

            case "created":
            default:
                if (isDesc || string.IsNullOrEmpty(q.SortDir)) Query.OrderByDescending(r => r.CreatedAt);
                else Query.OrderBy(r => r.CreatedAt);
                break;
        }

        var skip = (q.Page - 1) * q.PageSize;
        Query.Skip(skip).Take(q.PageSize);
    }
}
