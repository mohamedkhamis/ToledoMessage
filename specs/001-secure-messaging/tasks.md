# Tasks: UX & Responsive Enhancements

**Input**: Design documents from `/specs/001-secure-messaging/`
**Prerequisites**: plan.md (required), research-ux.md (research decisions)

**Tests**: Manual browser testing only (no automated tests). Testing checklist included in Phase 8.

**Organization**: Tasks are grouped by enhancement area (mapped to plan phases) to enable independent implementation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1=Responsive, US2=CSS Polish, US3=Big Emoji, US4=Home Page, US5=Device Restore)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Analysis & Preparation)

**Purpose**: Verify current state and establish baseline before making changes

- [X] T001 Read and audit current responsive breakpoints in `src/ToledoVault/wwwroot/app.css` — document all existing `@media` queries, their ranges, and which components they cover
- [X] T002 Read `src/ToledoVault.Client/Components/MessageBubble.razor` to understand current message text rendering and identify where to inject emoji-only detection
- [X] T003 Read `src/ToledoVault/Components/Pages/Home.razor` to catalog all technical jargon text and current header button markup
- [X] T004 Read `src/ToledoVault.Client/Pages/Login.razor` lines 100-160 to understand the full device validation flow and identify the exact branch where device-not-found triggers re-registration

---

## Phase 2: Foundational (CSS Infrastructure)

**Purpose**: Add missing CSS breakpoints and infrastructure that multiple stories depend on

**⚠️ CRITICAL**: The 480px and landscape breakpoints are needed by US1, US2, and US3

- [X] T005 [US1] Add small phone breakpoint block (`@media (max-width: 480px)`) to `src/ToledoVault/wwwroot/app.css` with base rules:
  ```css
  @media (max-width: 480px) {
      .msg-textarea { max-height: 100px; }
      .send-btn, .mic-btn { width: 38px; height: 38px; min-width: 38px; }
      .chat-input-row { gap: 4px; }
  }
  ```
  Place this AFTER the existing `@media (max-width: 767px)` block.

- [X] T006 [US1] Add landscape breakpoint block to `src/ToledoVault/wwwroot/app.css`:
  ```css
  @media (max-height: 500px) and (orientation: landscape) {
      .chat-header { padding: 4px 12px; }
      .msg-textarea { max-height: 60px; }
      .emoji-picker-popup { max-height: 200px; }
  }
  ```
  Place this AFTER the 480px block from T005.

**Checkpoint**: Base responsive infrastructure ready — story-specific responsive work can proceed

---

## Phase 3: User Story 1 — Responsive CSS (Priority: P1 — High)

**Goal**: Make all interactive components (emoji picker, forward dialog, voice recorder, input area) fully usable on mobile phones (320px+), tablets (768px+), and landscape orientation.

**Independent Test**: Open DevTools → resize to 320px, 375px, 768px, 1024px, 1280px, and landscape. Every component must fit within viewport with no horizontal overflow.

### Implementation for User Story 1

- [X] T007 [P] [US1] Make emoji picker responsive in `src/ToledoVault/wwwroot/app.css` — add inside the existing `@media (max-width: 767px)` block:
  ```css
  .emoji-picker-popup {
      width: calc(100vw - 24px);
      max-width: 360px;
      max-height: 300px;
  }
  .emoji-grid { grid-template-columns: repeat(7, 1fr); }
  ```
  And add inside the 480px block (from T005):
  ```css
  .emoji-picker-popup { width: 100vw; left: 0; right: 0; border-radius: 12px 12px 0 0; }
  ```

- [X] T008 [P] [US1] Make forward dialog responsive in `src/ToledoVault/wwwroot/app.css` — add inside the existing `@media (max-width: 767px)` block:
  ```css
  .forward-dialog {
      max-width: calc(100vw - 32px);
      max-height: 80vh;
  }
  ```

