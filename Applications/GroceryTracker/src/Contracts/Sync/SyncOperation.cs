namespace GroceryTracker.Contracts.Sync;

public enum SyncEntityType
{
  Household,
  Member,
  Store,
  Category,
  ShoppingTrip,
  ExpenseItem,

  // Appended rather than slotted in: the server applies operations in enum order, and a settlement
  // only needs the members it points at to exist first.
  Settlement,
}

public enum SyncOperationKind
{
  Upsert,
  Delete,
}

/// <summary>
/// A single change made on a device, recorded in the client outbox and replayed to the API once a
/// connection is available.
/// </summary>
/// <param name="OperationId">
/// Idempotency key. The server remembers applied operation ids, so replaying an outbox after a
/// dropped response is safe and never duplicates a write.
/// </param>
/// <param name="BaseSyncStamp">
/// The <c>SyncStamp</c> the device had for this entity when the edit was made, or 0 for a new
/// entity. The server compares it against the live stamp to detect a concurrent edit.
/// </param>
/// <param name="ClientTimestampUtc">
/// When the edit happened on the device. Used as the last-write-wins tiebreaker on conflict.
/// </param>
/// <param name="PayloadJson">
/// The serialised entity for an upsert, null for a delete.
/// </param>
public sealed record SyncOperation(
  Guid OperationId,
  SyncEntityType EntityType,
  Guid EntityId,
  SyncOperationKind Kind,
  long BaseSyncStamp,
  DateTimeOffset ClientTimestampUtc,
  string? PayloadJson);
