# Implementation Plan: UX & Responsive Enhancements

**Branch**: `001-secure-messaging` | **Date**: 2026-03-02 | **Spec**: `specs/001-secure-messaging/spec.md`
**Input**: 6 user-requested improvements (responsive CSS, CSS review, big emoji, home page simplify, auth button fix, device re-registration)

## Summary

Six enhancements to improve the app's UX: (1) full responsive CSS for mobile/tablet/desktop, (2) CSS review with image preview and audio bar polish, (3) WhatsApp-style big emoji + cross-session recent emoji, (4) simplified non-technical home page, (5) consistent auth button styles, and (6) fix critical device re-registration bug on server redeployment.

## Technical Context

**Language/Version**: C# / .NET 10 (LTS)
**Primary Dependencies**: Blazor WebAssembly, ASP.NET Core, SignalR, EF Core 10
**Storage**: SQL Server 2022 (server) + Browser localStorage/IndexedDB (client)
**Testing**: xUnit, manual browser testing (mobile/tablet/desktop viewports)
**Target Platform**: Web — all screen sizes (mobile 320px+, tablet 768px+, desktop 992px+, landscape)
**Project Type**: Web application (real-time messaging)
**Constraints**: All CSS changes must respect the existing theme system (CSS variables). No breaking changes to existing functionality.

## Constitution Check

*GATE: All changes are client-side CSS/Razor. No crypto or protocol changes.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero-Trust Server | PASS | Device restore endpoint only accepts public keys (no private key exposure) |
| II. Hybrid Cryptography | N/A | No crypto changes |
| III. Established Libraries Only | N/A | No new libraries |
| IV. Signal Protocol Fidelity | PASS | Device restore reuses existing keys, no protocol change |
| V. .NET Ecosystem | PASS | Same stack |
| VI. Test-First Development | PASS | Manual testing checklist included |
| VII. Open-Source Transparency | PASS | No hidden changes |

## Project Structure

### Files to Modify

```text
src/ToledoMessage/wwwroot/app.css                          # Responsive CSS, emoji, audio, image
src/ToledoMessage/Components/Pages/Home.razor              # Simplify content, fix button styles
src/ToledoMessage.Client/Components/MessageBubble.razor    # Big emoji detection + rendering
src/ToledoMessage.Client/Components/VoiceRecorder.razor    # Mobile responsive adjustments
src/ToledoMessage.Client/Components/EmojiPicker.razor      # Mobile responsive
src/ToledoMessage.Client/Pages/Login.razor                 # Device restore logic
src/ToledoMessage.Client/Pages/Chat.razor                  # Forward dialog responsive
src/ToledoMessage.Client/Services/KeyGenerationService.cs  # RestoreDeviceRequest builder
src/ToledoMessage/Controllers/DevicesController.cs         # Device restore endpoint
src/ToledoMessage.Shared/DTOs/                             # RestoreDeviceRequest DTO (if needed)
```

---

## Phase 1: Responsive CSS (Mobile, Tablet, Desktop)

### 1.1 Add Missing Breakpoints to app.css

Add a **small phone breakpoint** (`max-width: 480px`) and a **landscape breakpoint**:

```css
/* Small phones (iPhone SE, etc.) */
@media (max-width: 480px) {
    .msg-textarea { max-height: 100px; }
    .send-btn, .mic-btn { width: 38px; height: 38px; min-width: 38px; }
    .chat-input-row { gap: 4px; }
    .emoji-picker-popup { width: 100vw; left: 0; right: 0; border-radius: 12px 12px 0 0; }
    .voice-recorder-bar { border-radius: 20px; padding: 6px 10px; }
    .slide-hint { display: none; }
}

/* Landscape on mobile */
@media (max-height: 500px) and (orientation: landscape) {
    .chat-header { padding: 4px 12px; }
    .msg-textarea { max-height: 60px; }
    .emoji-picker-popup { max-height: 200px; }
}
```

### 1.2 Make Emoji Picker Responsive

In the existing `@media (max-width: 767px)` block, add:
```css
.emoji-picker-popup {
    width: calc(100vw - 24px);
    max-width: 360px;
    max-height: 300px;
}
.emoji-grid { grid-template-columns: repeat(7, 1fr); }
```

### 1.3 Make Forward Dialog Responsive

```css
@media (max-width: 767px) {
    .forward-dialog {
        max-width: calc(100vw - 32px);
        max-height: 80vh;
    }
}
```

### 1.4 Make Voice Recorder Responsive