- [X] T009 [P] [US1] Make voice recorder responsive in `src/ToledoVault/wwwroot/app.css` — add inside the 480px block:
  ```css
  .voice-recorder-bar { min-height: 40px; border-radius: 20px; padding: 6px 10px; }
  .wave-bar { width: 1.5px; }
  .voice-waveform { gap: 1px; }
  .voice-waveform-static { gap: 1px; }
  .wave-bar-static { width: 1.5px; }
  .recording-timer { font-size: 0.8rem; }
  .slide-hint { display: none; }
  ```

**Checkpoint**: All interactive components responsive at all breakpoints

---

## Phase 4: User Story 2 — CSS Review & Polish (Priority: P2 — Medium)

**Goal**: Fix image preview sizing on mobile, polish audio recording bar on narrow screens, and audit CSS for consistency.

**Independent Test**: Send an image in chat → view on 375px → image should not overflow. Record a voice message on 375px → waveform bars should not overflow.

### Implementation for User Story 2

- [X] T010 [P] [US2] Fix image preview mobile sizing in `src/ToledoVault/wwwroot/app.css` — add inside the existing `@media (max-width: 767px)` block:
  ```css
  .message-image { max-height: 280px; }
  .lightbox-image { max-width: 95vw; max-height: 85vh; }
  .lightbox-close { top: 8px; right: 8px; }
  .lightbox-nav { width: 40px; height: 60px; }
  ```

- [X] T011 [P] [US2] Polish audio recording bar for narrow screens in `src/ToledoVault/wwwroot/app.css` — add waveform overflow prevention. Inside the 480px block, add:
  ```css
  .voice-waveform, .voice-waveform-static { overflow: hidden; max-width: calc(100vw - 180px); }
  .voice-preview-bar { gap: 6px; }
  .voice-preview-duration { font-size: 0.75rem; min-width: 36px; }
  ```

- [X] T012 [US2] CSS audit pass on `src/ToledoVault/wwwroot/app.css`:
  - Verify all theme CSS variables from `themes.css` are used consistently (check `--msg-bubble-radius`, `--input-radius`, `--sidebar-width`, `--chat-bg-image`, `--msg-font-size`, `--input-padding`)
  - Check z-index values on mobile overlays (`.emoji-picker-popup`, `.lightbox-overlay`, `.forward-dialog`, `.context-menu`) don't conflict — emoji picker and context menu should be below lightbox
  - Verify no hardcoded color values that should use CSS variables

**Checkpoint**: Image preview and audio bar polished on all viewports

---

## Phase 5: User Story 3 — Big Emoji (Priority: P1 — High)

**Goal**: Messages containing only 1-3 emoji characters (no text) render at ~2.8x size with transparent bubble background, like WhatsApp.

**Independent Test**: Send "😀" → renders large (~2.8em). Send "😀😂" → large. Send "😀😂🎉" → large. Send "😀😂🎉🔥" → normal size. Send "hello 😀" → normal size.

### Implementation for User Story 3

- [X] T013 [US3] Add `IsEmojiOnly` helper method in `src/ToledoVault.Client/Components/MessageBubble.razor` `@code` block:
  ```csharp
  private static bool IsEmojiOnly(string? text)
  {
      if (string.IsNullOrWhiteSpace(text)) return false;
      var trimmed = text.Trim();
      // Match 1-3 emoji characters (including compound emoji like flags, skin tones)
      return System.Text.RegularExpressions.Regex.IsMatch(
          trimmed,
          @"^(\p{So}|\p{Cs}{2}|\u200d|\ufe0f|\u20e3|[\u2600-\u27bf]|[\ud83c-\ud83f][\ud800-\udfff]){1,3}$")
          && trimmed.Length <= 14; // Safety cap for compound emoji
  }
  ```

- [X] T014 [US3] Apply emoji-only CSS class in `src/ToledoVault.Client/Components/MessageBubble.razor` — find the `<div>` that wraps the message text content and add conditional class:
  ```razor
  <div class="message-text @(IsEmojiOnly(messageText) ? "emoji-only" : "")">
  ```
  Where `messageText` is the variable holding the decrypted/plain text of the message. Identify the exact variable name by reading the existing rendering code.

