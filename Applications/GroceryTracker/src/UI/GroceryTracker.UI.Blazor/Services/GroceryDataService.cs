using System.Net.Http.Json;
using System.Text.Json;
using GroceryTracker.Contracts.Sync;
using GroceryTracker.Domain.Analytics;
using GroceryTracker.Domain.Balance;
using GroceryTracker.Domain.Import;
using GroceryTracker.Domain.Model;
using GroceryTracker.Domain.Sync;

namespace GroceryTracker.UI.Blazor.Services;

/// <summary>Which person a spreadsheet column belongs to.</summary>
/// <param name="MemberId">An existing person, or null.</param>
/// <param name="NewMemberName">Set instead of <paramref name="MemberId"/> to create the person.</param>
public sealed record ColumnAssignment(int Column, Guid? MemberId, string? NewMemberName);

/// <summary>A trip that already exists and only needs a payer assigned.</summary>
public sealed record PayerUpdate(Guid TripId, Guid MemberId);

/// <summary>What an import would do, worked out before anything is written.</summary>
/// <param name="NewTrips">Bookings not yet on this device, ready to be saved.</param>
/// <param name="PayerUpdates">
/// Bookings that are already here but were imported without a payer (or with a different one), so
/// re-importing with people assigned fixes them instead of duplicating them.
/// </param>
/// <param name="NewMembers">People to create, or bring back if they were removed.</param>
/// <param name="AlreadyImported">
/// Bookings that are here and need no change. This includes trips the user deleted afterwards, so a
/// re-import does not resurrect them.
/// </param>
public sealed record ImportPlan(
  Household Household,
  Guid StoreId,
  IReadOnlyList<ShoppingTrip> NewTrips,
  IReadOnlyList<PayerUpdate> PayerUpdates,
  IReadOnlyList<Member> NewMembers,
  int AlreadyImported)
{
  public bool HasChanges => NewTrips.Count > 0 || PayerUpdates.Count > 0 || NewMembers.Count > 0;
}

public sealed record ImportResult(int NewTrips, int UpdatedTrips, int NewMembers);

/// <summary>
/// The only data API the pages use.
/// </summary>
/// <remarks>
/// Reads come from the IndexedDB cache and writes go to the cache plus the outbox, so nothing here
/// ever blocks on the network. Sync is kicked off after a write but never awaited: the save is
/// already durable on the device by then.
/// </remarks>
public sealed class GroceryDataService
{
  private const string Households = "households";
  private const string Members = "members";
  private const string Stores = "stores";
  private const string Categories = "categories";
  private const string Trips = "trips";
  private const string Items = "items";
  private const string Settlements = "settlements";

  private readonly LocalStore _store;
  private readonly SyncEngine _sync;
  private readonly ConnectivityService _connectivity;
  private readonly HttpClient _http;

  public GroceryDataService(
    LocalStore store,
    SyncEngine sync,
    ConnectivityService connectivity,
    HttpClient http)
  {
    _store = store;
    _sync = sync;
    _connectivity = connectivity;
    _http = http;
  }

  public async Task<Household?> GetHouseholdAsync() =>
    (await _store.GetHouseholdsAsync())
      .Where(h => !h.IsDeleted)
      .OrderBy(h => h.SyncStamp)
      .FirstOrDefault();

  public async Task<List<Store>> GetStoresAsync() =>
    (await _store.GetStoresAsync())
      .Where(s => !s.IsDeleted)
      .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
      .ToList();

  public async Task<List<Category>> GetCategoriesAsync() =>
    (await _store.GetCategoriesAsync())
      .Where(c => !c.IsDeleted)
      .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
      .ToList();

  public async Task<List<Member>> GetMembersAsync() =>
    (await _store.GetMembersAsync())
      .Where(m => !m.IsDeleted)
      .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
      .ToList();

  /// <summary>Trips newest first, with their line items reattached from the items store.</summary>
  public async Task<List<ShoppingTrip>> GetTripsAsync()
  {
    var trips = (await _store.GetTripsAsync()).Where(t => !t.IsDeleted).ToList();
    var items = (await _store.GetItemsAsync()).Where(i => !i.IsDeleted).ToLookup(i => i.TripId);

    foreach (var trip in trips)
    {
      trip.Items.Clear();
      foreach (var item in items[trip.Id])
      {
        trip.Items.Add(item);
      }
    }

    return trips
      .OrderByDescending(t => t.PurchasedOn)
      .ThenByDescending(t => t.UpdatedAtUtc)
      .ToList();
  }

