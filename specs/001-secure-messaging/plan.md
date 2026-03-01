# Implementation Plan: Hybrid Post-Quantum Secure Messaging

**Branch**: `001-secure-messaging` | **Date**: 2026-02-26 | **Spec**: `specs/001-secure-messaging/spec.md`
**Input**: Feature specification from `/specs/001-secure-messaging/spec.md`

## Summary

Build a secure messaging application using hybrid post-quantum cryptography (classical X25519/Ed25519 + post-quantum ML-KEM-768/ML-DSA-65) to protect conversations against current and future quantum computing threats. The system implements the Signal Protocol (X3DH + Double Ratchet) with hybrid extensions, Sender Keys for groups, and a zero-trust server model where all crypto executes client-side in Blazor WebAssembly via BouncyCastle.Cryptography. Supports media attachments (image, video, audio, file), message reactions, replies, forwarding, link previews, in-conversation search, and opt-in encrypted key backup for multi-device identity continuity (see Constitution Exception CE-001).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS)
**Primary Dependencies**: BouncyCastle.Cryptography 2.6.2, ASP.NET Core Identity, SignalR, EF Core 10, Serilog
**Storage**: SQL Server 2022 (server-side via EF Core Code First) + Browser IndexedDB (client-side)
**Testing**: xUnit + BenchmarkDotNet + NBomber/k6 (load testing)
**Target Platform**: Web (Blazor WebAssembly), future: .NET MAUI mobile
**Project Type**: Web application (ASP.NET Core API + Blazor WASM client)
**Performance Goals**: Key exchange <500ms, message encrypt/decrypt <50ms, <1KB hybrid overhead, <2s message delivery, 10K concurrent users
**Constraints**: All crypto client-side only (zero-trust), BouncyCastle only (WASM-compatible), 64 KB max message size, 99.5% uptime
**Scale/Scope**: 10K concurrent users, up to 10 devices/user, up to 100 group participants, 90-day undelivered message retention

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Zero-Trust Server | ✅ PASS (CE-001) | All crypto client-side in Blazor WASM. Server stores only ciphertext, public keys, pre-key bundles, and encrypted key backup blobs (CE-001). 90-day auto-purge. No plaintext logging (Serilog configured). |
| II | Hybrid Cryptography | ✅ PASS | X25519 + ML-KEM-768 for KEM, Ed25519 + ML-DSA-65 for signatures, AES-256-GCM for AEAD, HKDF-SHA256 for KDF. Both layers must be broken for compromise. |
| III | Established Libraries Only | ✅ PASS | BouncyCastle.Cryptography 2.6.2 for all crypto (classical + PQ). No custom primitives. Single library for WASM compatibility. |
| IV | Signal Protocol Fidelity | ✅ PASS | X3DH + Double Ratchet with hybrid PQ extensions. Sender Keys for groups with membership-change rotation. |
| V | .NET Ecosystem | ✅ PASS | .NET 10 LTS, ASP.NET Core, SignalR, Blazor WASM (InteractiveWebAssembly), EF Core 10, SQL Server 2022, xUnit. |
| VI | Test-First Development | ✅ PASS | TDD (red-green-refactor). >=80% coverage, >=90% crypto. Dedicated crypto test suites. Integration tests for E2E flow. |
| VII | Open-Source Transparency | ✅ PASS | All source code and protocol documentation public. Security from algorithm strength, not obscurity. |

All gates PASS. One authorized exception documented: CE-001 (encrypted key backup extends server-stored data types). See constitution v1.2.0.

## Project Structure

### Documentation (this feature)

```text
specs/001-secure-messaging/
├── plan.md              # This file
├── research.md          # Phase 0: Technology decisions
├── data-model.md        # Phase 1: Entity definitions
├── quickstart.md        # Phase 1: Getting started guide
├── contracts/
│   ├── rest-api.md      # Phase 1: REST API contracts
│   └── signalr-hub.md   # Phase 1: SignalR hub contracts
└── tasks.md             # Phase 2: Implementation tasks (130 tasks)
```

### Source Code (repository root)

