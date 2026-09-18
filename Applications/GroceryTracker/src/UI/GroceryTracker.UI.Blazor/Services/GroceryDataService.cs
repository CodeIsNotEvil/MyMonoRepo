using System.Net.Http.Json;
using System.Text.Json;
using GroceryTracker.Contracts.Sync;
using GroceryTracker.Domain.Analytics;
using GroceryTracker.Domain.Model;
using GroceryTracker.Domain.Sync;

namespace GroceryTracker.UI.Blazor.Services;

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
