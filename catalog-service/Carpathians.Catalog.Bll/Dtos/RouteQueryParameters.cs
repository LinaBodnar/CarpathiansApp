using Carpathians.Domain.Enums;

namespace Carpathians.Bll.Dtos;

public record RouteQueryParameters
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value, 1, 100);
    }

    public string? Search { get; init; }
    public int? RegionId { get; init; }
    public Difficulty? Difficulty { get; init; }
    public decimal? MaxDistanceKm { get; init; }
    public string? SortBy { get; init; }
    public string? SortDir { get; init; }
}
