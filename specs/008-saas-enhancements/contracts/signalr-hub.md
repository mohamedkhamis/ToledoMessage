# Contract: SignalR Hub (ChatHub) — Changes for v2.0

**Feature**: `008-saas-enhancements`
**Date**: 2026-03-09

## Purpose

Documents all changes to the SignalR hub contract (server → client and client → server messages). Existing methods are preserved; changes are additive or behavioral.

---

## Client → Server (Hub Method Invocations)

### Modified Methods

#### `SendMessage(SendMessageRequest request) → SendMessageResult`

**Behavioral change**: Now rate-limited at 60 invocations per user per minute.

- **When rate-limited**: Server throws `HubException` with message containing "Rate limit exceeded".
- **Client handling**: Catch the exception, display inline warning below message input with cooldown countdown.
- **No signature change**: Same parameters and return type.

#### `TypingIndicator(long conversationId) → void`

**Behavioral change**: Now rate-limited at 10 invocations per user per minute. Display name and participant list are now cached server-side (no behavioral change visible to client).

- **When rate-limited**: Server silently drops the indicator (no exception thrown, to avoid disrupting typing flow).
- **No signature change**: Same parameters and return type.

#### `AdvanceReadPointer(long conversationId, long upToSequenceNumber) → void`

**Behavioral change**: Server now clamps `upToSequenceNumber` to `MAX(SequenceNumber)` in the conversation.

- **Effect**: If client sends `upToSequenceNumber: 999999` but max sequence is `42`, server treats it as `42`.
- **No error thrown**: Clamping is transparent to the client.
- **No signature change**: Same parameters and return type.

#### `DeleteForEveryone(long messageId) → void`

**Behavioral change**: Server now verifies the caller is a participant in the message's conversation before allowing deletion.

- **When unauthorized**: Server throws `HubException` with message "You are not a participant in this conversation."
- **Previous behavior**: Only checked if caller was the message sender (still checked).
- **No signature change**: Same parameters and return type.

### Unchanged Methods

- `RegisterDevice(long deviceId)` — No changes
- `AcknowledgeDelivery(long messageId)` — No changes
- `AddReaction(long messageId, string emoji)` — No changes
- `RemoveReaction(long messageId, string emoji)` — No changes
- `ClearMessages(long conversationId, DateTimeOffset from, DateTimeOffset to)` — No changes
- `IsUserOnline(long targetUserId)` — No changes

---

## Server → Client (Hub Events)

### New Events

#### `MessagesDelivered(List<long> messageIds)`

**Purpose**: Batched delivery acknowledgment notification. Sent when multiple messages to the same sender device are acknowledged at once.

- **Triggered by**: `MessagesController.BulkAcknowledgeDelivery` (REST endpoint)
- **Target**: Sender's device group (`device_{senderDeviceId}`)
- **Payload**: List of message IDs that were delivered
- **Client handling**: Process each messageId same as individual `MessageDelivered` event

#### `RateLimitWarning(string message, int cooldownSeconds)`

**Purpose**: Notify client that they've been rate-limited on message sending.

- **Triggered by**: `ChatHub.SendMessage` when rate limit is exceeded
- **Target**: The calling connection
- **Payload**: Human-readable message + cooldown duration in seconds
- **Client handling**: Display inline warning with countdown timer below message input

### Unchanged Events

- `MessageDelivered(long messageId)` — Still used for individual acks
- `MessageRead(long messageId)` — No changes
- `UserTyping(long conversationId, string displayName)` — No changes
- `UserOnline(long userId)` — No changes
- `UserOffline(long userId, DateTimeOffset lastSeenAt)` — No changes
- `MessageDeleted(long messageId, long conversationId)` — No changes
- `ReactionAdded(long messageId, long userId, string displayName, string emoji)` — No changes
- `ReactionRemoved(long messageId, long userId, string emoji)` — No changes

---

## SignalR Configuration Changes

### Server (Program.cs)

```text
Current:
  builder.Services.AddSignalR() with default options

New:
  builder.Services.AddSignalR(options =>
  {
      options.KeepAliveInterval = TimeSpan.FromSeconds(30);    // Ping every 30s
      options.ClientTimeoutInterval = TimeSpan.FromSeconds(90); // Dead after 90s
  })
```

### Client (SignalRService.cs)

```text
Current:
  HubConnectionBuilder with default timeouts

New:
  .WithAutomaticReconnect() already configured
  ServerTimeout auto-matches server's ClientTimeoutInterval
  No client-side changes needed for keep-alive
```
