// Offline shell for the published app. See https://aka.ms/blazor-offline-considerations
//
// This caches the application itself, not its data. Grocery data lives in IndexedDB and is
// reconciled by the sync engine, so API traffic must always go to the network and never be served
// from a stale cache entry.

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

const base = '/';
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
  console.info('Service worker: Install');

  const assetsRequests = self.assetsManifest.assets
    .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
    .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
    .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));

  await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));

  // Take over as soon as the new version is cached; the app carries no cross-version state in the
  // page itself, so there is nothing to migrate.
  await self.skipWaiting();
}

async function onActivate(event) {
  console.info('Service worker: Activate');

  const cacheKeys = await caches.keys();
  await Promise.all(cacheKeys
    .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
    .map(key => caches.delete(key)));

  await self.clients.claim();
}

async function onFetch(event) {
  const url = new URL(event.request.url);

  // Never cache the API. A stale sync or analytics response would be worse than an honest failure,
  // which the client already handles by falling back to its local cache.
  if (url.pathname.startsWith('/api/') || url.pathname === '/health') {
    return fetch(event.request);
  }

  if (event.request.method === 'GET') {
    // Serve the SPA shell for navigations so a deep link works while offline.
    const shouldServeIndexHtml = event.request.mode === 'navigate'
      && !manifestUrlList.some(candidate => candidate === event.request.url);

    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);

    if (cachedResponse) {
      return cachedResponse;
    }
  }

  return fetch(event.request);
}