- [X] T015 [US3] Add big emoji CSS styles in `src/ToledoVault/wwwroot/app.css` — add near the `.message-text` styles:
  ```css
  .message-text.emoji-only {
      font-size: 2.8em;
      line-height: 1.2;
      padding: 4px 0;
  }
  .message-bubble:has(.emoji-only) {
      background: transparent;
      box-shadow: none;
      padding: 4px 8px;
  }
  ```

**Checkpoint**: Big emoji rendering works for 1-3 emoji messages, normal rendering for everything else

---

## Phase 6: User Story 4 — Simplify Home Page (Priority: P1 — High)

**Goal**: Replace all technical/cryptographic jargon on the home page with user-friendly language. Fix inconsistent header button styles.

**Independent Test**: Load home page → no visible text containing "ML-KEM", "X25519", "AES", "Double Ratchet", "Zero-Knowledge", "Quantum". "Sign In" button styled consistently with "Get Started".

### Implementation for User Story 4

- [ ] T016 [US4] Replace hero section content in `src/ToledoVault/Components/Pages/Home.razor`:
  - Hero badge: "Quantum-Resistant Messaging" → **"Secure Messaging"**
  - Hero title: "Secure Messaging for Tomorrow's Threat" → **"Private Messaging Made Simple"**
  - Hero description: technical crypto description → **"Send messages, photos, and voice notes — all protected with the strongest encryption. Only you and the people you talk to can read them."**

- [ ] T017 [P] [US4] Replace stat cards in `src/ToledoVault/Components/Pages/Home.razor`:
  - Stat 1: "256-bit / AES Encryption" → **"End-to-End / Encrypted"**
  - Stat 2: "Hybrid PQ / Key Exchange" → **"Future-Proof / Protection"**
  - Stat 3: "Zero-Knowledge / Server Architecture" → **"Private / By Design"**

- [ ] T018 [P] [US4] Replace feature descriptions in `src/ToledoVault/Components/Pages/Home.razor`:
  - Feature 1 title: "Post-Quantum Safe" → **"Always Protected"**
  - Feature 1 desc: "ML-KEM-768 + X25519..." → **"Your messages are secured with cutting-edge encryption that protects against both today's and tomorrow's threats"**
  - Feature 2 desc: "Signal Protocol with Double Ratchet..." → **"Only you and the person you're talking to can read your messages. Not even we can."**
  - Feature 3 desc: "All cryptography runs in your browser..." → **"Everything is encrypted on your device before it's sent. We never see your messages or personal data."**

- [ ] T019 [US4] Fix header "Sign In" button style in `src/ToledoVault/Components/Pages/Home.razor` — change:
  ```html
  <!-- Before -->
  <a href="/login">Sign In</a>
  <!-- After -->
  <a href="/login" class="btn-nav-outline">Sign In</a>
  ```

- [ ] T020 [US4] Add `.btn-nav-outline` CSS class in `src/ToledoVault/wwwroot/app.css` near the existing `.btn-nav` styles:
  ```css
  .btn-nav-outline {
      color: #f8fafc !important;
      padding: 8px 20px;
      border-radius: 6px;
      font-weight: 600;
      border: 1px solid rgba(255, 255, 255, 0.3);
      text-decoration: none;
      transition: background 0.2s;
  }
  .btn-nav-outline:hover {
      background: rgba(255, 255, 255, 0.1) !important;
  }
  ```

**Checkpoint**: Home page is user-friendly with no technical jargon; both header buttons styled consistently

---

## Phase 7: User Story 5 — Fix Device Re-Registration (Priority: CRITICAL)

**Goal**: When a user logs in from the same browser after server redeployment (database reset), the client reuses existing identity keys from localStorage instead of generating new ones. This preserves the device identity.

