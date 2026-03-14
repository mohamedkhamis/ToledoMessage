# Implementation Plan: ToledoVault SaaS Enhancement Plan (v2.0)

**Branch**: `008-saas-enhancements` | **Date**: 2026-03-09 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-saas-enhancements/spec.md`

## Summary

Comprehensive hardening and enhancement of the ToledoVault E2EE messaging SaaS across 6 pillars: security (rate limiting in SignalR hub, authorization gap closures, error sanitization, eval removal, storage encryption checks), performance (response compression, message list virtualization, typing indicator caching, batched notifications, query optimization, debounce, background jitter, pre-key cleanup), UI (accessibility WCAG 2.1 AA, responsive fixes, date separators, localization completeness), UX (error recovery, offline indicator, voice recording limits, file send preservation, in-conversation search), encryption (signature versioning, disappearing messages wiring), and functionality (offline message queue, group multi-admin, presence heartbeat timeout).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS), JavaScript (browser interop)
**Primary Dependencies**: ASP.NET Core, Blazor WebAssembly, SignalR, EF Core 10, BouncyCastle.Cryptography 2.6.2, Serilog
**Storage**: SQL Server 2022 (server-side via EF Core Code First) + Browser IndexedDB (client-side via JS interop)
**Testing**: MSTest 4.1.0, 231 existing tests (152 server + 65 crypto + 8 integration + 6 client)
**Target Platform**: Windows Server (IIS), Blazor WASM in browser
**Project Type**: Web application (Blazor WebAssembly hosted on ASP.NET Core)
**Performance Goals**: 60fps message list scrolling, 0 DB queries for typing indicators, 40%+ transfer size reduction with compression
**Constraints**: No breaking wire protocol changes, additive-only DB schema changes, backward-compatible signature format, WCAG 2.1 AA compliance
**Scale/Scope**: Single-instance deployment, ~10 concurrent users currently, 6 source projects + 5 test projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero-Trust Server | PASS | All crypto remains client-side. Offline queue stores encrypted ciphertext only. No new server-side access to plaintext. |
| II. Hybrid Cryptography | PASS | Signature versioning adds a version byte prefix to existing hybrid signatures. No single-layer paths introduced. |
| III. Established Libraries Only | PASS | No new crypto libraries. BouncyCastle remains the sole crypto library for WASM. |
| IV. Signal Protocol Fidelity | PASS | Disappearing messages are a client-side display feature, not a protocol change. Group admin changes don't affect sender keys. |
| V. .NET Ecosystem | PASS | All changes use existing .NET stack. No new frameworks introduced. |
| VI. Test-First Development | PASS | All 231+ existing tests must continue to pass. New tests required for rate limiting, authorization, offline queue. |
| VII. Open-Source Transparency | PASS | No security-through-obscurity. Error sanitization hides internals from users, not from code reviewers. |

**Gate result: PASS — no violations. Proceeding to Phase 0.**

## Project Structure

### Documentation (this feature)

```text
specs/008-saas-enhancements/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Toledo.SharedKernel/              # Shared utilities (IdGenerator)
├── ToledoVault/                    # ASP.NET Core server
│   ├── Controllers/                  # 9 REST API controllers
│   │   ├── AuthController.cs
│   │   ├── ConversationsController.cs
│   │   ├── MessagesController.cs
│   │   ├── UsersController.cs
│   │   ├── DevicesController.cs
│   │   ├── KeyBackupController.cs
│   │   ├── LinkPreviewController.cs
│   │   ├── PreferencesController.cs
│   │   └── BaseApiController.cs
│   ├── Hubs/
│   │   └── ChatHub.cs                # SignalR hub (main target for rate limiting)
│   ├── Middleware/
│   │   └── RateLimitMiddleware.cs    # Existing HTTP rate limiting
│   ├── Models/                       # EF Core entities
│   ├── Data/                         # DbContext + configurations
│   ├── Services/                     # Server business logic
│   │   ├── RateLimitService.cs       # Existing — reuse for SignalR
│   │   ├── PresenceService.cs        # Extend for heartbeat timeout
│   │   ├── MessageCleanupHostedService.cs  # Extend for pre-key cleanup + jitter
│   │   ├── MessageRelayService.cs    # Extend for read pointer clamping + batching
│   │   ├── LinkPreviewService.cs
│   │   ├── PreKeyService.cs
│   │   └── AccountDeletionService.cs
│   └── wwwroot/                      # Server static assets (app.css, themes.css)
├── ToledoVault.Client/             # Blazor WebAssembly
│   ├── Pages/                        # Blazor pages
│   │   ├── Chat.razor                # Main chat — virtualization, offline queue, search, expiry
│   │   ├── Login.razor               # Error sanitization
│   │   ├── Register.razor            # Error sanitization
│   │   ├── Settings.razor            # Voice limit settings display
│   │   ├── SecurityInfo.razor
│   │   └── NewConversation.razor
│   ├── Components/                   # Reusable components
│   │   ├── ConversationListSidebar.razor   # Accessibility, debounce
│   │   ├── MessageBubble.razor       # Date separators, search highlight
│   │   ├── MessageInput.razor        # Offline queue, file preservation, rate limit warning
│   │   ├── EmojiPicker.razor         # Keyboard nav, debounce, memoize
│   │   ├── VoiceRecorder.razor       # 5-min limit, countdown
│   │   ├── ImageLightbox.razor       # Mobile responsive fix
│   │   └── DisappearingTimerConfig.razor  # Localization
│   ├── Services/                     # Client-side services
│   │   ├── SignalRService.cs         # Offline detection, reconnect queue
│   │   ├── MessageExpiryService.cs   # Wire to actual message flow
│   │   ├── LocalStorageService.cs    # Encryption init check
│   │   ├── CryptoService.cs
│   │   └── ...
│   └── wwwroot/                      # Client JS files
│       ├── storage.js                # Cookie management (replace eval)
│       ├── tab-leader.js
│       ├── notifications.js
│       ├── media-helpers.js
│       └── voice-recorder.js         # 5-min limit enforcement
├── ToledoVault.Shared/             # DTOs, enums, constants
│   └── Enums/
│       └── ParticipantRole.cs        # Already has Admin role
└── ToledoVault.Crypto/             # BouncyCastle crypto (signature versioning)

