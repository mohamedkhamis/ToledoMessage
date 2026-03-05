# Plan: Multi-App Theme System

**Created:** 2026-03-02
**Updated:** 2026-03-02
**Feature:** UI theming system that transforms ToledoMessage into WhatsApp, Telegram, or Signal style

---

## Overview

Extend the existing color-based theme system (7 themes in `themes.css` with 22 CSS variables each) to include:
1. **Structural CSS** (bubble shapes, border-radius, spacing, chat background patterns)
2. **Theme labels** (app-specific terminology for all UI strings)
3. **Theme change notification** (so components re-render labels on theme switch)

### What Already Exists
- `ThemeService.cs` — reads/writes theme via JS interop (`toledoStorage.getTheme/setTheme`)
- `themes.css` — 7 themes: default, default-dark, whatsapp, whatsapp-dark, telegram, signal, signal-dark
- Each theme defines color variables (`--bg-primary`, `--msg-sent-bg`, `--accent`, etc.)
- `Settings.razor` — theme picker grid + font size selector
- Theme applied via `[data-theme]` attribute on `<html>` element

### What's Missing
- No Telegram Dark theme (all other apps have light + dark)
- No structural CSS variables (border-radius, spacing differ per app)
- All UI strings are hardcoded in English with no per-theme variations
- No `OnThemeChanged` event — components can't react to theme switches

### Explicit Out of Scope
- Home/landing page (`Home.razor`) — branding page, not themed
- Login/Register pages — auth pages keep default styling
- Server-side changes — all theming is client-side only

---

## Phase 1: CSS Structural Theming (Quick Visual Win)

### 1.1 Add Telegram Dark Theme
**File:** `src/ToledoMessage/wwwroot/themes.css`

Add 8th theme `telegram-dark` after the existing Telegram block:
```css
:root[data-theme="telegram-dark"] {
    --bg-primary: #17212b;
    --bg-secondary: #0e1621;
    --bg-chat: #0e1621;
    --text-primary: #f5f5f5;
    --text-secondary: #708499;
    --accent: #2aabee;
    --accent-hover: #229ed9;
    --accent-text: #ffffff;
    --border: #1f2936;
    --msg-sent-bg: #2b5278;
    --msg-sent-text: #f5f5f5;
    --msg-received-bg: #182533;
    --msg-received-text: #f5f5f5;
    --nav-bg: #17212b;
    --nav-text: #f5f5f5;
    --input-bg: #242f3d;
    --input-border: #1f2936;
    --shadow: 0 1px 3px rgba(0,0,0,0.4);
    --shadow-md: 0 2px 5px rgba(0,0,0,0.4);
    --card-bg: #17212b;
    --hover-bg: #202b36;
    --accent-light: #1a3045;
    color-scheme: dark;
}
```

**File:** `src/ToledoMessage.Client/Services/ThemeService.cs`

Add to `GetAvailableThemes()`:
```csharp
new ThemeInfo("telegram-dark", "Telegram Dark", "#17212b")
```

### 1.2 Add Structural CSS Variables to Each Theme
**File:** `src/ToledoMessage/wwwroot/themes.css`

Append these variables inside each existing theme block:

| Variable | Default | WhatsApp | Telegram | Signal |
|----------|---------|----------|----------|--------|
| `--msg-bubble-radius` | `8px` | `7.5px` | `12px` | `18px` |
| `--msg-bubble-tail` | `none` | `block` | `none` | `none` |
| `--input-radius` | `8px` | `24px` | `22px` | `20px` |
| `--input-padding` | `10px 14px` | `10px 16px` | `10px 14px` | `10px 14px` |
| `--sidebar-width` | `340px` | `340px` | `380px` | `320px` |
| `--chat-bg-image` | `none` | doodle pattern | `none` | `none` |
| `--avatar-size` | `40px` | `40px` | `42px` | `36px` |
| `--msg-font-size` | `14.5px` | `14.2px` | `15px` | `14px` |

Dark variants inherit their light counterpart's structural values (only colors differ).

### 1.3 Update app.css to Use Structural Variables
**File:** `src/ToledoMessage/wwwroot/app.css`

Replace hardcoded values with `var()` references:

```css
.message-bubble {
    border-radius: var(--msg-bubble-radius, 8px);
}

.msg-textarea {
    border-radius: var(--input-radius, 8px);
    padding: var(--input-padding, 10px 14px);
}

.chat-sidebar {
    width: var(--sidebar-width, 340px);
    min-width: var(--sidebar-width, 340px);
}

.chat-messages-container {
    background-image: var(--chat-bg-image, none);
}
```

