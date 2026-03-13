# Implementation Plan: PWA Support

**Branch**: `009-pwa-support` | **Date**: 2026-03-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-pwa-support/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add Progressive Web App (PWA) support to the existing Blazor WebAssembly chat application, enabling installation on iOS, Android, and desktop browsers. The implementation includes:
- Web App Manifest for installability
- Service Worker for offline shell caching
- Push notification support for new messages and reactions
- Offline message queuing with automatic send on reconnect

## Technical Context

**Language/Version**: C# / .NET 11.0 (preview), JavaScript (ES2020 for service worker)
**Primary Dependencies**: Blazor WebAssembly, Service Worker API, Web Push API, WebAPIs for .NET
**Storage**: IndexedDB (client-side for offline queue), Service Worker Cache API
**Testing**: MSTest (existing server tests), browser-based testing (PWA requires browser environment)
**Target Platform**: Web browsers supporting PWA - iOS Safari 15+, Android Chrome 90+, Desktop Chrome/Edge 90+
**Project Type**: Progressive Web App (PWA) layered on existing Blazor WASM application
**Performance Goals**: Offline shell load < 2s, PWA Lighthouse score 90+
**Constraints**: HTTPS required for full PWA (except localhost dev), icons generated from existing favicon.svg
**Scale/Scope**: Existing ToledoMessage users - no scale changes, same backend API

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Gate Analysis

| Constitution Principle | Compliance | Notes |
|----------------------|------------|-------|
| I. Zero-Trust Server | ✅ PASS | PWA features are client-side only; no server changes to message handling |
| II. Hybrid Cryptography | ✅ N/A | No crypto changes - PWA is presentation/infrastructure layer |
| III. Established Libraries Only | ✅ PASS | Using native Web APIs (Service Worker, Web Push) - standard browser APIs |
| IV. Signal Protocol Fidelity | ✅ N/A | No changes to Signal Protocol implementation |
| V. .NET Ecosystem | ✅ PASS | Uses existing Blazor WASM; adds JavaScript interop for PWA APIs |
| VI. Test-First Development | ✅ PASS | Will add browser-based PWA tests alongside implementation |
| VII. Open-Source Transparency | ✅ PASS | All PWA code is client-side and visible |

**Gate Status**: ✅ ALL PASS - Proceed to Phase 0

### Phase 0 Complete: Re-check After Research

All technical decisions documented in `research.md`. No constitutional violations identified.

**Post-Research Constitution Check**:
| Constitution Principle | Compliance | Notes |
|----------------------|------------|-------|
| I. Zero-Trust Server | ✅ PASS | PWA is client-side only; push subscriptions stored per-device (not message content) |
| II. Hybrid Cryptography | ✅ N/A | No crypto changes |
| III. Established Libraries Only | ✅ PASS | Using native Web APIs + WebPush.NET for server |
| IV. Signal Protocol Fidelity | ✅ N/A | No changes to Signal Protocol |
| V. .NET Ecosystem | ✅ PASS | Uses Blazor WASM; WebPush.NET is .NET library |
| VI. Test-First Development | ✅ PASS | Will add PWA tests |
| VII. Open-Source Transparency | ✅ PASS | All client-side PWA code visible |

**Gate Status**: ✅ ALL PASS - Ready for Phase 1

## Project Structure

### Documentation (this feature)

```text
specs/009-pwa-support/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (not needed - no external API contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

The PWA feature adds files to the existing Blazor WebAssembly project structure:

```text
src/ToledoMessage/                                    # Server project (host)
├── wwwroot/
│   ├── manifest.json                                 # NEW: Web App Manifest
│   ├── icons/
│   │   ├── icon-192.png                             # NEW: Generated from favicon.svg
│   │   └── icon-512.png                             # NEW: Generated from favicon.svg
│   └── service-worker.js                            # NEW: Service Worker script

src/ToledoMessage.Client/                            # Blazor WASM client
├── wwwroot/
│   ├── push-notification.js                         # NEW: Push notification handling
│   ├── offline-queue.js                             # NEW: Offline message queue
│   └── app.js                                       # MODIFIED: Add PWA initialization
├── Services/
│   ├── PushNotificationService.cs                   # NEW: Push notification service
│   ├── OfflineQueueService.cs                       # NEW: Offline message queue service
│   └── ServiceWorkerRegistrationService.cs          # NEW: SW registration
└── Pages/
    └── (existing pages remain unchanged)
```

**Structure Decision**: PWA features are implemented as client-side JavaScript with C# wrapper services. No server-side code changes required for the core PWA functionality. Push notifications will require minimal server-side additions for VAPID key management.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
