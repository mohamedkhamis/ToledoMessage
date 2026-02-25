# Tasks: Hybrid Post-Quantum Secure Messaging

**Input**: Design documents from `/specs/001-secure-messaging/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included per Constitution Principle VI (Test-First Development). Write tests FIRST, verify they FAIL, then implement.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. The Crypto Library phase is a foundational prerequisite since all user stories depend on it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Project Scaffolding)

**Purpose**: Create solution structure, all projects, NuGet packages, and project references per the implementation plan.

- [x] T001 Create solution file ToledoMessage.sln and directory structure (src/, tests/) at repository root
- [x] T002 Create Toledo.SharedKernel class library project at src/Toledo.SharedKernel/Toledo.SharedKernel.csproj targeting net10.0
- [x] T003 Implement DecimalTools.GetNewId() in src/Toledo.SharedKernel/Helpers/DecimalTools.cs (user-provided code, decimal(28,8) ID generation)
- [x] T004 [P] Create ToledoMessage server project at src/ToledoMessage/ToledoMessage.csproj using Blazor Web App template (dotnet new blazor) targeting net10.0
- [x] T005 [P] Create ToledoMessage.Client class library project at src/ToledoMessage.Client/ToledoMessage.Client.csproj targeting net10.0
- [x] T006 [P] Create ToledoMessage.Shared class library project at src/ToledoMessage.Shared/ToledoMessage.Shared.csproj targeting net10.0
- [x] T007 [P] Create ToledoMessage.Crypto class library project at src/ToledoMessage.Crypto/ToledoMessage.Crypto.csproj targeting net10.0
- [x] T008 [P] Create ToledoMessage.Crypto.Tests xUnit v3 test project at tests/ToledoMessage.Crypto.Tests/ToledoMessage.Crypto.Tests.csproj
- [x] T009 [P] Create ToledoMessage.Server.Tests xUnit v3 test project at tests/ToledoMessage.Server.Tests/ToledoMessage.Server.Tests.csproj
- [x] T010 [P] Create ToledoMessage.Client.Tests xUnit v3 test project at tests/ToledoMessage.Client.Tests/ToledoMessage.Client.Tests.csproj
- [x] T011 [P] Create ToledoMessage.Integration.Tests xUnit v3 test project at tests/ToledoMessage.Integration.Tests/ToledoMessage.Integration.Tests.csproj
- [x] T012 [P] Create ToledoMessage.Benchmarks console project at tests/ToledoMessage.Benchmarks/ToledoMessage.Benchmarks.csproj with BenchmarkDotNet 0.15.8
- [x] T013 Configure project references per dependency graph: SharedKernel ← Shared ← Server, SharedKernel ← Shared ← Client ← Crypto; add NuGet packages (BouncyCastle.Cryptography 2.6.2 to Crypto, EF Core SqlServer 10.0.3 + Identity.EF 10.0.3 to Server, SignalR.Client 10.0.3 to Client, xunit.v3 3.2.2 to all test projects)
- [x] T014 Configure appsettings.json and appsettings.Development.json with SQL Server connection string and JWT settings in src/ToledoMessage/

**Checkpoint**: Solution builds with `dotnet build` — all projects compile, no code yet

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, enums, entity models, EF Core DbContext, ASP.NET Core Identity, and Blazor layout. MUST complete before any user story.

**CRITICAL**: No user story work can begin until this phase is complete

### Shared Contracts (ToledoMessage.Shared)

- [ ] T015 [P] Create MessageType enum (PreKeyMessage=0, NormalMessage=1) in src/ToledoMessage.Shared/Enums/MessageType.cs
- [ ] T016 [P] Create ContentType enum (Text=0) in src/ToledoMessage.Shared/Enums/ContentType.cs
- [ ] T017 [P] Create ConversationType enum (OneToOne=0, Group=1) in src/ToledoMessage.Shared/Enums/ConversationType.cs
- [ ] T018 [P] Create DeliveryStatus enum (Sending=0, Sent=1, Delivered=2, Read=3) in src/ToledoMessage.Shared/Enums/DeliveryStatus.cs
- [ ] T019 [P] Create ParticipantRole enum (Member=0, Admin=1) in src/ToledoMessage.Shared/Enums/ParticipantRole.cs
- [ ] T020 [P] Create ProtocolConstants class (key sizes, batch sizes, limits) in src/ToledoMessage.Shared/Constants/ProtocolConstants.cs
- [ ] T021 [P] Create RegisterRequest and LoginRequest DTOs in src/ToledoMessage.Shared/DTOs/RegisterRequest.cs and src/ToledoMessage.Shared/DTOs/LoginRequest.cs
- [ ] T022 [P] Create AuthResponse DTO in src/ToledoMessage.Shared/DTOs/AuthResponse.cs
- [ ] T023 [P] Create DeviceRegistrationRequest DTO in src/ToledoMessage.Shared/DTOs/DeviceRegistrationRequest.cs
- [ ] T024 [P] Create PreKeyBundleResponse DTO in src/ToledoMessage.Shared/DTOs/PreKeyBundleResponse.cs
- [ ] T025 [P] Create SendMessageRequest and SendMessageResult DTOs in src/ToledoMessage.Shared/DTOs/SendMessageRequest.cs and src/ToledoMessage.Shared/DTOs/SendMessageResult.cs
- [ ] T026 [P] Create MessageEnvelope DTO in src/ToledoMessage.Shared/DTOs/MessageEnvelope.cs
- [ ] T027 [P] Create UserSearchResult DTO in src/ToledoMessage.Shared/DTOs/UserSearchResult.cs

### Server Entity Models

- [ ] T028 [P] Create User entity model in src/ToledoMessage/Models/User.cs (Id decimal(28,8), DisplayName, PasswordHash, CreatedAt, IsActive)
- [ ] T029 [P] Create Device entity model in src/ToledoMessage/Models/Device.cs (Id decimal(28,8), UserId FK, identity keys, signed pre-key, Kyber pre-key, timestamps, IsActive)
- [ ] T030 [P] Create OneTimePreKey entity model in src/ToledoMessage/Models/OneTimePreKey.cs (Id decimal(28,8), DeviceId FK, KeyId, PublicKey, IsUsed)
- [ ] T031 [P] Create Conversation entity model in src/ToledoMessage/Models/Conversation.cs (Id decimal(28,8), Type, CreatedAt, DisappearingTimerSeconds)
- [ ] T032 [P] Create ConversationParticipant entity model in src/ToledoMessage/Models/ConversationParticipant.cs (composite PK: ConversationId + UserId, JoinedAt, Role)
- [ ] T033 [P] Create EncryptedMessage entity model in src/ToledoMessage/Models/EncryptedMessage.cs (Id decimal(28,8), ConversationId FK, SenderDeviceId FK, RecipientDeviceId FK, Ciphertext, MessageType, ContentType, SequenceNumber, ServerTimestamp, IsDelivered, DeliveredAt)

### EF Core Configuration

- [ ] T034 [P] Create UserConfiguration in src/ToledoMessage/Data/Configurations/UserConfiguration.cs (decimal(28,8) PK, unique DisplayName index)
- [ ] T035 [P] Create DeviceConfiguration in src/ToledoMessage/Data/Configurations/DeviceConfiguration.cs (decimal(28,8) PK/FK, max 10 devices validation, UserId index)
- [ ] T036 [P] Create OneTimePreKeyConfiguration in src/ToledoMessage/Data/Configurations/OneTimePreKeyConfiguration.cs (decimal(28,8) PK/FK, unique (DeviceId, KeyId) constraint)
- [ ] T037 [P] Create ConversationConfiguration in src/ToledoMessage/Data/Configurations/ConversationConfiguration.cs (decimal(28,8) PK)
- [ ] T038 [P] Create ConversationParticipantConfiguration in src/ToledoMessage/Data/Configurations/ConversationParticipantConfiguration.cs (composite PK, decimal(28,8) FKs)
- [ ] T039 [P] Create EncryptedMessageConfiguration in src/ToledoMessage/Data/Configurations/EncryptedMessageConfiguration.cs (decimal(28,8) PK/FKs, (RecipientDeviceId, IsDelivered) index)
- [ ] T040 Create ApplicationDbContext with all DbSets and configuration application in src/ToledoMessage/Data/ApplicationDbContext.cs

### Server Bootstrap

- [ ] T041 Configure Program.cs with EF Core, ASP.NET Core Identity (JWT bearer), SignalR, CORS, and service registration in src/ToledoMessage/Program.cs
- [ ] T042 Create initial EF Core migration by running `dotnet ef migrations add InitialCreate` in src/ToledoMessage/

### Blazor Layout

- [ ] T043 [P] Create App.razor (root component with HeadOutlet and Routes) in src/ToledoMessage/Components/App.razor
- [ ] T044 [P] Create Routes.razor (Router component) in src/ToledoMessage/Components/Routes.razor
- [ ] T045 [P] Create MainLayout.razor (base layout shell) in src/ToledoMessage/Components/Layout/MainLayout.razor
- [ ] T046 [P] Create NavMenu.razor (navigation component) in src/ToledoMessage/Components/Layout/NavMenu.razor
- [ ] T047 [P] Create _Imports.razor for client project in src/ToledoMessage.Client/_Imports.razor

**Checkpoint**: Foundation ready — `dotnet build` succeeds, `dotnet ef database update` applies migration, server starts with Identity auth. User story and crypto implementation can now begin.

---

## Phase 3: Cryptographic Library (ToledoMessage.Crypto)

**Purpose**: Implement all cryptographic primitives, hybrid operations, and the Signal Protocol (X3DH + Double Ratchet) using BouncyCastle. This is a foundational prerequisite — all user stories depend on it.

**CRITICAL**: No user story involving encryption can proceed without this phase.

### Tests (TDD — write first, verify they fail)

- [ ] T048 [P] Write X25519 key exchange tests (generate, agree, round-trip) in tests/ToledoMessage.Crypto.Tests/Classical/X25519KeyExchangeTests.cs
- [ ] T049 [P] Write Ed25519 signer tests (sign, verify, reject tampered) in tests/ToledoMessage.Crypto.Tests/Classical/Ed25519SignerTests.cs
- [ ] T050 [P] Write AES-256-GCM cipher tests (encrypt, decrypt, reject tampered, AAD) in tests/ToledoMessage.Crypto.Tests/Classical/AesGcmCipherTests.cs
- [ ] T051 [P] Write ML-KEM-768 key exchange tests (generate, encapsulate, decapsulate) in tests/ToledoMessage.Crypto.Tests/PostQuantum/MlKemKeyExchangeTests.cs
- [ ] T052 [P] Write ML-DSA-65 signer tests (sign, verify, reject tampered) in tests/ToledoMessage.Crypto.Tests/PostQuantum/MlDsaSignerTests.cs
- [ ] T053 [P] Write hybrid key exchange tests (X25519 + ML-KEM combined KEM) in tests/ToledoMessage.Crypto.Tests/Hybrid/HybridKeyExchangeTests.cs
- [ ] T054 [P] Write hybrid signer tests (Ed25519 + ML-DSA combined signatures) in tests/ToledoMessage.Crypto.Tests/Hybrid/HybridSignerTests.cs
- [ ] T055 [P] Write HKDF-SHA256 key derivation tests (KDF, domain separation, output length) in tests/ToledoMessage.Crypto.Tests/Hybrid/HybridKeyDerivationTests.cs
- [ ] T056 [P] Write X3DH protocol tests (initiator+responder full handshake with PQ extension) in tests/ToledoMessage.Crypto.Tests/Protocol/X3dhTests.cs
- [ ] T057 [P] Write Double Ratchet tests (symmetric ratchet, DH ratchet, out-of-order messages) in tests/ToledoMessage.Crypto.Tests/Protocol/DoubleRatchetTests.cs
- [ ] T058 [P] Write message key derivation tests (chain key → message key, index progression) in tests/ToledoMessage.Crypto.Tests/Protocol/MessageKeysTests.cs

### Classical Primitives Implementation

- [ ] T059 [P] Implement X25519 key exchange (generate keypair, compute shared secret) in src/ToledoMessage.Crypto/Classical/X25519KeyExchange.cs using BouncyCastle X25519Agreement
- [ ] T060 [P] Implement Ed25519 signer (sign, verify) in src/ToledoMessage.Crypto/Classical/Ed25519Signer.cs using BouncyCastle Ed25519Signer
- [ ] T061 [P] Implement AES-256-GCM cipher (encrypt, decrypt with nonce + AAD) in src/ToledoMessage.Crypto/Classical/AesGcmCipher.cs using BouncyCastle GcmBlockCipher

### Post-Quantum Primitives Implementation

- [ ] T062 [P] Implement ML-KEM-768 key exchange (generate, encapsulate, decapsulate) in src/ToledoMessage.Crypto/PostQuantum/MlKemKeyExchange.cs using BouncyCastle MLKem
- [ ] T063 [P] Implement ML-DSA-65 signer (sign, verify) in src/ToledoMessage.Crypto/PostQuantum/MlDsaSigner.cs using BouncyCastle MLDsa

### Hybrid Operations Implementation

- [ ] T064 Implement hybrid key exchange (X25519 + ML-KEM-768 combined, HKDF to derive shared secret) in src/ToledoMessage.Crypto/Hybrid/HybridKeyExchange.cs (depends on T059, T062)
- [ ] T065 [P] Implement hybrid signer (Ed25519 + ML-DSA-65 concatenated signatures) in src/ToledoMessage.Crypto/Hybrid/HybridSigner.cs (depends on T060, T063)
- [ ] T066 [P] Implement HKDF-SHA256 key derivation with domain separation in src/ToledoMessage.Crypto/Hybrid/HybridKeyDerivation.cs using BouncyCastle HkdfBytesGenerator

### Protocol Implementation

- [ ] T067 Create PreKeyBundle data structure in src/ToledoMessage.Crypto/Protocol/PreKeyBundle.cs (identity keys, signed pre-key, Kyber pre-key, one-time pre-key)
- [ ] T068 Implement RatchetState (serializable session state) in src/ToledoMessage.Crypto/Protocol/RatchetState.cs (root key, chain keys, indices, skipped message keys)
- [ ] T069 Implement MessageKeys derivation (chain key → message key + next chain key) in src/ToledoMessage.Crypto/Protocol/MessageKeys.cs
- [ ] T070 Implement X3DH initiator (Alice side — 4 DH + 1 KEM → shared secret via HKDF) in src/ToledoMessage.Crypto/Protocol/X3dhInitiator.cs (depends on T064, T066)
- [ ] T071 Implement X3DH responder (Bob side — matching DH + KEM → same shared secret) in src/ToledoMessage.Crypto/Protocol/X3dhResponder.cs (depends on T064, T066)
- [ ] T072 Implement Double Ratchet state machine (symmetric ratchet + DH ratchet + out-of-order handling) in src/ToledoMessage.Crypto/Protocol/DoubleRatchet.cs (depends on T059, T066, T068, T069)

### Key Management Implementation

- [ ] T073 [P] Implement identity key generator (hybrid Ed25519 + ML-DSA-65 keypair) in src/ToledoMessage.Crypto/KeyManagement/IdentityKeyGenerator.cs
- [ ] T074 [P] Implement pre-key generator (signed pre-keys + one-time pre-keys + Kyber pre-keys with hybrid signatures) in src/ToledoMessage.Crypto/KeyManagement/PreKeyGenerator.cs
- [ ] T075 [P] Implement fingerprint generator (safety number from identity key pairs) in src/ToledoMessage.Crypto/KeyManagement/FingerprintGenerator.cs

**Checkpoint**: All crypto tests pass with `dotnet test tests/ToledoMessage.Crypto.Tests`. Full crypto library ready for consumption by client services.

---

## Phase 4: User Story 1 — Account Registration & Key Generation (Priority: P1) MVP

**Goal**: User creates account, system generates hybrid identity key material (classical + PQ), publishes pre-key bundle to server, displays security fingerprint.

**Independent Test**: Register a new user, verify server holds a valid pre-key bundle, verify client holds private keys in IndexedDB.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T076 [P] [US1] Write AuthController tests (register success, duplicate display name 409, weak password 400, login success, invalid credentials 401) in tests/ToledoMessage.Server.Tests/Controllers/AuthControllerTests.cs
- [ ] T077 [P] [US1] Write DevicesController registration tests (POST /api/devices success 201, validation 400, max devices 403) in tests/ToledoMessage.Server.Tests/Controllers/DevicesControllerTests.cs
- [ ] T078 [P] [US1] Write PreKeyService tests (store pre-keys, consume one-time pre-key, count remaining) in tests/ToledoMessage.Server.Tests/Services/PreKeyServiceTests.cs
- [ ] T079 [P] [US1] Write KeyGenerationService tests (generates identity keypair, generates pre-key batch, stores in IndexedDB) in tests/ToledoMessage.Client.Tests/Services/KeyGenerationServiceTests.cs

### Implementation for User Story 1

- [ ] T080 [US1] Implement AuthController (POST /api/auth/register, POST /api/auth/login) with Identity + JWT in src/ToledoMessage/Controllers/AuthController.cs
- [ ] T081 [US1] Implement DevicesController POST /api/devices (register device with pre-key bundle) in src/ToledoMessage/Controllers/DevicesController.cs
- [ ] T082 [US1] Implement PreKeyService (store pre-key bundle, consume one-time pre-keys, count remaining) in src/ToledoMessage/Services/PreKeyService.cs
- [ ] T083 [US1] Implement KeyGenerationService (orchestrate identity + pre-key generation using Crypto project) in src/ToledoMessage.Client/Services/KeyGenerationService.cs
- [ ] T084 [US1] Implement LocalStorageService (IndexedDB JS interop wrapper for storing private keys, sessions, messages) in src/ToledoMessage.Client/Services/LocalStorageService.cs
- [ ] T085 [P] [US1] Create Register.razor page (display name, password, auto key generation, pre-key upload) with @rendermode InteractiveWebAssembly in src/ToledoMessage.Client/Pages/Register.razor
- [ ] T086 [P] [US1] Create Login.razor page (display name, password, JWT storage) with @rendermode InteractiveWebAssembly in src/ToledoMessage.Client/Pages/Login.razor
- [ ] T087 [US1] Create Home.razor landing page (redirect to ChatList if authenticated, else show Register/Login) in src/ToledoMessage/Components/Pages/Home.razor

**Checkpoint**: User can register, login, and the server holds their pre-key bundle. Client IndexedDB holds private keys. US1 is fully functional and testable.

---

## Phase 5: User Story 2 — Initiating a Secure Conversation (Priority: P1)

**Goal**: User searches for another user, fetches their pre-key bundle, performs hybrid X3DH key exchange, sends first encrypted message, recipient decrypts it.

**Independent Test**: Two registered users — User A sends first message to User B, verify message arrives encrypted, decrypts correctly, server never sees plaintext.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T088 [P] [US2] Write UsersController search tests (GET /api/users/search returns matches, empty results, rate limit) in tests/ToledoMessage.Server.Tests/Controllers/UsersControllerTests.cs
- [ ] T089 [P] [US2] Write DevicesController pre-key bundle fetch tests (GET /api/users/{userId}/prekey-bundle, consumes one-time pre-key, 404 handling) in tests/ToledoMessage.Server.Tests/Controllers/DevicesControllerPreKeyTests.cs
- [ ] T090 [P] [US2] Write ConversationsController creation tests (POST /api/conversations, OneToOne type, duplicate prevention) in tests/ToledoMessage.Server.Tests/Controllers/ConversationsControllerTests.cs
- [ ] T091 [P] [US2] Write SessionService tests (X3DH initiation, session state persistence, pre-key bundle validation) in tests/ToledoMessage.Client.Tests/Services/SessionServiceTests.cs

### Implementation for User Story 2

- [ ] T092 [US2] Implement UsersController (GET /api/users/search?q={displayName}) in src/ToledoMessage/Controllers/UsersController.cs
- [ ] T093 [US2] Implement pre-key bundle fetch endpoint (GET /api/users/{userId}/prekey-bundle?deviceId={deviceId}) consuming one-time pre-key in src/ToledoMessage/Controllers/DevicesController.cs
- [ ] T094 [US2] Implement ConversationsController (POST /api/conversations for OneToOne type) in src/ToledoMessage/Controllers/ConversationsController.cs
- [ ] T095 [US2] Implement SessionService (X3DH session establishment — fetch bundle, run initiator, store session in IndexedDB) in src/ToledoMessage.Client/Services/SessionService.cs
- [ ] T096 [US2] Implement CryptoService (orchestrate session establishment + message encrypt/decrypt, facade over Crypto project) in src/ToledoMessage.Client/Services/CryptoService.cs
- [ ] T097 [US2] Implement MessageEncryptionService (encrypt plaintext → ciphertext using session, decrypt ciphertext → plaintext) in src/ToledoMessage.Client/Services/MessageEncryptionService.cs
- [ ] T098 [US2] Create NewConversation.razor page (user search input, results list, initiate conversation button) with @rendermode InteractiveWebAssembly in src/ToledoMessage.Client/Pages/NewConversation.razor
- [ ] T099 [US2] Create ChatList.razor page (list of conversations sorted by last message) with @rendermode InteractiveWebAssembly in src/ToledoMessage.Client/Pages/ChatList.razor
- [ ] T100 [P] [US2] Create ConversationListItem.razor component (conversation summary, unread count, last message preview) in src/ToledoMessage.Client/Components/ConversationListItem.razor

**Checkpoint**: Two users can establish a secure session via X3DH and exchange a first encrypted message. US2 is fully functional and testable.

---

## Phase 6: User Story 3 — Real-Time Message Exchange (Priority: P1)

**Goal**: Two users with an established session exchange messages in real-time via SignalR. Each message uses a unique key via Double Ratchet. Delivery/read status indicators update live. Offline messages are queued and delivered on reconnect.

**Independent Test**: Two users exchange 10+ messages rapidly; verify all arrive in order, each encrypted with distinct key, delivery status updates in real-time.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T101 [P] [US3] Write ChatHub tests (SendMessage, AcknowledgeDelivery, AcknowledgeRead, RegisterDevice, TypingIndicator) in tests/ToledoMessage.Server.Tests/Hubs/ChatHubTests.cs
- [ ] T102 [P] [US3] Write MessagesController tests (POST /api/messages, GET /api/messages/pending, POST /api/messages/acknowledge) in tests/ToledoMessage.Server.Tests/Controllers/MessagesControllerTests.cs
- [ ] T103 [P] [US3] Write MessageRelayService tests (online routing via SignalR, offline storage in DB, delivery confirmation) in tests/ToledoMessage.Server.Tests/Services/MessageRelayServiceTests.cs

### Implementation for User Story 3

- [ ] T104 [US3] Implement ChatHub (RegisterDevice, SendMessage, AcknowledgeDelivery, AcknowledgeRead, TypingIndicator) per SignalR hub contract in src/ToledoMessage/Hubs/ChatHub.cs
- [ ] T105 [US3] Implement MessagesController (POST /api/messages, GET /api/messages/pending?deviceId, POST /api/messages/acknowledge) in src/ToledoMessage/Controllers/MessagesController.cs
- [ ] T106 [US3] Implement MessageRelayService (route online messages via SignalR, store offline messages in DB, handle delivery confirmations) in src/ToledoMessage/Services/MessageRelayService.cs
- [ ] T107 [US3] Implement SignalRService (client-side hub connection, event handlers for ReceiveMessage, MessageDelivered, MessageRead, reconnection logic) in src/ToledoMessage.Client/Services/SignalRService.cs
- [ ] T108 [US3] Create Chat.razor page (message list, auto-scroll, real-time updates, Double Ratchet integration for per-message keys) with @rendermode InteractiveWebAssembly in src/ToledoMessage.Client/Pages/Chat.razor
- [ ] T109 [P] [US3] Create MessageBubble.razor component (message text, timestamp, delivery status icon) in src/ToledoMessage.Client/Components/MessageBubble.razor
- [ ] T110 [P] [US3] Create MessageInput.razor component (text input, send button, typing indicator trigger) in src/ToledoMessage.Client/Components/MessageInput.razor
- [ ] T111 [P] [US3] Create DeliveryStatus.razor component (sent/delivered/read icons) in src/ToledoMessage.Client/Components/DeliveryStatus.razor

**Checkpoint**: Full real-time messaging works — send/receive with Double Ratchet, delivery/read receipts, offline queuing. US3 is fully functional. MVP is now complete (US1 + US2 + US3).

---

## Phase 7: User Story 4 — Security Verification (Priority: P2)

**Goal**: Users can view and compare safety numbers (fingerprints) for identity verification. Conversations can be marked as "verified". Key change warnings alert users when a contact's identity key changes.

**Independent Test**: Two users compare displayed fingerprints (must match); simulate key change, verify warning appears and conversation marked unverified.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T112 [P] [US4] Write FingerprintService tests (generate matching fingerprints for both parties, different fingerprints for different keys) in tests/ToledoMessage.Client.Tests/Services/FingerprintServiceTests.cs
- [ ] T113 [P] [US4] Write FingerprintGenerator crypto tests (deterministic output, cross-verification) in tests/ToledoMessage.Crypto.Tests/KeyManagement/FingerprintGeneratorTests.cs

### Implementation for User Story 4

- [ ] T114 [US4] Implement FingerprintService (compute safety numbers from both users' identity keys, cache verification state) in src/ToledoMessage.Client/Services/FingerprintService.cs
- [ ] T115 [US4] Create SecurityInfo.razor page (display fingerprint as numeric code, verify/unverify button, both parties' identity info) with @rendermode InteractiveWebAssembly in src/ToledoMessage.Client/Pages/SecurityInfo.razor
- [ ] T116 [US4] Create KeyChangeWarning.razor component (prominent warning banner when IdentityKeyChanged event received) in src/ToledoMessage.Client/Components/KeyChangeWarning.razor
- [ ] T117 [US4] Handle IdentityKeyChanged SignalR event (update verification state, show warning, mark conversation unverified) in src/ToledoMessage.Client/Services/SignalRService.cs

**Checkpoint**: Users can verify each other's identity via safety numbers. Key changes trigger visible warnings. US4 is fully functional.

---

## Phase 8: User Story 5 — Multi-Device Support (Priority: P2)

**Goal**: Users can link up to 10 devices, each with independent key material. Messages are encrypted per-recipient-device (fan-out). Devices can be unlinked. Pre-key replenishment is automated.

**Independent Test**: Link a second device to an existing account; verify new messages arrive and decrypt on both devices; unlink one device, verify it stops receiving.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T118 [P] [US5] Write DevicesController multi-device tests (DELETE /api/devices/{id}, GET /api/devices/{id}/prekeys/count, POST /api/devices/{id}/prekeys replenishment) in tests/ToledoMessage.Server.Tests/Controllers/DevicesControllerMultiDeviceTests.cs
- [ ] T119 [P] [US5] Write fan-out encryption tests (message encrypted for each recipient device independently) in tests/ToledoMessage.Client.Tests/Services/MessageEncryptionFanOutTests.cs

### Implementation for User Story 5

- [ ] T120 [US5] Implement device management endpoints (DELETE /api/devices/{deviceId} revoke, GET /api/devices/{deviceId}/prekeys/count, POST /api/devices/{deviceId}/prekeys replenish) in src/ToledoMessage/Controllers/DevicesController.cs
- [ ] T121 [US5] Implement fan-out encryption (encrypt message once per recipient device using per-device session) in src/ToledoMessage.Client/Services/MessageEncryptionService.cs
- [ ] T122 [US5] Update ChatHub for multi-device message routing (route to all active devices of recipient user) in src/ToledoMessage/Hubs/ChatHub.cs
- [ ] T123 [US5] Handle PreKeyCountLow SignalR event (auto-generate and upload new one-time pre-keys) in src/ToledoMessage.Client/Services/SignalRService.cs
- [ ] T124 [US5] Create Settings.razor page (list linked devices, link new device, unlink device, device name display) with @rendermode InteractiveWebAssembly in src/ToledoMessage.Client/Pages/Settings.razor

**Checkpoint**: Multi-device support works — fan-out encryption, device management, pre-key replenishment. US5 is fully functional.

---

## Phase 9: User Story 6 — Group Messaging (Priority: P3)

**Goal**: Users create group conversations with up to 100 participants. Group messages use pairwise encrypted channels. Membership changes trigger key rotation. Admin management controls who can add/remove members.

**Independent Test**: Create group of 3+ users, exchange messages (all decrypt), add/remove member, verify key rotation and access control.

### Tests for User Story 6

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T125 [P] [US6] Write ConversationsController group tests (POST /api/conversations type=Group, POST /api/conversations/{id}/participants, DELETE /api/conversations/{id}/participants/{userId}, admin-only enforcement) in tests/ToledoMessage.Server.Tests/Controllers/ConversationsControllerGroupTests.cs
- [ ] T126 [P] [US6] Write group key distribution tests (pairwise channel encryption, key rotation on membership change) in tests/ToledoMessage.Client.Tests/Services/GroupSessionTests.cs

### Implementation for User Story 6

- [ ] T127 [US6] Implement group conversation creation (POST /api/conversations with type=Group, validate 2-100 participants, assign Admin role to creator) in src/ToledoMessage/Controllers/ConversationsController.cs
- [ ] T128 [US6] Implement participant management (POST /api/conversations/{id}/participants admin-only add, DELETE /api/conversations/{id}/participants/{userId} admin or self-removal) in src/ToledoMessage/Controllers/ConversationsController.cs
- [ ] T129 [US6] Implement group key distribution via pairwise channels (send group message encrypted individually per participant device) in src/ToledoMessage.Client/Services/SessionService.cs
- [ ] T130 [US6] Handle ParticipantAdded and ParticipantRemoved SignalR events (update local group state, trigger key rotation on removal) in src/ToledoMessage.Client/Services/SignalRService.cs
- [ ] T131 [US6] Update Chat.razor for group conversation display (participant list header, group name, member indicators) in src/ToledoMessage.Client/Pages/Chat.razor

**Checkpoint**: Group messaging works — creation, pairwise encryption, membership management, key rotation. US6 is fully functional.

---

## Phase 10: User Story 7 — Disappearing Messages (Priority: P3)

**Goal**: Users set disappearing message timers per conversation (e.g., 24h, 7d). Expired messages auto-delete from both devices. Manual deletion removes from local device only.

**Independent Test**: Enable 1-minute timer, send messages, verify automatic deletion after expiry on both sides.

### Tests for User Story 7

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T132 [P] [US7] Write conversation timer tests (PUT /api/conversations/{id}/timer, validate timer values, null to disable) in tests/ToledoMessage.Server.Tests/Controllers/ConversationsControllerTimerTests.cs
- [ ] T133 [P] [US7] Write client-side expiry tests (messages deleted after timer, manual delete local only) in tests/ToledoMessage.Client.Tests/Services/MessageExpiryTests.cs

### Implementation for User Story 7

- [ ] T134 [US7] Implement timer endpoint (PUT /api/conversations/{conversationId}/timer) in src/ToledoMessage/Controllers/ConversationsController.cs
- [ ] T135 [US7] Implement client-side message expiry (background timer checks ExpiresAt on LocalMessages, auto-delete expired) in src/ToledoMessage.Client/Services/LocalStorageService.cs
- [ ] T136 [US7] Implement server-side expired message cleanup (delete delivered EncryptedMessages past retention period) in src/ToledoMessage/Services/MessageRelayService.cs
- [ ] T137 [US7] Update Settings.razor to add disappearing timer configuration per conversation in src/ToledoMessage.Client/Pages/Settings.razor

**Checkpoint**: Disappearing messages work — timer configuration, client-side expiry, server cleanup. US7 is fully functional.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, performance, rate limiting, end-to-end tests, and final validation

- [ ] T138 [P] Implement RateLimitService middleware (registration per IP, messages per user/minute, search per user/minute) in src/ToledoMessage/Services/RateLimitService.cs
- [ ] T139 [P] Create performance benchmarks (key exchange, encrypt/decrypt, ratchet step) in tests/ToledoMessage.Benchmarks/CryptoBenchmarks.cs
- [ ] T140 Implement IndexedDB encryption at rest (derive storage encryption key from user password via HKDF) in src/ToledoMessage.Client/Services/LocalStorageService.cs
- [ ] T141 [P] Write end-to-end integration test: two-user registration + messaging flow in tests/ToledoMessage.Integration.Tests/TwoUserMessagingTests.cs
- [ ] T142 [P] Write end-to-end integration test: multi-device fan-out in tests/ToledoMessage.Integration.Tests/MultiDeviceTests.cs
- [ ] T143 [P] Write end-to-end integration test: group messaging flow in tests/ToledoMessage.Integration.Tests/GroupMessagingTests.cs
- [ ] T144 Security hardening review: verify no plaintext logging, no private keys on server, auth tags on all messages, HTTPS enforcement
- [ ] T145 Run quickstart.md validation (full two-user test scenario per specs/001-secure-messaging/quickstart.md)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all downstream phases
- **Crypto Library (Phase 3)**: Depends on Phase 1 completion (project exists) — BLOCKS all user stories involving encryption (US1-US7)
- **User Stories (Phase 4-10)**: Depend on BOTH Phase 2 and Phase 3 completion
  - User stories can then proceed sequentially in priority order (P1 → P2 → P3)
  - Or partially in parallel where dependencies allow
- **Polish (Phase 11)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Requires Phase 2 + Phase 3 — No dependencies on other stories
- **US2 (P1)**: Requires Phase 2 + Phase 3 + US1 (needs registered users with pre-key bundles)
- **US3 (P1)**: Requires Phase 2 + Phase 3 + US2 (needs established sessions for ratcheted messaging)
- **US4 (P2)**: Requires Phase 3 (fingerprint generator) + US1 (identity keys) — can start after US1
- **US5 (P2)**: Requires US1 + US3 (needs device registration + message routing infrastructure)
- **US6 (P3)**: Requires US2 + US3 (needs conversation creation + message exchange)
- **US7 (P3)**: Requires US3 (needs message storage infrastructure)

### Within Each User Story

- Tests MUST be written and FAIL before implementation (Constitution Principle VI)
- Server endpoints before client services that consume them
- Client services before UI components that use them
- Core implementation before integration/wiring

### Parallel Opportunities

**Phase 2 parallel groups:**
- All enums (T015-T019) in parallel
- All DTOs (T021-T027) in parallel
- All entity models (T028-T033) in parallel
- All EF Core configurations (T034-T039) in parallel
- All Blazor layout files (T043-T047) in parallel

**Phase 3 parallel groups:**
- All crypto tests (T048-T058) in parallel
- All classical primitives (T059-T061) in parallel
- Both PQ primitives (T062-T063) in parallel
- Key management classes (T073-T075) in parallel

**Cross-story parallelism:**
- US4 can start after US1 completes (doesn't need US2/US3)
- US7 can potentially start server-side timer work after US3

---

## Parallel Example: Phase 3 (Crypto Library)

```bash
# Launch all crypto tests in parallel (TDD — write first):
Task: "Write X25519 tests in tests/ToledoMessage.Crypto.Tests/Classical/X25519KeyExchangeTests.cs"
Task: "Write Ed25519 tests in tests/ToledoMessage.Crypto.Tests/Classical/Ed25519SignerTests.cs"
Task: "Write AES-GCM tests in tests/ToledoMessage.Crypto.Tests/Classical/AesGcmCipherTests.cs"
Task: "Write ML-KEM tests in tests/ToledoMessage.Crypto.Tests/PostQuantum/MlKemKeyExchangeTests.cs"
Task: "Write ML-DSA tests in tests/ToledoMessage.Crypto.Tests/PostQuantum/MlDsaSignerTests.cs"

