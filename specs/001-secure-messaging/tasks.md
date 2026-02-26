# Tasks: Hybrid Post-Quantum Secure Messaging

**Input**: Design documents from `/specs/001-secure-messaging/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included per Constitution Principle VI (Test-First Development). Write tests FIRST, verify they FAIL, then implement.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. The Crypto Library and Server Infrastructure phases are foundational prerequisites since all user stories depend on them.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Project Initialization)

**Purpose**: Solution structure, dependencies, and configuration

- [X] T001 Create solution file and project structure with 5 source projects (ToledoMessage, ToledoMessage.Client, ToledoMessage.Crypto, ToledoMessage.Shared, Toledo.SharedKernel) and 4 test projects (ToledoMessage.Crypto.Tests, ToledoMessage.Client.Tests, ToledoMessage.Server.Tests, ToledoMessage.Integration.Tests) plus ToledoMessage.Benchmarks
- [X] T002 [P] Configure NuGet dependencies: BouncyCastle.Cryptography 2.6.2 in src/ToledoMessage.Crypto/ToledoMessage.Crypto.csproj, EF Core 10 + Identity + JWT Bearer in src/ToledoMessage/ToledoMessage.csproj, SignalR Client in src/ToledoMessage.Client/ToledoMessage.Client.csproj
- [X] T003 [P] Configure xUnit + BenchmarkDotNet in test projects: tests/ToledoMessage.Crypto.Tests/ToledoMessage.Crypto.Tests.csproj, tests/ToledoMessage.Benchmarks/ToledoMessage.Benchmarks.csproj
- [X] T004 [P] Define protocol constants (key sizes, batch sizes, rate limits, HKDF info strings, max message size 64 KB, account deletion grace period 7 days) in src/ToledoMessage.Shared/Constants/ProtocolConstants.cs
- [X] T005 [P] Define enums (MessageType, ContentType, ConversationType, DeliveryStatus, ParticipantRole, UserStatus) in src/ToledoMessage.Shared/Enums/
- [X] T006 [P] Define all shared DTOs (RegisterRequest, LoginRequest, AuthResponse, RefreshTokenRequest, RefreshTokenResponse, AccountDeletionResponse, DeviceRegistrationRequest, SendMessageRequest, MessageEnvelope, PreKeyBundleResponse, UserSearchResult, etc.) in src/ToledoMessage.Shared/DTOs/
- [X] T007 [P] Create DecimalTools helper for snowflake-style ID generation in src/Toledo.SharedKernel/Helpers/DecimalTools.cs

---

## Phase 2: Foundational — Crypto Library (Blocking Prerequisites)

**Purpose**: Cryptographic primitives and protocol implementation. MUST complete before ANY user story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests (write FIRST, verify they FAIL)

- [X] T008 [P] Write AES-256-GCM encrypt/decrypt round-trip tests, tamper detection tests, and nonce uniqueness tests in tests/ToledoMessage.Crypto.Tests/Classical/AesGcmCipherTests.cs
- [X] T009 [P] Write Ed25519 sign/verify tests (valid signature, invalid signature, wrong key) in tests/ToledoMessage.Crypto.Tests/Classical/Ed25519SignerTests.cs
- [X] T010 [P] Write X25519 key exchange tests (shared secret agreement, different keys produce different secrets) in tests/ToledoMessage.Crypto.Tests/Classical/X25519KeyExchangeTests.cs
- [X] T011 [P] Write ML-KEM-768 encapsulate/decapsulate tests (round-trip, wrong key rejection) in tests/ToledoMessage.Crypto.Tests/PostQuantum/MlKemKeyExchangeTests.cs
- [X] T012 [P] Write ML-DSA-65 sign/verify tests (valid, invalid, wrong key) in tests/ToledoMessage.Crypto.Tests/PostQuantum/MlDsaSignerTests.cs
- [X] T013 [P] Write hybrid key exchange tests (combined classical+PQ, verify both layers contribute to shared secret) in tests/ToledoMessage.Crypto.Tests/Hybrid/HybridKeyExchangeTests.cs
- [X] T014 [P] Write hybrid key derivation tests (HKDF-SHA256 with domain separation, deterministic output) in tests/ToledoMessage.Crypto.Tests/Hybrid/HybridKeyDerivationTests.cs
- [X] T015 [P] Write hybrid signer tests (combined Ed25519+ML-DSA, both signatures verified) in tests/ToledoMessage.Crypto.Tests/Hybrid/HybridSignerTests.cs
- [X] T016 [P] Write key management tests (identity key generation, pre-key generation, fingerprint generation) in tests/ToledoMessage.Crypto.Tests/KeyManagement/KeyManagementTests.cs
- [X] T017 [P] Write X3DH protocol tests (initiator/responder agree on shared secret, with and without OTP) in tests/ToledoMessage.Crypto.Tests/Protocol/X3dhTests.cs
- [X] T018 [P] Write Double Ratchet tests (forward secrecy, message key uniqueness, out-of-order decryption) in tests/ToledoMessage.Crypto.Tests/Protocol/DoubleRatchetTests.cs
- [X] T019 [P] Write message keys tests (per-message key derivation, chain key advancement) in tests/ToledoMessage.Crypto.Tests/Protocol/MessageKeysTests.cs

### Implementation — Classical Primitives

- [X] T020 [P] Implement AES-256-GCM encrypt/decrypt with BouncyCastle (unique nonce per call, authentication tag validation) in src/ToledoMessage.Crypto/Classical/AesGcmCipher.cs
- [X] T021 [P] Implement Ed25519 key generation, sign, verify with BouncyCastle in src/ToledoMessage.Crypto/Classical/Ed25519Signer.cs
- [X] T022 [P] Implement X25519 key generation and Diffie-Hellman shared secret computation with BouncyCastle in src/ToledoMessage.Crypto/Classical/X25519KeyExchange.cs

### Implementation — Post-Quantum Primitives

- [X] T023 [P] Implement ML-KEM-768 key generation, encapsulate, decapsulate with BouncyCastle in src/ToledoMessage.Crypto/PostQuantum/MlKemKeyExchange.cs
- [X] T024 [P] Implement ML-DSA-65 key generation, sign, verify with BouncyCastle in src/ToledoMessage.Crypto/PostQuantum/MlDsaSigner.cs

### Implementation — Hybrid Operations

- [X] T025 Implement hybrid key exchange (X25519 + ML-KEM-768, combine shared secrets via HKDF) in src/ToledoMessage.Crypto/Hybrid/HybridKeyExchange.cs (depends on T022, T023)
- [X] T026 [P] Implement HKDF-SHA256 key derivation with domain separation (RootKey, ChainKey, MessageKey info strings) in src/ToledoMessage.Crypto/Hybrid/HybridKeyDerivation.cs
- [X] T027 Implement hybrid signer (Ed25519 + ML-DSA-65, concatenated signatures, both-must-verify) in src/ToledoMessage.Crypto/Hybrid/HybridSigner.cs (depends on T021, T024)

### Implementation — Key Management

- [X] T028 Implement identity key generator (classical X25519+Ed25519 pair + post-quantum ML-KEM+ML-DSA pair) in src/ToledoMessage.Crypto/KeyManagement/IdentityKeyGenerator.cs (depends on T021-T024)
- [X] T029 Implement pre-key generator (signed pre-key with hybrid signature, one-time pre-key batch of 100) in src/ToledoMessage.Crypto/KeyManagement/PreKeyGenerator.cs (depends on T027)
- [X] T030 [P] Implement fingerprint generator (safety number derivation from both users' identity public keys) in src/ToledoMessage.Crypto/KeyManagement/FingerprintGenerator.cs

### Implementation — Signal Protocol

- [X] T031 Define RatchetState data structure (root key, sending/receiving chain keys, message counters, DH ratchet keys) in src/ToledoMessage.Crypto/Protocol/RatchetState.cs
- [X] T032 Define PreKeyBundle data structure (identity keys, signed pre-key, OTP, Kyber pre-key) in src/ToledoMessage.Crypto/Protocol/PreKeyBundle.cs
- [X] T033 Implement X3DH initiator (fetch bundle, compute 4 DH operations + PQ KEM, derive initial root key) in src/ToledoMessage.Crypto/Protocol/X3dhInitiator.cs (depends on T025, T028)
- [X] T034 Implement X3DH responder (accept PreKeyMessage, reconstruct shared secret, initialize ratchet) in src/ToledoMessage.Crypto/Protocol/X3dhResponder.cs (depends on T025, T028)
- [X] T035 Implement message key derivation (chain key → message key + next chain key) in src/ToledoMessage.Crypto/Protocol/MessageKeys.cs (depends on T026)
- [X] T036 Implement Double Ratchet algorithm (symmetric ratchet step, DH ratchet step, encrypt/decrypt with forward secrecy) in src/ToledoMessage.Crypto/Protocol/DoubleRatchet.cs (depends on T025, T026, T035)

### Performance Benchmarks

- [X] T037 Implement crypto benchmarks (key exchange, message encrypt/decrypt, key generation) validating NFR targets (<500ms KX, <50ms encrypt, <1KB overhead) in tests/ToledoMessage.Benchmarks/CryptoBenchmarks.cs

**Checkpoint**: Crypto library complete. All crypto tests pass. Benchmarks validate NFR targets.

---

## Phase 3: Foundational — Server Infrastructure (Blocking Prerequisites)

**Purpose**: Database, authentication, middleware, real-time hub, observability, and core services.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Data Model & Migrations

- [X] T038 [P] Implement User entity (Id, DisplayName, PasswordHash, CreatedAt, IsActive, DeletionRequestedAt) in src/ToledoMessage/Models/User.cs
- [X] T039 [P] Implement Device entity (Id, UserId, DeviceName, all key fields, timestamps, IsActive) in src/ToledoMessage/Models/Device.cs
- [X] T040 [P] Implement OneTimePreKey entity (Id, DeviceId, KeyId, PublicKey, IsUsed) in src/ToledoMessage/Models/OneTimePreKey.cs
- [X] T041 [P] Implement Conversation entity (Id, Type, GroupName, CreatedAt, DisappearingTimerSeconds) in src/ToledoMessage/Models/Conversation.cs
- [X] T042 [P] Implement ConversationParticipant entity (composite PK, JoinedAt, Role) in src/ToledoMessage/Models/ConversationParticipant.cs
- [X] T043 [P] Implement EncryptedMessage entity (Id, ConversationId, SenderDeviceId, RecipientDeviceId, Ciphertext max ~66KB, MessageType, ContentType, SequenceNumber, ServerTimestamp, IsDelivered, DeliveredAt) in src/ToledoMessage/Models/EncryptedMessage.cs
- [X] T044 Implement ApplicationDbContext with DbSets and configure entity relationships in src/ToledoMessage/Data/ApplicationDbContext.cs (depends on T038-T043)
- [X] T045 [P] Implement EF Core Fluent API configurations (UserConfiguration, DeviceConfiguration, OneTimePreKeyConfiguration, ConversationConfiguration, ConversationParticipantConfiguration, EncryptedMessageConfiguration) in src/ToledoMessage/Data/Configurations/
- [X] T046 Create and apply initial EF Core migration in src/ToledoMessage/Migrations/

### Server Authentication & Middleware

- [X] T047 Configure ASP.NET Core Identity password hashing, JWT Bearer authentication (issuer, audience, signing key, 15-min access token expiry), and SignalR token extraction from query string in src/ToledoMessage/Program.cs
- [X] T048 [P] Implement RateLimitService (in-memory per-IP and per-user rate tracking) in src/ToledoMessage/Services/RateLimitService.cs
- [X] T049 Implement RateLimitMiddleware (enforce limits from ProtocolConstants: 60 msg/min, 10 search/min) in src/ToledoMessage/Middleware/RateLimitMiddleware.cs (depends on T048)

### Server Services

- [X] T050 [P] Implement PreKeyService (store, consume, count one-time pre-keys) in src/ToledoMessage/Services/PreKeyService.cs
- [X] T051 [P] Implement MessageRelayService (store message with auto-incrementing sequence number, relay via SignalR to online recipient, get pending messages, acknowledge delivery) in src/ToledoMessage/Services/MessageRelayService.cs
- [X] T052 [P] Implement AccountDeletionService (initiate deletion with 7-day grace period, cancel pending deletion on login, background hosted service to scan and permanently deactivate expired accounts, revoke device keys on deactivation) in src/ToledoMessage/Services/AccountDeletionService.cs

### Observability

- [X] T053 [P] Configure Serilog structured logging (console + file sinks, request logging middleware, error tracking, ensure no plaintext message content or private key material in logs per NFR-011) and map /health endpoint with SQL Server connectivity check in src/ToledoMessage/Program.cs

### SignalR Hub

- [X] T054 Implement ChatHub with RegisterDevice (join device/user groups), connection lifecycle management in src/ToledoMessage/Hubs/ChatHub.cs (depends on T047)

### DI Registration

- [X] T055 Register all server services (PreKeyService, MessageRelayService, RateLimitService, AccountDeletionService, MessageCleanupHostedService), configure Serilog, configure CORS, map controllers, map SignalR hub (/hubs/chat), map Blazor components in src/ToledoMessage/Program.cs (depends on T047-T054)

**Checkpoint**: Server infrastructure ready. Database created. JWT auth works. SignalR hub accepts connections. /health endpoint responds. Serilog logging active.

---

## Phase 4: User Story 1 — Account Registration & Key Generation (P1) 🎯 MVP

**Goal**: User creates account, system generates hybrid identity keys, publishes pre-key bundle.

**Independent Test**: Register a new user → verify server holds valid pre-key bundle → user sees security fingerprint.

### Tests (write FIRST, verify they FAIL)

- [X] T056 [P] [US1] Write integration test: successful registration creates account and returns JWT in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs
- [X] T057 [P] [US1] Write integration test: duplicate display name rejected in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs
- [X] T058 [P] [US1] Write integration test: weak password (<12 chars) rejected in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs

### Server Implementation

- [X] T059 [US1] Implement AuthController POST /api/auth/register (validate display name 3-32 chars ^[a-zA-Z0-9_-]+$ case-insensitive uniqueness, password min 12 chars, hash password, create user, return JWT) in src/ToledoMessage/Controllers/AuthController.cs
- [X] T060 [US1] Implement AuthController POST /api/auth/login (validate credentials, cancel pending deletion if account in PendingDeletion state via AccountDeletionService, return JWT) in src/ToledoMessage/Controllers/AuthController.cs
- [X] T061 [US1] Implement AuthController DELETE /api/auth/account (initiate account deletion with 7-day grace period via AccountDeletionService, return deletionScheduledAt and gracePeriodEndsAt) in src/ToledoMessage/Controllers/AuthController.cs (depends on T052)
- [X] T062 [US1] Implement DevicesController POST /api/devices (register device with key material, store OTPs, enforce max 10 devices) in src/ToledoMessage/Controllers/DevicesController.cs
- [X] T063 [US1] Implement DevicesController GET /api/devices (list active devices for authenticated user) in src/ToledoMessage/Controllers/DevicesController.cs

### Client Implementation

- [X] T064 [US1] Implement KeyGenerationService (generate X25519+Ed25519 classical keys, ML-KEM-768+ML-DSA-65 PQ keys, signed pre-key with hybrid signature, batch of 100 OTPs) in src/ToledoMessage.Client/Services/KeyGenerationService.cs
- [X] T065 [US1] Implement LocalStorageService (IndexedDB wrapper for storing/retrieving private keys, ratchet state, messages, user preferences) in src/ToledoMessage.Client/Services/LocalStorageService.cs
- [X] T066 [US1] Implement Register page (display name input, password input with min 12 char validation, register button, on success: generate keys via KeyGenerationService, register device via POST /api/devices, store private keys in IndexedDB, navigate to chat list) in src/ToledoMessage.Client/Pages/Register.razor
- [X] T067 [US1] Implement Login page (display name + password inputs, login button, on success: store JWT, navigate to chat list) in src/ToledoMessage.Client/Pages/Login.razor
- [X] T068 [US1] Configure client-side HttpClient with base address and JWT auth header, register all services in DI in src/ToledoMessage.Client/Program.cs

### Refresh Token Support

- [X] T069 [US1] Implement RefreshToken entity (Id, UserId FK, Token, DeviceId FK, ExpiresAt, CreatedAt, IsRevoked) and add EF Core configuration in src/ToledoMessage/Models/RefreshToken.cs and src/ToledoMessage/Data/Configurations/RefreshTokenConfiguration.cs
- [X] T070 [US1] Implement POST /api/auth/refresh endpoint (accept expired access token + refresh token, validate, issue new token pair, rotate refresh token) in src/ToledoMessage/Controllers/AuthController.cs (depends on T069)
- [X] T071 [US1] Implement client-side token refresh interceptor (detect 401 responses, auto-refresh via POST /api/auth/refresh, retry original request, redirect to login on refresh failure) in src/ToledoMessage.Client/Services/AuthTokenHandler.cs

**Checkpoint**: User can register, login, and has cryptographic identity. Pre-key bundle on server. Token refresh works. Account deletion initiable.

---

## Phase 5: User Story 2 — Initiating a Secure Conversation (P1)

**Goal**: User searches for another user, initiates conversation with hybrid X3DH key exchange, sends first encrypted message.

**Independent Test**: Two registered users → User A sends message to User B → message arrives encrypted, decrypts correctly, server never sees plaintext.

### Tests (write FIRST, verify they FAIL)

- [X] T072 [P] [US2] Write integration test: two users establish session via X3DH and exchange first encrypted message in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs
- [X] T073 [P] [US2] Write integration test: message to offline user is queued and delivered on reconnect in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs

### Server Implementation

- [X] T074 [US2] Implement UsersController GET /api/users/search (case-insensitive partial match on display name, rate limited 10/min) in src/ToledoMessage/Controllers/UsersController.cs
- [X] T075 [US2] Implement UsersController GET /api/users/{userId}/prekey-bundle (fetch device's pre-key bundle, consume one OTP, return all public keys) in src/ToledoMessage/Controllers/UsersController.cs
- [X] T076 [US2] Implement UsersController GET /api/users/{userId}/devices (list active devices for fan-out) in src/ToledoMessage/Controllers/UsersController.cs
- [X] T077 [US2] Implement ConversationsController POST /api/conversations (create 1:1 conversation or return existing, add both users as participants) in src/ToledoMessage/Controllers/ConversationsController.cs

### Client Implementation

- [X] T078 [US2] Implement SessionService (fetch pre-key bundle via API, execute X3DH initiator, initialize Double Ratchet, persist RatchetState to IndexedDB) in src/ToledoMessage.Client/Services/SessionService.cs
- [X] T079 [US2] Implement CryptoService (orchestrate session establishment + encryption: check for existing session, establish if needed, cache X3DH InitiationResult for PreKeyMessage, encrypt message via Double Ratchet, persist updated ratchet state) in src/ToledoMessage.Client/Services/CryptoService.cs
- [X] T080 [US2] Implement MessageEncryptionService (encrypt: serialize plaintext + MessageHeader, AES-256-GCM encrypt with message key from ratchet, return ciphertext; decrypt: AES-256-GCM decrypt, deserialize, advance ratchet) in src/ToledoMessage.Client/Services/MessageEncryptionService.cs
- [X] T081 [US2] Implement NewConversation page (search input with API call to /api/users/search, display results, select user, create conversation via API, navigate to chat) in src/ToledoMessage.Client/Pages/NewConversation.razor

**Checkpoint**: Two users can establish a secure session and exchange their first end-to-end encrypted message.

---

## Phase 6: User Story 3 — Real-Time Message Exchange (P1)

**Goal**: Two users exchange messages in real-time via SignalR with delivery status, message ordering, offline queueing, browser notifications, and tab coordination.

**Independent Test**: Two users exchange 10+ messages rapidly → all arrive in order, each encrypted with distinct key, delivery status updates in real-time.

### Tests (write FIRST, verify they FAIL)

- [X] T082 [P] [US3] Write integration test: real-time message exchange with delivery status (sent → delivered → read) in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs
- [X] T083 [P] [US3] Write integration test: messages maintain correct ordering via sequence numbers in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs

### Server Implementation

- [X] T084 [US3] Implement ChatHub SendMessage (validate sender, validate message size ≤64 KB per FR-019, validate recipient device is active and recipient user is not deactivated, store via MessageRelayService, relay to recipient device group, return SendMessageResult with messageId + sequenceNumber + serverTimestamp) in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T085 [US3] Implement ChatHub AcknowledgeDelivery and AcknowledgeRead (update message status, broadcast MessageDelivered/MessageRead to sender's device group) in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T086 [US3] Implement ChatHub TypingIndicator (broadcast UserTyping to all other conversation participants' user groups) in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T087 [US3] Implement MessagesController POST /api/messages (REST fallback for sending when SignalR unavailable, validate message size ≤64 KB per FR-019, validate recipient device is active and recipient user is not deactivated) in src/ToledoMessage/Controllers/MessagesController.cs
- [X] T088 [US3] Implement MessagesController GET /api/messages/pending (retrieve undelivered messages for device, ordered by sequence number) in src/ToledoMessage/Controllers/MessagesController.cs
- [X] T089 [US3] Implement MessagesController POST /api/messages/{messageId}/acknowledge (mark delivered) in src/ToledoMessage/Controllers/MessagesController.cs

### Client Implementation

- [X] T090 [US3] Implement TabLeaderService (BroadcastChannel API leader election, periodic heartbeat, automatic leader promotion when leader tab closes, coordinate SignalR connection and IndexedDB write ownership per FR-022) in src/ToledoMessage.Client/Services/TabLeaderService.cs
- [X] T091 [US3] Implement SignalRService (connect to /hubs/chat with JWT only when leader tab per TabLeaderService, register device, handle ReceiveMessage/MessageDelivered/MessageRead/UserTyping events, auto-reconnect on disconnect, fetch pending messages on reconnect, broadcast received messages to follower tabs via BroadcastChannel, queue outbound messages in IndexedDB when disconnected and flush on reconnect) in src/ToledoMessage.Client/Services/SignalRService.cs (depends on T090)
- [X] T092 [P] [US3] Implement NotificationService (Browser Notification API, request user permission, show desktop notification with sender name only when tab unfocused per FR-021, integrate with SignalRService message events) in src/ToledoMessage.Client/Services/NotificationService.cs
- [X] T093 [US3] Implement Chat page (message list with chronological ordering, auto-scroll, send message: validate size ≤64 KB per FR-019 → encrypt via CryptoService → send via SignalR, receive message: decrypt via CryptoService → display, delivery status indicators per message) in src/ToledoMessage.Client/Pages/Chat.razor
- [X] T094 [US3] Implement ConversationsController GET /api/conversations (list all user's conversations with metadata: last message timestamp, participant count, disappearing timer, type) in src/ToledoMessage/Controllers/ConversationsController.cs
- [X] T095 [US3] Implement ChatList page (list all conversations with last message preview, unread count, sort by most recent activity, navigate to Chat on select, navigate to NewConversation on FAB) in src/ToledoMessage.Client/Pages/ChatList.razor (depends on T094)
- [X] T096 [P] [US3] Implement MessageBubble component (sender/recipient alignment, timestamp, delivery status icon) in src/ToledoMessage.Client/Components/MessageBubble.razor
- [X] T097 [P] [US3] Implement MessageInput component (text input, send button, typing indicator trigger, validate message size ≤64 KB with user-facing error per FR-019) in src/ToledoMessage.Client/Components/MessageInput.razor
- [X] T098 [P] [US3] Implement DeliveryStatus component (sending/sent/delivered/read icons) in src/ToledoMessage.Client/Components/DeliveryStatus.razor
- [X] T099 [P] [US3] Implement ConversationListItem component (display name, last message preview, timestamp, unread badge) in src/ToledoMessage.Client/Components/ConversationListItem.razor

**Checkpoint**: Users can exchange messages in real-time with delivery/read status. Offline messages are queued and delivered on reconnect. Browser notifications for unfocused tabs. Tab coordination via leader election. US1+US2+US3 form a complete MVP.

---

## Phase 7: User Story 4 — Security Verification Between Users (P2)

**Goal**: Users can compare security fingerprints and verify each other's identity. Key changes trigger warnings.

**Independent Test**: Two users view fingerprints → fingerprints match → mark verified → key change triggers warning.

### Tests (write FIRST, verify they FAIL)

- [X] T100 [P] [US4] Write unit test: fingerprint generation produces identical output for both users given same identity keys in tests/ToledoMessage.Crypto.Tests/KeyManagement/KeyManagementTests.cs
- [X] T101 [P] [US4] Write unit test: fingerprint changes when identity key changes in tests/ToledoMessage.Crypto.Tests/KeyManagement/KeyManagementTests.cs

### Client Implementation

- [X] T102 [US4] Implement FingerprintService (derive safety number from both users' classical + PQ identity public keys, format as human-readable groups, detect key changes by comparing stored vs. current fingerprint) in src/ToledoMessage.Client/Services/FingerprintService.cs
- [X] T103 [US4] Implement SecurityInfo page (display fingerprint for current conversation, show both users' identity key hashes, verified/unverified status, button to mark as verified, persist verification state in IndexedDB) in src/ToledoMessage.Client/Pages/SecurityInfo.razor
- [X] T104 [US4] Implement KeyChangeWarning component (prominent warning banner when a contact's identity key changes, mark conversation as unverified, require re-verification) in src/ToledoMessage.Client/Components/KeyChangeWarning.razor

**Checkpoint**: Users can verify identities via fingerprints. Key change warnings protect against MITM.

---

## Phase 8: User Story 5 — Multi-Device Support (P2)

**Goal**: User links up to 10 devices, each with independent keys. Messages delivered to all active devices.

**Independent Test**: Link second device → new messages arrive and decrypt on both devices.

### Tests (write FIRST, verify they FAIL)

- [X] T105 [P] [US5] Write integration test: two devices receive and independently decrypt the same message in tests/ToledoMessage.Integration.Tests/MultiDeviceTests.cs
- [X] T106 [P] [US5] Write integration test: revoked device no longer receives messages in tests/ToledoMessage.Integration.Tests/MultiDeviceTests.cs

### Server Implementation

- [X] T107 [US5] Implement DevicesController DELETE /api/devices/{deviceId} (deactivate device, revoke keys, remove from SignalR groups) in src/ToledoMessage/Controllers/DevicesController.cs
- [X] T108 [US5] Implement DevicesController GET /api/devices/{deviceId}/prekeys/count (count remaining unused OTPs for a device) in src/ToledoMessage/Controllers/DevicesController.cs
- [X] T109 [US5] Implement DevicesController POST /api/devices/{deviceId}/prekeys (upload additional OTPs for replenishment) in src/ToledoMessage/Controllers/DevicesController.cs

### Client Implementation

- [X] T110 [US5] Implement fan-out encryption in CryptoService (for each recipient user: fetch all active devices, establish session per device if needed, encrypt message independently for each device) in src/ToledoMessage.Client/Services/CryptoService.cs
- [X] T111 [US5] Implement PreKeyReplenishmentService (periodically check OTP count via API, replenish when below threshold of 10, generate and upload new batch of OTPs) in src/ToledoMessage.Client/Services/PreKeyReplenishmentService.cs
- [X] T112 [US5] Implement device management section in Settings page (list linked devices, add device button, remove device button with confirmation) in src/ToledoMessage.Client/Pages/Settings.razor
- [X] T113 [US5] Add account deletion section to Settings page (delete account button, confirmation dialog with 7-day grace period warning, call DELETE /api/auth/account, show scheduled deletion date, option to cancel during grace period by logging in) in src/ToledoMessage.Client/Pages/Settings.razor

**Checkpoint**: Multi-device messaging works. Each device has independent crypto state. Revoked devices stop receiving. Account deletion accessible from Settings.

---

## Phase 9: User Story 6 — Group Messaging (P3)

**Goal**: Group conversations with Sender Keys protocol, end-to-end encrypted, key rotation on membership change.

**Independent Test**: Create group of 3+ users → exchange messages → add/remove member → verify encryption and key rotation.

### Tests (write FIRST, verify they FAIL)

- [X] T114 [P] [US6] Write integration test: group of 3 users can exchange encrypted messages in tests/ToledoMessage.Integration.Tests/GroupMessagingTests.cs
- [X] T115 [P] [US6] Write integration test: added member reads new messages but not history in tests/ToledoMessage.Integration.Tests/GroupMessagingTests.cs
- [X] T116 [P] [US6] Write integration test: removed member cannot decrypt new messages (key rotation) in tests/ToledoMessage.Integration.Tests/GroupMessagingTests.cs

### Server Implementation

- [X] T117 [US6] Implement ConversationsController POST /api/conversations/group (create group with name and 2-100 participants, creator gets Admin role) in src/ToledoMessage/Controllers/ConversationsController.cs
- [X] T118 [US6] Implement ConversationsController GET /api/conversations/{id} and GET /api/conversations/{id}/participants (return group details with participant list and roles) in src/ToledoMessage/Controllers/ConversationsController.cs
- [X] T119 [US6] Implement ConversationsController POST /api/conversations/{id}/participants (Admin adds participant) and DELETE /api/conversations/{id}/participants/{userId} (Admin removes or self-leave) in src/ToledoMessage/Controllers/ConversationsController.cs

### Client Implementation

- [X] T120 [US6] Implement Sender Keys client-side protocol: generate sender key, distribute to all group members via pairwise encrypted sessions, encrypt group messages with sender key O(1), rotate sender key on membership change in src/ToledoMessage.Client/Services/CryptoService.cs
- [X] T121 [US6] Extend Chat page to support group conversations: display group name, participant list, admin controls (add/remove member), group message send/receive using Sender Keys in src/ToledoMessage.Client/Pages/Chat.razor
- [X] T122 [US6] Extend NewConversation page to support group creation: multi-select participants, group name input, create group via POST /api/conversations/group in src/ToledoMessage.Client/Pages/NewConversation.razor

**Checkpoint**: Group messaging works with Sender Keys. Key rotation on membership change protects forward secrecy.

---

## Phase 10: User Story 7 — Disappearing Messages & Retention Control (P3)

**Goal**: Configurable per-conversation auto-delete timers. Manual local deletion.

**Independent Test**: Set 1-minute timer → send messages → verify auto-deleted after expiry on both sides.

### Tests (write FIRST, verify they FAIL)

- [X] T123 [P] [US7] Write integration test: messages auto-deleted after timer expires in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs

### Server Implementation

- [X] T124 [US7] Implement ConversationsController PUT /api/conversations/{id}/timer (set or disable disappearing timer, validate positive int or null) in src/ToledoMessage/Controllers/ConversationsController.cs
- [X] T125 [US7] Implement MessageCleanupHostedService (background service: periodically scan for expired messages based on conversation timer and 90-day undelivered retention, delete expired rows) in src/ToledoMessage/Services/MessageCleanupHostedService.cs

### Client Implementation

- [X] T126 [US7] Implement MessageExpiryService (client-side timer management: track message timestamps, auto-delete from IndexedDB when timer expires, trigger UI refresh) in src/ToledoMessage.Client/Services/MessageExpiryService.cs
- [X] T127 [US7] Implement DisappearingTimerConfig component (timer duration selector: off, 1 hour, 24 hours, 7 days, 30 days; update via API, display active timer in chat header) in src/ToledoMessage.Client/Components/DisappearingTimerConfig.razor
- [X] T128 [US7] Add manual message deletion to Chat page (long-press/right-click on message → delete from local device only, confirm dialog) in src/ToledoMessage.Client/Pages/Chat.razor

**Checkpoint**: Disappearing messages work. Server purges expired and 90-day undelivered messages.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T129 [P] Implement ThemeService (dark/light mode toggle, persist preference in IndexedDB) in src/ToledoMessage.Client/Services/ThemeService.cs
- [X] T130 [P] Implement Blazor server-side shell: App.razor, MainLayout.razor, NavMenu.razor (navigation between pages, auth-aware menu items) in src/ToledoMessage/Components/
- [X] T131 [P] Implement Error and NotFound pages in src/ToledoMessage/Components/Pages/Error.razor and src/ToledoMessage/Components/Pages/NotFound.razor
- [ ] T132 Security hardening: validate all API inputs against ProtocolConstants (key sizes, display name format, password length, message size 64 KB), ensure no plaintext logging (Constitution security requirement), validate JWT claims on all authorized endpoints
- [ ] T133 Implement load testing with NBomber or k6: target 10K concurrent SignalR connections, validate NFR latency targets (<500ms key exchange, <50ms message encrypt) in tests/ToledoMessage.Benchmarks/LoadTests/
- [ ] T134 Validate code coverage meets thresholds: >=80% overall, >=90% crypto library (ToledoMessage.Crypto). Run `dotnet test --collect:"XPlat Code Coverage"` and verify with ReportGenerator
- [ ] T135 Run quickstart.md validation: follow all steps in specs/001-secure-messaging/quickstart.md from scratch and verify successful end-to-end flow
- [ ] T136 Run full test suite and verify all tests pass: `dotnet test` across all test projects

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Crypto Library)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (Server Infrastructure)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 4-10 (User Stories)**: All depend on Phase 2 + Phase 3 completion
- **Phase 11 (Polish)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Registration — no dependencies on other stories
- **US2 (P1)**: Secure conversation — depends on US1 (needs registered users)
- **US3 (P1)**: Real-time exchange — depends on US2 (needs established session)
- **US4 (P2)**: Security verification — depends on US2 (needs active conversation)
- **US5 (P2)**: Multi-device — depends on US1 (needs registered device)
- **US6 (P3)**: Group messaging — depends on US2 + US3 (needs 1:1 sessions)
- **US7 (P3)**: Disappearing messages — depends on US3 (needs message exchange)

### Within Each User Story

1. Tests FIRST → verify they FAIL
2. Server implementation (models → services → controllers)
3. Client implementation (services → pages → components)
4. Integration verification

### Parallel Opportunities

Within phases, all tasks marked [P] can run in parallel:
- Phase 2: All 12 test tasks (T008-T019) in parallel, then all 3 classical primitives (T020-T022), both PQ primitives (T023-T024)
- Phase 3: All 6 entity models (T038-T043) in parallel, services T050-T053 in parallel
- Phase 4: All 3 test tasks (T056-T058) in parallel
- Phase 5: Both test tasks (T072-T073) in parallel
- Phase 6: All 4 UI components (T096-T099) in parallel, NotificationService (T092) in parallel with UI components
- Phase 7: Both test tasks (T100-T101) in parallel
- Phase 8: Both test tasks (T105-T106) in parallel
- Phase 9: All 3 test tasks (T114-T116) in parallel

---

## Parallel Example: Phase 2 (Crypto Library)

```text
# Parallel: all test tasks
T008, T009, T010, T011, T012, T013, T014, T015, T016, T017, T018, T019