```css
@media (max-width: 480px) {
    .voice-recorder-bar { min-height: 40px; }
    .wave-bar { width: 1.5px; }
    .voice-waveform { gap: 1px; }
    .voice-waveform-static { gap: 1px; }
    .wave-bar-static { width: 1.5px; }
    .recording-timer { font-size: 0.8rem; }
}
```

### 1.5 Test Responsive on All Targets

Manual testing at these viewports:
- 320px (iPhone SE)
- 375px (iPhone 12 mini)
- 390px (iPhone 14)
- 768px (iPad portrait)
- 1024px (iPad landscape)
- 1280px+ (desktop)
- Landscape mode on each mobile size

---

## Phase 2: CSS Review & Polish

### 2.1 Image Preview Mobile Fix

```css
@media (max-width: 767px) {
    .message-image { max-height: 280px; }
    .lightbox-image { max-width: 95vw; max-height: 85vh; }
    .lightbox-close { top: 8px; right: 8px; }
    .lightbox-nav { width: 40px; height: 60px; }
}
```

### 2.2 Audio Recording Bar Polish

- Ensure waveform bars don't overflow on narrow screens
- Reduce bar count on small screens via CSS (hide every other bar below 480px)
- Improve preview duration display alignment

### 2.3 General CSS Audit

- Check all `px` values that should be `rem` or `em` for scalability
- Verify all theme CSS variables are used consistently
- Check for any z-index conflicts on mobile overlays

---

## Phase 3: Big Emoji

### 3.1 Add Emoji Detection in MessageBubble.razor

Add helper method:
```csharp
private static bool IsEmojiOnly(string? text)
{
    if (string.IsNullOrWhiteSpace(text)) return false;
    var trimmed = text.Trim();
    // Match 1-3 emoji characters (including compound emoji like flags, skin tones)
    return Regex.IsMatch(trimmed, @"^(\p{So}|\p{Cs}{2}|\u200d|\ufe0f|\u20e3|[\u2600-\u27bf]|[\ud83c-\ud83f][\ud800-\udfff]){1,3}$")
           && trimmed.Length <= 14; // Safety cap
}
```

### 3.2 Apply Big Emoji CSS Class

In MessageBubble.razor, when rendering text content:
```razor
<div class="message-text @(IsEmojiOnly(Text) ? "emoji-only" : "")">@Text</div>
```

### 3.3 Add CSS

```css
.message-text.emoji-only {
    font-size: 2.8em;
    line-height: 1.2;
    padding: 4px 0;
}
.message-bubble:has(.emoji-only) {
    background: transparent;
    box-shadow: none;
    padding: 4px 8px;
}
```

### 3.4 Recent Emoji (Already Implemented)

Recent emoji already persists in `localStorage["emoji.recent"]` (CSV, max 30 items). Survives browser sessions. No change needed — already works per research.

---

## Phase 4: Simplify Home Page

### 4.1 Replace Technical Content

**File**: `src/ToledoMessage/Components/Pages/Home.razor`

| Section | Current | New |
|---------|---------|-----|
| Hero badge | "Quantum-Resistant Messaging" | "Secure Messaging" |
| Hero title | "Secure Messaging for Tomorrow's Threat" | "Private Messaging Made Simple" |
| Hero description | Technical crypto description | "Send messages, photos, and voice notes — all protected with the strongest encryption. Only you and the people you talk to can read them." |
| Stat 1 | "256-bit / AES Encryption" | "End-to-End / Encrypted" |
| Stat 2 | "Hybrid PQ / Key Exchange" | "Future-Proof / Protection" |
| Stat 3 | "Zero-Knowledge / Server Architecture" | "Private / By Design" |
| Feature 1 title | "Post-Quantum Safe" | "Always Protected" |
| Feature 1 desc | "ML-KEM-768 + X25519..." | "Your messages are secured with cutting-edge encryption that protects against both today's and tomorrow's threats" |
| Feature 2 desc | "Signal Protocol with Double Ratchet..." | "Only you and the person you're talking to can read your messages. Not even we can." |
| Feature 3 desc | "All cryptography runs in your browser..." | "Everything is encrypted on your device before it's sent. We never see your messages or personal data." |

### 4.2 Fix Header Button Styles

The "Sign In" link in the Home page header has no styling class while "Get Started" has `btn-nav`. Fix:

```html
<!-- Before -->
<a href="/login">Sign In</a>
<a href="/register" class="btn-nav">Get Started</a>

<!-- After -->
<a href="/login" class="btn-nav-outline">Sign In</a>
<a href="/register" class="btn-nav">Get Started</a>
```

