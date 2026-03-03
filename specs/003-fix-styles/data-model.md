# Data Model: 003-fix-styles

**Date**: 2026-03-03

## Overview

This feature involves no data model changes. All modifications are CSS, HTML markup, and Blazor component behavior (JS interop, navigation). No database migrations, API changes, or DTO modifications are required.

## Affected Files (by area)

### CSS
- `src/ToledoMessage/wwwroot/app.css` — primary stylesheet (reply bar, mobile responsive, touch targets, loading spinner)
- `src/ToledoMessage/wwwroot/themes.css` — theme variables (no structural changes expected)

### Razor Components
- `src/ToledoMessage.Client/Components/MessageInput.razor` — reply preview bar markup
- `src/ToledoMessage.Client/Components/MessageBubble.razor` — quoted message block markup
- `src/ToledoMessage.Client/Components/ConversationListSidebar.razor` — mobile panel switching, JS interop
- `src/ToledoMessage.Client/Pages/Chat.razor` — back button (change from `<a>` to NavigationManager)
- `src/ToledoMessage.Client/Pages/Login.razor` — status message layout (pre-allocate space)
- `src/ToledoMessage.Client/Pages/Register.razor` — status message layout (pre-allocate space)
- `src/ToledoMessage/Components/App.razor` — WASM loading spinner

### No Changes Expected
- Database / EF Core migrations
- API endpoints / controllers
- SignalR hub
- DTOs / shared models
- Crypto service
