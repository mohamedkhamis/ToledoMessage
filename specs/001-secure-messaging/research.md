# Research: Hybrid Post-Quantum Secure Messaging

**Phase**: 0 — Outline & Research
**Date**: 2026-02-26
**Status**: Complete (all unknowns resolved)

## Technology Decisions

### 1. Post-Quantum KEM Algorithm

**Decision**: ML-KEM-768 (CRYSTALS-Kyber) via BouncyCastle.Cryptography

**Rationale**: ML-KEM (formerly CRYSTALS-Kyber) is the NIST-standardized
post-quantum key encapsulation mechanism (FIPS 203, August 2024). The
768 parameter set provides NIST Security Level 3 (equivalent to AES-192),
balancing security and performance. BouncyCastle.Cryptography 2.6.2
provides a production-ready, pure managed C# implementation that runs
in Blazor WebAssembly without native dependencies.

**Alternatives considered**:
- ML-KEM-512 (Level 1): Rejected — insufficient security margin for
  long-term protection of messaging keys.
- ML-KEM-1024 (Level 5): Rejected — larger keys (1568B public key)
  and ciphertext increase bandwidth overhead beyond the <1KB target
  for per-message hybrid overhead. Reserve for future high-assurance
  mode.
- NTRU (ntruhrss701): Rejected — not selected as NIST primary standard;
  smaller community and audit surface than ML-KEM.
- FrodoKEM: Rejected — significantly larger keys and ciphertexts, lower
  performance, not standardized by NIST.

### 2. Post-Quantum Signature Algorithm

**Decision**: ML-DSA-65 (CRYSTALS-Dilithium) via BouncyCastle.Cryptography

**Rationale**: ML-DSA (formerly CRYSTALS-Dilithium) is the NIST-standardized
post-quantum digital signature algorithm (FIPS 204, August 2024). The
65 parameter set provides Security Level 3, matching ML-KEM-768. Used
for signing pre-keys and identity verification in the hybrid signature
scheme alongside Ed25519.

**Alternatives considered**:
- ML-DSA-44 (Level 2): Rejected — does not match KEM security level.
- ML-DSA-87 (Level 5): Rejected — 2592B signatures significantly
  increase pre-key bundle size; overkill for messaging use case.
- SPHINCS+ (SLH-DSA): Rejected — hash-based signatures are much larger
  (up to 49KB) and slower, unsuitable for the bandwidth constraints.
- FALCON: Rejected — not yet finalized as NIST standard (pending FIPS
  206); complex floating-point arithmetic complicates WASM execution.

### 3. Classical Cryptography Suite

**Decision**: X25519 (DH), Ed25519 (signatures), AES-256-GCM (AEAD),
HKDF-SHA256 (KDF) — all via BouncyCastle.Cryptography

**Rationale**: Standard Signal Protocol primitives. X25519 and Ed25519
are the de facto standard for modern messaging protocols (Signal,
WhatsApp, Wire). AES-256-GCM provides authenticated encryption. Using
BouncyCastle for all primitives (including classical) ensures a single
crypto library dependency that works uniformly in Blazor WebAssembly,
eliminating the need for native interop.

**Alternatives considered**:
- libsodium (Sodium.Core): Rejected for client — native library cannot
  load in Blazor WASM. Acceptable for server-side, but constitution
  requires consistency; using a single library simplifies auditing.
- System.Security.Cryptography: Rejected for client — relies on OS
  crypto providers (CNG/OpenSSL) unavailable in WASM. Acceptable
  server-side but adds inconsistency.

### 4. Messaging Protocol

**Decision**: Signal Protocol (X3DH + Double Ratchet) with hybrid
post-quantum extensions

**Rationale**: The Signal Protocol is the most widely deployed and
audited end-to-end encryption protocol (used by Signal, WhatsApp,
Facebook Messenger). It provides forward secrecy (past messages stay
secret even if long-term keys are compromised) and post-compromise
security (future messages become secure again after ratchet advance).
The hybrid extension adds a ML-KEM KEM step alongside each X25519 DH
operation, combining shared secrets via HKDF with domain separation.

**Alternatives considered**:
- Matrix/Olm protocol: Rejected — less mature, known weaknesses in
  group key management, smaller audit surface.
- MLS (RFC 9420): Rejected for MVP — more complex tree-based key
  agreement, better suited for large groups but adds significant
  implementation complexity. May be reconsidered post-MVP for groups.
- Custom protocol: Rejected — Constitution Principle III prohibits
  custom cryptographic implementations.

### 5. Group Messaging Protocol