---

## Phase 2: Theme Labels System

### 2.1 Create ThemeLabelSet Record
**File:** `src/ToledoMessage.Client/Services/ThemeLabelSet.cs` (new)

A single record with all theme-varying strings. Four static instances (Default, WhatsApp, Telegram, Signal):

```csharp
public sealed record ThemeLabelSet
{
    // -- Sidebar --
    public string SearchPlaceholder { get; init; } = "Search conversations...";
    public string NoConversations { get; init; } = "No conversations yet";
    public string StartConversation { get; init; } = "Start a conversation";
    public string NewChat { get; init; } = "New conversation";
    public string SettingsLabel { get; init; } = "Settings";
    public string LogoutLabel { get; init; } = "Logout";

    // -- Chat header --
    public string Online { get; init; } = "online";
    public string LastSeen { get; init; } = "last seen {0}";       // {0} = formatted date
    public string TypingFormat { get; init; } = "{0} is typing..."; // {0} = display name
    public string MembersFormat { get; init; } = "{0} members";     // {0} = count
    public string SearchMessages { get; init; } = "Search messages...";

    // -- Chat body --
    public string NoMessages { get; init; } = "No messages yet";
    public string SendFirstMessage { get; init; } = "Send the first message to start the conversation.";
    public string NewMessagesFormat { get; init; } = "{0} new message(s)"; // {0} = count

    // -- Context menu --
    public string Copy { get; init; } = "Copy";
    public string Reply { get; init; } = "Reply";
    public string Forward { get; init; } = "Forward";
    public string DeleteForMe { get; init; } = "Delete for me";
    public string DeleteForEveryone { get; init; } = "Delete for everyone";
    public string ForwardMessage { get; init; } = "Forward message";

    // -- Clear chat dialog --
    public string ClearChat { get; init; } = "Clear chat";
    public string ClearChatHeader { get; init; } = "Clear chat messages";
    public string ChooseMessages { get; init; } = "Choose which messages to delete:";
    public string LastHour { get; init; } = "Last hour";
    public string Last24Hours { get; init; } = "Last 24 hours";
    public string Last7Days { get; init; } = "Last 7 days";
    public string Last30Days { get; init; } = "Last 30 days";
    public string AllMessages { get; init; } = "All messages";
    public string Cancel { get; init; } = "Cancel";

    // -- Message input --
    public string TypeMessage { get; init; } = "Type a message...";
    public string PhotoOrVideo { get; init; } = "Photo or Video";
    public string Audio { get; init; } = "Audio";
    public string Document { get; init; } = "Document";

    // -- Message bubble --
    public string Forwarded { get; init; } = "Forwarded";
    public string ImageUnavailable { get; init; } = "Image unavailable";
    public string VideoUnavailable { get; init; } = "Video unavailable";
    public string AudioUnavailable { get; init; } = "Audio unavailable";
    public string FileUnavailable { get; init; } = "File unavailable";
    public string Download { get; init; } = "Download";

    // -- Settings page --
    public string Appearance { get; init; } = "Appearance";
    public string FontSize { get; init; } = "Font Size";
    public string Privacy { get; init; } = "Privacy";
    public string ReadReceipts { get; init; } = "Read Receipts";
    public string TypingIndicators { get; init; } = "Typing Indicators";
    public string Security { get; init; } = "Security";
    public string Notifications { get; init; } = "Notifications";
    public string LinkedDevices { get; init; } = "Linked Devices";
    public string AccountDeletion { get; init; } = "Account Deletion";

    // -- Factory --
    public static ThemeLabelSet Default { get; } = new();

    public static ThemeLabelSet WhatsApp { get; } = new()
    {
        SearchPlaceholder = "Search or start new chat",
        NoConversations = "No chats yet",
        StartConversation = "Start a new chat",
        NewChat = "New chat",
        Online = "online",
        LastSeen = "last seen {0}",
        TypingFormat = "typing...",
        MembersFormat = "{0} participants",
        NoMessages = "No messages here yet",
        SendFirstMessage = "Send a message or tap the greeting below.",
        TypeMessage = "Type a message",
        ClearChat = "Clear chat",
        DeleteForMe = "Delete for me",
        DeleteForEveryone = "Delete for everyone",
        Forwarded = "Forwarded",
        // ... etc
    };

    public static ThemeLabelSet Telegram { get; } = new()
    {
        SearchPlaceholder = "Search",
        NoConversations = "No chats",
        StartConversation = "Start messaging",
        NewChat = "New Message",
        Online = "online",
        LastSeen = "last seen {0}",
        TypingFormat = "typing...",
        MembersFormat = "{0} members",
        NoMessages = "No messages here yet",
        SendFirstMessage = "Send a message",
        TypeMessage = "Write a message...",
        ClearChat = "Clear History",
        DeleteForMe = "Delete for me",
        DeleteForEveryone = "Delete for everyone",
        Forwarded = "Forwarded message",
        // ... etc
    };

    public static ThemeLabelSet Signal { get; } = new()
    {
        SearchPlaceholder = "Search...",
        NoConversations = "No conversations",
        StartConversation = "Compose a new message",
        NewChat = "New message",
        Online = "Active now",
        LastSeen = "Active {0}",
        TypingFormat = "{0} is typing",
        MembersFormat = "{0} members",
        NoMessages = "No messages",
        SendFirstMessage = "Send a message to get started",
        TypeMessage = "Message",
        ClearChat = "Delete conversation",
        DeleteForMe = "Delete for me",
        DeleteForEveryone = "Delete for everyone",
        Forwarded = "Forwarded",
        // ... etc
    };
}
```

