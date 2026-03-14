# Tasks: UI Polish & Visual Enhancement

**Input**: Design documents from `/specs/007-ui-polish/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/
**Tests**: No dedicated test tasks — this is a CSS/markup-only feature verified via visual inspection across 8 themes.
**Organization**: Tasks are grouped by user story (7 stories from spec.md, priorities P1-P3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Key File Reference

| File | Purpose |
|------|---------|
| `src/ToledoVault/wwwroot/app.css` | Main stylesheet (~4050 lines) — bulk of CSS changes |
| `src/ToledoVault/wwwroot/themes.css` | Theme CSS custom properties (8 themes) |
| `src/ToledoVault.Client/Components/MessageBubble.razor` | Message bubble component (464 lines) |
| `src/ToledoVault.Client/Components/DeliveryStatus.razor` | Delivery status icon component |
| `src/ToledoVault.Client/Components/LinkPreview.razor` | Link preview card component |
| `src/ToledoVault.Client/Pages/Chat.razor` | Main chat page (2206 lines) — unread divider, context menu, search, toasts |
| `src/ToledoVault.Client/Pages/Settings.razor` | Settings page |

## Current Theme Variables Available

All 8 themes define these CSS custom properties in `themes.css`:
- `--bg-primary`, `--bg-secondary`, `--bg-chat`
- `--text-primary`, `--text-secondary`
- `--accent`, `--accent-hover`, `--accent-text`, `--accent-light`
- `--border`, `--shadow`, `--shadow-md`
- `--msg-sent-bg`, `--msg-sent-text`, `--msg-received-bg`, `--msg-received-text`
- `--nav-bg`, `--nav-text`
- `--input-bg`, `--input-border`
- `--card-bg`, `--hover-bg`
- Structural: `--msg-bubble-radius`, `--input-radius`, `--sidebar-width`, `--avatar-size`, `--msg-font-size`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add new CSS custom properties to the theme system that will be consumed by multiple user stories. This phase MUST complete before any user story work begins.

- [ ] T001 Add new CSS custom properties `--waveform-played`, `--waveform-unplayed`, `--skeleton-base`, `--skeleton-shimmer` to ALL 8 theme definitions in `src/ToledoVault/wwwroot/themes.css`

  **Details**: Each theme block is defined via `:root[data-theme="..."]` selectors. Add 4 new properties to each of the 8 themes. Use theme-appropriate colors:

  | Theme | `--waveform-played` | `--waveform-unplayed` | `--skeleton-base` | `--skeleton-shimmer` |
  |-------|--------------------|-----------------------|-------------------|---------------------|
  | Default (`:root` fallback) | `var(--accent)` | `var(--border)` | `var(--hover-bg)` | `var(--bg-secondary)` |
  | Default Dark | `#5c9ce6` | `#2a3a52` | `#1e2a3e` | `#253650` |
  | WhatsApp | `#25d366` | `#c5c5c5` | `#e9e9e9` | `#f5f5f5` |
  | WhatsApp Dark | `#25d366` | `#374045` | `#1a2329` | `#222e35` |
  | Telegram | `#2aabee` | `#c5c5c5` | `#e8e8ea` | `#f4f4f5` |
  | Telegram Dark | `#2aabee` | `#2a3a4a` | `#141e28` | `#1a2735` |
  | Signal | `#3a76f0` | `#c5c5c5` | `#e8e8e8` | `#f0f0f0` |
  | Signal Dark | `#3a76f0` | `#383a3d` | `#212224` | `#2a2b2e` |

  **File**: `src/ToledoVault/wwwroot/themes.css`
  **Lines affected**: Each theme block (lines ~18-51, 53-85, 87-120, 122-154, 156-188, 190-223, 225-258, and root fallback)

  **Verification**: After this task, open browser DevTools → Elements → `:root` computed styles → confirm all 4 new variables resolve for each theme when switched in Settings.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add foundational CSS `@keyframes` animations and `prefers-reduced-motion` rules that multiple user stories depend on.

**CRITICAL**: No user story work (Phase 3+) should begin until this phase is complete.

- [ ] T002 Add the `@keyframes message-slide-in-left` and `@keyframes message-slide-in-right` animation definitions in `src/ToledoVault/wwwroot/app.css`

  **Details**: Insert these keyframe definitions near the existing animation block (around line 2010-2035 where `fadeIn`, `slideInRight`, `slideInLeft`, `pulse`, `badgePop` are defined). These are NEW keyframes distinct from the existing `slideInLeft`/`slideInRight` which are generic slide-ins. The message variants include a slight vertical shift for a more natural feel:

  ```css
  @keyframes message-slide-in-left {
      from { opacity: 0; transform: translateX(-20px) translateY(6px); }
      to { opacity: 1; transform: translateX(0) translateY(0); }
  }
  @keyframes message-slide-in-right {
      from { opacity: 0; transform: translateX(20px) translateY(6px); }
      to { opacity: 1; transform: translateX(0) translateY(0); }
  }
  ```

  Duration: 200ms, easing: ease-out (applied via the `.message-row` class in a later task).

  **File**: `src/ToledoVault/wwwroot/app.css` (insert near line ~2035)

- [ ] T003 [P] Add the `@keyframes reaction-pop` animation definition in `src/ToledoVault/wwwroot/app.css`

  **Details**: Insert near the existing `badgePop` keyframe (~line 2030):

  ```css
  @keyframes reaction-pop {
      0% { transform: scale(1); }
      50% { transform: scale(1.25); }
      100% { transform: scale(1); }
  }
  ```

  This will be applied to `.reaction-badge` on click in a later task (US5).

  **File**: `src/ToledoVault/wwwroot/app.css` (insert near line ~2033)

- [ ] T004 [P] Add the `@keyframes context-menu-enter` animation definition in `src/ToledoVault/wwwroot/app.css`

  **Details**: Insert near existing animation keyframes:

  ```css
  @keyframes context-menu-enter {
      from { opacity: 0; transform: scale(0.95); }
      to { opacity: 1; transform: scale(1); }
  }
  ```

  Duration: 150ms, easing: ease-out (applied to `.context-menu` in a later task).

  **File**: `src/ToledoVault/wwwroot/app.css` (insert near line ~2035)

