# Feature Specification: Fix All Chat Functions

**Feature Branch**: `005-fix-chat-functions`
**Created**: 2026-03-04
**Status**: Draft
**Input**: User description: "Fix sending encrypted images, videos, files — all have issues. Clear chat button does not work. Some chat features have issues. Fix all chat functions."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send and Receive Encrypted Media (Priority: P1)

A user selects an image, video, audio, or file from their device, sees a preview, and sends it to a conversation. The recipient receives the media, decrypts it, and views it correctly as the original format (image renders, video plays, audio plays, file downloads).

**Why this priority**: Core messaging feature. Without working media, the app is text-only and fundamentally broken for modern messaging use cases.

**Independent Test**: Send an image from one device to another. Sender sees preview before sending. Recipient sees the image rendered correctly in the chat bubble.

**Acceptance Scenarios**:

1. **Given** a user has a conversation open, **When** they select a JPEG image and tap send, **Then** the image appears as a preview before sending, sends successfully, and the recipient sees the image rendered in a chat bubble.
2. **Given** a user sends an HEIC image from an iPhone, **When** the recipient opens the conversation, **Then** the image displays correctly (or a compatible fallback is shown).
3. **Given** a user selects a video file, **When** they send it, **Then** the recipient can play the video inline.
4. **Given** a user records a voice message, **When** the recipient receives it, **Then** the audio plays correctly with waveform and duration.
5. **Given** a user sends a PDF document, **When** the recipient receives it, **Then** they see a file card with the filename and can download it.
6. **Given** a user forwards a media message to another conversation, **When** the forward completes, **Then** the media is intact and viewable in the new conversation.

---

### User Story 2 - Clear Chat History (Priority: P2)

A user wants to clear their chat history for a specific conversation. They tap a "clear chat" option, choose a time range (last hour, last day, all), and the messages are removed from their view and local storage. Server-side cleanup is also requested.

**Why this priority**: Users expect to manage their message history. A broken clear-chat feature causes frustration and gives the impression of a buggy app.

**Independent Test**: Open a conversation with messages, tap clear chat, select "all messages", confirm the messages disappear and don't return on page refresh.

**Acceptance Scenarios**:

1. **Given** a conversation has 50 messages, **When** the user selects "Clear all messages", **Then** all messages are removed from the UI, local storage, and a server request is sent.
2. **Given** a conversation has messages with reactions, **When** the user clears messages from the last hour, **Then** only reactions belonging to deleted messages are removed; reactions on older messages remain.
3. **Given** the server clear request fails, **When** the user refreshes the page, **Then** the messages reappear (local-only deletion is rolled back or the user is notified of the failure).

---

### User Story 3 - Context Menu Actions (Priority: P2)

A user long-presses or right-clicks a message to open a context menu with options: Reply, Copy, Forward, Delete for me, Delete for everyone. Each action operates on the correct message and provides feedback.

**Why this priority**: Context menu is the primary interaction model for message management. Broken actions reduce trust in the app.

**Independent Test**: Right-click a message, select "Delete for me", confirm the correct message is removed.

**Acceptance Scenarios**:

1. **Given** a user right-clicks a sent message, **When** they select "Delete for me", **Then** that specific message is removed from their view and local storage.
2. **Given** a user is composing a reply to a message, **When** they delete that message via context menu, **Then** the reply preview is also cleared.
3. **Given** a user selects "Forward" on a media message, **When** they choose a target conversation, **Then** the media is re-encrypted and sent to the target conversation intact.
4. **Given** a user right-clicks while a context menu is already open, **When** they click on a different message, **Then** the old menu closes and the new menu opens for the correct message.

---

### User Story 4 - Audio Playback in Chat (Priority: P3)

A user taps play on a received voice message. The waveform animates, the duration counter updates, and the audio plays through to completion or until paused.

**Why this priority**: Audio messages are a popular feature, but playback issues are less critical than inability to send/receive media.

**Independent Test**: Receive a voice message, tap play, audio plays with progress indicator and correct duration.

