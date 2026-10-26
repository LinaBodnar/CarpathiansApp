using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Entities;
using Carpathians.Domain.Exceptions;

namespace Carpathians.Bll.Services;

public class RegionService(IUnitOfWork uow, IMapper mapper) : IRegionService
{
    private readonly IUnitOfWork _uow = uow;
    private readonly IMapper _mapper = mapper;

    public async Task<IReadOnlyList<RegionDto>> GetAllAsync(CancellationToken ct = default)
    {
        var regions = await _uow.Regions.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<RegionDto>>(regions);
    }

    public async Task<RegionDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var region = await _uow.Regions.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Region with ID {id} was not found");

        return _mapper.Map<RegionDto>(region);
    }

    public async Task<RegionDto> CreateAsync(CreateRegionDto input, CancellationToken ct = default)
    {
        if (await _uow.Regions.ExistsByNameAsync(input.Name, ct: ct))
            throw new ConflictException($"Region with name '{input.Name}' already exists");

        var region = new Region(input.Name, input.Description);

        await _uow.Regions.AddAsync(region, ct);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<RegionDto>(region);
    }

    public async Task<RegionDto> UpdateAsync(int id, UpdateRegionDto input, CancellationToken ct = default)
    {
        var region = await _uow.Regions.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException($"Region with ID {id} was not found");

        if (await _uow.Regions.ExistsByNameAsync(input.Name, excludeId: id, ct: ct))
            throw new ConflictException($"Region with name '{input.Name}' already exists");

        region.Update(input.Name, input.Description);

        _uow.Regions.Update(region);
        await _uow.SaveChangesAsync(ct);

        return _mapper.Map<RegionDto>(region);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var region = await _uow.Regions.GetWithRoutesAsync(id, ct)
            ?? throw new EntityNotFoundException($"Region with ID {id} was not found");

        if (region.Routes.Count > 0)
            throw new ConflictException($"Cannot delete region '{region.Name}' because it has linked routes");

        _uow.Regions.Delete(region);
        await _uow.SaveChangesAsync(ct);
    }
}
