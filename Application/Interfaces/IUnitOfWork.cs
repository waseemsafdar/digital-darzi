namespace Application.Interfaces;

/// <summary>
/// Unit of Work pattern for managing transactions at the service layer.
/// Ensures multiple repository operations execute within a single atomic transaction.
/// Uses IDisposable to avoid EF Core dependency in the Application layer.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>Begins a new database transaction. Returns a disposable handle.</summary>
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>Saves all pending changes to the database (without committing the transaction).</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Commits the current transaction and saves changes atomically.</summary>
    Task CommitAsync(CancellationToken ct = default);

    /// <summary>Rolls back the current transaction and discards changes.</summary>
    Task RollbackAsync(CancellationToken ct = default);
}
