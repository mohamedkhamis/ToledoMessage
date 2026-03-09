# Feature Specification: UI Polish & Visual Enhancement

**Feature Branch**: `007-ui-polish`
**Created**: 2026-03-06
**Status**: Draft
**Input**: Comprehensive styling improvements covering theme consistency, micro-animations, accessibility, missing CSS, message bubble polish, timestamp visibility, delivery status icons, and dark mode fixes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Theme-Consistent Colors Everywhere (Priority: P1)

A user switches between themes (Default, WhatsApp, Telegram, Signal, and their dark variants) and expects all visual elements to adapt accordingly. Currently, hardcoded colors in audio waveforms, PDF previews, avatar backgrounds, HD toggle, and video play buttons break the visual consistency when themes change.

**Why this priority**: Broken theme colors are the most visible defect — users who select a dark theme see jarring light-colored elements (e.g., white PDF preview background, WhatsApp-green waveform in Signal theme). This undermines trust in the theme system.

**Independent Test**: Switch through all 8 themes and verify every visible element uses theme-appropriate colors with no hardcoded values standing out.

**Acceptance Scenarios**:

1. **Given** the user selects WhatsApp Dark theme, **When** they view a PDF file message, **Then** the PDF preview background uses the theme's dark background color, not a hardcoded light gray.
2. **Given** the user is on Signal theme, **When** they play an audio message, **Then** the played waveform bars use the Signal accent color, not a hardcoded WhatsApp green.
3. **Given** the user is on Telegram Dark theme, **When** they view contact avatars, **Then** avatar background colors harmonize with the dark theme palette.
4. **Given** any theme is active, **When** the user views a video message, **Then** the play button overlay adapts to the theme rather than using a hardcoded dark overlay.

---

### User Story 2 - Readable Timestamps on Mobile (Priority: P1)

Mobile users cannot hover to reveal message timestamps. Currently, timestamps have reduced opacity and rely on hover for full visibility — this means mobile users always see faded, hard-to-read timestamps.

**Why this priority**: Timestamps are essential information for any messaging app. Making them unreadable on mobile is a critical usability gap.

**Independent Test**: Open the chat on a mobile device (or narrow viewport) and verify timestamps are clearly readable without any interaction.

**Acceptance Scenarios**:

1. **Given** a user views a chat on a mobile device, **When** messages are displayed, **Then** timestamps are visible at full or near-full opacity without requiring hover.
2. **Given** a user views a chat on desktop, **When** they hover over a message, **Then** timestamps may optionally increase prominence but must already be readable without hover.

---

### User Story 3 - Micro-Animations for Polish (Priority: P2)

The app feels static compared to competitors. New messages appear instantly, toasts pop in without animation, skeleton loaders use basic pulse, reactions don't animate, and context menus appear abruptly.

**Why this priority**: Micro-animations significantly improve perceived quality and help users understand state changes. They are the difference between "functional" and "polished."

**Independent Test**: Trigger each animation scenario (receive message, show toast, load chat, react to message, open context menu) and verify smooth, non-janky animations.

**Acceptance Scenarios**:

1. **Given** a new message arrives, **When** it is rendered in the chat, **Then** it slides in from the appropriate side with a brief animation (under 300ms).
2. **Given** a toast notification is triggered, **When** it appears, **Then** it slides/fades in from the top with a smooth entry animation and auto-dismisses with a fade-out.
3. **Given** a chat is loading, **When** skeleton placeholders are shown, **Then** they use a shimmer (gradient sweep) animation instead of basic pulse.
4. **Given** a user taps a reaction badge, **When** the count changes, **Then** the badge shows a brief pop/scale animation.
5. **Given** a user right-clicks/long-presses a message, **When** the context menu appears, **Then** it fades in with a slight scale-up animation.

---

### User Story 4 - Missing Component Styling (Priority: P2)

Several UI components render with no CSS — forward dialog, search result counter, link previews, and clear chat dialog appear unstyled or invisible.

**Why this priority**: Unstyled components are broken components. Users see raw HTML or invisible elements for features that are supposed to work.

**Independent Test**: Trigger each component (forward a message, search in chat, send a link, clear chat) and verify they render with proper styling matching the current theme.

**Acceptance Scenarios**:

1. **Given** a user opens the forward dialog, **When** the dialog renders, **Then** it shows a styled overlay with conversation list, search input, and proper spacing matching the app's design language.
2. **Given** a user searches within a chat, **When** results are found, **Then** a styled counter shows "X of Y" in the search bar and matched messages are visually highlighted.
3. **Given** a user sends a message containing a URL, **When** the link preview renders, **Then** it shows in a styled card with proper borders, padding, and theme colors.
4. **Given** a user opens the clear chat dialog, **When** the dialog renders, **Then** options are styled as distinct, interactive buttons in a themed dialog box.

---

### User Story 5 - Message Bubble Polish (Priority: P2)

Message bubbles lack visual refinement: reply quotes have no accent border, grouped messages have too much spacing between them, and the unread message divider is too subtle to notice.

**Why this priority**: These are the core visual elements users interact with constantly. Small improvements here have outsized impact on perceived quality.

**Independent Test**: View a conversation with replies, grouped messages, and an unread divider, and verify each renders with professional visual quality.

**Acceptance Scenarios**:

1. **Given** a message includes a reply quote, **When** it renders, **Then** the quote block has a colored left accent border matching the theme accent color.
2. **Given** consecutive messages from the same sender, **When** they are grouped, **Then** the vertical spacing between them is tighter (near-zero gap) to visually connect them.
3. **Given** a user has unread messages, **When** the unread divider renders, **Then** it has a prominent background highlight, readable text, and is immediately noticeable.

