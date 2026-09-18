using System.Net.Http.Json;
using GroceryTracker.Contracts.Sync;
using Microsoft.Extensions.Logging;

namespace GroceryTracker.UI.Blazor.Services;

public enum SyncStatus
{
  Idle,
  Syncing,
  Offline,
  Failed,

  /// <summary>The server's database was wiped since this device last synced; see Settings.</summary>
  ServerReset,
}

public sealed record SyncOutcome(SyncStatus Status, int Pulled, IReadOnlyList<SyncConflict> Conflicts)
{
  public static readonly SyncOutcome Offline = new(SyncStatus.Offline, 0, []);
}

/// <summary>
/// Drives the client half of the sync protocol.
/// </summary>
/// <remarks>
/// Runs on app start, whenever the browser regains connectivity, after every local edit, and on a
/// slow timer as a backstop for the cases the browser never reports. Calls are serialised so a
/// burst of edits cannot start overlapping rounds that replay the same outbox twice.
/// </remarks>
public sealed class SyncEngine : IAsyncDisposable
{
  private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

  private readonly HttpClient _http;
  private readonly LocalStore _store;
  private readonly ConnectivityService _connectivity;
  private readonly ILogger<SyncEngine> _logger;
  private readonly SemaphoreSlim _gate = new(1, 1);
  private readonly CancellationTokenSource _shutdown = new();

  private Task? _pollingLoop;

  public SyncEngine(
    HttpClient http,
    LocalStore store,
    ConnectivityService connectivity,
    ILogger<SyncEngine> logger)
  {
    _http = http;
    _store = store;
    _connectivity = connectivity;
    _logger = logger;
  }

  public SyncStatus Status { get; private set; } = SyncStatus.Idle;

  public int PendingCount { get; private set; }

  public DateTimeOffset? LastSyncUtc { get; private set; }

  public IReadOnlyList<SyncConflict> LastConflicts { get; private set; } = [];

  /// <summary>Raised whenever the cache or the sync status changed, so pages can re-render.</summary>
  public event Action? Changed;

  public async Task InitialiseAsync()
  {
    await _connectivity.InitialiseAsync();
    _connectivity.ConnectivityChanged += OnConnectivityChangedAsync;

    LastSyncUtc = await _store.GetLastSyncAsync();
    await RefreshPendingCountAsync();

    await SyncAsync();

    _pollingLoop ??= Task.Run(PollAsync);
  }

  private async Task OnConnectivityChangedAsync(bool isOnline)
  {
    if (isOnline)
    {
      await SyncAsync();
    }
    else
    {
      Status = SyncStatus.Offline;
      Changed?.Invoke();
    }
  }

  private async Task PollAsync()
  {
    using var timer = new PeriodicTimer(PollInterval);

    try
    {
      while (await timer.WaitForNextTickAsync(_shutdown.Token))
      {
        await SyncAsync();
      }
    }
    catch (OperationCanceledException)
    {
      // Shutting down.
    }
  }

  /// <summary>
  /// Pushes the outbox and pulls everything new. Safe to call as often as you like; overlapping
  /// calls collapse into the one already running.
  /// </summary>
  public async Task<SyncOutcome> SyncAsync()
  {
    if (!await _gate.WaitAsync(0))
    {
      return new SyncOutcome(Status, 0, LastConflicts);
    }

    try
    {
      if (!_connectivity.IsOnline)
      {
        Status = SyncStatus.Offline;
        Changed?.Invoke();
        return SyncOutcome.Offline;
      }

      // Checked before anything is sent: pushing this device's queued changes at a database that no
      // longer holds its household would only get them rejected and dropped from the outbox.
      if (await HoldsSeveralHouseholdsAsync())
      {
        Status = SyncStatus.ServerReset;
        return new SyncOutcome(SyncStatus.ServerReset, 0, []);
      }

      Status = SyncStatus.Syncing;
      Changed?.Invoke();

      var cursor = await _store.GetCursorAsync();
      var operations = await _store.GetOutboxAsync();
      var request = new SyncRequest(cursor, operations);

      var httpResponse = await _http.PostAsJsonAsync("api/sync", request, GroceryJson.Options, _shutdown.Token);
      httpResponse.EnsureSuccessStatusCode();

      var response = await httpResponse.Content.ReadFromJsonAsync<SyncResponse>(
        GroceryJson.Options, _shutdown.Token);

      if (response is null)
      {
        throw new InvalidOperationException("The BFF returned an empty sync response.");
      }

      if (response.ServerReset)
      {
        // Keep everything as it is: the outbox and the cache are the only copy of this device's data
        // left. The user decides whether to discard them (Settings) once they understand why.
        _logger.LogWarning("The server reports that its database was reset since this device last synced.");
        Status = SyncStatus.ServerReset;
        return new SyncOutcome(SyncStatus.ServerReset, 0, []);
      }

      await _store.ApplyPayloadAsync(response.Payload);
      await _store.RemoveOperationsAsync(response.AppliedOperationIds);
      await _store.SetCursorAsync(response.Cursor);
      await _store.SetLastSyncAsync(response.ServerTimeUtc);

      LastSyncUtc = response.ServerTimeUtc;
      LastConflicts = response.Conflicts;
      await RefreshPendingCountAsync();

      // The other way a wiped server shows itself: it seeded a fresh household, which has just arrived
      // next to the one this device already had. There is meant to be exactly one.
      if (await HoldsSeveralHouseholdsAsync())
      {
        _logger.LogWarning("This device now holds two households; the server's database was replaced.");
        Status = SyncStatus.ServerReset;
        return new SyncOutcome(SyncStatus.ServerReset, response.Payload.Count, response.Conflicts);
      }

      Status = SyncStatus.Idle;

      if (response.Conflicts.Count > 0)
      {
        _logger.LogWarning("Sync finished with {Count} conflict(s).", response.Conflicts.Count);
      }

      return new SyncOutcome(SyncStatus.Idle, response.Payload.Count, response.Conflicts);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
      // The interface claimed to be up but the Pi was not reachable. Nothing is lost: the outbox
      // still holds every change and the next attempt will replay it.
      _logger.LogInformation(ex, "Sync could not reach the server; staying offline.");
      Status = SyncStatus.Offline;
      return SyncOutcome.Offline;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Sync failed.");
      Status = SyncStatus.Failed;
      return new SyncOutcome(SyncStatus.Failed, 0, []);
    }
    finally
    {
      _gate.Release();
      Changed?.Invoke();
    }
  }

  private async Task<bool> HoldsSeveralHouseholdsAsync() =>
    (await _store.GetHouseholdsAsync()).Count(h => !h.IsDeleted) > 1;

  public async Task RefreshPendingCountAsync()
  {
    PendingCount = (await _store.GetOutboxAsync()).Count;
  }

  /// <summary>Called by the data layer after a local write so the badge and status stay honest.</summary>
  public void NotifyLocalChange()
  {
    PendingCount++;
    Changed?.Invoke();
  }

  public async ValueTask DisposeAsync()
  {
    _connectivity.ConnectivityChanged -= OnConnectivityChangedAsync;
    await _shutdown.CancelAsync();

    if (_pollingLoop is not null)
    {
      try
      {
        await _pollingLoop;
      }
      catch (OperationCanceledException)
      {
        // Expected on shutdown.
      }
    }

    _shutdown.Dispose();
    _gate.Dispose();
  }
}
