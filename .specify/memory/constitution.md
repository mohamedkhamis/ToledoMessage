<!--
  Sync Impact Report
  ===================
  Version change: 1.0.1 → 1.1.0
  Modified principles:
    - I. Zero-Trust Server: Added server-side data retention policy —
      undelivered encrypted messages MUST be purged after 90 days.
      Codifies the data lifecycle boundary for server-stored ciphertext.
    - IV. Signal Protocol Fidelity: Added Sender Keys protocol requirement
      for group messaging. Each member generates a sender key distributed
      via pairwise sessions; O(1) encrypt per send; key rotation on
      membership change.
  Added sections: None
  Removed sections: None
  Templates requiring updates:
    - .specify/templates/plan-template.md: ✅ No changes needed (generic Constitution Check)
    - .specify/templates/spec-template.md: ✅ No changes needed (generic structure)
    - .specify/templates/tasks-template.md: ✅ No changes needed (generic structure)
    - CLAUDE.md: ✅ No changes needed (no constitution references to update)
    - docs/contribution_explained_simple.md: ✅ No changes needed (general overview doc)
  Follow-up TODOs: None
-->

# ToledoMessage Constitution

## Core Principles

### I. Zero-Trust Server (NON-NEGOTIABLE)

The server is an untrusted relay. All cryptographic operations — key
generation, key exchange, encryption, decryption, signing, and
verification — MUST execute exclusively on the client device.

- The server MUST never receive, compute, or store plaintext messages,
  private keys, session keys, or ratchet state.
- The server MUST store only: encrypted ciphertext blobs, public keys,
  and pre-key bundles.
- Undelivered encrypted messages queued for offline recipients MUST be
  automatically purged after 90 days. No server-side data MUST persist
  beyond its defined retention window.
- A full server compromise MUST NOT reveal any message content or
  enable decryption of past or future messages.
- This principle is absolute. No feature, optimization, or convenience
  shortcut may violate it.

### II. Hybrid Cryptography (NON-NEGOTIABLE)

Every cryptographic exchange MUST use a hybrid approach combining
classical and post-quantum algorithms ("belt and suspenders").

- Session establishment MUST combine classical Diffie-Hellman key
  exchange (X25519) with a NIST-standardized post-quantum key
  encapsulation mechanism (CRYSTALS-Kyber / ML-KEM).
- Digital signatures MUST combine classical (Ed25519) with
  post-quantum (CRYSTALS-Dilithium / ML-DSA) where authentication
  is required.
- Symmetric encryption MUST use AES-256-GCM with unique nonces per
  message.
- Key derivation MUST use HKDF-SHA256 with proper domain separation.
- If either the classical or post-quantum layer is compromised, the
  remaining layer MUST independently protect confidentiality.
- No single-layer-only cryptographic path is permitted for any
  user-facing operation.

### III. Established Libraries Only (NON-NEGOTIABLE)

All cryptographic primitives MUST come from well-audited, established
libraries. No custom cryptographic implementations are permitted.

- Classical operations (X25519, Ed25519, AES-256-GCM): MUST use one
  of the following approved libraries:
  - **BouncyCastle.Cryptography** — pure managed C#, required when
    targeting platforms where native libraries cannot load (e.g.,
    Blazor WebAssembly in the browser).
  - **libsodium via Sodium.Core** — native bindings, suitable for
    server-side or desktop targets with native library support.
  - **System.Security.Cryptography** — .NET built-in, suitable for
    server-side or desktop targets with OS-level crypto providers.
- Post-quantum operations (Kyber / ML-KEM, Dilithium / ML-DSA): MUST
  use BouncyCastle.Cryptography.
- When all crypto MUST execute in Blazor WebAssembly (per Principle I),
  BouncyCastle.Cryptography is the required single library for both
  classical and post-quantum operations, since native libraries
  (libsodium, CNG, OpenSSL) cannot load in the browser WASM runtime.
- Rolling custom crypto code (custom KEM, custom signatures, custom
  ciphers) is strictly prohibited. Wrapper and composition code is
  permitted; primitive reimplementation is not.
- Library versions MUST track the latest stable releases with known
  CVE patches applied.

### IV. Signal Protocol Fidelity

The messaging protocol MUST faithfully implement the Signal Protocol
with hybrid post-quantum extensions.

- Session establishment MUST follow X3DH (Extended Triple
  Diffie-Hellman) with an additional post-quantum KEM step.
- Per-message key derivation MUST use the Double Ratchet algorithm
  providing forward secrecy and post-compromise security.
- Pre-key bundles (identity key, signed pre-key, one-time pre-keys)
  MUST be published and managed per the Signal specification.
- Group messaging MUST use the Sender Keys protocol (Signal-style).
  Each group member generates a sender key and distributes it to all
  other members via existing pairwise encrypted sessions. Messages
  are encrypted O(1) per send. Sender keys MUST be rotated when
  group membership changes (member added or removed).
- Deviations from the Signal Protocol MUST be documented with
  security rationale and limited to the hybrid extension points.

### V. .NET Ecosystem

The project MUST use the .NET technology stack as defined below.
Deviations require explicit justification and constitution amendment.