**Acceptance Scenarios**:

1. **Given** a voice message is displayed in the chat, **When** the user taps play, **Then** the audio plays and the waveform/timer update in real time.
2. **Given** an audio message is playing, **When** the user taps pause, **Then** playback stops and can be resumed.

---

### User Story 5 - Memory Efficiency for Media Messages (Priority: P3)

After a media message is decrypted and displayed (via a blob URL), the raw decrypted bytes should not remain in memory unnecessarily.

**Why this priority**: Long conversations with many media messages can cause the browser tab to run out of memory and crash.

**Independent Test**: Open a conversation with 20+ images, verify browser memory usage stays reasonable.

**Acceptance Scenarios**:

1. **Given** a media message has been decrypted and a blob URL created, **When** the message is rendered, **Then** the raw byte array reference is released from the message object (unless needed for persistence).

---

### Edge Cases

- What happens when a user sends a 15 MB file (maximum allowed size)?
- What happens when decryption fails for a media message? (Should show a placeholder, not crash)
- What happens when a user clears chat while a message is being sent?
- What happens when a blob URL is revoked before a forward operation? (User should be notified)
- What happens when the user has no internet during clear chat?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST correctly transfer binary data between the application layer and browser for all media types (images, video, audio, files).
- **FR-002**: System MUST display a working preview of selected media before the user sends it.
- **FR-003**: System MUST correctly encrypt binary media data and transmit it to the recipient, who decrypts and renders it in its original format.
- **FR-004**: System MUST support clearing chat messages by time range (last hour, last day, all) and remove associated data (reactions, local storage entries).
- **FR-005**: System MUST only remove reactions for messages that are actually deleted during a clear operation, preserving reactions for retained messages.
- **FR-006**: System MUST notify the user if a server-side clear operation fails, rather than silently discarding messages that the server still holds.
- **FR-007**: Context menu actions (delete, forward, reply, copy) MUST operate on the correct message, even after re-renders or list reordering.
- **FR-008**: System MUST clear the reply preview if the message being replied to is deleted.
- **FR-009**: Forwarding a media message MUST re-encrypt and send the full media payload to the target conversation.
- **FR-010**: If a media forward cannot retrieve the original bytes (e.g., expired reference), the system MUST notify the user rather than silently falling back to text-only.
- **FR-011**: Audio playback controls MUST function safely without relying on dynamic code evaluation.
- **FR-012**: System SHOULD release raw decrypted byte arrays from message objects after display URLs are created, to reduce memory pressure.

### Key Entities

- **ChatMessage**: In-memory representation of a decrypted message with text, media display URL, content type, reactions, reply context, and delivery status.
- **MessageEnvelope**: Server-side encrypted message container with ciphertext, content type metadata, file name, and MIME type.
- **StoredMessage**: Locally-persisted message with encoded media, used for offline cache.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can send and receive images, videos, audio, and files without corruption — media renders correctly 100% of the time for supported formats.
- **SC-002**: Clear chat removes the correct messages and only their associated reactions; unaffected messages and reactions remain intact.
- **SC-003**: All context menu actions operate on the intended message with zero mis-targeting.
- **SC-004**: Media forwarding delivers the full media payload to the recipient in the target conversation.
- **SC-005**: Voice message playback starts, pauses, and shows accurate progress without errors.
- **SC-006**: Browser memory usage does not grow unboundedly with the number of media messages viewed in a single session.
- **SC-007**: All 215+ existing tests continue to pass after fixes are applied.

## Clarifications

### Session 2026-03-04

- Q: What fallback should be shown for unsupported image formats (like iPhone HEIC)? → A: Show a download link with the original filename (no inline preview)
- Q: Should group messaging be included in scope? → A: Group messaging is out of scope - focus only on 1:1 fixes
- Q: When clear chat server operation fails, what should happen? → A: Show error toast and keep messages visible in UI
- Q: What should be the memory limit threshold for media messages? → A: Limit memory to 500MB for 50+ media messages