**Decision**: Sender Keys (Signal-style)

**Rationale**: Each group member generates a sender key and distributes
it to all other members via existing pairwise encrypted sessions.
Messages are encrypted O(1) per send (single symmetric encryption)
rather than O(n) per recipient. Key rotation occurs on membership
change. This is the proven approach used by Signal and WhatsApp,
well-documented, and aligns with the existing pairwise session
infrastructure.

**Alternatives considered**:
- Pairwise fan-out: Rejected — O(n) encryption per message scales
  poorly with 100-member groups and 10 devices per user (up to 1000
  encryptions per send).
- MLS (RFC 9420): Rejected for MVP — tree-based key agreement is more
  efficient at scale but adds significant protocol complexity (tree
  management, commit/welcome messages, epoch tracking).

### 6. Server Framework & Real-Time Transport

**Decision**: ASP.NET Core Web API + SignalR (WebSocket-based)

**Rationale**: ASP.NET Core provides a mature, high-performance web
framework with built-in support for JWT authentication, dependency
injection, and Entity Framework Core. SignalR provides real-time
bidirectional communication via WebSockets with automatic fallback
to long-polling, connection management, and group-based broadcasting.
The combination supports both REST endpoints (offline message retrieval,
account management) and real-time messaging.

**Alternatives considered**:
- gRPC: Rejected — limited browser support without gRPC-Web proxy;
  SignalR has native Blazor WASM integration.
- Raw WebSockets: Rejected — would require reimplementing connection
  management, reconnection, group broadcasting, and authentication
  token propagation that SignalR provides out of the box.

### 7. Client Framework

**Decision**: Blazor Web App (InteractiveWebAssembly render mode)

**Rationale**: Blazor WebAssembly runs C# directly in the browser
via WASM, enabling the crypto library (BouncyCastle.Cryptography) to
execute client-side without JavaScript interop or transpilation. This
is critical for the zero-trust model: all crypto operations happen in
the browser runtime. The InteractiveWebAssembly render mode allows
server-side prerendering with client-side interactivity for crypto.

**Alternatives considered**:
- React/Angular with JavaScript crypto: Rejected — would require
  JavaScript crypto libraries (or WASM crypto compiled from C/Rust),
  duplicating the crypto implementation and breaking the single-library
  constraint.
- .NET MAUI: Deferred to post-MVP — will be added for native mobile
  (iOS/Android) support using the same crypto library.

### 8. Data Persistence

**Decision**: SQL Server 2022 (server) + Browser IndexedDB (client)

**Rationale**: SQL Server provides ACID transactions, robust querying
(for user search, message retrieval), and scales to the 10K concurrent
user target. EF Core Code First migrations manage schema evolution.
Client-side IndexedDB (via LocalStorageService) stores private keys,
ratchet state, and decrypted messages — data that must never leave the
device per the zero-trust model.

**Alternatives considered**:
- PostgreSQL: Viable but SQL Server is specified in constitution
  (Principle V).
- SQLite (server): Rejected — concurrent write limitations at 10K
  users.
- SQLite (client, via sql.js): Considered for client — IndexedDB
  is simpler for key-value storage of crypto material.

### 9. Authentication Mechanism

**Decision**: JWT with short-lived access tokens (15 min) + refresh
tokens, per-device token pairs

**Rationale**: JWTs are stateless (no server-side session store),
support multi-device scenarios (each device holds its own token pair),
and integrate with SignalR via query string token propagation. Short
access token lifetime (15 min) limits the window of a stolen token.
ASP.NET Core has built-in JWT Bearer middleware.

**Alternatives considered**:
- Server-side sessions with cookies: Rejected — requires session store,
  does not naturally support multi-device, cookie-based auth is
  problematic for native mobile clients.
- Opaque bearer tokens (DB-backed): Rejected — requires DB lookup per
  request, negating the stateless benefit.

### 10. Pre-Key Management Strategy

**Decision**: Batch of 100 one-time pre-keys per device, replenish
at threshold of 10 remaining

**Rationale**: Signal's approach — upload 100 OTPs at registration,
monitor count, replenish when low. The batch size balances storage
(100 keys x ~32 bytes = ~3.2KB per device) against replenishment
frequency. Threshold of 10 ensures keys are available for new session
initiations even under burst conditions.

**Alternatives considered**:
- Larger batches (500+): Rejected — unnecessary storage overhead for
  most users; most users won't have 100 new contacts initiating in
  quick succession.
- On-demand generation: Rejected — requires the device to be online
  to generate keys, defeating the asynchronous session initiation
  model.

### 11. Server Observability

