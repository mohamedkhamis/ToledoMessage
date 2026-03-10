# Feature Specification: ToledoMessage SaaS Enhancement Plan (v2.0)

**Feature Branch**: `008-saas-enhancements`
**Created**: 2026-03-09
**Status**: Draft
**Input**: Comprehensive enhancement across 6 pillars: Security hardening, Performance optimization, UI/UX polish, Encryption improvements, Functionality gaps, and Speed/Infrastructure.

## Clarifications

### Session 2026-03-09

- Q: What feedback does the user see when rate-limited mid-conversation? → A: Inline warning below the message input with a cooldown countdown timer.
- Q: What happens when the offline message queue exceeds storage capacity? → A: Cap at 50 queued messages; warn user when limit reached, block new sends.
- Q: What happens when a disappearing message timer expires while the app is closed? → A: Clean up expired messages on next app open by scanning tracked messages at startup.
- Q: How are concurrent conflicting admin actions resolved? → A: First-write-wins; second conflicting action fails with "state changed" error.
- Q: How does presence heartbeat handle browser tab backgrounding? → A: Use the real-time connection's built-in keep-alive (transport-level, not JS timers).
- Q: What happens to offline queue messages after max retries? → A: Messages permanently failed after 3 retries, user must manually resend.
- Q: What observability is needed for the new features? → A: Add structured logging for key operations: message send, rate limit events, presence changes, errors.
- Q: How does virtual scrolling handle rapid scroll to very old messages? → A: Load messages in batches on-demand; show loading indicator when fetching older messages.
- Q: What are the scalability limits for concurrent users? → A: Support up to 100 concurrent users. Configurable via appsettings for future upgrade to Redis-backed scaling (100+ users).
- Q: How to handle IndexedDB storage approaching capacity? → A: Show warning at 80% capacity; block new offline queue entries at 95%.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure Messaging Without Exploitable Gaps (Priority: P1)

As a security-conscious user, I want the messaging platform to protect me from common attack vectors (rate abuse, authorization bypasses, error information leaks) so that my conversations and account remain safe.

**Why this priority**: Security vulnerabilities can lead to data breaches, unauthorized access, and loss of user trust. These must be addressed before any feature additions.

**Independent Test**: Can be tested by running security audit tools, attempting rate limit bypasses, and verifying error responses contain no internal details.

**Acceptance Scenarios**:

1. **Given** a user sending messages rapidly, **When** they exceed 60 messages/minute via SignalR, **Then** they receive a rate limit error and further messages are blocked for the cooldown period.
2. **Given** a user submitting a typing indicator rapidly, **When** they exceed 10 indicators/minute, **Then** additional indicators are silently dropped.
3. **Given** an unauthenticated user, **When** they attempt to register more than 5 times per minute, **Then** subsequent attempts are rejected with a rate limit response.
4. **Given** a user not participating in a conversation, **When** they request unread counts or attempt to delete messages in that conversation, **Then** they receive an authorization error.
5. **Given** invalid login credentials, **When** the login attempt fails, **Then** the error message is a generic localized string with no internal details exposed.
6. **Given** a link preview request with a URL longer than 2048 characters, **When** submitted, **Then** the request is rejected.
7. **Given** a read pointer update with a sequence number beyond the actual max, **When** submitted, **Then** the system clamps it to the actual maximum sequence number.

---

### User Story 2 - Fast and Responsive Experience (Priority: P1)

As a user with many conversations and messages, I want the application to load quickly and respond instantly to my actions so that messaging feels seamless.

**Why this priority**: Performance directly impacts user retention and satisfaction. Slow apps drive users to competitors.

**Independent Test**: Can be tested by measuring page load times, message list scroll performance, and typing indicator latency with browser dev tools.

**Acceptance Scenarios**:

1. **Given** a conversation with 500+ messages, **When** I open it, **Then** only visible messages plus a buffer are rendered (virtual scrolling), and scrolling is smooth at 60fps.
2. **Given** the application is loaded, **When** assets are transferred over the network, **Then** they are compressed (gzip/brotli), reducing transfer size.
3. **Given** a user is typing in a conversation, **When** the typing indicator fires, **Then** no database queries are made (display name and participant list are cached).
4. **Given** multiple messages are delivered simultaneously, **When** delivery acknowledgments are sent, **Then** they are batched per sender device rather than sent individually.
5. **Given** a user searches for contacts, **When** the query executes, **Then** it uses efficient database indexing rather than full-table scans.
6. **Given** the emoji picker is open, **When** I search for an emoji, **Then** input is debounced (300ms) and results are memoized to avoid redundant computation.

