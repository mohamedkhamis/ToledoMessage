# Feature Specification: PWA Support

**Feature Branch**: `009-pwa-support`
**Created**: 2026-03-12
**Status**: Draft
**Input**: User description: "Add PWA support for installable app on iOS, Android, and desktop browsers. Update CLAUDE.md and needed files."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Install App on Mobile Home Screen (Priority: P1)

A user visits the Toledo Message web app on their phone (iOS Safari or Android Chrome). The browser shows an "Add to Home Screen" or install prompt. After installing, the app opens in a standalone full-screen window without browser chrome — looking and feeling like a native app. The app icon appears on the home screen with the Toledo Message branding.

**Why this priority**: This is the core value of PWA — making the web app installable and giving users a native-like experience on mobile devices, which is where most messaging happens.

**Independent Test**: Can be tested by visiting the app URL on a mobile device, installing it, and verifying it launches as a standalone app with proper icon and splash screen.

**Acceptance Scenarios**:

1. **Given** a user visits the app on Android Chrome, **When** the browser detects the valid web manifest, **Then** the browser shows an install banner or the user can install via the browser menu.
2. **Given** a user visits the app on iOS Safari, **When** the user taps "Share > Add to Home Screen", **Then** the app is added with the correct name and icon.
3. **Given** the user has installed the app, **When** they tap the home screen icon, **Then** the app opens in standalone mode (no browser URL bar) with a splash screen.

---

### User Story 2 - Install App on Desktop (Priority: P2)

A user visits the app on a desktop browser (Chrome, Edge). The browser shows an install icon in the address bar. After installing, the app runs in its own window — separate from the browser — with the Toledo Message title and icon.

**Why this priority**: Desktop install provides a dedicated window experience, keeping the messaging app separate from browser tabs, which improves multitasking and accessibility.

**Independent Test**: Can be tested by visiting the app on Chrome/Edge desktop, clicking the install button, and verifying the app opens in its own OS window.

**Acceptance Scenarios**:

1. **Given** a user visits the app on Chrome desktop, **When** the install icon appears in the address bar, **Then** clicking it installs the app as a desktop application.
2. **Given** the user has installed the desktop app, **When** they launch it from their OS app launcher, **Then** it opens in a standalone window with correct title and icon.

---

### User Story 3 - Offline Shell with Reconnection (Priority: P3)

When a user opens the installed PWA without network connectivity, the app shell (HTML/CSS/JS/WASM framework files) loads from the cache instantly. The user sees their cached UI with a clear "Connecting..." indicator. Once connectivity returns, the app reconnects automatically and resumes normal operation.

**Why this priority**: Offline shell loading makes the app feel instant and native. The app should not show a blank page or browser error when offline. With offline message queuing, users can compose messages while offline which are automatically sent when back online.

**Independent Test**: Can be tested by installing the app, enabling airplane mode, launching the app, and verifying the shell loads with an appropriate offline indicator.

**Acceptance Scenarios**:

1. **Given** the user has previously loaded the app (assets cached), **When** they open the app without internet, **Then** the app shell renders (login page or last known state) instead of a browser error page.
2. **Given** the app is showing an offline state, **When** the network reconnects, **Then** the app automatically reconnects and resumes normal operation without user intervention.
3. **Given** the user has never visited the app before, **When** they try to open it offline, **Then** a standard browser offline page is shown (expected — no cached assets).
4. **Given** the user composes a message while offline, **When** the network reconnects, **Then** the message is automatically sent to the server.
5. **Given** the user receives push notifications while offline, **Then** notifications are displayed when the device wakes up.

---

### User Story 4 - App Updates Seamlessly (Priority: P3)

When a new version of the app is deployed, the service worker detects the update. The next time the user opens or refreshes the app, the new version loads automatically. The user does not need to manually clear cache or reinstall.

**Why this priority**: PWA caching must not trap users on stale versions. Seamless updates are essential for a production SaaS product.

**Independent Test**: Can be tested by deploying a new version and verifying the installed app picks up the update on next launch.

**Acceptance Scenarios**:

1. **Given** a new version is deployed, **When** the user opens the app, **Then** the service worker detects the update and activates the new version.
2. **Given** the service worker has updated, **When** the app reloads, **Then** all cached assets reflect the new version.

---

### User Story 5 - Push Notifications (Priority: P2)

The installed PWA receives push notifications for new messages and reactions even when the app is closed. Users can tap notifications to open the app directly to the relevant conversation.

