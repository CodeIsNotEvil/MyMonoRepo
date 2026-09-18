using System.Text.Json;
using GroceryTracker.Contracts.Sync;
using GroceryTracker.Domain.Model;
using GroceryTracker.Domain.Sync;
using GroceryTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroceryTracker.Application.Sync;

public interface ISyncService
{
  Task<SyncResponse> SynchroniseAsync(SyncRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies a device's queued changes and returns everything that device has not seen yet.
/// </summary>
/// <remarks>
/// Push and pull share one transaction so the cursor handed back always covers the writes made in
/// the same call. Conflicts are settled last-write-wins on the client's edit time, but are still
/// reported either way so the UI can tell the user when something was overwritten.
/// </remarks>
public sealed class SyncService : ISyncService
{
  private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

  private readonly GroceryTrackerDbContext _db;
  private readonly TimeProvider _timeProvider;
  private readonly ILogger<SyncService> _logger;

  public SyncService(
    GroceryTrackerDbContext db,
    TimeProvider timeProvider,
    ILogger<SyncService> logger)
  {
    _db = db ?? throw new ArgumentNullException(nameof(db));
    _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  public async Task<SyncResponse> SynchroniseAsync(
    SyncRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    // A cursor from the future can only come from a database that has since been wiped: the counter
    // never goes backwards. Applying the device's queued changes to a household that no longer
    // exists would just get them rejected and dropped from its outbox, so refuse the whole request.
    var lastIssued = await _db.SyncCounters.AsNoTracking()
      .Select(c => c.LastStamp)
      .SingleOrDefaultAsync(cancellationToken);

    if (request.Cursor > lastIssued)
    {
      _logger.LogWarning(
        "Refusing sync: device cursor {Cursor} is ahead of the server counter {Counter}; the database was reset.",
        request.Cursor,
        lastIssued);

      return new SyncResponse(request.Cursor, [], [], SyncPayload.Empty, _timeProvider.GetUtcNow(), ServerReset: true);
    }

    var applied = new List<Guid>();
    var conflicts = new List<SyncConflict>();

    await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

    try
    {
      if (request.Operations.Count > 0)
      {
        await ApplyOperationsAsync(request, applied, conflicts, cancellationToken);
      }

      var payload = await PullAsync(request.Cursor, cancellationToken);
      var cursor = Math.Max(request.Cursor, HighestStamp(payload));

      await transaction.CommitAsync(cancellationToken);

      return new SyncResponse(
        cursor,
        applied,
        conflicts,
        payload,
        _timeProvider.GetUtcNow());
    }
    catch
    {
      await transaction.RollbackAsync(cancellationToken);
      throw;
    }
  }

  private async Task ApplyOperationsAsync(
    SyncRequest request,
    List<Guid> applied,
    List<SyncConflict> conflicts,
    CancellationToken cancellationToken)
  {
    var incomingIds = request.Operations.Select(o => o.OperationId).ToList();

    var alreadyApplied = await _db.AppliedSyncOperations
      .Where(o => incomingIds.Contains(o.OperationId))
      .Select(o => o.OperationId)
      .ToListAsync(cancellationToken);

    var seen = alreadyApplied.ToHashSet();

    // Parents before children, so a trip and its items can arrive in the same batch without the
    // items tripping over a foreign key that does not exist yet.
    var ordered = request.Operations
      .Where(o => !seen.Contains(o.OperationId))
      .OrderBy(o => (int)o.EntityType)
      .ToList();

    applied.AddRange(alreadyApplied);

    var newlyApplied = new List<AppliedSyncOperation>();

    foreach (var operation in ordered)
    {
      // A duplicate inside a single batch would otherwise insert the same key twice.
      if (!seen.Add(operation.OperationId))
      {
        continue;
      }

      var outcome = await ApplyOneAsync(operation, cancellationToken);

      if (outcome is not null)
      {
        conflicts.Add(outcome);
      }

      applied.Add(operation.OperationId);
      newlyApplied.Add(new AppliedSyncOperation
      {
        OperationId = operation.OperationId,
        EntityId = operation.EntityId,
        EntityType = operation.EntityType.ToString(),
        AppliedAtUtc = _timeProvider.GetUtcNow(),
      });
    }

    if (newlyApplied.Count > 0)
    {
      _db.AppliedSyncOperations.AddRange(newlyApplied);
    }

    await _db.SaveChangesAsync(cancellationToken);
  }

  private Task<SyncConflict?> ApplyOneAsync(SyncOperation operation, CancellationToken cancellationToken) =>
    operation.EntityType switch
    {
      SyncEntityType.Household => ApplyToSetAsync(_db.Households, operation, CopyHousehold, null, cancellationToken),
      SyncEntityType.Member => ApplyToSetAsync(_db.Members, operation, CopyMember, ValidateMemberAsync, cancellationToken),
      SyncEntityType.Store => ApplyToSetAsync(_db.Stores, operation, CopyStore, ValidateStoreAsync, cancellationToken),
      SyncEntityType.Category => ApplyToSetAsync(_db.Categories, operation, CopyCategory, ValidateCategoryAsync, cancellationToken),
      SyncEntityType.ShoppingTrip => ApplyToSetAsync(_db.Trips, operation, CopyTrip, ValidateTripAsync, cancellationToken),
      SyncEntityType.ExpenseItem => ApplyToSetAsync(_db.Items, operation, CopyItem, ValidateItemAsync, cancellationToken),
      SyncEntityType.Settlement => ApplyToSetAsync(_db.Settlements, operation, CopySettlement, ValidateSettlementAsync, cancellationToken),
      _ => Task.FromResult<SyncConflict?>(Reject(operation, $"Unknown entity type '{operation.EntityType}'.")),
    };

  private async Task<SyncConflict?> ApplyToSetAsync<TEntity>(
    DbSet<TEntity> set,
    SyncOperation operation,
    Action<TEntity, TEntity> copyInto,
    Func<TEntity, CancellationToken, Task<string?>>? validate,
    CancellationToken cancellationToken)
    where TEntity : class, ISyncEntity
  {
    // FindAsync rather than a query: entities added earlier in this same batch are still only in
    // the change tracker, and a second operation on one of them must see it.
    var existing = await set.FindAsync([operation.EntityId], cancellationToken);

    if (operation.Kind == SyncOperationKind.Delete)
    {
      if (existing is null || existing.IsDeleted)
      {
        return null;
      }

      var deleteConflict = DetectConflict(operation, existing);
      if (deleteConflict?.Outcome == ConflictOutcome.ServerWon)
      {
        return deleteConflict;
      }

      existing.IsDeleted = true;
      return deleteConflict;
    }

    if (string.IsNullOrWhiteSpace(operation.PayloadJson))
    {
      return Reject(operation, "Upsert carried no payload.");
    }

    TEntity? incoming;
    try
    {
      incoming = JsonSerializer.Deserialize<TEntity>(operation.PayloadJson, SerializerOptions);
    }
    catch (JsonException ex)
    {
      _logger.LogWarning(ex, "Discarding malformed sync payload for {EntityType} {EntityId}.",
        operation.EntityType, operation.EntityId);
      return Reject(operation, "Payload could not be deserialised.");
    }

    if (incoming is null)
    {
      return Reject(operation, "Payload could not be deserialised.");
    }

    if (validate is not null)
    {
      var failure = await validate(incoming, cancellationToken);
      if (failure is not null)
      {
        return Reject(operation, failure);
      }
    }

    if (existing is null)
    {
      incoming.IsDeleted = false;
      set.Add(incoming);
      return null;
    }

    var conflict = DetectConflict(operation, existing);
    if (conflict?.Outcome == ConflictOutcome.ServerWon)
    {
      return conflict;
    }

    copyInto(existing, incoming);
    existing.IsDeleted = false;
    return conflict;
  }

  /// <summary>
  /// Compares the stamp the device edited against what the row carries now. Equal stamps mean a
  /// clean edit; anything else means the row moved on and last-write-wins decides.
  /// </summary>
  private static SyncConflict? DetectConflict(SyncOperation operation, ISyncEntity existing)
  {
    if (existing.SyncStamp == operation.BaseSyncStamp)
    {
      return null;
    }

    var clientWins = operation.ClientTimestampUtc > existing.UpdatedAtUtc;

    return new SyncConflict(
      operation.OperationId,
      operation.EntityType,
      operation.EntityId,
      clientWins ? ConflictOutcome.ClientWon : ConflictOutcome.ServerWon,
      existing.SyncStamp,
      clientWins
        ? "The device's edit was newer and replaced a server change it had not seen."
        : "The server held a newer edit, so the device's change was discarded.");
  }

  private static SyncConflict Reject(SyncOperation operation, string reason) => new(
    operation.OperationId,
    operation.EntityType,
    operation.EntityId,
    ConflictOutcome.Rejected,
    ServerSyncStamp: 0,
    reason);

  private async Task<SyncPayload> PullAsync(long cursor, CancellationToken cancellationToken) => new(
    await _db.Households.AsNoTracking().Where(e => e.SyncStamp > cursor).ToListAsync(cancellationToken),
    await _db.Members.AsNoTracking().Where(e => e.SyncStamp > cursor).ToListAsync(cancellationToken),
    await _db.Stores.AsNoTracking().Where(e => e.SyncStamp > cursor).ToListAsync(cancellationToken),
    await _db.Categories.AsNoTracking().Where(e => e.SyncStamp > cursor).ToListAsync(cancellationToken),
    await _db.Trips.AsNoTracking().Where(e => e.SyncStamp > cursor).ToListAsync(cancellationToken),
    await _db.Items.AsNoTracking().Where(e => e.SyncStamp > cursor).ToListAsync(cancellationToken),
    await _db.Settlements.AsNoTracking().Where(e => e.SyncStamp > cursor).ToListAsync(cancellationToken));

  /// <summary>
  /// The high-water mark is taken from rows this transaction can actually see rather than from the
  /// counter, which may already have been claimed by a write that has not committed yet.
  /// </summary>
  private static long HighestStamp(SyncPayload payload)
  {
    var stamps = payload.Households.Cast<ISyncEntity>()
      .Concat(payload.Members)
      .Concat(payload.Stores)
      .Concat(payload.Categories)
      .Concat(payload.Trips)
      .Concat(payload.Items)
      .Concat(payload.Settlements)
      .Select(e => e.SyncStamp);

    return stamps.DefaultIfEmpty(0L).Max();
  }

  private async Task<string?> ValidateMemberAsync(Member member, CancellationToken cancellationToken) =>
    await HouseholdMissingAsync(member.HouseholdId, cancellationToken);

  private async Task<string?> ValidateStoreAsync(Store store, CancellationToken cancellationToken) =>
    await HouseholdMissingAsync(store.HouseholdId, cancellationToken);

  private async Task<string?> ValidateCategoryAsync(Category category, CancellationToken cancellationToken) =>
    await HouseholdMissingAsync(category.HouseholdId, cancellationToken);

  private async Task<string?> ValidateTripAsync(ShoppingTrip trip, CancellationToken cancellationToken)
  {
    var householdFailure = await HouseholdMissingAsync(trip.HouseholdId, cancellationToken);
    if (householdFailure is not null)
    {
      return householdFailure;
    }

    if (!await ExistsAsync(_db.Stores, trip.StoreId, cancellationToken))
    {
      return $"Store {trip.StoreId} does not exist.";
    }

    if (trip.PaidByMemberId is { } memberId
      && !await ExistsAsync(_db.Members, memberId, cancellationToken))
    {
      return $"Member {memberId} does not exist.";
    }

    return null;
  }

  private async Task<string?> ValidateItemAsync(ExpenseItem item, CancellationToken cancellationToken)
  {
    if (!await ExistsAsync(_db.Trips, item.TripId, cancellationToken))
    {
      return $"Trip {item.TripId} does not exist.";
    }

    if (item.CategoryId is { } categoryId
      && !await ExistsAsync(_db.Categories, categoryId, cancellationToken))
    {
      return $"Category {categoryId} does not exist.";
    }

    return null;
  }

  private async Task<string?> ValidateSettlementAsync(Settlement settlement, CancellationToken cancellationToken)
  {
    var householdFailure = await HouseholdMissingAsync(settlement.HouseholdId, cancellationToken);
    if (householdFailure is not null)
    {
      return householdFailure;
    }

    if (settlement.FromMemberId == settlement.ToMemberId)
    {
      return "A transfer needs two different people.";
    }

    if (!await ExistsAsync(_db.Members, settlement.FromMemberId, cancellationToken))
    {
      return $"Member {settlement.FromMemberId} does not exist.";
    }

    if (!await ExistsAsync(_db.Members, settlement.ToMemberId, cancellationToken))
    {
      return $"Member {settlement.ToMemberId} does not exist.";
    }

    return null;
  }

  private async Task<string?> HouseholdMissingAsync(Guid householdId, CancellationToken cancellationToken) =>
    await ExistsAsync(_db.Households, householdId, cancellationToken)
      ? null
      : $"Household {householdId} does not exist.";

  /// <summary>
  /// Existence check that also sees rows created earlier in the same batch, so a trip and its items
  /// can be sent together on their very first sync.
  /// </summary>
  private static async Task<bool> ExistsAsync<TEntity>(
    DbSet<TEntity> set,
    Guid id,
    CancellationToken cancellationToken)
    where TEntity : class =>
    await set.FindAsync([id], cancellationToken) is not null;

  private static void CopyHousehold(Household target, Household source)
  {
    target.Name = source.Name;
    target.CurrencyCode = source.CurrencyCode;
  }

  private static void CopyMember(Member target, Member source)
  {
    target.DisplayName = source.DisplayName;
    target.HouseholdId = source.HouseholdId;
  }

  private static void CopyStore(Store target, Store source)
  {
    target.Name = source.Name;
    target.HouseholdId = source.HouseholdId;
  }

  private static void CopyCategory(Category target, Category source)
  {
    target.Name = source.Name;
    target.ColorHex = source.ColorHex;
    target.HouseholdId = source.HouseholdId;
  }

  private static void CopyTrip(ShoppingTrip target, ShoppingTrip source)
  {
    target.PurchasedOn = source.PurchasedOn;
    target.TotalAmount = source.TotalAmount;
    target.Note = source.Note;
    target.StoreId = source.StoreId;
    target.PaidByMemberId = source.PaidByMemberId;
    target.HouseholdId = source.HouseholdId;
  }

  private static void CopySettlement(Settlement target, Settlement source)
  {
    target.FromMemberId = source.FromMemberId;
    target.ToMemberId = source.ToMemberId;
    target.Amount = source.Amount;
    target.Date = source.Date;
    target.Note = source.Note;
    target.HouseholdId = source.HouseholdId;
  }

  private static void CopyItem(ExpenseItem target, ExpenseItem source)
  {
    target.Description = source.Description;
    target.Amount = source.Amount;
    target.Quantity = source.Quantity;
    target.Unit = source.Unit;
    target.TripId = source.TripId;
    target.CategoryId = source.CategoryId;
    target.HouseholdId = source.HouseholdId;
  }
}
