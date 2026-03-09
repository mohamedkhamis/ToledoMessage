# Data Model: UI Polish & Visual Enhancement

**Feature**: 007-ui-polish | **Date**: 2026-03-06

## No Data Model Changes

This feature is entirely CSS and markup focused. No database entities, tables, migrations, or data model changes are required.

### Affected UI State (Client-Side Only)

| State | Type | Description |
|-------|------|-------------|
| Theme selection | `localStorage` | Already exists — drives CSS custom property loading |
| Wallpaper selection | `localStorage` | Already exists from prior feature |
| `prefers-reduced-motion` | OS setting | Read via CSS media query, no storage needed |

### CSS Custom Properties (New/Modified)

These are not data model entities but document the "contract" between themes and components:

| Variable | Purpose | Used By |
|----------|---------|---------|
| `--accent-color` | Already exists | Reply quote border, read checkmarks, focus rings |
| `--bg-primary` | Already exists | PDF preview background in dark mode |
| `--bg-secondary` | Already exists | Unread divider background |
| `--text-secondary` | Already exists | Timestamp text color at full opacity |
| `--waveform-played` | New | Audio waveform played bar color (replaces hardcoded green) |
| `--waveform-unplayed` | New | Audio waveform unplayed bar color |
| `--skeleton-base` | New | Skeleton loader base color |
| `--skeleton-shimmer` | New | Skeleton loader shimmer highlight color |
