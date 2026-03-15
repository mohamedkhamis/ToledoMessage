# Tasks: PWA Support

**Input**: Design documents from `/specs/009-pwa-support/`
**Prerequisites**: plan.md (loaded), spec.md (loaded), research.md (loaded), data-model.md (loaded), quickstart.md (loaded)

**Tests**: No automated tests requested. Validation is via manual Lighthouse PWA audit and cross-platform install testing.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. Each task includes explicit goals, inputs, outputs, dependencies, and expected results so that any AI system can execute them without additional context.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- All file paths are relative to the repository root: `D:\Repo\01-Khamis-Projects\ToledoVault\`

## Path Conventions

- **Server project**: `src/ToledoVault/` (ASP.NET Core host — serves static files from `wwwroot/`)
- **Client project**: `src/ToledoVault.Client/` (Blazor WASM — runs in browser)
- **Shared project**: `src/ToledoVault.Shared/` (DTOs, enums, constants)
- **Static files**: `src/ToledoVault/wwwroot/` (served at root URL `/`)
- **App entry point (HTML host)**: `src/ToledoVault/Components/App.razor` (the Razor component that renders the `<html>` tag, `<head>`, and `<body>`)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No new project structure is needed. PWA support adds only static files to the existing `wwwroot/` directory and modifies one existing Razor file. This phase creates placeholder icon files since real icon generation requires an image tool.

**Goal**: Ensure the `wwwroot/` directory is ready to receive all PWA artifacts.

**Expected state after Phase 1**: Two placeholder PNG icon files exist in `src/ToledoVault/wwwroot/`. No functionality changes yet.

- [x] T001 [P] Create app icon file `src/ToledoVault/wwwroot/icon-192.png` (192x192 PNG)

  **Goal**: Provide a 192x192 pixel PNG app icon for the PWA manifest. This icon appears on mobile home screens and in app drawers.

  **What to do**:
  1. Create a 192x192 pixel PNG image file at the path `src/ToledoVault/wwwroot/icon-192.png`.
  2. The icon should have a solid background using the app's accent color `#1976d2` (a medium blue).
  3. The icon should display the letters "TM" (for "Toledo Message") in white, centered, using a bold sans-serif font.
  4. If you cannot generate a real PNG image, create a minimal valid 192x192 PNG file (even a solid blue square is acceptable as a placeholder). The important thing is that the file exists, is a valid PNG, and is 192x192 pixels.

  **Output**: A valid PNG file at `src/ToledoVault/wwwroot/icon-192.png`, dimensions 192x192.

  **Dependencies**: None — can start immediately.

  **Expected result**: The file `icon-192.png` exists in `wwwroot/` and can be served at the URL `/icon-192.png`. Opening the file in an image viewer shows a 192x192 image.

- [x] T002 [P] Create app icon file `src/ToledoVault/wwwroot/icon-512.png` (512x512 PNG)

  **Goal**: Provide a 512x512 pixel PNG app icon for the PWA manifest. This icon is used for splash screens and high-resolution displays.

  **What to do**:
  1. Create a 512x512 pixel PNG image file at the path `src/ToledoVault/wwwroot/icon-512.png`.
  2. The icon should have a solid background using the app's accent color `#1976d2` (a medium blue).
  3. The icon should display the letters "TM" (for "Toledo Message") in white, centered, using a bold sans-serif font.
  4. If you cannot generate a real PNG image, create a minimal valid 512x512 PNG file (even a solid blue square is acceptable as a placeholder). The important thing is that the file exists, is a valid PNG, and is 512x512 pixels.

  **Output**: A valid PNG file at `src/ToledoVault/wwwroot/icon-512.png`, dimensions 512x512.

  **Dependencies**: None — can start immediately.

  **Expected result**: The file `icon-512.png` exists in `wwwroot/` and can be served at the URL `/icon-512.png`. Opening the file in an image viewer shows a 512x512 image.

**Checkpoint**: Two icon PNG files exist in `src/ToledoVault/wwwroot/`. No other changes yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create the three core PWA static files that ALL user stories depend on: the Web App Manifest, the Service Worker, and the Offline Fallback Page. These files must exist before any install, offline, or update functionality can work.

**⚠️ CRITICAL**: No user story work (Phase 3–6) can begin until ALL three tasks in this phase are complete, because:
- The manifest is required for install (US1, US2)
- The service worker is required for offline (US3) and updates (US4)
- The offline page is required for the service worker's fallback behavior (US3)

### T003: Create Web App Manifest

