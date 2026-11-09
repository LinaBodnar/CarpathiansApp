using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Enums;
using Carpathians.Domain.Exceptions;

namespace Carpathians.Bll.Services;

public class PointService(IUnitOfWork uow, IMapper mapper) : IPointService
{
    private readonly IUnitOfWork _uow = uow;
    private readonly IMapper _mapper = mapper;

    public async Task<IReadOnlyList<MapPointDto>> GetAllAsync(CancellationToken ct = default)
    {
        var points = await _uow.Points.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<MapPointDto>>(points);
    }

    public async Task<MapPointDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var point = await _uow.Points.GetWithGalleryAsync(id, ct)
            ?? throw new EntityNotFoundException($"Point with ID {id} was not found");

        return _mapper.Map<MapPointDto>(point);
    }

    public async Task<IReadOnlyList<MapPointDto>> GetByTypeAsync(PointType pointType, CancellationToken ct = default)
    {
        var points = await _uow.Points.GetByTypeAsync(pointType, ct);
        return _mapper.Map<IReadOnlyList<MapPointDto>>(points);
    }

    public async Task<MapPointDto> CreateAsync(CreatePointDto input, CancellationToken ct = default)
    {
        if (await _uow.Points.ExistsByNameAsync(input.Name, ct: ct))
            throw new ConflictException($"Point with name '{input.Name}' already exists");

        var point = new MapPoint(
            input.Name,
            input.Latitude,
            input.Longitude,
            input.PointType,
            input.AltitudeM);

        await _uow.Points.AddAsync(point, ct);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<MapPointDto>(point);
    }

    public async Task<MapPointDto> UpdateAsync(long id, UpdatePointDto input, CancellationToken ct = default)
    {
        var point = await _uow.Points.GetWithGalleryAsync(id, ct)
            ?? throw new EntityNotFoundException($"Point with ID {id} was not found");

        if (await _uow.Points.ExistsByNameAsync(input.Name, excludeId: id, ct: ct))
            throw new ConflictException($"Point with name '{input.Name}' already exists");

        point.Update(
            input.Name,
            input.Latitude,
            input.Longitude,
            input.PointType,
            input.AltitudeM);

        _uow.Points.Update(point);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<MapPointDto>(point);
    }

    public async Task<MapPointDto> AddImageAsync(long id, AddGalleryImageDto input, CancellationToken ct = default)
    {
        var point = await _uow.Points.GetWithGalleryAsync(id, ct)
            ?? throw new EntityNotFoundException($"Point with ID {id} was not found");

        point.AddImage(input.FileName, input.Url, input.Caption, input.SortOrder);

        _uow.Points.Update(point);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<MapPointDto>(point);
    }

    public async Task<MapPointDto> RemoveImageAsync(long id, long imageId, CancellationToken ct = default)
    {
        var point = await _uow.Points.GetWithGalleryAsync(id, ct)
            ?? throw new EntityNotFoundException($"Point with ID {id} was not found");

        point.RemoveImage(imageId);

        _uow.Points.Update(point);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<MapPointDto>(point);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var point = await _uow.Points.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Point with ID {id} was not found");

        point.MarkDeleted();
        _uow.Points.Update(point);
        await _uow.SaveChangesAsync(ct);
    }
}
