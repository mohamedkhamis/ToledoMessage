# Implementation Plan: Fix Media Sharing

**Branch**: `006-fix-media-sharing` | **Date**: 2026-03-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-fix-media-sharing/spec.md`

## Summary

Fix media file sharing (images, videos, documents) for 1:1 conversations to achieve WhatsApp-like functionality and UI. Key changes: (1) encrypt file metadata inside the payload instead of sending plaintext, (2) bundle captions with media in a single message, (3) fix memory leak from retained byte arrays on outgoing messages, (4) fix received media not persisting to IndexedDB, (5) add client-side image compression with "send as document" option, (6) embed thumbnails for instant preview, (7) improve video/document display UI, (8) add comprehensive unit tests. All changes scoped to 1:1 conversations only.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS), JavaScript (browser interop)
**Primary Dependencies**: Blazor WebAssembly, ASP.NET Core, SignalR, BouncyCastle.Cryptography 2.6.2, EF Core 10
**Storage**: SQL Server 2022 (server-side encrypted ciphertext), Browser IndexedDB (client-side cached media as base64)
**Testing**: MSTest 4.1.0, EF Core InMemory, hand-rolled stubs (no Moq)
**Target Platform**: Browser (Blazor WebAssembly) + ASP.NET Core server on Windows/IIS
**Project Type**: Web application (Blazor WASM hosted)
**Performance Goals**: Image compression <2s, encryption <500ms for 15 MB, thumbnail generation <500ms
**Constraints**: 15 MB max media size, all crypto client-side (WASM), no server-side plaintext access
**Scale/Scope**: 1:1 conversations only (group deferred), ~10 files modified, ~5 new test files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero-Trust Server | **PASS** | Media encrypted client-side. Metadata (filename, MIME) moves inside encrypted payload. Server sees only opaque ciphertext + ContentType (needed for size limit enforcement). |
| II. Hybrid Cryptography | **PASS** | Uses existing Signal Protocol encryption path (`EncryptBytesAsync`). No new crypto primitives introduced. |
| III. Established Libraries Only | **PASS** | BouncyCastle for encryption (existing). Canvas API for image compression (browser built-in). No new crypto libraries. |
| IV. Signal Protocol Fidelity | **PASS** | MediaPayload is just a different plaintext format fed into the same Double Ratchet. No protocol changes. |
| V. .NET Ecosystem | **PASS** | All changes within existing .NET/Blazor/SignalR stack. Canvas API accessed via standard JS interop. |
| VI. Test-First Development | **PASS** | Unit tests planned for encryption round-trips, payload serialization, compression, server validation. |
| VII. Open-Source Transparency | **PASS** | No security-through-obscurity. Payload format documented. |

**Post-Phase 1 re-check**: All gates still pass. The `MediaPayload` JSON serialization is a plaintext format change before encryption — it does not affect the wire format or crypto protocol.

## Project Structure

### Documentation (this feature)

```text
specs/006-fix-media-sharing/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── ToledoMessage/                          # Server (ASP.NET Core)
│   ├── Hubs/ChatHub.cs                     # No changes needed
│   ├── Services/MessageRelayService.cs     # No changes needed
│   └── Models/EncryptedMessage.cs          # No schema changes
│
├── ToledoMessage.Client/                   # Client (Blazor WASM)
│   ├── Pages/Chat.razor                    # MediaPayload integration, memory fix, persistence fix
│   ├── Components/
│   │   ├── MessageInput.razor              # Compression, "send as document", thumbnail gen
│   │   └── MessageBubble.razor             # Video player UI, document card, thumbnail preview
│   ├── Services/
│   │   └── CryptoService.cs                # No changes (encryption API unchanged)
│   └── wwwroot/
│       └── media-helpers.js                # compressImage, generateThumbnail, captureVideoFrame
│
├── ToledoMessage.Shared/                   # Shared DTOs & Models
│   ├── DTOs/SendMessageRequest.cs          # FileName/MimeType null for media
│   └── Models/MediaPayload.cs              # NEW — serializable payload record
│
tests/
├── ToledoMessage.Server.Tests/             # Add media content type tests
├── ToledoMessage.Integration.Tests/        # Add media encryption round-trip tests
└── ToledoMessage.Client.Tests/             # Add MediaPayload, compression, caption tests
```

**Structure Decision**: Existing multi-project structure (Server, Client, Shared, Crypto + 5 test projects). No new projects needed. One new file (`MediaPayload.cs`) in Shared. New test files in existing test projects.

## Complexity Tracking

No constitution violations to justify. All changes use existing patterns and infrastructure.
