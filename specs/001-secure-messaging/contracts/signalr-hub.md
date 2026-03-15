# SignalR Hub Contracts

**Branch**: `001-secure-messaging` | **Date**: 2026-02-26

Hub URL: `/hubs/chat`
Authentication: JWT Bearer token via query string (`?access_token=<jwt>`)
Source: `src/ToledoVault/Hubs/ChatHub.cs`

## Connection Lifecycle

1. Client connects with JWT token in query string
2. Client calls `RegisterDevice(deviceId)` to join device/user groups
3. Server adds connection to groups: `device_{deviceId}`, `user_{userId}`
4. Messages are routed to `device_{recipientDeviceId}` groups
5. On disconnect, connection is automatically removed from groups

## Client → Server (Invocations)

### RegisterDevice

Register the connected client with a specific device ID. Adds the
connection to device-specific and user-specific SignalR groups.

```csharp
Task RegisterDevice(decimal deviceId)
```

**Groups joined**: `device_{deviceId}`, `user_{userId}`

Must be called immediately after connection establishment.

---

### SendMessage

Send an encrypted message to a recipient device via real-time channel.
The server stores the message, assigns a sequence number, and relays
to the recipient if online.

```csharp
Task<SendMessageResult> SendMessage(SendMessageRequest request)
```

**Request** (`SendMessageRequest`):
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

**Response** (`SendMessageResult`):
```json
{
  "messageId": "decimal",
  "sequenceNumber": "long",
  "serverTimestamp": "ISO 8601"
}
```

**Rate limit**: 60 messages/minute per user

---

### AcknowledgeDelivery

Acknowledge that a message has been received and decrypted by the
recipient device. Updates IsDelivered and DeliveredAt on the server.

```csharp
Task AcknowledgeDelivery(decimal messageId)
```

**Side effect**: Broadcasts `MessageDelivered` to sender's device group.

---

### AcknowledgeRead

Acknowledge that a message has been read (displayed to user).

```csharp
Task AcknowledgeRead(decimal messageId)
```

**Side effect**: Broadcasts `MessageRead` to sender's device group.

---

### TypingIndicator

Broadcast a typing indicator to all other participants in a
conversation.

```csharp
Task TypingIndicator(decimal conversationId)
```

**Side effect**: Broadcasts `UserTyping` to all other participants'
user groups (excludes sender).

---

## Server → Client (Broadcasts)

### ReceiveMessage

Sent to the recipient device group when a new encrypted message
arrives.

```csharp
Task ReceiveMessage(MessageEnvelope envelope)
```

**Payload** (`MessageEnvelope`):
```json
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
```

**Target group**: `device_{recipientDeviceId}`

---

### MessageDelivered

Notifies the sender that their message was received by the recipient.

```csharp
Task MessageDelivered(decimal messageId, DateTimeOffset deliveredAt)
```

**Target group**: `device_{senderDeviceId}`

---

### MessageRead

Notifies the sender that their message was read by the recipient.

```csharp
Task MessageRead(decimal messageId, DateTimeOffset readAt)
```

**Target group**: `device_{senderDeviceId}`

---

### UserTyping

Notifies conversation participants that a user is currently typing.

```csharp
Task UserTyping(decimal conversationId, decimal userId)
```

**Target group**: `user_{participantUserId}` (all participants
except the typing user)

### ReactionToggled — FR-026

Notifies conversation participants that a reaction was added or removed.

```csharp
Task ReactionToggled(decimal messageId, decimal conversationId, decimal userId, string emoji, bool added)
```

**Target group**: `user_{participantUserId}` (all participants in the conversation)

---

### MessageForwarded — FR-026

Notifies the target conversation that a forwarded message has arrived.
Uses the same `ReceiveMessage` event with the `IsForwarded` flag in the envelope.

```csharp
// Uses standard ReceiveMessage with extended MessageEnvelope:
// { ..., "isForwarded": true, "originalSenderName": "string?" }
```

---

## Message Flow Diagram

```text
Sender Client                Server                Recipient Client
     │                          │                          │
     │──RegisterDevice(did)────>│                          │
     │                          │<──RegisterDevice(did)────│
     │                          │                          │
     │──SendMessage(req)───────>│                          │
     │                          │──[store message]         │
     │                          │──[assign seq#]           │
     │<──SendMessageResult──────│                          │
     │                          │──ReceiveMessage(env)────>│
     │                          │                          │──[decrypt]
     │                          │<──AcknowledgeDelivery────│
     │<──MessageDelivered───────│                          │
     │                          │                          │──[user reads]
     │                          │<──AcknowledgeRead────────│
     │<──MessageRead────────────│                          │
     │                          │                          │
```
