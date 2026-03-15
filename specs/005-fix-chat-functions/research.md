# Research: Fix All Chat Functions

**Date**: 2026-03-04
**Branch**: `005-fix-chat-functions`

## R-001: Blazor WASM byte[] JS Interop Marshalling

**Decision**: byte[] passed to JS interop via `IJSRuntime` is marshalled as `Uint8Array` (NOT Base64). No fix needed for JS interop byte handling.

**Rationale**: Since .NET 6 (breaking change), Blazor has special-cased `byte[]` to bypass `System.Text.Json` Base64 encoding. JavaScript receives `Uint8Array` directly. The existing `createObjectUrl` function in `media-helpers.js` (`new Uint8Array(byteArray)`) creates a valid copy. This was confirmed via Microsoft official documentation.

**Alternatives considered**:
- Adding Base64 decoding in JS: Unnecessary — would double-process the data
- Using `IJSStreamReference`: More complex, no benefit for our payload sizes (<15 MB)

**Source**: https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/6.0/byte-array-interop

## R-002: SignalR Enum Serialization

**Decision**: Default integer enum serialization works correctly. No `JsonStringEnumConverter` needed.

**Rationale**: Both server (`AddSignalR`, `AddControllers`) and client (`HubConnectionBuilder`) use default `System.Text.Json` which serializes enums as integers. `ContentType.Image = 1` → JSON `1` → deserialized as `ContentType.Image`. This is consistent across SignalR hub and REST API endpoints. No mismatch exists.

**Alternatives considered**:
- Adding `JsonStringEnumConverter`: Would work but is unnecessary — integer serialization is correct and consistent

## R-003: Root Causes Identified

After investigation, the confirmed issues are:

### Critical: storage.js storeMessages early-return bug
- File: `src/ToledoVault.Client/wwwroot/storage.js`
- The `storeMessages` function has a logic error where the `db` variable is referenced before `this.open()` is called in the early-return path
- This can cause IndexedDB persistence failures

### High: ClearChat silent failure
- File: `src/ToledoVault.Client/Pages/Chat.razor`
- `ClearChat()` catches and ignores all errors from both IndexedDB and SignalR clear operations
- User never knows if clear failed
- Also `_messageReactions.Clear()` removes ALL reactions instead of only reactions for deleted messages

### High: Reply context not cleared on message delete
- File: `src/ToledoVault.Client/Pages/Chat.razor`
- When a message is deleted via context menu (`DeleteForMe`), the reply preview is not checked/cleared if it references the deleted message

### Medium: Audio playback uses eval()
- File: `src/ToledoVault.Client/Components/MessageBubble.razor`
- Audio play/pause and time tracking use `Js.InvokeVoidAsync("eval", ...)` which is fragile
- Should use proper JS helper functions in `media-helpers.js`

### Medium: MediaBytes memory not released
- File: `src/ToledoVault.Client/Pages/Chat.razor`
- `DecryptEnvelopeToChatMessage` stores `MediaBytes = bytes` in the ChatMessage object
- These bytes stay in memory even after the blob URL is created

### Medium: Forward media fails silently
- File: `src/ToledoVault.Client/Pages/Chat.razor`
- `SendForwardedMessage` silently falls back to text-only if blob URL fetch fails
- User is not notified that media wasn't forwarded

### To investigate at implementation time
- The exact error users see when sending images from iPhone (HEIC validation, encryption session failures, etc.)
- Whether IndexedDB cached messages have corrupted ContentType values

## R-004: ClearChat SignalR Method

**Decision**: The server-side `ClearMessages` hub method exists and works correctly.

**Rationale**: `ChatHub.cs` (line 275) has `ClearMessages(decimal conversationId, DateTimeOffset from, DateTimeOffset to)` which correctly filters messages by the user's device IDs and timestamp range. The client-side issue is the silent error handling, not the server logic.
