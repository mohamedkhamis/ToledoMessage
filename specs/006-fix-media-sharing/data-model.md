# Data Model: Fix Media Sharing

**Feature**: 006-fix-media-sharing
**Date**: 2026-03-04

## New Entities

### MediaPayload (Client-side only — serialized before encryption)

Represents the structured payload that is serialized to JSON, UTF-8 encoded, and then encrypted as a single binary blob.

| Field       | Type     | Description                                                     | Constraints                          |
|-------------|----------|-----------------------------------------------------------------|--------------------------------------|
| `FileName`  | string?  | Original filename (e.g., "photo.jpg")                           | Max 255 chars, sanitized             |
| `MimeType`  | string   | MIME type (e.g., "image/jpeg")                                  | Required, validated against allowlist |
| `Caption`   | string?  | Optional caption text from the sender                           | Max 2000 chars (same as text limit)  |
| `Thumbnail` | string?  | Base64-encoded small JPEG thumbnail (~2-5 KB)                   | Max 10 KB decoded, null for documents |
| `Data`      | string   | Base64-encoded media bytes (compressed image or original file)  | Required, max ~15 MB decoded         |

**Lifecycle**:
1. Sender constructs `MediaPayload` → serializes to JSON → UTF-8 encodes → `EncryptBytesAsync` → Base64 ciphertext
2. Recipient → `DecryptToBytesAsync` → UTF-8 decode → deserialize JSON → extract fields

### BlobUrlRegistry (Client-side runtime only — not persisted)

Tracks active blob URLs for cleanup on dispose/navigation.

| Field       | Type                  | Description                          |
|-------------|-----------------------|--------------------------------------|
| `_blobUrls` | List&lt;string&gt;    | Active blob: URLs created this session |

**Note**: This already exists as `_blobUrls` in `Chat.razor`. No new entity needed, just consistent usage.

## Modified Entities

### ChatMessage (Client-side — `Chat.razor` inner class)

| Field        | Change                       | Reason                                          |
|--------------|------------------------------|-------------------------------------------------|
| `MediaBytes` | `init` → `set`              | Allow nulling after IndexedDB persistence        |
| `Text`       | Now carries caption for media | Caption extracted from `MediaPayload.Caption`    |

### SendMessageRequest (Shared DTO)

| Field      | Change                           | Reason                                    |
|------------|----------------------------------|-------------------------------------------|
| `FileName` | Always `null` for media messages | Moved inside encrypted `MediaPayload`     |
| `MimeType` | Always `null` for media messages | Moved inside encrypted `MediaPayload`     |

**Note**: The fields remain on the DTO for backward compatibility of the record type, but are set to `null` when `ContentType != Text`. No schema migration needed (DB will be cleared).

### MessageEnvelope (Shared DTO)

Same changes as `SendMessageRequest` — `FileName` and `MimeType` will be `null` for media; the recipient extracts them from the decrypted `MediaPayload`.

### EncryptedMessage (Server EF model)

No schema change. `FileName` and `MimeType` columns will simply be `null` for new media messages. The `Ciphertext` column continues to store the opaque encrypted blob (now containing the full `MediaPayload` instead of just raw bytes).

### StoredMessage (Client-side — IndexedDB model)

No schema change. `MediaDataBase64` and `MimeType` and `FileName` fields remain. The fix ensures received messages actually populate `MediaDataBase64` (currently broken — always `null` for incoming media).

## State Transitions

### Media Message Lifecycle (Sender)

```
[User selects file] → Attached (preview shown)
  → [User taps send] → Compressing (images only)
    → Encrypting → Sending (optimistic UI shown)
      → [Server acknowledges] → Sent
        → [Recipient acknowledges] → Delivered
```

### Media Message Lifecycle (Recipient)

```
[Envelope received] → Decrypting
  → [Payload deserialized] → Rendering (thumbnail shown instantly)
    → [Full media blob URL created] → Displayed
      → [Persisted to IndexedDB] → Cached
```

## Validation Rules

- File size: Max 15 MB after encryption overhead (enforced client-side before encryption AND server-side on receipt)
- Image compression: Images attached via photo picker are compressed (max 1600px, JPEG 80%). Documents bypass compression.
- Filename: Sanitized to remove path separators and null bytes. Max 255 characters.
- MIME type: Validated against a known allowlist. Unknown types default to `application/octet-stream`.
- Thumbnail: Max 10 KB decoded. Generated for images and videos only. Documents have no thumbnail.
- Caption: Max 2000 characters (matches existing text message limit).