---

### User Story 3 - Accessible and Responsive Interface (Priority: P2)

As a user on any device (desktop, tablet, mobile) including users with disabilities, I want the interface to be fully accessible and responsive so that I can use the app comfortably regardless of my device or abilities.

**Why this priority**: Accessibility compliance (WCAG 2.1 AA) is both a legal requirement in many jurisdictions and essential for inclusive design.

**Independent Test**: Can be tested with screen readers, keyboard-only navigation, and responsive viewport testing.

**Acceptance Scenarios**:

1. **Given** I use a screen reader, **When** I navigate the sidebar, **Then** all interactive elements are proper button/link elements with ARIA labels.
2. **Given** I use keyboard only, **When** I open the emoji picker, **Then** I can navigate with arrow keys and select with Enter.
3. **Given** I view the app on a mobile phone, **When** theme cards are displayed in Settings, **Then** they reflow to fit the screen without horizontal overflow.
4. **Given** a video is displayed in a message, **When** viewed on mobile, **Then** it scales to fit the viewport width.
5. **Given** an error occurs, **When** it is displayed, **Then** screen readers announce it immediately via aria-live regions.
6. **Given** messages span multiple days, **When** viewing a conversation, **Then** date separators (Today, Yesterday, specific dates) appear between message groups.

---

### User Story 4 - Graceful Error Recovery and Offline Resilience (Priority: P2)

As a user with an unreliable network connection, I want the app to handle disconnections gracefully, queue my messages, and recover automatically so that I don't lose my work.

**Why this priority**: Real-world network conditions are imperfect. Graceful degradation prevents frustration and data loss.

**Independent Test**: Can be tested by simulating network disconnection/reconnection and verifying message queue behavior.

**Acceptance Scenarios**:

1. **Given** SignalR is disconnected, **When** I send a message, **Then** it is queued locally with a "pending" indicator.
2. **Given** queued messages exist, **When** SignalR reconnects, **Then** queued messages are automatically sent in order.
3. **Given** the message list fails to load, **When** the error is displayed, **Then** a retry button is shown.
4. **Given** SignalR is disconnected, **When** I look at the UI, **Then** an explicit "offline" indicator is visible.
5. **Given** I selected a file to send, **When** the send fails, **Then** the file selection is preserved (not cleared) so I can retry.

---

### User Story 5 - Voice Recording with Safeguards (Priority: P2)

As a user recording a voice message, I want clear time limits and remaining time feedback so that I don't accidentally create excessively long recordings.

**Why this priority**: Unbounded recordings waste storage and create poor UX. Time limits are a standard feature in messaging apps.

**Independent Test**: Can be tested by starting a recording and observing the time limit indicator and auto-stop behavior.

**Acceptance Scenarios**:

1. **Given** I am recording a voice message, **When** the recording reaches 5 minutes, **Then** it automatically stops.
2. **Given** I am recording, **When** I look at the recorder UI, **Then** I see a remaining time indicator counting down.

---

### User Story 6 - In-Conversation Message Search (Priority: P3)

As a user looking for a specific message, I want to search within a conversation by text content so that I can quickly find past messages.

**Why this priority**: Search within conversations is a common power-user feature that improves daily workflow.

**Independent Test**: Can be tested by typing a search term and verifying matching messages are highlighted.

**Acceptance Scenarios**:

1. **Given** I am in a conversation, **When** I enter a search term, **Then** messages matching the text are filtered and displayed.
2. **Given** search results are shown, **When** I view a matching message, **Then** the matching text is highlighted.

---

### User Story 7 - Group Administration (Priority: P3)

As a group conversation admin, I want to manage admin roles and member permissions so that group management is flexible and shared.

**Why this priority**: Multi-admin support is needed for groups to function reliably when the original creator is unavailable.

**Independent Test**: Can be tested by transferring admin role and verifying permissions change.

**Acceptance Scenarios**:

1. **Given** I am a group admin, **When** I promote another participant to admin, **Then** they gain admin capabilities.
2. **Given** multiple admins exist, **When** any admin removes a member or sets a disappearing timer, **Then** the action succeeds.
3. **Given** I am the only admin, **When** I attempt to leave, **Then** I am prompted to transfer admin role first.

