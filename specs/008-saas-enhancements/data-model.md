# Data Model: ToledoVault SaaS Enhancement Plan (v2.0)

**Feature**: `008-saas-enhancements`
**Date**: 2026-03-09

## Purpose

This document describes all data model changes required for the v2.0 enhancements. Changes are **additive only** — no existing columns are removed or renamed, no existing tables are dropped.

---

## Server-Side (SQL Server via EF Core)

### Existing Entities — No Schema Changes Required

The following existing entities are affected by code changes but require **no database schema modifications**:

| Entity | Code Changes | Why No Schema Change |
|--------|-------------|---------------------|
| `ConversationParticipant` | Group admin logic uses existing `Role` column (`ParticipantRole.Admin`) | `ParticipantRole` enum already has `Admin = 1` value |
| `EncryptedMessage` | Read pointer clamping queries `MAX(SequenceNumber)` | Existing `SequenceNumber` column is sufficient |
| `ConversationReadPointer` | Clamped value written to existing pointer | No new columns needed |
| `User` | `LastSeenAt` updated by presence timeout | Already exists |
| `OneTimePreKey` | Cleanup of consumed keys (`IsUsed = true`, older than 30 days) | Existing `IsUsed` and `CreatedAt` columns are sufficient |
| `RefreshToken` | Already cleaned up by `MessageCleanupHostedService` | No changes |

### New Database Index (Performance)

**Purpose**: Optimize user search queries (FR-015).

```text
Entity: User
Index Name: IX_User_Username_DisplayName
Columns: Username, DisplayName
Type: Non-clustered
Rationale: UsersController.Search currently uses .ToLower().Contains() which causes
           full table scan. With this index + EF.Functions.Like(), SQL Server can use
           index seek for prefix searches or index scan for contains searches,
           both faster than the current approach.
```

**Implementation**: Add in `UserConfiguration.cs`:
```
builder.HasIndex(u => u.Username).HasDatabaseName("IX_User_Username");
builder.HasIndex(u => u.DisplayName).HasDatabaseName("IX_User_DisplayName");
```

### Concurrency Token for Group Admin (First-Write-Wins)

**Purpose**: Support first-write-wins conflict resolution for concurrent admin actions (FR-033).

```text
Entity: ConversationParticipant
New Column: RowVersion (byte[], SQL Server rowversion/timestamp)
Purpose: EF Core concurrency token. When two admins simultaneously modify
         participation (e.g., remove each other), the second SaveChangesAsync
         call throws DbUpdateConcurrencyException, which the controller
         catches and returns a "state changed, please refresh" error.
```

**Implementation**: Add to `ConversationParticipant` model:
```
[Timestamp]
public byte[] RowVersion { get; set; }
```

Add in `ConversationParticipantConfiguration.cs`:
```
builder.Property(cp => cp.RowVersion).IsRowVersion();
```

---

## Client-Side (Browser IndexedDB)

### Existing Object Store: `messages`

**No schema changes.** Virtual scrolling reads from this store using range queries (offset + count).

**New query pattern**: `getMessages(conversationId, offset, count)` uses `IDBKeyRange` on the `conversationId` index with cursor advancement for offset/count pagination.

### New Object Store: `offlineQueue`

**Purpose**: Store messages queued for sending when SignalR is disconnected (FR-032).

```text
Store Name: offlineQueue
Key Path: id (auto-increment)
Indexes:
  - conversationId (non-unique) — for filtering queue by conversation
  - status (non-unique) — for querying pending/failed messages
  - createdAt (non-unique) — for ordering

Entry Schema:
{
  id:                 number          (auto-increment primary key)
  conversationId:     string          (long serialized as string for JS safety)
  recipientDeviceId:  string          (long serialized as string)
  senderDeviceId:     string          (long serialized as string)
  ciphertext:         string          (base64-encoded encrypted message)
  contentType:        number          (ContentType enum value: 0=Text, 1=Image, etc.)
  messageType:        number          (MessageType enum value)
  fileName:           string | null   (for file/media messages)
  mimeType:           string | null   (for file/media messages)
  replyToMessageId:   string | null   (long as string, for reply messages)
  status:             string          ('pending' | 'sending' | 'failed')
  retryCount:         number          (0-3, incremented on each failed attempt)
  createdAt:          string          (ISO 8601 timestamp)
  error:              string | null   (error message if status is 'failed')
}

Constraints:
  - Maximum 50 entries total (enforced in JS before insertion)
  - Entries with status 'failed' and retryCount >= 3 are permanent failures
  - Entries are deleted on successful send
```