  public async Task<ShoppingTrip?> GetTripAsync(Guid id) =>
    (await GetTripsAsync()).FirstOrDefault(t => t.Id == id);

  public async Task SaveStoreAsync(Store store)
  {
    await UpsertAsync(Stores, store, SyncEntityType.Store);
    TriggerSync();
  }

  public async Task DeleteStoreAsync(Store store)
  {
    await TombstoneAsync(Stores, store, SyncEntityType.Store);
    TriggerSync();
  }

  public async Task SaveCategoryAsync(Category category)
  {
    await UpsertAsync(Categories, category, SyncEntityType.Category);
    TriggerSync();
  }

  public async Task DeleteCategoryAsync(Category category)
  {
    await TombstoneAsync(Categories, category, SyncEntityType.Category);
    TriggerSync();
  }

  public async Task SaveMemberAsync(Member member)
  {
    await UpsertAsync(Members, member, SyncEntityType.Member);
    TriggerSync();
  }

  public async Task DeleteMemberAsync(Member member)
  {
    await TombstoneAsync(Members, member, SyncEntityType.Member);
    TriggerSync();
  }

  /// <summary>
  /// Saves a receipt and its lines together, tombstoning any line the user removed while editing.
  /// </summary>
  public async Task SaveTripAsync(ShoppingTrip trip, IReadOnlyList<ExpenseItem> items)
  {
    var existing = (await _store.GetItemsAsync())
      .Where(i => i.TripId == trip.Id && !i.IsDeleted)
      .ToList();

    await UpsertAsync(Trips, trip, SyncEntityType.ShoppingTrip);

    foreach (var item in items)
    {
      item.TripId = trip.Id;
      item.HouseholdId = trip.HouseholdId;
      await UpsertAsync(Items, item, SyncEntityType.ExpenseItem);
    }

    var keptIds = items.Select(i => i.Id).ToHashSet();
    foreach (var removed in existing.Where(i => !keptIds.Contains(i.Id)))
    {
      await TombstoneAsync(Items, removed, SyncEntityType.ExpenseItem);
    }

    TriggerSync();
  }

  public async Task DeleteTripAsync(ShoppingTrip trip)
  {
    var items = (await _store.GetItemsAsync())
      .Where(i => i.TripId == trip.Id && !i.IsDeleted)
      .ToList();

    foreach (var item in items)
    {
      await TombstoneAsync(Items, item, SyncEntityType.ExpenseItem);
    }

    await TombstoneAsync(Trips, trip, SyncEntityType.ShoppingTrip);
    TriggerSync();
  }

  /// <summary>
  /// Asks the BFF for the summary and falls back to computing it from the cache.
  /// </summary>
  /// <remarks>
  /// Both paths run the same <see cref="SpendAnalyzer"/>, so the dashboard does not change its
  /// numbers when the connection drops, only where they were calculated.
  /// </remarks>
  public async Task<SpendSummary> GetSummaryAsync(DateOnly from, DateOnly to, SpendGranularity granularity)
  {
    if (_connectivity.IsOnline)
    {
      try
      {
        var url = $"api/analytics/summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&granularity={granularity}";
        var summary = await _http.GetFromJsonAsync<SpendSummary>(url, GroceryJson.Options);

        if (summary is not null)
        {
          return summary;
        }
      }
      catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
      {
        // Unreachable server: fall through to the cache rather than showing an error.
      }
    }

    return await ComputeSummaryLocallyAsync(from, to, granularity);
  }

  private async Task<SpendSummary> ComputeSummaryLocallyAsync(
    DateOnly from,
    DateOnly to,
    SpendGranularity granularity)
  {
    var household = await GetHouseholdAsync();
    var trips = await GetTripsAsync();
    var stores = await _store.GetStoresAsync();
    var categories = await _store.GetCategoriesAsync();

    return SpendAnalyzer.Summarise(
      trips,
      stores,
      categories,
      from,
      to,
      granularity,
      household?.CurrencyCode ?? "EUR");
  }

  private const string ImportStoreName = "Imported";
  private const string ImportStoreKey = "csv-import-store";
  private const string CurrentMemberSetting = "currentMemberId";