- [x] T003 Create Web App Manifest at `src/ToledoVault/wwwroot/manifest.json`

  **Goal**: Create a W3C Web App Manifest file that tells browsers this web app is installable. The manifest defines the app's name, icons, colors, display mode, and start URL.

  **What to do**:
  1. Create a new file at `src/ToledoVault/wwwroot/manifest.json`.
  2. The file must contain valid JSON with the following properties:

  ```json
  {
    "name": "Toledo Message",
    "short_name": "Toledo",
    "description": "Secure end-to-end encrypted messaging",
    "start_url": "/",
    "display": "standalone",
    "background_color": "#ffffff",
    "theme_color": "#1976d2",
    "orientation": "any",
    "icons": [
      {
        "src": "/icon-192.png",
        "sizes": "192x192",
        "type": "image/png",
        "purpose": "any maskable"
      },
      {
        "src": "/icon-512.png",
        "sizes": "512x512",
        "type": "image/png",
        "purpose": "any maskable"
      }
    ]
  }
  ```

  3. Important details about each property:
     - `"name"`: Full app name shown during install. Must be "Toledo Message".
     - `"short_name"`: Short name shown under the home screen icon. Must be "Toledo".
     - `"description"`: Brief description of the app. Must be "Secure end-to-end encrypted messaging".
     - `"start_url"`: The URL that opens when the user launches the installed app. Must be `"/"` (root).
     - `"display"`: Must be `"standalone"` — this hides the browser chrome (URL bar, tabs) so the app looks native.
     - `"background_color"`: Shown on the splash screen while the app loads. Must be `"#ffffff"` (white).
     - `"theme_color"`: Colors the browser UI (status bar on Android, title bar on desktop). Must be `"#1976d2"` (the app's accent blue).
     - `"orientation"`: Must be `"any"` — allows both portrait and landscape.
     - `"icons"`: Array with exactly two entries referencing the icon files created in T001 and T002. The `"purpose": "any maskable"` allows the OS to crop the icon for adaptive icon shapes (Android).

  **Output**: A valid JSON file at `src/ToledoVault/wwwroot/manifest.json` with all the properties listed above.

  **Dependencies**: T001 and T002 (icon files must exist for the manifest to reference them).

  **Expected result**: The file `manifest.json` is served at the URL `/manifest.json`. Opening it in a browser shows valid JSON with all required PWA manifest fields. Chrome DevTools > Application > Manifest should parse it successfully.

### T004: Create Service Worker

- [x] T004 Create service worker at `src/ToledoVault/wwwroot/service-worker.js`

  **Goal**: Create a service worker JavaScript file that:
  1. Caches the app shell (static files) on install
  2. Serves cached files when offline (cache-first strategy for static assets)
  3. Passes API and SignalR requests through to the network (network-first)
  4. Cleans up old caches when a new version is deployed
  5. Falls back to an offline HTML page when the network is unavailable and the requested page is not cached

  **What to do**:
  1. Create a new file at `src/ToledoVault/wwwroot/service-worker.js`.
  2. The file must contain the following JavaScript code (explained section by section):

  ```javascript
  // ============================================================
  // Toledo Message — Service Worker
  // Strategy: Cache-first for static assets, network-first for API/hubs
  // ============================================================

  // VERSION: Bump this string on every deploy to trigger cache refresh.
  // When this value changes, the browser detects a "new" service worker
  // and triggers the install + activate lifecycle events.
  const CACHE_VERSION = 'toledo-v1';

  // STATIC_ASSETS: List of URLs to pre-cache during the install event.
  // These are the files that make up the "app shell" — everything needed
  // to render the UI without network access.
  const STATIC_ASSETS = [
    '/',
    '/index.html',
    '/css/app.css',
    '/manifest.json',
    '/icon-192.png',
    '/icon-512.png',
    '/offline.html'
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
  ```

  3. **Critical rules for the service worker**:
     - The file MUST be located at the root of `wwwroot/` (not in a subdirectory) so its scope is `/` (the entire app).
     - The `CACHE_VERSION` string MUST be changed (bumped) on every deploy. This is what triggers cache invalidation.
     - API paths (`/api/*`) and SignalR paths (`/hubs/*`) MUST NEVER be cached. They carry real-time messaging data.
     - `self.skipWaiting()` ensures the new SW activates immediately (no waiting for tab closure).
     - `self.clients.claim()` ensures the new SW controls all open tabs immediately after activation.

  **Output**: A JavaScript file at `src/ToledoVault/wwwroot/service-worker.js` with install, activate, and fetch event handlers implementing the cache strategy described above.

  **Dependencies**: None (but the file references `/offline.html` which is created in T005, and icon files from T001/T002).

  **Expected result**: The file `service-worker.js` is served at the URL `/service-worker.js`. It contains valid JavaScript with no syntax errors. Chrome DevTools > Application > Service Workers shows it as registered (after T007 adds the registration script).

### T005: Create Offline Fallback Page

- [x] T005 [P] Create offline fallback page at `src/ToledoVault/wwwroot/offline.html`

  **Goal**: Create a simple, self-contained HTML page that the service worker shows when:
  1. The user is offline (no network)
  2. The requested page is not in the cache
  3. The service worker's fetch handler falls back to this page

  This page should look professional and match the app's branding. It tells the user they are offline and to check their connection.

  **What to do**:
  1. Create a new file at `src/ToledoVault/wwwroot/offline.html`.
  2. The file must be a complete, self-contained HTML document (all CSS must be inline — no external stylesheets, since we may be offline and external files may not be cached).
  3. The file must contain:

  ```html
  <!DOCTYPE html>
  <html lang="en">
  <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
      <title>Toledo Message — Offline</title>
      <style>
          * { margin: 0; padding: 0; box-sizing: border-box; }
          body {
              font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
              background-color: #f5f5f5;
              color: #333;
              display: flex;
              justify-content: center;
              align-items: center;
              min-height: 100vh;
              padding: 24px;
              text-align: center;
          }
          .offline-container {
              max-width: 400px;
          }
          .offline-icon {
              font-size: 64px;
              margin-bottom: 24px;
              opacity: 0.6;
          }
          h1 {
              font-size: 24px;
              font-weight: 600;
              margin-bottom: 12px;
              color: #1976d2;
          }
          p {
              font-size: 16px;
              line-height: 1.5;
              color: #666;
              margin-bottom: 24px;
          }
          .retry-btn {
              display: inline-block;
              padding: 12px 32px;
              background-color: #1976d2;
              color: #fff;
              border: none;
              border-radius: 8px;
              font-size: 16px;
              cursor: pointer;
              text-decoration: none;
          }
          .retry-btn:hover {
              background-color: #1565c0;
          }
      </style>
  </head>
  <body>
      <div class="offline-container">
          <div class="offline-icon">📡</div>
          <h1>You're Offline</h1>
          <p>Toledo Message needs an internet connection to send and receive messages. Please check your connection and try again.</p>
          <button class="retry-btn" onclick="window.location.reload()">Retry</button>
      </div>
  </body>
  </html>
  ```

  4. **Important details**:
     - ALL styling must be inline (`<style>` tag) — no external CSS files.
     - No external JavaScript dependencies — only the simple inline `onclick` for retry.
     - The page uses the app's accent color `#1976d2` for branding consistency.
     - The "Retry" button simply reloads the page, which will work if the user has regained connectivity.

  **Output**: A self-contained HTML file at `src/ToledoVault/wwwroot/offline.html`.

  **Dependencies**: None — can be created in parallel with T003 and T004.

  **Expected result**: The file `offline.html` is served at the URL `/offline.html`. Opening it in a browser shows a centered offline message with a retry button. The page looks good on both mobile and desktop viewports.

**Checkpoint**: Three core PWA files exist: `manifest.json`, `service-worker.js`, `offline.html`, plus two icon files. The foundation is complete. User story implementation can now begin.

---

## Phase 3: User Story 1 — Install App on Mobile Home Screen (Priority: P1) 🎯 MVP

**Goal**: Make the app installable on mobile devices (Android Chrome and iOS Safari). When a user visits the app URL on their phone, the browser recognizes it as a PWA and allows installation. The installed app opens in standalone mode with the Toledo Message icon on the home screen.

**Independent Test**:
1. Open Chrome on an Android device (or Android emulator).
2. Navigate to the app URL (e.g., `http://localhost:8080` or `https://chat.khamis.work`).
3. Verify that Chrome shows an "Install" option in the 3-dot menu or an install banner.
4. Install the app.
5. Verify the app icon appears on the home screen.
6. Tap the icon. Verify the app opens in standalone mode (no browser URL bar).
7. Repeat steps 1-6 on iOS Safari using "Share > Add to Home Screen".

**Why this is MVP**: Without the manifest link and service worker registration in the HTML host page, none of the PWA features work. This phase wires everything together.

### Implementation for User Story 1

- [x] T006 [US1] Modify `src/ToledoVault/Components/App.razor` to add manifest link, iOS meta tags, theme-color meta tag, and service worker registration script

  **Goal**: Wire up all PWA artifacts by adding the necessary HTML tags and JavaScript to the app's root Razor component. This is the ONLY file that needs to be modified for PWA to work. After this change, the browser can discover the manifest, register the service worker, and iOS can detect the app as standalone-capable.

  **What to do**:

  1. Open the file `src/ToledoVault/Components/App.razor`.
  2. Find the `<head>` section of the HTML.
  3. Add the following lines INSIDE the `<head>` tag (after existing `<link>` and `<meta>` tags):

  ```html
  <!-- PWA: Web App Manifest -->
  <link rel="manifest" href="/manifest.json" />

  <!-- PWA: Theme color for browser chrome (Android status bar, desktop title bar) -->
  <meta name="theme-color" content="#1976d2" />

  <!-- PWA: iOS-specific meta tags for standalone mode -->
  <meta name="apple-mobile-web-app-capable" content="yes" />
  <meta name="apple-mobile-web-app-status-bar-style" content="default" />
  <meta name="apple-mobile-web-app-title" content="Toledo Message" />

  <!-- PWA: iOS touch icon (iOS ignores manifest icons) -->
  <link rel="apple-touch-icon" href="/icon-192.png" />
  ```

  4. Find the `<body>` section, and just BEFORE the closing `</body>` tag, add the service worker registration script:

  ```html
  <!-- PWA: Service Worker Registration -->
  <script>
      if ('serviceWorker' in navigator) {
          navigator.serviceWorker.register('/service-worker.js')
              .then(function(registration) {
                  console.log('SW registered: ', registration.scope);
              })
              .catch(function(error) {
                  console.log('SW registration failed: ', error);
              });
      }
  </script>
  ```

  5. **Detailed explanation of each addition**:
     - `<link rel="manifest" href="/manifest.json" />` — Tells the browser where to find the Web App Manifest. Without this, the browser cannot detect that this is a PWA.
     - `<meta name="theme-color" content="#1976d2" />` — Sets the color of the Android status bar and desktop title bar when the app is installed.
     - `<meta name="apple-mobile-web-app-capable" content="yes" />` — Tells iOS Safari that this app should launch in standalone mode (no browser chrome) when opened from the home screen. iOS ignores the manifest's `display` property.
     - `<meta name="apple-mobile-web-app-status-bar-style" content="default" />` — Controls the iOS status bar appearance. "default" shows a white status bar with black text.
     - `<meta name="apple-mobile-web-app-title" content="Toledo Message" />` — Sets the app name on iOS. iOS may ignore the manifest's `name` property.
     - `<link rel="apple-touch-icon" href="/icon-192.png" />` — Provides the home screen icon for iOS. iOS ignores manifest icons and uses this tag instead.
     - The `<script>` block at the bottom registers the service worker. It first checks if the browser supports service workers (`'serviceWorker' in navigator`), then calls `navigator.serviceWorker.register()` with the path to our service worker file. If registration fails (e.g., in older browsers), it logs an error but does not break the app.

  6. **Do NOT** remove or modify any existing content in `App.razor`. Only ADD the new tags and script.

  **Input**: The existing file `src/ToledoVault/Components/App.razor`.

  **Output**: The modified file with 7 new HTML tags in `<head>` and 1 new `<script>` block before `</body>`.

  **Dependencies**: T003 (manifest.json must exist), T004 (service-worker.js must exist), T001 (icon-192.png must exist for apple-touch-icon).

  **Expected result**:
  - After deploying, opening Chrome DevTools > Application > Manifest shows the parsed manifest with all fields.
  - Chrome DevTools > Application > Service Workers shows the service worker as "activated and running".
  - On Android Chrome, the 3-dot menu shows "Install app" or "Add to Home Screen".
  - On iOS Safari, "Share > Add to Home Screen" creates a standalone app entry.

**Checkpoint**: At this point, the app is installable on mobile devices. User Story 1 (P1 — MVP) is complete. You can stop here for a minimum viable PWA.

---

## Phase 4: User Story 2 — Install App on Desktop (Priority: P2)

**Goal**: The app should be installable on desktop Chrome and Edge browsers. When visiting the app, users see an install icon in the browser address bar. Clicking it installs the app as a standalone desktop window.

**Independent Test**:
1. Open Chrome or Edge on a desktop computer.
2. Navigate to the app URL.
3. Look for an install icon (circled arrow or "+" icon) in the right side of the address bar.
4. Click the install icon. Verify an install dialog appears.
5. Install the app. Verify it opens in a standalone window (no tabs, no URL bar).
6. Close the window. Find the app in your OS app launcher (Start menu on Windows, Launchpad on macOS).
7. Launch it from the app launcher. Verify it opens correctly.

**Why no new tasks**: Desktop installation requires the EXACT same manifest, service worker, and registration as mobile (all done in Phase 2 and Phase 3). Chrome and Edge automatically detect the PWA and show the install button if:
- A valid manifest with `name`, `icons`, `start_url`, and `display: standalone` exists ✅ (T003)
- A service worker is registered ✅ (T006)
- The site is served over HTTPS (or localhost) ✅ (existing)

### Implementation for User Story 2

- [x] T007 [US2] Verify desktop PWA install works — no code changes needed, validation only

  **Goal**: Confirm that the PWA artifacts from Phase 2 and Phase 3 are sufficient for desktop installation. Desktop browsers (Chrome, Edge) use the same manifest and service worker as mobile — no additional code is required.

  **What to do**:
  1. Build and deploy the app: run `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force` from the repository root.
  2. Open Chrome or Edge on a desktop.
  3. Navigate to `http://localhost:8080`.
  4. Open Chrome DevTools (F12) > Application tab.
  5. Click "Manifest" in the left sidebar. Verify all fields are populated correctly.
  6. Click "Service Workers" in the left sidebar. Verify the service worker is "activated and running".
  7. Look for an install icon in the address bar (right side). If present, desktop PWA install is working.
  8. If the install icon does NOT appear, check the Chrome DevTools Console for errors and the Manifest section for warnings. Common issues:
     - Missing icon sizes → Fix manifest.json icons array
     - Service worker not registered → Check the script in App.razor
     - Not served over HTTPS → Localhost is exempt, but production needs HTTPS

  **Output**: A confirmation that desktop install works, OR a list of issues found.

  **Dependencies**: T006 must be complete (all PWA artifacts wired up).

  **Expected result**: The desktop install icon appears. Clicking it installs the app. The installed app opens in a standalone window with "Toledo Message" as the window title.

**Checkpoint**: User Stories 1 AND 2 are complete. The app is installable on mobile and desktop.

---

## Phase 5: User Story 3 — Offline Shell with Reconnection (Priority: P3)

**Goal**: When a user opens the installed PWA without network connectivity, the app shell (HTML, CSS, JS, WASM framework files) loads from the service worker cache. The user sees the app UI (login page or loading state) instead of a browser error page. This "offline shell" makes the app feel instant and native.

**Independent Test**:
1. Install the app (from Phase 3 or 4).
2. Open the app at least once with network connected (to populate the cache).
3. Enable airplane mode or disconnect from the network.
4. Open the app from the home screen / app launcher.
5. Verify the app shell loads (shows UI, not a browser error page).
6. If the user is not logged in: they should see the login page.
7. If the app cannot connect to SignalR: it should show a "Connecting..." or reconnection indicator (this is existing Blazor behavior).
8. Re-enable network. Verify the app reconnects automatically.

**Why the service worker from T004 already handles this**: The cache-first strategy in `service-worker.js` (T004) serves cached static files when offline. The offline fallback page (T005) is shown for pages not yet cached. The Blazor WASM framework has built-in reconnection behavior for SignalR.

### Implementation for User Story 3

- [x] T008 [US3] Enhance service worker static assets list in `src/ToledoVault/wwwroot/service-worker.js` to include Blazor WASM framework files

  **Goal**: The initial `STATIC_ASSETS` list in T004 only includes basic files. For the offline shell to work fully, the service worker must also pre-cache the Blazor WASM framework files (the `.dll` files, `blazor.boot.json`, and the `dotnet.wasm` runtime). These files are large (~15-30 MB) but essential for the app to render offline.

  **What to do**:
  1. Open `src/ToledoVault/wwwroot/service-worker.js`.
  2. Find the `STATIC_ASSETS` array near the top of the file.
  3. The current array from T004 contains:
     ```javascript
     const STATIC_ASSETS = [
       '/',
       '/index.html',
       '/css/app.css',
       '/manifest.json',
       '/icon-192.png',
       '/icon-512.png',
       '/offline.html'
     ];
     ```
  4. **Update the approach**: Instead of listing every Blazor file (which changes on each build), change the caching strategy so that the service worker dynamically caches ALL successful responses during the first visit. This way, all Blazor WASM files get cached automatically.
  5. Replace the `STATIC_ASSETS` array and the `install` event handler with this updated version:

  ```javascript
  // Core assets to pre-cache during install.
  // Blazor WASM framework files (.dll, .wasm, blazor.boot.json) are cached
  // dynamically on first load via the fetch handler's cache-on-response logic.
  const STATIC_ASSETS = [
    '/',
    '/offline.html',
    '/manifest.json',
    '/icon-192.png',
    '/icon-512.png'
  ];
  ```

  6. The fetch handler from T004 already caches successful responses dynamically:
     ```javascript
     if (networkResponse && networkResponse.status === 200 && networkResponse.type === 'basic') {
       const responseToCache = networkResponse.clone();
       caches.open(CACHE_VERSION).then((cache) => cache.put(event.request, responseToCache));
     }
     ```
     This means ALL Blazor framework files (`.dll`, `.wasm`, `.js`, `blazor.boot.json`) will be automatically cached on the first visit without needing to list them explicitly.

  7. **Do NOT change** the fetch event handler, activate event handler, or CACHE_VERSION. Only update the STATIC_ASSETS array.

  **Input**: The existing `service-worker.js` file from T004.

  **Output**: Updated `service-worker.js` with a simplified STATIC_ASSETS array.

  **Dependencies**: T004 (service-worker.js must exist).

  **Expected result**: After visiting the app once (which caches all framework files), enabling airplane mode and reopening the app shows the app shell loading from cache instead of a browser error page.

**Checkpoint**: User Stories 1, 2, AND 3 are complete. The app is installable and has offline shell support.

---

## Phase 6: User Story 4 — App Updates Seamlessly (Priority: P3)

**Goal**: When a new version of the app is deployed, the service worker detects the change, downloads the new files, and updates the cache. The user sees the new version on their next app launch without needing to manually clear cache or reinstall.

**Independent Test**:
1. Install the app and verify it works.
2. Change the `CACHE_VERSION` in `service-worker.js` from `'toledo-v1'` to `'toledo-v2'`.
3. Make any visible change to the app (e.g., change a color or add text).
4. Deploy the updated app.
5. Open the installed app (or refresh the browser tab).
6. Verify the visible change appears — confirming the new version loaded.
7. Open Chrome DevTools > Application > Cache Storage. Verify:
   - A cache named `toledo-v2` exists with the new files.
   - The old `toledo-v1` cache has been deleted.

**Why no new code is needed**: The service worker from T004 already implements the complete update lifecycle:
- `CACHE_VERSION` change → browser detects new service worker
- `install` event → opens new cache, downloads assets
- `activate` event → deletes all old caches
- `skipWaiting()` + `clients.claim()` → takes control immediately

### Implementation for User Story 4

- [x] T009 [US4] Verify cache update mechanism — no code changes needed, validation only

  **Goal**: Confirm that changing the `CACHE_VERSION` string in `service-worker.js` and redeploying triggers a full cache refresh for all users.

  **What to do**:
  1. Open `src/ToledoVault/wwwroot/service-worker.js`.
  2. Change `const CACHE_VERSION = 'toledo-v1';` to `const CACHE_VERSION = 'toledo-v2';`.
  3. Deploy the app: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force`.
  4. Open the app in Chrome. Open DevTools > Application > Service Workers.
  5. Verify that a new service worker is detected ("waiting to activate" or "activated").
  6. Refresh the page. Verify the new service worker is now active.
  7. Go to Application > Cache Storage. Verify `toledo-v2` exists and `toledo-v1` is deleted.
  8. **After verifying**, change the version back to `'toledo-v1'` (so it's ready for the actual first deploy).

  **Output**: Confirmation that the cache versioning mechanism works correctly.

  **Dependencies**: T004 and T008 (service worker must be complete).

  **Expected result**: Changing the version string and redeploying causes the old cache to be deleted and replaced with a new one. The user sees the latest version of the app.

**Checkpoint**: All four user stories are complete. The app is installable, has offline shell support, and updates seamlessly.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements that affect the overall project quality: documentation updates, Lighthouse audit, and cross-platform testing.

- [x] T010 [P] Update `CLAUDE.md` with PWA support documentation

  **Goal**: Add PWA support to the project's development guidelines file so that future developers and AI agents know about the PWA artifacts and how to maintain them.

  **What to do**:
  1. Open `CLAUDE.md` at the repository root.
  2. In the "Active Technologies" section, add a new bullet:
     ```
     - PWA (Progressive Web App): manifest.json, service-worker.js, offline.html in src/ToledoVault/wwwroot/ (009-pwa-support)
     ```
  3. In the "Project Structure" section (or equivalent), add:
     ```
     PWA artifacts: src/ToledoVault/wwwroot/ (manifest.json, service-worker.js, offline.html, icon-192.png, icon-512.png)
     ```
  4. Add a note about the cache versioning:
     ```
     ## PWA Cache Versioning
     - Bump `CACHE_VERSION` in `src/ToledoVault/wwwroot/service-worker.js` on every deploy
     - Current format: 'toledo-vN' where N is an incrementing integer
     - Forgetting to bump the version means users will get stale cached files
     ```

  **Output**: Updated `CLAUDE.md` with PWA documentation.

  **Dependencies**: None — can run in parallel with other polish tasks.

  **Expected result**: `CLAUDE.md` contains clear documentation about PWA support, file locations, and the cache versioning requirement.

- [x] T011 [P] Run Lighthouse PWA audit and fix any issues

  **Goal**: Validate that the PWA implementation meets Google's Lighthouse PWA criteria with a score of 90+.

  **What to do**:
  1. Deploy the app: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force`.
  2. Open Chrome and navigate to the app URL.
  3. Open Chrome DevTools (F12) > Lighthouse tab.
  4. Select "Progressive Web App" category.
  5. Click "Analyze page load".
  6. Review the results:
     - **Installable**: Should pass (manifest + service worker + HTTPS/localhost).
     - **PWA Optimized**: Should pass (theme-color, viewport, apple-touch-icon).
     - **Best Practices**: Should pass.
  7. If any audit items fail, fix them by updating the relevant file:
     - Missing meta tags → Update `App.razor` (T006)
     - Manifest issues → Update `manifest.json` (T003)
     - Service worker issues → Update `service-worker.js` (T004)
  8. Re-run the audit until the PWA score is 90+.

  **Output**: Lighthouse PWA audit score of 90+ (or list of issues that could not be resolved).

  **Dependencies**: T006 must be complete (all PWA artifacts wired up and deployed).

  **Expected result**: Lighthouse PWA audit passes with a score of 90 or higher.

- [x] T012 Run quickstart.md validation scenarios from `specs/009-pwa-support/quickstart.md`

  **Goal**: Execute all the manual test scenarios documented in the quickstart file to verify full PWA functionality across platforms.

  **What to do**:
  1. Read `specs/009-pwa-support/quickstart.md` for the full list of test scenarios.
  2. Execute each scenario:
     - **Build and deploy**: `dotnet publish` or `deploy-iis.ps1`
     - **Lighthouse audit**: (Already done in T011)
     - **Android install**: Visit app in Chrome on Android > tap "Install" or 3-dot menu > "Add to Home Screen"
     - **iOS install**: Visit app in Safari on iOS > Share > "Add to Home Screen"
     - **Desktop install**: Visit app in Chrome/Edge > click install icon in address bar
     - **Offline test**: Install app > enable airplane mode > open app > verify shell loads from cache
     - **Update test**: Change `CACHE_VERSION` in service-worker.js > deploy > reopen app > verify new version loads
  3. Document pass/fail for each scenario.

  **Output**: Pass/fail results for each quickstart scenario.

  **Dependencies**: All previous tasks (T001–T011) must be complete.

  **Expected result**: All quickstart scenarios pass. The PWA works on Android, iOS, and desktop. Offline shell loads. Updates propagate.

---

## Phase 8: Review Fixes

**Purpose**: Address issues found during code review of Phase 1–7 implementation.

- [x] T013 [P] Fix manifest.json icon `purpose` field — split `"any maskable"` into separate entries

  **Goal**: Avoid Lighthouse deprecation warning. The combined `"any maskable"` value is deprecated; each purpose must be a separate icon entry.

  **What was done**: Split 2 icon entries into 4 (192 any, 192 maskable, 512 any, 512 maskable).

- [x] T014 [P] Add bilingual support to `offline.html` (FR-014)

  **Goal**: The offline fallback page was English-only. Added Arabic translations with language detection from `localStorage('app.culture')`, matching the app's existing localization pattern.

- [x] T015 [P] Fix quickstart.md icon path references

  **Goal**: `quickstart.md` referenced `icons/icon-192.png` (subdirectory) but actual files are at root `/icon-192.png`. Fixed to match implementation.

- [x] T016 [P] Add HTTP warning banner to App.razor (FR-015)

  **Goal**: Show a dismissible warning banner when the app is accessed via non-localhost HTTP. PWA features require HTTPS. Banner is bilingual (EN/AR), dismissible with localStorage persistence.

- [x] T017 Mark FR-012 (push notifications) as deferred in spec.md

  **Goal**: The requirements checklist and tasks scoped push notifications out of this feature. Updated spec.md FR-012 to reflect deferred status. Push notifications will be a separate feature branch due to scope (DB schema, NuGet package, VAPID, API endpoints).

**Checkpoint**: All review issues resolved. FR-013 (offline queue) was already implemented in Chat.razor + storage.js. FR-012 (push) deferred. FR-014 (localization) now covered. FR-015 (HTTP warning) implemented.

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup: Icons)
    ↓
