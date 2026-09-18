namespace GroceryTracker.Infrastructure.Data;

/// <summary>
/// Single-row table holding the household's global change counter.
/// </summary>
/// <remarks>
/// Every write bumps this row inside its own transaction, which locks it until commit. That lock is
/// what makes sync stamps strictly commit-ordered: a client pulling <c>SyncStamp &gt; cursor</c>
/// cannot miss a write that committed after it started reading.
/// </remarks>
public sealed class SyncCounter
{
  public const int SingletonId = 1;

  public int Id { get; init; } = SingletonId;

  public long LastStamp { get; set; }
}

/// <summary>
/// Remembers which client operations were already applied so a replayed outbox is a no-op.
/// </summary>
public sealed class AppliedSyncOperation
{
  public Guid OperationId { get; init; }

  public Guid EntityId { get; init; }

  public string EntityType { get; init; } = string.Empty;

  public DateTimeOffset AppliedAtUtc { get; init; }
}