- [ ] T005 Add a comprehensive `@media (prefers-reduced-motion: reduce)` block at the END of `src/ToledoVault/wwwroot/app.css`

  **Details**: This single media query block suppresses ALL animations added by this feature AND existing animations. Insert at the very end of app.css (after all other rules):

  ```css
  @media (prefers-reduced-motion: reduce) {
      .message-row,
      .toast,
      .context-menu,
      .reaction-badge,
      .skeleton-line,
      .skeleton-avatar,
      .skeleton-conversation-item,
      .skeleton-message {
          animation: none !important;
          transition: none !important;
      }
  }
  ```

  **Why at the end**: CSS specificity — `!important` in a media query at the end ensures it overrides all animation declarations regardless of source order.

  **File**: `src/ToledoVault/wwwroot/app.css` (append at end of file)

  **Verification**: In Chrome DevTools → Rendering tab → check "Emulate CSS media feature prefers-reduced-motion" → set to "reduce" → confirm no animations play.

**Checkpoint**: Foundation ready — all keyframe animations defined, `prefers-reduced-motion` handled. User story implementation can begin.

---

## Phase 3: User Story 1 — Theme-Consistent Colors Everywhere (Priority: P1) MVP

**Goal**: Replace ALL hardcoded color values with CSS custom properties so every visual element adapts when themes are switched. This is the most visible defect — users who select a dark theme see jarring light-colored elements.

**Independent Test**: Switch through all 8 themes (Default, Default Dark, WhatsApp, WhatsApp Dark, Telegram, Telegram Dark, Signal, Signal Dark) in Settings and verify every visible element uses theme-appropriate colors with no hardcoded values standing out.

**Acceptance Criteria** (from spec.md):
1. PDF preview background uses theme's dark background color in dark themes (not hardcoded light gray)
2. Audio waveform played bars use the theme's accent/waveform color (not hardcoded `#00a884` WhatsApp green)
3. Contact avatar background colors harmonize with dark theme palettes
4. Video play button overlay adapts to the theme

### Implementation for User Story 1

- [ ] T006 [US1] Replace hardcoded `#00a884` in audio waveform "mine" styles with `var(--waveform-played)` in `src/ToledoVault/wwwroot/app.css`

  **Details**: There are 3 occurrences of hardcoded WhatsApp green `#00a884` in audio-related CSS:

  1. **Line ~749**: `.audio-avatar.mine` — background uses `rgba(0, 168, 132, 0.15)`. Replace with a CSS custom property or use `color-mix()` / an opacity wrapper on `var(--waveform-played)`. Simplest approach: replace with `var(--accent-light)` which already exists in all themes.
     ```css
     /* BEFORE */ .audio-avatar.mine { background: rgba(0, 168, 132, 0.15); color: #00a884; }
     /* AFTER  */ .audio-avatar.mine { background: var(--accent-light); color: var(--waveform-played); }
     ```

  2. **Line ~1030**: `.message-audio .audio-accent-color` (if exists) or similar — replace `#00a884` with `var(--waveform-played)`.

  3. **Line ~1073**: `.message-bubble.mine .aw-bar.played` — currently `background: #00a884`. Replace with:
     ```css
     .message-bubble.mine .aw-bar.played { background: var(--waveform-played); }
     ```

  **Search pattern**: Search for `00a884` in app.css to find all instances.

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Switch to Signal theme → play an audio message → waveform bars should be Signal blue (`#3a76f0`), not WhatsApp green.

- [ ] T007 [P] [US1] Replace hardcoded PDF preview background color with theme variable in `src/ToledoVault/wwwroot/app.css`

  **Details**: The PDF preview frame wrapper (`.pdf-preview-frame-wrapper`, line ~1125) may have a hardcoded background or inherit a non-themed color. Ensure it uses `var(--bg-secondary)` for the frame background so dark themes show a dark PDF preview area:

  ```css
  .pdf-preview-frame-wrapper {
      background: var(--bg-secondary);
      /* ... rest of existing styles */
  }
  ```

  Also check `.message-pdf-preview` and `.pdf-preview-frame` for any hardcoded backgrounds.

  The file download button (`.message-file-download-btn`) currently uses `#fff` for text color (lines ~1156, 1529). Replace with `var(--accent-text)`:
  ```css
  .message-file-download-btn { color: var(--accent-text); }
  ```

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Switch to WhatsApp Dark theme → open a PDF message → preview area should have dark background, not white/light gray.

