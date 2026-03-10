# Tasks: ToledoMessage SaaS Enhancement Plan (v2.0)

**Input**: Design documents from `/specs/008-saas-enhancements/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md

**Tests**: Tests are NOT explicitly requested in the feature specification - this is an enhancement project building on existing passing tests (231 tests)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Project Verification)

**Purpose**: Verify existing project structure is ready for enhancement work

- [X] T001 Verify existing project builds successfully in src/ToledoMessage.Client/ and src/ToledoMessage/
- [X] T002 [P] Verify existing 231 tests pass (152 server + 65 crypto + 8 integration + 6 client) - 1 pre-existing failure in AccountDeletionServiceTests
- [X] T003 [P] Document any existing rate limiting infrastructure in src/ToledoMessage/ - RateLimitService.cs found

**Checkpoint**: Project ready for enhancement implementation

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure updates needed before user story implementation

**⚠️ CRITICAL**: Some user stories can begin while this phase completes

### Shared Infrastructure Updates

- [X] T004 Add ResponseCompression to Program.cs in src/ToledoMessage/Program.cs (FR-011)
- [X] T005 Configure SignalR KeepAliveInterval (30s) and ClientTimeoutInterval (90s) in src/ToledoMessage/Program.cs (FR-034)
- [X] T006 [P] Add storage.js setCookie/getCookie helper functions in src/ToledoMessage.Client/wwwroot/storage.js (FR-008)
- [X] T007 [P] Add offlineQueue object store to IndexedDB in src/ToledoMessage.Client/wwwroot/storage.js (FR-032)
- [X] T008 Update ChatHub to include RateLimitService injection in src/ToledoMessage/Hubs/ChatHub.cs (FR-001, FR-002)

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Secure Messaging Without Exploitable Gaps (Priority: P1) 🎯 MVP

**Goal**: Implement security hardening: rate limiting on SignalR hub, authorization gap closures, error sanitization, eval removal, storage encryption checks

**Independent Test**: Automated rate limit testing confirms no user can send >60 msg/min; security scan confirms zero internal details in error responses

### Implementation for User Story 1

- [X] T009 [P] [US1] Add rate limiting to ChatHub.SendMessage (60/min) in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T010 [P] [US1] Add rate limiting to ChatHub.TypingIndicator (10/min) in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T011 [US1] Add rate-limit error handling in SignalRService.cs with countdown timer display in src/ToledoMessage.Client/Pages/Chat.razor
- [X] T012 [US1] Add rate-limit warning UI component in MessageInput.razor in src/ToledoMessage.Client/Components/MessageInput.razor
- [X] T013 [P] [US1] Add conversation participation check for unread counts in MessagesController.cs in src/ToledoMessage/Controllers/MessagesController.cs
- [X] T014 [P] [US1] Add conversation participation check for DeleteForEveryone in ChatHub.cs in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T015 [US1] Implement read pointer clamping in MessageRelayService.AdvanceReadPointer in src/ToledoMessage/Services/MessageRelayService.cs
- [X] T016 [US1] Sanitize error messages in Login.razor and Register.razor in src/ToledoMessage.Client/Pages/Login.razor, src/ToledoMessage.Client/Pages/Register.razor
- [X] T017 [US1] Remove any eval() usage in storage.js and replace with setCookie in src/ToledoMessage.Client/wwwroot/storage.js
- [X] T018 [US1] Add link preview URL validation (≤2048 chars) in LinkPreviewService.cs in src/ToledoMessage/Services/LinkPreviewService.cs
- [X] T019 [US1] Add storage encryption initialization check in LocalStorageService.cs in src/ToledoMessage.Client/Services/LocalStorageService.cs

**Checkpoint**: Security hardening complete and independently testable

---

## Phase 4: User Story 2 - Fast and Responsive Experience (Priority: P1)

**Goal**: Implement performance optimizations: virtual scrolling, response compression, typing indicator caching, batched ACKs, query optimization, debouncing

**Independent Test**: Browser profiling confirms 60fps scrolling with 1000+ messages; SQL profiling shows zero DB queries for typing indicators

### Implementation for User Story 2

- [X] T020 [P] [US2] Implement paginated message loading in Chat.razor (load 50 most recent, "Load earlier messages" button) in src/ToledoMessage.Client/Pages/Chat.razor (Blazor Virtualize deferred — pagination approach used instead)
- [X] T021 [US2] Add IndexedDB getMessages with offset/count support in storage.js in src/ToledoMessage.Client/wwwroot/storage.js
- [X] T022 [P] [US2] Add display name caching to ChatHub in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T023 [P] [US2] Add participant list caching (60s TTL) to ChatHub in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T024 [US2] Implement batched delivery acknowledgments in MessagesController.BulkAcknowledgeDelivery in src/ToledoMessage/Controllers/MessagesController.cs
- [X] T025 [US2] Add batched MessagesDelivered handler to SignalRService.cs in src/ToledoMessage.Client/Services/SignalRService.cs
- [X] T026 [US2] Add debouncing (300ms) to EmojiPicker search in src/ToledoMessage.Client/Components/EmojiPicker.razor
- [X] T027 [US2] Add debouncing (200ms) to conversation list filter in ConversationListSidebar.razor in src/ToledoMessage.Client/Components/ConversationListSidebar.razor
- [X] T028 [US2] Add random jitter (±30s) to MessageCleanupHostedService in src/ToledoMessage/Services/MessageCleanupHostedService.cs
- [X] T029 [US2] Add pre-key cleanup to MessageCleanupHostedService in src/ToledoMessage/Services/MessageCleanupHostedService.cs (Deletes all consumed IsUsed=true pre-keys)
- [X] T030 [P] [US2] Optimize user search queries with indexing in src/ToledoMessage/Controllers/UsersController.cs

**Checkpoint**: Performance optimizations complete and independently testable

---

## Phase 5: User Story 3 - Accessible and Responsive Interface (Priority: P2)

**Goal**: Implement accessibility (WCAG 2.1 AA), keyboard navigation, responsive design, date separators, complete localization

**Independent Test**: Automated WCAG 2.1 AA audit passes with zero violations; keyboard-only navigation works throughout

### Implementation for User Story 3

- [X] T031 [P] [US3] Add ARIA labels to all sidebar interactive elements in ConversationListSidebar.razor (already has aria-label, title attributes)
- [X] T032 [P] [US3] Convert sidebar elements to semantic buttons where needed in ConversationListSidebar.razor (already uses semantic buttons)
- [X] T033 [US3] Add keyboard navigation (arrow keys, Enter) to EmojiPicker in src/ToledoMessage.Client/Components/EmojiPicker.razor
- [X] T034 [US3] Make theme cards responsive (reflow on mobile) in Settings.razor (CSS grid already responsive)
- [X] T035 [US3] Make video elements responsive (max-width: 100%) in MessageBubble.razor in src/ToledoMessage.Client/Components/MessageBubble.razor
- [X] T036 [US3] Add date separators between message groups from different days in MessageBubble.razor (already implemented in Chat.razor)
- [X] T037 [P] [US3] Complete Arabic localization for all user-visible strings in SharedResource.ar.resx in src/ToledoMessage.Shared/SharedResource.ar.resx
- [X] T038 [P] [US3] Complete Arabic localization for emoji picker and timer labels

**Checkpoint**: Accessibility and UI improvements complete and independently testable

---

## Phase 6: User Story 4 - Graceful Error Recovery and Offline Resilience (Priority: P2)

**Goal**: Implement offline support: message queue, auto-retry, retry button, offline indicator, file preservation

**Independent Test**: Network simulation confirms queued messages deliver within 5 seconds of reconnection

### Implementation for User Story 4

- [X] T039 [P] [US4] Add offlineQueue JS functions (add, get, update, remove, count) in storage.js in src/ToledoMessage.Client/wwwroot/storage.js
- [X] T040 [US4] Add IsConnected property and OnReconnected event to SignalRService in src/ToledoMessage.Client/Services/SignalRService.cs
- [X] T041 [US4] Implement offline queue flush on reconnection in SignalRService.cs
- [X] T042 [US4] Modify MessageInput to queue messages when offline in src/ToledoMessage.Client/Components/MessageInput.razor
- [X] T043 [US4] Add pending status indicator to queued messages in Chat.razor in src/ToledoMessage.Client/Pages/Chat.razor
- [X] T044 [US4] Add retry button when message list fails to load in Chat.razor
- [X] T045 [US4] Add offline indicator UI in ChatLayout or Chat.razor
- [X] T046 [US4] Preserve file selection on send failure in MessageInput.razor

**Checkpoint**: Offline resilience complete and independently testable

---

## Phase 7: User Story 5 - Voice Recording with Safeguards (Priority: P2)

**Goal**: Implement voice recording with 5-minute limit and countdown display

**Independent Test**: Recording auto-stops at 5 minutes; countdown timer displays correctly

### Implementation for User Story 5

- [X] T047 [US5] Add 5-minute max duration enforcement to voice-recorder.js in src/ToledoMessage.Client/wwwroot/voice-recorder.js
- [X] T048 [US5] Add remaining time countdown display to VoiceRecorder.razor in src/ToledoMessage.Client/Components/VoiceRecorder.razor
- [X] T049 [US5] Add auto-stop at 5-minute limit in VoiceRecorder.razor

**Checkpoint**: Voice recording safeguards complete and independently testable

---

## Phase 8: User Story 6 - In-Conversation Message Search (Priority: P3)

**Goal**: Implement text search within conversation with highlighting

**Independent Test**: Typing search term filters messages and highlights matching text

### Implementation for User Story 6

- [X] T050 [P] [US6] Add search input UI to Chat.razor in src/ToledoMessage.Client/Pages/Chat.razor
- [X] T051 [US6] Implement message filtering by search term in Chat.razor
- [X] T052 [US6] Add text highlighting for matching messages in MessageBubble.razor

**Checkpoint**: In-conversation search complete and independently testable

---

## Phase 9: User Story 7 - Group Administration (Priority: P3)

**Goal**: Implement multi-admin support and admin role transfer

**Independent Test**: Promoting another user to admin succeeds; leaving group when last admin prompts transfer

### Implementation for User Story 7

- [X] T053 [P] [US7] Add admin role check in ChatHub delete operations in src/ToledoMessage/Hubs/ChatHub.cs
- [X] T054 [P] [US7] Add promote/demote admin methods in ConversationsController in src/ToledoMessage/Controllers/ConversationsController.cs
- [X] T055 [US7] Add admin transfer prompt when last admin attempts to leave in ChatHub.cs

**Checkpoint**: Group administration complete and independently testable

---

## Phase 10: User Story 8 - Encryption Hardening and Disappearing Messages (Priority: P2)

**Goal**: Implement signature versioning and wire disappearing messages to actual message flow

**Independent Test**: v1 signatures include 0x01 prefix; v0 signatures still verify; expired messages removed on app open

### Implementation for User Story 8

- [X] T056 [P] [US8] Add version byte (0x01) prefix to hybrid signatures in HybridSigner.cs in src/ToledoMessage.Crypto/
- [X] T057 [P] [US8] Update signature verifier to handle both v0 and v1 formats in HybridSigner.cs
- [X] T058 [US8] Wire MessageExpiryService to actual message flow in src/ToledoMessage.Client/Services/MessageExpiryService.cs
- [X] T059 [US8] Add expired message cleanup on app startup in Chat.razor or App.razor

**Checkpoint**: Encryption hardening complete and independently testable

---

## Phase 11: User Story 9 - Presence Accuracy (Priority: P3)

**Goal**: Implement 90-second presence timeout using SignalR keep-alive

**Independent Test**: Closing browser tab results in user showing offline within 120 seconds

### Implementation for User Story 9

- [X] T060 [US9] Verify SignalR KeepAliveInterval (30s) and ClientTimeoutInterval (90s) configured in Program.cs
- [X] T061 [US9] Verify OnDisconnectedAsync properly calls presence.RemoveConnection in src/ToledoMessage/Hubs/ChatHub.cs
- [ ] T062 [US9] Test presence accuracy with browser tab close simulation (MANUAL: Requires deployed app — open 2 browser tabs, close one, verify other shows "offline" within 90s. Server config verified: KeepAliveInterval=30s, ClientTimeoutInterval=90s)

**Checkpoint**: Presence accuracy complete and independently testable

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Final integration, testing, and deployment

- [X] T063 [P] Run all 231+ existing tests to ensure no regressions
- [X] T064 [P] Verify both English and Arabic localizations complete
- [X] T065 Perform final accessibility audit
- [X] T066 Performance test with 1000+ messages
- [X] T067 Update CLAUDE.md with new .specify entry
- [X] T068 Build and verify application compiles without errors

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - enables most user stories
- **User Stories (Phase 3-11)**: All can begin after Phase 2 foundational tasks complete
  - US1 and US2 are both P1 - can run in parallel
  - US3-US5 are P2 - can run in parallel with US1/US2
  - US6-US9 are P3 - can run in parallel with above
- **Polish (Phase 12)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1) Security**: Starts after T008 (RateLimitService in ChatHub) - No other story dependencies
- **US2 (P1) Performance**: Starts after T004-T005 (compression, SignalR config) - No other story dependencies
- **US3 (P2) Accessibility**: Can start after Phase 2 - No story dependencies
- **US4 (P2) Offline**: Depends on T007 (offlineQueue in storage.js) - Can start after T007
- **US5 (P2) Voice**: Independent - No dependencies
- **US6 (P3) Search**: Independent - No dependencies
- **US7 (P3) Group Admin**: Independent - No dependencies
- **US8 (P2) Encryption**: Independent - No dependencies
- **US9 (P3) Presence**: Depends on T005 (SignalR keep-alive config)

### Within Each User Story

- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- T001-T003 (Setup): Can run in parallel
- T004-T008 (Foundational): Can mostly run in parallel
- US1 and US2 can run in parallel after Phase 2
- All P2/P3 stories can run in parallel after their dependencies met
- T009-T010, T013-T014, T031-T032, T056-T057 are marked [P] for parallel execution

---

## Parallel Example: User Story 1 & 2 (P1 - MVP Core)

```bash
# User Story 1: Security Hardening
Task T009: Add rate limiting to ChatHub.SendMessage
Task T010: Add rate limiting to ChatHub.TypingIndicator
Task T011-T012: Rate limit error handling UI

# User Story 2: Performance
Task T020: Implement virtual scrolling
Task T022-T023: Typing indicator caching
Task T024-T025: Batched delivery ACKs
```

---

## Implementation Strategy

### MVP First (User Stories 1 & 2 - Both P1)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1 (Security)
4. Complete Phase 4: User Story 2 (Performance)
5. **STOP and VALIDATE**: Test security and performance independently
6. Deploy/demo if ready

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add US1 (Security) → Test independently → Deploy/Demo
3. Add US2 (Performance) → Test independently → Deploy/Demo
4. Add US3 (Accessibility) → Test independently → Deploy/Demo
5. Add US4 (Offline) → Test independently → Deploy/Demo
6. Add remaining P2/P3 stories → Test → Deploy
7. Polish phase → Final deployment

---

## Summary

| Metric | Value |
|--------|-------|
| **Total Tasks** | 68 |
| **User Stories** | 9 |
| **P1 Tasks (MVP)** | 30 (US1 + US2) |
| **P2 Tasks** | 22 (US3 + US4 + US5 + US8) |
| **P3 Tasks** | 10 (US6 + US7 + US9) |
| **Polish Tasks** | 6 |
| **Parallelizable Tasks** | ~20 |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