### 2.2 Extend ThemeService with Labels + Change Event
**File:** `src/ToledoMessage.Client/Services/ThemeService.cs`

```csharp
public sealed class ThemeService(IJSRuntime js)
{
    private string _cachedThemeId = "default";
    private ThemeLabelSet _cachedLabels = ThemeLabelSet.Default;

    // Event: fires when theme changes so components can re-render
    public event Action? OnThemeChanged;

    public ThemeLabelSet Labels => _cachedLabels;

    public async Task<string> GetThemeAsync()
    {
        _cachedThemeId = await js.InvokeAsync<string?>("toledoStorage.getTheme") ?? "default";
        _cachedLabels = ResolveLabels(_cachedThemeId);
        return _cachedThemeId;
    }

    public async Task SetThemeAsync(string themeName)
    {
        await js.InvokeVoidAsync("toledoStorage.setTheme", themeName);
        _cachedThemeId = themeName;
        _cachedLabels = ResolveLabels(themeName);
        OnThemeChanged?.Invoke();
    }

    private static ThemeLabelSet ResolveLabels(string themeId) => themeId switch
    {
        "whatsapp" or "whatsapp-dark" => ThemeLabelSet.WhatsApp,
        "telegram" or "telegram-dark" => ThemeLabelSet.Telegram,
        "signal" or "signal-dark" => ThemeLabelSet.Signal,
        _ => ThemeLabelSet.Default
    };

    // ... existing font size methods unchanged ...
}
```

**No ThemeContext.razor needed.** Components already inject `ThemeService` — they access `Theme.Labels.PropertyName` directly. Components subscribe to `OnThemeChanged` in `OnInitialized` and call `StateHasChanged()`.

---

## Phase 3: Component Integration

### 3.1 Pattern for Each Component

Every component that uses labels follows this pattern:
```csharp
@inject ThemeService Theme

// In markup: @Theme.Labels.SearchPlaceholder

@code {
    protected override void OnInitialized()
    {
        Theme.OnThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Theme.OnThemeChanged -= OnThemeChanged;
    }
}
```

### 3.2 ConversationListSidebar.razor
**File:** `src/ToledoMessage.Client/Components/ConversationListSidebar.razor`

| Line | Current | Replace With |
|------|---------|-------------|
| 17 | `title="Settings"` | `title="@Theme.Labels.SettingsLabel"` |
| 18 | `title="New conversation"` | `title="@Theme.Labels.NewChat"` |
| 19 | `title="Logout"` | `title="@Theme.Labels.LogoutLabel"` |
| 24 | `placeholder="Search conversations..."` | `placeholder="@Theme.Labels.SearchPlaceholder"` |
| 38 | `"No conversations yet"` | `@Theme.Labels.NoConversations` |
| 40 | `"Start a conversation"` | `@Theme.Labels.StartConversation` |

### 3.3 Chat.razor
**File:** `src/ToledoMessage.Client/Pages/Chat.razor`