---

### User Story 6 - Delivery Status Icon Clarity (Priority: P3)

Users cannot easily distinguish between sent, delivered, and read message states. The delivery status indicators lack clear visual differentiation.

**Why this priority**: While functional, clearer icons help users understand message delivery without cognitive effort.

**Independent Test**: Send messages and verify each status (sending, sent, delivered, read) is visually distinct with appropriate iconography.

**Acceptance Scenarios**:

1. **Given** a message is being sent, **When** the status indicator shows, **Then** it displays a clock or spinner icon.
2. **Given** a message is sent to server, **When** the status updates, **Then** it shows a single checkmark.
3. **Given** a message is delivered to recipient, **When** the status updates, **Then** it shows double checkmarks.
4. **Given** a message is read by recipient, **When** the status updates, **Then** the double checkmarks change to the theme's accent color.

---

### User Story 7 - Accessibility Improvements (Priority: P3)

Interactive elements have touch targets smaller than the 44x44px minimum, focus indicators are missing or inconsistent, and scrollbars are too thin on mobile.

**Why this priority**: Accessibility improvements benefit all users, not just those with disabilities. Larger touch targets reduce mis-taps; visible focus rings help keyboard users.

**Independent Test**: Tab through the app with keyboard and verify focus rings on all interactive elements. Test touch targets on mobile.

**Acceptance Scenarios**:

1. **Given** a user interacts with emoji, attach, or send buttons on mobile, **When** they tap, **Then** the touch target is at least 44x44 pixels.
2. **Given** a user navigates with keyboard, **When** they tab through interactive elements, **Then** each element shows a visible focus ring.
3. **Given** a user scrolls on a mobile device, **When** the scrollbar appears, **Then** it is wide enough to be usable (at least 8px).

---

### Edge Cases

- What happens when animations are disabled in OS accessibility settings (`prefers-reduced-motion`)? All animations should be suppressed.
- How do micro-animations behave on low-performance devices? Animations should be lightweight (CSS-only, no JavaScript-driven animations) to avoid jank.
- What happens when a theme is switched while animations are playing? Animations should complete gracefully with the new theme colors.
- How do styled components render when their data is empty (e.g., forward dialog with no conversations)? Empty states should be styled consistently.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All color values in the styling system MUST use CSS custom properties (`var(--*)`) instead of hardcoded hex/rgba values.
- **FR-002**: Message timestamps MUST be readable without hover interaction on all devices.
- **FR-003**: New messages MUST animate into view with a slide-in effect (duration under 300ms).
- **FR-004**: Toast notifications MUST animate in and out with fade/slide transitions.
- **FR-005**: Skeleton loaders MUST use a shimmer (gradient sweep) animation.
- **FR-006**: Reaction badges MUST show a brief scale animation when tapped.
- **FR-007**: Context menus MUST fade in with a slight scale transition.
- **FR-008**: The forward dialog MUST have complete styling matching the app's design language.
- **FR-009**: The search result counter MUST be styled and visible in the search bar.
- **FR-010**: Message search highlights MUST have a visible background color.
- **FR-011**: The clear chat dialog MUST be styled with proper button and layout styling.
- **FR-012**: Reply quote blocks MUST have a left accent border using the theme's accent color.
- **FR-013**: Grouped message spacing MUST be tighter than non-grouped messages.
- **FR-014**: The unread message divider MUST have a prominent background highlight and readable text.
- **FR-015**: Delivery status icons MUST visually distinguish between sending, sent, delivered, and read states using appropriate iconography and colors.
- **FR-016**: All interactive button touch targets MUST be at least 44x44 pixels.
- **FR-017**: All interactive elements MUST show a visible focus indicator when navigated via keyboard.
- **FR-018**: Scrollbars MUST be at least 8px wide on touch devices.
- **FR-019**: All animations MUST respect the `prefers-reduced-motion` media query.
- **FR-020**: The PDF preview component MUST use theme-aware background colors in dark mode.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero hardcoded color values remain in production styling — all colors reference theme variables.
- **SC-002**: Message timestamps are readable (sufficient contrast ratio) on all 8 themes without user interaction.
- **SC-003**: All micro-animations complete in under 300ms and do not cause visible frame drops.
- **SC-004**: All previously unstyled components (forward dialog, search counter, link preview, clear chat dialog) render with theme-consistent styling.
- **SC-005**: 100% of interactive elements meet the 44x44px minimum touch target on mobile viewports.
- **SC-006**: All interactive elements show a visible focus ring when navigated via keyboard (Tab key).
- **SC-007**: Users can visually distinguish all 4 delivery states (sending, sent, delivered, read) without reading tooltip text.
- **SC-008**: The unread message divider is immediately noticeable when scrolling through messages (prominent visual treatment).

## Assumptions

- Animations will be CSS-only (no JavaScript animation libraries) for performance.
- The 8 existing themes (Default, Default Dark, WhatsApp, WhatsApp Dark, Telegram, Telegram Dark, Signal, Signal Dark) are the complete set — no new themes are added in this feature.
- Link preview component already exists in markup — only CSS styling is missing.
- Touch target size adjustments may require padding/margin changes but will not alter the visual layout significantly.
- Avatar color palettes will be curated per theme family (warm for WhatsApp, cool for Telegram/Signal, neutral for Default).
