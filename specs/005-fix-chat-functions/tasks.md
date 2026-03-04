# Tasks: Fix All Chat Functions

**Input**: Design documents from `/specs/005-fix-chat-functions/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md
**Branch**: `005-fix-chat-functions`
**Tests**: No new test tasks (existing 215 tests must continue passing)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. All fixes are client-side only (no server changes).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Fix foundational JavaScript bugs that affect multiple user stories. These are shared infrastructure fixes that must land first.

- [X] T001 Fix `storeMessages` early-return bug in `src/ToledoMessage.Client/wwwroot/storage.js` (line 104-124)

  **Context**: The `storeMessages` function has a critical logic error. When `msgs` is empty/null, it immediately enters a `return new Promise(...)` block that references `db` — but `db` is declared on line 114 (`const db = await this.open()`), AFTER the early return. This means:
  - If called with an empty array, `db` is `undefined` → `db.transaction(...)` throws `TypeError: Cannot read properties of undefined`
  - The function should simply return early (resolve immediately) for empty input

  **Current broken code (lines 104-124)**:
  ```javascript
  storeMessages: async function (msgs) {
      if (!msgs || msgs.length === 0) return new Promise((resolve, reject) => {
          const tx = db.transaction('messages', 'readwrite');  // BUG: db is undefined here
          const store = tx.objectStore('messages');
          for (const msg of msgs) { store.put(msg); }
          tx.oncomplete = () => resolve();
          tx.onerror = (e) => reject(e.target.error);
      });
      const db = await this.open();
      return new Promise((resolve, reject) => {
          const tx = db.transaction('messages', 'readwrite');
          const store = tx.objectStore('messages');
          for (const msg of msgs) { store.put(msg); }
          tx.oncomplete = () => resolve();
          tx.onerror = (e) => reject(e.target.error);
      });
  },
  ```

  **Fix**: Replace the early-return block with a simple `return;` statement. The function should be:
  ```javascript
  storeMessages: async function (msgs) {
      if (!msgs || msgs.length === 0) return;
      const db = await this.open();
      return new Promise((resolve, reject) => {
          const tx = db.transaction('messages', 'readwrite');
          const store = tx.objectStore('messages');
          for (const msg of msgs) { store.put(msg); }
          tx.oncomplete = () => resolve();
          tx.onerror = (e) => reject(e.target.error);
      });
  },
  ```

  **Acceptance**: `storeMessages([])` returns without error. `storeMessages([msg1, msg2])` persists both messages to IndexedDB. Remove the `// ReSharper disable` comment on line 103 since the variable-before-declaration issue is eliminated.

- [X] T002 [P] Add audio helper functions to `src/ToledoMessage.Client/wwwroot/media-helpers.js`

  **Context**: `MessageBubble.razor` currently uses `Js.InvokeVoidAsync("eval", ...)` for all audio DOM operations (play, pause, get currentTime, get duration). This is fragile and a security concern. We need proper named JS functions in `media-helpers.js` that the Blazor component can call instead.

  **Current eval usage in MessageBubble.razor (lines 199, 204, 221, 237)**:
  ```javascript
  // Play:  eval(`document.querySelector('[data-msg-id="${MessageId}"] audio').play()`)
  // Pause: eval(`document.querySelector('[data-msg-id="${MessageId}"] audio').pause()`)
  // Time:  eval(`document.querySelector('[data-msg-id="${MessageId}"] audio')?.currentTime ?? 0`)
  // Duration: eval(`(function(){ var a=document.querySelector('[data-msg-id="${MessageId}"] audio'); return a && isFinite(a.duration) ? a.duration : 0; })()`)
  ```

  **Add these functions** to `window.mediaHelpers` object in `media-helpers.js` (after the existing `fetchBlobAsBytes` function, before `registerLongPress`):
  ```javascript
  playAudio: function (messageId) {
      var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
      if (audio) return audio.play();
  },
  pauseAudio: function (messageId) {
      var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
      if (audio) audio.pause();
  },
  getAudioCurrentTime: function (messageId) {
      var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
      return audio ? audio.currentTime : 0;
  },
  getAudioDuration: function (messageId) {
      var audio = document.querySelector('[data-msg-id="' + messageId + '"] audio');
      return (audio && isFinite(audio.duration)) ? audio.duration : 0;
  },
  ```

  **Acceptance**: Functions exist in `window.mediaHelpers`. Can be called from Blazor via `Js.InvokeVoidAsync("mediaHelpers.playAudio", messageId)`. T002 is a prerequisite for T012 (which updates the Blazor component to use these).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No additional foundational work needed. Phase 1 covers the shared JS fixes. User stories can proceed after Phase 1.