Phase 2 (Foundational: manifest.json, service-worker.js, offline.html)
    ↓
Phase 3 (US1: Wire up App.razor — Mobile Install) ← MVP STOP POINT
    ↓
Phase 4 (US2: Desktop Install — validation only, no code)
    ↓
Phase 5 (US3: Offline Shell — update service-worker.js STATIC_ASSETS)
    ↓
Phase 6 (US4: Seamless Updates — validation only, no code)
    ↓
Phase 7 (Polish: CLAUDE.md, Lighthouse, quickstart validation)
```

### Task Dependencies (Detailed)

| Task | Depends On | Blocks |
|------|-----------|--------|
| T001 (icon-192.png) | Nothing | T003, T006 |
| T002 (icon-512.png) | Nothing | T003 |
| T003 (manifest.json) | T001, T002 | T006 |
| T004 (service-worker.js) | Nothing | T006, T008, T009 |
| T005 (offline.html) | Nothing | T004 (referenced in SW) |
| T006 (App.razor) | T003, T004, T001 | T007, T008, T009, T010, T011, T012 |
| T007 (Desktop validation) | T006 | T012 |
| T008 (SW STATIC_ASSETS) | T004 | T009 |
| T009 (Update validation) | T008 | T012 |
| T010 (CLAUDE.md) | Nothing | T012 |
| T011 (Lighthouse) | T006 | T012 |
| T012 (Quickstart validation) | T007, T009, T010, T011 | Nothing |

### User Story Dependencies

- **User Story 1 (P1 — Mobile Install)**: Requires Phase 1 + Phase 2. Can start after foundational files exist. **This is the MVP.**
- **User Story 2 (P2 — Desktop Install)**: No new code needed beyond US1. Validation only.
- **User Story 3 (P3 — Offline Shell)**: Requires US1 complete (service worker registered). Enhances the service worker's caching list.
- **User Story 4 (P3 — Seamless Updates)**: Requires US3 complete (service worker fully configured). Validation only.

### Within Each User Story

1. Foundational files must exist (manifest, service worker, icons, offline page)
2. App.razor must wire them up (registration, meta tags)
3. Validation confirms it works
4. Polish adds documentation and audit scores

### Parallel Opportunities

```
PARALLEL GROUP 1 (Phase 1 — can all run simultaneously):
  T001 (icon-192.png)    — independent file
  T002 (icon-512.png)    — independent file

