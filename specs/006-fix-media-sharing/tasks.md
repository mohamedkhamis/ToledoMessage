# Tasks: Fix Media Sharing

**Input**: Design documents from `/specs/006-fix-media-sharing/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Included — explicitly requested in the feature specification (US5).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the MediaPayload model, JS helper functions, and foundational changes that all user stories depend on.

- [ ] T001 Create `MediaPayload` record in `src/ToledoVault.Shared/Models/MediaPayload.cs` with fields: `FileName` (string?), `MimeType` (string), `Caption` (string?), `Thumbnail` (string?), `Data` (string). Include JSON serialization support and a static `Serialize(MediaPayload) → byte[]` method (UTF-8 JSON) and `Deserialize(byte[]) → MediaPayload` method.
- [ ] T002 [P] Add image compression JS function `compressImage(byteArray, mimeType, maxDimension, quality)` to `src/ToledoVault.Client/wwwroot/media-helpers.js` that uses canvas API to resize and re-encode as JPEG. Returns `{ bytes: Uint8Array, width: int, height: int }`.
- [ ] T003 [P] Add thumbnail generation JS function `generateThumbnail(byteArray, mimeType, maxDimension, quality)` to `src/ToledoVault.Client/wwwroot/media-helpers.js` that uses canvas API to create a small JPEG thumbnail (~200px, ~60% quality). Returns `Uint8Array`.
- [ ] T004 [P] Add video frame capture JS function `captureVideoFrame(byteArray, mimeType)` to `src/ToledoVault.Client/wwwroot/media-helpers.js` that loads video into a `<video>` element, seeks to 1s, draws frame to canvas, returns thumbnail as `Uint8Array`.
- [ ] T005 [P] Add file download JS function `downloadFile(byteArray, fileName, mimeType)` to `src/ToledoVault.Client/wwwroot/media-helpers.js` that creates a temporary `<a>` element with blob URL and triggers download.
- [ ] T006 Change `ChatMessage.MediaBytes` property from `init` to `set` in `src/ToledoVault.Client/Pages/Chat.razor` (line ~1718) to allow nulling after persistence.
- [ ] T007 Change `ChatMessage.Text` property from `init` to `set` in `src/ToledoVault.Client/Pages/Chat.razor` to allow setting caption from MediaPayload on received messages.

**Checkpoint**: MediaPayload model exists, JS helpers ready, ChatMessage properties are mutable. Foundation ready for user story implementation.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire up MediaPayload into the encryption/decryption pipeline, replacing the current raw-bytes approach.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T008 Update `SendMediaToRecipients()` in `src/ToledoVault.Client/Pages/Chat.razor` (line ~941) to: (a) construct a `MediaPayload` with `Data` = base64 of media bytes, `FileName`, `MimeType`, `Caption` = `input.Text`, `Thumbnail` = null (added later per story), (b) serialize to `byte[]` via `MediaPayload.Serialize()`, (c) pass serialized bytes to `Crypto.EncryptBytesForAllDevicesAsync()`, (d) set `FileName = null` and `MimeType = null` on the `SendMessageRequest` (metadata now inside encrypted payload), (e) **remove** the separate caption text message sending block (lines ~998-1014) since caption is now inside the payload.
- [ ] T009 Update `DecryptEnvelopeToChatMessage()` in `src/ToledoVault.Client/Pages/Chat.razor` (line ~1017) for media content types to: (a) decrypt to `byte[]` via `Crypto.DecryptToBytesAsync()`, (b) deserialize as `MediaPayload` via `MediaPayload.Deserialize()`, (c) decode `payload.Data` from base64 to get raw media bytes, (d) extract `FileName`, `MimeType`, `Caption` from the payload (not from the envelope), (e) create blob URL from the raw media bytes, (f) set `ChatMessage.Text = payload.Caption ?? ""`, (g) set `ChatMessage.MediaBytes` to the raw bytes (for IndexedDB persistence — will be nulled after persist), (h) if `payload.Thumbnail` is not null, create a separate thumbnail blob URL for instant preview.
- [ ] T010 Update `HandleSend()` in `src/ToledoVault.Client/Pages/Chat.razor` (line ~822) to null out `tempMessage.MediaBytes = null` after `PersistMessageAsync(tempMessage)` completes (line ~876). This fixes the memory leak for outgoing messages.
- [ ] T011 Update `PersistMessageAsync()` call site for **received** messages in `src/ToledoVault.Client/Pages/Chat.razor`: after `DecryptEnvelopeToChatMessage()` returns, call `PersistMessageAsync(chatMessage)` and then set `chatMessage.MediaBytes = null`. This fixes received media not being cached in IndexedDB.
- [ ] T012 Update `DeleteForMe` handler in `src/ToledoVault.Client/Pages/Chat.razor` (line ~1269) to use `mediaHelpers.revokeObjectUrl` instead of direct `URL.revokeObjectURL` call, for consistency (FR-012).
- [ ] T013 Update `GetDefaultMimeType()` in `src/ToledoVault.Client/Pages/Chat.razor` (line ~1083) to return `"audio/webm"` for `ContentType.Audio` instead of `"audio/mpeg"` (fixes voice message playback when MIME is null). Note: with MediaPayload, MIME type comes from the encrypted payload, but keep the fallback correct for edge cases.

**Checkpoint**: Encryption pipeline uses MediaPayload. Captions bundled. Memory leaks fixed. Received media persisted. Core plumbing complete.

---

## Phase 3: User Story 1 - Send and Receive Images Reliably (Priority: P1) 🎯 MVP

**Goal**: Images are compressed, include thumbnail preview, display inline with lightbox, and captions appear in the same bubble.

**Independent Test**: Send an image with caption in a 1:1 chat. Recipient sees compressed image inline with caption below it in one bubble. Tap opens lightbox. Reload page — image loads from IndexedDB cache.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T014 [P] [US1] Write test `MediaPayload_Serialize_Deserialize_RoundTrip` in `tests/ToledoVault.Client.Tests/Models/MediaPayloadTests.cs`: Create a `MediaPayload` with all fields populated (including base64 data, caption, thumbnail, filename, mimetype), serialize to bytes, deserialize back, assert all fields match.
- [ ] T015 [P] [US1] Write test `MediaPayload_Serialize_WithNullOptionalFields` in `tests/ToledoVault.Client.Tests/Models/MediaPayloadTests.cs`: Create a `MediaPayload` with `Caption = null`, `Thumbnail = null`, `FileName = null`. Serialize and deserialize. Assert nulls are preserved and required fields (`Data`, `MimeType`) are correct.
- [ ] T016 [P] [US1] Write test `MediaPayload_Encrypt_Decrypt_RoundTrip_Image` in `tests/ToledoVault.Integration.Tests/MediaEncryptionTests.cs`: Create two `DoubleRatchet` session pairs (alice/bob). Construct a `MediaPayload` with JPEG image bytes (use a small synthetic byte array), serialize, encrypt via alice's ratchet, decrypt via bob's ratchet, deserialize, assert `Data` bytes match original, `FileName` matches, `MimeType` matches, `Caption` matches.
- [ ] T017 [P] [US1] Write test `MediaPayload_Caption_Bundled_Not_Separate` in `tests/ToledoVault.Client.Tests/Models/MediaPayloadTests.cs`: Create a `MediaPayload` with `Caption = "Check this out!"` and `Data` = base64 image bytes. Serialize and deserialize. Assert `Caption` field is present in the deserialized payload, proving caption travels with media (not as separate message).
- [ ] T018 [P] [US1] Write test `SendMessage_Media_FileName_MimeType_Null_On_Request` in `tests/ToledoVault.Server.Tests/Hubs/ChatHubTests.cs`: Send a `SendMessageRequest` with `ContentType = Image`, `FileName = null`, `MimeType = null` (metadata inside ciphertext). Assert the hub accepts and stores the message without error.

### Implementation for User Story 1

- [ ] T019 [US1] Add image compression call in `MessageInput.razor` `SetSelectedFile()` method (`src/ToledoVault.Client/Components/MessageInput.razor`, line ~307): When `_selectedContentType == Image` and user did NOT choose "send as document", call `mediaHelpers.compressImage(_selectedFileBytes, mimeType, 1600, 0.8)` via JS interop. Replace `_selectedFileBytes` with compressed result. Update preview blob URL with compressed bytes.
- [ ] T020 [US1] Add "Send as document" toggle in `MessageInput.razor` (`src/ToledoVault.Client/Components/MessageInput.razor`): When an image is selected, show a small toggle/button "Send as document" that sets a `_sendAsDocument` flag. When true, skip compression and set `ContentType = File` instead of `Image`.
- [ ] T021 [US1] Add thumbnail generation in `HandleSend()` or `SendMediaToRecipients()` in `src/ToledoVault.Client/Pages/Chat.razor`: Before constructing the `MediaPayload`, if `ContentType == Image`, call `mediaHelpers.generateThumbnail(mediaBytes, mimeType, 200, 0.6)` via JS interop. Set `MediaPayload.Thumbnail` to base64 of the result.
- [ ] T022 [US1] Update `MessageBubble.razor` image rendering (`src/ToledoVault.Client/Components/MessageBubble.razor`, line ~16): (a) Show `ThumbnailDataUrl` as blurred placeholder while full image loads (if thumbnail available), (b) Display caption text below the image within the same bubble if `Text` is not empty, (c) Keep existing lightbox tap-to-expand and pinch-to-zoom functionality.
- [ ] T023 [US1] Add `ThumbnailDataUrl` parameter to `MessageBubble.razor` (`src/ToledoVault.Client/Components/MessageBubble.razor`): New `[Parameter] public string? ThumbnailDataUrl { get; set; }`. Pass from `Chat.razor` when rendering media messages (extract from `MediaPayload.Thumbnail` during decryption, create blob URL).
- [ ] T024 [US1] Update `Chat.razor` `HandleSend()` to set `tempMessage.Text = input.Text` for media messages (line ~845) so the caption displays in the sender's UI immediately (optimistic UI). Currently `Text` is set but gets sent separately — now it stays in the same bubble.
- [ ] T025 [US1] Update `LoadCachedMessages()` in `src/ToledoVault.Client/Pages/Chat.razor` (line ~451) to display cached media messages with their stored caption text. Ensure `StoredMessage.Text` (which now contains the caption) is mapped to `ChatMessage.Text`.

**Checkpoint**: Images send compressed with embedded thumbnail, display inline with caption in single bubble, lightbox works, cached correctly in IndexedDB.

---

## Phase 4: User Story 2 - Send and Receive Videos Reliably (Priority: P1)

**Goal**: Videos display inline with play controls, include thumbnail from first frame, and captions appear in the same bubble.

**Independent Test**: Send a video in a 1:1 chat. Recipient sees video thumbnail, taps play, video plays inline with controls. Caption shows below video.

### Tests for User Story 2

- [ ] T026 [P] [US2] Write test `MediaPayload_Encrypt_Decrypt_RoundTrip_Video` in `tests/ToledoVault.Integration.Tests/MediaEncryptionTests.cs`: Same pattern as T016 but with `ContentType.Video`, different MIME type (`video/mp4`), and larger synthetic byte array. Assert round-trip integrity.
- [ ] T027 [P] [US2] Write test `SendMessage_Video_SizeLimit_Enforced` in `tests/ToledoVault.Server.Tests/Hubs/ChatHubTests.cs`: Send a `SendMessageRequest` with `ContentType = Video` and ciphertext exceeding `MaxMediaCiphertextSizeBytes` (15 MB). Assert the hub rejects with appropriate error.

### Implementation for User Story 2

- [ ] T028 [US2] Add video thumbnail generation in `SendMediaToRecipients()` in `src/ToledoVault.Client/Pages/Chat.razor`: When `ContentType == Video`, call `mediaHelpers.captureVideoFrame(mediaBytes, mimeType)` via JS interop. Set `MediaPayload.Thumbnail` to base64 of the captured frame.
- [ ] T029 [US2] Update `MessageBubble.razor` video rendering (`src/ToledoVault.Client/Components/MessageBubble.razor`, line ~29): (a) Show thumbnail with play button overlay before user taps play, (b) Replace thumbnail with `<video>` element on play tap with `controls`, `preload="metadata"`, (c) Display caption below video if `Text` is not empty, (d) Add fullscreen button.
- [ ] T030 [US2] Add video preview in `MessageInput.razor` `SetSelectedFile()` (`src/ToledoVault.Client/Components/MessageInput.razor`): When `_selectedContentType == Video`, call `mediaHelpers.captureVideoFrame` to generate a preview thumbnail for the compose area. Show thumbnail with video icon overlay and file size.
- [ ] T031 [US2] Add file size validation in `MessageInput.razor` `SetSelectedFile()` (`src/ToledoVault.Client/Components/MessageInput.razor`): Check `file.Size > ProtocolConstants.MaxMediaCiphertextSizeBytes` before reading the file. If exceeded, show error toast "File too large. Maximum size is 15 MB." and clear the selection. Apply to all media types (images, videos, documents, audio).

**Checkpoint**: Videos send with first-frame thumbnail, play inline with controls, caption in same bubble.

---

## Phase 5: User Story 3 - Send and Receive Documents Reliably (Priority: P2)

**Goal**: Documents display as file cards with icon, name, size, and download button. Captions in same bubble.

**Independent Test**: Send a PDF in a 1:1 chat. Recipient sees file card with PDF icon, filename, size. Tap download saves the file with original filename.

### Tests for User Story 3

- [ ] T032 [P] [US3] Write test `MediaPayload_Encrypt_Decrypt_RoundTrip_Document` in `tests/ToledoVault.Integration.Tests/MediaEncryptionTests.cs`: Round-trip with `ContentType.File`, MIME `application/pdf`, filename `report.pdf`. Assert all fields preserved.
- [ ] T033 [P] [US3] Write test `MediaPayload_FileName_Sanitized` in `tests/ToledoVault.Client.Tests/Models/MediaPayloadTests.cs`: Create payloads with filenames containing path separators (`../etc/passwd`, `C:\Windows\system32\file.exe`), null bytes, and strings exceeding 255 chars. Assert `MediaPayload.SanitizeFileName()` strips dangerous chars and truncates.

### Implementation for User Story 3

- [ ] T034 [US3] Add `SanitizeFileName(string? fileName)` static method to `MediaPayload` in `src/ToledoVault.Shared/Models/MediaPayload.cs`: Strip path separators (`/`, `\`), null bytes, control characters. Truncate to 255 chars. Return `null` if input is null or empty after sanitization.
- [ ] T035 [US3] Update `MessageBubble.razor` file rendering (`src/ToledoVault.Client/Components/MessageBubble.razor`, line ~91): (a) Show file-type icon based on MIME type or extension (PDF=📄, Word=📝, Excel=📊, ZIP=📦, generic=📎), (b) Display filename and human-readable file size (e.g., "2.4 MB"), (c) Add download button that calls `mediaHelpers.downloadFile()`, (d) Display caption below file card if `Text` is not empty.
- [ ] T036 [US3] Add `FormatFileSize(long bytes)` helper method (in `MessageBubble.razor` or a shared utility): Convert bytes to human-readable string (B, KB, MB). Used for document file cards.
- [ ] T037 [US3] Update `MessageInput.razor` document preview (`src/ToledoVault.Client/Components/MessageInput.razor`): When `_selectedContentType == File`, show file-type icon, filename, and file size in the compose area (instead of generic "File attached" text).
- [ ] T038 [US3] Wire `mediaHelpers.downloadFile()` call in `MessageBubble.razor` download button: On click, get the decrypted bytes from the blob URL via `mediaHelpers.fetchBlobAsBytes(blobUrl)`, then call `mediaHelpers.downloadFile(bytes, fileName, mimeType)`.

**Checkpoint**: Documents display as file cards with metadata, download works with original filename, caption in same bubble.

---

## Phase 6: User Story 4 - Media Memory and Lifecycle Management (Priority: P2)

**Goal**: No memory leaks from media byte arrays. All blob URLs cleaned up on dispose/delete/clear.

**Independent Test**: Send 20 images in a session. Monitor browser memory — should not grow unboundedly. Navigate away and back — no stale blob URLs.

### Tests for User Story 4

- [ ] T039 [P] [US4] Write test `MediaPayload_Bytes_Released_After_Persistence` in `tests/ToledoVault.Client.Tests/Models/MediaPayloadTests.cs`: Simulate the lifecycle: create `ChatMessage` with `MediaBytes` set, call a persistence mock, then verify `MediaBytes` can be set to null (property is settable, not init-only).
- [ ] T040 [P] [US4] Write test `BlobUrl_Revoked_On_DeleteForMe` in `tests/ToledoVault.Client.Tests/Chat/MediaLifecycleTests.cs`: Verify that `DeleteForMe` calls the centralized `revokeObjectUrl` helper (not direct browser API). This is a design/code review verification test.

### Implementation for User Story 4

- [ ] T041 [US4] Audit all blob URL creation in `src/ToledoVault.Client/Pages/Chat.razor` and ensure every `CreateMediaBlobUrl()` call registers the URL in `_blobUrls` for cleanup. Verify `RevokeBlobUrls()` in `DisposeAsync()` revokes all tracked URLs.
- [ ] T042 [US4] Audit `ClearChat()` in `src/ToledoVault.Client/Pages/Chat.razor` to revoke all blob URLs in `_blobUrls` before clearing the message list. Add error handling (currently silently swallows exceptions in catch blocks — add error toast per research R-003 from 005 spec).
- [ ] T043 [US4] Verify `PendingForward` static field cleanup in `src/ToledoVault.Client/Pages/Chat.razor` (line ~340): Ensure `PendingForward` is set to `null` in a `finally` block in `OnInitializedAsync` to prevent stale forwarded media if initialization throws.
- [ ] T044 [US4] Add thumbnail blob URL tracking: When thumbnail blob URLs are created (from `MediaPayload.Thumbnail`), register them in `_blobUrls` so they are revoked on dispose.

**Checkpoint**: No memory leaks. All blob URLs tracked and cleaned up. Error handling for clear/delete operations.

---

## Phase 7: User Story 5 - Unit Tests for Media Operations (Priority: P2)

**Goal**: Comprehensive test coverage for all media send/receive/encrypt/decrypt paths.

**Independent Test**: Run `dotnet test` — all new media tests pass with >90% coverage on media paths.

### Tests (this IS the implementation for US5)

- [ ] T045 [P] [US5] Write test `MediaEncryption_LargePayload_RoundTrip` in `tests/ToledoVault.Integration.Tests/MediaEncryptionTests.cs`: Encrypt/decrypt a `MediaPayload` with `Data` containing ~1 MB of random bytes. Assert round-trip integrity and verify no data corruption for large payloads.
- [ ] T046 [P] [US5] Write test `MediaEncryption_PreKeyMessage_RoundTrip` in `tests/ToledoVault.Integration.Tests/MediaEncryptionTests.cs`: Test media encryption as a PreKeyMessage (first message to a new device). Use `MessageEncryptionService.EncryptPreKeyMessageBytes()` and verify decryption works.
- [ ] T047 [P] [US5] Write test `SendMessage_Media_ContentTypes_Accepted` in `tests/ToledoVault.Server.Tests/Hubs/ChatHubTests.cs`: Test that `ChatHub.SendMessage` accepts all media content types (Image, Audio, Video, File) with valid ciphertext and null FileName/MimeType. Assert messages are stored and relayed.
- [ ] T048 [P] [US5] Write test `SendMessage_Media_SizeLimit_PerContentType` in `tests/ToledoVault.Server.Tests/Services/MessageRelayServiceTests.cs`: Verify `GetMaxCiphertextSize()` returns `MaxMediaCiphertextSizeBytes` for Image, Audio, Video, File and `MaxCiphertextSizeBytes` for Text.
- [ ] T049 [P] [US5] Write test `MediaPayload_MimeType_Validation` in `tests/ToledoVault.Client.Tests/Models/MediaPayloadTests.cs`: Test MIME type detection fallback — when `MimeType` is null or unknown, verify it defaults to `application/octet-stream`. Test common MIME types: `image/jpeg`, `image/png`, `video/mp4`, `audio/webm`, `application/pdf`.
- [ ] T050 [P] [US5] Write test `MediaPayload_MaxSize_Validation` in `tests/ToledoVault.Client.Tests/Models/MediaPayloadTests.cs`: Create a `MediaPayload` with `Data` exceeding 15 MB (base64). Verify that a validation method rejects it with appropriate error before encryption is attempted.
- [ ] T051 [P] [US5] Write test `StoredMessage_MediaDataBase64_Persisted_For_Incoming` in `tests/ToledoVault.Client.Tests/Services/MessageStoreTests.cs`: Verify that the `StoredMessage` created from a received media `ChatMessage` has `MediaDataBase64` populated (not null), confirming the fix from T011.

**Checkpoint**: All media tests pass. Coverage target met on media paths.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: UI polish, progress indicators, error states, and final validation.

- [ ] T052 [P] Add sending progress indicator in `MessageBubble.razor` (`src/ToledoVault.Client/Components/MessageBubble.razor`): Show a spinner or progress bar overlay on media messages with `Status == Sending`. Replace with checkmark on `Sent`/`Delivered`.
- [ ] T053 [P] Add "failed to decrypt" placeholder in `MessageBubble.razor` (`src/ToledoVault.Client/Components/MessageBubble.razor`): When `MediaDataUrl` is null for a media content type, show "Cannot display media" with a retry button instead of just a placeholder emoji. Retry should attempt re-decryption from IndexedDB cached base64.
- [ ] T054 [P] Add file size display in `MessageInput.razor` preview area (`src/ToledoVault.Client/Components/MessageInput.razor`): Show the file size next to the filename in the compose preview for all media types.
- [ ] T055 Update `src/ToledoVault.Client/Pages/Chat.razor` `HandleSend()` to handle `SendMediaToRecipients` exceptions gracefully: if encryption or sending fails, set `tempMessage.Status = DeliveryStatus.Failed`, show error toast, do NOT persist failed messages.
- [ ] T056 Run full test suite (`dotnet test`) and verify all existing tests still pass (no regressions) and all new media tests pass.
- [ ] T057 Run quickstart.md manual validation: test all 7 scenarios from the quickstart (image, video, document, caption, reload cache, lightbox, download).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (T001 for MediaPayload, T006-T007 for ChatMessage mutability)
- **User Stories (Phase 3-7)**: All depend on Phase 2 completion (encryption pipeline wired up)
  - US1 (Images) and US2 (Videos) can proceed in parallel after Phase 2
  - US3 (Documents) can proceed in parallel with US1/US2
  - US4 (Memory) depends on Phase 2 but is independent of US1-US3
  - US5 (Tests) depends on Phase 2 for encryption tests; some tests can be written earlier
- **Polish (Phase 8)**: Depends on US1-US4 being complete

### User Story Dependencies

- **US1 (P1 - Images)**: Depends on Phase 2 only. No cross-story dependencies.
- **US2 (P1 - Videos)**: Depends on Phase 2 only. No cross-story dependencies. Shares file size validation with US1 (T031 applies to all types).
- **US3 (P2 - Documents)**: Depends on Phase 2 only. Uses `downloadFile` JS helper from Phase 1.
- **US4 (P2 - Memory)**: Depends on Phase 2 only. Audit tasks verify changes from Phase 2.
- **US5 (P2 - Tests)**: Depends on Phase 2 for integration tests. Can write unit tests (T014-T015) in parallel with Phase 2.

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- JS interop helpers before Blazor component changes
- Component parameter changes before rendering changes
- Core implementation before UI polish

### Parallel Opportunities

**Phase 1**: T002, T003, T004, T005 are all independent JS functions — run in parallel.
**Phase 2**: T012 and T013 are independent fixes — run in parallel.
**Phase 3 (US1)**: T014-T018 tests can all run in parallel. T019 and T020 can run in parallel (different files).
**Phase 4 (US2)**: T026-T027 tests in parallel. T028 and T030 in parallel (different files).
**Phase 5 (US3)**: T032-T033 tests in parallel. T035 and T037 in parallel (different files).
**Phase 7 (US5)**: T045-T051 all test files — fully parallelizable.

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel (different test files):
Task: T014 "MediaPayload round-trip test in tests/ToledoVault.Client.Tests/"
Task: T015 "MediaPayload null fields test in tests/ToledoVault.Client.Tests/"
Task: T016 "Encryption round-trip test in tests/ToledoVault.Integration.Tests/"
Task: T017 "Caption bundling test in tests/ToledoVault.Client.Tests/"
Task: T018 "Hub accepts null metadata test in tests/ToledoVault.Server.Tests/"

# After tests written, launch parallel implementation:
Task: T019 "Image compression in MessageInput.razor"
Task: T020 "Send-as-document toggle in MessageInput.razor" (same file — sequential with T019)
Task: T021 "Thumbnail generation in Chat.razor"
Task: T022 "MessageBubble image rendering" (different file — parallel with T021)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T007)
2. Complete Phase 2: Foundational (T008-T013)
3. Complete Phase 3: User Story 1 - Images (T014-T025)
4. **STOP and VALIDATE**: Send image with caption, verify single bubble, verify lightbox, verify IndexedDB cache
5. Deploy and test at `http://localhost:8080`

### Incremental Delivery

1. Setup + Foundational → Core pipeline working (caption bundling, metadata encryption, memory fix)
2. Add US1 (Images) → Compressed images with thumbnails → Deploy (MVP!)
3. Add US2 (Videos) → Video with inline playback → Deploy
4. Add US3 (Documents) → File cards with download → Deploy
5. Add US4 (Memory audit) → Verify no leaks → Deploy
6. Add US5 (Tests) → Full test coverage → Deploy
7. Polish → Progress indicators, error states → Final deploy

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- All changes scoped to 1:1 conversations only (group media deferred per spec FR-018)
- No database migration needed — DB will be cleared manually after completion
- No server-side changes needed (server already handles opaque ciphertext blobs)
- Total: 57 tasks across 8 phases
