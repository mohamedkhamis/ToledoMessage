# REST API Contracts

**Branch**: `001-secure-messaging` | **Date**: 2026-02-26

All endpoints served by ASP.NET Core (`src/ToledoMessage/Controllers/`).
Request/response bodies are JSON. Authentication uses JWT Bearer tokens.
Base URL: `https://<host>/api`

## Authentication

### POST /api/auth/register

Register a new user account.

**Request**:
```json
{
  "displayName": "string (3-32 chars, ^[a-zA-Z0-9_-]+$)",
  "password": "string (min 12 chars)"
}
```

**Response 200**:
```json
{
  "userId": "decimal",
  "displayName": "string",
  "token": "string (JWT)"
}
```

**Errors**: 400 (validation), 409 (display name taken)

---

### POST /api/auth/login

Authenticate and receive JWT token.

**Request**:
```json
{
  "displayName": "string",
  "password": "string"
}
```

**Response 200**:
```json
{
  "userId": "decimal",
  "displayName": "string",
  "token": "string (JWT)"
}
```

**Errors**: 401 (invalid credentials)

---

### POST /api/auth/refresh

Refresh an expired access token using a valid refresh token.

**Request**:
```json
{
  "accessToken": "string (expired JWT)",
  "refreshToken": "string"
}
```

**Response 200**:
```json
{
  "token": "string (new JWT)",
  "refreshToken": "string (rotated)"
}
```

**Errors**: 401 (invalid/expired refresh token)

---

### DELETE /api/auth/account

Initiate account deletion with 7-day grace period.

**Response 200**:
```json
{
  "deletionScheduledAt": "ISO 8601",
  "gracePeriodEndsAt": "ISO 8601"
}
```

**Notes**: Logging in during the grace period automatically cancels
the pending deletion and re-activates the account. After 7 days,
the account is permanently deactivated, all device keys are revoked,
and contacts see a key change warning.

**Errors**: 401 (not authenticated)

---

## Health (Public)

### GET /health

Server health check endpoint for uptime monitoring.

**Response 200**:
```json
{
  "status": "Healthy",
  "timestamp": "ISO 8601"
}
```

---

## Devices (Authorized)

### POST /api/devices

Register a new device with cryptographic key material.

**Request**:
```json
{
  "deviceName": "string",
  "identityPublicKeyClassical": "base64 (32 bytes, X25519)",
  "identityPublicKeyPostQuantum": "base64 (1184 bytes, ML-KEM-768)",
  "signedPreKeyPublic": "base64 (32 bytes, X25519)",
  "signedPreKeySignature": "base64 (64 bytes, Ed25519)",
  "signedPreKeyId": "int",
  "kyberPreKeyPublic": "base64 (1184 bytes, ML-KEM-768)",
  "kyberPreKeySignature": "base64 (ML-DSA signature)",
  "oneTimePreKeys": [
    { "keyId": "int", "publicKey": "base64 (32 bytes)" }
  ]
}
```

**Response 200**:
```json
{
  "deviceId": "decimal",
  "deviceName": "string"
}
```

**Errors**: 400 (validation), 409 (max 10 devices reached)

---

### GET /api/devices

List all active devices for the authenticated user.

**Response 200**:
```json
[
  {
    "deviceId": "decimal",
    "deviceName": "string",
    "createdAt": "ISO 8601",
    "lastSeenAt": "ISO 8601",
    "isActive": true
  }
]
```

---

### DELETE /api/devices/{deviceId}

Deactivate (revoke) a device.

**Response 204**: No content

**Errors**: 404 (device not found or not owned)

---

### GET /api/devices/{deviceId}/prekeys/count

Get remaining unused one-time pre-key count for a device.

**Response 200**:
```json
{ "count": "int" }
```

---

### POST /api/devices/{deviceId}/prekeys

Upload additional one-time pre-keys for replenishment.

**Request**:
```json
[
  { "keyId": "int", "publicKey": "base64 (32 bytes)" }
]
```

**Response 200**: OK

---

## Users (Authorized)

### GET /api/users/search?q={query}

Search for users by display name (case-insensitive partial match).

**Query params**: `q` (string, required)

**Response 200**:
```json
[
  {
    "userId": "decimal",
    "displayName": "string"
  }
]
```

**Rate limit**: 10 requests/minute

---

### GET /api/users/{userId}/prekey-bundle?deviceId={deviceId}

Fetch a device's pre-key bundle for X3DH session establishment.
Consumes one one-time pre-key (if available).

