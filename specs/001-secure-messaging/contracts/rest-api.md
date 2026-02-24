# REST API Contracts

**Branch**: `001-secure-messaging` | **Date**: 2026-02-25

All endpoints are served by the ASP.NET Core server project. Request/response bodies are JSON. Authentication uses ASP.NET Core Identity bearer tokens (JWT).

---

## Authentication

### POST /api/auth/register

Create a new user account.

**Request**:
```json
{
  "displayName": "alice",
  "password": "strongPassword123!"
}
```

**Response 201**:
```json
{
  "userId": "guid",
  "displayName": "alice",
  "token": "jwt-bearer-token"
}
```

**Errors**: 400 (validation), 409 (display name taken)

---

### POST /api/auth/login

Authenticate an existing user.

**Request**:
```json
{
  "displayName": "alice",
  "password": "strongPassword123!"
}
```

**Response 200**:
```json
{
  "userId": "guid",
  "displayName": "alice",
  "token": "jwt-bearer-token"
}
```

**Errors**: 401 (invalid credentials)

---

## Device Management

### POST /api/devices

Register a new device for the authenticated user. Publishes the device's pre-key bundle.

**Auth**: Required

**Request**:
```json
{
  "deviceName": "Chrome on Windows",
  "identityPublicKeyClassical": "base64(Ed25519 public key)",
  "identityPublicKeyPostQuantum": "base64(ML-DSA-65 public key)",
  "signedPreKeyPublic": "base64(X25519 public key)",
  "signedPreKeySignature": "base64(hybrid signature)",
  "signedPreKeyId": 1,
  "kyberPreKeyPublic": "base64(ML-KEM-768 public key)",
  "kyberPreKeySignature": "base64(hybrid signature)",
  "oneTimePreKeys": [
    { "keyId": 0, "publicKey": "base64(X25519 public key)" },
    { "keyId": 1, "publicKey": "base64(X25519 public key)" }
  ]
}
```

**Response 201**:
```json
{
  "deviceId": "guid"
}
```

**Errors**: 400 (validation), 403 (max 10 devices reached)

---

### DELETE /api/devices/{deviceId}

Revoke/unlink a device.

**Auth**: Required (must own device)

**Response**: 204 No Content

---

## Pre-Key Bundles

### GET /api/users/{userId}/prekey-bundle?deviceId={deviceId}

Fetch a user's pre-key bundle for session establishment. Consumes one one-time pre-key (if available).

**Auth**: Required

**Response 200**:
```json
{
  "deviceId": "guid",
  "identityPublicKeyClassical": "base64",
  "identityPublicKeyPostQuantum": "base64",
  "signedPreKeyPublic": "base64",
  "signedPreKeySignature": "base64",
  "signedPreKeyId": 1,
  "kyberPreKeyPublic": "base64",
  "kyberPreKeySignature": "base64",
  "oneTimePreKey": {
    "keyId": 42,
    "publicKey": "base64"
  }
}
```

**Note**: `oneTimePreKey` is null if all one-time pre-keys are exhausted (fallback to signed pre-key only).

**Errors**: 404 (user/device not found)

---

### POST /api/devices/{deviceId}/prekeys

Upload additional one-time pre-keys (replenishment).

**Auth**: Required (must own device)

**Request**:
```json
{
  "oneTimePreKeys": [
    { "keyId": 50, "publicKey": "base64" },
    { "keyId": 51, "publicKey": "base64" }
  ]
}
```

**Response**: 204 No Content

---

### GET /api/devices/{deviceId}/prekeys/count

Check remaining unused one-time pre-key count.

**Auth**: Required (must own device)

**Response 200**:
```json
{
  "count": 23
}
```

---

## User Discovery

### GET /api/users/search?q={displayName}

Search for users by display name.

**Auth**: Required

**Response 200**:
```json
{
  "users": [
    {
      "userId": "guid",
      "displayName": "bob",
      "deviceCount": 2
    }
  ]
}
```

**Rate limit**: 10 requests per minute per user.

---

## Messages

### POST /api/messages

Submit an encrypted message for delivery. The server stores it for the recipient device.

**Auth**: Required

**Request**:
```json
{
  "conversationId": "guid",
  "recipientDeviceId": "guid",
  "ciphertext": "base64(encrypted envelope)",
  "messageType": 0,
  "contentType": 0
}
```

`messageType`: 0 = PreKeyMessage (first message in new session), 1 = NormalMessage
`contentType`: 0 = Text (extensible for future media types)

**Response 201**:
```json
{
  "messageId": "guid",
  "serverTimestamp": "2026-02-25T10:30:00Z",
  "sequenceNumber": 42
}
```

**Rate limit**: 60 messages per minute per user.

---

### GET /api/messages/pending?deviceId={deviceId}

Retrieve all undelivered messages for a device (offline message pickup).

**Auth**: Required (must own device)

**Response 200**:
```json
{
  "messages": [
    {
      "messageId": "guid",
      "conversationId": "guid",
      "senderDeviceId": "guid",
      "ciphertext": "base64",
      "messageType": 0,
      "contentType": 0,
      "sequenceNumber": 42,
      "serverTimestamp": "2026-02-25T10:30:00Z"
    }
  ]
}
```

---

### POST /api/messages/acknowledge

Acknowledge message delivery (allows server to delete stored messages).

**Auth**: Required

**Request**:
```json
{
  "messageIds": ["guid", "guid"]
}
```

**Response**: 204 No Content

---

## Conversations

### POST /api/conversations

Create a new conversation.

**Auth**: Required

**Request**:
```json
{
  "type": 0,
  "participantUserIds": ["guid", "guid"],
  "disappearingTimerSeconds": null
}
```

`type`: 0 = OneToOne, 1 = Group

**Response 201**:
```json
{
  "conversationId": "guid"
}
```

---

### PUT /api/conversations/{conversationId}/timer

Update disappearing message timer.

**Auth**: Required (must be participant)

**Request**:
```json
{
  "disappearingTimerSeconds": 86400
}
```

**Response**: 204 No Content

---

### POST /api/conversations/{conversationId}/participants

Add participant to a group conversation.

**Auth**: Required (must be Admin)

**Request**:
```json
{
  "userId": "guid"
}
```

**Response**: 204 No Content

---

### DELETE /api/conversations/{conversationId}/participants/{userId}

Remove participant from a group conversation.

**Auth**: Required (must be Admin, or self-removal)

**Response**: 204 No Content