- [ ] T008 [P] [US1] Replace hardcoded video play button overlay colors with theme variables in `src/ToledoVault/wwwroot/app.css`

  **Details**: The video play button (`.message-video-play-btn`, lines ~951-979) uses:
  - `background: rgba(0, 0, 0, 0.55)` — hardcoded dark overlay
  - Hover: `rgba(0, 0, 0, 0.7)` — hardcoded darker overlay
  - Triangle: white border (`transparent transparent transparent #fff`) via `::before`

  For theme awareness, these can remain as semi-transparent overlays (they work on both light/dark) BUT the triangle color should use `var(--accent-text)` or `#fff` (white is acceptable for play buttons universally). The background overlay is acceptable as-is since it provides contrast against any video thumbnail.

  **Decision**: Keep the semi-transparent black overlay (it's a video player convention, not a theme color). But verify it looks good on all 8 themes. If any theme has issues, adjust.

  **Minimal change needed**: This is already theme-compatible. Mark as verified/no-change-needed if inspection confirms.

  **File**: `src/ToledoVault/wwwroot/app.css`

- [ ] T009 [P] [US1] Audit and replace remaining hardcoded color values in alert/notification styles in `src/ToledoVault/wwwroot/app.css`

  **Details**: Several alert/notification styles use hardcoded colors that should use CSS variables:

  1. **Line ~162**: `.error-badge` uses `#c82333`. Replace with `var(--danger)`.
  2. **Line ~237-238**: Error alert uses `#fce4ec` bg and `#c62828` text. These are semantic colors that could use theme variables or remain as fixed semantic colors (red for error is universal). **Decision**: Keep as-is — these are intentionally semantic (error = red regardless of theme).
  3. **Line ~242-243**: Success alert uses `#e8f5e9` bg and `#2e7d32` text. Same reasoning — keep as semantic.

  **Only change**: Line ~162 `.error-badge` background from `#c82333` to `var(--danger)`.

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Open app in each theme → trigger an error state → badge should use theme's danger color.

- [ ] T010 [US1] Audit HD toggle button for hardcoded colors in `src/ToledoVault/wwwroot/app.css`

  **Details**: The HD toggle (`.hd-toggle-btn`, lines ~736-765) already uses `var(--text-secondary)` for inactive and `var(--accent)` for active states. The active state uses `color: #fff` for text.

  Replace `color: #fff` with `var(--accent-text)` to be theme-consistent:
  ```css
  .hd-toggle-btn.active { color: var(--accent-text); }
  ```

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Switch themes → toggle HD button → active text should always be readable against the accent background.

**Checkpoint**: All hardcoded colors replaced with theme variables. Switch through all 8 themes and verify no jarring out-of-place colors remain.

---

## Phase 4: User Story 2 — Readable Timestamps on Mobile (Priority: P1)

**Goal**: Make message timestamps always readable without hover interaction, especially on mobile where hover doesn't exist.

**Independent Test**: Open the chat on a mobile viewport (Chrome DevTools device toolbar, e.g., iPhone 14 Pro) and verify timestamps are clearly visible at full or near-full opacity without any interaction.

**Acceptance Criteria** (from spec.md):
1. Timestamps visible at full or near-full opacity on mobile without requiring hover
2. On desktop, timestamps may optionally increase prominence on hover but must already be readable

### Implementation for User Story 2

- [ ] T011 [US2] Update timestamp opacity from 0.7 to 0.85 baseline and remove hover-only visibility dependency in `src/ToledoVault/wwwroot/app.css`

  **Details**: Currently the timestamp styling has:
  - Base opacity: 0.7 (lines ~564-572 and ~1571-1583 in `.message-timestamp` / `.msg-meta`)
  - Hover state: opacity increases to 1.0 on `.message-bubble:hover`

  **Changes**:
  1. Increase base opacity from `0.7` to `0.85` — readable without hover but still subtle enough not to dominate:
     ```css
     .msg-meta { opacity: 0.85; }
     ```

  2. Keep the hover enhancement to `1.0` on desktop (it's a nice polish touch):
     ```css
     .message-bubble:hover .msg-meta { opacity: 1; }
     ```

  3. For mobile viewports, set opacity to `1.0` always (no hover exists):
     ```css
     @media (hover: none) {
         .msg-meta { opacity: 1; }
     }
     ```

  The `@media (hover: none)` query targets touch-only devices (phones, tablets) where hover is not available.

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**:
  - Desktop: Timestamps visible at 0.85 opacity, increase to 1.0 on bubble hover
  - Mobile (DevTools device toolbar): Timestamps at full opacity, no hover needed

**Checkpoint**: Timestamps readable on all devices without interaction.

---

## Phase 5: User Story 3 — Micro-Animations for Polish (Priority: P2)

**Goal**: Add smooth, performant micro-animations to key UI interactions: message slide-in, toast transitions, skeleton shimmer, reaction pop, and context menu fade-in. All CSS-only, all under 300ms, all respecting `prefers-reduced-motion`.

**Independent Test**: Trigger each animation scenario (receive a message, show a toast, load a chat, react to a message, open context menu) and verify smooth animations with no jank. Then enable `prefers-reduced-motion: reduce` in DevTools and verify all animations are suppressed.

**Acceptance Criteria** (from spec.md):
1. New messages slide in from the appropriate side (left for received, right for sent) under 200ms
2. Toast notifications slide/fade in from top-right with smooth entry and auto-dismiss with fade-out
3. Skeleton loaders use shimmer (gradient sweep) instead of basic pulse
4. Reaction badges show a brief pop/scale animation when tapped
5. Context menus fade in with a slight scale-up animation

### Implementation for User Story 3

- [ ] T012 [US3] Apply message slide-in animation to `.message-row` in `src/ToledoVault/wwwroot/app.css`

  **Details**: Add animation to new messages using the keyframes defined in T002. Messages should slide in from their respective side:

  ```css
  .message-row {
      animation: message-slide-in-left 200ms ease-out both;
  }
  .message-row.mine {
      animation: message-slide-in-right 200ms ease-out both;
  }
  ```

  **Important**: The `both` fill mode ensures the element is visible after animation completes. Without it, the element would snap back to `opacity: 0`.

  **Caveat**: This will animate ALL message rows on initial load (which could look weird for a full chat history). To limit animation to newly arrived messages only, the Blazor component should add a CSS class like `new-message` that triggers the animation. Check if `Chat.razor` already has such a mechanism (e.g., `_newlyReceivedIds` set or similar). If not, a Blazor-side change is needed.

  **Blazor-side change** (if needed): In `src/ToledoVault.Client/Pages/Chat.razor`, track newly received message IDs and add a `new-message` CSS class to their `.message-row` div. Then scope the animation to `.message-row.new-message` only.

  **Files**:
  - `src/ToledoVault/wwwroot/app.css` — CSS animation rule
  - `src/ToledoVault.Client/Pages/Chat.razor` — Add `new-message` class to newly received messages (optional, but recommended)

  **Verification**: Send a message → it slides in from the right. Receive a message → it slides in from the left. Existing messages on chat load should NOT animate.

- [ ] T013 [P] [US3] Update skeleton loader animation from basic pulse to shimmer gradient in `src/ToledoVault/wwwroot/app.css`

  **Details**: The skeleton loader already has a `shimmer` keyframe defined (line ~2950) and uses it. However, verify that ALL skeleton elements use shimmer instead of pulse:

  Currently at lines ~2936-2999:
  - `.skeleton-line` uses `background: linear-gradient(90deg, var(--hover-bg) 25%, var(--border) 50%, var(--hover-bg) 75%)`
  - Animation: `shimmer 1.5s ease-in-out infinite`
  - Background-size: `200% 100%`

  **Update**: Replace `var(--hover-bg)` and `var(--border)` in the gradient with the new theme variables:
  ```css
  .skeleton-line,
  .skeleton-avatar,
  .skeleton-message {
      background: linear-gradient(90deg, var(--skeleton-base) 25%, var(--skeleton-shimmer) 50%, var(--skeleton-base) 75%);
      background-size: 200% 100%;
      animation: shimmer 1.5s ease-in-out infinite;
  }
  ```

  Also ensure ALL skeleton elements (`.skeleton-avatar`, `.skeleton-conversation-item`, `.skeleton-message`) use the shimmer gradient, not just `.skeleton-line`.

  **File**: `src/ToledoVault/wwwroot/app.css` (lines ~2936-2999)

  **Verification**: Navigate to a chat that takes a moment to load → skeleton should show a sweeping shimmer gradient, not a pulsing opacity change.

- [ ] T014 [P] [US3] Apply reaction pop animation to `.reaction-badge` on interaction in `src/ToledoVault/wwwroot/app.css`

  **Details**: When a user clicks a reaction badge, it should briefly scale up and back down. The keyframe `reaction-pop` was defined in T003.

  Add to the existing `.reaction-badge` styles:

  ```css
  .reaction-badge:active {
      animation: reaction-pop 200ms ease-out;
  }
  ```

  The `:active` pseudo-class triggers on click/tap, which is appropriate for a brief pop effect.

  **Alternative approach**: If `:active` doesn't feel right (it's very brief on click), use a Blazor-side approach: add a temporary `popping` CSS class on click via `@onclick`, remove after 200ms with `Task.Delay`. Then:
  ```css
  .reaction-badge.popping { animation: reaction-pop 200ms ease-out; }
  ```

  **Recommended**: Start with `:active` — it's simpler and CSS-only.

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Click a reaction badge → it should briefly enlarge then return to normal size.

- [ ] T015 [P] [US3] Apply context menu fade-in animation to `.context-menu` in `src/ToledoVault/wwwroot/app.css`

  **Details**: The context menu (`.context-menu`, lines ~3445-3500 in app.css) currently appears instantly. Apply the `context-menu-enter` keyframe defined in T004:

  ```css
  .context-menu {
      animation: context-menu-enter 150ms ease-out;
      transform-origin: top right; /* or top left depending on position */
  }
  ```

  The `transform-origin` should be set to the corner closest to where the menu appears (usually top-right for header menus, or near the click point for message context menus).

  **File**: `src/ToledoVault/wwwroot/app.css` (modify existing `.context-menu` block around line ~3450)

  **Verification**: Right-click (or long-press on mobile) a message → context menu should fade in with a slight scale-up, not appear instantly.

- [ ] T016 [US3] Verify toast notification animations are working correctly in `src/ToledoVault/wwwroot/app.css`

  **Details**: Toast animations already exist in app.css (lines ~3045-3058):
  - `toast-slide-in`: translateX 100% → 0, 0.3s ease-out
  - `toast-slide-out`: translateX 0 → 100%, 0.3s ease-in forwards

  These are applied to `.toast` elements. **Verify** they are actually being used:
  1. Check if the toast component applies these CSS classes/animations
  2. If toast appears without animation, ensure the `.toast` class includes:
     ```css
     .toast { animation: toast-slide-in 0.3s ease-out; }
     .toast.removing { animation: toast-slide-out 0.3s ease-in forwards; }
     ```
  3. Check if the toast component adds a `removing` class before removal (for exit animation)

  If the toast component doesn't support exit animation, add a `removing` CSS class mechanism in the Blazor toast component.

  **Files**:
  - `src/ToledoVault/wwwroot/app.css` — Verify/fix animation rules
  - Toast component (search for `toast` in `src/ToledoVault.Client/Components/`) — Verify/fix class toggling

  **Verification**: Trigger a toast (e.g., copy a message) → toast should slide in from right. After timeout → toast should slide out to right.

**Checkpoint**: All 5 micro-animations working. Test with `prefers-reduced-motion: reduce` enabled → all should be suppressed (handled by T005).

---

## Phase 6: User Story 4 — Missing Component Styling (Priority: P2)

**Goal**: Ensure all UI components that were previously unstyled or partially styled render with proper, theme-consistent styling.

**Independent Test**: Trigger each component (forward a message, search in chat, send a link, clear chat) and verify they render with proper styling matching the current theme.

**Acceptance Criteria** (from spec.md):
1. Forward dialog shows a styled overlay with conversation list, search input, proper spacing
2. Search counter shows "X of Y" styled in the search bar with highlighted matches
3. Link previews show in a styled card with proper borders, padding, theme colors
4. Clear chat dialog shows styled buttons in a themed dialog box

### Implementation for User Story 4

- [ ] T017 [US4] Review and enhance forward dialog styling in `src/ToledoVault/wwwroot/app.css`

  **Details**: The forward dialog already has comprehensive CSS (lines ~3977-4042):
  - `.forward-dialog-overlay`: fixed overlay, z-1000, rgba backdrop
  - `.forward-dialog`: card bg, 12px radius, shadow, max 380px
  - `.forward-search-input`: bordered input with focus accent
  - `.forward-conversation-list`: scrollable list
  - `.forward-conversation-item`: hover states, padding, gap

  **Review tasks**:
  1. Verify all colors use CSS variables (not hardcoded) — scan each property
  2. Ensure the dialog has smooth entry animation (apply `slideUp` keyframe):
     ```css
     .forward-dialog { animation: slideUp 0.2s ease-out; }
     ```
  3. Ensure empty state (no conversations) has styled placeholder text
  4. Test on all 8 themes — verify borders, backgrounds, text colors adapt

  **File**: `src/ToledoVault/wwwroot/app.css` (lines ~3977-4042)

  **Verification**: Open a message → click Forward → dialog should appear with smooth animation, themed colors, searchable conversation list. Test on WhatsApp Dark and Signal themes specifically.

- [ ] T018 [P] [US4] Review and enhance search result counter and highlight styling in `src/ToledoVault/wwwroot/app.css`

  **Details**: The search counter already has basic CSS (line ~3960-3963):
  - `.chat-search-count`: 0.8em, `--text-secondary`, nowrap, 4px/8px padding

  The search highlight on messages uses `.message-highlight` class (referenced in MessageBubble.razor line 427).

  **Review and enhance**:
  1. Verify `.chat-search-count` is properly visible — add a subtle background for better readability:
     ```css
     .chat-search-count {
         background: var(--hover-bg);
         border-radius: 4px;
         font-variant-numeric: tabular-nums; /* Prevent layout shift as numbers change */
     }
     ```

  2. Verify `.message-highlight` (search result highlight) has a visible background color:
     ```css
     .message-row.message-highlight .message-bubble {
         outline: 2px solid var(--accent);
         outline-offset: 2px;
     }
     ```
     Search app.css for existing `.message-highlight` rules and enhance if needed.

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Type a search query → results counter shows "1 / 5" with subtle background → highlighted message has visible accent outline.

- [ ] T019 [P] [US4] Review and enhance link preview card styling in `src/ToledoVault/wwwroot/app.css`

  **Details**: Link preview already has comprehensive CSS (lines ~3747-3801):
  - `.link-preview-card`: flex, bordered, 8px radius, hover state
  - `.link-preview-image`: 80px square, object-fit cover
  - `.link-preview-info`: padded, flex column, gap
  - Title, desc, domain: proper font sizes and colors

  **Review tasks**:
  1. Verify all colors use CSS variables — scan for any hardcoded values
  2. Ensure the card has proper max-width within message bubbles (should not overflow)
  3. Ensure loading state (`.link-preview-loading`) is themed
  4. Test image fallback — when no image, info section should take full width
  5. Verify dark theme contrast — link preview card should be distinguishable from message bubble background

  **Potential enhancement**: Add a subtle left accent border (like WhatsApp's link previews):
  ```css
  .link-preview-card { border-left: 3px solid var(--accent); }
  ```

  **File**: `src/ToledoVault/wwwroot/app.css` (lines ~3747-3801)

  **Verification**: Send a URL in a message → link preview card appears with image, title, description, domain → card is properly themed and readable in all 8 themes.

- [ ] T020 [P] [US4] Review and enhance clear chat dialog styling in `src/ToledoVault/wwwroot/app.css`

  **Details**: Clear chat dialog already has CSS (lines ~3875-3936):
  - `.clear-chat-dialog`: fixed centered, max 360px, card bg, padded
  - `.clear-chat-options`: flex column, gap, margin
  - `.clear-chat-option`: bordered, hover states
  - `.clear-chat-option.danger`: danger colors, hover inversion

  **Review tasks**:
  1. Verify smooth entry animation:
     ```css
     .clear-chat-dialog { animation: slideUp 0.2s ease-out; }
     ```
  2. Verify the backdrop/overlay dims the background (check if `.clear-chat-backdrop` exists)
  3. Test button focus states — danger button should have visible focus ring
  4. Ensure cancel button is clearly distinguished from action buttons

  **File**: `src/ToledoVault/wwwroot/app.css` (lines ~3875-3936)

  **Verification**: Click menu → Clear Chat → dialog should animate in with dimmed backdrop, danger button clearly styled red, cancel button neutral.

**Checkpoint**: All 4 previously unstyled/under-styled components now render with theme-consistent styling. Test each on at least 3 themes (Default, WhatsApp Dark, Signal).

---

## Phase 7: User Story 5 — Message Bubble Polish (Priority: P2)

**Goal**: Refine message bubble visual quality: reply quote accent borders, tighter grouped message spacing, and a prominent unread message divider.

**Independent Test**: View a conversation with replies, grouped messages from the same sender, and an unread divider. Verify each renders with professional visual quality matching WhatsApp/Telegram standards.

**Acceptance Criteria** (from spec.md):
1. Reply quote block has a colored left accent border matching the theme accent color
2. Consecutive messages from the same sender have tighter (near-zero gap) spacing
3. Unread divider has a prominent background highlight, readable text, and is immediately noticeable

### Implementation for User Story 5

- [ ] T021 [US5] Verify and enhance reply quote block left accent border in `src/ToledoVault/wwwroot/app.css`

  **Details**: The reply quote block already has a 3px left border using `var(--accent)` based on the exploration. Verify this in the actual CSS:

  Search for `.reply-quote-block` in app.css. Expected:
  ```css
  .reply-quote-block {
      border-left: 3px solid var(--accent);
      background: rgba(0, 0, 0, 0.06);
      border-radius: 6px;
      padding: 4px;
  }
  ```

  **Enhancements** (if not already present):
  1. Ensure dark theme variant uses appropriate background:
     ```css
     [data-theme*="dark"] .reply-quote-block {
         background: rgba(255, 255, 255, 0.08);
     }
     ```
     Or use a single rule with `var()`:
     ```css
     .reply-quote-block { background: var(--hover-bg); }
     ```

  2. Ensure the sender name in the reply quote uses accent color:
     ```css
     .reply-sender-name { color: var(--accent); font-weight: 600; }
     ```

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Reply to a message → the quote block should have a clear colored left border in the theme's accent color. Switch between themes → border color changes accordingly.

- [ ] T022 [US5] Tighten grouped message spacing for consecutive same-sender messages in `src/ToledoVault/wwwroot/app.css`

  **Details**: Currently grouped messages use classes `group-first`, `group-middle`, `group-last` with 1px margins between them (app.css lines ~2878-2893). This is already quite tight.

  **Review current spacing**:
  - `.message-row` default has `margin-bottom` (check exact value — likely 4-8px)
  - Grouped messages override to 1px

  **Enhancement**: Ensure the grouped spacing is visually distinct from non-grouped:
  ```css
  .message-row { margin-bottom: 6px; } /* Default spacing */
  .message-row.group-first { margin-bottom: 1px; }
  .message-row.group-middle { margin-bottom: 1px; }
  .message-row.group-last { margin-bottom: 6px; } /* Reset to default after group */
  ```

  Also adjust border-radius for grouped bubbles (middle messages should have smaller radius on the connecting side):
  ```css
  /* Already exists in app.css — verify and enhance */
  .message-bubble.group-first { border-bottom-left-radius: 4px; }
  .message-bubble.group-middle { border-radius: 4px; }
  .message-bubble.group-last { border-top-left-radius: 4px; }
  /* Mirror for .mine bubbles */
  .message-bubble.mine.group-first { border-bottom-right-radius: 4px; }
  .message-bubble.mine.group-middle { border-radius: 4px; }
  .message-bubble.mine.group-last { border-top-right-radius: 4px; }
  ```

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Send 3+ consecutive messages → they should appear as a visually connected group with minimal gaps. A message from a different sender should have normal spacing.

- [ ] T023 [US5] Enhance unread message divider to be more prominent in `src/ToledoVault/wwwroot/app.css` and `src/ToledoVault.Client/Pages/Chat.razor`

  **Details**: The current unread divider (app.css lines ~3061-3078) uses `var(--accent)` color with `::before`/`::after` lines. Per research decision R-006, it should be a full-width bar with accent background and contrasting text.

  **Replace** the current thin-line divider with a prominent bar:

  ```css
  .unread-divider {
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 12px 0;
      padding: 6px 16px;
      background: var(--accent);
      border-radius: 8px;
      color: var(--accent-text);
      font-size: 0.8rem;
      font-weight: 600;
      text-align: center;
      user-select: none;
  }
  .unread-divider::before,
  .unread-divider::after {
      display: none; /* Remove the old line pseudo-elements */
  }
  ```

  **Blazor markup** (Chat.razor lines ~148-154): The markup already renders `<div class="unread-divider"><span>...</span></div>`. No Blazor changes needed unless the text format needs updating.

  **File**: `src/ToledoVault/wwwroot/app.css` (lines ~3061-3078 — replace existing rules)

  **Verification**: Open a chat with unread messages → a prominent colored bar reading "X new messages" should be immediately visible when scrolling through the chat. The bar should use the theme's accent color as background.

**Checkpoint**: Message bubbles look polished — reply quotes have accent borders, grouped messages are tightly spaced, unread divider is impossible to miss.

---

## Phase 8: User Story 6 — Delivery Status Icon Clarity (Priority: P3)

**Goal**: Make delivery status icons (sending, sent, delivered, read) visually distinct so users can tell message states apart at a glance.

**Independent Test**: Send messages and verify each status (sending, sent, delivered, read) is visually distinct with appropriate iconography and colors.

**Acceptance Criteria** (from spec.md):
1. Sending: clock or spinner icon
2. Sent: single checkmark
3. Delivered: double checkmarks
4. Read: double checkmarks in theme accent color

### Implementation for User Story 6

- [ ] T024 [US6] Review and enhance `DeliveryStatus.razor` component for clear icon differentiation in `src/ToledoVault.Client/Components/DeliveryStatus.razor`

  **Details**: First, read the current `DeliveryStatus.razor` component to understand the current implementation. The component renders delivery status icons based on an enum (Sending, Sent, Delivered, Read).

  **Expected enhancements**:
  1. **Sending**: Should show a clock icon (&#128339; or SVG clock) or a small spinner. If currently just text, replace with an icon.
  2. **Sent**: Should show a single checkmark (&#10003; or SVG). Muted color using `var(--text-secondary)`.
  3. **Delivered**: Should show double checkmarks (&#10003;&#10003; or SVG double-check). Muted color using `var(--text-secondary)`.
  4. **Read**: Should show double checkmarks in `var(--accent)` — the ONLY state that uses accent color.

  **CSS for delivery icons** (add to app.css if not exists):
  ```css
  .delivery-status { display: inline-flex; align-items: center; font-size: 0.75rem; }
  .delivery-status.sending { color: var(--text-secondary); }
  .delivery-status.sent { color: var(--text-secondary); }
  .delivery-status.delivered { color: var(--text-secondary); }
  .delivery-status.read { color: var(--accent); }
  .delivery-status.sending .delivery-icon { animation: spin 0.8s linear infinite; }
  ```

  **Files**:
  - `src/ToledoVault.Client/Components/DeliveryStatus.razor` — Review and update markup/icons
  - `src/ToledoVault/wwwroot/app.css` — Add/update delivery status CSS

  **Verification**: Send a message → observe the status icon change through: clock (sending) → single check (sent) → double check (delivered) → blue/accent double check (read). Each state should be visually distinct without reading tooltip text.

**Checkpoint**: All 4 delivery states are visually distinct with appropriate iconography and theme-aware colors.

---

## Phase 9: User Story 7 — Accessibility Improvements (Priority: P3)

**Goal**: Ensure all interactive elements meet accessibility standards: 44x44px touch targets, visible focus rings, and usable scrollbar width on mobile.

**Independent Test**: Tab through the app with keyboard and verify focus rings on all interactive elements. Test touch targets on mobile viewport. Verify scrollbar width on touch devices.

**Acceptance Criteria** (from spec.md):
1. Emoji, attach, send buttons have at least 44x44px touch targets on mobile
2. All interactive elements show visible focus ring when navigated via keyboard
3. Scrollbars are at least 8px wide on touch devices

### Implementation for User Story 7

- [ ] T025 [US7] Verify and fix touch target sizes for interactive buttons in `src/ToledoVault/wwwroot/app.css`

  **Details**: Current button sizes from exploration:
  - Emoji button: 40px x 40px (needs 44px)
  - Attach button: 40px x 40px (needs 44px)
  - Send button: 42px x 42px (needs 44px)
  - Audio play button: 32px x 32px (needs 44px)

  **For mobile viewports only** (don't change desktop layout), increase touch targets:
  ```css
  @media (hover: none) and (pointer: coarse) {
      .emoji-btn,
      .attach-btn,
      .msg-action-btn {
          min-width: 44px;
          min-height: 44px;
      }
      .send-btn {
          min-width: 44px;
          min-height: 44px;
      }
      .audio-play-btn {
          min-width: 44px;
          min-height: 44px;
      }
  }
  ```

  The `@media (hover: none) and (pointer: coarse)` query targets touch-only devices, avoiding layout changes on desktop.

  **Alternative approach**: Use invisible `::before` pseudo-elements to expand the hit area without changing visual size:
  ```css
  @media (hover: none) and (pointer: coarse) {
      .audio-play-btn {
          position: relative;
      }
      .audio-play-btn::before {
          content: '';
          position: absolute;
          top: 50%; left: 50%;
          width: 44px; height: 44px;
          transform: translate(-50%, -50%);
      }
  }
  ```

  **Recommended**: Use `min-width`/`min-height` — it's simpler and the 2-4px size increase is barely noticeable.

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Open DevTools → device toolbar (iPhone/Android) → inspect button elements → computed size should be >= 44x44px.

- [ ] T026 [P] [US7] Verify focus-visible rings are applied to ALL interactive elements in `src/ToledoVault/wwwroot/app.css`

  **Details**: Focus rings already exist globally (lines ~2074-2082):
  ```css
  .focus-ring:focus-visible,
  button:focus-visible,
  a:focus-visible,
  input:focus-visible,
  select:focus-visible,
  textarea:focus-visible {
      outline: 2px solid var(--accent);
      outline-offset: 2px;
  }
  ```

  **Verify coverage**:
  1. Tab through the ENTIRE app and check every interactive element has a focus ring
  2. Check these specific elements that might be missed:
     - Reaction badges (they're `<button>` so should be covered)
     - Context menu items (they're `<button>` so should be covered)
     - Forward dialog items (check if they're `<button>` or `<div>` — only `<button>` gets automatic focus ring)
     - Wallpaper cards in Settings
     - Theme selection buttons in Settings
  3. If any element uses `<div>` with `@onclick` instead of `<button>`, it won't get focus ring. Options:
     - Change to `<button>` (preferred)
     - Add `tabindex="0"` and `.focus-ring` class

  **File**: `src/ToledoVault/wwwroot/app.css` (and potentially Blazor components if `<div>` needs changing to `<button>`)

  **Verification**: Start at the top of the app → Tab key repeatedly → every interactive element should show a 2px accent-colored outline. No element should be skipped or have no visible focus indicator.

- [ ] T027 [P] [US7] Increase scrollbar width to 8px on touch devices in `src/ToledoVault/wwwroot/app.css`

  **Details**: Current scrollbar (lines ~2253-2262):
  ```css
  ::-webkit-scrollbar { width: 6px; }
  ```

  For touch devices, increase to 8px:
  ```css
  @media (hover: none) and (pointer: coarse) {
      ::-webkit-scrollbar {
          width: 8px;
      }
      ::-webkit-scrollbar-thumb {
          background: var(--text-secondary);
          border-radius: 4px;
      }
  }
  ```

  Also add Firefox scrollbar support:
  ```css
  @media (hover: none) and (pointer: coarse) {
      * {
          scrollbar-width: auto; /* Firefox: 'auto' is wider than 'thin' */
          scrollbar-color: var(--text-secondary) transparent;
      }
  }
  ```

  **File**: `src/ToledoVault/wwwroot/app.css`

  **Verification**: Open on mobile viewport → scroll through chat or conversation list → scrollbar should be noticeably wider (8px) and easily graspable.

**Checkpoint**: All accessibility improvements in place. Keyboard navigation shows focus rings, mobile touch targets are 44px+, scrollbars are 8px on touch devices.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final verification, edge case handling, and cross-story consistency checks.

- [ ] T028 Run full 8-theme visual regression test across all user stories

  **Details**: This is a manual verification task. For EACH of the 8 themes (Default, Default Dark, WhatsApp, WhatsApp Dark, Telegram, Telegram Dark, Signal, Signal Dark):

  1. Switch theme in Settings
  2. Verify US1: No hardcoded colors (audio waveform, PDF preview, avatars, HD toggle)
  3. Verify US2: Timestamps readable without hover
  4. Verify US3: Animations play smoothly (send a message, trigger toast, load chat)
  5. Verify US4: Forward dialog, search counter, link preview, clear chat dialog all themed
  6. Verify US5: Reply quotes have accent border, grouped messages tight, unread divider prominent
  7. Verify US6: Delivery status icons distinct (sending/sent/delivered/read)
  8. Verify US7: Focus rings visible, touch targets adequate (test on mobile viewport)

  **No file changes** — this is a verification/sign-off task.

- [ ] T029 [P] Verify `prefers-reduced-motion` suppresses ALL animations added in this feature

  **Details**: In Chrome DevTools → Rendering tab → "Emulate CSS media feature prefers-reduced-motion" → set to "reduce". Then:

  1. Send a message → should appear instantly (no slide-in)
  2. Trigger a toast → should appear instantly (no slide)
  3. Load a chat → skeleton should show static color (no shimmer)
  4. Click a reaction → should update instantly (no pop)
  5. Open context menu → should appear instantly (no fade)

  If any animation still plays, update the `@media (prefers-reduced-motion: reduce)` block from T005.

  **File**: `src/ToledoVault/wwwroot/app.css` (update media query block if needed)

- [ ] T030 [P] Verify all existing tests still pass after UI changes

  **Details**: Run the full test suite to ensure no regressions:
  ```bash
  cd tests && dotnet test
  ```

  All 231 tests should pass. CSS-only changes should not break any tests, but verify in case any Blazor component markup changes (e.g., T012 adding `new-message` class, T024 updating DeliveryStatus) affected component rendering.

  **Files**: Test projects in `tests/` directory

- [ ] T031 Run quickstart.md validation workflow

  **Details**: Follow the complete quickstart workflow documented in `specs/007-ui-polish/quickstart.md`:
  1. Start the app (`dotnet run`)
  2. Test across all 8 themes
  3. Test accessibility (keyboard, reduced motion, mobile)
  4. Run existing tests
  5. Deploy to IIS and verify at localhost:8080

  **File**: `specs/007-ui-polish/quickstart.md` (reference only)

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup: Theme Variables)
    └──► Phase 2 (Foundational: Keyframes + Reduced Motion)
            └──► Phase 3-9 (User Stories — can proceed in parallel or sequentially)
                    └──► Phase 10 (Polish: Cross-cutting verification)
```

- **Phase 1**: No dependencies — starts immediately
- **Phase 2**: Depends on Phase 1 (keyframes reference theme variables for skeleton shimmer)
- **Phase 3 (US1 - Theme Colors)**: Depends on Phase 1 (uses `--waveform-played` variable)
- **Phase 4 (US2 - Timestamps)**: Depends on Phase 2 only (no new variables needed)
- **Phase 5 (US3 - Animations)**: Depends on Phase 2 (uses keyframe definitions from T002-T004)
- **Phase 6 (US4 - Component Styling)**: Independent after Phase 2
- **Phase 7 (US5 - Bubble Polish)**: Independent after Phase 2
- **Phase 8 (US6 - Delivery Icons)**: Independent after Phase 2
- **Phase 9 (US7 - Accessibility)**: Independent after Phase 2
- **Phase 10 (Polish)**: Depends on ALL user stories being complete

### User Story Dependencies

- **US1 (P1 - Theme Colors)**: MUST complete before US3 (animations use themed skeleton colors)
- **US2 (P1 - Timestamps)**: Fully independent
- **US3 (P2 - Animations)**: Depends on US1 (skeleton shimmer uses `--skeleton-base`/`--skeleton-shimmer`)
- **US4 (P2 - Component Styling)**: Fully independent
- **US5 (P2 - Bubble Polish)**: Fully independent
- **US6 (P3 - Delivery Icons)**: Fully independent
- **US7 (P3 - Accessibility)**: Fully independent

### Within Each User Story

- Read existing CSS/markup first to understand current state
- Make CSS changes
- Verify on all 8 themes
- No test-first approach (CSS changes verified visually)

### Parallel Opportunities

**Phase 2** (all 4 tasks can run in parallel — different keyframes, no file conflicts if inserted at different lines):
- T002 + T003 + T004 can be done together (all insert keyframes near line ~2030-2035)
- T005 is independent (appended at end of file)

**User Stories 4-9** can all proceed in parallel after Phase 2:
- US2 (T011) — touches timestamp opacity rules
- US4 (T017-T020) — touches forward dialog, search, link preview, clear chat CSS
- US5 (T021-T023) — touches reply quote, grouped messages, unread divider CSS
- US6 (T024) — touches DeliveryStatus component
- US7 (T025-T027) — touches button sizes, focus rings, scrollbar CSS

**Within US4**, all 4 tasks (T017-T020) can run in parallel (different CSS selectors, no conflicts).
**Within US7**, all 3 tasks (T025-T027) can run in parallel (different CSS sections).

---

## Parallel Example: User Story 4 (Missing Component Styling)

```text
# All 4 component styling tasks can run in parallel (different CSS selectors):
Task T017: "Review forward dialog styling in app.css (.forward-dialog-*)"
Task T018: "Review search counter styling in app.css (.chat-search-count)"
Task T019: "Review link preview styling in app.css (.link-preview-*)"
Task T020: "Review clear chat dialog styling in app.css (.clear-chat-*)"
```

## Parallel Example: User Story 7 (Accessibility)

```text
# All 3 accessibility tasks can run in parallel (different CSS sections):
Task T025: "Fix touch target sizes in app.css (@media hover:none)"
Task T026: "Verify focus-visible rings in app.css (button:focus-visible)"
Task T027: "Increase scrollbar width in app.css (::-webkit-scrollbar)"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Add theme variables to `themes.css` (T001)
2. Complete Phase 2: Add keyframes and reduced-motion rules (T002-T005)
3. Complete Phase 3: US1 — Fix hardcoded colors (T006-T010)
4. Complete Phase 4: US2 — Fix timestamp visibility (T011)
5. **STOP and VALIDATE**: Switch through all 8 themes, verify colors and timestamps
6. Deploy/demo if ready — the two most visible defects are fixed

### Incremental Delivery

1. **Setup + Foundation** → Theme system ready, animation infrastructure in place
2. **US1 (Theme Colors)** → Test on all themes → Deploy (MVP)
3. **US2 (Timestamps)** → Test on mobile → Deploy
4. **US3 (Animations)** → Test each animation type → Deploy
5. **US4 (Component Styling)** → Test each component → Deploy
6. **US5 (Bubble Polish)** → Test visual quality → Deploy
7. **US6 (Delivery Icons)** → Test status differentiation → Deploy
8. **US7 (Accessibility)** → Test keyboard nav + mobile → Deploy
9. **Polish** → Final 8-theme regression → Final deploy

### Single Developer Strategy (Recommended)

Execute phases sequentially in priority order (P1 → P2 → P3):
1. Phase 1 + 2 (Setup + Foundation) — ~30 min
2. Phase 3 (US1 - Theme Colors) — ~45 min
3. Phase 4 (US2 - Timestamps) — ~15 min
4. Phase 5 (US3 - Animations) — ~45 min
5. Phase 6 (US4 - Component Styling) — ~30 min
6. Phase 7 (US5 - Bubble Polish) — ~30 min
7. Phase 8 (US6 - Delivery Icons) — ~30 min
8. Phase 9 (US7 - Accessibility) — ~20 min
9. Phase 10 (Polish) — ~30 min

---

## Notes

- [P] tasks = different files or different CSS selectors, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable via visual inspection
- The primary files modified are `app.css` (~95% of changes) and `themes.css` (~5%)
- Blazor component changes are minimal (adding CSS classes, not changing logic)
- All 231 existing tests should continue to pass — CSS changes don't affect server/crypto logic
- Commit after each completed user story for clean git history
- Total: 31 tasks across 10 phases (7 user stories + setup + foundation + polish)
