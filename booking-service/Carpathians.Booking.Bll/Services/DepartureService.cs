using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Exceptions;
using Carpathians.Domain.Models;
namespace Carpathians.Bll.Services;

public sealed class DepartureService(IUnitOfWork uow, IMapper mapper) : IDepartureService
{
    public async Task<DepartureDto> GetByIdAsync(long id, CancellationToken ct)
        => mapper.Map<DepartureDto>(await uow.Departures.GetWithRouteNameAsync(id, ct)
                                    ?? throw new NotFoundException($"Departure {id} not found"));

    public async Task<IReadOnlyList<DepartureDto>> GetAllAsync(CancellationToken ct)
        => mapper.Map<List<DepartureDto>>(await uow.Departures.GetAllAsync(ct));

    public async Task<DepartureDto> CreateAsync(CreateDepartureDto input, CancellationToken ct)
    {
        if (input.DepartureDate.Date < DateTime.UtcNow.Date)
            throw new ValidationException("Departure date is in the past");
        var entity = mapper.Map<Departure>(input);
        var id = await uow.Departures.AddAsync(entity, ct);   
        return await GetByIdAsync(id, ct);
    }

    public async Task<DepartureDto> UpdateAsync(long id, UpdateDepartureDto input, CancellationToken ct)
    {
        var entity = await uow.Departures.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException($"Departure {id} not found");
        entity.DepartureDate = input.DepartureDate;
        entity.Price = input.Price;
        if (!await uow.Departures.UpdateAsync(entity, ct))
            throw new NotFoundException($"Departure {id} not found");
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        if (!await uow.Departures.DeleteAsync(id, ct))
            throw new NotFoundException($"Departure {id} not found");
    }
}