| Line | Current | Replace With |
|------|---------|-------------|
| 31 | `"@_participants.Count members"` | `string.Format(Theme.Labels.MembersFormat, _participants.Count)` |
| 35 | `"online"` | `@Theme.Labels.Online` |
| 39 | `"last seen @date"` | `string.Format(Theme.Labels.LastSeen, date)` |
| 44 | `"{name} is typing..."` | `string.Format(Theme.Labels.TypingFormat, name)` |
| 54 | `"Clear chat"` | `@Theme.Labels.ClearChat` |
| 64 | `"Search messages..."` | `@Theme.Labels.SearchMessages` |
| 125 | `"No messages yet"` | `@Theme.Labels.NoMessages` |
| 126 | `"Send the first message..."` | `@Theme.Labels.SendFirstMessage` |
| 149 | `"@n new message(s)"` | `string.Format(Theme.Labels.NewMessagesFormat, n)` |
| 198 | `"Copy"` | `@Theme.Labels.Copy` |
| 201 | `"Reply"` | `@Theme.Labels.Reply` |
| 204 | `"Forward"` | `@Theme.Labels.Forward` |
| 207 | `"Delete for me"` | `@Theme.Labels.DeleteForMe` |
| 212 | `"Delete for everyone"` | `@Theme.Labels.DeleteForEveryone` |
| 222 | `"Clear chat messages"` | `@Theme.Labels.ClearChatHeader` |
| 223 | `"Choose which messages..."` | `@Theme.Labels.ChooseMessages` |
| 225-229 | Time period strings | `@Theme.Labels.LastHour`, etc. |
| 231 | `"Cancel"` | `@Theme.Labels.Cancel` |
| 240 | `"Forward message"` | `@Theme.Labels.ForwardMessage` |

### 3.4 MessageInput.razor
**File:** `src/ToledoMessage.Client/Components/MessageInput.razor`

| Line | Current | Replace With |
|------|---------|-------------|
| 47 | `"Photo or Video"` | `@Theme.Labels.PhotoOrVideo` |
| 52 | `"Audio"` | `@Theme.Labels.Audio` |
| 57 | `"Document"` | `@Theme.Labels.Document` |
| 96 | `placeholder="Type a message..."` | `placeholder="@Theme.Labels.TypeMessage"` |

### 3.5 MessageBubble.razor
**File:** `src/ToledoMessage.Client/Components/MessageBubble.razor`

| Line | Current | Replace With |
|------|---------|-------------|
| 7 | `"Forwarded"` | `@Theme.Labels.Forwarded` |
| 26 | `"Image unavailable"` | `@Theme.Labels.ImageUnavailable` |
| 42 | `"Video unavailable"` | `@Theme.Labels.VideoUnavailable` |
| 88 | `"Audio unavailable"` | `@Theme.Labels.AudioUnavailable` |
| 101 | `"Download"` | `@Theme.Labels.Download` |
| 108 | `"File unavailable"` | `@Theme.Labels.FileUnavailable` |

CSS structural changes (bubble radius) are handled automatically by Phase 1 CSS variables — no C# changes needed for styling.

### 3.6 Settings.razor
**File:** `src/ToledoMessage.Client/Pages/Settings.razor`

| Line | Current | Replace With |
|------|---------|-------------|
| 17 | `"Settings"` | `@Theme.Labels.SettingsLabel` |
| 30 | `"Appearance"` | `@Theme.Labels.Appearance` |
| 43 | `"Font Size"` | `@Theme.Labels.FontSize` |
| 55 | `"Privacy"` | `@Theme.Labels.Privacy` |
| 60 | `"Read Receipts"` | `@Theme.Labels.ReadReceipts` |
| 72 | `"Typing Indicators"` | `@Theme.Labels.TypingIndicators` |
| 83 | `"Security"` | `@Theme.Labels.Security` |
| 99 | `"Notifications"` | `@Theme.Labels.Notifications` |
| 115 | `"Linked Devices"` | `@Theme.Labels.LinkedDevices` |
| 174 | `"Account Deletion"` | `@Theme.Labels.AccountDeletion` |

**Note:** Section descriptions and confirmations can stay in English — they are help text, not brand-specific terminology.

---

## Phase 4: Delivery Indicators (Per-Theme)

Delivery status is rendered in `MessageBubble.razor` via C# code with SVG ticks. This **cannot** be done with CSS `content` variables because the existing code uses inline SVG.

### Approach
Add a `DeliveryStyle` property to `ThemeLabelSet`:

```csharp
public enum DeliveryDisplayStyle
{
    DoubleTick,    // WhatsApp: grey ✓✓ → blue ✓✓
    SingleTick,    // Telegram: ✓ (sent) / ✓ (read)
    Minimal,       // Signal: no visible ticks, small dot only
    Default        // Current behavior
}

// In ThemeLabelSet:
public DeliveryDisplayStyle DeliveryStyle { get; init; } = DeliveryDisplayStyle.Default;
```

`MessageBubble.razor` reads `Theme.Labels.DeliveryStyle` to decide which SVG/indicator to render.