  public async Task<List<Settlement>> GetSettlementsAsync() =>
    (await _store.GetSettlementsAsync())
      .Where(s => !s.IsDeleted)
      .OrderByDescending(s => s.Date)
      .ThenByDescending(s => s.UpdatedAtUtc)
      .ToList();

  public async Task SaveSettlementAsync(Settlement settlement)
  {
    await UpsertAsync(Settlements, settlement, SyncEntityType.Settlement);
    TriggerSync();
  }

  public async Task DeleteSettlementAsync(Settlement settlement)
  {
    await TombstoneAsync(Settlements, settlement, SyncEntityType.Settlement);
    TriggerSync();
  }

  /// <summary>Who owes whom, computed from this device's cache so it works with no connection.</summary>
  public async Task<BalanceSummary> GetBalanceAsync() => BalanceCalculator.Calculate(
    await _store.GetTripsAsync(),
    await _store.GetMembersAsync(),
    await _store.GetSettlementsAsync());

  /// <summary>Which person this device belongs to, so the balance can say "you owe" instead of a name.</summary>
  public async Task<Guid?> GetCurrentMemberIdAsync() =>
    Guid.TryParse(await _store.GetSettingAsync(CurrentMemberSetting), out var id) ? id : null;

  public async Task SetCurrentMemberIdAsync(Guid? memberId) =>
    await _store.SetSettingAsync(CurrentMemberSetting, memberId?.ToString());

  /// <summary>
  /// Works out what importing would change. Ids come from the booking's key, so importing the same
  /// file again only adds what is not here yet, and only touches existing trips to assign a payer.
  /// </summary>
  /// <returns>Null until this device has synced a household to attach the trips to.</returns>
  public async Task<ImportPlan?> PlanImportAsync(
    IReadOnlyList<ImportedBooking> bookings,
    IReadOnlyList<ColumnAssignment> assignments)
  {
    var household = await GetHouseholdAsync();
    if (household is null)
    {
      return null;
    }

    var storeId = DeterministicGuid.Create(household.Id, ImportStoreKey);
    var localMembers = await _store.GetMembersAsync();
    var newMembers = new Dictionary<Guid, Member>();

    var payerByColumn = new Dictionary<int, Guid>();
    foreach (var assignment in assignments)
    {
      var payer = ResolvePayer(assignment, household, localMembers, newMembers);
      if (payer is { } id)
      {
        payerByColumn[assignment.Column] = id;
      }
    }

    var localTrips = (await _store.GetTripsAsync()).ToDictionary(t => t.Id);

    var fresh = new List<ShoppingTrip>();
    var updates = new List<PayerUpdate>();
    var unchanged = 0;

    foreach (var booking in bookings)
    {
      var id = DeterministicGuid.Create(household.Id, booking.Key);
      Guid? payer = payerByColumn.TryGetValue(booking.Column, out var payerId) ? payerId : null;

      if (!localTrips.TryGetValue(id, out var existing))
      {
        fresh.Add(new ShoppingTrip
        {
          Id = id,
          PurchasedOn = booking.Date,
          TotalAmount = booking.Amount,
          StoreId = storeId,
          HouseholdId = household.Id,
          PaidByMemberId = payer,
        });
      }
      else if (!existing.IsDeleted && payer is { } assigned && existing.PaidByMemberId != assigned)
      {
        updates.Add(new PayerUpdate(id, assigned));
      }
      else
      {
        unchanged++;
      }
    }

    return new ImportPlan(household, storeId, fresh, updates, [.. newMembers.Values], unchanged);
  }

  /// <summary>
  /// Picks the member a column belongs to, reusing a person with that name instead of creating a
  /// duplicate. New people get an id derived from their name, so two devices importing the same
  /// sheet end up with the same person rather than two.
  /// </summary>
  private static Guid? ResolvePayer(
    ColumnAssignment assignment,
    Household household,
    List<Member> localMembers,
    Dictionary<Guid, Member> newMembers)
  {
    if (assignment.MemberId is { } chosen)
    {
      return chosen;
    }

    var name = assignment.NewMemberName?.Trim();
    if (string.IsNullOrEmpty(name))
    {
      return null;
    }

    var sameName = localMembers.FirstOrDefault(m =>
      !m.IsDeleted && string.Equals(m.DisplayName, name, StringComparison.CurrentCultureIgnoreCase));
    if (sameName is not null)
    {
      return sameName.Id;
    }

    var id = DeterministicGuid.Create(household.Id, $"csv-member|{name.ToLowerInvariant()}");

    if (!newMembers.ContainsKey(id))
    {
      // A previously removed person is brought back rather than duplicated.
      newMembers[id] = localMembers.FirstOrDefault(m => m.Id == id)
        ?? new Member { Id = id, DisplayName = name, HouseholdId = household.Id };
    }

    return id;
  }

