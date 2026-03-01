# Feature Specification: Hybrid Post-Quantum Secure Messaging

**Feature Branch**: `001-secure-messaging`
**Created**: 2026-02-25
**Status**: Draft
**Input**: User description: "Secure messaging chat application with hybrid post-quantum cryptography protecting conversations against current and future quantum computing threats"

## Clarifications

### Session 2026-02-25

- Q: What happens when a user loses all their devices (account recovery)? → A: No recovery is supported. Losing all devices means creating a new identity. Contacts see a key change warning. This aligns with Signal's approach and the zero-trust server model — no key escrow or server-side recovery mechanism exists.
- Q: What message content types are supported? → A: Text-only for MVP (P1-P2 stories). The architecture MUST be extensible to support images, audio, and file attachments in future iterations without protocol changes. Media would be encrypted as blobs using the same per-message keys.
- Q: Can a newly linked device access messages sent before it was linked? → A: No. A new device only receives messages sent after linking. Past messages remain on the original device only. This aligns with forward secrecy (past message keys are deleted) and the zero-trust server model.
- Q: Are there rate limits or abuse prevention measures? → A: Yes. Server-enforced rate limits on registration (per IP), message sending (per user/minute), and search queries. Standard practice for production messaging services.
- Q: What is the maximum number of linked devices per user? → A: Maximum 10 linked devices. Each message is encrypted once per recipient device, so this caps fan-out at 10x per recipient.

### Session 2026-02-26

- Q: How long should the server retain undelivered encrypted messages for offline recipients? → A: 90 days, then auto-delete. Messages queued for offline users are purged after 90 days if still undelivered.
- Q: What authentication token mechanism should the server use for API requests? → A: JWT with short-lived access tokens (15 min) + refresh tokens. Each device holds its own token pair; stateless validation on the server.
- Q: What are the display name format constraints? → A: 3–32 characters, alphanumeric plus underscores and hyphens, case-insensitive uniqueness.
- Q: What is the target availability for the messaging service? → A: 99.5% uptime (~44 hrs downtime/year). Single-server deployment with monitoring and automated restarts for MVP.
- Q: Which group encryption protocol should be used? → A: Sender Keys (Signal-style). Each member generates a sender key distributed via pairwise sessions. O(1) encrypt per send, key rotation on membership change.

### Session 2026-02-26 (2)

- Q: What level of server observability is required? → A: Standard — `/health` endpoint, structured request logging via Serilog, and basic error tracking. No full metrics/tracing stack for MVP.
- Q: What is the maximum plaintext message size? → A: 64 KB (~16,000 words). Server and client MUST validate and reject messages exceeding this limit.
- Q: How can a user voluntarily deactivate their account? → A: Deactivation with 7-day grace period. User initiates deletion in Settings, confirms. Account enters pending-deletion state for 7 days (login re-activates). After 7 days, account is permanently deactivated, keys revoked, contacts see key change warning.
- Q: Should the app send browser notifications for new messages? → A: Yes, via Browser Notification API. Show desktop notifications when tab is unfocused/minimized (requires user permission). No service worker push notifications when browser is closed — that is deferred post-MVP.
- Q: How should the app handle multiple browser tabs open simultaneously? → A: Leader election via BroadcastChannel API. One tab owns the SignalR connection and crypto state (IndexedDB writes). Other tabs receive message updates via BroadcastChannel. If the leader tab closes, another tab promotes itself to leader.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Account Registration and Key Generation (Priority: P1)

A new user opens the application, creates an account with a display name and credentials, and the system automatically generates all required cryptographic identity material (classical and post-quantum key pairs). The user's identity keys and a bundle of pre-keys are published to the server so other users can initiate secure conversations. The user sees a confirmation that their account is ready and can view their security fingerprint for out-of-band verification.

**Why this priority**: Without account creation and key generation, no other functionality is possible. This is the foundational identity layer upon which all messaging depends.

**Independent Test**: Can be fully tested by registering a new user and verifying that the server holds a valid pre-key bundle; delivers the value of a verified cryptographic identity.

**Acceptance Scenarios**:

1. **Given** the user has no account, **When** they provide a valid display name and password, **Then** the system creates an account, generates an identity key pair (classical + post-quantum), generates signed pre-keys and one-time pre-keys, publishes the pre-key bundle to the server, and displays a confirmation with the user's security fingerprint.
2. **Given** the user submits an already-taken display name, **When** they attempt registration, **Then** the system rejects the request with a clear error and does not generate any keys.
3. **Given** the user provides a weak password (fewer than 12 characters), **When** they attempt registration, **Then** the system rejects the request and explains password requirements.

