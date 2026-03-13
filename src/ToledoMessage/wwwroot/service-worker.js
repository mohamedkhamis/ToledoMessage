// ============================================================
// Toledo Message — Service Worker
// Strategy: Cache-first for static assets, network-first for API/hubs
// ============================================================

// VERSION: Bump this string on every deploy to trigger cache refresh.
// When this value changes, the browser detects a "new" service worker
// and triggers the install + activate lifecycle events.
const CACHE_VERSION = 'toledo-v1';

// STATIC_ASSETS: List of URLs to pre-cache during the install event.
// Blazor WASM framework files (.dll, .wasm, blazor.boot.json) are cached
// dynamically on first load via the fetch handler's cache-on-response logic.
const STATIC_ASSETS = [
  '/',
  '/offline.html',
  '/manifest.json',
  '/icon-192.png',
  '/icon-512.png'
];

// ---- INSTALL EVENT ----
// Fired when the browser detects a new or updated service worker.
// We open a versioned cache and add all static assets to it.
// skipWaiting() tells the browser to activate this SW immediately
// instead of waiting for all tabs to close.
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_VERSION)
      .then((cache) => cache.addAll(STATIC_ASSETS))
      .then(() => self.skipWaiting())
  );
});

// ---- ACTIVATE EVENT ----
// Fired when the new service worker takes control.
// We delete all old caches (any cache whose name is not CACHE_VERSION).
// clients.claim() makes this SW control all open tabs immediately.
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(
        keys
          .filter((key) => key !== CACHE_VERSION)
          .map((key) => caches.delete(key))
      ))
      .then(() => self.clients.claim())
  );
});

// ---- FETCH EVENT ----
// Intercepts every network request made by the app.
// Strategy:
//   - API requests (/api/*) and SignalR (/hubs/*): NETWORK-FIRST
//     (always try the network; these are real-time and must not be cached)
//   - All other requests (static assets): CACHE-FIRST
//     (serve from cache if available; fall back to network; cache the response)
//   - If both cache and network fail: show offline.html fallback
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);

  // --- Skip non-HTTP(S) requests (e.g. chrome-extension://) ---
  if (!url.protocol.startsWith('http')) {
    return;
  }

  // --- Network-first for API and SignalR ---
  // These paths must NEVER be served from cache because they carry
  // real-time data (messages, presence, auth tokens).
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
    event.respondWith(
      fetch(event.request).catch(() => {
        // If network fails for an API request, return a 503 response
        return new Response(JSON.stringify({ error: 'Offline' }), {
          status: 503,
          headers: { 'Content-Type': 'application/json' }
        });
      })
    );
    return;
  }

  // --- Cache-first for static assets ---
  event.respondWith(
    caches.match(event.request)
      .then((cachedResponse) => {
        // If found in cache, return it immediately (fast!)
        if (cachedResponse) {
          return cachedResponse;
        }

        // Not in cache — try the network
        return fetch(event.request)
          .then((networkResponse) => {
            // Cache the new response for future offline use
            // Only cache successful (200 OK) same-origin responses
            if (networkResponse && networkResponse.status === 200 && networkResponse.type === 'basic') {
              const responseToCache = networkResponse.clone();
              caches.open(CACHE_VERSION)
                .then((cache) => cache.put(event.request, responseToCache));
            }
            return networkResponse;
          })
          .catch(() => {
            // Both cache and network failed
            // If the request is for a page (navigation), show the offline fallback
            if (event.request.mode === 'navigate') {
              return caches.match('/offline.html');
            }
            // For non-navigation requests (images, scripts), just fail silently
            return new Response('', { status: 408, statusText: 'Offline' });
          });
      })
  );
});
