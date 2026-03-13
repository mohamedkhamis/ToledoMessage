# Research: PWA Support

**Feature**: 009-pwa-support | **Date**: 2026-03-12

## R-001: Service Worker Caching Strategy for Blazor WASM

**Decision**: Cache-first for static assets (JS, CSS, WASM, HTML, images), network-first for API calls and SignalR.

**Rationale**: Blazor WASM apps have large framework files (~15-30 MB) that rarely change between deploys. Cache-first ensures instant offline loading. API/SignalR must always go to network since real-time messaging requires live connectivity. Version-based cache naming ensures clean updates on deploy.

**Alternatives considered**:
- Network-first for everything: Rejected — defeats the purpose of offline shell, and WASM files are too large to re-download every time.
- Cache-only: Rejected — would permanently trap users on stale versions.
- Blazor's built-in `service-worker.published.js` template: Considered but rejected — it's designed for the standalone Blazor WASM template, not the hosted model. A custom lightweight service worker gives more control over cache strategy and avoids framework coupling.

## R-002: Manifest Requirements for Cross-Platform Install

**Decision**: Standard W3C Web App Manifest with `display: standalone`, `start_url: "/"`, icons at 192px and 512px, and appropriate theme/background colors.

**Rationale**: This is the minimum required by Chrome's install criteria (manifest + service worker + HTTPS). iOS Safari reads the manifest for `display` mode but relies on `<meta>` tags for icons and colors. Edge follows Chrome's criteria.

**Alternatives considered**:
- `display: fullscreen`: Rejected — hides status bar, inappropriate for a messaging app.
- `display: minimal-ui`: Rejected — still shows browser chrome, doesn't feel native.

## R-003: iOS PWA Compatibility

**Decision**: Add iOS-specific `<meta>` tags: `apple-mobile-web-app-capable`, `apple-mobile-web-app-status-bar-style`, and `apple-touch-icon` link.

**Rationale**: iOS Safari ignores many manifest properties and relies on proprietary meta tags. Without these, the app won't launch in standalone mode from the home screen on iOS.

**Alternatives considered**:
- Rely on manifest only: Rejected — iOS ignores manifest `display` property for standalone behavior.
- Skip iOS support: Rejected — significant portion of users are on iOS.

## R-004: Cache Versioning and Update Strategy

**Decision**: Use a version string constant in the service worker file. On `install` event, open a new versioned cache. On `activate` event, delete all caches except the current version. Use `skipWaiting()` + `clients.claim()` for immediate activation.

**Rationale**: This is the standard recommended approach. The version string changes with each deploy (can be tied to build hash or manual version bump). `skipWaiting()` ensures the new service worker takes over immediately rather than waiting for all tabs to close.

**Alternatives considered**:
- Workbox library: Rejected — adds a dependency for a simple caching strategy that can be done in ~50 lines of vanilla JS.
- `self.registration.update()` polling: Not needed — the browser checks for SW updates on every navigation by default.

## R-005: App Icon Generation

**Decision**: Create a simple text-based icon using the Toledo Message branding (letter "T" or "TM" on the app's accent color background). Provide 192x192 and 512x512 PNG files.

**Rationale**: The minimum for PWA installability is two icon sizes. More sizes (72, 96, 128, 144, 152, 384) can be added later for better platform coverage. The icon should use the default theme's accent color (#1976d2) for brand consistency.

**Alternatives considered**:
- SVG icon only: Rejected — manifest requires PNG for broad compatibility.
- Generate many sizes: Deferred — 2 sizes meet minimum Lighthouse requirements.

## R-006: Push Notification Architecture (VAPID)

**Decision**: Server generates VAPID key pair on startup (or via configuration), stores private key for signing push messages. Client subscribes via service worker PushManager, sends subscription to server. Server uses WebPush library to send notifications.

**Rationale**: VAPID (Voluntary Application Server Identification) is the standard for Web Push. The server must hold the VAPID private key to authenticate push requests. The client subscribes and receives a PushSubscription object containing the endpoint and keys.

**Server-side**:
- Generate VAPID key pair (can be done once and stored, or generated on startup)
- Store in appsettings.json or generate dynamically
- Endpoint to receive push subscriptions from clients
- Service to send push notifications using WebPush.NET library

**Client-side**:
- Request notification permission
- Subscribe via service worker PushManager
- Send subscription to server via API
- Handle incoming push events in service worker

**Alternatives considered**:
- Firebase Cloud Messaging: Rejected — adds Google dependency, server must relay through FCM
- Third-party push service: Rejected — unnecessary, native Web Push is sufficient

## R-007: Offline Message Queuing

**Decision**: Use IndexedDB to queue outgoing messages when offline. On SignalR reconnect, process the queue in order. Show pending status in UI.

**Rationale**:
- IndexedDB is already available in the project (used for session storage)
- SignalR auto-reconnects, but messages sent during disconnection gap may be lost
- Queue ensures messages persist across page refreshes and are reliably delivered

**Implementation**:
1. Intercept message send in SignalR service
2. If online: send immediately via SignalR
3. If offline: store in IndexedDB queue with timestamp
4. On SignalR `onreconnected`: process queue, send all in order
5. On server ACK: remove from queue
6. Show pending indicator in chat UI

## R-008: Localization for PWA UI

**Decision**: Use existing localization infrastructure (ToledoMessage.Shared resources) for all PWA UI strings. Service worker and JavaScript will load localized strings from Blazor's localization system.

**Rationale**: The app already has full English/Arabic localization. PWA strings (install prompts, offline indicators, notification text) should follow the same pattern.

**Implementation**:
- Add PWA-specific strings to shared resources
- JavaScript loads from `/api/preferences/locale` or reads from Blazor state
- Service worker notifications use same localization

## R-009: HTTP/HTTPS Handling

**Decision**: Allow PWA features on localhost HTTP. Show warning banner for non-localhost HTTP. Production requires HTTPS.

**Rationale**: Service workers and push notifications require HTTPS (except localhost for development). This is standard browser security policy.

**Implementation**:
- Check `window.location.hostname` in app initialization
- If not localhost and not HTTPS: show warning banner
- PWA features may be limited on HTTP

---

## Resolved NEEDS CLARIFICATION

No items required clarification — all technical decisions are straightforward for PWA.
