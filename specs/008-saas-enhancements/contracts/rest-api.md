# Contract: REST API — Changes for v2.0

**Feature**: `008-saas-enhancements`
**Date**: 2026-03-09

## Purpose

Documents all changes to REST API endpoints. Existing endpoints are preserved; changes are additive or behavioral.

---

## Modified Endpoints

### `GET /api/messages/unread-count?conversationId={id}`

**Controller**: `MessagesController`
**Behavioral change**: Now verifies caller is a participant in the conversation before returning count.

- **When unauthorized**: Returns `403 Forbidden`
- **Previous behavior**: Returned count for any authenticated user regardless of participation
- **Response format**: Unchanged — `{ "unreadCount": number }`

### `POST /api/messages/acknowledge-all?deviceId={id}`

**Controller**: `MessagesController`
**Behavioral change**: SignalR notifications are now batched by sender device.

- **Previous behavior**: Sent individual `MessageDelivered` per message
- **New behavior**: Sends `MessagesDelivered` (batch) per unique sender device
- **Response format**: Unchanged — `{ "acknowledged": number }`

### `POST /api/messages/read?conversationId={id}&upToSequenceNumber={seq}`

**Controller**: `MessagesController`
**Behavioral change**: Server now clamps `upToSequenceNumber` to actual max sequence.

- **Effect**: Transparent clamping, no error returned
- **Response format**: Unchanged — `{ "markedRead": number }`

### `GET /api/link-preview?url={url}`

**Controller**: `LinkPreviewController`
**Behavioral change**: Now validates URL length.

- **When URL > 2048 characters**: Returns `400 Bad Request` with message "URL exceeds maximum length."
- **Previous behavior**: No length validation
- **Response format**: Unchanged for valid URLs

### `POST /api/auth/register-with-device`

**Controller**: `AuthController`
**Behavioral change**: Now rate-limited at 5 requests per minute per IP (anonymous).

- **When rate-limited**: Returns `429 Too Many Requests` with `Retry-After` header
- **Previous behavior**: No specific rate limit (covered by general auth rate limit)
- **Implementation**: Add rule to `RateLimitMiddleware`

---

## Unchanged Endpoints

All other endpoints remain unchanged in contract:

- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `GET /api/conversations`
- `POST /api/conversations`
- `POST /api/conversations/group`
- `GET /api/conversations/{id}`
- `POST /api/conversations/{id}/participants`
- `DELETE /api/conversations/{id}/participants/{userId}`
- `PUT /api/conversations/{id}/timer`
- `POST /api/messages`
- `GET /api/messages/pending?deviceId={id}`
- `POST /api/messages/{messageId}/acknowledge`
- `GET /api/users/search?query={q}`
- `GET /api/devices`
- `POST /api/devices`
- `DELETE /api/devices/{id}`
- `GET /api/devices/{id}/prekeys`
- `GET /api/preferences`
- `PUT /api/preferences`
- `POST /api/key-backup`
- `GET /api/key-backup`
- `DELETE /api/key-backup`

---

## New HTTP Headers

### Security Headers (Already Implemented)

The following headers are already present in `Program.cs` inline middleware. No changes needed:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(self), geolocation=(), payment=()`
- `Content-Security-Policy: ...`

### Response Compression Headers (New)

All responses will include one of:
- `Content-Encoding: br` (Brotli, preferred)
- `Content-Encoding: gzip` (fallback)

When the client sends `Accept-Encoding: br, gzip` in the request.

---

## Error Response Sanitization

### Login (`POST /api/auth/login`)

**Previous client behavior** (Login.razor):
```text
On non-success: _errorMessage = raw error body from API
```

**New client behavior**:
```text
On 401: _errorMessage = Loc["Invalid credentials."]
On 429: _errorMessage = Loc["Too many attempts. Please wait."]
On other: _errorMessage = Loc["An error occurred. Please try again."]
Never display raw API response body.
```

### Register (`POST /api/auth/register-with-device`)

**Previous client behavior** (Register.razor):
```text
On non-success: _errorMessage = raw error body from API
```

**New client behavior**:
```text
On 400 (validation): _errorMessage = Loc["Please check your input and try again."]
On 409 (conflict): _errorMessage = Loc["Username already taken."]
On 429: _errorMessage = Loc["Too many attempts. Please wait."]
On other: _errorMessage = Loc["An error occurred. Please try again."]
Never display raw API response body.
```
