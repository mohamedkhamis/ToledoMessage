# Quickstart: ToledoMessage SaaS Enhancement Plan (v2.0)

**Feature**: `008-saas-enhancements`
**Date**: 2026-03-09

## Purpose

Step-by-step guide for an implementer to understand the enhancement scope, set up their environment, and begin implementation in the correct order.

---

## Prerequisites

### Environment

- **.NET SDK**: 10.0 or later (currently using 11.0.100-preview)
- **SQL Server**: 2022 (local or remote)
- **Node.js**: Not required (Blazor WASM, no npm build step)
- **IDE**: Visual Studio 2022+ or JetBrains Rider
- **Browser**: Chrome/Edge (for testing with DevTools)
- **IIS**: For local deployment testing (optional — `deploy-iis.ps1`)

### Repository Setup

```bash
git checkout 008-saas-enhancements
dotnet restore
dotnet build
dotnet test   # Verify all 231 tests pass before starting
```

---

## Implementation Order

The enhancements are organized into 6 pillars. The recommended implementation order prioritizes dependencies and risk:

### Phase A: Security Hardening (P1 — Do First)

**Why first**: Security fixes have no dependencies and reduce risk for all subsequent work.

| Order | Task | Files | Estimated Complexity |
|-------|------|-------|---------------------|
| A1 | SignalR rate limiting (FR-001, FR-002) | `ChatHub.cs` | Low — 6 lines added |
| A2 | Register rate limit (FR-003) | `RateLimitMiddleware.cs` | Low — 1 rule added |
| A3 | Unread count auth check (FR-004) | `MessagesController.cs` | Low — 3 lines added |
| A4 | DeleteForEveryone auth check (FR-005) | `ChatHub.cs` | Low — 4 lines added |
| A5 | Read pointer clamping (FR-006) | `MessageRelayService.cs` | Low — 3 lines added |
| A6 | Error message sanitization (FR-007) | `Login.razor`, `Register.razor` | Low — replace error strings |
| A7 | eval() elimination (FR-008) | `storage.js`, `App.razor` | Low — replace eval with direct cookie |
| A8 | Link preview URL length (FR-009) | `LinkPreviewController.cs` | Low — 1 line added |
| A9 | Storage encryption check (FR-010) | `LocalStorageService.cs` | Low — add assertion |

### Phase B: Performance (P1 — Do Second)

**Why second**: Performance improvements benefit all users immediately. Some depend on Phase A (rate limiting already done).

| Order | Task | Files | Estimated Complexity |
|-------|------|-------|---------------------|
| B1 | Response compression (FR-011) | `Program.cs` | Low — middleware config |
| B2 | Typing indicator caching (FR-013) | `ChatHub.cs` | Medium — 2 new caches |
| B3 | Batch delivery acks (FR-014) | `MessagesController.cs`, `SignalRService.cs` | Medium — GroupBy + new event |
| B4 | User search optimization (FR-015) | `UsersController.cs`, `UserConfiguration.cs` | Low — EF.Functions.Like + index |
| B5 | Client debounce (FR-016) | `EmojiPicker.razor`, `ConversationListSidebar.razor` | Low — Timer debounce |
| B6 | Background jitter (FR-017) | `MessageCleanupHostedService.cs` | Low — Random offset |
| B7 | Pre-key cleanup (FR-018) | `MessageCleanupHostedService.cs` | Low — additional query |
| B8 | Message virtualization (FR-012) | `Chat.razor`, `storage.js` | High — Virtualize component + IndexedDB pagination |

### Phase C: UI/Accessibility (P2)

**Why third**: UI changes are visible and testable. Some depend on Phase B (virtualization affects message rendering).

| Order | Task | Files | Estimated Complexity |
|-------|------|-------|---------------------|
| C1 | Semantic sidebar elements (FR-019) | `ConversationListSidebar.razor` | Medium — HTML restructure |
| C2 | Emoji keyboard navigation (FR-020) | `EmojiPicker.razor` | Medium — arrow key handler |
| C3 | Theme card responsive grid (FR-021) | `app.css` or `Settings.razor` | Low — CSS auto-fit |
| C4 | Responsive video (FR-022) | `MessageBubble.razor`, `app.css` | Low — max-width CSS |
| C5 | Date separators (FR-023) | `Chat.razor`, `MessageBubble.razor` | Medium — date grouping logic |
| C6 | Localization completeness (FR-024) | `SharedResource.resx`, `SharedResource.ar.resx` | Low — add missing keys |
| C7 | ImageLightbox mobile fix | `ImageLightbox.razor`, `app.css` | Low — viewport scaling |

### Phase D: UX Improvements (P2)

**Why fourth**: UX features depend on SignalR infrastructure (Phase A/B).

