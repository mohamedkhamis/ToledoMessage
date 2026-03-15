# Research: ToledoVault SaaS Enhancement Plan (v2.0)

**Feature**: `008-saas-enhancements`
**Date**: 2026-03-09

## Purpose

This document records all research findings and technical decisions made during Phase 0 planning. Each decision resolves a technical unknown or confirms a best practice. All "NEEDS CLARIFICATION" items from the Technical Context are resolved here.

---

## R-001: SignalR Hub Rate Limiting

**Question**: How to apply rate limiting to SignalR hub methods (not HTTP endpoints)?

**Decision**: Inject existing `RateLimitService` directly into `ChatHub` constructor and call `IsRateLimited()` at the beginning of `SendMessage()` and `TypingIndicator()` methods.

**Key details**:
- **Rate limit keys**: Use pattern `signalr:send:{userId}` for messages, `signalr:typing:{userId}` for typing indicators. The `userId` is extracted from `Context.User` claims (same as `GetUserId()` helper already in ChatHub).
- **SendMessage limit**: 60 invocations per user per minute (TimeSpan.FromMinutes(1), maxRequests: 60).
- **TypingIndicator limit**: 10 invocations per user per minute (TimeSpan.FromMinutes(1), maxRequests: 10).
- **When rate-limited**: Throw `HubException("Rate limit exceeded. Please wait before sending more messages.")`.
- **Client-side handling**: The `SignalRService.cs` catches HubExceptions. The client displays an inline warning below the message input with a countdown timer (per clarification Q1).
- **No new classes needed**: `RateLimitService` is already registered as a singleton. Just add it to `ChatHub`'s constructor injection.

**Implementation steps**:
1. Add `RateLimitService rateLimitService` to `ChatHub` constructor parameters.
2. Add 3 lines at top of `SendMessage`: `var userId = GetUserId(); if (rateLimitService.IsRateLimited($"signalr:send:{userId}", 60, TimeSpan.FromMinutes(1))) throw new HubException("Rate limit exceeded.");`
3. Add same pattern at top of `TypingIndicator` with limit 10.
4. Client: Add rate-limit error detection in `SignalRService.cs` and surface via event to `MessageInput.razor`.

**Alternatives rejected**:
- ASP.NET Core `RateLimiterOptions` middleware: Only intercepts HTTP pipeline, not SignalR hub method invocations.
- `IHubFilter`: Possible but adds unnecessary abstraction for 2 methods.

---

## R-002: Message List Virtualization

**Question**: How to implement virtual scrolling for potentially thousands of messages in Chat.razor?

**Decision**: Use Blazor's built-in `<Virtualize>` component with `ItemsProvider` delegate.

**Key details**:
- **Component**: `Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize<TItem>` — already available, no new NuGet package.
- **Item provider**: An async delegate that fetches messages from IndexedDB by range (offset + count), called `LoadMessagesAsync(ItemsProviderRequest request)`.
- **Item size**: Set `ItemSize="72"` (approximate height of a message bubble in pixels) for initial estimation. The component auto-adjusts.
- **Placeholder**: Show a lightweight shimmer/skeleton placeholder while items load.
- **Scroll direction**: Messages scroll bottom-to-top (newest at bottom). This requires reversing the data provider or using CSS `flex-direction: column-reverse`.
- **Buffer**: Default overscan of 3 items above/below viewport. Adjust to 5 for smoother scrolling.

**Implementation steps**:
1. Replace the `@foreach` message loop in `Chat.razor` with `<Virtualize ItemsProvider="LoadMessagesAsync" ItemSize="72" OverscanCount="5">`.
2. Implement `LoadMessagesAsync` that queries IndexedDB via JS interop (`storage.js` `getMessages(conversationId, offset, count)`).
3. Update `storage.js` `getMessages` to support offset/count parameters using `IDBKeyRange`.
4. Add a `<Placeholder>` template with a message skeleton.
5. Handle scroll-to-bottom on new message receipt.

**Alternatives rejected**:
- Custom IntersectionObserver: 3-4x more code, manual DOM measurement, no built-in placeholder.
- Rendering all messages with `display:none` toggle: Still creates all DOM nodes, defeating the purpose.

---

