using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Exceptions;

namespace Carpathians.Bll.Services;

public class RouteService(IUnitOfWork uow, IMapper mapper) : IRouteService
{
    private readonly IUnitOfWork _uow = uow;
    private readonly IMapper _mapper = mapper;

    public async Task<IReadOnlyList<RouteDto>> GetAllAsync(CancellationToken ct = default)
    {
        var routes = await _uow.Routes.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<RouteDto>>(routes);
    }

    public async Task<RouteDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsEagerAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        return _mapper.Map<RouteDto>(route);
    }

    public async Task<RouteDto> GetWithDetailsExplicitAsync(long id, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsExplicitAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        return _mapper.Map<RouteDto>(route);
    }

    public async Task<RouteDto> CreateAsync(CreateRouteDto input, CancellationToken ct = default)
    {
        var region = await _uow.Regions.GetByIdAsync(input.RegionId, ct)
            ?? throw new EntityNotFoundException($"Region with ID {input.RegionId} was not found");

        if (await _uow.Routes.ExistsBySlugAsync(input.Slug, ct: ct))
            throw new ConflictException($"Route with slug '{input.Slug}' already exists");

        var route = new TrailRoute(
            input.RegionId,
            input.Name,
            input.Slug,
            input.Difficulty,
            input.DistanceKm,
            input.DurationHours,
            input.ElevationGainM,
            input.CreatedBy);

        if (input.Detail is not null)
        {
            route.SetDetail(
                input.Detail.Description,
                input.Detail.Season,
                input.Detail.MaxAltitudeM,
                input.Detail.StartPoint,
                input.Detail.Requirements);
        }

        await _uow.Routes.AddAsync(route, ct);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(route.Id, ct);
    }

    public async Task<RouteDto> UpdateAsync(long id, UpdateRouteDto input, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsEagerAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        route.Update(
            input.Name,
            input.Difficulty,
            input.DistanceKm,
            input.DurationHours,
            input.ElevationGainM,
            input.UpdatedBy);

        _uow.Routes.Update(route);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<RouteDto>(route);
    }

    public async Task<RouteDto> SetDetailAsync(long id, SetRouteDetailDto input, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsEagerAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        route.SetDetail(
            input.Description,
            input.Season,
            input.MaxAltitudeM,
            input.StartPoint,
            input.Requirements);

        _uow.Routes.Update(route);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<RouteDto>(route);
    }

    public async Task<RouteDto> AddGearItemAsync(long id, AddGearItemDto input, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsEagerAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        route.AddGearItem(input.Name, input.IsRequired, input.Notes);

        _uow.Routes.Update(route);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<RouteDto>(route);
    }

    public async Task<RouteDto> RemoveGearItemAsync(long id, long gearItemId, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsEagerAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        route.RemoveGearItem(gearItemId);

        _uow.Routes.Update(route);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<RouteDto>(route);
    }

    public async Task<RouteDto> AddPointAsync(long id, AddRoutePointDto input, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsEagerAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        var point = await _uow.Points.GetByIdAsync(input.PointId, ct)
            ?? throw new EntityNotFoundException($"Point with ID {input.PointId} was not found");

        route.AddPoint(point, input.SequenceNo);

        _uow.Routes.Update(route);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<RouteDto> RemovePointAsync(long id, long pointId, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetWithDetailsEagerAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        route.RemovePoint(pointId);

        _uow.Routes.Update(route);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(long id, string? deletedBy = null, CancellationToken ct = default)
    {
        var route = await _uow.Routes.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Route with ID {id} was not found");

        route.MarkDeleted(deletedBy);
        _uow.Routes.Update(route);
        await _uow.SaveChangesAsync(ct);
    }
}
