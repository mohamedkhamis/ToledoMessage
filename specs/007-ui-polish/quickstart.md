# Quickstart: UI Polish & Visual Enhancement

**Feature**: 007-ui-polish | **Date**: 2026-03-06

## Prerequisites

- .NET 10 SDK installed
- Project builds and runs (`dotnet run` from `src/ToledoMessage/`)
- Browser with DevTools (for CSS inspection and mobile viewport simulation)

## Development Workflow

### 1. Start the app

```bash
cd src/ToledoMessage
dotnet run
```

Navigate to `http://localhost:5000` (or configured port).

### 2. Key files to modify

| File | What to change |
|------|---------------|
| `src/ToledoMessage/wwwroot/app.css` | Main stylesheet — animations, component styles, accessibility |
| `src/ToledoMessage/wwwroot/themes.css` | CSS custom properties per theme (new variables) |
| `src/ToledoMessage.Client/Components/MessageBubble.razor` | Reply quote markup, delivery icons, grouped spacing |
| `src/ToledoMessage.Client/Pages/Chat.razor` | Unread divider, context menu, message animations |

### 3. Testing across themes

1. Open Settings page
2. Switch between all 8 themes: Default, Default Dark, WhatsApp, WhatsApp Dark, Telegram, Telegram Dark, Signal, Signal Dark
3. For each theme, verify:
   - No hardcoded colors visible (audio waveform, PDF preview, avatars, play buttons)
   - Animations play smoothly (message slide-in, toast, skeleton)
   - Timestamps readable without hover
   - Touch targets adequate on mobile viewport (use DevTools device toolbar)

### 4. Accessibility testing

- **Keyboard**: Tab through all interactive elements — verify focus rings
- **Reduced motion**: Enable `prefers-reduced-motion: reduce` in DevTools → Rendering tab → verify animations suppressed
- **Mobile**: Use DevTools device toolbar → verify 44px touch targets and 8px scrollbars

### 5. Run existing tests

```bash
cd tests
dotnet test
```

All 231 existing tests must continue to pass. No new test projects expected.

## Deployment

After changes are complete and approved:

```bash
powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force
```

Verify at `http://localhost:8080`.
