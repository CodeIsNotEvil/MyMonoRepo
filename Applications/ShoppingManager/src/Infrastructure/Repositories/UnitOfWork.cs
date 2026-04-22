using Microsoft.EntityFrameworkCore.Storage;
using ShoppingManager.Domain.Model;
using ShoppingManager.Infrastructure.Abstractions;
using ShoppingManager.Infrastructure.Data;

namespace ShoppingManager.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation that coordinates repositories and manages transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ShoppingManagerDbContext _context;
    private IDbContextTransaction? _transaction;

    private IRepository<User>? _userRepository;
    private IRepository<Payment>? _paymentRepository;
    private IRepository<PaymentGroup>? _paymentGroupRepository;

    public UnitOfWork(ShoppingManagerDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IRepository<User> Users => _userRepository ??= new Repository<User>(_context);
    public IRepository<Payment> Payments => _paymentRepository ??= new Repository<Payment>(_context);
    public IRepository<PaymentGroup> PaymentGroups => _paymentGroupRepository ??= new Repository<PaymentGroup>(_context);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (_transaction is not null)
            {
                await _transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_transaction is not null)
            {
                await _transaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }
        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
