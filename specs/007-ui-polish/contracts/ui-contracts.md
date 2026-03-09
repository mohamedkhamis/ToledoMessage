# UI Contracts: UI Polish & Visual Enhancement

**Feature**: 007-ui-polish | **Date**: 2026-03-06

## CSS Custom Property Contract

All theme-aware components MUST use these CSS custom properties instead of hardcoded values. Each theme (8 total) MUST define all properties.

### Existing Properties (already defined in themes.css)

| Property | Purpose |
|----------|---------|
| `--accent-color` | Primary accent (buttons, links, active states) |
| `--bg-primary` | Main background |
| `--bg-secondary` | Secondary/elevated background |
| `--bg-tertiary` | Tertiary background (cards, popups) |
| `--text-primary` | Primary text color |
| `--text-secondary` | Secondary/muted text color |
| `--border-color` | Default border color |
| `--sent-bubble-bg` | Sent message bubble background |
| `--received-bubble-bg` | Received message bubble background |

### New Properties (to be added per theme)

| Property | Purpose | Fallback |
|----------|---------|----------|
| `--waveform-played` | Audio waveform played portion | `var(--accent-color)` |
| `--waveform-unplayed` | Audio waveform unplayed portion | `var(--border-color)` |
| `--skeleton-base` | Skeleton loader base color | `var(--bg-secondary)` |
| `--skeleton-shimmer` | Skeleton shimmer highlight | `var(--bg-tertiary)` |

## Animation Contract

All animations MUST:
1. Complete in under 300ms
2. Use CSS transitions or `@keyframes` only (no JavaScript)
3. Be suppressed when `prefers-reduced-motion: reduce` is active

### Required Animations

| Animation | Trigger | Duration | Easing |
|-----------|---------|----------|--------|
| Message slide-in | New message rendered | 200ms | ease-out |
| Toast enter | Toast notification shown | 250ms | ease-out |
| Toast exit | Toast auto-dismiss | 200ms | ease-in |
| Skeleton shimmer | Loading state active | 1.5s loop | linear |
| Reaction pop | Reaction badge tapped | 200ms | ease-out |
| Context menu fade | Right-click/long-press | 150ms | ease-out |

## Accessibility Contract

| Requirement | Specification |
|-------------|--------------|
| Touch targets | min 44x44px on mobile viewports |
| Focus rings | 2px solid `var(--accent-color)`, 2px offset, `:focus-visible` only |
| Scrollbar width | min 8px on touch devices |
| Timestamp visibility | Full opacity on mobile (no hover dependency) |
| Reduced motion | All animations suppressed via `@media (prefers-reduced-motion: reduce)` |

## Component Styling Contract

### Forward Dialog
- Modal overlay with semi-transparent backdrop
- Conversation list with avatars, names, search input
- Theme-consistent colors and spacing

### Search Result Counter
- Positioned within search bar
- Format: "X of Y"
- Uses `--text-secondary` color

### Link Preview Card
- Bordered card with image thumbnail, title, description, URL
- Uses `--bg-tertiary` background, `--border-color` border

### Clear Chat Dialog
- Modal with warning text
- Distinct action buttons (cancel/confirm)
- Confirm button uses destructive color styling

### Reply Quote Block
- 3px left border in `var(--accent-color)`
- Slightly indented with muted background
- Sender name in accent color

### Unread Divider
- Full-width bar with `var(--accent-color)` background
- Centered white text: "X unread messages"
- Top/bottom margin for visual separation

### Delivery Status Icons
- Sending: clock/spinner icon
- Sent: single checkmark (muted color)
- Delivered: double checkmark (muted color)
- Read: double checkmark in `var(--accent-color)`
