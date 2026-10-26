using Carpathians.Bll.Dtos;
using Carpathians.Domain.Enums;

namespace Carpathians.Bll.Interfaces;

public interface IRouteService
{
    Task<IReadOnlyList<RouteDto>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<RouteDto>> GetPagedAsync(RouteQueryParameters queryParams, CancellationToken ct = default);
    Task<RouteDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<RouteDto> GetWithDetailsExplicitAsync(long id, CancellationToken ct = default);
    Task<RouteDto> CreateAsync(CreateRouteDto input, CancellationToken ct = default);
    Task<RouteDto> UpdateAsync(long id, UpdateRouteDto input, CancellationToken ct = default);
    Task<RouteDto> SetDetailAsync(long id, SetRouteDetailDto input, CancellationToken ct = default);
    Task<RouteDto> AddGearItemAsync(long id, AddGearItemDto input, CancellationToken ct = default);
    Task<RouteDto> RemoveGearItemAsync(long id, long gearItemId, CancellationToken ct = default);
    Task<RouteDto> AddPointAsync(long id, AddRoutePointDto input, CancellationToken ct = default);
    Task<RouteDto> RemovePointAsync(long id, long pointId, CancellationToken ct = default);
    Task DeleteAsync(long id, string? deletedBy = null, CancellationToken ct = default);
}

public interface IPointService
{
    Task<IReadOnlyList<MapPointDto>> GetAllAsync(CancellationToken ct = default);
    Task<MapPointDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<MapPointDto>> GetByTypeAsync(PointType pointType, CancellationToken ct = default);
    Task<MapPointDto> CreateAsync(CreatePointDto input, CancellationToken ct = default);
    Task<MapPointDto> UpdateAsync(long id, UpdatePointDto input, CancellationToken ct = default);
    Task<MapPointDto> AddImageAsync(long id, AddGalleryImageDto input, CancellationToken ct = default);
    Task<MapPointDto> RemoveImageAsync(long id, long imageId, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}

public interface IRegionService
{
    Task<IReadOnlyList<RegionDto>> GetAllAsync(CancellationToken ct = default);
    Task<RegionDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<RegionDto> CreateAsync(CreateRegionDto input, CancellationToken ct = default);
    Task<RegionDto> UpdateAsync(int id, UpdateRegionDto input, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
