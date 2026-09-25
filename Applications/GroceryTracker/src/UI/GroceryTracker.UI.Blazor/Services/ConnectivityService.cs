using Microsoft.JSInterop;

namespace CINE.GroceryTracker.UI.Blazor.Services;

/// <summary>
/// Tracks whether the browser thinks it has a network, and raises an event when that flips.
/// </summary>
/// <remarks>
/// This is a hint, not a guarantee: a phone can be on wifi that cannot reach the Pi. The sync
/// engine therefore treats any failed request as offline as well, and uses this only to retry
/// promptly when connectivity comes back.
/// </remarks>
public sealed class ConnectivityService : IAsyncDisposable
{
  private readonly IJSRuntime _js;
  private DotNetObjectReference<ConnectivityService>? _selfReference;
  private IJSObjectReference? _module;

  public ConnectivityService(IJSRuntime js)
  {
    _js = js ?? throw new ArgumentNullException(nameof(js));
  }

  public bool IsOnline { get; private set; } = true;

  public event Func<bool, Task>? ConnectivityChanged;

  public async Task InitialiseAsync()
  {
    if (_module is not null)
    {
      return;
    }

    _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/connectivity.js");
    _selfReference = DotNetObjectReference.Create(this);
    IsOnline = await _module.InvokeAsync<bool>("register", _selfReference);
  }

  [JSInvokable]
  public async Task OnConnectivityChanged(bool isOnline)
  {
    IsOnline = isOnline;

    if (ConnectivityChanged is not null)
    {
      await ConnectivityChanged.Invoke(isOnline);
    }
  }

  public async ValueTask DisposeAsync()
  {
    if (_module is not null)
    {
      try
      {
        await _module.InvokeVoidAsync("unregister");
        await _module.DisposeAsync();
      }
      catch (JSDisconnectedException)
      {
        // The page is going away; nothing to unhook.
      }
    }

    _selfReference?.Dispose();
  }
}
