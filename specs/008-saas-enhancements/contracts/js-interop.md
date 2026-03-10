# Contract: JavaScript Interop — Changes for v2.0

**Feature**: `008-saas-enhancements`
**Date**: 2026-03-09

## Purpose

Documents all changes to JavaScript functions exposed to Blazor WASM via JS interop.

---

## storage.js — Modified Functions

### `getMessages(conversationId, offset, count)`

**Change**: Add optional `offset` and `count` parameters for pagination (virtual scrolling support).

- **Previous signature**: `getMessages(conversationId)` — returns all messages
- **New signature**: `getMessages(conversationId, offset?, count?)` — returns paginated results
- **Behavior when offset/count omitted**: Returns all messages (backward compatible)
- **Behavior when offset/count provided**: Uses `IDBKeyRange` cursor with `.advance(offset)` and collects `count` items
- **Return**: Array of message objects, ordered by timestamp ascending

### `getMessageCount(conversationId)` (NEW)

**Purpose**: Return total message count for a conversation (needed by Virtualize component's ItemsProvider).

- **Signature**: `getMessageCount(conversationId)` → `number`
- **Implementation**: `store.index('conversationId').count(IDBKeyRange.only(conversationId))`

## storage.js — New Functions (Offline Queue)

### `addToOfflineQueue(entry)` → `number` (entry ID)

**Purpose**: Add a message to the offline queue.

- **Input**: Object with fields: `conversationId`, `recipientDeviceId`, `senderDeviceId`, `ciphertext`, `contentType`, `messageType`, `fileName`, `mimeType`, `replyToMessageId`, `status` (default 'pending'), `retryCount` (default 0), `createdAt` (ISO string)
- **Validation**: Checks total count first. If >= 50, throws error "Offline queue is full".
- **Return**: Auto-generated entry ID

### `getOfflineQueue(conversationId?)` → `Array`

**Purpose**: Get all offline queue entries, optionally filtered by conversation.

- **Input**: Optional `conversationId` filter
- **Return**: Array of queue entries ordered by `createdAt` ascending

### `getOfflineQueueCount()` → `number`

**Purpose**: Get total number of entries in offline queue.

- **Return**: Integer count

### `updateOfflineQueueStatus(id, status, error?)` → `void`

**Purpose**: Update the status of a queue entry.

- **Input**: Entry `id` (number), `status` ('pending'|'sending'|'failed'), optional `error` message
- **Side effect**: If status is 'failed', increments `retryCount` by 1

### `removeFromOfflineQueue(id)` → `void`

**Purpose**: Delete a queue entry (after successful send or user discard).

- **Input**: Entry `id` (number)

## storage.js — New Functions (Expiry Tracking)

### `addExpiryTracker(messageId, conversationId, expiresAt)` → `void`

**Purpose**: Track a disappearing message for expiry.

- **Input**: `messageId` (string), `conversationId` (string), `expiresAt` (ISO 8601 string)

### `getExpiredTrackers()` → `Array`

**Purpose**: Get all expiry trackers where `expiresAt` < current time.

- **Return**: Array of `{ messageId, conversationId, expiresAt }` objects

### `getAllExpiryTrackers()` → `Array`

**Purpose**: Get all expiry trackers (for startup registration with MessageExpiryService).

- **Return**: Array of all tracker objects

### `removeExpiryTracker(messageId)` → `void`

**Purpose**: Remove a tracker after the message has been cleaned up.

- **Input**: `messageId` (string)

## storage.js — New Functions (Cookie Management)

### `setCookie(name, value, path, maxAge, sameSite)` → `void`

**Purpose**: Set a browser cookie without using eval().

- **Input**: `name` (string), `value` (string), `path` (string, default '/'), `maxAge` (number, seconds), `sameSite` (string, default 'lax')
- **Implementation**: `document.cookie = name + '=' + value + ';path=' + path + ';max-age=' + maxAge + ';samesite=' + sameSite;`

### `getCookie(name)` → `string | null`

**Purpose**: Read a browser cookie value by name.

- **Input**: `name` (string)
- **Return**: Cookie value or null if not found

## voice-recorder.js — Modified Functions

### Recording Duration Limit

**Change**: Add 5-minute (300 second) maximum recording duration.

- **New behavior**: When recording starts, a timer begins counting. At 300 seconds, recording automatically stops (calls the same stop function as the stop button).
- **New callback**: `onTimeUpdate(elapsedSeconds, remainingSeconds)` — fired every second during recording. Used by `VoiceRecorder.razor` to display countdown.
- **New callback**: `onMaxDurationReached()` — fired when 300 seconds is reached, before auto-stop.

## tab-leader.js — Bug Fix

**Change**: Wrap both `invokeMethodAsync` calls in try-catch blocks.

- **Lines affected**: The two locations where `.invokeMethodAsync()` is called
- **Reason**: When the Blazor circuit is disposed (page navigation, tab close), these calls throw. The try-catch prevents unhandled promise rejections.
