# Research: UX & Responsive Enhancements

**Phase**: 0 — Outline & Research
**Date**: 2026-03-02
**Status**: Complete (all unknowns resolved)

## 1. Responsive CSS Gaps

**Decision**: Add breakpoints for 480px (small phone), improve 768px (tablet), and add landscape handling.

**Current State**:
- 3 breakpoints exist: mobile (<768px), tablet (768-991px), desktop (992+)
- Chat layout has slide-over mobile nav (works)
- Missing: voice recorder, emoji picker, forward dialog, input area are NOT responsive
- No landscape orientation handling
- No very-small-screen support (<480px)

**Gaps Found**:
| Element | Current | Fix |
|---------|---------|-----|
| `.msg-textarea` | Fixed 150px max-height all sizes | Reduce on small screens |
| `.voice-recorder-bar` | No mobile variant | Add responsive sizing |
| `.emoji-picker-popup` | Fixed 320px × 380px | Full-width on mobile |
| `.forward-dialog` | Fixed 420px max-width | Responsive width on mobile |
| Modal overlays | Fixed sizing | Responsive to viewport |
| Landscape mode | No handling | Reduce chrome in landscape |

## 2. Device Fingerprinting Issue

**Decision**: Use browser fingerprint (stable across redeployments) instead of relying solely on server device records.

**Root Cause**: After server redeployment, if the database is reset/migrated and device records are lost, the client's `local.deviceId` in localStorage no longer matches any server record. Login.razor (line 120-125) detects this mismatch and forces re-registration as a new device.

**The flow**:
1. User logs in → `GET /api/devices` returns device list
2. Client checks if stored `local.deviceId` exists in server list
3. If not found → clears auth and re-registers as NEW device
4. This happens because server device records were deleted during redeploy

**Recommendation**: Implement device re-authentication instead of re-registration.

When stored deviceId is not found on server:
1. Check if client still has the original identity keys in localStorage
2. If yes → re-register the device with the SAME public keys, effectively restoring the device record
3. If no → truly a new device, register normally

**Alternative considered**: Browser fingerprinting (canvas, WebGL, user agent) — rejected because fingerprints change with browser updates and are privacy-invasive.

**Alternative considered**: Store deviceId in a cookie — rejected because cookies are cleared more often than localStorage.

**Implementation**:
- Modify `Login.razor` device validation (lines 120-145): when device not found on server, attempt to re-register with existing keys instead of generating new ones
- Add `PUT /api/devices/{id}/restore` endpoint that accepts existing public keys and recreates the device record
- This preserves the device identity across server redeployments

## 3. Big Emoji Display

**Decision**: Detect messages with only 1-3 emoji characters (no other text) and render them at 2.5x size.

**Current State**: No emoji-only detection exists. All messages render at `.message-text` default font size.

**Implementation**:
- Add `IsEmojiOnly(string text)` helper in `MessageBubble.razor`
- Uses regex to detect 1-3 Unicode emoji with optional whitespace, no other text
- Apply CSS class `.emoji-only` with `font-size: 2.5em; line-height: 1.2`
- Remove message bubble padding/background for emoji-only (like WhatsApp)

**Recent Emoji Persistence**: Already implemented via `localStorage["emoji.recent"]` (CSV format, max 30). Persists across sessions for the same browser. This is per-device, not cross-device — acceptable for UX.

## 4. Home Page Simplification

**Decision**: Remove all technical jargon. Replace with user-friendly language.

**Technical content to remove/replace**:
| Current | Replace With |
|---------|-------------|
| "256-bit AES Encryption" | "Military-Grade Security" |
| "Hybrid PQ Key Exchange" | "Future-Proof Protection" |
| "Zero-Knowledge Server Architecture" | "Private by Design" |
| "ML-KEM-768 + X25519 hybrid encryption protects against future quantum computers" | "Your messages are protected with the strongest encryption available — both now and in the future" |
| "Signal Protocol with Double Ratchet ensures only you and your recipient can read messages" | "Only you and the person you're talking to can read your messages. Not even we can." |
| "All cryptography runs in your browser. The server never sees your plaintext or private keys" | "Everything is encrypted on your device. We never see your messages." |
| "Quantum-Resistant Messaging" badge | "Secure Messaging" badge |

## 5. Auth Button Style Mismatch

**Decision**: Both Login and Register pages already use identical `btn-primary btn-full` classes. The real inconsistency is on the Home page where "Sign In" is an unstyled link and "Get Started" uses `btn-nav`.

**Fix**: Make the Home page header "Sign In" link styled consistently with `btn-secondary-sm` (outlined) and keep "Get Started" as `btn-nav` (solid). The hero section CTA buttons (`btn-primary-lg` and `btn-secondary-lg`) are intentionally different — they're call-to-action buttons, not form buttons.

## 6. Image Preview & Audio Bar Review

**Decision**: Both are already fully implemented. Minor enhancements only.

**Image Preview**:
- Inline display with `max-height: 400px` ✓
- Lightbox with zoom (0.5x-5x), pan, prev/next navigation ✓
- Minor fix: image preview sizing on mobile could be tighter

**Audio Recording Bar**:
- Recording phase: pulsing red dot, animated waveform, slide-to-cancel ✓
- Preview phase: play/pause, seek slider, waveform progress ✓
- Minor fix: not responsive on small screens, bars may overflow
