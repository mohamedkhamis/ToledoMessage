# ToledoMessage — Bug Tracker

> **Purpose:** Central bug tracking file for all features/components.
> **Process:** See [Bug Workflow](#bug-workflow) at the bottom of this file.

---

## Open Bugs

### BUG-MS-007: Missing integration tests for media encryption round-trips (Medium)

**Date:** 2026-03-04
**Feature:** 006-fix-media-sharing / Tests
**Severity:** Medium
**Category:** Logic

**Description:**
The tasks.md specifies integration tests for media encryption round-trips (T016, T026, T032, T045, T046) in `tests/ToledoMessage.Integration.Tests/MediaEncryptionTests.cs`. These tests do not exist yet. The spec requires >90% code coverage on media send/receive/encrypt/decrypt paths (SC-005). Currently, only client-side `MediaPayloadTests` exist — no tests verify that `MediaPayload` survives the full `DoubleRatchet` encrypt/decrypt cycle.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `tests/ToledoMessage.Integration.Tests/MediaEncryptionTests.cs` | N/A | File does not exist | Should contain media encryption round-trip tests |

**Impact:** No automated verification that encrypted media payloads can be successfully decrypted. A regression in the encryption pipeline or payload serialization would go undetected.

**Fix Steps:**
1. Create `tests/ToledoMessage.Integration.Tests/MediaEncryptionTests.cs`
2. Implement tests T016, T026, T032, T045, T046 as specified in tasks.md
3. Use the existing `DoubleRatchet` session pair pattern from `DoubleRatchetTests.cs`

---

### ~~BUG-CR-001: Theme event handler memory leak — Dispose() never called on 5 components~~ FIXED 2026-03-06
Moved `Theme.OnThemeChanged -= OnThemeChanged` into `DisposeAsync()` for Chat.razor and ConversationListSidebar.razor (which had orphaned `Dispose()` methods that Blazor never called). MessageBubble.razor and Settings.razor already had `@implements IDisposable`.

---

### ~~BUG-CR-002: async void SignalR handlers missing try/catch — unhandled exceptions crash WASM~~ FIXED 2026-03-06
Added try/catch wrapper to all async void SignalR event handlers in Chat.razor (HandleMessageDelivered, HandleMessageRead, HandleTypingIndicator, HandleParticipantAdded, HandleParticipantRemoved, HandleUserOnline, HandleUserOffline, HandleReactionAdded, HandleReactionRemoved, HandleMessageDeleted).

---

### BUG-CR-003: JWT base64url decoding missing character replacement (Medium)

**Date:** 2026-03-06
**Feature:** Core / ConversationListSidebar
**Severity:** Medium
**Category:** Logic

**Description:**
`ExtractDisplayNameFromJwt` adds base64 padding but does NOT replace base64url characters (`-` → `+`, `_` → `/`). JWT payloads use base64url encoding per RFC 7515. If the JWT payload contains `-` or `_`, `Convert.FromBase64String` throws `FormatException`, caught silently, returning `"?"` as display name.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `src/ToledoMessage.Client/Components/ConversationListSidebar.razor` | 311 | `Convert.FromBase64String(payload)` | Add `payload = payload.Replace('-', '+').Replace('_', '/');` before this line |

**Impact:** Some users will see "?" as their display name in the sidebar instead of their actual name, depending on the JWT payload content.

**Fix Steps:**
1. In `ExtractDisplayNameFromJwt`, after the padding switch block (line 310), add: `payload = payload.Replace('-', '+').Replace('_', '/');`

---

### ~~BUG-CR-004: Forwarded message blob URL revoked before consumption~~ FIXED 2026-03-06
Changed `ForwardToConversation` to async, pre-fetch blob bytes before navigation, and store bytes in PendingForward. Updated `SendForwardedMessage` to accept optional byte[] parameter.

---

### BUG-CR-005: GetUserId() returns 0 instead of failing on missing JWT claims (Medium)

**Date:** 2026-03-06
**Feature:** Core / BaseApiController
**Severity:** Medium
**Category:** Security

**Description:**
`BaseApiController.GetUserId()` returns `0` when the JWT `sub` claim is missing, instead of throwing or returning an error. All `[Authorize]` endpoints call this method. While userId `0` is unlikely to match real data, it's a defense-in-depth failure — the method should fail loudly rather than silently returning an invalid ID. Additionally, `decimal.Parse(sub)` will throw unhandled `FormatException` if the claim is present but not a valid decimal.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `src/ToledoMessage/Controllers/BaseApiController.cs` | 21 | `return sub == null ? 0 : decimal.Parse(sub);` | Throw `UnauthorizedAccessException` if sub is null or unparseable |

**Impact:** If JWT is malformed or claims are stripped, API returns data for userId 0 instead of 401. Low real-world probability since [Authorize] validates the token, but violates defense-in-depth.

**Fix Steps:**
1. Replace line 21 with: `if (sub == null || !decimal.TryParse(sub, out var id)) throw new UnauthorizedAccessException("Invalid user identity claim"); return id;`

---

### BUG-CR-006: AdvanceReadPointer has no conversation participant authorization (Medium)

**Date:** 2026-03-06
**Feature:** Core / MessagesController + ChatHub
**Severity:** Medium
**Category:** Security

**Description:**
The `POST api/messages/read` endpoint and `ChatHub.AdvanceReadPointer` call `relayService.AdvanceReadPointer(userId, conversationId, sequenceNumber)` without verifying the user is a participant in the conversation. Any authenticated user can create read pointers in arbitrary conversations, polluting the database.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `src/ToledoMessage/Controllers/MessagesController.cs` | 154-170 | No participant check | Add participant verification before calling AdvanceReadPointer |
| `src/ToledoMessage/Hubs/ChatHub.cs` | 127-139 | No participant check | Add participant verification |

**Impact:** Data integrity issue. An attacker could create bogus read pointers for conversations they don't belong to.

**Fix Steps:**
1. Before calling `AdvanceReadPointer`, query `ConversationParticipants` to verify `userId` is a member of `conversationId`
2. Return 403/throw if not a participant

---

### ~~BUG-CR-007: IsUserOnline exposes online status of any user without authorization~~ FIXED 2026-03-06
Added authorization check in ChatHub.IsUserOnline - verifies caller shares a conversation with target user before revealing online status.

---

### BUG-CR-008: GetUnreadCount fallback excludes undelivered messages (Medium)

**Date:** 2026-03-06
**Feature:** Core / MessageRelayService
**Severity:** Medium
**Category:** Logic

**Description:**
When no read pointer exists (new conversation), `GetUnreadCount` fallback counts only messages where `IsDelivered = true`. But undelivered messages are also unread. This causes the unread badge to show fewer unreads than reality for new conversations.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `src/ToledoMessage/Services/MessageRelayService.cs` | 280-289 | `&& m.IsDelivered` filter | Remove `&& m.IsDelivered` — count all messages to user's devices |

**Impact:** Unread badge shows incorrect (lower) count for conversations that haven't been opened yet.

**Fix Steps:**
1. Remove `&& m.IsDelivered` from the count query in the fallback branch of `GetUnreadCount`

---

### ~~BUG-CR-009: NewConversation.razor missing IDisposable — CancellationTokenSource leaks~~ FIXED 2026-03-06
Added `@implements IDisposable` to NewConversation.razor and implemented Dispose() to cancel and dispose _debounceCts.

---

### BUG-CR-010: SignalRService _registeredDeviceId not reset on new connection (Medium)

**Date:** 2026-03-06
**Feature:** Core / SignalRService
**Severity:** Medium
**Category:** Logic

**Description:**
When `ConnectAsync` creates a new `HubConnection` (disposing the old one), `_registeredDeviceId` retains the old value. If `RegisterDeviceAsync` is called with the same device ID, it returns early (line 185) without registering the `Reconnected` handler on the new connection. After a reconnect on the new connection, the device won't be re-registered with the server.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `src/ToledoMessage.Client/Services/SignalRService.cs` | 184-185 | Guard `if (_registeredDeviceId == deviceId) return;` | Reset `_registeredDeviceId = 0` in `ConnectAsync` after creating new connection |

**Impact:** After connection rebuild (e.g., network change), device may not re-register on reconnect, causing messages to not be delivered until manual refresh.

**Fix Steps:**
1. In `ConnectAsync`, after `_hubConnection = new HubConnectionBuilder()...`, add `_registeredDeviceId = 0;`

---

### ~~BUG-CR-011: Login/Register never stores refresh token — token refresh always fails~~ FIXED 2026-03-07
Login.razor and Register.razor stored `auth.token` but never `auth.refreshToken`. AuthTokenHandler couldn't refresh expired JWTs. Also `clearAuthData` in storage.js didn't clear `auth.refreshToken`. Fixed all three.

---

### ~~BUG-CR-012: telegram-dark theme missing from PreferencesController ValidThemes~~ FIXED 2026-03-07
Added `"telegram-dark"` to the valid themes set in PreferencesController.cs. Theme was offered by ThemeService but rejected by the API.

---

## Resolved Bugs

### ~~BUG-MS-008: Sender does not display thumbnail for sent images/videos~~ FIXED 2026-03-04
Changed `ThumbnailDataUrl` from `init` to `set` in `ChatMessage`. In `SendMediaToRecipients`, after generating thumbnail, create blob URL and set on `tempMessage.ThumbnailDataUrl`.

### ~~BUG-MS-009: Receiver sees raw MediaPayload JSON instead of rendered media~~ FIXED 2026-03-04
Added defensive MediaPayload detection in the text decryption path of `DecryptEnvelopeToChatMessage`. Now uses `DecryptToBytesAsync` and checks if decrypted bytes are a MediaPayload JSON before treating as text. If detected, handles as media with proper blob URLs and inferred ContentType.

### ~~BUG-DEV-001: Stale devices accumulate on every login cycle~~ FIXED 2026-03-03
Server now deactivates existing device with the same name before creating a new one in DevicesController.cs. Prevents device accumulation across logout/login cycles. Login.razor also shows real server error body instead of generic "Device registration failed".

### ~~BUG-FS-001: Reply text formatting inconsistent across code paths~~ FIXED 2026-03-03
Extracted shared `FormatReplyText()` helper method and applied it to all 3 code paths in Chat.razor (SetReplyTo, optimistic send, SignalR incoming). All paths now consistently show "[Voice message]", "[File: name.pdf]", etc.

### ~~BUG-FS-002: WASM loading spinner uses non-existent `blazor:initialized` event~~ FIXED 2026-03-03
Replaced `document.addEventListener('blazor:initialized', ...)` with `Blazor.addEventListener('afterStarted', ...)` in App.razor. Spinner now hides when WASM finishes loading (~1-2s) instead of waiting for the 10s fallback timeout.

### ~~BUG-003: Timestamp format mismatch in IndexedDB deleteConversationMessages~~ FIXED 2026-03-02
Changed to pass ISO 8601 string instead of Unix milliseconds, and removed `.toString()` in JS comparison.

### ~~BUG-004: Misleading parameter name `olderThanTimestamp`~~ FIXED 2026-03-02
Renamed parameter to `fromTimestamp` in both MessageStoreService.cs and storage.js.

### ~~BUG-IMP-001: Missing `.message-bubble:has(.emoji-only)` CSS~~ FIXED 2026-03-02
Added transparent bubble styling for emoji-only messages.

### ~~BUG-IMP-002: IsEmojiOnly doesn't limit to 1-3 emoji~~ FIXED 2026-03-02
Updated regex to only match 1-3 emoji with 14 char safety cap.

### ~~BUG-IMP-003: Missing mobile image preview sizing~~ FIXED 2026-03-02
Added max-height, lightbox styles for mobile.

### ~~BUG-IMP-004: Missing `.btn-nav-outline` class and header button styling~~ FIXED 2026-03-02
Added btn-nav-outline class and updated header "Sign In" button.

### ~~BUG-001: app.css not using structural CSS variables from themes.css~~ FIXED 2026-03-02
Applied CSS variables to:
- `.message-bubble`: border-radius, font-size
- `.msg-textarea`: padding, border-radius
- `.chat-sidebar`: width
- `.chat-messages`: background-image

### ~~BUG-002: Settings.razor has hardcoded strings that should use ThemeLabelSet~~ FIXED 2026-03-02
Added missing ThemeLabelSet properties and updated Settings.razor.

### ~~BUG-HP-001: Hardcoded version `v1.0.0` in Home.razor~~ FIXED 2026-03-02
Changed to `v@(AppVersion.Current)`.

### ~~BUG-HP-002: Unused `@inject IHttpContextAccessor` in Home.razor~~ FIXED 2026-03-02
Removed unused injection.

### ~~BUG-HP-003: Duplicate floating version display~~ FIXED 2026-03-02
Removed `app-version-floating` div from App.razor.

### ~~BUG-HP-004: Redundant `@using` directive in Home.razor~~ FIXED 2026-03-02
Removed — already in `_Imports.razor`.

### ~~BUG-HP-005: Dead CSS `.app-version-floating`~~ FIXED 2026-03-02
Removed orphaned CSS from app.css.

### ~~BUG-HP-006: "API Version" label incorrect~~ FIXED 2026-03-02
Changed to "App Version".

### ~~BUG-MS-001: HandleMessageDeleted uses direct URL.revokeObjectURL~~ FIXED 2026-03-04
Changed to use `mediaHelpers.revokeObjectUrl` for consistency with other code paths.

### ~~BUG-MS-002: HandleMessageDeleted does not revoke ThumbnailDataUrl blob~~ FIXED 2026-03-04
Added ThumbnailDataUrl revocation in HandleMessageDeleted to prevent memory leaks.

### ~~BUG-MS-003: LoadCachedMessages does not set MimeType on ChatMessage~~ FIXED 2026-03-04
Added `MimeType = stored.MimeType` mapping in LoadCachedMessages.

### ~~BUG-MS-004: ForwardToConversation loses MimeType metadata~~ FIXED 2026-03-04
Added MimeType to PendingForward tuple and all related code paths.

### ~~BUG-MS-005: generateThumbnail returns base64 string but C# expects byte[]~~ FIXED 2026-03-04
Changed to use `InvokeAsync<string>` instead of `InvokeAsync<byte[]>` to match JS return type.

### ~~BUG-MS-006: PendingForward not cleared in finally block~~ FIXED 2026-03-04
Captured and cleared PendingForward at the start of OnInitializedAsync before any throwing code.

---

## Bug Workflow

### How to Report a Bug

Every bug entry follows this template:

```markdown
### BUG-XXX: Short descriptive title (Severity)

**Date:** YYYY-MM-DD
**Feature:** Feature or component name
**Severity:** Critical / High / Medium / Low
**Category:** CSS / Theme Labels / Logic / UI / Performance / Security

**Description:**
One paragraph explaining what's wrong and why it matters.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `path/to/file` | 123 | What it says now | What it should say |

**Impact:** What the user sees or what breaks.

**Fix Steps:**
1. Step-by-step instructions to fix
2. Specific enough that any developer/AI can follow
3. Include code snippets if helpful
```

### Severity Levels

| Level | Definition | Examples |
|-------|-----------|----------|
| **Critical** | Feature broken or not working at all | CSS variables defined but not used, crash on load |
| **High** | Feature partially broken, bad UX | Wrong data displayed, layout broken on mobile |
| **Medium** | Feature works but inconsistent or incomplete | Some labels missed, minor style mismatch |
| **Low** | Cosmetic or cleanup issue | Dead code, redundant imports, typos |

### Bug Lifecycle

```
Open → In Progress → Fixed → Verified
                  → Won't Fix (with reason)
```

1. **Open:** Bug reported with full details in this file under `## Open Bugs`
2. **In Progress:** Developer/AI is actively fixing it
3. **Fixed:** Move to `## Resolved Bugs` with fix date and one-line summary
4. **Verified:** Confirmed working after deploy (can be removed from Resolved after 30 days)

### Rules

- **Bug IDs are permanent** — never reuse a BUG-XXX number
- **Numbering:** Use `BUG-001`, `BUG-002`, etc. for global bugs
- **Feature-specific prefix:** Use `BUG-HP-XXX` for Home Page, `BUG-TH-XXX` for Theme, etc. (optional)
- **One bug per entry** — don't combine multiple issues
- **Always include file paths and line numbers** — so the fix can be applied without searching
- **Always include "Fix Steps"** — specific enough for another developer or AI agent to execute
- **When fixed:** Move the entry from "Open" to "Resolved" with strikethrough title and fix date
- **Delete `BUG-REPORT-*.md` files** — all bugs go in this central `BUGS.md` file
