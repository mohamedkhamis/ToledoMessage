# Data Model: Hybrid Post-Quantum Secure Messaging

**Phase**: 1 — Design & Contracts
**Date**: 2026-02-26
**Source**: EF Core Code First entities in `src/ToledoMessage/Models/`

## Entity Relationship Diagram (Text)

```text
┌──────────────────┐       ┌──────────────────────┐
│      User        │       │       Device          │
│──────────────────│       │──────────────────────│
│ Id (PK, decimal) │1────*│ Id (PK, decimal)     │
│ DisplayName      │       │ UserId (FK → User)   │
│ PasswordHash     │       │ DeviceName            │
│ CreatedAt        │       │ IdentityPubClassical  │
│ IsActive         │       │ IdentityPubPQ         │
│ DeletionReqAt    │       │                       │
│                  │       │ SignedPreKeyPublic    │
│                  │       │ SignedPreKeySignature │
│                  │       │ SignedPreKeyId        │
│                  │       │ KyberPreKeyPublic     │
│                  │       │ KyberPreKeySignature  │
│                  │       │ CreatedAt             │
│                  │       │ LastSeenAt            │
│                  │       │ IsActive              │
└──────┬───────────┘       └────────┬─────────────┘
       │                            │
       │                            │1
       │                            │
       │                    ┌───────┴──────────────┐
       │                    │    OneTimePreKey      │
       │                    │──────────────────────│
       │                    │ Id (PK, decimal)     │
       │                    │ DeviceId (FK→Device) │
       │                    │ KeyId (int)          │
       │                    │ PublicKey (byte[])   │
       │                    │ IsUsed (bool)        │
       │                    └──────────────────────┘
       │
       │*
┌──────┴───────────────────┐
│ ConversationParticipant  │
│──────────────────────────│
│ ConversationId (FK, PK)  │
│ UserId (FK → User, PK)  │
│ JoinedAt                 │
│ Role (ParticipantRole)   │
└──────┬───────────────────┘
       │*
       │
┌──────┴───────────────────┐       ┌──────────────────────┐
│     Conversation         │       │   EncryptedMessage    │
│──────────────────────────│       │──────────────────────│
│ Id (PK, decimal)         │1────*│ Id (PK, decimal)     │
│ Type (ConversationType)  │       │ ConversationId (FK)  │
│ GroupName (string?)      │       │ SenderDeviceId (FK)  │
│ CreatedAt                │       │ RecipientDeviceId(FK)│
│ DisappearingTimerSeconds │       │ Ciphertext (byte[])  │
│                          │       │ MessageType          │
│                          │       │ ContentType          │
│                          │       │ SequenceNumber       │
│                          │       │ ServerTimestamp       │
│                          │       │ IsDelivered          │
│                          │       │ DeliveredAt          │
└──────────────────────────┘       └──────────────────────┘
```

## Entities

### User

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | decimal | PK, auto-generated | Snowflake-style via DecimalTools |
| DisplayName | string | Required, 3-32 chars, `^[a-zA-Z0-9_-]{3,32}$`, unique (case-insensitive) | Primary discovery identifier |
| PasswordHash | string | Required | Hashed via ASP.NET Core `IPasswordHasher<User>` |
| CreatedAt | DateTimeOffset | Required, default: now | Registration timestamp |
| IsActive | bool | Required, default: true | Soft-delete flag |
| DeletionRequestedAt | DateTimeOffset? | Optional, nullable | Set when user initiates account deletion |

**Relationships**: Has many `Devices`, has many `ConversationParticipants`

**Validation rules**:
- DisplayName: regex `^[a-zA-Z0-9_-]{3,32}$`, case-insensitive uniqueness
- Password: minimum 12 characters (per spec), hashed before storage
- IsActive=false prevents login and message delivery

**State transitions**: Active → PendingDeletion (7-day grace period; login re-activates) → Deactivated (permanent, keys revoked, no recovery per constitution)