# Then launch all classical + PQ implementations in parallel:
Task: "Implement X25519 in src/ToledoMessage.Crypto/Classical/X25519KeyExchange.cs"
Task: "Implement Ed25519 in src/ToledoMessage.Crypto/Classical/Ed25519Signer.cs"
Task: "Implement AES-GCM in src/ToledoMessage.Crypto/Classical/AesGcmCipher.cs"
Task: "Implement ML-KEM in src/ToledoMessage.Crypto/PostQuantum/MlKemKeyExchange.cs"
Task: "Implement ML-DSA in src/ToledoMessage.Crypto/PostQuantum/MlDsaSigner.cs"
```

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel:
Task: "Write AuthController tests in tests/ToledoMessage.Server.Tests/Controllers/AuthControllerTests.cs"
Task: "Write DevicesController tests in tests/ToledoMessage.Server.Tests/Controllers/DevicesControllerTests.cs"
Task: "Write PreKeyService tests in tests/ToledoMessage.Server.Tests/Services/PreKeyServiceTests.cs"
Task: "Write KeyGenerationService tests in tests/ToledoMessage.Client.Tests/Services/KeyGenerationServiceTests.cs"

# Then launch parallel UI components (after services are done):
Task: "Create Register.razor in src/ToledoMessage.Client/Pages/Register.razor"
Task: "Create Login.razor in src/ToledoMessage.Client/Pages/Login.razor"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 + 3)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: Crypto Library (CRITICAL — blocks all encryption)
4. Complete Phase 4: User Story 1 (Registration + Key Generation)
5. Complete Phase 5: User Story 2 (Initiate Secure Conversation)
6. Complete Phase 6: User Story 3 (Real-Time Message Exchange)
7. **STOP and VALIDATE**: Run quickstart.md two-user test scenario
8. Deploy/demo if ready — MVP delivers end-to-end encrypted real-time messaging

### Incremental Delivery

1. Complete Setup + Foundational + Crypto Library → Foundation ready
2. Add US1 → Test: user registers, keys generated → Deploy/Demo
3. Add US2 → Test: two users establish secure session → Deploy/Demo
4. Add US3 → Test: real-time encrypted messaging → Deploy/Demo (MVP!)
5. Add US4 → Test: safety number verification → Deploy/Demo
6. Add US5 → Test: multi-device support → Deploy/Demo
7. Add US6 → Test: group messaging → Deploy/Demo
8. Add US7 → Test: disappearing messages → Deploy/Demo
9. Polish → Security hardening, benchmarks, integration tests → Final release

### Single Developer Strategy

With one developer working sequentially:

1. Phase 1 → Phase 2 → Phase 3 (foundation complete)
2. US1 → US2 → US3 (MVP complete, ~60% of tasks)
3. US4 → US5 (P2 features complete)
4. US6 → US7 (P3 features complete)
5. Phase 11: Polish (hardening + benchmarks)

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the same phase
- [US#] label maps task to specific user story for traceability
- Each user story should be independently completable and testable after its prerequisites
- Tests MUST fail before implementing (Constitution Principle VI: Test-First Development)
- ALL primary keys use decimal(28,8) via DecimalTools.GetNewId() (Constitution compliance)
- ALL crypto runs client-side only (Constitution Principle I: Zero-Trust Server)
- BouncyCastle.Cryptography 2.6.2 is the ONLY crypto library (WASM constraint)
- Commit after each task or logical group
- Stop at any checkpoint to validate the story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