**Checkpoint**: Phase 1 complete → user story implementation can begin.

---

## Phase 3: User Story 1 — Send and Receive Encrypted Media (Priority: P1) 🎯 MVP

**Goal**: Ensure encrypted media (images, video, audio, files) sent from one user is correctly received and rendered by the recipient. Fix the MediaBytes memory leak that causes browser crashes on media-heavy conversations.

**Independent Test**: Two browser windows, two users. User A sends a JPEG image. User B sees the image rendered correctly in the chat bubble — not garbled text or a broken icon.

**Key Finding from Research (R-001)**: Blazor WASM `byte[]` is already marshalled as `Uint8Array` to JavaScript since .NET 6. The existing `createObjectUrl` in `media-helpers.js` is correct. The bug is NOT in JS interop — it's in `DecryptEnvelopeToChatMessage` keeping `MediaBytes` in memory forever and potential IndexedDB persistence failures from T001.

### Implementation for User Story 1

- [X] T003 [US1] Null out `MediaBytes` after blob URL creation in `DecryptEnvelopeToChatMessage` in `src/ToledoMessage.Client/Pages/Chat.razor` (line 1035)

  **Context**: When a media message is decrypted, `DecryptEnvelopeToChatMessage` (line 1000) stores the raw decrypted bytes in `MediaBytes = bytes` (line 1035). These bytes stay in memory for the lifetime of the `ChatMessage` object, even though a blob URL has already been created from them. For a conversation with 20+ images, this causes unbounded memory growth → browser tab crash.

  **Current code (lines 1020-1042)**:
  ```csharp
  if (envelope.ContentType is ContentType.Image or ContentType.Audio or ContentType.Video or ContentType.File)
  {
      var bytes = await Crypto.DecryptToBytesAsync(
          envelope.SenderDeviceId, envelope.Ciphertext, envelope.MessageType);
      var mimeType = envelope.MimeType ?? GetDefaultMimeType(envelope.ContentType);
      var blobUrl = await CreateMediaBlobUrl(mimeType, bytes);
      return new ChatMessage
      {
          // ...
          MediaDataUrl = blobUrl,
          MediaBytes = bytes,  // ← BUG: keeps raw bytes in memory after blob URL created
          // ...
      };
  }
  ```

  **Fix**: Set `MediaBytes = null` instead of `MediaBytes = bytes`. The blob URL (`MediaDataUrl`) is sufficient for rendering. The raw bytes are no longer needed after the blob URL is created.

  ```csharp
  return new ChatMessage
  {
      // ...
      MediaDataUrl = blobUrl,
      MediaBytes = null,  // blob URL is sufficient; release bytes to reduce memory pressure
      // ...
  };
  ```

  **Important**: Check if `MediaBytes` is used anywhere else after this point. It IS used in `SendForwardedMessage` (line 1576) to re-encrypt forwarded media — but that path uses `fetchBlobAsBytes` from the blob URL instead, so `MediaBytes` is not needed there. Also check `HandleSend` — when the *sender* creates a temp message (line 837-area), `MediaBytes` is set from the input; that's fine because the sender needs bytes for encryption. The fix here is only for the *receiver* path (`DecryptEnvelopeToChatMessage`).

  **Also fix the same pattern in `SendForwardedMessage`** (line 1603): After `CreateMediaBlobUrl(mimeType, mediaBytes)` returns, null out `MediaBytes` on the temp message:
  ```csharp
  var tempMessage = new ChatMessage
  {
      // ...
      MediaDataUrl = await CreateMediaBlobUrl(mimeType, mediaBytes),
      MediaBytes = null,  // was: mediaBytes — blob URL is sufficient
      // ...
  };
  ```

  **Acceptance**: Open DevTools → Memory tab. Load a conversation with 20+ images. Memory should not grow unboundedly. Each `ChatMessage` object should have `MediaBytes = null` after rendering.

