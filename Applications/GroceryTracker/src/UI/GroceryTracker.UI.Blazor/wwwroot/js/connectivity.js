// Reports browser connectivity changes to the sync engine.
//
// navigator.onLine only proves a network interface exists, not that the Pi is reachable, so it is
// used purely as a hint to try syncing sooner. The sync engine still treats a failed request as
// "offline" regardless of what this says.

let handler = null;

export function isOnline() {
  return navigator.onLine;
}

export function register(dotNetRef) {
  unregister();

  handler = {
    online: () => dotNetRef.invokeMethodAsync('OnConnectivityChanged', true),
    offline: () => dotNetRef.invokeMethodAsync('OnConnectivityChanged', false),
  };

  window.addEventListener('online', handler.online);
  window.addEventListener('offline', handler.offline);

  return navigator.onLine;
}

export function unregister() {
  if (!handler) {
    return;
  }

  window.removeEventListener('online', handler.online);
  window.removeEventListener('offline', handler.offline);
  handler = null;
}
