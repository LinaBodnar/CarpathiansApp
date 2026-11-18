using Carpathians.Domain.Models;
namespace Carpathians.Dal.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct = default);
    Task<long> AddAsync(Customer customer, CancellationToken ct = default);
    Task<bool> UpdateAsync(Customer customer, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}