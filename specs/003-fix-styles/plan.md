# Implementation Plan: Fix Styles

**Branch**: `003-fix-styles` | **Date**: 2026-03-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-fix-styles/spec.md`

## Summary

Fix four categories of style/UX issues: (1) oversized reply/quote bar — redesign to WhatsApp/Telegram compact pattern, (2) mobile navigation bugs — back button broken, Settings not tappable on iPhone, (3) responsive layout polish for all viewports 320px–1920px, (4) style flash during sign-in/key generation and blank WASM loading screen. All changes are CSS, HTML markup, and Blazor component behavior — no data model or API changes.

## Technical Context

**Language/Version**: C# / .NET 10, CSS3, JavaScript (Blazor WASM interop)
**Primary Dependencies**: Blazor WebAssembly, ASP.NET Core, SignalR
**Storage**: N/A (no data model changes)
**Testing**: Manual visual testing at 5 viewports (320px, 375px, 480px, 768px, 1280px) + iPhone Safari
**Target Platform**: Web (mobile Safari/Chrome primary, desktop Chrome/Firefox/Edge secondary)
**Project Type**: Web application (Blazor WebAssembly hosted)
**Performance Goals**: CLS score = 0 during sign-in, all taps respond on first touch
**Constraints**: Must work across all 8 themes, no regression to existing functionality
**Scale/Scope**: ~10 files modified (CSS + Razor components), 0 new files except loading spinner markup

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|------|--------|-------|
| I. Zero-Trust Server | PASS | No server changes — all modifications are client-side CSS/HTML/JS |
| II. Hybrid Cryptography | PASS | No crypto changes |
| III. Established Libraries | PASS | No new libraries added |
| IV. Signal Protocol | PASS | No protocol changes |
| V. .NET Ecosystem | PASS | Uses existing Blazor WASM + ASP.NET Core stack |
| VI. Test-First | PASS | Visual testing plan defined; no unit-testable logic changes |
| VII. Open-Source | PASS | All changes are in public codebase |

No gate violations. No complexity tracking needed.

## Project Structure

### Documentation (this feature)

```text
specs/003-fix-styles/
├── plan.md              # This file
├── research.md          # Phase 0 output - root cause analysis
├── data-model.md        # Phase 1 output - affected files (no DB changes)
├── quickstart.md        # Phase 1 output - testing guide
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (files to modify)

```text
src/ToledoVault/
├── Components/
│   └── App.razor                          # Add WASM loading spinner
└── wwwroot/
    └── app.css                            # Reply bar, responsive, touch targets, spinner CSS

src/ToledoVault.Client/
├── Components/
│   ├── MessageInput.razor                 # Reply preview bar markup (media placeholder)
│   ├── MessageBubble.razor                # Quoted block markup
│   └── ConversationListSidebar.razor      # Mobile panel switching fix
└── Pages/
    ├── Chat.razor                         # Back button: <a> → NavigationManager
    ├── Login.razor                        # Pre-allocate status message space
    └── Register.razor                     # Pre-allocate status message space
```

**Structure Decision**: Existing project structure. No new projects, folders, or files beyond loading spinner markup in App.razor.

## Implementation Phases

### Phase A: Reply/Quote Bar Redesign (P1) — FR-001 through FR-006a

**Goal**: Single compact reply bar matching WhatsApp/Telegram pattern.

**Tasks**:

1. **Remove duplicate CSS**: Delete the first `.reply-preview-bar` rule set (~lines 1220-1262 in app.css). Keep only the second set (~lines 2944-2976).

2. **Redesign reply preview bar CSS** to WhatsApp/Telegram pattern:
   - `max-height: 64px; overflow: hidden`
   - `padding: 8px 12px; gap: 12px`
   - `border-left: 3px solid var(--accent)`
   - `background: color-mix(in srgb, var(--accent) 5%, transparent)` or theme-aware subtle tint
   - `.reply-sender-name`: `font-weight: 600; font-size: 0.78em; color: var(--accent)`
   - `.reply-quote-text`: `font-size: 0.82em; color: var(--text-secondary); display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden`
   - `.reply-preview-close`: `min-width: 44px; min-height: 44px; display: flex; align-items: center; justify-content: center; font-size: 1.1em`

