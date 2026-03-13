# Data Model: PWA Support

**Feature**: 009-pwa-support | **Date**: 2026-03-12

## Summary

PWA support introduces **minimal new server-side data model changes** (only VAPID keys). Most PWA artifacts are static files served from `wwwroot/`:

| Artifact | Type | Description |
|----------|------|-------------|
| `manifest.json` | Static JSON | Web App Manifest — app identity, icons, display mode |
| `service-worker.js` | Static JS | Caching logic, offline fallback, version management |
| `push-notification.js` | Static JS | Push event handling in service worker |
| `offline-queue.js` | Static JS | Offline message queue management |
| `icon-192.png` | Static image | App icon 192x192 |
| `icon-512.png` | Static image | App icon 512x512 |
| `offline.html` | Static HTML | Fallback page shown when offline and no cache available |

## Server-Side Storage

### VAPID Keys (NEW)

Push notifications require a VAPID (Voluntary Application Server Identification) key pair:

| Field | Type | Description |
|-------|------|-------------|
| `VapidPublicKey` | string | Public key included in manifest and client subscription |
| `VapidPrivateKey` | string | Private key used to sign push messages (server-only) |

**Storage**: Configuration file (`appsettings.json`) or environment variables. Generated once at deployment time.

**Note**: This is the only server-side addition. No database schema changes required.

## Client-Side Storage (Browser)

### Service Worker Cache API

- **Cache name**: `toledo-v{VERSION}` (e.g., `toledo-v1`)
- **Cached content**: HTML shell, CSS, JS bundles, WASM framework files, app icons
- **Not cached**: API responses (`/api/*`), SignalR connections (`/hubs/*`), external URLs
- **Eviction**: Old versioned caches deleted on service worker `activate` event
- **Size**: ~15-30 MB (Blazor WASM framework size)

### IndexedDB (Offline Message Queue)

The offline message queue uses IndexedDB with the following schema:

| Object Store | Key Path | Indexes |
|--------------|----------|---------|
| `offline-messages` | `id` (auto) | `timestamp`, `conversationId`, `status` |

**OfflineMessage Schema**:
```typescript
interface OfflineMessage {
  id: number;           // Auto-generated
  conversationId: string;
  recipientId: string;
  encryptedContent: string;  // Already encrypted by Signal Protocol
  timestamp: number;   // Unix ms when queued
  status: 'pending' | 'sent' | 'failed';
  retryCount: number;
}
```

### Push Subscription Storage

Push subscriptions are stored on the server (associated with user devices):

| Field | Type | Description |
|-------|------|-------------|
| `Endpoint` | string | Push service endpoint URL |
| `P256DH` | string | Elliptic curve public key |
| `Auth` | string | Auth secret |
| `DeviceId` | long | Foreign key to Devices table |

No changes to existing IndexedDB usage for session/ratchet state.