**Decision**: Serilog structured logging + ASP.NET Core `/health` endpoint

**Rationale**: Serilog provides structured logging with configurable sinks
(Console, File, Seq), integrates natively with ASP.NET Core's logging
pipeline, and supports enrichment (request ID, user ID). The built-in
ASP.NET Core health check middleware (`Microsoft.Extensions.Diagnostics.
HealthChecks`) provides `/health` with SQL Server connectivity check.
No full metrics/tracing stack (Prometheus, OpenTelemetry) for MVP.

**Alternatives considered**:
- NLog: Viable but Serilog has stronger structured logging and richer
  sink ecosystem.
- OpenTelemetry: Deferred — adds tracing/metrics infrastructure beyond
  MVP needs. Can be added later without breaking changes.
- Application Insights: Rejected — Azure-specific, adds vendor lock-in.

### 12. Browser Tab Coordination

**Decision**: BroadcastChannel API for leader election

**Rationale**: The BroadcastChannel API is a browser-native mechanism for
inter-tab communication with no external dependencies. A leader-follower
pattern ensures only one tab owns the SignalR connection and all crypto/
IndexedDB write operations, preventing ratchet state corruption from
concurrent writes. If the leader tab closes, followers detect the loss
via heartbeat messages and one promotes itself to leader.

**Alternatives considered**:
- SharedWorker: Rejected — limited browser support (no Safari), more
  complex lifecycle management.
- Service Worker: Rejected — designed for push notifications and caching,
  not inter-tab coordination. Deferred for push notification support.
- No coordination: Rejected — concurrent IndexedDB writes from multiple
  tabs would corrupt ratchet state.

### 13. Browser Notifications

**Decision**: Browser Notification API (not Push API)

**Rationale**: The Notification API is synchronous, requires the page to
be loaded (but can be unfocused), and only needs user permission. It's
straightforward to integrate from Blazor WASM via JS interop. Sufficient
for MVP where the user has the app open. Push notifications via Service
Worker are deferred post-MVP.

**Alternatives considered**:
- Push API + Service Worker: Deferred — requires VAPID key management,
  push subscription storage, and server-side push delivery. Significantly
  more complex.
- No notifications: Rejected — messaging apps require notification
  support for basic usability.

### 14. Key Backup Encryption (FR-024 / CE-001)

**Decision**: PBKDF2 (100K iterations, SHA-256) + AES-256-GCM for encrypting
identity key backups client-side before server upload.

**Rationale**: The user's password (already available during login) serves as
the key derivation input. PBKDF2 with 100,000 iterations and SHA-256 derives
a 256-bit AES key from the password + random 16-byte salt. The identity keys
(classical Ed25519/X25519 private + public, post-quantum ML-DSA-65/ML-KEM-768
private + public) are serialized to a JSON payload, encrypted with AES-256-GCM
(random 12-byte nonce), and the (blob, salt, nonce) triple is uploaded. The
server stores only opaque ciphertext. Uses BouncyCastle's `Pkcs5S2ParametersGenerator`
for PBKDF2 and the existing `AesGcmCipher` from `ToledoMessage.Crypto`.

**Alternatives considered**:
- Argon2id: Rejected — BouncyCastle.Cryptography 2.6.2 does not include Argon2.
  Would require an additional dependency (`Konscious.Security.Cryptography`),
  violating the single-crypto-library constraint.
- scrypt: Rejected — same dependency issue as Argon2.
- Higher PBKDF2 iterations (500K+): Rejected — would add multi-second delay
  on login in Blazor WASM (single-threaded). 100K is OWASP-recommended minimum.
- No backup (strict zero-trust): Rejected — unacceptable UX. Each new device
  gets a different identity, breaking contact trust chains. Constitution
  exception CE-001 documents this authorized trade-off.

### 15. Media Encryption Approach (FR-023)

**Decision**: Media bytes encrypted inline within the per-message E2E payload.

**Rationale**: Media content (image, video, audio, file) is included as base64
in the plaintext message payload before Double Ratchet encryption. This reuses
the existing per-message key derivation and AES-256-GCM encryption pipeline
without any additional crypto code. On the client, media is persisted to
IndexedDB as base64 alongside the message text and rendered via blob URLs.

**Alternatives considered**:
- Separate media upload with attachment keys: Rejected for MVP — adds server
  endpoints for encrypted blob storage, key distribution for media, and
  increases implementation complexity significantly.
- WebRTC data channels for large media: Rejected — overkill for file sharing;
  adds peer-to-peer complexity.

## Unresolved Items

None — all technical decisions are resolved.