---

### Device

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | decimal | PK, auto-generated | |
| UserId | decimal | FK → User, required | Owning user |
| DeviceName | string | Required | Human-readable label |
| IdentityPublicKeyClassical | byte[] | Required, 32 bytes | X25519 public key |
| IdentityPublicKeyPostQuantum | byte[] | Required, 1184 bytes | ML-KEM-768 public key |
| SignedPreKeyPublic | byte[] | Required, 32 bytes | X25519 signed pre-key |
| SignedPreKeySignature | byte[] | Required, 64 bytes | Ed25519 signature |
| SignedPreKeyId | int | Required | Key rotation identifier |
| KyberPreKeyPublic | byte[] | Required, 1184 bytes | ML-KEM signed pre-key |
| KyberPreKeySignature | byte[] | Required | ML-DSA signature |
| CreatedAt | DateTimeOffset | Required | |
| LastSeenAt | DateTimeOffset | Required | Updated on connection |
| IsActive | bool | Required, default: true | |

**Relationships**: Belongs to `User`, has many `OneTimePreKeys`,
referenced by `EncryptedMessage` (as sender and recipient)

**Validation rules**:
- Maximum 10 active devices per user (ProtocolConstants.MaxDevicesPerUser)
- Key sizes validated against ProtocolConstants
- IsActive=false revokes the device from message delivery

---

### OneTimePreKey

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | decimal | PK, auto-generated | |
| DeviceId | decimal | FK → Device, required | |
| KeyId | int | Required | Client-assigned identifier |
| PublicKey | byte[] | Required, 32 bytes | X25519 one-time pre-key |
| IsUsed | bool | Required, default: false | Consumed on session init |

**Relationships**: Belongs to `Device`

**Lifecycle**: Created in batch (100), consumed once during X3DH
session establishment (IsUsed → true), replenished when count drops
below 10 (threshold from ProtocolConstants).

---

### Conversation

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | decimal | PK, auto-generated | |
| Type | ConversationType | Required | OneToOne (0) or Group (1) |
| GroupName | string? | Optional | Only for Group conversations |
| CreatedAt | DateTimeOffset | Required | |
| DisappearingTimerSeconds | int? | Optional, nullable | null = messages persist |

**Relationships**: Has many `ConversationParticipants`, has many
`EncryptedMessages`

**Validation rules**:
- OneToOne: exactly 2 participants, no GroupName
- Group: 2-100 participants (ProtocolConstants.MaxGroupParticipants),
  GroupName required
- DisappearingTimerSeconds: positive integer or null

---

### ConversationParticipant (Join Table)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| ConversationId | decimal | PK (composite), FK → Conversation | |
| UserId | decimal | PK (composite), FK → User | |
| JoinedAt | DateTimeOffset | Required | |
| Role | ParticipantRole | Required | Member (0) or Admin (1) |

**Relationships**: Belongs to `Conversation`, belongs to `User`

**Rules**:
- Group creator gets Admin role
- Only Admins can add/remove participants
- Members can remove themselves (leave)
- Membership changes trigger Sender Key rotation (application-level)

---

### EncryptedMessage

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | decimal | PK, auto-generated | |
| ConversationId | decimal | FK → Conversation, required | |
| SenderDeviceId | decimal | FK → Device, required | Sending device |
| RecipientDeviceId | decimal | FK → Device, required | Target device |
| Ciphertext | byte[] | Required, max ~66 KB (64 KB plaintext + overhead) | Encrypted payload (AES-256-GCM) |
| MessageType | MessageType | Required | PreKeyMessage (0) or NormalMessage (1) |
| ContentType | ContentType | Required | Text (0); extensible |
| SequenceNumber | long | Required | Per-conversation auto-increment |
| ServerTimestamp | DateTimeOffset | Required | Server-assigned ordering |
| IsDelivered | bool | Required, default: false | |
| DeliveredAt | DateTimeOffset? | Optional | Set on acknowledgment |