PARALLEL GROUP 2 (Phase 2 — can run after Phase 1):
  T003 (manifest.json)   — depends on T001, T002
  T004 (service-worker.js) — independent file
  T005 (offline.html)    — independent file
  Note: T004 and T005 can run in parallel with each other and with T003

SEQUENTIAL (Phases 3-6):
  T006 → T007 → T008 → T009
  (Each depends on the previous)

PARALLEL GROUP 3 (Phase 7 — after T009):
  T010 (CLAUDE.md)       — independent file
  T011 (Lighthouse)      — independent activity

SEQUENTIAL (Final):
  T012 (quickstart validation) — depends on T010, T011
```

---

## Parallel Example: Phase 1 + Phase 2

```bash
# Launch Phase 1 tasks in parallel (both are independent files):
Task T001: "Create icon-192.png at src/ToledoVault/wwwroot/icon-192.png"
Task T002: "Create icon-512.png at src/ToledoVault/wwwroot/icon-512.png"

# After Phase 1, launch Phase 2 tasks (T004 and T005 are parallel):
Task T003: "Create manifest.json at src/ToledoVault/wwwroot/manifest.json" (needs T001, T002)
Task T004: "Create service-worker.js at src/ToledoVault/wwwroot/service-worker.js" (independent)
Task T005: "Create offline.html at src/ToledoVault/wwwroot/offline.html" (independent)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only — Phases 1-3)

