using Ardalis.Specification;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;

namespace Carpathians.Bll.Specifications;

/// <summary>
/// Specification for popular challenging routes:
/// filters routes with Difficulty >= Moderate and ElevationGain >= minElevation,
/// includes Region and Detail, and orders by ElevationGain descending.
/// </summary>
public class PopularChallengingRoutesSpec : Specification<TrailRoute>
{
    public PopularChallengingRoutesSpec(Difficulty minDifficulty = Difficulty.Moderate, int minElevation = 500)
    {
        Query.Include(r => r.Region)
             .Include(r => r.Detail)
             .Where(r => r.Difficulty >= minDifficulty && r.ElevationGainM >= minElevation)
             .OrderByDescending(r => r.ElevationGainM);
    }
}

public class PopularChallengingRoutesCountSpec : Specification<TrailRoute>
{
    public PopularChallengingRoutesCountSpec(Difficulty minDifficulty = Difficulty.Moderate, int minElevation = 500)
    {
        Query.Where(r => r.Difficulty >= minDifficulty && r.ElevationGainM >= minElevation);
    }
}
