# Data Model: Fix All Chat Functions

**Date**: 2026-03-04
**Branch**: `005-fix-chat-functions`

## No Data Model Changes Required

This is a bug-fix feature. All issues are in client-side logic (JavaScript interop, Blazor component state management, error handling). No changes to:

- Database schema (SQL Server / EF Core entities)
- IndexedDB schema (storage.js object stores)
- DTO records (SendMessageRequest, MessageEnvelope, StoredMessage)
- API contracts (REST endpoints, SignalR hub methods)

## Existing Entities (reference only)

### ChatMessage (in-memory, Chat.razor)
- `MessageId`: decimal
- `Text`: string
- `Timestamp`: DateTimeOffset
- `IsMine`: bool
- `ContentType`: ContentType enum (Text=0, Image=1, Audio=2, Video=3, File=4)
- `FileName`: string?
- `MediaDataUrl`: string? (blob URL)
- `MediaBytes`: byte[]? — **to be nulled after blob URL creation (memory fix)**
- `MimeType`: string?
- `Status`: DeliveryStatus enum
- `ReplyToMessageId`: decimal?
- `ReplyToSenderName`: string?
- `ReplyToText`: string?
- `IsForwarded`: bool

### StoredMessage (IndexedDB)
- `MessageId`: string
- `ConversationId`: string
- `Text`: string
- `Timestamp`: string (ISO 8601)
- `IsMine`: bool
- `ContentType`: int
- `FileName`: string?
- `MediaDataBase64`: string? (Base64-encoded bytes)
- `MimeType`: string?
- `Status`: int
- `ReplyToMessageId`: string?
- `ReplyToText`: string?
- `ReplyToSenderName`: string?