---

### User Story 8 - Encryption Hardening and Disappearing Messages (Priority: P2)

As a privacy-focused user, I want my disappearing messages to actually disappear on time and my encryption signatures to be future-proof so that my privacy guarantees are real.

**Why this priority**: Disappearing messages are a promised feature that currently doesn't work end-to-end. Signature versioning prevents breaking changes.

**Independent Test**: Can be tested by sending a disappearing message and verifying it is removed from display and storage after the timer expires.

**Acceptance Scenarios**:

1. **Given** a message with a disappearing timer is received, **When** the timer expires, **Then** the message is removed from display and cleaned from local storage.
2. **Given** a signed message, **When** the signature is examined, **Then** it includes a version byte prefix for forward compatibility.
3. **Given** old signatures without version prefix, **When** verified, **Then** they are still accepted (backward compatible).

---

### User Story 9 - Presence Accuracy (Priority: P3)

As a user viewing contact online status, I want presence indicators to be accurate so that I know when someone is actually online.

**Why this priority**: Stale presence (showing online when user left) reduces trust in the platform.

**Independent Test**: Can be tested by closing a browser tab and verifying the user shows as offline within 90 seconds.

**Acceptance Scenarios**:

1. **Given** a user closes their browser, **When** 90 seconds elapse without heartbeat, **Then** they are marked offline.
2. **Given** an active user, **When** heartbeats are sent regularly, **Then** they remain shown as online.

---

### Edge Cases

- When a user is rate-limited mid-conversation, an inline warning with cooldown countdown is shown below the message input.
- How does virtual scrolling handle rapid scroll to very old messages?
- Offline message queue is capped at 50 messages; user is warned and new sends are blocked until reconnection.
- When a disappearing message timer expires while the app is closed, expired messages are cleaned up on next app open by scanning tracked messages at startup.
- Concurrent admin conflicts (e.g., two admins removing each other) are resolved by first-write-wins; the second action fails with a "state changed" error.
- Presence heartbeat uses transport-level keep-alive (not JS timers) to avoid false-offline status from browser tab throttling.

## Requirements *(mandatory)*

### Functional Requirements

**Security Hardening**
- **FR-001**: System MUST rate-limit SignalR `SendMessage` to 60 invocations per user per minute.
- **FR-002**: System MUST rate-limit SignalR `TypingIndicator` to 10 invocations per user per minute.
- **FR-002a**: When a user is rate-limited, the system MUST display an inline warning below the message input with a cooldown countdown timer.
- **FR-003**: System MUST rate-limit `/api/auth/register-with-device` to 5 requests per minute for anonymous users.
- **FR-004**: System MUST verify conversation participation before returning unread counts.
- **FR-005**: System MUST verify conversation participation before processing `DeleteForEveryone`.
- **FR-006**: System MUST clamp read pointer `upToSequenceNumber` to the actual maximum sequence in the conversation.
- **FR-007**: System MUST display only generic, localized error messages on login/register failures (no raw API bodies or stack traces).
- **FR-008**: System MUST NOT use `eval()` for cookie management; use direct `document.cookie` assignment.
- **FR-009**: System MUST reject link preview URLs longer than 2048 characters.
- **FR-010**: System MUST validate that storage encryption is initialized before storing private keys.

**Performance**
- **FR-011**: System MUST serve responses with gzip or brotli compression for text-based content types.
- **FR-012**: System MUST use virtual scrolling for message lists, rendering only visible messages plus a configurable buffer.
- **FR-013**: System MUST cache typing indicator display names and participant lists to avoid per-keystroke database queries.
- **FR-014**: System MUST batch delivery acknowledgment notifications per sender device.
- **FR-015**: System MUST use efficient database queries for user search (indexed, case-insensitive without runtime string manipulation).
- **FR-016**: System MUST debounce emoji picker search (300ms) and conversation list filter (200ms) inputs.
- **FR-017**: System MUST add random jitter (±30s) to background cleanup task intervals.
- **FR-018**: System MUST clean up consumed one-time pre-keys older than 30 days.