tests/
├── ToledoVault.Server.Tests/       # Server unit tests
├── ToledoVault.Client.Tests/       # Client unit tests
├── ToledoVault.Crypto.Tests/       # Crypto unit tests
├── ToledoVault.Integration.Tests/  # Integration tests
└── ToledoVault.Benchmarks/         # Performance benchmarks
```

**Structure Decision**: Existing multi-project Blazor hosted structure is retained. No new projects needed. All changes are modifications to existing files or new files within existing project directories.

## Complexity Tracking

> No constitution violations detected. No complexity justifications needed.

---

## Phase 0: Research

### Research Findings

All technical decisions below were determined by analyzing the existing codebase. No external research agents were needed as the tech stack and patterns are well-established.

### R-001: SignalR Hub Rate Limiting Strategy

**Decision**: Inject the existing `RateLimitService` into `ChatHub` and call `IsRateLimited()` at the top of `SendMessage` and `TypingIndicator` methods.

**Rationale**: The `RateLimitService` (in-memory ConcurrentDictionary) is already battle-tested in the HTTP middleware. SignalR hub methods can use the same service with different keys (e.g., `signalr:send:{userId}`, `signalr:typing:{userId}`). No new infrastructure needed.

**Alternatives considered**:
- ASP.NET Core built-in rate limiting middleware: Does not apply to SignalR hub methods (only HTTP pipeline).
- Custom SignalR filter: Adds unnecessary abstraction. Direct calls in hub methods are simpler and more explicit.

### R-002: Message List Virtualization Approach

**Decision**: Use Blazor's built-in `<Virtualize>` component with `ItemsProvider` delegate for on-demand message loading.

**Rationale**: `<Virtualize>` is part of `Microsoft.AspNetCore.Components.Web` (already referenced). It handles viewport measurement, placeholder rendering, and item recycling. Messages are loaded from IndexedDB via the existing `storage.js` `getMessages()` function.

**Alternatives considered**:
- Custom IntersectionObserver JS interop: More complex, requires manual DOM measurement, no built-in placeholder support.
- Third-party Blazor virtualization library: Unnecessary dependency when built-in component exists.

### R-003: Response Compression Configuration

**Decision**: Add `builder.Services.AddResponseCompression()` with Brotli (priority) and Gzip providers. Enable for HTTPS. Configure for `application/json`, `text/html`, `application/javascript`, `text/css`, `application/wasm`.

**Rationale**: ASP.NET Core built-in response compression middleware is zero-dependency. Brotli offers ~20% better compression than gzip for text content. WASM files benefit significantly.

**Alternatives considered**:
- IIS-level compression: Requires IIS configuration changes outside the application. Application-level is more portable and testable.
- CDN compression (Cloudflare): Already used in production but doesn't help local development. Application-level compression benefits all deployments.

### R-004: Typing Indicator Caching Strategy

**Decision**: Add a `ConcurrentDictionary<string, (string DisplayName, DateTimeOffset CachedAt)>` to `ChatHub` for connection-to-display-name mapping. Populate on `RegisterDevice`. Add a `ConcurrentDictionary<long, (List<long> ParticipantUserIds, DateTimeOffset CachedAt)>` for conversation participant lists with 60-second TTL.

**Rationale**: `RegisterDevice` already queries the user and device. Caching the display name at connection time avoids the per-keystroke DB query in `TypingIndicator`. Participant list caching with TTL handles membership changes without staleness risk.

**Alternatives considered**:
- Redis cache: Overkill for single-instance deployment. ConcurrentDictionary is sufficient.
- Passing display name from client: Trust boundary violation — server should not trust client-supplied display names.

### R-005: Offline Message Queue Storage

**Decision**: Store queued messages in IndexedDB via a new `offlineQueue` object store in `storage.js`. Each entry contains: `id` (auto-increment), `conversationId`, `recipientDeviceId`, `ciphertext`, `contentType`, `status` ('pending'|'sending'|'failed'), `createdAt`. Cap at 50 entries.

**Rationale**: IndexedDB is already used for message caching. Adding another object store is trivial. The cap prevents storage exhaustion. Status tracking enables UI indicators.

**Alternatives considered**:
- localStorage: Size-limited (5-10MB), synchronous API blocks UI thread.
- In-memory only: Lost on page refresh, unacceptable for queued messages.

### R-006: Signature Format Versioning

**Decision**: Prepend a single version byte (`0x01`) to the combined hybrid signature output. Current format becomes v0 (no prefix). Verification checks first byte: if `0x01`, parse as v1; otherwise, parse as v0 (legacy). This happens in the `HybridSigner` class in `ToledoVault.Crypto`.

**Rationale**: Single version byte is the standard approach (TLS, SSH, Signal Protocol all use version prefixes). Backward compatible: v0 signatures don't start with `0x01` (they start with Ed25519 signature bytes which have different structure).

**Alternatives considered**:
- JSON envelope: Adds parsing overhead and increases signature size significantly.
- Magic bytes: More than 1 byte is wasteful when we only need ~256 versions.

### R-007: Presence Heartbeat via SignalR KeepAlive

**Decision**: Configure SignalR `KeepAliveInterval` to 30 seconds and `ClientTimeoutInterval` to 90 seconds in server `Program.cs`. When SignalR detects a dead connection (no transport-level activity for 90s), it fires `OnDisconnectedAsync` which already calls `presence.RemoveConnection()` and broadcasts offline status.

**Rationale**: SignalR's built-in keep-alive operates at the WebSocket transport level, immune to browser JS timer throttling in background tabs. The existing `OnDisconnectedAsync` handler already handles offline marking — no new code needed for the core mechanism.

**Alternatives considered**:
- Custom JS heartbeat timer: Throttled by browsers in background tabs, causing false-offline.
- Separate WebSocket connection for heartbeat: Unnecessary complexity when SignalR provides this natively.

### R-008: Batch Delivery Acknowledgment Strategy

**Decision**: In `MessagesController.BulkAcknowledgeDelivery`, group the returned `(messageId, senderDeviceId)` tuples by `senderDeviceId`, then send one `MessagesDelivered` (plural) SignalR notification per unique sender device containing the list of message IDs.

**Rationale**: Current code sends `N` individual `MessageDelivered` calls. Grouping reduces SignalR traffic from `O(N)` to `O(unique_senders)` which is typically 1-3 for a single device's pending messages.

**Alternatives considered**:
- Single notification with all IDs: Doesn't tell the sender which messages were delivered (they need per-device granularity).
- No change: Current approach works but scales poorly with message volume.

### R-009: Read Pointer Clamping

**Decision**: In `MessageRelayService.AdvanceReadPointer`, before updating the read pointer, query `MAX(SequenceNumber)` for the conversation and clamp `upToSequenceNumber` to that value.

**Rationale**: Prevents clients from setting arbitrarily high sequence numbers that could cause future messages to appear "already read" before they arrive.

**Alternatives considered**:
- Client-side validation only: Insufficient — server must enforce since clients can be tampered with.
- Reject instead of clamp: Clamping is more user-friendly — the read pointer still advances to the latest message.

### R-010: Cookie Management Without eval()

**Decision**: Replace any `eval()`-based cookie setting in JS files with direct `document.cookie = "..."` assignment. Create a dedicated `setCookie(name, value, options)` helper function in `storage.js`.

**Rationale**: `eval()` is a code injection vector. Direct `document.cookie` assignment is the standard, safe approach. A helper function centralizes cookie management.

**Alternatives considered**:
- Blazor JSInterop for cookies: Adds unnecessary round-trip for a synchronous operation.
- Third-party cookie library: Overkill for simple cookie assignment.

---

## Phase 1: Design & Contracts

*See separate artifacts: [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)*

### Constitution Re-Check (Post-Design)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero-Trust Server | PASS | Offline queue stores only encrypted ciphertext. Signature versioning is client-only. |
| II. Hybrid Cryptography | PASS | Version byte prefix preserves hybrid signature structure. |
| III. Established Libraries Only | PASS | No new crypto libraries added. |
| IV. Signal Protocol Fidelity | PASS | No protocol changes. Disappearing messages are display-layer only. |
| V. .NET Ecosystem | PASS | Built-in ASP.NET Core compression, Blazor Virtualize. No new dependencies. |
| VI. Test-First Development | PASS | Tests required for all new functionality. |
| VII. Open-Source Transparency | PASS | No changes to open-source posture. |

**Post-design gate: PASS.**