---

## Phase 5: Implementation Tasks

### Task List (Ordered by Dependency)

| ID | Description | Files | Depends On |
|----|-------------|-------|------------|
| T1 | Add Telegram Dark theme to themes.css + ThemeService | `themes.css`, `ThemeService.cs` | — |
| T2 | Add structural CSS variables to all 8 theme blocks | `themes.css` | T1 |
| T3 | Update app.css to use structural CSS variables | `app.css` | T2 |
| T4 | Create ThemeLabelSet.cs with all label strings | `Services/ThemeLabelSet.cs` | — |
| T5 | Add `OnThemeChanged` event + `Labels` property to ThemeService | `Services/ThemeService.cs` | T4 |
| T6 | Update ConversationListSidebar with theme labels | `Components/ConversationListSidebar.razor` | T5 |
| T7 | Update Chat.razor with theme labels | `Pages/Chat.razor` | T5 |
| T8 | Update MessageInput with theme labels | `Components/MessageInput.razor` | T5 |
| T9 | Update MessageBubble with theme labels + delivery style | `Components/MessageBubble.razor` | T5 |
| T10 | Update Settings.razor with theme labels | `Pages/Settings.razor` | T5 |
| T11 | Test all 8 themes render correctly | Manual testing | T3, T6-T10 |

**Parallel opportunities:** T1-T3 (CSS) can be done in parallel with T4-T5 (labels system). T6-T10 can all be done in parallel after T5.

---

## Files Summary

| File | Action |
|------|--------|
| `src/ToledoMessage/wwwroot/themes.css` | Add Telegram Dark + structural CSS vars to all themes |
| `src/ToledoMessage/wwwroot/app.css` | Replace hardcoded values with `var()` references |
| `src/ToledoMessage.Client/Services/ThemeLabelSet.cs` | **New** — label record with 4 static instances |
| `src/ToledoMessage.Client/Services/ThemeService.cs` | Add `Labels`, `OnThemeChanged`, `ResolveLabels()` |
| `src/ToledoMessage.Client/Components/ConversationListSidebar.razor` | Use `Theme.Labels.*` for 6 strings |
| `src/ToledoMessage.Client/Pages/Chat.razor` | Use `Theme.Labels.*` for 20+ strings |
| `src/ToledoMessage.Client/Components/MessageInput.razor` | Use `Theme.Labels.*` for 4 strings |
| `src/ToledoMessage.Client/Components/MessageBubble.razor` | Use `Theme.Labels.*` for 6 strings + delivery style |
| `src/ToledoMessage.Client/Pages/Settings.razor` | Use `Theme.Labels.*` for 10 section headers |

**Not changed:** Home.razor, Login.razor, Register.razor, VoiceRecorder.razor (kept as-is — not theme-specific)

---

## Notes

- **Existing theme colors remain unchanged** — structural variables are additive
- **Dark variants share labels with their light counterpart** — `whatsapp-dark` uses `ThemeLabelSet.WhatsApp`
- **No server changes needed** — all theming is client-side
- **No CascadingValue needed** — `ThemeService` is already a scoped service injected everywhere
- **Mobile responsiveness** — structural CSS variables must include responsive overrides in `@media` queries

---

## Testing Checklist

### CSS Structural
- [ ] Default: 8px bubble radius, no chat background pattern
- [ ] WhatsApp: 7.5px radius, green sent bubbles, doodle background pattern
- [ ] WhatsApp Dark: same structure as WhatsApp light, dark colors
- [ ] Telegram: 12px radius, light green sent bubbles, wider sidebar
- [ ] Telegram Dark: same structure as Telegram light, dark colors
- [ ] Signal: 18px radius (pill-shaped), blue sent bubbles
- [ ] Signal Dark: same structure as Signal light, dark colors

### Labels
- [ ] Switch to WhatsApp: placeholder says "Search or start new chat", input says "Type a message"
- [ ] Switch to Telegram: placeholder says "Search", input says "Write a message..."
- [ ] Switch to Signal: placeholder says "Search...", input says "Message"
- [ ] Labels update immediately on theme change (no page refresh needed)
- [ ] All context menu items reflect current theme labels
- [ ] Settings page section headers use theme labels

### Delivery Indicators
- [ ] WhatsApp: double grey ticks (delivered), double blue ticks (read)
- [ ] Telegram: single check (sent/delivered)
- [ ] Signal: minimal indicator (small dot or none)

### Mobile
- [ ] All 8 themes responsive on mobile viewport
- [ ] Sidebar width adapts correctly per theme on desktop