3. **Refine quoted message block** (`.reply-quote-block` in bubbles):
   - `max-height: 60px; overflow: hidden`
   - Same line-clamp truncation for text
   - Sender name truncated with `text-overflow: ellipsis; max-width: 150px`

4. **Add media placeholder labels** in `MessageInput.razor`:
   - When `ReplyContext.Text` is null/empty and message has media type, show "[Image]", "[Voice message]", or "[File: filename]"
   - Same logic in `MessageBubble.razor` for the quoted block

### Phase B: Mobile Navigation Fixes (P1) — FR-007 through FR-010

**Goal**: Reliable back navigation and tappable Settings on iPhone.

**Tasks**:

5. **Replace back link in Chat.razor**: Change `<a href="/chat">` to a `<button>` with `@onclick` that calls `NavigationManager.NavigateTo("/chat")`. This gives Blazor control of the routing lifecycle.

6. **Fix mobile panel switching in ConversationListSidebar.razor**:
   - Add error recovery to `UpdateMobileLayout()`: if JS eval fails, retry once after 100ms
   - Log the error instead of silently swallowing it in `catch {}`
   - Ensure `show-mobile` is removed from `chat-panel` when navigating back to `/chat`

7. **Fix sidebar button touch targets**:
   - `.sidebar-icon-btn`: add `min-width: 44px; min-height: 44px; display: flex; align-items: center; justify-content: center`
   - `.sidebar-header-actions`: increase `gap` from 4px to 8px
   - Add `touch-action: manipulation` to all buttons and interactive elements globally

8. **Add global touch-action rule** in app.css:
   - `button, a, [role="button"], input, select, textarea { touch-action: manipulation; }` — eliminates iOS 300ms tap delay

### Phase C: Responsive Layout Polish (P2) — FR-011 through FR-015

**Goal**: No overflow at any viewport, all elements usable on mobile.

**Tasks**:

9. **Audit all pages at 320px width**: Check each page (chat, chat list, settings, login, register, new conversation, security info) for horizontal overflow. Fix any `width`, `min-width`, or `padding` values that cause overflow on small screens.

10. **Voice recorder responsive refinement**: Verify waveform bars, timer, and controls fit at 320px. Adjust bar count/width and control spacing if needed.

11. **Overlay sizing on mobile**: Ensure emoji picker, forward dialog, and image lightbox are viewport-aware (max-width/max-height relative to viewport, not fixed px values).

12. **Tablet breakpoint (768px)**: Verify the two-panel layout correctly switches to single-panel mode. Test the exact boundary — at 767px should be single panel, at 768px should be two-panel.

### Phase D: Style Flash & WASM Loading (P3) — FR-016 through FR-019

**Goal**: Stable visual experience during sign-in and initial load.

**Tasks**:

13. **Add WASM loading spinner to App.razor**:
    - Add a `<div id="app-loading">` before `<Routes />` with pure CSS spinner + "ToledoVault" text
    - CSS: centered, theme-aware background, spinner animation
    - Auto-hide via: `blazor:initialized` event listener that sets `display: none` on the loading div

14. **Pre-allocate status message space on auth pages**:
    - In `Login.razor` and `Register.razor`: replace `@if (!string.IsNullOrEmpty(_statusMessage))` with an always-present container
    - Use `min-height: 48px` on the status area
    - Toggle visibility with `opacity` and `visibility` instead of conditional rendering
    - This prevents layout shift when the message appears

15. **Stabilize alert animation**: Change `.alert` animation from `fadeIn` (which involves `translateY`) to `opacity` only — prevents layout reflow.

### Phase E: Cross-cutting & Testing

16. **Theme verification**: Test all changes across all 8 themes to ensure no hardcoded colors were introduced.

17. **Mobile browser testing**: Test on iPhone Safari and Chrome mobile (or DevTools emulation) at all 5 viewports.

18. **Regression check**: Verify existing features (voice recorder, emoji picker, image preview, forward dialog) still work correctly.

## Complexity Tracking

No constitution violations. No complexity justifications needed.
