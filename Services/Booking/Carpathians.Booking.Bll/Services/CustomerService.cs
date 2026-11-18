using AutoMapper;
using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Dal.Interfaces;
using Carpathians.Domain.Exceptions;
using Carpathians.Domain.Models;
namespace Carpathians.Bll.Services;

public sealed class CustomerService(IUnitOfWork uow, IMapper mapper) : ICustomerService
{
    public async Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct)
        => mapper.Map<CustomerDto>(await uow.Customers.GetByIdAsync(id, ct)
                                   ?? throw new NotFoundException($"Customer {id} not found"));

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken ct)
        => mapper.Map<List<CustomerDto>>(await uow.Customers.GetAllAsync(ct));

    public async Task<CustomerDto> CreateAsync(CustomerInputDto input, CancellationToken ct)
    {
        var entity = mapper.Map<Customer>(input);
        var id = await uow.Customers.AddAsync(entity, ct);       
        return await GetByIdAsync(id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(long id, CustomerInputDto input, CancellationToken ct)
    {
        var entity = await uow.Customers.GetByIdAsync(id, ct)
                     ?? throw new NotFoundException($"Customer {id} not found");
        entity.Email = input.Email;
        entity.FullName = input.FullName;
        entity.Phone = input.Phone;
        if (!await uow.Customers.UpdateAsync(entity, ct))
            throw new NotFoundException($"Customer {id} not found");
        return mapper.Map<CustomerDto>(entity);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        if (!await uow.Customers.DeleteAsync(id, ct))
            throw new NotFoundException($"Customer {id} not found");
    }
}