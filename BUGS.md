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