**Relationships**: Belongs to `Conversation`, references sender `Device`
and recipient `Device`

**Lifecycle**:
1. Created → IsDelivered=false
2. Delivered (ack received) → IsDelivered=true, DeliveredAt set
3. Expired (>90 days undelivered) → auto-purged by
   MessageCleanupHostedService
4. Disappearing (timer set) → auto-purged after timer expires

**Note**: Each message is stored per recipient device (fan-out). For a
message to a user with 3 devices, 3 EncryptedMessage rows are created,
each with different RecipientDeviceId and independently encrypted
ciphertext.

### RefreshToken

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | decimal | PK, auto-generated | |
| UserId | decimal | FK → User, required | Owning user |
| Token | string | Required, unique | Opaque refresh token value |
| DeviceId | decimal | FK → Device, required | Token is per-device |
| ExpiresAt | DateTimeOffset | Required | Token expiration |
| CreatedAt | DateTimeOffset | Required | Issuance timestamp |
| IsRevoked | bool | Required, default: false | Revoked on rotation or logout |

**Relationships**: Belongs to `User`, belongs to `Device`

**Lifecycle**:
1. Created on login/registration (one per device)
2. Rotated on each refresh (old token revoked, new token issued)
3. Revoked on logout or device removal

---

## Enums

| Enum | Values | Usage |
|------|--------|-------|
| MessageType | PreKeyMessage (0), NormalMessage (1) | First vs. subsequent messages in session |
| ContentType | Text (0) | MVP text; extensible for Image, Audio, File |
| ConversationType | OneToOne (0), Group (1) | Conversation type discriminator |
| DeliveryStatus | Sending (0), Sent (1), Delivered (2), Read (3) | Client-side UI state |
| ParticipantRole | Member (0), Admin (1) | Group permission level |
| UserStatus | Active (0), PendingDeletion (1), Deactivated (2) | Account lifecycle state |

## Client-Side Data (IndexedDB)

Data stored only on the client device via `LocalStorageService`,
never sent to the server:

| Store | Content | Purpose |
|-------|---------|---------|
| identity_keys | Private keys (X25519 + ML-KEM) | User identity |
| signed_pre_key | Signed pre-key private key | Session establishment |
| one_time_pre_keys | OTP private keys (keyed by KeyId) | Session establishment |
| sessions | RatchetState per remote device | Double Ratchet state |
| messages | Decrypted message content | Chat history display |
| preferences | User settings (theme, notification permission) | UI preferences |

## Protocol Constants

| Constant | Value | Notes |
|----------|-------|-------|
| X25519 Public Key | 32 bytes | |
| X25519 Private Key | 32 bytes | |
| Ed25519 Public Key | 32 bytes | |
| Ed25519 Signature | 64 bytes | |
| ML-KEM-768 Public Key | 1184 bytes | |
| ML-KEM-768 Ciphertext | 1088 bytes | |
| ML-KEM-768 Shared Secret | 32 bytes | |
| ML-DSA-65 Public Key | 1952 bytes | |
| AES-256 Key | 32 bytes | |
| AES-GCM Nonce | 12 bytes | |
| AES-GCM Tag | 16 bytes | |
| OTP Batch Size | 100 keys | |
| OTP Low Threshold | 10 keys | |
| Max Devices Per User | 10 | |
| Max Group Participants | 100 | |
| Message Rate Limit | 60/minute | |
| Search Rate Limit | 10/minute | |
| Max Message Size | 65,536 bytes (64 KB) | Plaintext limit before encryption |
| Account Deletion Grace Period | 7 days | Login re-activates during grace period |
| HKDF Info - Root Key | `ToledoMessage_RootKey` | Domain separation |
| HKDF Info - Chain Key | `ToledoMessage_ChainKey` | Domain separation |
| HKDF Info - Message Key | `ToledoMessage_MessageKey` | Domain separation |