- [X] T004 [US1] Verify media send flow handles `MediaBytes = null` correctly in `src/ToledoMessage.Client/Pages/Chat.razor`

  **Context**: After T003, received messages have `MediaBytes = null`. Verify that no code path in Chat.razor reads `MediaBytes` on a received message after blob URL creation.

  **Check these code paths**:
  1. `SendForwardedMessage` (line 1566): Uses `fetchBlobAsBytes(mediaDataUrl)` to get bytes from blob URL — does NOT read `MediaBytes`. ✅ Safe.
  2. `PersistMessageAsync` / `StoreMessageToIndexedDB`: Check if it reads `MediaBytes` to persist to IndexedDB. If it does, it needs to fetch from blob URL instead, or persist before nulling bytes.
  3. `HandleSend` sender path (line 822-area): Sender creates temp message with `MediaBytes` from the input file. Sender needs bytes for `SendMediaToRecipients`. After send completes, null out `MediaBytes` on the temp message.

  **Action**: Read `PersistMessageAsync` and `HandleSend` carefully. If `PersistMessageAsync` reads `MediaBytes` to convert to base64 for IndexedDB storage, then the fix in T003 needs adjustment: persist FIRST, then null out bytes. Make this fix if needed.

  **For the sender path in HandleSend**: After `SendMediaToRecipients` and `PersistMessageAsync` both complete (around line 860-870), add:
  ```csharp
  tempMessage.MediaBytes = null; // Release after encryption and persistence
  ```

  **Acceptance**: All media types (image, video, audio, file) can be sent, received, and displayed. No `NullReferenceException` from code trying to read null `MediaBytes`. IndexedDB persistence still works correctly.

- [X] T005 [US1] Run full test suite to verify no regressions from Phase 1 + US1 changes

  **Context**: 215 existing tests (142 server + 65 crypto + 8 integration) must continue passing.

  **Command**: `dotnet test ToledoMessage.slnx` from repo root.

  **Acceptance**: All 215+ tests pass. Zero failures, zero errors.

**Checkpoint**: Media sending/receiving should now work correctly. Memory leak is fixed. Test manually with image, video, audio, and file types between two browser windows.

---

## Phase 4: User Story 2 — Clear Chat History (Priority: P2)

**Goal**: Fix ClearChat to properly handle errors (notify user on failure), and only remove reactions for deleted messages (not all reactions).

**Independent Test**: Open a conversation with 50 messages (some with reactions). Clear "last hour". Verify: only recent messages removed, reactions on older messages preserved, user notified if server clear fails.

### Implementation for User Story 2

- [X] T006 [US2] Fix `_messageReactions.Clear()` to only remove reactions for deleted messages in `ClearChat` method in `src/ToledoMessage.Client/Pages/Chat.razor` (line 1347)

  **Context**: `ClearChat` (line 1321) removes messages matching the time threshold, but then calls `_messageReactions.Clear()` (line 1347) which removes ALL reactions — including reactions for messages that were NOT deleted.

  **Current code (lines 1345-1347)**:
  ```csharp
  // Remove from local UI
  _messages.RemoveAll(m => m.Timestamp >= threshold);
  _messageReactions.Clear();  // BUG: removes ALL reactions, not just for deleted messages
  ```

  **Fix**: Collect the IDs of messages being deleted, then remove only their reactions:
  ```csharp
  // Collect IDs of messages to be removed
  var deletedIds = _messages
      .Where(m => m.Timestamp >= threshold)
      .Select(m => m.MessageId)
      .ToHashSet();

  // Remove from local UI
  _messages.RemoveAll(m => m.Timestamp >= threshold);

  // Remove only reactions for deleted messages
  foreach (var id in deletedIds)
      _messageReactions.Remove(id);
  ```

  **Acceptance**: After clearing "last hour" messages, reactions on older messages remain visible. After clearing "all messages", all reactions are gone.