# Parallel: classical primitives
T020 (AES-GCM), T021 (Ed25519), T022 (X25519)

# Parallel: post-quantum primitives
T023 (ML-KEM), T024 (ML-DSA)

# Sequential: hybrid depends on primitives
T025 (HybridKX ← T022+T023), T026 (HKDF), T027 (HybridSign ← T021+T024)

# Sequential: protocol depends on hybrid
T028 (IdentityKeys ← T021-T024), T031 (RatchetState), T032 (PreKeyBundle)
T033 (X3DH Init ← T025+T028), T034 (X3DH Resp ← T025+T028)
T035 (MessageKeys ← T026), T036 (DoubleRatchet ← T025+T026+T035)
```

---

## Parallel Example: Phase 3 (Server Infrastructure)

```text
# Parallel: entity models
T038, T039, T040, T041, T042, T043

# Sequential: DbContext depends on entities
T044 (DbContext ← T038-T043), T045 (Configurations), T046 (Migration)

# Parallel: auth + middleware
T047 (JWT auth), T048 (RateLimitService)
T049 (RateLimitMiddleware ← T048)

# Parallel: server services
T050 (PreKeyService), T051 (MessageRelayService), T052 (AccountDeletionService), T053 (Serilog + /health)

# Sequential: hub + DI
T054 (ChatHub ← T047), T055 (DI Registration ← T047-T054)
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US3)

1. Complete Phase 1: Setup
2. Complete Phase 2: Crypto Library (CRITICAL)
3. Complete Phase 3: Server Infrastructure (CRITICAL)
4. Complete Phase 4: US1 — Registration & Keys
5. Complete Phase 5: US2 — Secure Conversation
6. Complete Phase 6: US3 — Real-Time Exchange
7. **STOP and VALIDATE**: Full E2E encrypted messaging works
8. Deploy/demo (MVP!)

### Incremental Delivery

1. Setup + Crypto + Infrastructure → Foundation ready
2. US1 → Account creation works → Verify
3. US2 → Secure messaging works → Verify
4. US3 → Real-time exchange works → Deploy/Demo (MVP!)
5. US4 → Security verification → Deploy
6. US5 → Multi-device + account management → Deploy
7. US6 → Group messaging → Deploy
8. US7 → Disappearing messages → Deploy
9. Polish → Final release

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable after its dependencies
- Tests MUST be written and FAIL before implementation (TDD)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
