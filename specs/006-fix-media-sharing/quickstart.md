# Quickstart: Fix Media Sharing

**Feature**: 006-fix-media-sharing
**Date**: 2026-03-04

## Prerequisites

- .NET 10 SDK installed
- SQL Server 2022 running locally
- Node.js (for any JS tooling, if needed)
- Browser with WebAssembly support (Chrome/Edge/Firefox)

## Build & Run

```bash
# From repository root
cd src/ToledoMessage
dotnet build
dotnet run
```

App runs at `https://localhost:5001` (development) or `http://localhost:8080` (IIS deploy).

## Run Tests

```bash
# All tests
dotnet test

# Specific test projects
dotnet test tests/ToledoMessage.Server.Tests/
dotnet test tests/ToledoMessage.Crypto.Tests/
dotnet test tests/ToledoMessage.Integration.Tests/
dotnet test tests/ToledoMessage.Client.Tests/
```

## Deploy to IIS

```powershell
powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force
```

## Key Files to Modify

### Client (Blazor WASM)

| File | Changes |
|------|---------|
| `src/ToledoMessage.Client/Pages/Chat.razor` | MediaPayload serialization, caption bundling, memory fix, received media persistence |
| `src/ToledoMessage.Client/Components/MessageInput.razor` | Image compression via JS interop, "send as document" option |
| `src/ToledoMessage.Client/Components/MessageBubble.razor` | Video player UI, document file card, thumbnail preview |
| `src/ToledoMessage.Client/wwwroot/media-helpers.js` | Image compression, thumbnail generation, video frame capture |
| `src/ToledoMessage.Client/Services/CryptoService.cs` | No changes (encryption API unchanged) |

### Shared (DTOs)

| File | Changes |
|------|---------|
| `src/ToledoMessage.Shared/DTOs/SendMessageRequest.cs` | No schema change; `FileName`/`MimeType` set to null for media |
| `src/ToledoMessage.Shared/Models/MediaPayload.cs` | **NEW** — MediaPayload record for serialization |

### Server

| File | Changes |
|------|---------|
| `src/ToledoMessage/Hubs/ChatHub.cs` | No changes needed (validates ciphertext size, doesn't inspect content) |
| `src/ToledoMessage/Services/MessageRelayService.cs` | No changes needed |

### Tests

| File | Changes |
|------|---------|
| `tests/ToledoMessage.Server.Tests/` | Add media content type validation tests |
| `tests/ToledoMessage.Integration.Tests/` | Add media encryption round-trip tests |
| `tests/ToledoMessage.Client.Tests/` | Add MediaPayload serialization, compression, caption bundling tests |

## Testing Media Locally

1. Open two browser windows (or use two different browsers)
2. Register two users and start a 1:1 conversation
3. Test: Attach image → verify compression → send → verify recipient sees thumbnail → tap for lightbox
4. Test: Attach video → send → verify recipient sees inline player
5. Test: Attach document → send → verify recipient can download
6. Test: Attach image with caption → verify single bubble with caption below image
7. Test: Send image, reload page → verify image loads from IndexedDB cache