---

### User Story 2 - Initiating a Secure Conversation (Priority: P1)

A registered user searches for another registered user by display name, selects them, and initiates a new conversation. The system performs a hybrid key exchange (combining classical X3DH with post-quantum KEM) to establish an initial shared secret. The first message is encrypted using the derived session key and delivered in real-time. The recipient sees the incoming message appear without any manual key exchange steps.

**Why this priority**: The core value proposition of the application. Without the ability to establish a secure session and exchange messages, there is no product.

**Independent Test**: Can be tested by having two registered users where User A sends a message to User B; verify the message arrives encrypted, is decrypted correctly by User B, and the server never sees plaintext.

**Acceptance Scenarios**:

1. **Given** User A and User B both have accounts with published pre-key bundles, **When** User A initiates a conversation with User B and sends a message, **Then** the system fetches User B's pre-key bundle, performs a hybrid key exchange (classical + post-quantum), encrypts the message, delivers it in real-time, and User B decrypts and reads it.
2. **Given** User A initiates a conversation with User B who is offline, **When** User A sends a message, **Then** the message is encrypted and stored on the server; when User B comes online, the message is delivered and decrypted successfully.
3. **Given** User A has an active session with User B, **When** User A sends multiple messages, **Then** each message uses a unique encryption key derived via the ratcheting mechanism, providing forward secrecy.

---

### User Story 3 - Real-Time Message Exchange (Priority: P1)

Two users with an established secure session exchange messages back and forth in real-time. Each message is individually encrypted with a unique key via the Double Ratchet mechanism. Messages appear instantly on both sides. Message ordering is preserved. Both users see delivery status indicators (sent, delivered, read).

**Why this priority**: Real-time messaging is the core interaction loop. Users expect instant delivery with visible feedback, making this essential to the baseline product experience.

**Independent Test**: Can be tested by two users in an active session exchanging 10+ messages rapidly; verify all arrive in order, each encrypted with a distinct key, and delivery status updates in real-time.

**Acceptance Scenarios**:

1. **Given** two users have an established secure session, **When** User A sends a message, **Then** User B receives it within 2 seconds and both sides see updated delivery status.
2. **Given** two users are exchanging messages, **When** the conversation includes 100+ messages, **Then** all messages remain individually decryptable and ordered correctly.
3. **Given** User A sends a message, **When** User B's connection is temporarily interrupted, **Then** the message is queued and delivered when User B reconnects, with correct ordering preserved.

---

### User Story 4 - Security Verification Between Users (Priority: P2)

Two users who have an established conversation can verify each other's identity by comparing security fingerprints (safety numbers). Each user can view a visual fingerprint derived from both users' identity keys. They can mark the conversation as "verified" after confirming fingerprints match via an out-of-band channel (e.g., in person, phone call).

**Why this priority**: Identity verification prevents man-in-the-middle attacks. While the system is functional without it, it is essential for high-assurance use cases.

**Independent Test**: Can be tested by two users comparing displayed fingerprints and marking the conversation as verified; verify that a key change triggers a warning.

**Acceptance Scenarios**:

1. **Given** User A and User B have an active conversation, **When** User A views the security info screen, **Then** they see a fingerprint derived from both users' identity keys that matches what User B sees on their screen.
2. **Given** User A has verified User B, **When** User B's identity key changes (e.g., new device), **Then** User A sees a prominent warning that the security fingerprint has changed and the conversation is marked as unverified.

---

### User Story 5 - Multi-Device Support (Priority: P2)

A user who has registered on one device can link an additional device (e.g., web browser alongside mobile). The new device generates its own key pairs and registers them with the server. Messages sent to that user are encrypted for all linked devices. Each device maintains its own ratchet state independently.

**Why this priority**: Users expect to access their messages from multiple devices. This is a standard expectation for modern messaging applications.

**Independent Test**: Can be tested by linking a second device to an existing account, then verifying that new messages arrive and decrypt on both devices.

**Acceptance Scenarios**:

1. **Given** a user has one registered device, **When** they link a second device, **Then** the new device generates its own key material, registers pre-keys with the server, and receives only messages sent after linking — no historical message sync occurs.
2. **Given** a user has two linked devices, **When** a contact sends a message, **Then** both devices receive and independently decrypt the message.
3. **Given** a user removes a linked device, **When** new messages arrive, **Then** the removed device no longer receives messages and its keys are revoked from the server.

---

