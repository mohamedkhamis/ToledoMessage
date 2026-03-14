# Quickstart: 003-fix-styles

## Prerequisites

- .NET 10 SDK
- SQL Server (for running the app locally)
- A mobile browser or browser DevTools with device emulation (320px, 375px, 480px, 768px, 1280px)

## Build & Run

```bash
cd src/ToledoVault
dotnet run
```

App runs at https://localhost:5001 (or configured port).

## Testing Changes

### Reply Bar
1. Open a chat conversation
2. Reply to a long message — verify the reply preview bar is compact (max 64px height, 2-line clamp)
3. Reply to an image-only message — verify "[Image]" placeholder shows
4. Check in-bubble quoted message block is compact (max 60px)

### Mobile Navigation
1. Open browser DevTools → toggle device emulation to iPhone 12 (375px)
2. Tap a conversation → verify chat opens
3. Tap back → verify conversation list returns without blank screen
4. Repeat 10 times rapidly
5. Tap Settings in sidebar → verify it navigates on first tap

### Responsive Layout
1. Test at 320px, 375px, 480px, 768px, 1280px
2. Verify no horizontal scrollbar on any page
3. Verify all buttons have 44px+ touch targets

### Style Flash
1. Sign in to the app
2. Watch for layout shifts during "Generating encryption keys..." phase
3. Verify no flash of unstyled content

### WASM Loading
1. Hard refresh (Ctrl+Shift+R) the login page
2. Verify a centered spinner with app name shows during WASM download
3. Verify it transitions smoothly to the rendered page

## Deploy

```powershell
powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force
```
