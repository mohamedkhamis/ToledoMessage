# Research: Hybrid Post-Quantum Secure Messaging

**Branch**: `001-secure-messaging` | **Date**: 2026-02-25

## R1: Cryptographic Library Selection for Blazor WASM

**Decision**: Use **BouncyCastle.Cryptography 2.6.2** as the single cryptographic library for ALL operations (classical + post-quantum).

**Rationale**: All cryptographic operations must execute client-side in Blazor WebAssembly (Constitution Principle I: Zero-Trust Server). This constrains library choices to pure managed C# — native libraries (libsodium, OpenSSL, Windows CNG) cannot load in the browser WASM runtime.

- **BouncyCastle.Cryptography** is pure managed C#, works in Blazor WASM, and provides every primitive we need:
  - X25519 key agreement (`Org.BouncyCastle.Crypto.Agreement.X25519Agreement`)
  - Ed25519 signatures (`Org.BouncyCastle.Crypto.Signers.Ed25519Signer`)
  - AES-256-GCM (`Org.BouncyCastle.Crypto.Modes.GcmBlockCipher`)
  - ML-KEM / Kyber-768 (`Org.BouncyCastle.Pqc.Crypto.MLKem.*`)
  - ML-DSA / Dilithium-3 (`Org.BouncyCastle.Pqc.Crypto.MLDsa.*`)
  - HKDF-SHA256 (`Org.BouncyCastle.Crypto.Generators.HkdfBytesGenerator`)

**Alternatives considered**:

| Alternative | Why Rejected |
|-------------|-------------|
| .NET 10 native `System.Security.Cryptography.MLKem` / `MLDsa` | Platform-specific backends (CNG, OpenSSL) — will not work in browser WASM |
| NSec.Cryptography 25.4.0 | Wraps native libsodium — will not load in browser WASM |
| Sodium.Core 1.4.0 | Wraps native libsodium — will not load in browser WASM |
| Native `AesGcm` + BouncyCastle for PQ only | Mixed library strategy adds complexity; BouncyCastle's AES-GCM is adequate for messaging workloads |

**Constitution note**: Principle III specifies "libsodium via Sodium.Core or System.Security.Cryptography" for classical operations. Since neither works in WASM, using BouncyCastle for all operations is a justified deviation. A constitution PATCH amendment (v1.0.1) should update Principle III to reflect the WASM constraint.

## R2: .NET 10 and Blazor Web App Template

**Decision**: Use .NET 10 with the Blazor Web App template (`dotnet new blazor -int Auto`).

**Rationale**: .NET 10 is the upcoming LTS release (Nov 2026). The Blazor Web App template provides a hybrid rendering model where crypto-heavy components use `@rendermode InteractiveWebAssembly` (runs in browser) while non-interactive pages use Static SSR (fast initial loads).

**Key architecture implication**: The template generates two projects:
- **Server project** (`ToledoMessage`): ASP.NET Core host, API endpoints, SignalR hubs, EF Core data access
- **Client project** (`ToledoMessage.Client`): Blazor WASM, all crypto operations, interactive UI

Components using `InteractiveWebAssembly` MUST live in the `.Client` project. All crypto code MUST be in the `.Client` project or a library it references.

## R3: Data Persistence — EF Core 10 Code First with SQL Server

**Decision**: Use **Microsoft.EntityFrameworkCore.SqlServer 10.0.3** with Code First migrations.

**Rationale**: User specified SQL Server Code First. EF Core 10 ships with .NET 10, supports named query filters (useful for soft deletes), JSON column types, and improved parameterized queries.

**Server stores only**:
- User accounts (display name, password hash)
- Device registrations and public keys
- Pre-key bundles (public keys only)
- Encrypted message envelopes (ciphertext blobs)
- Conversation metadata

**Server NEVER stores**: Private keys, session state, ratchet state, plaintext messages, decrypted content.

**Client-side storage**: Browser IndexedDB (via a JS interop wrapper or Blazored.LocalStorage) for local session state, ratchet keys, and decrypted message history.

## R4: Real-Time Transport — SignalR

**Decision**: Use ASP.NET Core SignalR (built-in) with `Microsoft.AspNetCore.SignalR.Client 10.0.3` in the client project.

**Rationale**: SignalR ships with ASP.NET Core — no extra server package needed. The client package installs in the `.Client` project. SignalR provides WebSocket-based bidirectional communication for real-time message delivery and status updates.

**Key pattern**: SignalR components MUST use `@rendermode InteractiveWebAssembly` to avoid server-to-self connections (port exhaustion issue).

## R5: Testing Framework

**Decision**: Use **xUnit v3 3.2.2** for unit/integration tests and **BenchmarkDotNet 0.15.8** for performance benchmarks.

**Rationale**: xUnit v3 is the recommended version for new .NET 10 projects with native Microsoft Testing Platform integration. BenchmarkDotNet 0.15.8 has explicit .NET 10 runtime monikers (`RuntimeMoniker.Net10`).

## R6: Signal Protocol Implementation Approach

**Decision**: Implement X3DH + Double Ratchet from scratch using BouncyCastle primitives, following the Signal Protocol specification with hybrid PQ extensions.

**Rationale**: No existing .NET library implements the Signal Protocol with post-quantum extensions. The protocol is well-documented (Signal Foundation specifications + PQXDH paper) and consists of composing established primitives:

1. **X3DH (with PQ extension)**: 4 classical DH operations (X25519) + 1 KEM operation (ML-KEM-768) → combined shared secret via HKDF
2. **Double Ratchet**: Symmetric-key ratchet (HKDF) + DH ratchet (X25519) → per-message keys
3. **Message encryption**: AES-256-GCM with derived per-message key

This is **composition code** (permitted by Constitution Principle III), not primitive reimplementation.

## R7: Client-Side Storage for Crypto State

**Decision**: Use browser **IndexedDB** via JS interop for client-side persistent storage of crypto state, session data, and decrypted messages.

**Rationale**: Ratchet state, session keys, and plaintext messages must persist across browser sessions but MUST never leave the client. IndexedDB is the only browser storage API with sufficient capacity (no 5MB limit like localStorage) and supports structured data. Access will be through a thin JS interop wrapper.

**Alternatives considered**:

| Alternative | Why Rejected |
|-------------|-------------|
| localStorage | 5MB limit; string-only; insufficient for key material + message history |
| Cache API | Designed for HTTP responses, not structured data |
| WebSQL | Deprecated |
| OPFS (Origin Private File System) | Limited browser support; more complex API |

## Package Version Summary

| Package | NuGet ID | Version |
|---------|----------|---------|
| BouncyCastle | `BouncyCastle.Cryptography` | 2.6.2 |
| EF Core SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.3 |
| EF Core Design | `Microsoft.EntityFrameworkCore.Design` | 10.0.3 |
| EF Core Tools | `Microsoft.EntityFrameworkCore.Tools` | 10.0.3 |
| SignalR Client | `Microsoft.AspNetCore.SignalR.Client` | 10.0.3 |
| xUnit v3 | `xunit.v3` | 3.2.2 |
| BenchmarkDotNet | `BenchmarkDotNet` | 0.15.8 |
| ASP.NET Core Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | 10.0.3 |