- [X] T007 [US2] Add error notification for failed ClearChat operations in `src/ToledoMessage.Client/Pages/Chat.razor` (lines 1349-1361)

  **Context**: Both the IndexedDB clear (line 1350) and the SignalR server clear (line 1357) catch and silently ignore all exceptions. The user has no idea if the clear actually worked. If the server clear fails, messages will reappear on next load.

  **Current code (lines 1349-1361)**:
  ```csharp
  // Clear from IndexedDB (delete messages within the time range)
  try { await MessageStore.DeleteConversationMessagesAsync(...); }
  catch { /* ignored */ }

  // Clear from server
  try { await SignalR.ClearMessagesAsync(ConversationId, threshold, cutoff); }
  catch { /* ignored */ }
  ```

  **Fix**: Show an error message to the user if either operation fails. Use the existing `_errorMessage` field (line 296) which is already rendered in the UI (line 110-112 as `<div class="alert alert-error">`).

  ```csharp
  // Clear from IndexedDB
  try { await MessageStore.DeleteConversationMessagesAsync(ConversationId.ToString(CultureInfo.InvariantCulture), threshold); }
  catch (Exception ex)
  {
      _errorMessage = $"Failed to clear local messages: {ex.Message}";
  }

  // Clear from server
  try { await SignalR.ClearMessagesAsync(ConversationId, threshold, cutoff); }
  catch (Exception ex)
  {
      _errorMessage = $"Failed to clear messages from server: {ex.Message}";
  }
  ```

  **Design decision**: The local UI removal (line 1346) happens BEFORE the IndexedDB/server calls. This means messages disappear from the UI immediately for responsiveness. If the server call fails, the user sees an error but messages are already gone from the UI. On page refresh, server-side messages will reappear — which is the correct behavior (server is the source of truth). The error message tells the user something went wrong.

  **Acceptance**: Disconnect from internet → try ClearChat → error message appears. Reconnect → messages reappear on refresh (server source of truth).

**Checkpoint**: ClearChat now correctly preserves reactions for non-deleted messages and notifies the user on failure.

---

## Phase 5: User Story 3 — Context Menu Actions (Priority: P2)

**Goal**: Fix reply context not being cleared when a replied-to message is deleted. Fix silent forward failure.

**Independent Test**: Start replying to a message → delete that message → reply preview should auto-clear.

### Implementation for User Story 3

- [X] T008 [US3] Clear reply context when replied-to message is deleted in `DeleteForMe` method in `src/ToledoMessage.Client/Pages/Chat.razor` (line 1245)

  **Context**: When a user is composing a reply (reply preview shown at bottom of chat) and then deletes the message they're replying to via the context menu, the reply preview remains — pointing to a now-deleted message. Sending that reply would reference a non-existent message.

  **Relevant state fields (from Chat.razor)**:
  - `_replyingTo` (line 311): `ChatMessage?` — the message being replied to
  - `_replyContext` (line 1164): `MessageInput.ReplyContextModel?` — the UI model for the reply preview
  - `CancelReply()` method (line 1194): Sets both to null

  **Current `DeleteForMe` code (lines 1245-1266)**:
  ```csharp
  private async Task DeleteForMe()
  {
      if (_contextMenuMessage is not null)
      {
          // Revoke blob URL...
          _messages.Remove(_contextMenuMessage);
          _messageReactions.Remove(_contextMenuMessage.MessageId);
          // IndexedDB update...
      }
      CloseContextMenu();
  }
  ```

  **Fix**: After removing the message, check if `_replyingTo` references the deleted message. If so, clear the reply context:
  ```csharp
  private async Task DeleteForMe()
  {
      if (_contextMenuMessage is not null)
      {
          // Revoke blob URL...
          _messages.Remove(_contextMenuMessage);
          _messageReactions.Remove(_contextMenuMessage.MessageId);

          // Clear reply context if replying to the deleted message
          if (_replyingTo?.MessageId == _contextMenuMessage.MessageId)
              CancelReply();

          // IndexedDB update...
      }
      CloseContextMenu();
  }
  ```

  **Also fix `HandleMessageDeleted`** (line 1285): This handles messages deleted by the other user (via "Delete for everyone"). Same pattern — if the deleted message is the one being replied to, clear the reply:
  ```csharp
  // Inside the InvokeAsync lambda, after _messages.Remove(msg):
  if (_replyingTo?.MessageId == messageId)
      CancelReply();
  ```

  **Acceptance**: Start replying to message → delete that message (via context menu or by receiving a "Delete for everyone") → reply preview disappears.

