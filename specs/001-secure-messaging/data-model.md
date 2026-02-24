# Data Model: Hybrid Post-Quantum Secure Messaging

**Branch**: `001-secure-messaging` | **Date**: 2026-02-25

## Overview

The data model is split into two domains:
1. **Server-side** (SQL Server via EF Core) — stores only public keys, encrypted blobs, and metadata
2. **Client-side** (Browser IndexedDB) — stores private keys, session state, ratchet keys, and plaintext messages

The server NEVER stores private keys, session keys, ratchet state, or plaintext content (Constitution Principle I).

---

## Server-Side Entities (EF Core / SQL Server)

### User

| Field | Type | Constraints |
|-------|------|-------------|
| Id | Guid | PK, generated |
| DisplayName | string(50) | Unique, required, indexed |
| PasswordHash | string(256) | Required (hashed with ASP.NET Core Identity) |
| CreatedAt | DateTimeOffset | Required, default UTC now |
| IsActive | bool | Required, default true |

**Relationships**: Has many Devices. Participates in many Conversations (via ConversationParticipant).

**State transitions**: Active → Deactivated (soft delete; IsActive = false).

---

### Device

| Field | Type | Constraints |
|-------|------|-------------|
| Id | Guid | PK, generated |
| UserId | Guid | FK → User, required, indexed |
| DeviceName | string(100) | Required (e.g., "Chrome on Windows") |
| IdentityPublicKeyClassical | byte[] | Required (Ed25519 public key, 32 bytes) |
| IdentityPublicKeyPostQuantum | byte[] | Required (ML-DSA-65 public key, ~1952 bytes) |
| SignedPreKeyPublic | byte[] | Required (X25519 public key, 32 bytes) |
| SignedPreKeySignature | byte[] | Required (hybrid signature over signed pre-key) |
| SignedPreKeyId | int | Required |
| KyberPreKeyPublic | byte[] | Required (ML-KEM-768 public key, 1184 bytes) |
| KyberPreKeySignature | byte[] | Required (hybrid signature over Kyber pre-key) |
| CreatedAt | DateTimeOffset | Required |
| LastSeenAt | DateTimeOffset | Required |
| IsActive | bool | Required, default true |

**Relationships**: Belongs to User. Has many OneTimePreKeys. Has many sent/received EncryptedMessages.

**Validation**: A User may have at most 10 active Devices.

**State transitions**: Active → Revoked (IsActive = false; triggered by user unlinking device).

---

### OneTimePreKey

| Field | Type | Constraints |
|-------|------|-------------|
| Id | Guid | PK, generated |
| DeviceId | Guid | FK → Device, required, indexed |
| KeyId | int | Required (unique per device) |
| PublicKey | byte[] | Required (X25519 public key, 32 bytes) |
| IsUsed | bool | Required, default false |

**Lifecycle**: Created in batches during registration / replenishment. Marked IsUsed = true when consumed by a session initiator. Used keys are never reused. Replenishment triggers when count of unused keys falls below threshold (e.g., 10).

**Unique constraint**: (DeviceId, KeyId) is unique.

---

### Conversation

| Field | Type | Constraints |
|-------|------|-------------|
| Id | Guid | PK, generated |
| Type | int (enum) | Required (0 = OneToOne, 1 = Group) |
| CreatedAt | DateTimeOffset | Required |
| DisappearingTimerSeconds | int? | Nullable (null = no auto-delete) |

**Relationships**: Has many ConversationParticipants. Has many EncryptedMessages.

---

### ConversationParticipant

| Field | Type | Constraints |
|-------|------|-------------|
| ConversationId | Guid | FK → Conversation, PK (composite) |
| UserId | Guid | FK → User, PK (composite) |
| JoinedAt | DateTimeOffset | Required |
| Role | int (enum) | Required (0 = Member, 1 = Admin) |

**Validation**: OneToOne conversations have exactly 2 participants. Group conversations have 2–100 participants. At least one Admin per group.

---

### EncryptedMessage