| Order | Task | Files | Estimated Complexity |
|-------|------|-------|---------------------|
| D1 | Offline indicator (FR-026) | `ChatLayout.razor`, `SignalRService.cs` | Medium — connection state UI |
| D2 | Retry button on load failure (FR-025) | `Chat.razor` | Low — error state + button |
| D3 | File selection preservation (FR-027) | `MessageInput.razor` | Low — don't clear on error |
| D4 | Voice recording limit (FR-028) | `VoiceRecorder.razor`, `voice-recorder.js` | Medium — timer + auto-stop |
| D5 | Rate limit warning UI (FR-002a) | `MessageInput.razor` | Medium — inline warning + countdown |

### Phase E: Encryption & Disappearing Messages (P2)

**Why fifth**: Signature versioning and message expiry are independent of UI changes.

| Order | Task | Files | Estimated Complexity |
|-------|------|-------|---------------------|
| E1 | Signature format versioning (FR-030) | `HybridSigner.cs` (Crypto project) | Medium — version byte + backward compat |
| E2 | Wire MessageExpiryService (FR-031) | `MessageExpiryService.cs`, `Chat.razor`, `storage.js` | High — full lifecycle wiring |

### Phase F: Functionality (P2-P3)

**Why last**: New features have the most code and depend on all infrastructure above.

| Order | Task | Files | Estimated Complexity |
|-------|------|-------|---------------------|
| F1 | Offline message queue (FR-032) | `storage.js`, `SignalRService.cs`, `MessageInput.razor`, `Chat.razor` | High — full queue system |
| F2 | In-conversation search (FR-029) | `Chat.razor`, `storage.js` | High — search UI + IndexedDB query |
| F3 | Group multi-admin (FR-033) | `ConversationsController.cs`, `ChatHub.cs`, model changes | High — admin transfer + concurrency |
| F4 | Presence heartbeat timeout (FR-034) | `Program.cs` | Low — SignalR config only |

### Phase G: Database Migration

**When**: After all server-side model changes are complete (after Phase F).

| Order | Task | Files | Estimated Complexity |
|-------|------|-------|---------------------|
| G1 | EF Core migration | `Data/Migrations/` | Low — `dotnet ef migrations add V2Enhancements` |

---

## Testing Strategy

### Unit Tests Required

| Area | Test Focus | Project |
|------|-----------|---------|
| Rate limiting in hub | Verify SendMessage/TypingIndicator are limited | `ToledoMessage.Server.Tests` |
| Auth checks | Unread count, DeleteForEveryone participant validation | `ToledoMessage.Server.Tests` |
| Read pointer clamping | Verify clamping to max sequence | `ToledoMessage.Server.Tests` |
| Signature versioning | v0 backward compat, v1 round-trip | `ToledoMessage.Crypto.Tests` |
| Batch ack grouping | Verify GroupBy logic | `ToledoMessage.Server.Tests` |
| Participant cache | TTL expiry, invalidation | `ToledoMessage.Server.Tests` |

### Manual Tests Required

| Area | Test Steps |
|------|-----------|
| Compression | Open DevTools Network → verify `Content-Encoding: br` headers |
| Virtual scrolling | Open conversation with 100+ messages → scroll smoothly |
| Offline queue | Disconnect network → send message → reconnect → message delivered |
| Voice limit | Record voice → verify auto-stop at 5 min |
| Accessibility | Navigate entire app with keyboard only |
| Responsive | Test on mobile viewport (375px width) |
| Disappearing messages | Send with timer → verify removal after timer |

---

## Key Files Reference

| Purpose | File Path |
|---------|-----------|
| Server entry point | `src/ToledoMessage/Program.cs` |
| SignalR hub | `src/ToledoMessage/Hubs/ChatHub.cs` |
| Rate limit service | `src/ToledoMessage/Services/RateLimitService.cs` |
| Rate limit middleware | `src/ToledoMessage/Middleware/RateLimitMiddleware.cs` |
| Message relay | `src/ToledoMessage/Services/MessageRelayService.cs` |
| Presence service | `src/ToledoMessage/Services/PresenceService.cs` |
| Cleanup service | `src/ToledoMessage/Services/MessageCleanupHostedService.cs` |
| Main chat page | `src/ToledoMessage.Client/Pages/Chat.razor` |
| Message input | `src/ToledoMessage.Client/Components/MessageInput.razor` |
| SignalR client | `src/ToledoMessage.Client/Services/SignalRService.cs` |
| Message expiry | `src/ToledoMessage.Client/Services/MessageExpiryService.cs` |
| Local storage | `src/ToledoMessage.Client/Services/LocalStorageService.cs` |
| IndexedDB/JS | `src/ToledoMessage.Client/wwwroot/storage.js` |
| Voice recorder JS | `src/ToledoMessage.Client/wwwroot/voice-recorder.js` |
| CSS | `src/ToledoMessage/wwwroot/app.css` |
| Localization EN | `src/ToledoMessage.Shared/SharedResource.resx` |
| Localization AR | `src/ToledoMessage.Shared/SharedResource.ar.resx` |
| Crypto signing | `src/ToledoMessage.Crypto/` (HybridSigner) |
