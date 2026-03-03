# Research: 003-fix-styles

**Date**: 2026-03-03

## R1: Reply Bar Duplicate CSS

**Decision**: Remove the first set of CSS rules (lines ~1220-1262) and keep only the second set (lines ~2944-2976), then refine to WhatsApp/Telegram pattern.

**Rationale**: Two conflicting `.reply-preview-bar` rule sets exist in app.css. The first uses `align-items: flex-start` + `padding: 10px 14px`, the second uses `align-items: center` + `padding: 8px 12px`. CSS cascade means the second wins, but the first contributes conflicting sub-rules for child elements. A single consolidated rule set eliminates the conflict.

**Alternatives considered**:
- Merge both rule sets (rejected: introduces risk of keeping conflicting values)
- Use scoped component CSS (rejected: reply bar is in shared app.css, used by multiple components)

## R2: Mobile Navigation Back Button Bug

**Decision**: The back navigation issue is caused by multiple factors:
1. `<a href="/chat">` in Chat.razor triggers a full location change instead of Blazor navigation
2. `ConversationListSidebar.razor:OnLocationChanged` uses `async void` + JS `eval` to toggle mobile panel classes
3. The `catch {}` on the JS interop silently swallows errors — if the eval fails on iOS Safari, the panel stays stuck
4. No `touch-action: manipulation` CSS to eliminate iOS 300ms tap delay

**Fix approach**:
- Replace `<a href="/chat">` with `NavigationManager.NavigateTo("/chat")` for proper Blazor routing
- Add error recovery: if JS eval fails, retry once or use CSS-only fallback
- Add `touch-action: manipulation` to all interactive elements
- Ensure `show-mobile` / `hide-mobile` classes are correctly toggled

**Rationale**: iOS Safari + Blazor WASM interop has known timing issues. Using Blazor's own navigation manager instead of raw `<a>` tags gives more control over the component lifecycle.

**Alternatives considered**:
- Browser `history.back()` (rejected: doesn't integrate with Blazor router state)
- CSS-only panel switching without JS (rejected: needs state awareness of current route)

## R3: Settings Button Touch Target

**Decision**: The `.sidebar-icon-btn` has only `padding: 8px` with no explicit `min-width`/`min-height`. The actual touch target is ~35x35px, below the 44px minimum. The `gap: 4px` between buttons is also too tight.

**Fix approach**:
- Set `min-width: 44px; min-height: 44px` on `.sidebar-icon-btn`
- Increase `gap` in `.sidebar-header-actions` from 4px to 8px
- Add `touch-action: manipulation` to prevent 300ms delay

**Rationale**: Apple HIG requires 44pt minimum touch targets. The current 35px target causes missed taps on iOS.

## R4: WASM Loading Indicator

**Decision**: Add a centered spinner with app name to `App.razor` body, before `<Routes />`. The spinner auto-hides once Blazor renders.

**Rationale**: Currently there is NO loading indicator — the page is blank during WASM download. A simple CSS-only spinner in the initial HTML (not dependent on WASM) provides immediate visual feedback.

**Approach**: Add a `<div id="app-loading">` with CSS spinner + "ToledoMessage" text. Hide it via `blazor:initialized` event or by the Routes component rendering over it.

**Alternatives considered**:
- Skeleton screen (rejected: complex to maintain, overkill for 1-2 second load)
- Progress bar (rejected: no reliable API for WASM download percentage)

## R5: Style Flash During Sign-In / Key Generation

**Decision**: The flash is caused by two factors:
1. The `.alert-info` status message (`"Generating encryption keys..."`) uses `animation: fadeIn 0.25s` and is dynamically inserted, pushing layout
2. WASM component rendering causes brief unstyled state during route transitions

**Fix approach**:
- Pre-allocate space for the status message area with `min-height` on the auth page container
- Replace the dynamic `@if (!string.IsNullOrEmpty(_statusMessage))` with an always-present container that uses `visibility` or `opacity` instead of conditional rendering
- Ensure theme initialization script (already in App.razor) runs synchronously before any rendering

**Rationale**: Layout shift occurs because the alert element is conditionally rendered — it doesn't exist in the DOM until `_statusMessage` is set. Reserving space eliminates CLS.

## R6: WhatsApp/Telegram Reply Bar Pattern

**Decision**: Adopt the standard pattern:
- Max height: 64px (reply preview bar above input), 60px (quoted block in bubble)
- Left accent border: 3px solid var(--accent)
- Sender name: bold, 12-13px, accent color
- Message preview: 13px, muted gray, `-webkit-line-clamp: 2`
- Close button: 24px icon with 44px tap area (via padding)
- Background: subtle tint of accent color at 5% opacity
- Layout: flexbox row, gap 12px, padding 8px 12px

**Rationale**: Industry standard pattern used by WhatsApp Web, Telegram Web, Discord, and Slack. Users expect this layout.
