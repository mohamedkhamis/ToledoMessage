# Implementation Plan: Hybrid Post-Quantum Secure Messaging

**Branch**: `001-secure-messaging` | **Date**: 2026-02-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-secure-messaging/spec.md`

## Summary

End-to-end encrypted messaging application using a hybrid cryptographic
approach that combines classical algorithms (X25519, Ed25519, AES-256-GCM)
with NIST-standardized post-quantum algorithms (ML-KEM-768, ML-DSA-65) to
protect conversations against both current and future quantum computing
threats. The system implements the Signal Protocol (X3DH + Double Ratchet)
with post-quantum extensions, delivering real-time messaging via SignalR,
multi-device support (up to 10), group messaging via Sender Keys, and
disappearing messages — all with a zero-trust server model where the server
never sees plaintext.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS)
**Primary Dependencies**: BouncyCastle.Cryptography 2.6.2, ASP.NET Core
Identity, EF Core 10.0.3, SignalR, JWT Bearer Authentication
**Storage**: SQL Server 2022 (server-side via EF Core Code First) +
Browser IndexedDB (client-side via LocalStorageService)
**Testing**: xUnit + BenchmarkDotNet
**Target Platform**: Web — Blazor WebAssembly (client) + ASP.NET Core
(server), Windows/Linux server hosting
**Project Type**: Web application (real-time encrypted messaging)
**Performance Goals**: Key exchange <500ms, message encrypt/decrypt <50ms,
<1KB hybrid overhead per message, <2s delivery, 10K concurrent users
**Constraints**: All crypto client-side only (zero-trust), hybrid PQ +
classical mandatory, 99.5% uptime (single-server MVP)
**Scale/Scope**: 10K concurrent users, 100 max group participants,
10 devices per user, 90-day undelivered message retention

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Evidence |
|---|-----------|--------|----------|
| I | Zero-Trust Server | ✅ PASS | All crypto in `ToledoMessage.Crypto` + `ToledoMessage.Client`. Server stores only encrypted ciphertext, public keys, pre-key bundles. No private keys or plaintext on server. 90-day auto-purge for undelivered messages. |
| II | Hybrid Cryptography | ✅ PASS | X25519 + ML-KEM-768 for key exchange, Ed25519 + ML-DSA-65 for signatures, AES-256-GCM for symmetric encryption, HKDF-SHA256 with domain separation (`ToledoMessage_RootKey`, `ToledoMessage_ChainKey`, `ToledoMessage_MessageKey`). |
| III | Established Libraries Only | ✅ PASS | BouncyCastle.Cryptography 2.6.2 is the sole crypto library (required for WASM). No custom primitives — only wrapper/composition code. |
| IV | Signal Protocol Fidelity | ✅ PASS | X3DH with PQ KEM extension (`X3dhInitiator`/`X3dhResponder`), Double Ratchet (`DoubleRatchet`), pre-key bundles (`PreKeyBundle`/`PreKeyGenerator`), Sender Keys for groups (spec-level). |
| V | .NET Ecosystem | ✅ PASS | .NET 10, ASP.NET Core Web API, SignalR, Blazor WASM (InteractiveWebAssembly), EF Core 10 + SQL Server 2022, xUnit + BenchmarkDotNet. All deps via NuGet. |
| VI | Test-First Development | ✅ PASS | Crypto unit tests (classical, PQ, hybrid, protocol), integration tests (two-user, multi-device, group), performance benchmarks. |
| VII | Open-Source Transparency | ✅ PASS | All source public. Security from algorithm strength, not obscurity. Bilingual docs (English/Arabic). |

**Gate result: ALL PASS** — proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/001-secure-messaging/
├── plan.md              # This file
├── research.md          # Phase 0 output — technology decisions
├── data-model.md        # Phase 1 output — entity model
├── quickstart.md        # Phase 1 output — getting started guide
├── contracts/           # Phase 1 output — API & SignalR contracts
│   ├── rest-api.md
│   └── signalr-hub.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Toledo.SharedKernel/           # Cross-cutting utilities
│   └── Helpers/
│       └── DecimalTools.cs
├── ToledoMessage/                 # ASP.NET Core server (Blazor host)
│   ├── Controllers/               # REST API endpoints
│   │   ├── AuthController.cs      #   /api/auth (register, login)
│   │   ├── ConversationsController.cs  # /api/conversations (CRUD, groups)
│   │   ├── DevicesController.cs   #   /api/devices (register, revoke, pre-keys)
│   │   ├── MessagesController.cs  #   /api/messages (store, pending, ack)
│   │   └── UsersController.cs     #   /api/users (search, pre-key bundles)
│   ├── Data/
│   │   ├── ApplicationDbContext.cs
│   │   └── Configurations/        # EF Core Fluent API configurations
│   ├── Hubs/
│   │   └── ChatHub.cs             # SignalR real-time messaging hub
│   ├── Middleware/
│   │   └── RateLimitMiddleware.cs  # Global rate limiting
│   ├── Migrations/                # EF Core Code First migrations
│   ├── Models/                    # EF Core entities
│   │   ├── User.cs
│   │   ├── Device.cs
│   │   ├── OneTimePreKey.cs
│   │   ├── Conversation.cs
│   │   ├── ConversationParticipant.cs
│   │   └── EncryptedMessage.cs
│   ├── Services/
│   │   ├── MessageCleanupHostedService.cs  # Background message purge
│   │   ├── MessageRelayService.cs          # Store + relay messages
│   │   ├── PreKeyService.cs                # Pre-key management
│   │   └── RateLimitService.cs             # Rate limit enforcement
│   ├── Components/                # Blazor server-side shell
│   └── Program.cs                 # DI, middleware, JWT config
├── ToledoMessage.Client/          # Blazor WebAssembly client
│   ├── Pages/                     # UI pages
│   │   ├── Register.razor
│   │   ├── Login.razor
│   │   ├── ChatList.razor
│   │   ├── Chat.razor
│   │   ├── NewConversation.razor
│   │   ├── SecurityInfo.razor
│   │   └── Settings.razor
│   ├── Components/                # Reusable UI components
│   │   ├── ConversationListItem.razor
│   │   ├── DeliveryStatus.razor
│   │   ├── DisappearingTimerConfig.razor
│   │   ├── KeyChangeWarning.razor
│   │   ├── MessageBubble.razor
│   │   └── MessageInput.razor
│   ├── Services/                  # Client-side services
│   │   ├── CryptoService.cs       #   Orchestrates session + encryption
│   │   ├── FingerprintService.cs  #   Safety number generation
│   │   ├── KeyGenerationService.cs #  Identity + pre-key generation
│   │   ├── LocalStorageService.cs #   IndexedDB persistence
│   │   ├── MessageEncryptionService.cs # Double Ratchet encrypt/decrypt
│   │   ├── MessageExpiryService.cs #   Disappearing message timers
│   │   ├── PreKeyReplenishmentService.cs # Auto-replenish OTPs
│   │   ├── SessionService.cs      #   X3DH session establishment
│   │   ├── SignalRService.cs      #   Real-time connection management
│   │   └── ThemeService.cs        #   UI theming
│   └── Program.cs                 # Client DI registration
├── ToledoMessage.Crypto/          # Cryptographic library (pure C#)
│   ├── Classical/
│   │   ├── AesGcmCipher.cs        #   AES-256-GCM encrypt/decrypt
│   │   ├── Ed25519Signer.cs       #   Ed25519 sign/verify
│   │   └── X25519KeyExchange.cs   #   X25519 Diffie-Hellman
│   ├── Hybrid/
│   │   ├── HybridKeyDerivation.cs #   HKDF-SHA256 with domain separation
│   │   ├── HybridKeyExchange.cs   #   X25519 + ML-KEM combined exchange
│   │   └── HybridSigner.cs        #   Ed25519 + ML-DSA combined signing
│   ├── KeyManagement/
│   │   ├── FingerprintGenerator.cs #   Safety number derivation
│   │   ├── IdentityKeyGenerator.cs #   Classical + PQ identity keys
│   │   └── PreKeyGenerator.cs     #   Signed + one-time pre-keys
│   └── Protocol/
│       ├── DoubleRatchet.cs       #   Double Ratchet algorithm
│       ├── MessageKeys.cs         #   Per-message key derivation
│       ├── PreKeyBundle.cs        #   Bundle data structure
│       ├── RatchetState.cs        #   Ratchet state management
│       ├── X3dhInitiator.cs       #   X3DH initiator (Alice)
│       └── X3dhResponder.cs       #   X3DH responder (Bob)
└── ToledoMessage.Shared/          # Shared DTOs, enums, constants
    ├── Constants/
    │   └── ProtocolConstants.cs   #   Key sizes, limits, HKDF info strings
    ├── DTOs/                      #   Request/response models
    └── Enums/                     #   MessageType, ContentType, etc.

tests/
├── ToledoMessage.Benchmarks/      # BenchmarkDotNet performance tests
├── ToledoMessage.Client.Tests/    # Client unit tests (scaffold)
├── ToledoMessage.Crypto.Tests/    # Comprehensive crypto tests
│   ├── Classical/                 #   AES-GCM, Ed25519, X25519
│   ├── Hybrid/                   #   Hybrid KDF, KX, signer
│   ├── KeyManagement/            #   Key generation tests
│   └── Protocol/                 #   Double Ratchet, X3DH, message keys
├── ToledoMessage.Integration.Tests/ # End-to-end integration tests
│   ├── TwoUserMessagingTests.cs
│   ├── MultiDeviceTests.cs
│   └── GroupMessagingTests.cs
└── ToledoMessage.Server.Tests/    # Server unit tests (scaffold)
```

**Structure Decision**: Web application pattern with 5 source projects
(server host, WASM client, crypto library, shared DTOs, shared kernel)
and 4 test projects (crypto unit, client unit, server unit, integration)
plus benchmarks. This structure enforces the zero-trust boundary: the
`ToledoMessage.Crypto` library is referenced only by the client, never
by the server.

## Complexity Tracking

> No constitution violations detected. All complexity is justified by
> the core security requirements (hybrid crypto, Signal Protocol).

| Decision | Why Needed | Simpler Alternative Rejected Because |
|----------|------------|-------------------------------------|
| 5 source projects | Enforces zero-trust boundary (crypto never on server) | Fewer projects would risk server referencing crypto internals |
| Hybrid key exchange | Constitution Principle II (NON-NEGOTIABLE) | Classical-only would not protect against quantum threats |
| Double Ratchet + X3DH | Constitution Principle IV (Signal Protocol) | Simpler key exchange would lack forward/post-compromise secrecy |