**Independent Test**:
1. Login on browser A → register device → redeploy server (DB reset) → login again → device restored with same identity keys (check `local.identityKeyPublicClassical` unchanged in localStorage)
2. Login on fresh browser (no localStorage) → normal new device registration
3. Login after clearing localStorage → normal new device registration

### Implementation for User Story 5

- [ ] T021 [US5] Read `src/ToledoVault.Client/Services/KeyGenerationService.cs` to understand the existing `RestoreKeysAndBuildRequest` method signature and confirm it reads identity keys from localStorage and generates fresh pre-keys/OTPKs

- [ ] T022 [US5] Modify the device validation flow in `src/ToledoVault.Client/Pages/Login.razor` — find the branch where `deviceId` is not found in the server device list (around lines 133-145 per plan). Replace the "generate new keys" path with a check for existing identity keys:
  ```csharp
  // When stored deviceId not found on server:
  var existingIdentity = await Storage.GetAsync("local.identityKeyPublicClassical");
  if (existingIdentity is not null)
  {
      // Device restore: reuse identity keys, generate fresh pre-keys
      var deviceName = // existing device name logic
      var request = await KeyGen.RestoreKeysAndBuildRequest(deviceName);
      var restoreResponse = await Http.PostAsJsonAsync("/api/devices", request);
      if (restoreResponse.IsSuccessStatusCode)
      {
          var deviceResult = await restoreResponse.Content
              .ReadFromJsonAsync<DeviceRegistrationResponse>();
          await Storage.SetAsync("local.deviceId",
              System.Text.Encoding.UTF8.GetBytes(deviceResult.DeviceId.ToString()));
          // Continue with normal post-registration flow
      }
  }
  else
  {
      // Truly new device: full key generation (existing behavior)
      var request = await KeyGen.GenerateAndStoreKeys(deviceName);
      // ... existing registration code
  }
  ```
  **Important**: Adapt this to match the exact variable names and flow patterns already used in Login.razor. The `RestoreKeysAndBuildRequest` method should already exist from the SharedKeys feature — verify its signature in T021.

- [ ] T023 [US5] Test the device restore flow by verifying that `POST /api/devices` in `src/ToledoVault/Controllers/DevicesController.cs` accepts any valid public keys (no change expected — just confirm the endpoint works for restored keys with a different deviceId)

**Checkpoint**: Same browser preserves device identity across server redeployments

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final verification across all viewports and stories

- [ ] T024 Manual responsive testing at all target viewports per plan testing checklist:
  - 320px (iPhone SE): chat, sidebar, input, emoji picker, voice recorder all fit
  - 375px (iPhone 12 mini): same checks
  - 390px (iPhone 14): same checks
  - 768px (iPad portrait): two-panel layout works
  - 1024px (iPad landscape): same
  - 1280px+ (desktop): no regressions
  - Landscape mode on each mobile size: input area usable, header not too tall

- [ ] T025 Manual big emoji testing:
  - Single emoji "😀" → renders large
  - Two emoji "😀😂" → renders large
  - Three emoji "😀😂🎉" → renders large
  - Four+ emoji → normal size
  - Emoji + text "hello 😀" → normal size
  - Flag emoji "🇺🇸" → renders large (compound emoji)
  - Skin tone emoji "👋🏽" → renders large

- [ ] T026 Manual home page testing:
  - No technical jargon visible (search for "ML-KEM", "X25519", "AES", "Double Ratchet", "Zero-Knowledge")
  - "Sign In" button styled with outline, "Get Started" solid
  - All text reads naturally for non-technical users

- [ ] T027 Manual device restore testing:
  - Login → register → redeploy → login → same identity keys preserved
  - Fresh browser → normal new device registration
  - Clear localStorage → normal new device registration