## R-003: Response Compression

**Question**: Best approach for HTTP response compression in ASP.NET Core?

**Decision**: Use built-in `Microsoft.AspNetCore.ResponseCompression` middleware with Brotli (priority) and Gzip providers.

**Key details**:
- **Registration**: In `Program.cs`, add `builder.Services.AddResponseCompression(opts => { opts.EnableForHttps = true; opts.Providers.Add<BrotliCompressionProvider>(); opts.Providers.Add<GzipCompressionProvider>(); opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/wasm", ... }); });`
- **Middleware order**: `app.UseResponseCompression()` must be placed before `app.MapStaticAssets()` and before `app.UseStaticFiles()` so it can compress responses.
- **Brotli level**: Quality 4 (fast compression, good ratio). Quality 11 is too slow for dynamic content.
- **Gzip level**: `CompressionLevel.Fastest` for dynamic content.
- **Expected impact**: ~40-60% reduction in transfer size for JSON and HTML responses. WASM files are already served pre-compressed by MapStaticAssets.

**Implementation steps**:
1. Add `builder.Services.AddResponseCompression(...)` in Program.cs services section.
2. Add `builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);`
3. Add `app.UseResponseCompression();` before static files middleware.
4. Test with browser DevTools Network tab — verify `Content-Encoding: br` or `Content-Encoding: gzip` headers.

**Alternatives rejected**:
- IIS-only compression: Not portable, doesn't help local development.
- Pre-compressed static files only: Doesn't help dynamic API responses (JSON).

---

## R-004: Typing Indicator Caching

**Question**: How to eliminate the 2 DB queries per typing indicator keystroke?

**Decision**: Cache display name at connection time in a static `ConcurrentDictionary<string, string>` (connectionId → displayName). Cache conversation participants in a `ConcurrentDictionary<long, (List<long> Ids, DateTimeOffset CachedAt)>` with 60-second TTL.

**Key details**:
- **Display name cache**: Populated in `RegisterDevice()` which already validates the device/user. Add: `var displayName = await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync(); ConnectionDisplayNameMap[Context.ConnectionId] = displayName ?? "";`
- **Cleanup**: Remove from cache in `OnDisconnectedAsync()`: `ConnectionDisplayNameMap.TryRemove(Context.ConnectionId, out _);`
- **Participant cache**: In `TypingIndicator()`, check cache first. If entry exists and `(now - CachedAt) < 60s`, use cached list. Otherwise, query DB and update cache.
- **Cache invalidation**: Participant cache entries are evicted after 60s TTL. For immediate invalidation (member added/removed), clear the specific conversation entry in the relevant controller action.

**Implementation steps**:
1. Add `private static readonly ConcurrentDictionary<string, string> ConnectionDisplayNameMap = new();` to ChatHub.
2. Add `private static readonly ConcurrentDictionary<long, (List<long> Ids, DateTimeOffset CachedAt)> ParticipantCache = new();` to ChatHub.
3. In `RegisterDevice`, after validation, cache the display name.
4. In `OnDisconnectedAsync`, remove the connection's display name entry.
5. In `TypingIndicator`, replace the 2 DB queries with cache lookups.
6. In `ConversationsController` (add/remove participant), invalidate the participant cache for that conversation.

**Alternatives rejected**:
- Redis: Overkill for single-instance deployment.
- Client-provided display name: Trust boundary violation.

---

## R-005: Offline Message Queue

**Question**: Where and how to store offline messages on the client?

**Decision**: Use IndexedDB via a new `offlineQueue` object store in `storage.js`. Expose via `SignalRService.cs` events.

**Key details**:
- **Object store**: `offlineQueue` with auto-increment key, indexed on `conversationId`.
- **Schema per entry**: `{ id: autoIncrement, conversationId: long, recipientDeviceId: long, ciphertext: string, contentType: number, fileName: string|null, mimeType: string|null, replyToMessageId: long|null, status: 'pending'|'sending'|'failed', createdAt: ISO8601, retryCount: number }`
- **Cap**: 50 entries max. On `addToQueue`, count entries first. If >= 50, reject with error.
- **Retry on reconnect**: `SignalRService` listens for reconnection. On reconnect, read all `pending` entries ordered by `createdAt`, set status to `sending`, attempt `SendMessage` via hub. On success, delete entry. On failure, increment `retryCount`, set status back to `pending` if retryCount < 3, else `failed`.
- **Max retries**: 3 attempts. After 3 failures, status becomes `failed` and user must manually retry.
- **UI**: `MessageInput.razor` checks `SignalRService.IsConnected`. If false, queue via `storage.js` instead of sending via hub. Show pending icon (clock) on queued messages.

