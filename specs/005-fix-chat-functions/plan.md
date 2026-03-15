# Implementation Plan: Fix All Chat Functions

**Branch**: `005-fix-chat-functions` | **Date**: 2026-03-04 | **Spec**: `specs/005-fix-chat-functions/spec.md`
**Input**: Feature specification from `/specs/005-fix-chat-functions/spec.md`

## Summary

Fix broken chat functionality including: encrypted media transfer (images, videos, files), clear chat history, context menu actions, and audio playback. Primary focus is on fixing existing bugs in media handling where binary data transfer between app and browser is failing, and ensuring encrypted media renders correctly on the recipient side.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS)
**Primary Dependencies**: Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2, EF Core 10
**Storage**: SQL Server 2022 (server) + Browser IndexedDB (client via JS interop)
**Testing**: xUnit with BenchmarkDotNet
**Target Platform**: Browser (Blazor WebAssembly)
**Project Type**: Real-time encrypted messaging web application
**Performance Goals**: Message encryption <50ms, Media encryption <500ms for files up to 15MB
**Constraints**: Browser memory <500MB for 50+ media messages; all crypto client-side per constitution
**Scale/Scope**: 1:1 messaging (group messaging out of scope)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero-Trust Server | PASS | All crypto remains client-side; server only stores ciphertext |
| II. Hybrid Cryptography | PASS | Uses BouncyCastle for X25519 + Kyber; unchanged from existing |
| III. Established Libraries Only | PASS | BouncyCastle.Cryptography 2.6.2; no new libraries |
| IV. Signal Protocol Fidelity | PASS | No protocol changes; fixes are bug fixes only |
| V. .NET Ecosystem | PASS | .NET 10, Blazor WASM, SignalR, EF Core 10 |
| VI. Test-First Development | PASS | All fixes require passing tests; 215+ existing tests |
| VII. Open-Source Transparency | PASS | No changes to open-source nature |

**Gate Result**: PASS - Proceed to research

---

### Re-evaluation after Phase 1 (Research & Design)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero-Trust Server | PASS | Research confirmed: byte[] JS interop works correctly, no server-side changes needed |
| II. Hybrid Cryptography | PASS | No changes to crypto layer |
| III. Established Libraries Only | PASS | No new libraries added |
| IV. Signal Protocol Fidelity | PASS | No protocol changes |
| V. .NET Ecosystem | PASS | Same stack confirmed |
| VI. Test-First Development | PASS | quickstart.md provides test checklist |
| VII. Open-Source Transparency | PASS | No changes |

**Final Gate Result**: PASS - Ready for implementation

## Project Structure

### Documentation (this feature)

```text
specs/005-fix-chat-functions/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (NOT USED - internal app only)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── ToledoVault/               # Server (ASP.NET Core, SignalR hub, EF Core)
│   ├── Controllers/             # API endpoints
│   ├── Services/               # Business logic
│   ├── Hubs/                   # SignalR hubs
│   └── Data/                   # EF Core migrations
├── ToledoVault.Client/        # Blazor WebAssembly client
│   ├── Pages/                  # Route pages (Chat, Settings, etc.)
│   ├── Components/             # UI components
│   ├── Services/                # Client services
│   └── wwwroot/                 # JS interop
├── ToledoVault.Shared/        # Shared DTOs
├── ToledoVault.Crypto/        # Cryptographic operations
└── Toledo.SharedKernel/         # Common utilities

tests/                           # Existing test suite (215+ tests)
```

**Structure Decision**: Existing .NET solution structure with Blazor WASM client and separate crypto library. No new projects required for this bug-fix feature.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | This is a bug-fix feature using existing architecture | N/A |
