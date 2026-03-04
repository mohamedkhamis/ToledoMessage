# Feature Specification: Fix Media Sharing

**Feature Branch**: `006-fix-media-sharing`
**Created**: 2026-03-04
**Status**: Draft
**Input**: User description: "Fix sending files, images, and videos — issues when sending and receiving. Want WhatsApp-like functionality and UI with same end-to-end encryption. Write unit tests for sending images, videos, and files."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send and Receive Images Reliably (Priority: P1)

As a user, I want to select an image from my device, see a preview before sending, and have it delivered to the recipient who can view it inline and tap to see it full-screen — just like WhatsApp. The image must be end-to-end encrypted. Currently, outgoing media bytes stay in memory after sending, captions are sent as separate unlinked messages, and there are inconsistencies in blob URL management.

**Why this priority**: Image sharing is the most common media use case. Fixing the core send/receive/display pipeline unblocks all other media types.

**Independent Test**: Can be fully tested by sending an image in a 1:1 conversation and verifying the recipient sees it inline with correct rendering, and that memory is properly released after sending.

**Acceptance Scenarios**:

1. **Given** a user is in a chat, **When** they tap the attach button and select an image (JPEG, PNG, WebP, GIF), **Then** the image is compressed to a smaller size and a thumbnail preview appears in the compose area before sending.
2. **Given** a user has selected an image, **When** they tap send, **Then** the image is encrypted and delivered to the recipient who sees it rendered inline as a thumbnail.
3. **Given** a recipient receives an image message, **When** they tap the thumbnail, **Then** a full-screen lightbox opens with pinch-to-zoom support.
4. **Given** a user sends an image with a caption, **When** the recipient receives it, **Then** the caption appears directly below the image within the same message bubble (not as a separate message).
5. **Given** a user sends an image, **When** the send completes, **Then** the raw media bytes are released from memory (not held for the session lifetime).

---

### User Story 2 - Send and Receive Videos Reliably (Priority: P1)

As a user, I want to send and receive video files with inline playback, a video thumbnail preview, and playback controls — similar to WhatsApp. Videos must be end-to-end encrypted.

**Why this priority**: Video sharing is the second most used media feature and shares the same pipeline issues as images.

**Independent Test**: Can be fully tested by sending a video in a 1:1 conversation and verifying the recipient can play it inline with standard controls.

**Acceptance Scenarios**:

1. **Given** a user selects a video file (MP4, WebM, MOV), **When** they attach it, **Then** a video thumbnail preview with duration indicator appears in the compose area.
2. **Given** a user sends a video, **When** the recipient receives it, **Then** the video renders inline with play button overlay and playback controls.
3. **Given** a recipient taps play on a received video, **When** playback starts, **Then** standard controls (play/pause, seek, fullscreen) are available.
4. **Given** a video exceeds the 15 MB size limit, **When** the user tries to attach it, **Then** a clear error message explains the size limit.

---

### User Story 3 - Send and Receive Documents Reliably (Priority: P2)

As a user, I want to send and receive document files (PDF, Word, Excel, ZIP, etc.) with a file-type icon, file name, and file size displayed — similar to WhatsApp. Documents must be end-to-end encrypted.

**Why this priority**: Document sharing completes the media feature set but is less frequent than image/video sharing.

**Independent Test**: Can be fully tested by sending a PDF in a 1:1 conversation and verifying the recipient sees file metadata and can download it.

**Acceptance Scenarios**:

1. **Given** a user selects a document file, **When** they attach it, **Then** the compose area shows the file name, size, and a file-type icon.
2. **Given** a user sends a document, **When** the recipient receives it, **Then** it appears as a file card with file-type icon, file name, and file size.
3. **Given** a recipient taps a received document, **When** they tap download, **Then** the decrypted file downloads to their device with the original filename.
4. **Given** a document has a caption, **When** the recipient receives it, **Then** the caption appears below the file card within the same message bubble.

---

### User Story 4 - Media Message Memory and Lifecycle Management (Priority: P2)

As a user, I expect the app to manage memory efficiently when I send and receive media, so the app remains responsive even after many media messages in a session.

**Why this priority**: Current implementation holds raw media bytes in memory for outgoing messages for the entire session due to read-only properties on the message model. This degrades performance over time.

**Independent Test**: Can be tested by monitoring memory usage after sending multiple large media files and verifying bytes are released after blob URLs are created.

**Acceptance Scenarios**:

1. **Given** a user sends a media message, **When** the blob URL is created for display, **Then** the raw byte array reference is released (not held in the message object).
2. **Given** a user navigates away from a chat, **When** the chat component disposes, **Then** all blob URLs created for that chat session are revoked.
3. **Given** a user deletes a media message locally, **When** deletion completes, **Then** the associated blob URL is revoked using the standard helper function (not direct browser API calls).

---

### User Story 5 - Unit Tests for Media Operations (Priority: P2)

As a developer, I want comprehensive unit tests covering media encryption, decryption, blob URL management, file validation, and message lifecycle to prevent regressions.

**Why this priority**: Tests ensure the fixes are stable and prevent regressions as the codebase evolves.

**Independent Test**: Can be validated by running the test suite and confirming all media-related tests pass.

**Acceptance Scenarios**:

1. **Given** the test suite, **When** image encryption/decryption tests run, **Then** they verify round-trip integrity (encrypt then decrypt then compare bytes).
2. **Given** the test suite, **When** video and document tests run, **Then** they verify correct content type detection, MIME type handling, and file size validation.
3. **Given** the test suite, **When** caption-with-media tests run, **Then** they verify captions are bundled with media (not sent as separate messages).
4. **Given** the test suite, **When** memory lifecycle tests run, **Then** they verify byte arrays are released after blob URL creation.

---

### Edge Cases

- What happens when a user sends a file with no extension? System should detect MIME type from file header bytes (magic numbers) when possible, or default to `application/octet-stream`.
- What happens when a media message fails to decrypt? System should show a "Cannot display media" placeholder with an option to retry decryption.
- What happens when the browser doesn't support a video codec (e.g., MOV on non-Safari)? System should show a fallback message with a download option.
- What happens when a user sends media while offline? The message should be queued and sent when connectivity is restored, with a visual "pending" indicator.
- What happens when the recipient's local storage is full? System should show a warning and gracefully degrade (display media from server without caching).
- What happens when a forwarded media message's blob URL has been revoked? System should re-fetch bytes from local cache or show an error with retry option.
- What happens when MIME type metadata is null on an incoming audio message? System should detect the actual format rather than defaulting to an incorrect type (which breaks voice message playback).

## Clarifications

### Session 2026-03-04

- Q: Should images be compressed before sending, or sent at original quality? → A: Compress by default; user can choose "send as document" for original quality (WhatsApp-style).
- Q: Should media sharing fixes cover group chats as well, or just 1:1 conversations? → A: 1:1 only for now; group media deferred to a future release.
- Q: Should the sender generate a small thumbnail to embed in the message for instant preview? → A: Yes, generate and embed a small thumbnail (few KB) in each media message for instant recipient-side preview.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST encrypt media file content (images, videos, documents) end-to-end using the same encryption protocol as text messages.
- **FR-002**: System MUST bundle caption text with media content within a single message (not send as a separate unlinked message).
- **FR-003**: System MUST display image messages as inline thumbnails in the chat with tap-to-expand lightbox and pinch-to-zoom.
- **FR-004**: System MUST display video messages with inline playback controls (play/pause, seek, fullscreen).
- **FR-005**: System MUST display document messages as file cards showing file-type icon, file name, and human-readable file size.
- **FR-006**: System MUST allow recipients to download decrypted document files with the original filename preserved.
- **FR-007**: System MUST release raw media byte arrays from memory after creating display URLs for both sent and received messages.
- **FR-008**: System MUST clean up display URLs when messages are deleted, chat is cleared, or the chat view is disposed.
- **FR-009**: System MUST validate file size before upload and show a clear error when the size limit is exceeded.
- **FR-017**: System MUST compress images by default before sending (reducing file size while maintaining acceptable visual quality). Users MUST have a "send as document" option to send the original uncompressed image.
- **FR-010**: System MUST show upload/download progress indicators for media messages.
- **FR-011**: System MUST handle missing MIME type metadata by detecting format from file content rather than using incorrect defaults.
- **FR-012**: System MUST use consistent resource management through centralized helper functions (not mixed direct and helper calls).
- **FR-013**: System MUST encrypt file metadata (filename, MIME type) as part of the encrypted payload rather than transmitting as plaintext. No backward compatibility or migration is required — the database will be cleared manually after this feature is complete.
- **FR-014**: System MUST provide comprehensive unit tests covering media encryption round-trips, content type detection, file size validation, caption bundling, and memory lifecycle.
- **FR-015**: System MUST show a visual "sending" indicator on outgoing media messages until delivery is confirmed.
- **FR-016**: System MUST display a "failed to decrypt" placeholder for media that cannot be decrypted, with a retry option.
- **FR-018**: Media sharing fixes (compression, caption bundling, metadata encryption, memory management) MUST apply to 1:1 conversations only. Group chat media is out of scope and deferred to a future release.
- **FR-019**: System MUST generate a small thumbnail image (a few KB) for image and video messages on the sender side, and embed it in the encrypted media payload. Recipients MUST display this thumbnail instantly as a preview while the full media loads/decrypts.

### Key Entities

- **MediaPayload**: Represents the encrypted bundle containing media bytes, caption text, filename, MIME type, and an embedded thumbnail — all encrypted together as a single payload.
- **MediaMessage**: A chat message with content type of Image, Video, Audio, or File, displayed with type-appropriate UI rendering (thumbnail, player, or file card).
- **BlobUrlRegistry**: Tracks active display URLs per chat session for proper lifecycle management and cleanup.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can send and receive images, videos, and documents with 100% delivery reliability (no silent failures or missing media).
- **SC-002**: Media messages with captions display as a single unified message bubble (caption + media together).
- **SC-003**: App memory usage after sending 20 media messages remains within 2x of baseline (no unbounded memory growth from retained byte arrays).
- **SC-004**: All media display URLs are cleaned up when leaving a chat or deleting messages (zero resource leaks per session).
- **SC-005**: Media unit test suite achieves at least 90% code coverage on media send/receive/encrypt/decrypt paths.
- **SC-006**: File size validation prevents uploads over the limit with a user-friendly error message shown within 1 second of file selection.
- **SC-007**: Recipients can view images in full-screen and play videos inline within 2 seconds of tapping.
- **SC-008**: Document files can be downloaded with original filename preserved in 100% of cases.