```text
src/
├── ToledoMessage/                    # ASP.NET Core server (API + SignalR + Blazor host)
│   ├── Controllers/
│   │   ├── AuthController.cs         # POST /api/auth/register, /login, /refresh
│   │   ├── DevicesController.cs      # POST/GET/DELETE /api/devices, pre-key endpoints
│   │   ├── UsersController.cs        # GET /api/users/search, pre-key bundle, devices
│   │   ├── ConversationsController.cs # GET/POST /api/conversations, group, participants, timer
│   │   ├── MessagesController.cs     # POST/GET /api/messages, pending, acknowledge
│   │   ├── KeyBackupController.cs    # POST/GET/DELETE /api/keys/backup (encrypted key backup)
│   │   └── PreferencesController.cs  # GET/PUT /api/preferences (theme, fontSize, sharedKeys)
│   ├── Hubs/
│   │   └── ChatHub.cs                # SignalR: SendMessage, ReceiveMessage, delivery/read acks
│   ├── Models/
│   │   ├── User.cs                   # User entity (Active/PendingDeletion/Deactivated states)
│   │   ├── Device.cs                 # Device entity with crypto key fields
│   │   ├── OneTimePreKey.cs          # One-time pre-key entity
│   │   ├── Conversation.cs           # Conversation entity
│   │   ├── ConversationParticipant.cs # Join table with roles
│   │   ├── EncryptedMessage.cs       # Encrypted message entity
│   │   ├── RefreshToken.cs           # Refresh token entity
│   │   ├── EncryptedKeyBackup.cs    # Encrypted identity key backup (CE-001)
│   │   ├── UserPreferences.cs       # User preferences (theme, fontSize, sharedKeys)
│   │   └── MessageReaction.cs       # Per-user emoji reactions on messages
│   ├── Data/
│   │   ├── ApplicationDbContext.cs   # EF Core DbContext
│   │   └── Configurations/          # Fluent API entity configurations
│   ├── Services/
│   │   ├── PreKeyService.cs          # Pre-key store/consume/count
│   │   ├── MessageRelayService.cs    # Message store/relay/acknowledge
│   │   ├── RateLimitService.cs       # Per-IP and per-user rate tracking
│   │   ├── AccountDeletionService.cs # 7-day grace period account deactivation
│   │   └── MessageCleanupHostedService.cs # Background: purge expired/90-day messages
│   ├── Middleware/
│   │   └── RateLimitMiddleware.cs    # Rate limit enforcement
│   ├── Components/                   # Blazor server-side shell
│   │   ├── App.razor
│   │   ├── MainLayout.razor
│   │   └── Pages/
│   └── Program.cs                    # DI, auth, CORS, SignalR, Serilog, health endpoint
│
├── ToledoMessage.Client/             # Blazor WebAssembly client (UI + crypto)
│   ├── Services/
│   │   ├── KeyGenerationService.cs   # Identity key + pre-key generation
│   │   ├── LocalStorageService.cs    # IndexedDB wrapper for private keys/state
│   │   ├── SessionService.cs         # X3DH session establishment
│   │   ├── CryptoService.cs          # Orchestrate sessions + encrypt/decrypt + Sender Keys
│   │   ├── MessageEncryptionService.cs # AES-256-GCM message encrypt/decrypt
│   │   ├── SignalRService.cs         # Hub connection + event handling + reconnect
│   │   ├── FingerprintService.cs     # Safety number derivation
│   │   ├── PreKeyReplenishmentService.cs # Auto-replenish OTPs
│   │   ├── MessageExpiryService.cs   # Client-side disappearing message timer
│   │   ├── ThemeService.cs           # Dark/light mode
│   │   ├── PreferencesService.cs     # User preferences sync
│   │   ├── MessageStoreService.cs    # IndexedDB message persistence
│   │   ├── KeyBackupCryptoService.cs # PBKDF2 + AES-GCM key backup encryption
│   │   ├── KeyBackupService.cs       # Upload/restore/delete key backups
│   │   ├── ToastService.cs           # In-app toast notifications
│   │   ├── AuthTokenHandler.cs       # JWT refresh interceptor
│   │   ├── TabLeaderService.cs       # BroadcastChannel leader election
│   │   └── NotificationService.cs    # Browser Notification API
│   ├── Pages/
│   │   ├── Register.razor            # Account creation + key generation
│   │   ├── Login.razor               # Authentication
│   │   ├── ChatList.razor            # Conversation list
│   │   ├── Chat.razor                # Active chat view
│   │   ├── NewConversation.razor     # User search + conversation creation
│   │   ├── SecurityInfo.razor        # Fingerprint verification
│   │   └── Settings.razor            # Device management + account deletion + notification prefs
│   ├── Components/
│   │   ├── MessageBubble.razor
│   │   ├── MessageInput.razor
│   │   ├── DeliveryStatus.razor
│   │   ├── ConversationListSidebar.razor
│   │   ├── AvatarInitials.razor
│   │   ├── LinkPreview.razor
│   │   ├── SkeletonLoader.razor
│   │   ├── KeyChangeWarning.razor
│   │   └── DisappearingTimerConfig.razor
│   ├── Components/Layout/
│   │   └── ChatLayout.razor
│   └── Program.cs                    # Client DI + HttpClient + auth
│
├── ToledoMessage.Crypto/             # Cryptographic library (BouncyCastle)
│   ├── Classical/
│   │   ├── AesGcmCipher.cs           # AES-256-GCM encrypt/decrypt
│   │   ├── Ed25519Signer.cs          # Ed25519 sign/verify
│   │   └── X25519KeyExchange.cs      # X25519 DH key exchange
│   ├── PostQuantum/
│   │   ├── MlKemKeyExchange.cs       # ML-KEM-768 encapsulate/decapsulate
│   │   └── MlDsaSigner.cs           # ML-DSA-65 sign/verify
│   ├── Hybrid/
│   │   ├── HybridKeyExchange.cs      # X25519 + ML-KEM combined
│   │   ├── HybridKeyDerivation.cs    # HKDF-SHA256 with domain separation
│   │   └── HybridSigner.cs          # Ed25519 + ML-DSA combined
│   ├── KeyManagement/
│   │   ├── IdentityKeyGenerator.cs   # Classical + PQ identity key pairs
│   │   ├── PreKeyGenerator.cs        # Signed pre-key + OTP batch
│   │   └── FingerprintGenerator.cs   # Safety number derivation
│   └── Protocol/
│       ├── RatchetState.cs           # Double Ratchet state structure
│       ├── PreKeyBundle.cs           # Pre-key bundle structure
│       ├── X3dhInitiator.cs          # X3DH initiator (4 DH + PQ KEM)
│       ├── X3dhResponder.cs          # X3DH responder
│       ├── MessageKeys.cs            # Chain key → message key derivation
│       └── DoubleRatchet.cs          # Double Ratchet algorithm
│
├── ToledoMessage.Shared/             # Shared DTOs, enums, constants
│   ├── Constants/
│   │   └── ProtocolConstants.cs      # Key sizes, limits, HKDF info strings
│   ├── Enums/                        # MessageType, ContentType, etc.
│   └── DTOs/                         # Request/response DTOs
│
└── Toledo.SharedKernel/              # Cross-cutting utilities
    └── Helpers/
        └── DecimalTools.cs           # Snowflake-style ID generation

tests/
├── ToledoMessage.Crypto.Tests/       # Crypto unit tests (classical, PQ, hybrid, protocol)
├── ToledoMessage.Client.Tests/       # Client service unit tests
├── ToledoMessage.Server.Tests/       # Server controller/service tests
├── ToledoMessage.Integration.Tests/  # End-to-end integration tests
└── ToledoMessage.Benchmarks/         # Performance benchmarks + load tests
```

