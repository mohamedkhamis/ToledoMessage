# Tasks: Fix Styles

**Input**: Design documents from `/specs/003-fix-styles/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md
**Branch**: `003-fix-styles`

**Tests**: No automated tests — all validation is manual visual testing per quickstart.md.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## User Story Mapping

| Story | Spec Priority | Title |
|-------|---------------|-------|
| US1 | P1 | Compact Reply/Quote Message Bar |
| US2 | P1 | Mobile Navigation and Tap Targets |
| US3 | P2 | Responsive Layout for All Devices |
| US4 | P3 | Eliminate Style Flash During Sign-In and Key Generation |

---

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Remove duplicate/conflicting CSS that causes unpredictable cascade behavior. This MUST be done first because later tasks modify the same CSS sections.

- [X] T001 Remove the FIRST duplicate `.reply-preview-bar` CSS rule set (lines 1220–1262) in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T001**:
- Open `src/ToledoVault/wwwroot/app.css`
- Delete the entire block from line 1220 (`/* Enhanced reply preview bar */`) through line 1262 (`.reply-preview-close:hover { ... }`)
- This includes these selectors that must ALL be removed:
  - `.reply-preview-bar` (lines 1221–1230) — uses `align-items: flex-start` + `padding: 10px 14px` (CONFLICTING with the second set)
  - `.reply-preview-content` (lines 1231–1234)
  - `.reply-sender-name` (lines 1235–1240)
  - `.reply-quote-text` (lines 1241–1247) — uses `white-space: nowrap` (single-line truncation — WRONG, we want 2-line)
  - `.reply-preview-close` (lines 1248–1258)
  - `.reply-preview-close:hover` (lines 1259–1262)
- Keep the SECOND set at lines 2944–2976 (this is the one we will refine in later tasks)
- **WHY**: Two conflicting rule sets for `.reply-preview-bar` exist in the same file. CSS cascade means the second set wins for shared properties, but the first set contributes conflicting sub-rules (e.g., `.reply-sender-name` at line 1235 sets `font-size: 0.82em` while the second set at line 2928 sets `font-size: 0.78em`). Removing the first set eliminates all cascade conflicts.
- **VERIFY**: After deletion, search the file for `.reply-preview-bar` — it should appear ONLY at/after line ~2944 (line numbers will shift after deletion). Also search for `.reply-preview-close` — should appear only once (in the second set).

**Checkpoint**: The duplicate CSS is removed. All remaining tasks can now safely modify the surviving CSS rules without cascade conflicts.

---

## Phase 2: User Story 1 — Compact Reply/Quote Message Bar (Priority: P1) 🎯 MVP

**Goal**: Redesign the reply preview bar (above message input) and the quoted message block (inside message bubbles) to match the WhatsApp/Telegram compact pattern: max height, 2-line truncation, left accent border, compact close button with 44px touch target.

**Independent Test**: Open a chat, reply to a long message — the reply bar should be compact (max 64px), text truncated to 2 lines, close button tappable. Check quoted blocks inside message bubbles are also compact (max 60px).

### Implementation for User Story 1

- [X] T002 [US1] Redesign `.reply-preview-bar` CSS to WhatsApp/Telegram compact pattern in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T002**:
- In `src/ToledoVault/wwwroot/app.css`, find the SURVIVING `.reply-preview-bar` block (around line ~2944 after T001 deletion, originally at line 2944).
- Replace the entire `.reply-preview-bar` rule set AND its child selectors (`.reply-preview-content`, `.reply-preview-bar .reply-sender-name`, `.reply-preview-bar .reply-quote-text`, `.reply-preview-close`, `.reply-preview-close:hover`) with this consolidated design:

```css
/* ============ Reply Preview Bar (above message input) ============ */
.reply-preview-bar {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 12px;
    max-height: 64px;
    overflow: hidden;
    background: color-mix(in srgb, var(--accent) 5%, transparent);
    border-left: 3px solid var(--accent);
    border-radius: 4px;
}
.reply-preview-content {
    flex: 1;
    min-width: 0;
    overflow: hidden;
}
.reply-preview-bar .reply-sender-name {
    font-weight: 600;
    font-size: 0.78em;
    color: var(--accent);
    margin-bottom: 1px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 200px;
}
.reply-preview-bar .reply-quote-text {
    font-size: 0.82em;
    color: var(--text-secondary);
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
    line-height: 1.3;
}
.reply-preview-close {
    background: none;
    border: none;
    font-size: 1.1em;
    color: var(--text-secondary);
    cursor: pointer;
    min-width: 44px;
    min-height: 44px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    flex-shrink: 0;
    line-height: 1;
    padding: 0;
}
.reply-preview-close:hover {
    background: var(--border);
    color: var(--text-primary);
}
```

- **KEY DESIGN DECISIONS**:
  - `max-height: 64px; overflow: hidden` — prevents the bar from growing beyond compact size
  - `-webkit-line-clamp: 2` — truncates message text to 2 lines (industry standard)
  - `color-mix(in srgb, var(--accent) 5%, transparent)` — subtle accent tint background, works with all 8 themes
  - `min-width: 44px; min-height: 44px` on close button — meets Apple HIG 44px touch target requirement
  - `max-width: 200px` on sender name — prevents very long names from consuming the bar
- **VERIFY**: The reply bar uses theme variables (`var(--accent)`, `var(--text-secondary)`, `var(--border)`) — no hardcoded colors. Check in browser at 375px width that bar fits without overflow.

---

- [X] T003 [US1] Refine `.reply-quote-block` CSS (quoted message inside bubbles) in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T003**:
- In `src/ToledoVault/wwwroot/app.css`, find the `/* ============ Reply Quote ============ */` section (around line ~2913 after T001 deletion).
- Update the `.reply-quote-block` rule (currently has `max-height: 60px; overflow: hidden` — keep those):

```css
.reply-quote-block {
    border-left: 3px solid var(--accent);
    background: rgba(0,0,0,0.06);
    border-radius: 4px;
    padding: 6px 10px;
    margin-bottom: 6px;
    cursor: pointer;
    max-height: 60px;
    overflow: hidden;
}
```

- Update the `.reply-sender-name` rule (the one NOT inside `.reply-preview-bar`) to add truncation:

```css
.reply-sender-name {
    font-size: 0.78em;
    font-weight: 600;
    color: var(--accent);
    margin-bottom: 2px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    max-width: 150px;
}
```

- Update the `.reply-quote-text` rule (the one NOT inside `.reply-preview-bar`) to use 2-line clamp:

```css
.reply-quote-text {
    font-size: 0.83em;
    opacity: 0.8;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
    line-height: 1.3;
}
```

- **KEY CHANGES**: Replaced `white-space: nowrap` (single-line) with `-webkit-line-clamp: 2` (2-line truncation). Added `max-width: 150px` + `text-overflow: ellipsis` on sender name to truncate long names.
- **VERIFY**: Reply to a message with very long text — the quoted block in the bubble should show max 2 lines and be capped at 60px height.

---

- [X] T004 [US1] Add media placeholder labels for reply context in `src/ToledoVault.Client/Pages/Chat.razor` (line ~1176)

**DETAILED INSTRUCTIONS for T004**:
- In `src/ToledoVault.Client/Pages/Chat.razor`, find the `SetReplyTo` method (around line 1169).
- Current code at line 1176:
```csharp
Text = msg.ContentType != ContentType.Text ? $"[{msg.ContentType}]" : (msg.Text.Length > 80 ? msg.Text[..80] + "..." : msg.Text)
```
- Replace with friendlier labels:
```csharp
Text = msg.ContentType switch
{
    ContentType.Image => "[Image]",
    ContentType.Audio => "[Voice message]",
    ContentType.Video => "[Video]",
    ContentType.File => $"[File: {msg.FileName ?? "file"}]",
    _ => msg.Text.Length > 80 ? msg.Text[..80] + "..." : msg.Text
}
```
- **CONTEXT**: The `ChatMessage` class (defined later in Chat.razor around line 1650) has a `FileName` property (`public string? FileName { get; init; }`). The `ContentType` enum has values: `Text=0, Image=1, Audio=2, Video=3, File=4`.
- **WHY**: When replying to a photo-only message or voice message, the current code shows `[Image]` or `[Audio]` which is acceptable, but `[Audio]` should be `[Voice message]` per spec clarification. Also `[File]` should include the filename.
- **VERIFY**: Reply to an image-only message — preview should show "[Image]". Reply to a voice message — should show "[Voice message]". Reply to a document — should show "[File: report.pdf]" (or similar).

---

- [X] T005 [US1] Add media placeholder labels for quoted blocks in `src/ToledoVault.Client/Components/MessageBubble.razor` (line ~13)

**DETAILED INSTRUCTIONS for T005**:
- In `src/ToledoVault.Client/Components/MessageBubble.razor`, find the reply quote block (around line 9–15):
```razor
@if (!string.IsNullOrEmpty(ReplyToSenderName))
{
    <div class="reply-quote-block" @onclick="() => OnReplyQuoteClick.InvokeAsync(ReplyToMessageId ?? 0)">
        <div class="reply-sender-name">@ReplyToSenderName</div>
        <div class="reply-quote-text">@(ReplyToText ?? "")</div>
    </div>
}
```
- The `ReplyToText` parameter already receives the formatted text from Chat.razor (which we fixed in T004). However, if `ReplyToText` is null or empty (media-only message with no text set), we should show a fallback.
- Replace line 13 with:
```razor
<div class="reply-quote-text">@(!string.IsNullOrEmpty(ReplyToText) ? ReplyToText : "Message")</div>
```
- **WHY**: This is a safety net. The primary formatting happens in Chat.razor (T004), but if `ReplyToText` arrives null/empty for any reason (e.g., older cached messages), we show "Message" instead of blank.
- **VERIFY**: View a message that quotes another message — the quoted block should show the sender name and truncated text (or media placeholder label).

---

- [X] T006 [US1] Add media placeholder labels in the reply preview bar in `src/ToledoVault.Client/Components/MessageInput.razor` (line ~15)

**DETAILED INSTRUCTIONS for T006**:
- In `src/ToledoVault.Client/Components/MessageInput.razor`, find the reply preview bar (lines 10–19):
```razor
@if (ReplyContext is not null)
{
    <div class="reply-preview-bar">
        <div class="reply-preview-content">
            <div class="reply-sender-name">@ReplyContext.SenderName</div>
            <div class="reply-quote-text">@ReplyContext.Text</div>
        </div>
        <button class="reply-preview-close" @onclick="() => OnCancelReply.InvokeAsync()" aria-label="Cancel reply">&times;</button>
    </div>
}
```
- Replace line 15 with:
```razor
<div class="reply-quote-text">@(!string.IsNullOrEmpty(ReplyContext.Text) ? ReplyContext.Text : "Message")</div>
```
- **WHY**: Same safety net as T005. The `ReplyContext.Text` is set by Chat.razor's `SetReplyTo` method (fixed in T004), but if it arrives empty, show "Message" instead of blank space.
- **VERIFY**: Click reply on a message — the preview bar above the input should show sender name and message text (or "[Image]", "[Voice message]", etc.).

**Checkpoint**: User Story 1 is complete. The reply preview bar and quoted message blocks are compact, truncated to 2 lines, and show media placeholder labels. All 8 themes should work because only CSS variables are used.

---

## Phase 3: User Story 2 — Mobile Navigation and Tap Targets (Priority: P1)

**Goal**: Fix the broken back button navigation on mobile, make the Settings button tappable on iPhone, and eliminate iOS 300ms tap delay on all interactive elements.

**Independent Test**: On iPhone (or DevTools at 375px), open the app → tap a conversation → tap back → verify chat list returns. Tap Settings → verify it navigates. Repeat back/forth 10 times rapidly — should remain stable.

### Implementation for User Story 2

- [X] T007 [US2] Replace `<a href="/chat">` back link with NavigationManager button in `src/ToledoVault.Client/Pages/Chat.razor` (line ~21)

**DETAILED INSTRUCTIONS for T007**:
- In `src/ToledoVault.Client/Pages/Chat.razor`, find line 21:
```razor
<a href="/chat" class="back-link back-link-desktop-hidden" aria-label="Back to conversations">&larr;</a>
```
- Replace with:
```razor
<button class="back-link back-link-desktop-hidden" @onclick="NavigateBack" aria-label="Back to conversations">&larr;</button>
```
- Then add a method in the `@code` block (near the existing navigation-related methods):
```csharp
private void NavigateBack()
{
    Navigation.NavigateTo("/chat");
}
```
- **WHY**: The `<a href="/chat">` tag triggers a FULL browser location change, which breaks Blazor's internal routing lifecycle. On mobile, this causes the `ConversationListSidebar.razor:OnLocationChanged` handler to fire at the wrong time, and the JS `eval` for panel switching may fail silently on iOS Safari. Using `NavigationManager.NavigateTo()` keeps everything within Blazor's routing system.
- **CSS NOTE**: The `.back-link` CSS class currently targets `a` elements. Verify that the CSS works for `<button>` too. If the `.back-link` CSS has `text-decoration` or `color` rules meant for `<a>`, add reset styles:
```css
button.back-link {
    background: none;
    border: none;
    cursor: pointer;
}
```
Add this to `src/ToledoVault/wwwroot/app.css` near the existing `.back-link` rules if needed.
- **VERIFY**: On mobile (375px), tap a conversation to open it, then tap the back arrow — the conversation list should reappear without errors or blank screens.

---

- [X] T008 [US2] Add error recovery and logging to `UpdateMobileLayout()` in `src/ToledoVault.Client/Components/ConversationListSidebar.razor` (lines 208–225)

**DETAILED INSTRUCTIONS for T008**:
- In `src/ToledoVault.Client/Components/ConversationListSidebar.razor`, find the `UpdateMobileLayout` method (lines 208–225):
```csharp
private async Task UpdateMobileLayout()
{
    try
    {
        var inChat = IsInChatPage();
        await Js.InvokeVoidAsync("eval",
            "(function(){" +
            "var s=document.getElementById('chat-sidebar');" +
            "var p=document.getElementById('chat-panel');" +
            $"if(s)s.classList.{(inChat ? "add" : "remove")}('hide-mobile');" +
            $"if(p)p.classList.{(inChat ? "add" : "remove")}('show-mobile');" +
            "})()");
    }
    catch
    {
        // JS interop may fail during prerender or initial load
    }
}
```
- Replace with error recovery (retry once after 100ms) and logging:
```csharp
private async Task UpdateMobileLayout()
{
    var inChat = IsInChatPage();
    var jsCode = "(function(){" +
        "var s=document.getElementById('chat-sidebar');" +
        "var p=document.getElementById('chat-panel');" +
        $"if(s)s.classList.{(inChat ? "add" : "remove")}('hide-mobile');" +
        $"if(p)p.classList.{(inChat ? "add" : "remove")}('show-mobile');" +
        "})()";

    try
    {
        await Js.InvokeVoidAsync("eval", jsCode);
    }
    catch
    {
        // Retry once after 100ms — iOS Safari + Blazor WASM interop has timing issues
        try
        {
            await Task.Delay(100);
            await Js.InvokeVoidAsync("eval", jsCode);
        }
        catch
        {
            // Still failed — log and continue. Panel switching may need manual refresh.
            Console.WriteLine($"[ConversationListSidebar] UpdateMobileLayout failed. inChat={inChat}, path={_currentPath}");
        }
    }
}
```
- **WHY**: The original `catch {}` silently swallows errors. On iOS Safari, JS interop can fail due to timing issues during navigation. The retry gives one more chance. If it still fails, we log instead of hiding the error entirely.
- **VERIFY**: Navigate back and forth between chat and chat list on mobile 10+ times — should never get stuck on a blank screen. Check browser console for any `[ConversationListSidebar]` log messages (none expected under normal operation).

---

- [X] T009 [P] [US2] Fix sidebar button touch targets (44px minimum) in `src/ToledoVault/wwwroot/app.css` (lines ~2021–2040)

**DETAILED INSTRUCTIONS for T009**:
- In `src/ToledoVault/wwwroot/app.css`, find `.sidebar-header-actions` (line ~2021) and `.sidebar-icon-btn` (line ~2026).
- Update `.sidebar-header-actions` to increase gap:
```css
.sidebar-header-actions {
    margin-left: auto;
    display: flex;
    gap: 8px;
}
```
(Changed `gap` from `4px` to `8px`)

- Update `.sidebar-icon-btn` to add 44px minimum touch target:
```css
.sidebar-icon-btn {
    background: none;
    border: none;
    color: var(--nav-text);
    opacity: 0.85;
    padding: 8px;
    border-radius: 50%;
    cursor: pointer;
    font-size: 1.2em;
    line-height: 1;
    min-width: 44px;
    min-height: 44px;
    display: flex;
    align-items: center;
    justify-content: center;
}
```
(Added `min-width: 44px; min-height: 44px; display: flex; align-items: center; justify-content: center`)

- **WHY**: Apple Human Interface Guidelines require 44pt minimum touch targets. The current `.sidebar-icon-btn` has only `padding: 8px` on a ~19px icon, resulting in a ~35px tap area. This causes missed taps on iOS, especially for the Settings button.
- **VERIFY**: In DevTools, inspect the Settings button (⚙) — the rendered element should be at least 44×44px. On iPhone, the Settings button should respond to tap on the first attempt.

---

- [X] T010 [P] [US2] Add global `touch-action: manipulation` rule in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T010**:
- In `src/ToledoVault/wwwroot/app.css`, near the top of the file (after the `*, *::before, *::after` reset rules — look for the first few global rules), add:
```css
/* Eliminate iOS 300ms tap delay on all interactive elements */
button, a, [role="button"], input, select, textarea {
    touch-action: manipulation;
}
```
- Place this AFTER the existing global reset/normalize rules but BEFORE any component-specific rules. A good location is after the `body` rules (around line 10–20) or after the `*, *::before, *::after` block.
- **WHY**: iOS Safari has a 300ms delay on tap events to detect double-tap zoom. `touch-action: manipulation` tells the browser "this element only needs pan and pinch-zoom, not double-tap-zoom," eliminating the delay. Without this, all buttons and links feel sluggish on iPhone.
- **VERIFY**: On iPhone Safari (or DevTools mobile emulation), tap any button — it should respond immediately with no perceptible delay.

**Checkpoint**: User Story 2 is complete. Back navigation works reliably on mobile, Settings is tappable with 44px touch targets, and all buttons respond instantly on iOS.

---

## Phase 4: User Story 3 — Responsive Layout for All Devices (Priority: P2)

**Goal**: Ensure no horizontal overflow at any viewport from 320px to 1920px, all elements usable on mobile with 44px+ touch targets.

**Independent Test**: Open every page at 320px, 375px, 480px, 768px, 1280px — verify no horizontal scrollbar, all buttons tappable, text readable.

### Implementation for User Story 3

- [ ] T011 [US3] Audit and fix horizontal overflow at 320px width across all pages in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T011**:
- Open the app in browser DevTools at 320px width (iPhone SE). Navigate to each page:
  1. `/login` — Check form inputs, buttons, and auth footer fit within 320px
  2. `/register` — Check form inputs, password strength bar, buttons fit
  3. `/chat` (conversation list) — Check sidebar items, search bar, header actions fit
  4. `/chat/{id}` (individual chat) — Check message bubbles, input area, header fit
  5. `/settings` — Check all settings controls fit
  6. `/new-conversation` — Check user list items fit
  7. `/security-info` — Check info cards fit
- For each page, check for:
  - Horizontal scrollbar (should never appear)
  - Elements overflowing past the right edge
  - Fixed `width` or `min-width` values that are too large for 320px
  - `padding` that combined with content exceeds viewport
- Common fixes to apply in `src/ToledoVault/wwwroot/app.css`:
  - Replace fixed `width` with `max-width: 100%`
  - Add `overflow-wrap: break-word` to text containers
  - Use `box-sizing: border-box` (should already be global)
  - Reduce `padding` on small screens using media queries: `@media (max-width: 480px) { ... }`
- **NOTE**: This is an audit task — the specific fixes depend on what issues you find. Document each fix you make.
- **VERIFY**: At 320px width, navigate through all pages — NO horizontal scrollbar on any page.

---

- [ ] T012 [US3] Verify voice recorder responsive behavior at 320px in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T012**:
- At 320px viewport, start a voice recording in a chat.
- Check that the recording bar (waveform bars, timer, controls) fits within the viewport.
- If the waveform overflows, adjust the CSS:
  - The waveform uses 30 bars (defined in `MessageBubble.razor` line 72: `@for (var i = 0; i < 30; i++)`). Each bar has a class `.aw-bar`.
  - In `src/ToledoVault/wwwroot/app.css`, find the `.audio-wave-track` and `.aw-bar` rules.
  - Add responsive adjustments if needed:
    ```css
    @media (max-width: 375px) {
        .audio-wave-track {
            gap: 1px;
        }
        .aw-bar {
            width: 2px;
        }
    }
    ```
- Also check the voice recorder controls (in `VoiceRecorder.razor` component) at 320px — cancel, timer, and stop buttons should all fit.
- **VERIFY**: At 320px width, the voice recorder bar, timer, waveform, and controls fit without overflow.

---

- [ ] T013 [US3] Ensure overlay sizing is viewport-aware on mobile in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T013**:
- Check these overlays at 320px, 375px, and 480px viewports:
  1. **Emoji picker** — Should not exceed viewport width/height. Look for `.emoji-picker` CSS rules and ensure it has `max-width: calc(100vw - 20px); max-height: calc(100vh - 100px)` or similar viewport-relative values instead of fixed pixel sizes.
  2. **Forward dialog** — Look for the forward/share dialog CSS and ensure it's viewport-aware.
  3. **Image lightbox** — When tapping an image to view full-size, the lightbox should fit the viewport. Look for `.lightbox` or `.image-overlay` CSS rules and ensure `max-width: 100vw; max-height: 100vh`.
  4. **Context menu** — The right-click/long-press context menu should not overflow off-screen on small viewports.
- For each overlay that uses fixed pixel sizes, change to viewport-relative values.
- **VERIFY**: At 320px width, open each overlay — all should fit within the viewport and be dismissible.

---

- [ ] T014 [US3] Verify tablet breakpoint (768px) panel switching in `src/ToledoVault/wwwroot/app.css`

**DETAILED INSTRUCTIONS for T014**:
- In DevTools, test at exactly 767px and 768px viewports:
  - At 767px: Should show single-panel mode (only sidebar OR chat panel visible, not both)
  - At 768px: Should show two-panel mode (sidebar AND chat panel side by side)
- Look for the media query breakpoint in `src/ToledoVault/wwwroot/app.css` — search for `@media` rules near the mobile layout section.
- The existing breakpoint should be `@media (max-width: 768px)` or `@media (min-width: 769px)`.
- Verify the CSS correctly handles the boundary:
  - `hide-mobile` class should only apply at mobile breakpoint
  - `show-mobile` class should only apply at mobile breakpoint
  - At 768px+, both panels should be visible regardless of these classes
- If the breakpoint is wrong or the transition is janky, fix the media query values.
- **VERIFY**: At 767px = single panel. At 768px = two panels. Toggle between 767px and 769px rapidly — layout should switch cleanly without flicker.

**Checkpoint**: User Story 3 is complete. All pages render without horizontal scrollbar at all test viewports, overlays are viewport-aware, and the tablet breakpoint works correctly.

---

## Phase 5: User Story 4 — Eliminate Style Flash During Sign-In and Key Generation (Priority: P3)

**Goal**: Stable visual experience during sign-in flow (no layout shift when status messages appear) and a branded loading spinner during WASM download.

**Independent Test**: Sign in to the app — watch for layout shifts during "Generating encryption keys..." phase. Hard refresh (Ctrl+Shift+R) — verify a spinner shows during WASM download.

### Implementation for User Story 4

- [X] T015 [US4] Add WASM loading spinner to `src/ToledoVault/Components/App.razor`

**DETAILED INSTRUCTIONS for T015**:
- In `src/ToledoVault/Components/App.razor`, find `<body>` (line 59). Currently:
```html
<body>
    <Routes />
