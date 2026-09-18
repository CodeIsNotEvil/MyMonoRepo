using GroceryTracker.Domain.Model;

namespace GroceryTracker.Contracts.Sync;

/// <summary>
/// One round trip of the sync protocol: push everything queued on the device, then pull everything
/// the device has not seen. Combining both halves keeps the device's cursor consistent with the
/// writes it just made.
/// </summary>
/// <param name="Cursor">Highest <c>SyncStamp</c> the device already holds. 0 for a first sync.</param>
public sealed record SyncRequest(
  long Cursor,
  IReadOnlyList<SyncOperation> Operations)
{
  public static SyncRequest Pull(long cursor) => new(cursor, []);
}

public enum ConflictOutcome
{
  /// <summary>The device's edit was older, so the server's version stands and the client must reload.</summary>
  ServerWon,

  /// <summary>The device's edit was newer and overwrote a change the device had not seen yet.</summary>
  ClientWon,

  /// <summary>
  /// The operation could never be applied, for example it referenced a store that does not exist.
  /// Reported so the device drops it instead of retrying a write that will fail forever.
  /// </summary>
  Rejected,
}

/// <summary>
/// Raised when an operation targeted an entity that had moved on since the device last saw it.
/// Reported even when the client wins, so the UI can tell the user something was overwritten.
/// </summary>
public sealed record SyncConflict(
  Guid OperationId,
  SyncEntityType EntityType,
  Guid EntityId,
  ConflictOutcome Outcome,
  long ServerSyncStamp,
  string Reason);

/// <summary>Everything that changed on the server after the client's cursor.</summary>
public sealed record SyncPayload(
  IReadOnlyList<Household> Households,
  IReadOnlyList<Member> Members,
  IReadOnlyList<Store> Stores,
  IReadOnlyList<Category> Categories,
  IReadOnlyList<ShoppingTrip> Trips,
  IReadOnlyList<ExpenseItem> Items)
{
  public static SyncPayload Empty { get; } = new([], [], [], [], [], []);

  public int Count => Households.Count + Members.Count + Stores.Count
    + Categories.Count + Trips.Count + Items.Count;
}

/// <param name="Cursor">The cursor the device should store and send next time.</param>
/// <param name="AppliedOperationIds">
/// Operations the device may now drop from its outbox. An operation rejected by a conflict is also
/// listed here, because retrying it would only lose again; the device reloads the server copy instead.
/// </param>
public sealed record SyncResponse(
  long Cursor,
  IReadOnlyList<Guid> AppliedOperationIds,
  IReadOnlyList<SyncConflict> Conflicts,
  SyncPayload Payload,
  DateTimeOffset ServerTimeUtc);
