# Research: UI Polish & Visual Enhancement

**Feature**: 007-ui-polish | **Date**: 2026-03-06

## Overview

This feature is CSS-focused with no unknowns requiring deep research. All technologies (CSS3, CSS custom properties, CSS animations, Blazor component markup) are well-established and already in use in the project.

## Decisions

### R-001: Animation Approach — CSS-Only

**Decision**: Use CSS transitions and `@keyframes` animations exclusively. No JavaScript animation libraries.

**Rationale**: CSS animations are GPU-accelerated, don't block the main thread, and automatically respect `prefers-reduced-motion`. They are sufficient for all required animations (slide-in, fade, shimmer, scale pop). JS libraries (Framer Motion, GSAP) would add unnecessary bundle size to a Blazor WASM app.

**Alternatives considered**:
- JavaScript `requestAnimationFrame`: More control but blocks main thread, requires manual `prefers-reduced-motion` handling
- Web Animations API: Good performance but less browser support and overkill for simple transitions
- Animation libraries (GSAP, anime.js): Powerful but adds dependency, bundle size, and complexity

### R-002: Theme Variable Strategy

**Decision**: Audit all hardcoded color values in `app.css` and replace with existing CSS custom properties from `themes.css`. Add new custom properties only where no suitable variable exists.

**Rationale**: The theme system already defines variables for backgrounds, text, accents, and borders. Most hardcoded values are oversights from initial development, not intentional design choices.

**Alternatives considered**:
- Sass/LESS variables: Would require build tooling changes; CSS custom properties are runtime-dynamic and already used
- CSS `color-mix()`: Modern but limited browser support; not needed for this scope

### R-003: Touch Target Implementation

**Decision**: Use `min-width`/`min-height` with padding adjustments to meet 44x44px minimum. Use `::before` pseudo-elements for invisible hit area expansion where visual size must remain smaller.

**Rationale**: WCAG 2.5.5 (AAA) recommends 44x44px. Padding is the simplest approach; pseudo-elements handle cases where visual design requires smaller elements.

**Alternatives considered**:
- JavaScript touch area expansion: Over-engineered for what CSS can solve
- Wrapper elements: Adds DOM complexity unnecessarily

### R-004: Focus Ring Style

**Decision**: Use `:focus-visible` (not `:focus`) with a 2px solid outline in the theme's accent color, offset by 2px.

**Rationale**: `:focus-visible` only shows focus rings for keyboard navigation, not mouse clicks, avoiding visual noise. The accent color ensures theme consistency.

**Alternatives considered**:
- `:focus`: Shows ring on mouse click too — annoying for mouse users
- Box-shadow for focus ring: Works but `outline` with `outline-offset` is semantically correct and doesn't affect layout

### R-005: Skeleton Shimmer Technique

**Decision**: Use CSS `@keyframes` with a `linear-gradient` background animated via `background-position`. The gradient moves from left to right creating a shimmer effect.

**Rationale**: This is the standard approach used by Facebook, YouTube, and other major apps. It's lightweight, GPU-accelerated, and works across all browsers.

**Alternatives considered**:
- CSS `animation: pulse` (current): Too basic — just opacity fade, doesn't convey "loading" effectively
- SVG animated gradient: More complex, no real benefit
- JavaScript-driven skeleton: Unnecessary complexity

### R-006: Unread Divider Visibility

**Decision**: Use a full-width bar with theme accent background color, white/contrasting text, centered "X unread messages" text, and subtle top/bottom margin for separation.

**Rationale**: WhatsApp and Telegram both use colored bars for unread dividers. A colored background is immediately noticeable while scrolling, unlike a thin line.

**Alternatives considered**:
- Thin colored line with label: Less noticeable when scrolling fast
- Floating badge/pill: Could overlap content; positional ambiguity

## No Outstanding Unknowns

All NEEDS CLARIFICATION items from Technical Context have been resolved. No blocking research remains.