- **Runtime**: .NET 8 or latest LTS (currently .NET 10)
- **Backend API**: ASP.NET Core Web API
- **Real-time transport**: SignalR (WebSocket-based)
- **Client (web)**: Blazor Web App template with
  InteractiveWebAssembly render mode for crypto-heavy components
  (all crypto executes in the browser WASM runtime)
- **Data persistence**: SQL Server 2022 with Entity Framework Core
  (version matching the target runtime, e.g., EF Core 10 for .NET 10)
- **Cross-platform (future)**: .NET MAUI for mobile
- **Testing**: xUnit with BenchmarkDotNet for performance profiling
- Third-party dependencies MUST be NuGet packages with active
  maintenance and compatible licenses (MIT, Apache 2.0, BSD).

### VI. Test-First Development

All features MUST be developed using test-driven development (TDD).
Tests are written and approved before implementation code.

- Red-Green-Refactor cycle MUST be followed: write failing test,
  implement to pass, refactor.
- Minimum code coverage target: 80% across all projects.
- Cryptographic operations MUST have dedicated test suites covering:
  encrypt/decrypt round-trip, tamper detection, nonce uniqueness,
  key derivation correctness, and cross-device interoperability.
- Integration tests MUST verify: end-to-end message flow (encrypt
  on sender, transport, decrypt on recipient), session establishment,
  ratchet advancement, and pre-key bundle lifecycle.
- Performance benchmarks MUST validate: key exchange <500ms,
  message encryption <50ms, <1KB overhead per message.

### VII. Open-Source Transparency

The project MUST be fully open-source with no security through
obscurity.

- All source code, cryptographic implementations, and protocol
  documentation MUST be publicly accessible.
- Security MUST derive from the strength of algorithms and key
  management, never from hidden implementation details.
- The project MUST support academic peer review and independent
  security auditing.
- Bilingual documentation (English and Arabic) SHOULD be maintained
  for educational accessibility.

## Security Requirements

- **Forward secrecy**: Compromise of long-term identity keys MUST NOT
  allow decryption of previously exchanged messages. Each message key
  MUST be derived, used once, and then deleted.
- **Post-compromise security**: After a session key compromise, the
  ratchet mechanism MUST restore security within a bounded number of
  message exchanges once new key material is introduced.
- **Key isolation**: Each device MUST maintain independent key
  material and ratchet state. Compromise of one device MUST NOT
  compromise sessions on other devices.
- **Pre-key exhaustion handling**: When one-time pre-keys are
  depleted, the system MUST fall back to the signed pre-key and
  trigger background replenishment. This fallback MUST NOT weaken
  the security guarantees below the hybrid baseline.
- **No plaintext logging**: Application logs MUST NOT contain
  plaintext message content, private keys, session keys, or any
  material that could aid decryption. Diagnostic logging MUST be
  limited to metadata (message IDs, timestamps, error codes).
- **Authentication tags**: Every encrypted message MUST include an
  authentication tag (AES-256-GCM). Messages with invalid tags
  MUST be rejected and discarded without processing.

## Development Workflow & Quality Gates

- **Branch strategy**: Feature branches follow the `NNN-short-name`
  convention (e.g., `001-secure-messaging`). All work targets the
  feature branch; merges to `main` require passing all gates.
- **Quality gate 1 — Tests pass**: All unit, integration, and
  contract tests MUST pass before merge.
- **Quality gate 2 — Coverage**: Code coverage MUST meet or exceed
  80%. Cryptographic modules SHOULD target 90%+.
- **Quality gate 3 — No secrets in code**: No private keys, passwords,
  connection strings, or secrets MUST be committed to the repository.
  Use environment variables or secure vault references.
- **Quality gate 4 — Security review**: Changes to cryptographic
  code (key generation, key exchange, encryption, signing) MUST
  receive explicit review focused on correctness, side-channel
  resistance, and compliance with this constitution.
- **Commit discipline**: Each commit SHOULD represent a single logical
  change. Commit messages MUST clearly describe the change purpose.

## Governance

This constitution is the supreme authority for all development
decisions in the ToledoMessage project. In case of conflict between
this constitution and any other document (specs, plans, tasks, or
code), the constitution prevails.

- **Amendment process**: Any change to this constitution MUST be
  documented with rationale, reviewed, and approved before taking
  effect. Amendments MUST increment the version following semantic
  versioning (MAJOR for principle removals/redefinitions, MINOR for
  additions/expansions, PATCH for clarifications).
- **Compliance verification**: All pull requests and code reviews
  MUST verify compliance with the Core Principles. Non-compliant
  code MUST NOT be merged.
- **Complexity justification**: Any architectural decision that adds
  complexity beyond the simplest viable approach MUST be justified
  in writing against these principles. YAGNI (You Aren't Gonna
  Need It) applies — do not build for hypothetical future needs.
- **Principle hierarchy**: When principles conflict, priority order
  is: I (Zero-Trust) > II (Hybrid Crypto) > III (Established Libs)
  > IV (Signal Fidelity) > VI (Test-First) > V (.NET Ecosystem)
  > VII (Transparency).

**Version**: 1.1.0 | **Ratified**: 2026-02-25 | **Last Amended**: 2026-02-26