1. **Complete Phase 1**: Create icon files (T001, T002)
2. **Complete Phase 2**: Create manifest, service worker, offline page (T003, T004, T005)
3. **Complete Phase 3**: Wire up App.razor (T006)
4. **STOP AND VALIDATE**: Test mobile install on Android Chrome and iOS Safari
5. **Deploy if ready**: The app is now installable on mobile — this is a shippable MVP

### Incremental Delivery

1. **Phases 1-3** → Mobile install works → Deploy (MVP!)
2. **Phase 4** → Desktop install verified → Deploy
3. **Phase 5** → Offline shell works → Deploy
4. **Phase 6** → Update mechanism verified → Deploy
5. **Phase 7** → Documentation and audit → Final deploy

### File Summary

| File | Action | Phase | Task |
|------|--------|-------|------|
| `src/ToledoVault/wwwroot/icon-192.png` | CREATE | 1 | T001 |
| `src/ToledoVault/wwwroot/icon-512.png` | CREATE | 2 | T002 |
| `src/ToledoVault/wwwroot/manifest.json` | CREATE | 2 | T003 |
| `src/ToledoVault/wwwroot/service-worker.js` | CREATE | 2 | T004 |
| `src/ToledoVault/wwwroot/offline.html` | CREATE | 2 | T005 |
| `src/ToledoVault/Components/App.razor` | MODIFY | 3 | T006 |
| `src/ToledoVault/wwwroot/service-worker.js` | MODIFY | 5 | T008 |
| `CLAUDE.md` | MODIFY | 7 | T010 |

**Total new files**: 5 (icon-192.png, icon-512.png, manifest.json, service-worker.js, offline.html)
**Total modified files**: 2 (App.razor, CLAUDE.md)
**Total code tasks**: 7 (T001–T006, T008, T010)
**Total validation tasks**: 5 (T007, T009, T011, T012)

---

## Notes

- **[P] tasks** = different files, no dependencies on each other — can be executed simultaneously
- **[Story] label** maps task to specific user story for traceability
- Each user story is independently testable after completion
- **CACHE_VERSION must be bumped** on every production deploy — this is the single most important operational requirement
- The service worker MUST NOT cache `/api/*` or `/hubs/*` paths — these are real-time messaging endpoints
- iOS requires proprietary `<meta>` tags in addition to the manifest — the manifest alone is NOT sufficient for iOS
- All existing functionality (messaging, auth, themes, localization) must continue to work identically — zero regressions expected since PWA is purely additive (new files + meta tags)
- If Lighthouse audit fails, the most common fixes are: adding missing meta tags, fixing icon sizes, or ensuring the service worker registers correctly
