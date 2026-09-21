namespace GroceryTracker.Domain.Sync;

/// <summary>
/// Implemented by every entity that participates in offline synchronisation.
/// </summary>
/// <remarks>
/// <see cref="SyncStamp"/> is a server-assigned value from a single global counter that is
/// incremented atomically inside the same transaction as the write. Because the counter row is
/// locked for the duration of that transaction, stamps are handed out in commit order, so a client
/// that pulls everything with <c>SyncStamp &gt; cursor</c> can never step over a concurrent write
/// the way a wall-clock cursor would.
/// </remarks>
public interface ISyncEntity
{
    Guid Id { get; }

    Guid HouseholdId { get; set; }

    long SyncStamp { get; set; }

    DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>
    /// Tombstone marker. Rows are never physically removed, otherwise a client that was offline
    /// during the delete would never learn about it.
    /// </summary>
    bool IsDeleted { get; set; }
}