Add CSS:
```css
.btn-nav-outline {
    color: #f8fafc !important;
    padding: 8px 20px;
    border-radius: 6px;
    font-weight: 600;
    border: 1px solid rgba(255, 255, 255, 0.3);
}
.btn-nav-outline:hover {
    background: rgba(255, 255, 255, 0.1) !important;
}
```

---

## Phase 5: Fix Device Re-Registration (Critical)

### 5.1 Root Cause

When `Login.razor` (line 120-125) checks `GET /api/devices` and the stored `local.deviceId` is not in the server's device list (because the database was reset during redeployment), it forces a full re-registration with NEW keys — creating a new device identity.

### 5.2 Solution: Device Restore

When the server doesn't have the device record but the client has the original keys in localStorage:

1. **Client side (Login.razor)**: Instead of generating new keys, check if identity keys exist in localStorage
2. If keys exist → send a "restore device" request with the existing public keys
3. Server creates a new device record with the same public keys (the device ID will be new, but the identity is preserved)
4. Client generates fresh pre-keys/OTPKs (these are ephemeral anyway)

### 5.3 Implementation

**Login.razor** — modify the "device not found" branch (lines 133-145):

```csharp
// Current: always generates new keys
// New: check for existing keys first
var existingIdentity = await Storage.GetAsync("local.identityKeyPublicClassical");
if (existingIdentity is not null)
{
    // Restore: reuse identity keys, generate fresh pre-keys
    var request = await KeyGen.RestoreKeysAndBuildRequest(deviceName);
    var restoreResponse = await Http.PostAsJsonAsync("/api/devices", request);
    // ... store new deviceId
}
else
{
    // Truly new device: full key generation
    var request = await KeyGen.GenerateAndStoreKeys(deviceName);
    // ...
}
```

This is actually already how the `SharedKeysEnabled` (key backup) flow works — `RestoreKeysAndBuildRequest` reuses identity keys and generates fresh pre-keys. The fix extends this to the "device not found on server" case too, without needing the key backup feature.

**No new API endpoint needed** — the existing `POST /api/devices` already accepts any valid public keys. The only change is client-side: reuse stored keys instead of generating new ones.

---

## Task Summary

| # | Task | Files | Priority |
|---|------|-------|----------|
| T1 | Add responsive breakpoints (480px, landscape) | `app.css` | High |
| T2 | Make emoji picker responsive | `app.css` | Medium |
| T3 | Make forward dialog responsive | `app.css` | Medium |
| T4 | Make voice recorder responsive | `app.css` | Medium |
| T5 | Image preview mobile sizing | `app.css` | Medium |
| T6 | Audio bar CSS polish | `app.css` | Low |
| T7 | CSS audit (theme vars, z-index) | `app.css` | Low |
| T8 | Add big emoji detection + rendering | `MessageBubble.razor`, `app.css` | High |
| T9 | Simplify home page content | `Home.razor` | High |
| T10 | Fix home page button styles | `Home.razor`, `app.css` | Medium |
| T11 | Fix device restore on redeployment | `Login.razor` | Critical |
| T12 | Test all viewports | Manual | High |

---

## Testing Checklist

### Responsive
- [ ] 320px iPhone SE: chat, sidebar, input, emoji picker, voice recorder all fit
- [ ] 375px iPhone 12: same checks
- [ ] 768px iPad portrait: two-panel layout works
- [ ] 1024px iPad landscape: same
- [ ] 1280px+ desktop: same
- [ ] Landscape mobile: input area usable, header not too tall

### Big Emoji
- [ ] Single emoji message (e.g., "😀") renders large (~2.8x)
- [ ] Two emoji message (e.g., "😀😂") renders large
- [ ] Three emoji message renders large
- [ ] Four+ emoji renders normal size
- [ ] Emoji + text renders normal size
- [ ] Emoji reactions still work normally

### Home Page
- [ ] No technical jargon visible (no "ML-KEM", "X25519", "AES", "Double Ratchet")
- [ ] Sign In button styled consistently with Get Started
- [ ] All text is user-friendly and non-technical

### Device Restore
- [ ] Login on browser A → register device → redeploy server (without DB reset) → login again → SAME device
- [ ] Login on browser A → register device → reset database → login again → device restored with same identity keys
- [ ] Login on browser B (no previous keys) → normal new device registration
- [ ] Login on browser A after clearing localStorage → normal new device registration