**IndexedDB version**: Increment the version number in `storage.js` `indexedDB.open()` call to trigger the `onupgradeneeded` handler which creates the new store.

### New Object Store: `expiryTrackers`

**Purpose**: Track disappearing messages and their expiry times for cleanup (FR-031).

```text
Store Name: expiryTrackers
Key Path: messageId (string, long serialized)
Indexes:
  - expiresAt (non-unique) — for querying expired messages on startup

Entry Schema:
{
  messageId:        string          (long serialized as string)
  conversationId:   string          (long serialized as string)
  expiresAt:        string          (ISO 8601 timestamp when message should be removed)
}

Lifecycle:
  - Created when a message with disappearing timer is received/displayed
  - Deleted when the message is removed from display (timer expired)
  - On app startup: scan all entries, remove those where expiresAt < now
```

---

## In-Memory Data Structures (Server)

### ChatHub Caches

These are `static` fields on `ChatHub`, stored in server memory. They are NOT persisted to database.

```text
1. ConnectionDeviceMap (EXISTING)
   Type: ConcurrentDictionary<string, long>
   Key: SignalR connectionId
   Value: deviceId
   Lifecycle: Populated in RegisterDevice, removed in OnDisconnectedAsync

2. ConnectionDisplayNameMap (NEW)
   Type: ConcurrentDictionary<string, string>
   Key: SignalR connectionId
   Value: user's DisplayName
   Lifecycle: Populated in RegisterDevice, removed in OnDisconnectedAsync
   Purpose: Avoid DB query for display name in TypingIndicator

3. ParticipantCache (NEW)
   Type: ConcurrentDictionary<long, (List<long> UserIds, DateTimeOffset CachedAt)>
   Key: conversationId
   Value: tuple of participant user IDs and cache timestamp
   TTL: 60 seconds (checked on read)
   Lifecycle: Populated on first TypingIndicator call for a conversation,
              refreshed after TTL expires, invalidated when participants change
   Purpose: Avoid DB query for participant list in TypingIndicator
```

### RateLimitService Keys (Existing Service, New Key Patterns)

```text
Existing keys (HTTP middleware):
  - "ip:{ipAddress}:{route}" — anonymous rate limiting
  - "user:{userId}:{route}" — authenticated rate limiting

New keys (SignalR hub):
  - "signalr:send:{userId}" — SendMessage rate limit (60/min)
  - "signalr:typing:{userId}" — TypingIndicator rate limit (10/min)
```

---

## State Transitions

### Offline Queue Entry Lifecycle

```text
[User sends message while offline]
    → status: 'pending', retryCount: 0

[SignalR reconnects, flush starts]
    → status: 'sending'

    [Send succeeds]
        → entry DELETED from store

    [Send fails, retryCount < 3]
        → status: 'pending', retryCount: retryCount + 1
        → (will retry on next flush cycle)

    [Send fails, retryCount >= 3]
        → status: 'failed'
        → (permanent failure, user must manually retry or discard)

[User manually retries a 'failed' entry]
    → status: 'sending', retryCount: reset to 0
    → (follows same success/failure flow)

[User discards a 'failed' entry]
    → entry DELETED from store
```

### Disappearing Message Tracker Lifecycle

```text
[Message with timer received and displayed]
    → expiryTracker entry created with expiresAt = receivedAt + timerDuration

[Timer expires while app is open]
    → MessageExpiryService fires OnMessageExpired event
    → Message removed from display (Chat.razor)
    → Message removed from IndexedDB 'messages' store
    → expiryTracker entry DELETED

[Timer expires while app is closed]
    → (no action until app reopens)

[App opens / page loads]
    → Scan all expiryTracker entries
    → For each where expiresAt < now:
        → Remove message from IndexedDB 'messages' store
        → Delete expiryTracker entry
    → For each where expiresAt >= now:
        → Register with MessageExpiryService for future expiry
```

---

## Migration Notes

- **Database migration**: A new EF Core migration is needed for:
  1. New indexes on `User.Username` and `User.DisplayName`
  2. New `RowVersion` column on `ConversationParticipant`
- **IndexedDB migration**: Incrementing the version in `storage.js` triggers `onupgradeneeded` which creates the new object stores (`offlineQueue`, `expiryTrackers`). Existing data in the `messages` store is preserved.
- **No data migration needed**: All changes are additive. Existing data remains valid.