### User Story 6 - Group Messaging (Priority: P3)

A user creates a group conversation with multiple participants. The group uses a shared encryption key that is distributed securely to all members via pairwise encrypted channels. When members are added or removed, the group key is rotated. All group messages are end-to-end encrypted.

**Why this priority**: Group messaging extends the core value but depends on stable 1:1 messaging. It adds complexity in key management and is not required for initial value delivery.

**Independent Test**: Can be tested by creating a group of 3+ users, exchanging messages, adding/removing a member, and verifying all participants can decrypt only the messages they should see.

**Acceptance Scenarios**:

1. **Given** User A creates a group with Users B and C, **When** User A sends a group message, **Then** both User B and User C receive and decrypt the message.
2. **Given** an active group, **When** a new member is added, **Then** they can read new messages but cannot decrypt messages sent before they joined.
3. **Given** an active group, **When** a member is removed, **Then** the group key is rotated and the removed member cannot decrypt new messages.

---

### User Story 7 - Message Disappearing and Retention Control (Priority: P3)

A user sets a disappearing message timer on a conversation (e.g., 24 hours, 7 days). After the timer expires, messages are automatically deleted from both sender and recipient devices. Users can also manually delete individual messages from their own device.

**Why this priority**: Privacy-conscious users expect ephemeral messaging. This supports the security posture but is not required for core messaging functionality.

**Independent Test**: Can be tested by enabling a 1-minute timer, sending messages, and verifying they are automatically deleted after expiry on both sides.

**Acceptance Scenarios**:

1. **Given** a conversation with a 24-hour disappearing timer enabled, **When** a message is older than 24 hours, **Then** it is automatically deleted from both devices.
2. **Given** a user manually deletes a message, **When** the deletion is processed, **Then** the message is removed from their device only (the recipient retains their copy).

---

### Edge Cases

