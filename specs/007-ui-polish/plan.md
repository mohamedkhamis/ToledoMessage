# Implementation Plan: UI Polish & Visual Enhancement

**Branch**: `007-ui-polish` | **Date**: 2026-03-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-ui-polish/spec.md`

## Summary

Comprehensive CSS-only styling improvements across the chat application: replace hardcoded colors with CSS custom properties, add micro-animations (message slide-in, toast transitions, skeleton shimmer, reaction pop, context menu fade), fix accessibility gaps (touch targets, focus rings, scrollbar width), style missing components (forward dialog, search counter, link preview, clear chat dialog), polish message bubbles (reply accent border, grouped spacing, unread divider), fix mobile timestamp visibility, and improve delivery status icon clarity. All changes are CSS/markup-only with no data model, API, or cryptographic changes.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS), CSS3, JavaScript (Blazor WASM interop)
**Primary Dependencies**: Blazor WebAssembly, ASP.NET Core, SignalR (no new dependencies)
**Storage**: N/A (no data model changes)
**Testing**: Visual inspection across 8 themes; xUnit for any component logic changes
**Target Platform**: Web (Blazor WASM) — desktop and mobile browsers
**Project Type**: Web application (Blazor WebAssembly hosted)
**Performance Goals**: All animations < 300ms, CSS-only (no JS animation libraries), no frame drops
**Constraints**: Must respect `prefers-reduced-motion`; all colors via CSS custom properties; touch targets >= 44x44px
**Scale/Scope**: 8 themes (Default, WhatsApp, Telegram, Signal + dark variants), ~20 functional requirements

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Applicable? | Status | Notes |
|-----------|-------------|--------|-------|
| I. Zero-Trust Server | No | PASS | CSS-only changes — no server-side data handling |
| II. Hybrid Cryptography | No | PASS | No cryptographic operations involved |
| III. Established Libraries Only | No | PASS | No new libraries — CSS and existing Blazor components only |
| IV. Signal Protocol Fidelity | No | PASS | No protocol changes |
| V. .NET Ecosystem | Yes | PASS | Stays within Blazor/ASP.NET Core ecosystem |
| VI. Test-First Development | Partial | PASS | CSS changes are verified via visual inspection across themes; component logic changes (if any) will have unit tests |
| VII. Open-Source Transparency | Yes | PASS | All changes are in public repository |

**Quality Gates**:
- Gate 1 (Tests pass): All existing tests must continue to pass; no test changes expected
- Gate 2 (Coverage): No impact — CSS changes don't affect code coverage
- Gate 3 (No secrets): N/A — no secrets involved in styling
- Gate 4 (Security review): N/A — no cryptographic code changes

**Result**: All gates PASS. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/007-ui-polish/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (minimal — no data changes)
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (UI contracts only)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── ToledoVault/                    # Server project
│   └── wwwroot/
│       ├── app.css                   # PRIMARY: Main stylesheet (bulk of changes)
│       └── themes.css                # Theme CSS custom properties
├── ToledoVault.Client/             # Client WASM project
│   ├── Components/
│   │   ├── MessageBubble.razor       # Reply quotes, delivery icons, grouped spacing
│   │   ├── ConversationListSidebar.razor
│   │   └── [other components]
│   ├── Pages/
│   │   ├── Chat.razor                # Unread divider, context menu, animations
│   │   └── Settings.razor
│   └── wwwroot/
│       └── storage.js                # JS interop (if needed)
└── ToledoVault.Shared/             # Shared DTOs (no changes expected)
```

**Structure Decision**: All changes occur within existing files. Primary targets are `app.css` (styling), `themes.css` (CSS custom properties), and Blazor `.razor` components for markup adjustments. No new projects or directories needed.

## Complexity Tracking

No constitution violations — table not applicable.
