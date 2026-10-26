using Ardalis.Specification;

namespace Carpathians.Dal.Interfaces;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);
}
