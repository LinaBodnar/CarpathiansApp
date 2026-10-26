using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Dal.Repositories;

public class GenericRepository<T>(CatalogDbContext context) : IGenericRepository<T> where T : class
{
    protected readonly CatalogDbContext Context = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await DbSet.AddAsync(entity, ct);

    public virtual void Update(T entity)
        => DbSet.Update(entity);

    public virtual void Delete(T entity)
        => DbSet.Remove(entity);
}