**Why this priority**: Push notifications are essential for a messaging app — users need to know about new messages in real-time, not just when the app is open.

**Independent Test**: Can be tested by sending a message to the user while the PWA is closed, and verifying the notification appears.

**Acceptance Scenarios**:

1. **Given** a new message arrives for the user, **When** the app is closed, **Then** a push notification is displayed with sender name and message preview.
2. **Given** a new reaction is added to the user's message, **When** the app is closed, **Then** a push notification is displayed.
3. **Given** the user taps a push notification, **Then** the app opens to the relevant conversation.

---

### Edge Cases

- What happens when the user clears browser data? The service worker and cached assets are removed; next visit re-caches everything. Install status may be preserved by the OS.
- How does the app handle partial cache corruption? The service worker uses version-based cache names, so a full re-cache occurs on version mismatch.
- What happens on iOS where PWA support is limited? iOS Safari does not support install prompts or push notifications, but "Add to Home Screen" still works for standalone mode and basic caching.
- What if the service worker fails to register? The app continues to work as a normal web app — PWA features degrade gracefully.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST include a valid Web App Manifest with app name, short name, description, start URL, display mode (standalone), theme color, background color, and icons.
- **FR-002**: System MUST provide app icons in at least two sizes: 192x192 and 512x512 pixels (PNG format), generated from the existing favicon.svg using a build script.
- **FR-003**: System MUST include a service worker that caches the app shell (HTML, CSS, JS, WASM framework files) for offline loading.
- **FR-004**: The service worker MUST use a versioned cache strategy so that deploying a new version invalidates the old cache.
- **FR-005**: The manifest MUST specify standalone display mode so the installed app hides the browser chrome.
- **FR-006**: The manifest MUST specify appropriate theme color and background color matching the app's default theme.
- **FR-007**: The HTML host page MUST include a manifest link and the service worker registration script.
- **FR-008**: The service worker MUST NOT cache API responses or SignalR connections — only static assets.
- **FR-009**: The app MUST continue to function normally as a regular web app if the service worker fails to register or is unsupported.
- **FR-010**: System MUST include iOS-specific meta tags for home screen app compatibility.
- **FR-011**: The service worker MUST handle fetch requests with a cache-first strategy for static assets and network-first for API/hub requests.
- **FR-012**: *(DEFERRED — separate feature)* System SHOULD implement push notification support including VAPID key generation, service worker push event handling, and notification display for new messages and reactions. Deferred to a dedicated push notification feature branch due to scope (requires DB schema change, new NuGet package, VAPID infra, new API endpoints).
- **FR-013**: System MUST implement offline message queuing - messages composed offline are queued and automatically sent when connectivity is restored.
- **FR-014**: All PWA UI strings (install prompts, offline messages, notifications) MUST use the existing localization infrastructure (ToledoVault.Shared resources).
- **FR-015**: The app MUST show a warning banner when accessed via non-localhost HTTP; PWA features work fully on localhost HTTP.

### Key Entities

- **Web App Manifest**: Declaration of the app's identity, icons, display preferences, and start URL for browser install mechanics.
- **Service Worker**: Background script managing asset caching, offline fallback, and cache versioning.
- **App Icons**: PNG images at 192px and 512px (minimum) used by the OS for home screen, app drawer, and splash screens.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The app is installable on Android Chrome, iOS Safari ("Add to Home Screen"), and desktop Chrome/Edge — all three platforms show install capability.
- **SC-002**: The installed app opens in standalone mode (no browser URL bar) on all three platforms.
- **SC-003**: After first visit, the app shell loads from cache in under 2 seconds even without network connectivity.
- **SC-004**: Deploying a new version results in the updated app loading for users within one app restart — no manual cache clearing required.
- **SC-005**: The app passes the Lighthouse PWA audit with a score of 90+ (installable, optimized, best practices).
- **SC-006**: All existing functionality (messaging, auth, SignalR, themes, localization) continues to work identically after PWA changes — zero regressions.

---

## Clarifications

### Session 2026-03-12

- Q: Icon Assets → A: Generate PNG icons from the existing favicon.svg using a build script
- Q: Localization → A: Use the existing localization infrastructure (ToledoVault.Shared resources) for all PWA UI strings
- Q: HTTPS Requirement → A: Allow HTTP for localhost development, show warning for non-localhost HTTP
- Q: Push Notifications → A: Include push notifications now (new messages, reactions)
- Q: Offline Messaging → A: Implement basic offline message queuing (queue messages when offline, send on reconnect)