**Response 200**:
```json
{
  "deviceId": "decimal",
  "identityPublicKeyClassical": "base64",
  "identityPublicKeyPostQuantum": "base64",
  "signedPreKeyPublic": "base64",
  "signedPreKeySignature": "base64",
  "signedPreKeyId": "int",
  "kyberPreKeyPublic": "base64",
  "kyberPreKeySignature": "base64",
  "oneTimePreKeyId": "int? (null if exhausted)",
  "oneTimePreKeyPublic": "base64? (null if exhausted)"
}
```

**Errors**: 404 (user/device not found)

---

### GET /api/users/{userId}/devices

List all active devices for a user (for fan-out encryption).

**Response 200**:
```json
[
  {
    "deviceId": "decimal",
    "deviceName": "string"
  }
]
```

---

## Messages (Authorized)

### POST /api/messages

Store an encrypted message (REST fallback when SignalR unavailable).

**Request**:
```json
{
  "conversationId": "decimal",
  "senderDeviceId": "decimal",
  "recipientDeviceId": "decimal",
  "ciphertext": "base64",
  "messageType": "int (0=PreKey, 1=Normal)",
  "contentType": "int (0=Text)"
}
```

**Response 200**:
```json
{
  "messageId": "decimal",
  "sequenceNumber": "long",
  "serverTimestamp": "ISO 8601"
}
```

**Rate limit**: 60 messages/minute

---

### GET /api/messages/pending?deviceId={deviceId}

Retrieve undelivered messages for a device (offline catch-up).

**Response 200**:
```json
[
  {
    "messageId": "decimal",
    "conversationId": "decimal",
    "senderDeviceId": "decimal",
    "ciphertext": "base64",
    "messageType": "int",
    "contentType": "int",
    "sequenceNumber": "long",
    "serverTimestamp": "ISO 8601"
  }
]
```

---

### POST /api/messages/{messageId}/acknowledge

Mark a message as delivered.

**Response 204**: No content

---

## Conversations (Authorized)

### GET /api/conversations

List all conversations the authenticated user participates in.

**Response 200**:
```json
[
  {
    "conversationId": "decimal",
    "type": "int (0=OneToOne, 1=Group)",
    "groupName": "string?",
    "lastMessageTimestamp": "ISO 8601?",
    "disappearingTimerSeconds": "int?",
    "participantCount": "int"
  }
]
```

---

### POST /api/conversations

Create a one-to-one conversation (or return existing).

**Request**:
```json
{
  "otherUserId": "decimal"
}
```

**Response 200**:
```json
{
  "conversationId": "decimal",
  "type": 0,
  "createdAt": "ISO 8601"
}
```

---

### POST /api/conversations/group

Create a group conversation.

**Request**:
```json
{
  "groupName": "string",
  "participantUserIds": ["decimal"]
}
```

**Constraints**: 2-100 participants (including creator)

**Response 200**:
```json
{
  "conversationId": "decimal",
  "type": 1,
  "groupName": "string",
  "createdAt": "ISO 8601"
}
```

---

### GET /api/conversations/{conversationId}

Get conversation details.

**Response 200**:
```json
{
  "conversationId": "decimal",
  "type": "int",
  "groupName": "string?",
  "createdAt": "ISO 8601",
  "disappearingTimerSeconds": "int?",
  "participants": [
    {
      "userId": "decimal",
      "displayName": "string",
      "role": "int (0=Member, 1=Admin)",
      "joinedAt": "ISO 8601"
    }
  ]
}
```

---

### GET /api/conversations/{conversationId}/participants

List conversation participants.

**Response 200**:
```json
[
  {
    "userId": "decimal",
    "displayName": "string",
    "role": "int",
    "joinedAt": "ISO 8601"
  }
]
```

---

### POST /api/conversations/{conversationId}/participants

Add a participant to a group conversation (Admin only).

**Request**:
```json
{
  "userId": "decimal"
}
```

**Response 200**: OK

**Errors**: 403 (not admin), 400 (max participants, not group)

---

### DELETE /api/conversations/{conversationId}/participants/{targetUserId}

Remove a participant from a group (Admin or self-leave).

**Response 204**: No content

**Errors**: 403 (not admin and not self)

---

### PUT /api/conversations/{conversationId}/timer

Set or disable the disappearing message timer.

**Request**:
```json
{
  "timerSeconds": "int? (null to disable)"
}
```

**Response 204**: No content
