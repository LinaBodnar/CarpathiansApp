using System.Data;
using Carpathians.Dal.Data;
using Carpathians.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Carpathians.Dal.Repositories;

public class UnitOfWork(
    CatalogDbContext context,
    IRouteRepository routes,
    IPointRepository points,
    IRoutePointRepository routePoints,
    IRegionRepository regions) : IUnitOfWork
{
    private readonly CatalogDbContext _context = context;
    private IDbContextTransaction? _currentTransaction;

    public IRouteRepository Routes { get; } = routes;
    public IPointRepository Points { get; } = points;
    public IRoutePointRepository RoutePoints { get; } = routePoints;
    public IRegionRepository Regions { get; } = regions;

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_currentTransaction is not null)
            throw new InvalidOperationException("A transaction is already active");

        _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        try
        {
            if (_currentTransaction is null)
                throw new InvalidOperationException("No transaction has been started");

            await _context.SaveChangesAsync(ct);
            await _currentTransaction.CommitAsync(ct);
        }
        catch
        {
            await RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (_currentTransaction is not null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.RollbackAsync(ct);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }

        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