- What happens when a user's one-time pre-keys are exhausted? The system falls back to the signed pre-key for new session establishment and generates a new batch of one-time pre-keys in the background.
- What happens when a message fails to decrypt (corrupted or tampered)? The system discards the message, notifies the recipient that a message could not be decrypted, and logs the event for diagnostics without exposing any key material.
- What happens during a network partition mid-conversation? Messages are queued locally, encrypted, and sent when connectivity resumes. Message ordering is maintained via sequence numbers.
- How does the system handle clock skew between devices? Message timestamps use server-assigned ordering for sequencing; display timestamps use device-local time with server time as fallback.
- What happens if the server is compromised? The server only stores encrypted ciphertext and public keys. No plaintext, private keys, or session keys are ever transmitted to or stored on the server. A server compromise reveals no message content.
- What happens when a user attempts to message a deactivated account? The system informs the sender that the recipient is no longer available and prevents message delivery.
- What happens when a user loses all their devices? The user must create a new account and new identity. There is no server-side account recovery. Contacts are notified via a key change warning when the user re-registers, and previous message history is permanently lost.
- What happens when a user sends messages too rapidly or a bot spams registrations? The server enforces rate limits per IP (registration) and per user (message sending, search). Requests exceeding the limit receive an error response and are temporarily throttled.
- What happens when the same user opens multiple browser tabs? Leader election via BroadcastChannel API ensures only one tab owns the SignalR connection and crypto state. Other tabs act as followers receiving updates. If the leader tab closes, a follower promotes itself to leader automatically.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users to create accounts with a display name and password, generating all required cryptographic identity material upon registration. Authentication MUST use JWT with short-lived access tokens (15-minute expiry) and refresh tokens. Each linked device maintains its own token pair independently.
- **FR-002**: System MUST generate a hybrid identity consisting of both classical (curve-based) and post-quantum key pairs for each user at registration time.
- **FR-003**: System MUST publish pre-key bundles (identity key, signed pre-key, and a set of one-time pre-keys) to the server for each registered user.
- **FR-004**: System MUST perform a hybrid key exchange combining classical Diffie-Hellman operations with a post-quantum key encapsulation mechanism when establishing a new session between two users.
- **FR-005**: System MUST encrypt every message individually using authenticated encryption with a unique per-message key derived from a forward-secret ratcheting mechanism.
- **FR-018**: System MUST support text messages as the primary content type. The message format MUST be extensible to accommodate additional content types without requiring protocol changes. Media content types (Image, Video, Audio, File) are supported per FR-023.
- **FR-019**: System MUST enforce a maximum plaintext message size of 64 KB. Both client and server MUST validate and reject messages exceeding this limit before encryption or storage.
- **FR-020**: System MUST allow users to self-deactivate their account via Settings. Upon initiating deletion, the account enters a pending-deletion state with a 7-day grace period. Logging in during the grace period re-activates the account. After 7 days, the account is permanently deactivated: all device keys are revoked, the user cannot log in, and contacts see a key change warning.
- **FR-021**: System MUST use the Browser Notification API to show desktop notifications for incoming messages when the application tab is unfocused or minimized. Notification display MUST require explicit user permission. Notification content MUST show sender name and a generic alert (not decrypted message preview) to protect privacy.
- **FR-022**: System MUST implement leader election via BroadcastChannel API for multiple browser tabs. One leader tab owns the SignalR connection and all crypto/IndexedDB write operations. Follower tabs receive message updates via BroadcastChannel. If the leader tab closes, a follower MUST promote itself to leader and establish a new SignalR connection.
- **FR-006**: System MUST support real-time message delivery between online users with delivery and read status indicators.
- **FR-007**: System MUST queue and deliver messages for offline recipients when they reconnect, preserving message ordering. Undelivered messages MUST be automatically purged after 90 days.
- **FR-008**: System MUST perform all encryption and decryption operations exclusively on the client device; the server MUST never have access to plaintext messages or private keys.
- **FR-009**: System MUST provide a security fingerprint (safety number) for each conversation that both participants can compare for identity verification.
- **FR-010**: System MUST warn users when a contact's identity key changes, indicating a potential security event.
- **FR-011**: System MUST support linking up to 10 devices to a single account, with each device maintaining independent key material and ratchet state.
- **FR-012**: System MUST support group conversations with end-to-end encryption using the Sender Keys protocol (Signal-style). Each group member generates a sender key distributed to other members via pairwise encrypted sessions. Messages are encrypted O(1) per send. Group key MUST be rotated when membership changes.
- **FR-013**: System MUST support disappearing messages with configurable timers per conversation.
- **FR-014**: System MUST allow users to search for other registered users by display name to initiate conversations.
- **FR-015**: System MUST automatically replenish one-time pre-keys when the supply on the server runs low.
- **FR-016**: System MUST provide forward secrecy — compromise of long-term keys MUST NOT allow decryption of past messages.
- **FR-017**: System MUST provide post-compromise security — after a key compromise is detected and new keys are established, future messages MUST be protected.
- **FR-023**: System MUST support media message types: Image, Video, Audio, and File. Media content is encrypted as part of the message payload using the same per-message keys. Images are displayed inline with click-to-expand. Videos and audio use native browser playback controls. Files display a download card. Media data MUST be persisted to IndexedDB (base64-encoded) for offline access.
- **FR-024**: System MUST provide an opt-in (default ON) encrypted key backup feature. Identity keys (classical + post-quantum) are encrypted client-side using AES-256-GCM with a key derived from the user's password via PBKDF2 (100,000 iterations, SHA-256). The encrypted blob, salt, and nonce are uploaded to the server. On new device login, the system attempts to restore identity keys from the backup, generating fresh pre-keys and one-time pre-keys per device. Users can disable this feature in Settings, which deletes the server-side backup. See Constitution Exception CE-001.
- **FR-025**: System MUST support conversation search (sidebar search filtering by display name) and in-conversation message search (highlight matching messages, navigate between results).
- **FR-026**: System MUST support message reactions (emoji reactions with per-user toggle, aggregated badge display), message replies (quote the original message with sender name and preview text, click-to-scroll to original), and message forwarding (forward a message to another conversation with "Forwarded" label).
- **FR-027**: System MUST support link previews for URLs detected in message text. When a message contains a URL, the system displays a preview card below the message text.

### Non-Functional Requirements

- **NFR-001**: Key exchange operations MUST complete in under 500 milliseconds on standard consumer hardware.
- **NFR-002**: Individual message encryption/decryption MUST complete in under 50 milliseconds.
- **NFR-003**: The hybrid cryptographic approach MUST add no more than approximately 1 KB of overhead per message compared to classical-only encryption.
- **NFR-004**: The system MUST support at least 10,000 concurrent connected users without performance degradation.
- **NFR-005**: Messages MUST be delivered to online recipients within 2 seconds under normal network conditions.
- **NFR-006**: The system MUST work across web browsers, desktop applications, and mobile devices.
- **NFR-007**: All cryptographic operations MUST use well-audited, established libraries — no custom cryptographic primitives.
- **NFR-008**: The system MUST be open-source with transparent, auditable security implementation.
- **NFR-009**: The server MUST enforce rate limits on registration (per IP), message sending (per user per minute), and user search queries to prevent abuse and denial-of-service.
- **NFR-010**: The system MUST target 99.5% uptime (~44 hours downtime/year). MVP uses a single-server deployment with health monitoring and automated restarts.
- **NFR-011**: The server MUST expose a `/health` endpoint for uptime monitoring, use structured logging (Serilog) for all request/error tracking, and provide basic error tracking. No plaintext message content or private key material may appear in logs (per constitution security requirement).