```
- Replace with:
```html
<body>
    <div id="app-loading" style="display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;background:var(--bg-primary, #0f172a);color:var(--text-primary, #e2e8f0);font-family:system-ui,-apple-system,sans-serif;">
        <div style="width:40px;height:40px;border:3px solid rgba(255,255,255,0.1);border-top-color:var(--accent, #3b82f6);border-radius:50%;animation:app-spin 0.8s linear infinite;"></div>
        <div style="margin-top:16px;font-size:1.1em;font-weight:500;letter-spacing:0.5px;">ToledoVault</div>
    </div>
    <style>
        @keyframes app-spin {
            to { transform: rotate(360deg); }
        }
    </style>
    <Routes />
```
- Then add a script AFTER the Blazor script tag (after `<script src="@Assets["_framework/blazor.web.js"]"></script>`) to hide the loading div once Blazor is initialized:
```html
<script>
    // Hide loading spinner once Blazor WASM is fully initialized
    if (document.querySelector('#app-loading')) {
        var hideLoader = function() {
            var el = document.getElementById('app-loading');
            if (el) el.style.display = 'none';
        };
        // Blazor fires this event when the WASM runtime is ready
        document.addEventListener('blazor:initialized', hideLoader);
        // Fallback: hide after 10 seconds in case the event doesn't fire
        setTimeout(hideLoader, 10000);
    }
