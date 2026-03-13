# Quickstart: PWA Support

**Feature**: 009-pwa-support | **Date**: 2026-03-12

## What This Feature Does

Makes ToledoMessage installable as a Progressive Web App on mobile (iOS/Android) and desktop (Chrome/Edge). Users can add the app to their home screen or app launcher, and it opens in a standalone window without browser chrome.

Includes push notifications for new messages and offline message queuing.

## Files to Create

**Server (wwwroot)**:
1. `manifest.json` — Web App Manifest
2. `service-worker.js` — Service worker with cache-first strategy
3. `icon-192.png` — App icon 192x192
4. `icon-512.png` — App icon 512x512
5. `offline.html` — Offline fallback page

**Client (wwwroot)**:
1. `push-notification.js` — Push notification handling (subscribe, display)
2. `offline-queue.js` — Offline message queue management

**Server (Configuration)**:
1. Generate VAPID key pair (one-time)
2. Add to `appsettings.json`

## Files to Modify

1. **`src/ToledoMessage/Components/App.razor`** — Add `<link rel="manifest">`, iOS meta tags, service worker registration script
2. **`src/ToledoMessage.Client/Services/SignalRService.cs`** — Add offline queue processing
3. **`src/ToledoMessage.Client/Pages/Chat.razor`** — Add pending message indicator UI
4. **`src/ToledoMessage/appsettings.json`** — Add VAPID keys
5. **`CLAUDE.md`** — Document PWA support in the tech stack and architecture sections

## How to Test

1. **Build and deploy**: `dotnet publish` or `deploy-iis.ps1`
2. **Lighthouse audit**: Open Chrome DevTools > Lighthouse > Run PWA audit (target: 90+)
3. **Android install**: Visit app in Chrome > tap "Install" or 3-dot menu > "Add to Home Screen"
4. **iOS install**: Visit app in Safari > Share > "Add to Home Screen"
5. **Desktop install**: Visit app in Chrome/Edge > click install icon in address bar
6. **Offline test**: Install app > enable airplane mode > open app > verify shell loads from cache
7. **Update test**: Change version in service-worker.js > deploy > reopen app > verify new version loads
8. **Push notification test**: Send message to user while app is closed > verify notification appears
9. **Offline message test**: Enable airplane mode > compose message > disable airplane mode > verify message sends

## Key Implementation Notes

- Service worker scope is `/` (root) — must be served from root, not a subdirectory
- Cache strategy: cache-first for static assets, network-first for `/api/*` and `/hubs/*`
- iOS requires `<meta name="apple-mobile-web-app-capable" content="yes">` in addition to manifest
- Version string in service worker must be bumped on each deploy to trigger cache refresh
- VAPID keys are generated once per deployment; store securely
- Push subscriptions stored per-device in server database (linked to Devices table)
- Offline queue uses IndexedDB with FIFO processing on SignalR reconnect
