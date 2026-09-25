using CINE.GroceryTracker.Contracts.Sync;
using CINE.GroceryTracker.Domain.Model;
using Microsoft.JSInterop;

namespace CINE.GroceryTracker.UI.Blazor.Services;

/// <summary>
/// Typed access to the browser's IndexedDB cache and outbox.
/// </summary>
/// <remarks>
/// Every read the UI performs goes through here rather than over HTTP, which is what lets the app
/// work unchanged with no connection. The API only ever feeds this cache via the sync engine.
/// </remarks>
public sealed class LocalStore : IAsyncDisposable
{
  private const string CursorKey = "syncCursor";
  private const string LastSyncKey = "lastSyncUtc";

  private readonly IJSRuntime _js;
  private readonly SemaphoreSlim _moduleLock = new(1, 1);
  private IJSObjectReference? _module;

  public LocalStore(IJSRuntime js)
  {
    _js = js ?? throw new ArgumentNullException(nameof(js));
  }

  private async ValueTask<IJSObjectReference> ModuleAsync()
  {
    if (_module is not null)
    {
      return _module;
    }

    await _moduleLock.WaitAsync();
    try
    {
      _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/grocerydb.js");
      return _module;
    }
    finally
    {
      _moduleLock.Release();
    }
  }

  public async Task<List<Household>> GetHouseholdsAsync() => await GetAllAsync<Household>("households");

  public async Task<List<Member>> GetMembersAsync() => await GetAllAsync<Member>("members");

  public async Task<List<Store>> GetStoresAsync() => await GetAllAsync<Store>("stores");

  public async Task<List<Category>> GetCategoriesAsync() => await GetAllAsync<Category>("categories");

  public async Task<List<ShoppingTrip>> GetTripsAsync() => await GetAllAsync<ShoppingTrip>("trips");

  public async Task<List<ExpenseItem>> GetItemsAsync() => await GetAllAsync<ExpenseItem>("items");

  public async Task<List<Settlement>> GetSettlementsAsync() => await GetAllAsync<Settlement>("settlements");

  public async Task<List<TEntity>> GetAllAsync<TEntity>(string storeName)
  {
    var module = await ModuleAsync();
    var values = await module.InvokeAsync<List<TEntity>?>("getAll", storeName);
    return values ?? [];
  }

  public async Task PutAsync<TEntity>(string storeName, TEntity entity)
  {
    var module = await ModuleAsync();
    await module.InvokeVoidAsync("put", storeName, entity);
  }

  /// <summary>Writes an entire server payload into the cache in one transaction.</summary>
  public async Task ApplyPayloadAsync(SyncPayload payload)
  {
    var module = await ModuleAsync();
    await module.InvokeVoidAsync("applyPayload", new
    {
      households = payload.Households,
      members = payload.Members,
      stores = payload.Stores,
      categories = payload.Categories,
      trips = payload.Trips,
      items = payload.Items,
      settlements = payload.Settlements,
    });
  }

  public async Task EnqueueAsync(SyncOperation operation)
  {
    var module = await ModuleAsync();
    await module.InvokeVoidAsync("enqueue", operation);
  }

  public async Task<List<SyncOperation>> GetOutboxAsync()
  {
    var module = await ModuleAsync();
    var operations = await module.InvokeAsync<List<SyncOperation>?>("getOutbox");
    return operations ?? [];
  }

  public async Task RemoveOperationsAsync(IReadOnlyList<Guid> operationIds)
  {
    if (operationIds.Count == 0)
    {
      return;
    }

    var module = await ModuleAsync();
    await module.InvokeVoidAsync("removeOperations", operationIds);
  }

  /// <summary>A per-device setting that is deliberately not synced, such as which person this phone belongs to.</summary>
  public async Task<string?> GetSettingAsync(string key)
  {
    var module = await ModuleAsync();
    return await module.InvokeAsync<string?>("getMeta", $"setting:{key}");
  }

  public async Task SetSettingAsync(string key, string? value)
  {
    var module = await ModuleAsync();
    await module.InvokeVoidAsync("setMeta", $"setting:{key}", value);
  }

  public async Task<long> GetCursorAsync()
  {
    var module = await ModuleAsync();
    return await module.InvokeAsync<long?>("getMeta", CursorKey) ?? 0L;
  }

  public async Task SetCursorAsync(long cursor)
  {
    var module = await ModuleAsync();
    await module.InvokeVoidAsync("setMeta", CursorKey, cursor);
  }

  public async Task<DateTimeOffset?> GetLastSyncAsync()
  {
    var module = await ModuleAsync();
    var value = await module.InvokeAsync<string?>("getMeta", LastSyncKey);
    return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
  }

  public async Task SetLastSyncAsync(DateTimeOffset timestamp)
  {
    var module = await ModuleAsync();
    await module.InvokeVoidAsync("setMeta", LastSyncKey, timestamp.ToString("O"));
  }

  public async Task ClearAsync()
  {
    var module = await ModuleAsync();
    await module.InvokeVoidAsync("clearAll");
  }

  public async ValueTask DisposeAsync()
  {
    if (_module is not null)
    {
      try
      {
        await _module.DisposeAsync();
      }
      catch (JSDisconnectedException)
      {
        // The page is going away; the module went with it.
      }
    }

    _moduleLock.Dispose();
  }
}