**Implementation steps**:
1. In `storage.js`, add `offlineQueue` store to the IndexedDB `open` call (version bump).
2. Add JS functions: `addToOfflineQueue(entry)`, `getOfflineQueue(conversationId?)`, `updateOfflineQueueStatus(id, status)`, `removeFromOfflineQueue(id)`, `getOfflineQueueCount()`.
3. In `SignalRService.cs`, add `IsConnected` property and `OnReconnected` event that triggers queue flush.
4. In `MessageInput.razor`, check connection status before sending. If disconnected, call `addToOfflineQueue` via JS interop.
5. In `Chat.razor`, render queued messages with "pending" status indicator.

**Alternatives rejected**:
- localStorage: Limited to ~5MB, synchronous API.
- In-memory only: Lost on page refresh.

---

## R-006: Signature Format Versioning

**Question**: How to add version information to hybrid signatures without breaking existing signatures?

**Decision**: Prepend a single byte `0x01` to v1 signatures. Detect v0 (legacy) by absence of the version prefix.

**Key details**:
- **File**: `src/ToledoVault.Crypto/HybridSigner.cs` (or equivalent signing class).
- **v0 format**: `[Ed25519Signature(64 bytes)][DilithiumSignature(variable)]` — current format.
- **v1 format**: `[0x01][Ed25519Signature(64 bytes)][DilithiumSignature(variable)]`.
- **Detection**: Ed25519 signatures are 64 bytes and their first byte is NOT `0x01` with overwhelming probability (it's a point encoding). But for robustness, the verifier checks: if `signature[0] == 0x01` AND `signature.Length > 1 + 64 + minDilithiumSize`, parse as v1. Otherwise, parse as v0.
- **Sign method**: Always produces v1 going forward.
- **Verify method**: Auto-detects and handles both v0 and v1.

**Implementation steps**:
1. In the signing method, prepend `0x01` byte before the combined signature.
2. In the verification method, check first byte. If `0x01`, strip it and verify remainder. Otherwise, verify as v0.
3. Add unit tests: sign with v1 → verify succeeds. Verify existing v0 test vectors → still succeeds.
4. Document wire format in code comments.

**Alternatives rejected**:
- JSON wrapper: Increases size, adds parsing complexity.
- Length-prefix format: More complex than needed for version differentiation.

---

## R-007: Presence Heartbeat Timeout

**Question**: How to reliably detect user disconnection without JS timers (which browsers throttle)?

**Decision**: Configure SignalR's built-in `KeepAliveInterval` and `ClientTimeoutInterval`.

**Key details**:
- **Server config** (Program.cs): `options.KeepAliveInterval = TimeSpan.FromSeconds(30);` (server sends ping every 30s). `options.ClientTimeoutInterval = TimeSpan.FromSeconds(90);` (server considers client dead after 90s of no response).
- **Client config**: SignalR JS client automatically responds to server pings at the WebSocket transport level. This is NOT a JS timer — it's handled by the WebSocket protocol layer, immune to browser throttling.
- **Existing handler**: `OnDisconnectedAsync` already calls `presence.RemoveConnection()` and broadcasts `UserOffline`. No new code needed for the core mechanism.
- **Behavior**: Active tab = WebSocket responds to pings instantly. Background tab = WebSocket still responds (transport-level, not JS). Closed tab = no response → 90s timeout → `OnDisconnectedAsync` fires.

**Implementation steps**:
1. In `Program.cs`, update the `AddSignalR` configuration to set `KeepAliveInterval` and `ClientTimeoutInterval`.
2. Verify the client-side `HubConnectionBuilder` in `SignalRService.cs` has `ServerTimeout` matching (default auto-matches).
3. Test by closing a browser tab and measuring time until user shows offline.

**Alternatives rejected**:
- Custom JS heartbeat: Throttled in background tabs.
- Separate ping endpoint: Unnecessary when SignalR provides built-in mechanism.

---

## R-008: Batch Delivery Acknowledgments

**Question**: How to reduce SignalR notification spam during bulk delivery acknowledgment?

**Decision**: Group acknowledged messages by `senderDeviceId` and send one `MessagesDelivered` notification per unique sender device.

**Key details**:
- **Current code** (`MessagesController.BulkAcknowledgeDelivery`): Loops through each `(messageId, senderDeviceId)` tuple and sends individual `MessageDelivered` SignalR messages.
- **New approach**: Group by `senderDeviceId` using LINQ `.GroupBy()`, then send `MessagesDelivered` (plural) with `List<long>` messageIds per group.
- **Client update**: `SignalRService.cs` needs to handle both `MessageDelivered` (single, for backward compat) and `MessagesDelivered` (batch, new).
- **Typical improvement**: For a device with 20 pending messages from 2 senders, reduces from 20 SignalR calls to 2.

**Implementation steps**:
1. In `MessagesController.BulkAcknowledgeDelivery`, replace the foreach loop with: `var grouped = acknowledged.GroupBy(a => a.SenderDeviceId);` then `foreach (var group in grouped) { await hubContext.Clients.Group($"device_{group.Key}").SendAsync("MessagesDelivered", group.Select(g => g.MessageId).ToList()); }`
2. In `SignalRService.cs`, add `.On<List<long>>("MessagesDelivered", ...)` handler that processes each ID.
3. Keep the old `MessageDelivered` handler for individual acks (from `ChatHub.AcknowledgeDelivery`).

**Alternatives rejected**:
- Single notification with all IDs and sender info: Over-complex serialization.
- Remove individual ack entirely: Still needed for real-time single-message delivery notification.

---

## R-009: Read Pointer Clamping

**Question**: How to prevent clients from setting arbitrary future sequence numbers?

**Decision**: In `MessageRelayService.AdvanceReadPointer`, query `MAX(SequenceNumber)` and clamp.

**Key details**:
- **Current behavior**: `AdvanceReadPointer` takes `upToSequenceNumber` and updates the read pointer directly. No validation against actual message sequence numbers.
- **New behavior**: Before updating, query: `var maxSeq = await db.EncryptedMessages.Where(m => m.ConversationId == conversationId).MaxAsync(m => (long?)m.SequenceNumber) ?? 0;` then `upToSequenceNumber = Math.Min(upToSequenceNumber, maxSeq);`
- **Edge case**: If conversation has no messages (maxSeq = 0), the pointer stays at 0.

**Implementation steps**:
1. In `MessageRelayService.AdvanceReadPointer`, add the MAX query before the pointer update.
2. Add unit test: attempt to set pointer to 999999 when max sequence is 5 → pointer is clamped to 5.

**Alternatives rejected**:
- Reject request entirely: Less user-friendly. Clamping achieves the security goal while still advancing the pointer.

---

## R-010: eval() Elimination

**Question**: Where is eval() used and how to replace it?

**Decision**: Search for `eval(` in all JS files and replace with direct `document.cookie` assignment.

**Key details**:
- **Known location**: The `App.razor` inline script and `storage.js` `__toggleLang` function use `document.cookie = ...` directly (which is correct). Need to scan all JS files for any `eval()` usage.
- **Replacement**: Add a `setCookie(name, value, path, maxAge, sameSite)` function to `storage.js` that constructs the cookie string and assigns to `document.cookie`.
- **CSP note**: The existing CSP header includes `'unsafe-eval'` for Blazor WASM's IL interpreter. This cannot be removed. But eliminating `eval()` in application code reduces attack surface.

**Implementation steps**:
1. Search all `.js` and `.razor` files for `eval(`.
2. Replace each occurrence with direct `document.cookie` assignment or the new `setCookie` helper.
3. Add `setCookie` and `getCookie` helpers to `storage.js`.
4. Verify no functional regression in language switching and cookie-based culture.

**Alternatives rejected**:
- C# JSInterop for all cookie operations: Adds unnecessary async round-trip for synchronous operations.
