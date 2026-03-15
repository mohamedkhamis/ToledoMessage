# Research: Fix Media Sharing

**Feature**: 006-fix-media-sharing
**Date**: 2026-03-04

## R-001: Encrypted Media Payload Format

**Decision**: Serialize media metadata (filename, MIME type, caption, thumbnail) together with the raw media bytes into a single binary `MediaPayload` before encryption. The encrypted ciphertext will contain everything; `FileName`, `MimeType` fields on `SendMessageRequest` become null for media messages.

**Rationale**: Currently, `FileName` and `MimeType` travel as plaintext on the wire and are stored plaintext in SQL Server. This leaks metadata to the server. By bundling all metadata inside the encrypted payload, the server sees only opaque ciphertext — consistent with Principle I (Zero-Trust Server). No migration needed since DB will be cleared manually.

**Alternatives considered**:
- Encrypt metadata separately from media bytes (two ciphertext fields) — rejected: doubles encryption overhead and complicates the wire format.
- Keep metadata plaintext, encrypt only content — rejected: violates spec FR-013 and Zero-Trust principle.

**Format**:
```
MediaPayload (serialized as JSON, then encrypted as byte[]):
{
  "fileName": "photo.jpg",
  "mimeType": "image/jpeg",
  "caption": "Check this out!",
  "thumbnail": "<base64 of small JPEG thumbnail>",
  "data": "<base64 of media bytes>"
}
```

The JSON is UTF-8 encoded to `byte[]`, then passed to `EncryptBytesAsync` — same encryption path as today. On decryption, the recipient deserializes the JSON to extract all fields.

## R-002: Caption Bundling (Two-Message Problem)

**Decision**: Bundle caption text inside the `MediaPayload` JSON (the `caption` field) instead of sending it as a separate text message. The recipient extracts and displays the caption below the media in the same bubble.

**Rationale**: Current behavior sends captions as a separate unlinked text message that (a) appears disconnected from the media, (b) is not persisted to IndexedDB (`PersistMessageAsync` is never called for the caption), and (c) breaks the single-bubble WhatsApp UX.

**Alternatives considered**:
- Link caption via `ReplyToMessageId` — rejected: adds complexity, doesn't solve the display problem, and requires two decryptions.
- Concatenate caption as a text prefix in the encrypted bytes — rejected: fragile, no structured delimiter.

## R-003: Memory Leak — Outgoing Media Bytes

**Decision**: Change `ChatMessage.MediaBytes` from `init`-only to settable (`{ get; set; }`). After `PersistMessageAsync` completes, set `MediaBytes = null` to release the byte array. Similarly, for received media, persist the decrypted bytes to IndexedDB before nulling.

**Rationale**: Currently, `ChatMessage` properties are `init`-only (line 1718). Outgoing media holds the full `byte[]` for the entire session. For a user sending twenty 10 MB images, that's 200 MB of unreclaimable memory. `MediaDataUrl` (blob URL) is sufficient for display after persistence.

**Alternatives considered**:
- Use a `WeakReference<byte[]>` — rejected: GC may collect bytes before persistence completes.
- Store bytes in a separate dictionary keyed by message ID — rejected: over-engineering for a simple null-after-use pattern.

## R-004: Received Media Not Persisted to IndexedDB

**Decision**: After decrypting incoming media, persist the raw bytes (as base64) to IndexedDB via `PersistMessageAsync` before discarding them. This ensures received media survives page reloads.

**Rationale**: `DecryptEnvelopeToChatMessage` sets `MediaBytes = null` (line 1052), so `PersistMessageAsync` stores `MediaDataBase64 = null`. On reload, received media shows placeholder text ("Image unavailable"). This is a critical data loss bug.

**Alternatives considered**:
- Re-fetch from server on reload — rejected: server deletes messages after acknowledgment.
- Store the blob URL — rejected: blob URLs are session-scoped and invalid after page reload.

## R-005: Image Compression Strategy

**Decision**: Use client-side canvas-based compression for images before encryption. Resize large images to a max dimension (e.g., 1600px longest edge) and re-encode as JPEG at ~80% quality. Generate a thumbnail (200px) simultaneously. Offer "send as document" to skip compression and send original.

**Rationale**: WhatsApp compresses images by default. This reduces encryption time, transfer time, and storage. Canvas API is available in all modern browsers and works in Blazor WASM via JS interop.

**Alternatives considered**:
- Server-side compression — rejected: violates Zero-Trust (server would need plaintext image).
- No compression — rejected: user explicitly requested WhatsApp-like behavior.
- WebAssembly image library (e.g., ImageSharp) — rejected: adds large dependency, canvas API is simpler for browser context.

## R-006: Thumbnail Embedding

**Decision**: Generate a small JPEG thumbnail (200px longest edge, ~60% quality, ~2-5 KB) on the sender side using the canvas API. Embed as base64 in the `MediaPayload.thumbnail` field. For videos, extract first frame via `<video>` element + canvas capture.

**Rationale**: Enables instant preview on recipient side while full media decrypts/loads. WhatsApp uses blurred thumbnails for this purpose.

**Alternatives considered**:
- Send thumbnail as separate unencrypted message — rejected: leaks content to server.
- No thumbnail, show spinner — rejected: poor UX compared to WhatsApp.

## R-007: Test Infrastructure for Media

**Decision**: Add media tests in three locations:
1. `ToledoVault.Server.Tests` — server validation of media content types, size limits, relay
2. `ToledoVault.Crypto.Tests` or `ToledoVault.Integration.Tests` — `MessageEncryptionService` binary round-trip
3. `ToledoVault.Client.Tests` — `MediaPayload` serialization/deserialization, compression validation, caption bundling

**Rationale**: Existing test infrastructure uses MSTest 4.1.0 with hand-rolled stubs. No mocking library needed. `MessageEncryptionService` is pure C# (no JS interop) so can be tested directly. Server tests already have `TestDbContextFactory` and `StubHubContext`.

**Alternatives considered**:
- Jest for JS interop tests — rejected: out of scope; media-helpers.js is thin and tested implicitly through integration.
- Add Moq — rejected: project convention is hand-rolled stubs; adding a new dependency for this feature is unnecessary.

## R-008: ContentType Field on Wire

**Decision**: Keep `ContentType` as plaintext on `SendMessageRequest` since the server needs it to enforce different size limits (64 KB for text vs 15 MB for media). However, `FileName` and `MimeType` will be set to `null` for media messages (moved inside encrypted payload).

**Rationale**: The server's `GetMaxCiphertextSize(ContentType)` method uses `ContentType` to select the appropriate limit. Making `ContentType` encrypted would require the server to accept 15 MB for all messages (including text), which is a security risk.

**Alternatives considered**:
- Encrypt ContentType too, use single size limit — rejected: allows abuse by sending 15 MB "text" messages.
- Two-tier: encrypted ContentType + plaintext "isMedia" boolean — rejected: over-engineering, same information leakage.