**UI/UX**
- **FR-019**: All interactive sidebar elements MUST be semantic button/link elements with ARIA labels.
- **FR-020**: Emoji picker MUST support keyboard navigation (arrow keys, Enter to select).
- **FR-021**: Theme card grid MUST reflow on small viewports without horizontal overflow.
- **FR-022**: Video elements MUST be responsive (max-width: 100%).
- **FR-023**: Date separators MUST appear between message groups from different days.
- **FR-024**: All user-visible text MUST be localized (English and Arabic), including emoji picker and timer labels.
- **FR-025**: System MUST show a retry button when message list loading fails.
- **FR-026**: System MUST show an offline indicator when SignalR is disconnected.
- **FR-027**: System MUST preserve file selection on send failure.
- **FR-028**: Voice recorder MUST enforce a 5-minute maximum with remaining time indicator and auto-stop.
- **FR-029**: System MUST provide in-conversation message search with text highlighting.

**Encryption & Functionality**
- **FR-030**: Signature format MUST include a version byte prefix, with backward compatibility for v0 (unversioned) signatures.
- **FR-031**: MessageExpiryService MUST be wired to actual message flow: track received messages, fire expiry events, remove from display and local storage. On app startup, the system MUST scan tracked messages and immediately remove any whose timer has already elapsed.
- **FR-032**: System MUST queue unsent messages locally when offline and auto-retry on reconnection with pending status indicator. Queue is capped at 50 messages; when the limit is reached, the user is warned and new sends are blocked until reconnection.
- **FR-033**: Group conversations MUST support multiple admins and admin role transfer. Concurrent conflicting admin actions use first-write-wins: the second conflicting action fails with a "state changed" error prompting the user to refresh.
- **FR-034**: System MUST mark users offline after 90 seconds without heartbeat. Heartbeat MUST use the real-time connection's built-in keep-alive mechanism (transport-level) to avoid browser tab throttling of JS timers.

### Key Entities

- **OfflineMessageQueue**: Locally queued messages awaiting send, with status (pending/sending/failed), content, conversation reference, timestamp, and retryCount. Maximum capacity: 50 messages. Messages permanently failed after 3 retries require manual resend.
- **MessageExpiryTracker**: Tracked messages with expiry timers, linking message IDs to their scheduled removal time.
- **GroupAdmin**: Relationship between users and group conversations indicating admin privileges (extends existing ConversationParticipant).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: No user can send more than 60 messages per minute through any channel — verified by automated rate limit testing.
- **SC-002**: All API error responses to end users contain zero internal system details (no stack traces, no raw exception messages) — verified by security scan.
- **SC-003**: Message list with 1000+ messages scrolls at 60fps with no layout shifts — verified by browser performance profiling.
- **SC-004**: Page load transfer size decreases by at least 40% with compression enabled — verified by network measurement.
- **SC-005**: Typing indicator produces zero database queries during normal usage — verified by SQL profiling.
- **SC-006**: Application passes WCAG 2.1 AA automated audit with zero violations — verified by accessibility testing tool.
- **SC-007**: All interactive elements are keyboard-accessible — verified by complete keyboard-only navigation test.
- **SC-008**: Offline-queued messages are delivered within 5 seconds of reconnection — verified by network simulation test.
- **SC-009**: Disappearing messages are removed from display within 30 seconds of timer expiry — verified by timed observation.
- **SC-010**: Users marked offline within 120 seconds of actual disconnection — verified by presence monitoring.
- **SC-011**: All existing 231+ tests continue to pass after all changes.
- **SC-012**: Both English and Arabic localizations are complete with no untranslated strings visible.

## Assumptions

- The application uses the existing `RateLimitService` infrastructure which can be extended for SignalR hub methods.
- Virtual scrolling can be implemented with the platform's built-in virtualization component or equivalent approach.
- Offline message queuing uses browser local storage (already available in the client).
- Group admin management requires additive-only database schema changes (new column or table, no breaking changes).
- Background cleanup jitter is sufficient for current deployment scale (single or few instances).
- The 5-minute voice recording limit is appropriate for the target user base.
- Signature format versioning is backward compatible — existing signed data remains verifiable.

## Scope Boundaries

**In Scope**: All items listed in the 6 pillars (SEC-001 through FUNC-004).

**Out of Scope**:
- Migration to a different database engine
- Native mobile applications
- End-to-end encrypted group key management changes
- File encryption at rest on the server (already handled by E2EE)
- Push notifications (web push API)
- Video/audio calling