- [ ] T028 Run `dotnet build` on full solution to verify no compilation errors from MessageBubble.razor changes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup/Analysis)**: No dependencies — start immediately
- **Phase 2 (CSS Infrastructure)**: Depends on Phase 1 — creates breakpoint blocks needed by US1, US2
- **Phase 3 (Responsive — US1)**: Depends on Phase 2 (needs 480px and landscape blocks)
- **Phase 4 (CSS Polish — US2)**: Depends on Phase 2 (needs 480px block)
- **Phase 5 (Big Emoji — US3)**: Independent — no CSS breakpoint dependency, only needs MessageBubble.razor + app.css
- **Phase 6 (Home Page — US4)**: Independent — only touches Home.razor + app.css (different CSS area)
- **Phase 7 (Device Restore — US5)**: Independent — only touches Login.razor + KeyGenerationService.cs
- **Phase 8 (Polish)**: Depends on ALL previous phases

### Parallel Opportunities

After Phase 2 completes:
- **US1 (T007-T009)**, **US2 (T010-T012)** can run in parallel (different CSS sections, but same file — coordinate carefully)
- **US3 (T013-T015)** can run in parallel with US1/US2 (different files: MessageBubble.razor)
- **US4 (T016-T020)** can run in parallel with US1/US2/US3 (different file: Home.razor, different CSS section)
- **US5 (T021-T023)** can run in parallel with ALL other stories (completely different files: Login.razor, KeyGenerationService.cs)

### Within Each User Story

- Read/analysis tasks before modification tasks
- Razor changes before corresponding CSS changes (or parallel if independent)
- CSS additions in app.css should be grouped logically near related existing styles

---

## Parallel Example: Maximum Parallelism After Phase 2

```text
# These can all run simultaneously:
Agent 1: T007 + T008 + T009 (US1 — emoji picker, forward dialog, voice recorder responsive CSS)
Agent 2: T013 + T014 (US3 — Big emoji detection in MessageBubble.razor)
Agent 3: T016 + T017 + T018 + T019 (US4 — Home page content replacement)
Agent 4: T021 + T022 (US5 — Device restore in Login.razor)

# Then sequentially:
T015 (US3 CSS) + T020 (US4 CSS) + T010 + T011 (US2 CSS) — all app.css changes
T012 (CSS audit) — after all CSS changes are in
T024-T028 (Testing) — after all implementation complete
```

---

## Implementation Strategy

### MVP First (Critical Fix + High Priority)

1. Complete Phase 1: Setup (T001-T004)
2. Complete Phase 7: US5 — Device Restore (T021-T023) — **CRITICAL bug fix, deploy immediately**
3. Complete Phase 2: CSS Infrastructure (T005-T006)
4. Complete Phase 3: US1 — Responsive CSS (T007-T009)
5. Complete Phase 5: US3 — Big Emoji (T013-T015)
6. **STOP and VALIDATE**: Test responsive + emoji on all viewports

### Incremental Delivery

1. **Deploy 1**: Device restore fix (US5) — eliminates critical re-registration bug
2. **Deploy 2**: Responsive CSS (US1) + Big emoji (US3) — major UX improvements
3. **Deploy 3**: Home page simplification (US4) + CSS polish (US2) — polish and content
4. **Deploy 4**: Full testing pass (Phase 8)

---

## Summary

| # | Task Range | Story | Files | Priority |
|---|-----------|-------|-------|----------|
| T001-T004 | Setup | — | Multiple (read-only) | — |
| T005-T006 | CSS Infrastructure | US1 | `app.css` | High |
| T007-T009 | Responsive CSS | US1 | `app.css` | High |
| T010-T012 | CSS Polish | US2 | `app.css` | Medium |
| T013-T015 | Big Emoji | US3 | `MessageBubble.razor`, `app.css` | High |
| T016-T020 | Home Page | US4 | `Home.razor`, `app.css` | High |
| T021-T023 | Device Restore | US5 | `Login.razor`, `KeyGenerationService.cs` | **Critical** |
| T024-T028 | Testing | — | Manual | High |

**Total Tasks**: 28
**Critical**: 3 tasks (US5 — Device Restore)
**High**: 16 tasks (US1 + US3 + US4 + Testing)
**Medium**: 5 tasks (US2 — CSS Polish)
**Analysis/Setup**: 4 tasks