- [X] T009 [US3] Notify user when media forward fails instead of silent fallback in `SendForwardedMessage` in `src/ToledoMessage.Client/Pages/Chat.razor` (line 1578)

  **Context**: When forwarding a media message, `SendForwardedMessage` (line 1566) tries to fetch the media bytes from the blob URL. If the blob URL has expired or been revoked, the catch block on line 1578 silently swallows the error and falls through to the text-only forward path (line 1617). The user thinks they forwarded the image, but only text was sent.

  **Current code (lines 1573-1581)**:
  ```csharp
  byte[]? mediaBytes = null;
  try
  {
      mediaBytes = await Js.InvokeAsync<byte[]>("mediaHelpers.fetchBlobAsBytes", mediaDataUrl);
  }
  catch
  {
      // If blob URL is no longer valid, fall back to text-only forward
  }
  ```

  **Fix**: Show an error message and abort the forward. Do NOT silently fall back to text-only:
  ```csharp
  byte[]? mediaBytes = null;
  try
  {
      mediaBytes = await Js.InvokeAsync<byte[]>("mediaHelpers.fetchBlobAsBytes", mediaDataUrl);
  }
  catch
  {
      _errorMessage = "Could not forward media — the file is no longer available. Try re-sending.";
      return;
  }
  ```

  **Additionally**, after the `if (mediaBytes is not null)` block (line 1583), handle the case where `fetchBlobAsBytes` returned successfully but bytes are empty:
  ```csharp
  if (mediaBytes is null || mediaBytes.Length == 0)
  {
      _errorMessage = "Could not forward media — the file is no longer available. Try re-sending.";
      return;
  }
  ```

  **Acceptance**: Forward a media message after the blob URL has been revoked → user sees error message "Could not forward media". No silent fallback to text-only.

**Checkpoint**: Context menu actions now properly handle reply context and forward failures.

---

## Phase 6: User Story 4 — Audio Playback in Chat (Priority: P3)

**Goal**: Replace all `eval()` calls in audio playback with proper JS helper functions from T002.

**Independent Test**: Receive a voice message, tap play → audio plays with progress indicator. Tap pause → audio stops. Tap play again → resumes.

**Dependency**: Requires T002 (audio helper functions in media-helpers.js) to be complete.

### Implementation for User Story 4

- [X] T010 [US4] Replace `eval()` calls with `mediaHelpers` functions in `ToggleAudioPlayback` in `src/ToledoMessage.Client/Components/MessageBubble.razor` (lines 193-209)

  **Context**: The `ToggleAudioPlayback` method uses `eval()` to find and control the `<audio>` element in the DOM. This is fragile (breaks if the DOM selector changes), a security concern (eval is flagged by CSP), and unnecessarily complex.

  **Current code (lines 193-209)**:
  ```csharp
  private async Task ToggleAudioPlayback()
  {
      try
      {
          if (_isAudioPlaying)
          {
              await Js.InvokeVoidAsync("eval", $"document.querySelector('[data-msg-id=\"{MessageId}\"] audio').pause()");
              _isAudioPlaying = false;
          }
          else
          {
              await Js.InvokeVoidAsync("eval", $"document.querySelector('[data-msg-id=\"{MessageId}\"] audio').play()");
              _isAudioPlaying = true;
          }
      }
      catch { /* ignored */ }
  }
  ```

  **Fix**: Replace with calls to the helper functions added in T002:
  ```csharp
  private async Task ToggleAudioPlayback()
  {
      try
      {
          if (_isAudioPlaying)
          {
              await Js.InvokeVoidAsync("mediaHelpers.pauseAudio", MessageId);
              _isAudioPlaying = false;
          }
          else
          {
              await Js.InvokeVoidAsync("mediaHelpers.playAudio", MessageId);
              _isAudioPlaying = true;
          }
      }
      catch { /* ignored */ }
  }
  ```

  **Acceptance**: Audio play/pause works identically to before but without eval(). Verify in browser console: no eval() calls when clicking play/pause.