| Field | Type | Constraints |
|-------|------|-------------|
| Id | Guid | PK, generated |
| ConversationId | Guid | FK → Conversation, required, indexed |
| SenderDeviceId | Guid | FK → Device, required |
| RecipientDeviceId | Guid | FK → Device, required, indexed |
| Ciphertext | byte[] | Required (encrypted message envelope) |
| MessageType | int (enum) | Required (0 = PreKeyMessage, 1 = NormalMessage) |
| ContentType | int (enum) | Required (0 = Text; extensible for future media types) |
| SequenceNumber | long | Required |
| ServerTimestamp | DateTimeOffset | Required, server-assigned |
| IsDelivered | bool | Required, default false |
| DeliveredAt | DateTimeOffset? | Nullable |

**Lifecycle**: Created when sender submits encrypted message. Stored until delivered (IsDelivered = true, DeliveredAt set). Deleted from server after delivery confirmation (or after configurable retention period for offline recipients).

**Index**: (RecipientDeviceId, IsDelivered) for efficient offline message retrieval.

---

## Client-Side Entities (Browser IndexedDB)

These entities are stored exclusively in the user's browser. The server never sees this data.

### LocalIdentity

| Field | Type | Notes |
|-------|------|-------|
| DeviceId | Guid | Matches server Device.Id |
| IdentityPrivateKeyClassical | byte[] | Ed25519 private key (64 bytes) |
| IdentityPrivateKeyPostQuantum | byte[] | ML-DSA-65 private key |
| SignedPreKeyPrivate | byte[] | X25519 private key (32 bytes) |
| KyberPreKeyPrivate | byte[] | ML-KEM-768 private key |
| OneTimePreKeyPrivates | Map&lt;int, byte[]&gt; | KeyId → X25519 private key |

**Security**: Must be encrypted at rest in IndexedDB using a key derived from the user's password.

---

### LocalSession

| Field | Type | Notes |
|-------|------|-------|
| SessionId | string | Composite: {localDeviceId}:{remoteDeviceId} |
| RemoteDeviceId | Guid | The other party's device |
| RootKey | byte[] | Current root key (32 bytes) |
| SendChainKey | byte[] | Current sending chain key |
| SendChainIndex | int | Next send message number |
| ReceiveChainKey | byte[] | Current receiving chain key |
| ReceiveChainIndex | int | Next expected receive message number |
| RemoteRatchetPublicKey | byte[] | Remote party's current DH ratchet public key |
| LocalRatchetPrivateKey | byte[] | Our current DH ratchet private key |
| SkippedMessageKeys | Map&lt;string, byte[]&gt; | "{chainKey}:{index}" → message key (for out-of-order messages) |

---

### LocalMessage

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | Local message ID |
| ConversationId | Guid | Matches server Conversation.Id |
| SenderDisplayName | string | Display name of sender |
| PlaintextContent | string | Decrypted message text |
| Timestamp | DateTimeOffset | Server timestamp or local timestamp |
| Status | int (enum) | 0 = Sending, 1 = Sent, 2 = Delivered, 3 = Read |
| IsOutgoing | bool | true if sent by local user |
| ExpiresAt | DateTimeOffset? | Nullable; set if disappearing timer active |

---

### LocalConversation

| Field | Type | Notes |
|-------|------|-------|
| ConversationId | Guid | Matches server Conversation.Id |
| ParticipantDisplayNames | string[] | Display names of participants |
| IsVerified | bool | Whether safety number has been verified |
| LastMessageAt | DateTimeOffset? | For sorting conversation list |
| UnreadCount | int | Number of unread messages |

---

## Entity Relationship Diagram (Text)

```
SERVER-SIDE:
  User 1──* Device 1──* OneTimePreKey
    │                │
    │                ├──* EncryptedMessage (as sender)
    │                └──* EncryptedMessage (as recipient)
    │
    └──* ConversationParticipant *──1 Conversation 1──* EncryptedMessage

CLIENT-SIDE (per device, in IndexedDB):
  LocalIdentity 1──1 (matches Device on server)
  LocalSession *──1 (per remote device)
  LocalConversation 1──* LocalMessage
```
