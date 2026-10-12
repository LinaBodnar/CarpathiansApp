using System.Data;
using Carpathians.Dal.Interfaces;
using Microsoft.Data.SqlClient;

namespace Carpathians.Dal.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);

        Customers  = new CustomerRepository(_connection, () => _transaction);
        Departures = new DepartureRepository(_connection, () => _transaction);
        Bookings   = new BookingRepository(_connection, () => _transaction);
    }

    public ICustomerRepository Customers { get; }
    public IDepartureRepository Departures { get; }
    public IBookingRepository Bookings { get; }

    public async Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted,
                                            CancellationToken ct = default)
    {
        if (_connection.State != ConnectionState.Open) await _connection.OpenAsync(ct);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _transaction!.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        try { await _transaction.RollbackAsync(ct); }
        catch (InvalidOperationException) { /* сервер уже відкотив (THROW + XACT_ABORT у процедурі) */ }
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}