- [X] T011 [US4] Replace `eval()` calls with `mediaHelpers` functions in `UpdateAudioTimeAsync` and `LoadAudioDurationAsync` in `src/ToledoMessage.Client/Components/MessageBubble.razor` (lines 217-242)

  **Context**: Same eval() problem as T010, but for getting the audio current time and duration.

  **Current code (lines 217-242)**:
  ```csharp
  private async Task UpdateAudioTimeAsync()
  {
      try
      {
          var time = await Js.InvokeAsync<double>("eval",
              $"document.querySelector('[data-msg-id=\"{MessageId}\"] audio')?.currentTime ?? 0");
          _audioCurrentTime = time;
          await InvokeAsync(StateHasChanged);
      }
      catch { /* ignored */ }
  }

  private async Task LoadAudioDurationAsync()
  {
      try
      {
          var dur = await Js.InvokeAsync<double>("eval",
              $"(function(){{ var a=document.querySelector('[data-msg-id=\"{MessageId}\"] audio'); return a && isFinite(a.duration) ? a.duration : 0; }})()");
          _audioDuration = dur;
          await InvokeAsync(StateHasChanged);
      }
      catch { /* ignored */ }
  }
  ```

  **Fix**: Replace with calls to helper functions:
  ```csharp
  private async Task UpdateAudioTimeAsync()
  {
      try
      {
          var time = await Js.InvokeAsync<double>("mediaHelpers.getAudioCurrentTime", MessageId);
          _audioCurrentTime = time;
          await InvokeAsync(StateHasChanged);
      }
      catch { /* ignored */ }
  }

  private async Task LoadAudioDurationAsync()
  {
      try
      {
          var dur = await Js.InvokeAsync<double>("mediaHelpers.getAudioDuration", MessageId);
          _audioDuration = dur;
          await InvokeAsync(StateHasChanged);
      }
      catch { /* ignored */ }
  }
  ```

  **Acceptance**: Audio time counter updates in real-time during playback. Duration shows correctly after metadata loads. No eval() calls in browser console.

**Checkpoint**: All audio playback functionality works without eval(). Voice messages play, pause, and show correct time/duration.

---

## Phase 7: User Story 5 — Memory Efficiency for Media Messages (Priority: P3)

**Goal**: Already addressed by T003 and T004. This phase is a verification step.

**Note**: The core memory fix (`MediaBytes = null` after blob URL creation) is implemented in T003/T004 as part of User Story 1. This phase exists only for explicit verification.

- [ ] T012 [US5] Verify memory efficiency fix from T003/T004 with manual testing

  **Context**: T003 nulled out `MediaBytes` after blob URL creation in `DecryptEnvelopeToChatMessage`. T004 verified no code paths break. This task is a manual verification.

  **Steps**:
  1. Open two browser windows, log in as two users
  2. Send 20+ images in a conversation
  3. Open DevTools → Memory tab on the receiver's browser
  4. Take a heap snapshot
  5. Search for `byte[]` or large `Uint8Array` allocations
  6. Verify no large byte arrays are retained per-message
  7. Compare memory usage to what it would be without the fix (estimate: ~15 MB for 20 images at 500KB each → should be near 0 with fix since blob URLs are independent of JS heap)

  **Acceptance**: Browser memory usage stays reasonable (<50 MB) for a conversation with 20+ images. No `ChatMessage` object retains a non-null `MediaBytes` reference after rendering.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all user stories.

- [X] T013 Run full test suite to verify no regressions across all changes

  **Command**: `dotnet test ToledoMessage.slnx` from repo root.
  **Acceptance**: All 215+ tests pass. Zero failures, zero errors.

- [ ] T014 Run quickstart.md manual testing checklist against all user stories

  **Context**: Follow the checklist in `specs/005-fix-chat-functions/quickstart.md`:
  - Media Sending (P1): Image, PNG, video, audio, PDF, HEIC
  - Clear Chat (P2): Time-ranged clear, reaction preservation, offline error
  - Context Menu (P2): Delete for me, reply context clear, forward with media
  - Audio Playback (P3): Play, pause, resume, waveform
  - Memory (P3): 20+ images, memory tab check

  **Acceptance**: All checklist items pass.

- [ ] T015 Update `BUGS.md` with any bugs found during testing or move resolved bugs

  **Context**: Per project convention (CLAUDE.md), all bugs go in `BUGS.md`. After implementing all fixes:
  - Check if any of the 6 confirmed bugs from research.md were previously listed in `BUGS.md` → move to "Resolved Bugs"
  - If new bugs are found during testing → add to "Open Bugs" with full template

  **Acceptance**: `BUGS.md` is up-to-date with all resolved/new bugs from this feature.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
  - T001 (storage.js fix) and T002 (audio helpers) are independent [P] — can run in parallel