**Structure Decision**: Multi-project solution with 5 source projects and 5 test projects. The Crypto library is isolated for independent testing and potential reuse. Shared DTOs/constants prevent duplication between server and client. The Blazor WASM client runs all crypto operations client-side per zero-trust model.

## Complexity Tracking

One authorized exception documented (CE-001). All other architectural decisions align with the 7 principles.

| Decision | Justification | Simpler Alternative Rejected |
|----------|--------------|------------------------------|
| 5 source projects | Crypto isolation for independent testing + reuse; Shared DTOs prevent duplication; Client/Server separation enforces zero-trust boundary | Single project would mix server and client crypto code, violating zero-trust boundary |
| Leader election (BroadcastChannel) | Prevents ratchet state corruption from concurrent IndexedDB writes across tabs | No tab handling would cause duplicate SignalR connections and potential crypto state corruption |
| Account deletion grace period | 7-day window prevents accidental permanent data loss while maintaining security (permanent deactivation after grace) | Immediate deletion is simpler but provides no recourse for accidental clicks |
| Encrypted key backup (CE-001) | Multi-device usability requires identity key continuity; encrypted client-side with PBKDF2+AES-GCM before upload; server stores only opaque blob | No backup (simpler, stricter zero-trust) but new devices appear as different users to contacts |
| Media as encrypted payload | Media bytes encrypted inside the same per-message E2E envelope, persisted to IndexedDB as base64 | Separate media upload service adds complexity; inline approach reuses existing crypto pipeline |