  /// <summary>
  /// Saves the plan through the normal outbox, so an import made offline uploads on the next sync
  /// like any other edit. Bookings carry no store, so they share one "Imported" store.
  /// </summary>
  public async Task<ImportResult> ImportAsync(ImportPlan plan)
  {
    ArgumentNullException.ThrowIfNull(plan);

    if (!plan.HasChanges)
    {
      return new ImportResult(0, 0, 0);
    }

    foreach (var member in plan.NewMembers)
    {
      await UpsertAsync(Members, member, SyncEntityType.Member);
    }

    if (plan.NewTrips.Count > 0)
    {
      var store = (await _store.GetStoresAsync()).FirstOrDefault(s => s.Id == plan.StoreId);

      // Trips need a store to point at, so create it — or bring it back if it was removed.
      if (store is null || store.IsDeleted)
      {
        store ??= new Store { Id = plan.StoreId, Name = ImportStoreName, HouseholdId = plan.Household.Id };
        await UpsertAsync(Stores, store, SyncEntityType.Store);
      }

      foreach (var trip in plan.NewTrips)
      {
        await UpsertAsync(Trips, trip, SyncEntityType.ShoppingTrip);
      }
    }

    if (plan.PayerUpdates.Count > 0)
    {
      // Update the cached trips themselves, so each edit carries the stamp the server last gave us
      // and is a clean edit rather than looking like a conflicting one.
      var trips = (await _store.GetTripsAsync()).ToDictionary(t => t.Id);

      foreach (var update in plan.PayerUpdates)
      {
        if (trips.TryGetValue(update.TripId, out var trip))
        {
          trip.PaidByMemberId = update.MemberId;
          await UpsertAsync(Trips, trip, SyncEntityType.ShoppingTrip);
        }
      }
    }

    TriggerSync();
    return new ImportResult(plan.NewTrips.Count, plan.PayerUpdates.Count, plan.NewMembers.Count);
  }

  /// <summary>Drops the local cache so the next sync re-pulls everything from scratch.</summary>
  public async Task ResetLocalDataAsync()
  {
    await _store.ClearAsync();
    await _sync.RefreshPendingCountAsync();
    await _sync.SyncAsync();
  }

  private async Task UpsertAsync<TEntity>(string storeName, TEntity entity, SyncEntityType type)
    where TEntity : ISyncEntity
  {
    entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
    entity.IsDeleted = false;

    await _store.PutAsync(storeName, entity);

    // SyncStamp still holds the value the server last gave us, which is exactly the base the
    // conflict check needs. It stays untouched until a sync response updates it.
    await EnqueueAsync(type, entity.Id, SyncOperationKind.Upsert, entity.SyncStamp, entity.UpdatedAtUtc,
      JsonSerializer.Serialize(entity, entity.GetType(), GroceryJson.Options));
  }

  private async Task TombstoneAsync<TEntity>(string storeName, TEntity entity, SyncEntityType type)
    where TEntity : ISyncEntity
  {
    entity.IsDeleted = true;
    entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

    await _store.PutAsync(storeName, entity);
    await EnqueueAsync(type, entity.Id, SyncOperationKind.Delete, entity.SyncStamp, entity.UpdatedAtUtc, null);
  }

  private async Task EnqueueAsync(
    SyncEntityType type,
    Guid entityId,
    SyncOperationKind kind,
    long baseStamp,
    DateTimeOffset clientTime,
    string? payloadJson)
  {
    await _store.EnqueueAsync(new SyncOperation(
      Guid.NewGuid(),
      type,
      entityId,
      kind,
      baseStamp,
      clientTime,
      payloadJson));

    _sync.NotifyLocalChange();
  }

  private void TriggerSync() => _ = _sync.SyncAsync();
}