### Key Entities

- **User**: A registered individual with a unique display name (3–32 characters, alphanumeric/underscores/hyphens, case-insensitive uniqueness), password hash, and cryptographic identity. Has one or more linked devices. Can participate in conversations. State transitions: Active → PendingDeletion (7-day grace period, login re-activates) → Deactivated (permanent, keys revoked).
- **Identity Key Pair**: A long-term key pair (both classical and post-quantum variants) that uniquely identifies a user/device. Used for authentication and as a root of trust.
- **Pre-Key Bundle**: A published set containing the identity public key, a signed pre-key, and a collection of one-time pre-keys. Used by other users to initiate sessions asynchronously.
- **Session**: A secure communication channel between two users/devices, established via hybrid key exchange. Contains ratchet state for deriving per-message keys.
- **Message**: An encrypted payload sent within a session. Contains ciphertext, authentication tag, sender identifier, timestamp, and sequence number. Plaintext is never stored on the server. Maximum plaintext size: 64 KB.
- **Conversation**: A logical grouping of messages between two users (1:1) or among multiple users (group). Has associated metadata like disappearing timer settings and verification status.
- **Device**: A user's registered client (web, desktop, or mobile). Each device has its own key material and maintains independent session/ratchet state. A user may link up to 10 devices.
- **Group**: A multi-participant conversation using Sender Keys protocol. Each member holds a sender key distributed via pairwise encrypted sessions for O(1) message encryption. Membership changes trigger sender key rotation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can register an account and be ready to send their first message within 60 seconds, including all key generation.
- **SC-002**: Two users can establish a secure conversation and exchange their first message within 5 seconds of initiating contact.
- **SC-003**: 100% of messages are end-to-end encrypted; the server stores zero plaintext messages at any time.
- **SC-004**: Users experience no perceptible delay when sending or receiving messages (sub-2-second delivery for online recipients).
- **SC-005**: The system maintains message confidentiality even if the server is fully compromised — verified via security audit.
- **SC-006**: A compromised session key does not allow decryption of any past messages — verified by forward secrecy testing.
- **SC-007**: The system remains secure against known quantum computing attacks — verified by using NIST-standardized post-quantum algorithms.
- **SC-008**: 95% of users can successfully complete account creation and send their first message without external assistance.
- **SC-009**: The system supports simultaneous use from at least 2 devices per user without message loss or decryption failures.
- **SC-010**: Group conversations support at least 100 participants with all members able to decrypt messages correctly.

## Out of Scope

- Voice and video calling
- Contact list management or phone number-based discovery
- User profile pictures or status updates
- Push notifications via service worker (when browser is fully closed)
- Read-only message archival or export

### Previously Out of Scope — Now Implemented

- ~~Image, audio, video, and file attachments~~ → Implemented (see FR-023)
- ~~Account recovery or key backup/export~~ → Implemented as opt-in encrypted key backup (see FR-024)
- ~~Message search across conversations~~ → Implemented as conversation-level search and sidebar filtering (see FR-025)

## Assumptions

- Users have access to a modern web browser, desktop application, or mobile device with sufficient processing power for cryptographic operations.
- Network connectivity is intermittent but generally available; the system must handle offline scenarios gracefully.
- The server infrastructure is untrusted by design — the security model assumes a potentially compromised server.
- Classical cryptographic algorithms (X25519, Ed25519, AES-256-GCM) are currently secure and will remain so for the near term.
- NIST-standardized post-quantum algorithms (CRYSTALS-Kyber / ML-KEM, CRYSTALS-Dilithium / ML-DSA) provide adequate protection against future quantum attacks based on current academic consensus.
- Users are willing to complete a one-time registration process before messaging.
- Display names are unique across the system (case-insensitive) for user discovery purposes. Names must be 3–32 characters, containing only alphanumeric characters, underscores, and hyphens.
- The "belt and suspenders" hybrid approach means both classical and post-quantum layers must be independently broken for total compromise — if either remains secure, message confidentiality is preserved.
