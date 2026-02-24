# SignalR Hub Contracts

**Branch**: `001-secure-messaging` | **Date**: 2026-02-25

Hub URL: `/hubs/chat`

Authentication: Bearer token (same JWT as REST API) passed via `accessTokenFactory` in HubConnectionBuilder.

---

## Server → Client Methods

These methods are invoked by the server on connected clients.

### ReceiveMessage

A new encrypted message has arrived.

```csharp
Task ReceiveMessage(MessageEnvelope envelope);
```

**Payload**:
```json
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
```

---

### MessageDelivered

A sent message has been delivered to the recipient's device.

```csharp
Task MessageDelivered(Guid messageId, Guid recipientDeviceId, DateTimeOffset deliveredAt);
```

---

### MessageRead

A sent message has been read by the recipient.

```csharp
Task MessageRead(Guid messageId, Guid recipientDeviceId, DateTimeOffset readAt);
```

---

### IdentityKeyChanged

A contact's device identity key has changed (key change warning).

```csharp
Task IdentityKeyChanged(Guid userId, Guid deviceId, string newIdentityPublicKeyClassical, string newIdentityPublicKeyPostQuantum);
```

---

### PreKeyCountLow

The server notifies the client that its one-time pre-key supply is running low.

```csharp
Task PreKeyCountLow(Guid deviceId, int remainingCount);
```

---

### ParticipantAdded

A new participant has been added to a group conversation.

```csharp
Task ParticipantAdded(Guid conversationId, Guid userId, string displayName);
```

---

### ParticipantRemoved

A participant has been removed from a group conversation.

```csharp
Task ParticipantRemoved(Guid conversationId, Guid userId);
```

---

## Client → Server Methods

These methods are called by the client on the hub.

### SendMessage

Send an encrypted message to a specific recipient device.

```csharp
Task<SendMessageResult> SendMessage(SendMessageRequest request);
```

**Request**:
```json
{
  "conversationId": "guid",
  "recipientDeviceId": "guid",
  "ciphertext": "base64",
  "messageType": 0,
  "contentType": 0
}
```

**Response**:
```json
{
  "messageId": "guid",
  "serverTimestamp": "2026-02-25T10:30:00Z",
  "sequenceNumber": 42
}
```

The server stores the message if the recipient is offline, or forwards it in real-time via `ReceiveMessage` if online.

---

### AcknowledgeDelivery

Confirm that a message was received and decrypted.

```csharp
Task AcknowledgeDelivery(Guid messageId);
```

Triggers `MessageDelivered` on the sender's connected devices.

---

### AcknowledgeRead

Confirm that a message was read by the user.

```csharp
Task AcknowledgeRead(Guid messageId);
```

Triggers `MessageRead` on the sender's connected devices.

---

### TypingIndicator

Notify the conversation that the user is typing.

```csharp
Task TypingIndicator(Guid conversationId, bool isTyping);
```

Broadcasts to other participants in the conversation. Not persisted.

---

## Connection Lifecycle

1. **Connect**: Client connects with bearer token. Server associates the connection with the user's device.
2. **Register Device**: Client sends its deviceId on connection so the server can route messages to the correct device.
3. **Reconnect**: SignalR automatic reconnection handles transient disconnects. On reconnect, client fetches pending messages via REST API (`GET /api/messages/pending`).
4. **Disconnect**: Server marks the device as offline. Future messages are stored for offline delivery.

### RegisterDevice

Called immediately after hub connection is established.

```csharp
Task RegisterDevice(Guid deviceId);
```

Associates this SignalR connection with the specified device for message routing.