- **Phase 3 (US1 - Media)**: Depends on T001 (storage.js fix impacts IndexedDB persistence)
- **Phase 4 (US2 - Clear Chat)**: No dependency on Phase 3 — can run in parallel with US1
- **Phase 5 (US3 - Context Menu)**: No dependency on Phase 3 or 4 — can run in parallel
- **Phase 6 (US4 - Audio)**: Depends on T002 (audio helper functions)
- **Phase 7 (US5 - Memory)**: Depends on T003/T004 (US1 memory fix)
- **Phase 8 (Polish)**: Depends on all previous phases

### User Story Dependencies

```
T001 (storage.js) ──┬──→ T003/T004/T005 (US1: Media + Memory)
                    │
T002 (audio JS) ────┤──→ T010/T011 (US4: Audio)
                    │
                    ├──→ T006/T007 (US2: Clear Chat) [independent]
                    │
                    └──→ T008/T009 (US3: Context Menu) [independent]

All ────────────────────→ T012/T013/T014/T015 (Polish)
```

### Within Each User Story

- US1: T003 → T004 → T005 (sequential — each depends on previous)
- US2: T006 and T007 can run in parallel [P] (different code sections in ClearChat)
- US3: T008 and T009 can run in parallel [P] (different methods: DeleteForMe vs SendForwardedMessage)
- US4: T010 and T011 can run in parallel [P] (different methods in MessageBubble.razor)
- US5: T012 (depends on T003/T004)

### Parallel Opportunities

```
After Phase 1 completes, these can run simultaneously:
├── US1: T003 → T004 → T005
├── US2: T006 ∥ T007
├── US3: T008 ∥ T009
└── US4: T010 ∥ T011 (after T002)
```

---

## Parallel Example: Phase 1 Setup

```bash
# These two tasks modify different files — run in parallel:
Task T001: Fix storeMessages in storage.js
Task T002: Add audio helpers in media-helpers.js
```

## Parallel Example: After Phase 1

```bash
# These user stories modify different methods in Chat.razor — can interleave:
Task T006: Fix _messageReactions in ClearChat method
Task T008: Fix reply context in DeleteForMe method
Task T009: Fix forward failure in SendForwardedMessage method

# These modify MessageBubble.razor (same file but different methods):
Task T010: Fix ToggleAudioPlayback eval()
Task T011: Fix UpdateAudioTimeAsync/LoadAudioDurationAsync eval()
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: T001 (storage.js) + T002 (audio helpers)
2. Complete Phase 3: T003 → T004 → T005 (media + memory fix)
3. **STOP and VALIDATE**: Send image/video/audio/file between two users
4. If media works correctly → deploy/demo

### Incremental Delivery

1. Phase 1 (T001, T002) → Foundation ready
2. US1 (T003-T005) → Media works → Deploy/Demo (MVP!)
3. US2 (T006-T007) → Clear chat works → Deploy/Demo
4. US3 (T008-T009) → Context menu works → Deploy/Demo
5. US4 (T010-T011) → Audio playback clean → Deploy/Demo
6. US5 (T012) → Memory verified → Deploy/Demo
7. Polish (T013-T015) → Full validation → Final Deploy

### Single Developer Strategy

Recommended order for a single developer:
1. T001 + T002 (parallel, different files)
2. T003 → T004 → T005 (US1, sequential)
3. T006 + T007 (US2, same method but different sections)
4. T008 + T009 (US3, different methods)
5. T010 + T011 (US4, different methods)
6. T012 (US5, manual verification)
7. T013 → T014 → T015 (Polish, sequential)

---

## Notes

- **No server-side changes**: All fixes are in `src/ToledoMessage.Client/` (Blazor WASM + JS interop)
- **Files modified**: 3 files total
  - `src/ToledoMessage.Client/wwwroot/storage.js` (T001)
  - `src/ToledoMessage.Client/wwwroot/media-helpers.js` (T002)
  - `src/ToledoMessage.Client/Pages/Chat.razor` (T003, T004, T006, T007, T008, T009)
  - `src/ToledoMessage.Client/Components/MessageBubble.razor` (T010, T011)
- **Research confirms**: byte[] → Uint8Array marshalling is correct (R-001). No JS interop fix needed.
- **Existing tests**: 215 tests must pass throughout. Run after each user story.
- **User input note**: Tasks are written with rich context so another model can execute them without additional codebase exploration.
