/# Feature Specification: Fix Styles

**Feature Branch**: `003-fix-styles`
**Created**: 2026-03-03
**Status**: Draft
**Input**: User description: "Make all styles work with all device types (mobile focus and web). Enhance chat styles — reply/quote bar is oversized and needs WhatsApp/Telegram-style compact design. Fix style flash during sign-in and key generation. Mobile bugs: clicking a chat then going back causes issues; Settings button not clickable on mobile."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Compact Reply/Quote Message Bar (Priority: P1)

A user replies to a previous message in a chat conversation. The reply preview bar (above the message input) and the quoted message block (inside message bubbles) should be compact and visually polished, following the design patterns used by WhatsApp and Telegram — small accent border on the left, sender name, truncated message preview (max 2 lines), and a close button.

**Why this priority**: The reply bar is currently oversized and visually broken due to duplicate conflicting styles. This directly impacts the most common chat interaction — replying to messages. Users see this every time they reply.

**Independent Test**: Reply to a message containing a long paragraph and verify the reply preview bar is compact (under 64px height), text is truncated to 2 lines max, and the close button is easily tappable.

**Acceptance Scenarios**:

1. **Given** a user clicks reply on a message, **When** the reply preview bar appears above the input, **Then** it is no taller than 64px with the message text truncated to a maximum of 2 lines.
2. **Given** a user replies to a message with a very long body, **When** the reply preview is shown, **Then** the text is truncated with an ellipsis and does not cause the bar to expand beyond its max height.
3. **Given** a user views a message bubble that quotes another message, **When** the quoted block is rendered inside the bubble, **Then** it is compact (max 60px height), shows sender name and truncated text, and has a left accent border.
4. **Given** a user on mobile (320px–480px), **When** they reply to a message, **Then** the reply bar fits within the viewport width without overflow or horizontal scroll.

---

### User Story 2 - Mobile Navigation and Tap Targets (Priority: P1)

A user on an iPhone (or other mobile browser) navigates the app. When they tap a conversation from the chat list to open it, then tap back to return to the list, the navigation works smoothly without errors or broken state. When they tap the Settings button in the sidebar, it responds to the tap and navigates to the settings page.

**Why this priority**: These are critical navigation bugs that make the app unusable on mobile. If users cannot navigate between chats or access settings, the app is fundamentally broken on the primary platform.

**Independent Test**: On an iPhone browser, open the app, tap a conversation, tap back, and verify the chat list loads correctly. Then tap Settings and verify it navigates to the settings page.

**Acceptance Scenarios**:

1. **Given** a user on mobile taps a conversation in the chat list, **When** they tap the back button to return, **Then** the conversation list re-appears without errors, blank screens, or stale data.
2. **Given** a user on mobile taps back from a chat multiple times in succession, **When** they navigate back and forth repeatedly, **Then** the navigation remains stable with no JavaScript errors or frozen UI.
3. **Given** a user on mobile taps the Settings button/link in the sidebar or navigation, **When** they tap it, **Then** the app navigates to the settings page immediately with no unresponsive taps.
4. **Given** any navigation element (sidebar items, back button, settings, new conversation), **When** the user taps it on mobile, **Then** it responds on the first tap without requiring multiple attempts.

---

### User Story 3 - Responsive Layout for All Devices (Priority: P2)


A user accesses the messaging app from any device — mobile phone (320px–480px), tablet (768px), or desktop (1024px+) — and the entire UI adapts fluidly. Mobile is the primary focus: all pages (chat, conversation list, settings, login, register, new conversation, security info) must be fully usable on small screens with no horizontal overflow, clipped buttons, or unreadable text.

**Why this priority**: A messaging app is primarily used on mobile. If the UI doesn't work well on phones, the app fails its core use case regardless of desktop quality.

**Independent Test**: Open every page of the app at 320px, 375px, 480px, 768px, and 1280px viewport widths. Verify no horizontal scrollbar, all interactive elements are tappable (minimum 44px touch targets), and text is readable.

**Acceptance Scenarios**:

1. **Given** a user on a 320px-wide screen (iPhone SE), **When** they navigate through all pages (chat, chat list, settings, login, register), **Then** no horizontal scrollbar appears and all content fits within the viewport.
2. **Given** a user on a 375px-wide screen (iPhone 12/14), **When** they use the voice recorder, **Then** the recording bar, timer, waveform, and controls fit without overflow.
3. **Given** a user on a 768px tablet in portrait, **When** they view the two-panel layout, **Then** either the sidebar or chat panel is shown (not both cramped together) with smooth panel switching.
4. **Given** a user on desktop (1280px+), **When** they use the app, **Then** the two-panel layout displays sidebar and chat panel side by side with proper proportions.
5. **Given** any overlay (emoji picker, forward dialog, image lightbox), **When** opened on mobile, **Then** it is sized appropriately for the screen and dismissible with standard gestures.

---

### User Story 4 - Eliminate Style Flash During Sign-In and Key Generation (Priority: P3)

A user signs in to the app, and during the key generation/restoration process (which takes 1–2 seconds), the page layout and styles remain stable. There should be no visible "style flash" where the UI briefly changes appearance (colors, layout, fonts) and then snaps back to normal.

**Why this priority**: Style flashing during sign-in creates a feeling of instability and low quality. While it only occurs once per login session, it's the user's first impression after authenticating.

**Independent Test**: Sign in to the app and observe the UI during the "Generating encryption keys..." phase. The page layout, colors, and fonts must remain visually stable throughout the entire process — no flicker, no layout shift, no temporary unstyled content.

**Acceptance Scenarios**:

1. **Given** a user submits the login form, **When** the system displays "Setting up encryption keys..." or "Generating encryption keys...", **Then** the page layout does not shift, colors do not flash, and fonts remain consistent.
2. **Given** a new user registers and keys are generated for the first time, **When** the key generation status messages appear, **Then** the alert/status area is pre-allocated in the layout so that its appearance does not push other elements around.
3. **Given** a returning user signs in and keys are restored from backup, **When** the "Restoring encryption keys..." message appears, **Then** the transition between status messages is smooth with no visible layout recalculation.
4. **Given** the app loads in the browser for the first time (WASM download), **When** the WebAssembly runtime initializes, **Then** a loading indicator is shown and the page does not flash between styled and unstyled states.

---

### Edge Cases

- What happens when the user navigates back from a chat while a SignalR message is being received?
- What if the Settings tap target overlaps with another element on mobile (z-index or position issue)?
- When replying to a media-only message (image, audio, file), the preview bar shows a placeholder label: "[Image]", "[Voice message]", "[File: name.pdf]".
- How does the reply bar behave when the quoted sender name is very long (30+ characters)?
- What happens at exactly 768px (tablet breakpoint boundary) — does the layout toggle correctly?
- What happens when landscape mode is used on a phone (<500px height)?
- What if key generation fails — does the style remain stable during error display?
- How does the app look during WASM framework download (before any interactive rendering)?

## Requirements *(mandatory)*

### Functional Requirements

**Reply/Quote Bar (P1):**

- **FR-001**: The reply preview bar MUST have a single, consistent set of styles — no duplicate or conflicting rules.
- **FR-002**: The reply preview bar MUST have a maximum height of 64px with message text truncated to 2 lines using line-clamp.
- **FR-003**: The reply preview bar MUST follow the WhatsApp/Telegram pattern: left accent border (3px), sender name (bold, small font), truncated message preview, and a compact close button.
- **FR-004**: The close button on the reply bar MUST have a minimum touch target of 44x44px on mobile devices.
- **FR-005**: The quoted message block inside message bubbles MUST have a maximum height of 60px with overflow hidden.
- **FR-006**: Reply bar text MUST use `-webkit-line-clamp: 2` for reliable multi-line truncation across browsers.
- **FR-006a**: When replying to a media-only message (no text), the reply preview MUST show a descriptive placeholder label: "[Image]", "[Voice message]", or "[File: filename.ext]".

**Mobile Navigation (P1):**

- **FR-007**: Navigating from a chat back to the conversation list on mobile MUST work without errors, blank screens, or stale state.
- **FR-008**: The Settings button/link MUST be tappable on mobile with a minimum touch target of 44x44px and must navigate to the settings page on first tap.
- **FR-009**: All sidebar navigation items (conversations, settings, new conversation) MUST respond to taps reliably on mobile browsers (Safari, Chrome).
- **FR-010**: Repeated back-and-forth navigation between chat list and individual chats MUST remain stable without accumulating errors or memory issues.

**Responsive Layout (P2):**

- **FR-011**: All pages MUST render without horizontal scrollbar at viewport widths from 320px to 1920px.
- **FR-012**: All interactive elements (buttons, inputs, links) MUST have a minimum touch target of 44x44px on mobile viewports.
- **FR-013**: The voice recorder bar, waveform, and controls MUST fit within the viewport at 320px width without overflow.
- **FR-014**: The two-panel layout MUST switch to single-panel mode on screens narrower than 768px.
- **FR-015**: All text MUST remain readable (minimum effective size of 14px) on mobile viewports.

**Style Flash Fix (P3):**

- **FR-016**: The sign-in/register pages MUST pre-allocate space for status messages so their appearance does not cause layout shift.
- **FR-017**: During key generation, the page MUST NOT exhibit any visible style flash (color, font, or layout changes lasting less than 500ms).
- **FR-018**: The WASM loading phase MUST show a centered spinner with the app name/logo until the interactive UI is fully rendered. No skeleton screens or progress bars.
- **FR-019**: CSS animations on status messages MUST NOT cause the containing layout to reflow or shift.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The reply preview bar height does not exceed 64px regardless of the quoted message length.
- **SC-002**: 100% of pages render without horizontal scrollbar at all 5 test viewports (320px, 375px, 480px, 768px, 1280px).
- **SC-003**: Zero visible layout shifts (CLS score of 0) occur during the sign-in and key generation flow.
- **SC-004**: All interactive elements meet the 44x44px minimum touch target on mobile viewports.
- **SC-005**: The reply bar visual design matches the WhatsApp/Telegram compact pattern as judged by side-by-side comparison.
- **SC-006**: Users can complete the sign-in flow without noticing any style instability or visual flashing.
- **SC-007**: On iPhone Safari, a user can navigate chat → back → chat → back 10 times consecutively without any errors or broken UI state.
- **SC-008**: The Settings button responds to tap on first attempt on all tested mobile browsers (Safari, Chrome).

## Clarifications

### Session 2026-03-03

- Q: What should the reply preview bar display when replying to a media-only message (image, audio, file with no text)? → A: Show a placeholder label: "[Image]", "[Voice message]", "[File: name.pdf]"
- Q: What style of loading indicator should be shown during WASM framework download? → A: Centered spinner with app name/logo (simple, branded)

## Assumptions

- The WhatsApp/Telegram reply bar pattern (left accent border, sender name, 2-line truncated preview, close button) is the target design — not pixel-perfect copy but same UX pattern.
- The duplicate reply bar CSS rules (two sets in app.css) are a bug, not intentional — one set should be removed.
- The style flash during sign-in is caused by WASM initialization and late component rendering, not by server-side issues.
- Mobile-first responsive design is the approach — styles start from the smallest viewport and scale up.
- All 8 existing themes must continue to work correctly after these changes.
- The status message area during key generation should use reserved space (min-height) rather than dynamic insertion to prevent layout shift.