</script>
```
- **WHY**: Currently, during WASM download (~1-2 seconds on first load), the page is completely blank. This spinner provides immediate visual feedback using pure inline styles (no dependency on app.css which may not be loaded yet). The `var(--bg-primary, #0f172a)` syntax uses theme color with a dark fallback.
- **NOTE**: Using inline styles intentionally — the spinner must display BEFORE the CSS file loads. The CSS file (`app.css`) is loaded via `<link rel="stylesheet">` which may take time.
- **VERIFY**: Hard refresh (Ctrl+Shift+R) on the login page — a centered spinner with "ToledoVault" text should appear immediately, then transition to the rendered page once WASM loads.

---

- [X] T016 [P] [US4] Pre-allocate status message space on Login page in `src/ToledoVault.Client/Pages/Login.razor` (lines 18–26)

**DETAILED INSTRUCTIONS for T016**:
- In `src/ToledoVault.Client/Pages/Login.razor`, find the status message rendering (lines 18–26):
```razor
@if (!string.IsNullOrEmpty(_errorMessage))
{
    <div class="alert alert-error" role="alert">@_errorMessage</div>
}

@if (!string.IsNullOrEmpty(_statusMessage))
{
    <div class="alert alert-info" role="status">@_statusMessage</div>
}
```
- Replace with an always-present container that uses visibility instead of conditional rendering:
```razor
<div class="auth-status-area" style="min-height:48px;">
    <div class="alert alert-error @(string.IsNullOrEmpty(_errorMessage) ? "alert-hidden" : "")" role="alert">
        @(_errorMessage ?? "\u00A0")
    </div>
    <div class="alert alert-info @(string.IsNullOrEmpty(_statusMessage) ? "alert-hidden" : "")" role="status">
        @(_statusMessage ?? "\u00A0")
    </div>
</div>
```
- Then add the `.alert-hidden` and `.auth-status-area` CSS in `src/ToledoVault/wwwroot/app.css` (near the existing `.alert` rules around line ~217):
```css
.auth-status-area {
    min-height: 48px;
}
.alert-hidden {
    visibility: hidden;
    height: 0;
    min-height: 0;
    padding: 0;
    margin: 0;
    overflow: hidden;
}
```
- **WHY**: The current conditional rendering (`@if (!string.IsNullOrEmpty(...))`) inserts/removes the alert DOM element, causing layout shift (CLS). The new approach always has the container in the DOM but hides it with `visibility: hidden; height: 0`. When the message is set, the CSS class is removed and the alert smoothly appears. The `min-height: 48px` on the container reserves space so the form below doesn't jump.
- **NOTE**: `\u00A0` is a non-breaking space — prevents the alert from collapsing to zero height when empty (not needed when hidden, but used as fallback content).
- **VERIFY**: Sign in to the app. Watch the form during "Setting up encryption keys..." and "Generating encryption keys..." messages — the form elements below should NOT shift position.

---

- [X] T017 [P] [US4] Pre-allocate status message space on Register page in `src/ToledoVault.Client/Pages/Register.razor` (lines 15–19)

**DETAILED INSTRUCTIONS for T017**:
- In `src/ToledoVault.Client/Pages/Register.razor`, find the error message rendering (lines 15–19):
```razor
@if (!string.IsNullOrEmpty(_errorMessage))
{
    <div class="alert alert-error" role="alert">@_errorMessage</div>
}
```
- Replace with an always-present container (same pattern as Login):
```razor
<div class="auth-status-area" style="min-height:48px;">
    <div class="alert alert-error @(string.IsNullOrEmpty(_errorMessage) ? "alert-hidden" : "")" role="alert">
        @(_errorMessage ?? "\u00A0")
    </div>
</div>
```
- **NOTE**: Register.razor only has `_errorMessage` (no `_statusMessage`), so we only need one alert. The `.auth-status-area` and `.alert-hidden` CSS classes were already added in T016.
- **VERIFY**: Register a new account — if registration fails, the error message should appear without shifting the form below.

---

- [X] T018 [P] [US4] Stabilize alert animation to prevent layout reflow in `src/ToledoVault/wwwroot/app.css` (line ~1699)

**DETAILED INSTRUCTIONS for T018**:
- In `src/ToledoVault/wwwroot/app.css`, find the `@keyframes fadeIn` animation (around line 1699):
```css
@keyframes fadeIn {
    from { opacity: 0; transform: translateY(4px); }
    to { opacity: 1; transform: translateY(0); }
}
```
- Change to opacity-only animation (remove `translateY` which causes layout reflow):
```css
@keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
}
```
- **WHY**: The `translateY(4px)` in the animation causes the element to shift position during the 0.25s animation, which triggers layout recalculation on surrounding elements. This is one contributor to the "style flash" during sign-in. Opacity-only animation is composited on the GPU and doesn't affect layout.
- **NOTE**: This animation is used by the `.alert` class (line ~222: `animation: fadeIn 0.25s ease-out`) and the `.fade-in` class (line ~1737). Changing it affects all elements using `fadeIn`. The visual effect changes from "slide up + fade in" to just "fade in" — this is an acceptable trade-off for layout stability.
- **VERIFY**: Sign in and watch the status messages appear — they should fade in smoothly without any vertical movement or layout shift.

**Checkpoint**: User Story 4 is complete. WASM loading shows a spinner, sign-in status messages don't cause layout shift, and animations don't trigger reflow.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Verify all changes work across themes and devices. No new features — only testing and minor fixes discovered during testing.

- [ ] T019 Verify all changes across all 8 themes in `src/ToledoVault/wwwroot/app.css` and `src/ToledoVault/wwwroot/themes.css`

**DETAILED INSTRUCTIONS for T019**:
- Open the app and switch through all 8 themes (via Settings page):
  1. Default (light)
  2. Default Dark
  3. And the other 6 themes defined in `themes.css`
- For each theme, verify:
  - Reply preview bar: background tint is visible but subtle, accent border is correct color, sender name uses accent color, text is readable
  - Quoted message block in bubbles: background is visible, border color is correct
  - Sidebar buttons: icon color is visible and tappable
  - Loading spinner: background color matches theme (or dark fallback is acceptable)
  - Auth pages: alerts are visible and readable
- If any hardcoded colors were introduced (not using `var(--...)` variables), replace them with the appropriate CSS variable.
- **VERIFY**: All 8 themes display correctly with no hardcoded colors visible.

---

- [ ] T020 Run full mobile browser test per `specs/003-fix-styles/quickstart.md`

**DETAILED INSTRUCTIONS for T020**:
- Follow the complete testing guide in `specs/003-fix-styles/quickstart.md`:
  1. **Reply Bar**: Reply to long message → compact bar (max 64px). Reply to image → shows "[Image]". Check in-bubble quoted block.
  2. **Mobile Navigation**: DevTools iPhone 12 (375px) → tap conversation → tap back → verify. Repeat 10x. Tap Settings → verify first-tap response.
  3. **Responsive Layout**: Test at 320px, 375px, 480px, 768px, 1280px → no horizontal scrollbar.
  4. **Style Flash**: Sign in → watch for layout shifts during key generation.
  5. **WASM Loading**: Hard refresh → verify spinner shows.
- Document any issues found and fix them before marking complete.
- **VERIFY**: All quickstart.md test scenarios pass.

---

- [ ] T021 Regression check — verify existing features still work

**DETAILED INSTRUCTIONS for T021**:
- Test these existing features to ensure our CSS/markup changes didn't break them:
  1. **Voice recorder**: Start recording, see waveform, stop, send — verify it works
  2. **Emoji picker**: Open emoji picker, select emoji, verify it inserts into message
  3. **Image preview**: Send an image, click to view in lightbox — verify it opens/closes
  4. **Forward dialog**: Long-press/right-click a message, forward to another chat — verify dialog works
  5. **File attachment**: Attach a document, send it — verify the file card renders
  6. **Link preview**: Send a message with a URL — verify link preview renders
  7. **Reactions**: React to a message — verify reaction badge appears
  8. **Search**: Use the search bar in the sidebar — verify it filters conversations
- If any feature is broken, fix it before marking complete.
- **VERIFY**: All existing features work correctly after our changes.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — MUST complete first (removes duplicate CSS)
- **Phase 2 (US1 - Reply Bar)**: Depends on Phase 1 — modifies the CSS rules that remain after duplicate removal
- **Phase 3 (US2 - Mobile Nav)**: Can start after Phase 1 — modifies different files (Chat.razor, ConversationListSidebar.razor) and different CSS sections
- **Phase 4 (US3 - Responsive)**: Can start after Phase 1 — audits and fixes CSS, may overlap with US1/US2 CSS areas
- **Phase 5 (US4 - Style Flash)**: Can start after Phase 1 — modifies different files (App.razor, Login.razor, Register.razor) and different CSS sections
- **Phase 6 (Polish)**: Depends on ALL user stories being complete

### User Story Dependencies

- **US1 (Reply Bar)**: Depends on Phase 1 (T001). No dependencies on other stories.
- **US2 (Mobile Nav)**: Depends on Phase 1 (T001). No dependencies on other stories.
- **US3 (Responsive)**: Depends on Phase 1 (T001). Best done AFTER US1 and US2 since those add new CSS rules that should also be responsive.
- **US4 (Style Flash)**: Depends on Phase 1 (T001). No dependencies on other stories.

### Within Each User Story

- Tasks within a story should be executed in order (T002→T003→T004→T005→T006 for US1)
- Tasks marked [P] within the same story can run in parallel with other [P] tasks

### Task Dependency Graph

```
T001 (remove duplicate CSS)
 ├── T002 → T003 → T004 → T005 → T006  (US1: Reply Bar)
 ├── T007 → T008                         (US2: Mobile Nav - sequential)
 │   T009 ──────┐                        (US2: Touch targets - parallel)
 │   T010 ──────┘                        (US2: touch-action - parallel)
 ├── T011 → T012 → T013 → T014          (US3: Responsive - sequential audit)
 ├── T015                                (US4: WASM spinner)
 │   T016 ──────┐                        (US4: Login pre-allocate - parallel)
 │   T017 ──────┤                        (US4: Register pre-allocate - parallel)
 │   T018 ──────┘                        (US4: Alert animation - parallel)
 └── T019 → T020 → T021                  (Polish: sequential testing)
```

### Parallel Opportunities

After T001 completes, these groups can run IN PARALLEL (they modify different files):

**Parallel Group A** (CSS-heavy — same file, do sequentially within group):
- T002, T003 (US1 CSS in app.css)
- T009, T010 (US2 CSS in app.css)
- T018 (US4 CSS in app.css)

**Parallel Group B** (Razor files — different files, can parallel):
- T004 (Chat.razor — US1)
- T005 (MessageBubble.razor — US1)
- T006 (MessageInput.razor — US1)
- T007 (Chat.razor — US2, but same file as T004! Do after T004)
- T008 (ConversationListSidebar.razor — US2)
- T015 (App.razor — US4)
- T016 (Login.razor — US4)
- T017 (Register.razor — US4)

**IMPORTANT**: T004 and T007 both modify `Chat.razor` — do T004 first, then T007.

---

## Parallel Example: User Story 1

```text
# After T001 completes, launch CSS tasks sequentially (same file):
T002: Redesign .reply-preview-bar CSS in app.css
T003: Refine .reply-quote-block CSS in app.css (after T002)

# In parallel with CSS, launch Razor tasks (different files):
T004: Add media placeholders in Chat.razor
T005: Add media placeholders in MessageBubble.razor  (parallel with T004)
T006: Add media placeholders in MessageInput.razor   (parallel with T004, T005)
```

## Parallel Example: User Story 4

```text
# These all modify different files — run in parallel:
T015: WASM spinner in App.razor
T016: Pre-allocate status in Login.razor
T017: Pre-allocate status in Register.razor
T018: Stabilize fadeIn animation in app.css
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: T001 (remove duplicate CSS)
2. Complete Phase 2: T002–T006 (reply bar redesign)
3. **STOP and VALIDATE**: Test reply bar at all viewports, all themes
4. Deploy/demo if ready

### Incremental Delivery

1. T001 → Foundation ready
2. T002–T006 → US1 complete → Test → Deploy (MVP!)
3. T007–T010 → US2 complete → Test → Deploy
4. T011–T014 → US3 complete → Test → Deploy
5. T015–T018 → US4 complete → Test → Deploy
6. T019–T021 → Polish → Final Deploy

### Recommended Single-Developer Order

For a single developer working sequentially:

1. **T001** — Remove duplicate CSS (foundation)
2. **T002, T003** — Reply bar CSS (same file, do together)
3. **T009, T010, T018** — Touch targets, touch-action, fadeIn animation (more CSS in same file)
4. **T004** — Media placeholders in Chat.razor
5. **T007** — Back button in Chat.razor (same file as T004)
6. **T005** — Media placeholders in MessageBubble.razor
7. **T006** — Media placeholders in MessageInput.razor
8. **T008** — Error recovery in ConversationListSidebar.razor
9. **T015** — WASM spinner in App.razor
10. **T016** — Pre-allocate status in Login.razor
11. **T017** — Pre-allocate status in Register.razor
12. **T011–T014** — Responsive audit (best done after all other changes)
13. **T019–T021** — Polish and testing

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All CSS changes use theme variables (`var(--accent)`, `var(--text-secondary)`, etc.) — no hardcoded colors
- The `color-mix()` function in T002 has wide browser support (Chrome 111+, Safari 16.2+, Firefox 113+) — acceptable for this app's target browsers
- `-webkit-line-clamp` is supported in all modern browsers (Chrome, Safari, Firefox, Edge)
- `touch-action: manipulation` is supported in all modern mobile browsers
