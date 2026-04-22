using ShoppingManager.Domain.Model;

namespace ShoppingManager.Infrastructure.Abstractions;

/// <summary>
/// Unit of Work pattern interface for coordinating multiple repositories and managing transactions.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Repository for User entities.
    /// </summary>
    IRepository<User> Users { get; }

    /// <summary>
    /// Repository for Payment entities.
    /// </summary>
    IRepository<Payment> Payments { get; }

    /// <summary>
    /// Repository for PaymentGroup entities.
    /// </summary>
    IRepository<PaymentGroup> PaymentGroups { get; }

    /// <summary>
    /// Begin a database transaction.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit the current transaction.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback the current transaction.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Save all changes made in this unit of work to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
