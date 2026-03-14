# ToledoVault Development Guidelines

Last updated: 2026-03-12

## Tech Stack
- **Runtime**: .NET 11.0 (preview) — `global.json` pins SDK 11.0.100-preview.1
- **Server**: ASP.NET Core, EF Core 11, SignalR, Serilog, JWT (HS256)
- **Client**: Blazor WebAssembly (hosted), SignalR client, IndexedDB (via JS interop)
- **Database**: SQL Server 2022 (EF Core Code First)
- **Crypto**: BouncyCastle.Cryptography 2.7.0-beta.98 (Signal Protocol + hybrid post-quantum ML-KEM-768 / ML-DSA-65)
- **Tests**: MSTest 4.1.0, coverlet, BenchmarkDotNet
- **Localization**: English (en), Arabic (ar)

## Solution Structure

```
ToledoVault.slnx
src/
  ToledoVault/              # Server — API, SignalR hub, Blazor host
  ToledoVault.Client/       # Blazor WASM client
  ToledoVault.Shared/       # DTOs, enums, constants, converters
  ToledoVault.Crypto/       # Signal Protocol + hybrid PQ crypto
  Toledo.SharedKernel/        # Cross-cutting (IdGenerator)
tests/
  ToledoVault.Server.Tests/       # ~152 tests (MSTest, InMemory EF)
  ToledoVault.Crypto.Tests/       # ~65 tests
  ToledoVault.Client.Tests/       # ~8 tests
  ToledoVault.Integration.Tests/  # ~8 tests
  ToledoVault.Benchmarks/         # BenchmarkDotNet perf tests
```

## Commands

```bash
# Build
dotnet build ToledoVault.slnx

# Run (dev)
dotnet run --project src/ToledoVault
# Listens: http://localhost:5005, https://localhost:7159

# Test all
dotnet test ToledoVault.slnx

# Test specific project
dotnet test tests/ToledoVault.Server.Tests
dotnet test tests/ToledoVault.Crypto.Tests
dotnet test tests/ToledoVault.Client.Tests
dotnet test tests/ToledoVault.Integration.Tests

# EF Migrations
dotnet ef migrations add <Name> --project src/ToledoVault
dotnet ef database update --project src/ToledoVault

# Deploy to local IIS
powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force
powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Action Status
```

## REST API Routes

All routes prefixed with `/api`. All require `[Authorize]` except register/login/refresh.

| Controller | Base Route | Key Endpoints |
|---|---|---|
| AuthController | `/api/auth` | POST register, register-with-device, login, refresh, logout, logout-all; DELETE account |
| ConversationsController | `/api/conversations` | GET list, POST create, POST group, GET/{id}, participants CRUD, PUT timer |
| DevicesController | `/api/devices` | POST register, GET list, DELETE/{id}, prekeys count & upload |
| MessagesController | `/api/messages` | POST send, GET pending, POST acknowledge, POST read, GET unread-count |
| UsersController | `/api/users` | GET search, GET prekey-bundle, GET devices |
| PreferencesController | `/api/preferences` | GET, PUT |
| KeyBackupController | `/api/keys/backup` | POST, GET, DELETE |
| LinkPreviewController | `/api/link-preview` | GET |

## SignalR Hub — `/hubs/chat`

**Client-to-Server**: RegisterDevice, SendMessage, AcknowledgeDelivery, AdvanceReadPointer, TypingIndicator, AddReaction, RemoveReaction, DeleteForEveryone, ClearMessages, IsUserOnline

**Server-to-Client**: ReceiveMessage, MessageDelivered, MessageRead, UserTyping, UserOnline, UserOffline, ReactionAdded, ReactionRemoved, MessageDeleted, MessagesDelivered

**Rate limits**: SendMessage 60/min, TypingIndicator 10/min (per user)

## Blazor Pages & Routes

| Page | Route | Layout |
|---|---|---|
| Login | `/login` | MainLayout |
| Register | `/register` | MainLayout |
| ChatList | `/chat` | ChatLayout |
| Chat | `/chat/{ConversationId:long}` | ChatLayout |
| NewConversation | `/new-conversation` | ChatLayout |
| Settings | `/settings` | ChatLayout |
| SecurityInfo | `/security/{DeviceId:long}` | ChatLayout |

All chat pages use `@rendermode InteractiveWebAssemblyRenderMode(prerender: false)`.
ChatLayout is in the **client project** (WASM can't load server assemblies).

## Database Schema (11 tables)

Users, Devices, OneTimePreKeys, Conversations, ConversationParticipants, EncryptedMessages, RefreshTokens, UserPreferences, MessageReactions, EncryptedKeyBackups, ConversationReadPointers

**Migrations**: InitialCreate, AddDisplayNameSecondary, UpdateFontSizeDefault, AddRefreshTokenIsPersistent, AddSendPhotoHdPreference

## Key Services

**Server**: PreKeyService, MessageRelayService, AccountDeletionService (BackgroundService), PresenceService, RateLimitService, LinkPreviewService, MessageCleanupHostedService (BackgroundService)

**Client**: LocalStorageService, SignalRService, CryptoService, SessionService, MessageEncryptionService, KeyGenerationService, KeyBackupService, KeyBackupCryptoService, FingerprintService, PreKeyReplenishmentService, MessageExpiryService, MessageStoreService, ThemeService, PreferencesService, ToastService, AuthTokenHandler

## Server Middleware Pipeline (order)

1. Security headers (X-Content-Type-Options, X-Frame-Options, CSP)
2. Exception handler (non-dev)
3. Static files
4. Serilog request logging
5. CORS
6. Response compression (Brotli > Gzip)
7. Authentication (JWT Bearer + SignalR query string)
8. Rate limiting middleware
9. Authorization
10. Antiforgery
11. Route mapping: MapStaticAssets, MapControllers, MapHub, MapHealthChecks, MapRazorComponents

## Key Architecture Notes

- **IDs**: `long` type everywhere, generated by `IdGenerator.GetNewId()` (63-bit random). Serialized as JSON strings via `LongToStringConverter` to avoid JS precision loss.
- **Auth**: JWT HS256, 15-min access token. Refresh tokens: 90 days (remember me ON, default) or 24 hours (remember me OFF). Claims: sub, unique_name, name, name2, jti. Refresh tokens are device-bound (`RefreshToken.DeviceId`) and carry `IsPersistent` flag for TTL inheritance on rotation. Auto-login on app load via silent token refresh.
- **Crypto**: Hybrid approach — every operation combines classical (X25519/Ed25519/AES-256-GCM) + post-quantum (ML-KEM-768/ML-DSA-65). Signal Protocol X3DH + Double Ratchet.
- **TransactionFilter**: Wraps every controller action in a DB transaction. Commits on 2xx/3xx, rolls back on 4xx/5xx.
- **Message limits**: 64 KB text, 16 MB media. Server retention: 90 days undelivered.
- **Device limit**: Max 10 per user. Max 100 group participants.
- **Pre-keys**: Batch size 100, low threshold 10.
- **Account deletion**: 7-day grace period.
- **Logging**: Serilog to console + `logs/toledovault-.log` (daily rolling).
- **Health check**: GET `/health` (DB connectivity).

## Client JS Interop Files

- `wwwroot/storage.js` — localStorage/sessionStorage/cookie helpers
- `wwwroot/voice-recorder.js` — Browser MediaRecorder API
- `wwwroot/media-helpers.js` — Image/file/blob utilities

## PWA (Progressive Web App) Support

PWA artifacts in `src/ToledoVault/wwwroot/`:
- `manifest.json` — Web App Manifest for installability
- `service-worker.js` — Service Worker for offline caching
- `offline.html` — Offline fallback page
- `icon-192.png` — 192x192 app icon
- `icon-512.png` — 512x512 app icon

### PWA Cache Versioning
- Bump `CACHE_VERSION` in `service-worker.js` on every deploy
- Current format: `'toledo-vN'` where N is an incrementing integer
- Forgetting to bump the version means users will get stale cached files

### PWA Requirements
- HTTPS required for full PWA features (except localhost for development)
- Service worker scope is `/` (root)
- iOS requires proprietary `<meta>` tags in addition to manifest

## CSS & Theming

- `src/ToledoVault/wwwroot/app.css` — Base styles, layout, all components
- `src/ToledoVault/wwwroot/themes.css` — 8 themes: default, default-dark, whatsapp, whatsapp-dark, telegram, telegram-dark, signal, signal-dark

## Project Phase

This is a **production SaaS** product, not an MVP. All features should be implemented with:
- Full production quality — no shortcuts, no "good enough for now"
- Proper error handling, logging, and security hardening
- Complete localization (English + Arabic)
- Responsive design (desktop + mobile)
- Test coverage for new server-side logic

## Code Style

- C# standard conventions, nullable reference types enabled, implicit usings
- Follow existing patterns in the codebase

## Configuration

- **Connection string**: `appsettings.json` → `ConnectionStrings:DefaultConnection` (SQL Server)
- **JWT secret**: `Jwt:SecretKey` (min 32 chars for HS256)
- **Dev URL**: http://localhost:5005
- **Prod URL**: https://chat.khamis.work (Cloudflare)

## Security Findings (Audit 2026-03-12)

**Dependency audit**: `dotnet list package --vulnerable` — **0 vulnerable packages** across all 10 projects.

### CRITICAL

| # | Finding | File:Line | Risk |
|---|---------|-----------|------|
| S-01 | **Ratchet state no integrity check** | `Client/Services/SessionService.cs:112` | Ratchet state loaded from IndexedDB without HMAC verification. XSS/extension/physical access can roll back state → nonce reuse with same derived key → AES-GCM breaks. Loss of forward secrecy. |
| S-02 | **SSRF via DNS rebinding in link preview** | `Services/LinkPreviewService.cs:32-68` | `IsPrivateHost()` validates IP at DNS resolution time but does not pin the resolved IP. Attacker domain can rebind to 127.0.0.1 between check and fetch. |

**S-01 Fix**: HMAC-SHA256 the serialized `RatchetState` with a device-local key before storing; verify on load; include a monotonic version counter to reject downgrades.
**S-02 Fix**: Resolve DNS once, validate the IP, then connect to that IP directly (pin DNS result for the request lifetime).

### HIGH

| # | Finding | File:Line | Risk |
|---|---------|-----------|------|
| S-03 | **SSRF — og:image URL not re-validated** | `Services/LinkPreviewService.cs:80-87` | Relative `og:image` resolved to absolute but not re-checked against `IsPrivateHost()`. Attacker page can set `og:image="http://169.254.169.254/..."`. |
| S-04 | **PBKDF2 iterations too low for key backup** | `Client/Services/KeyBackupCryptoService.cs:13` | 100,000 iterations — NIST 2023 minimum is 600,000. GPU brute-force feasible on user passwords. |
| S-05 | **No AEAD associated data in Double Ratchet** | `Crypto/Classical/AesGcmCipher.cs:11`, `Crypto/Protocol/DoubleRatchet.cs:123,141,162` | `associatedData` parameter exists but is never passed. Allows header/metadata swapping without detection. |
| S-06 | **Refresh token not device-bound** | `Controllers/AuthController.cs:236-240` | `RefreshToken.DeviceId` is nullable and not validated on refresh. Stolen token usable from any device. |
| S-07 | **No password change/reset endpoint** | `Controllers/AuthController.cs` | Compromised password cannot be remediated without account deletion. |
| S-08 | **Login timing oracle for username enumeration** | `Controllers/AuthController.cs:200-202` | Early return on unknown username skips hash verification → measurable timing difference reveals valid usernames. |

**S-03 Fix**: Call `IsPrivateHost()` on the resolved `og:image` absolute URI before returning it.
**S-04 Fix**: Increase to `600_000` iterations, or migrate to Argon2id (64 MB memory, 3 passes).
**S-05 Fix**: Pass `(header.RatchetPublicKey || header.PreviousChainLength || header.MessageIndex)` as AD to `AesGcmCipher.Encrypt/Decrypt`.
**S-06 Fix**: Make `DeviceId` non-nullable; validate device match during refresh.
**S-07 Fix**: Add `POST /api/auth/change-password` requiring old + new password.
**S-08 Fix**: Always run `VerifyHashedPassword` against a dummy hash when user not found, then return the same error.

### MEDIUM

| # | Finding | File:Line | Risk |
|---|---------|-----------|------|
| S-09 | **CSP allows unsafe-eval + unsafe-inline** | `Program.cs:205-208` | Required for Blazor WASM IL interpreter. Weakens XSS protection if injection vector found. |
| S-10 | **Response compression over HTTPS** | `Program.cs:60` | `EnableForHttps = true` — CRIME/BREACH oracle risk if user-controlled data compressed alongside secrets. |
| S-11 | **Access token not revocable on logout** | `Controllers/AuthController.cs:267-284` | Logout revokes refresh token but access token remains valid for up to 15 min. |
| S-12 | **No audit logging for security events** | `Program.cs:238-248` | Account deletion, device revocation, failed logins not logged at application level (only HTTP request logs). |
| S-13 | **JWT in SignalR query string** | `Program.cs:114` | Standard for WebSocket (can't use custom headers in handshake), but tokens appear in server access logs and proxy caches. |

**S-09**: Acceptable tradeoff — no `MarkupString` usage found in any .razor file. Monitor for XSS vectors.
**S-10 Fix**: Set `EnableForHttps = false`, or exclude authenticated API responses from compression.
**S-11 Fix**: Cache revoked JTIs in-memory (ConcurrentDictionary keyed by JTI with TTL = token expiry).
**S-12 Fix**: Add `ILogger` calls for: failed logins, account deletion, device register/revoke, password changes.
**S-13**: Mitigate by ensuring access logs exclude query strings, and using short-lived tokens (already 15 min).

### LOW

| # | Finding | File:Line | Risk |
|---|---------|-----------|------|
| S-14 | **Console.WriteLine leaks device IDs** | `Client/Services/CryptoService.cs:162` | Error messages with device IDs written to browser console. |
| S-15 | **No Unicode normalization on group names** | `Controllers/ConversationsController.cs:164` | Homograph attacks possible (Cyrillic "А" vs Latin "A"). |
| S-16 | **Dev JWT secret in appsettings.Development.json** | `appsettings.Development.json:12` | Hardcoded dev key — acceptable, production enforced at `Program.cs:87-88`. |

### VERIFIED SECURE (No Issues Found)

- **SQL injection**: All queries use parameterized EF Core or `ExecuteSqlRawAsync` with parameters ✓
- **XSS**: No `MarkupString` usage anywhere; all user content rendered as plain text ✓
- **[Authorize]**: All controllers and hub methods properly guarded; no missing auth ✓
- **IDOR**: All endpoints validate caller owns the resource via userId/participant checks ✓
- **Password hashing**: ASP.NET Identity `PasswordHasher<User>` (PBKDF2-HMAC-SHA256) ✓
- **JWT validation**: Strict — ValidateIssuer, Audience, Lifetime, SigningKey; 30s clock skew ✓
- **Refresh token rotation**: Old token revoked on each refresh ✓
- **Random token generation**: `RandomNumberGenerator.GetBytes(64)` — 512 bits entropy ✓
- **Constant-time comparison**: `CryptographicOperations.FixedTimeEquals` in crypto ✓
- **CORS**: Explicit origin whitelist, no wildcard, throws on empty config ✓
- **Sensitive data logging**: Explicitly excluded auth headers and request bodies ✓
- **Production secret enforcement**: Startup throws if placeholder key detected in non-dev ✓
- **NuGet vulnerabilities**: 0 vulnerable packages ✓

<!-- MANUAL ADDITIONS START -->
## Commit Workflow
- **NEVER commit any changes without user approval**
- Always ask the user to type "commit" before committing
- After completing any work, summarize changes and ask: "Ready to commit?"
- Wait for user to explicitly type "commit" before running git add/commit/push
- **Bug tracking:** All bugs go in `docs/BUGS.md` — see its Bug Workflow section for format and rules
- Before starting work, check `BUGS.md` for open bugs related to your feature
- After code review, report any bugs found to `BUGS.md` under "Open Bugs" with full template (severity, file/line, fix steps)
- After fixing a bug, move it from "Open Bugs" to "Resolved Bugs" with fix date
- Do NOT create separate `BUG-REPORT-*.md` files — use `BUGS.md` only

## Deploy Workflow
- After finishing tasks or a group of tasks, ask user: "Deploy to IIS?"
- Run: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Force`
- Requires Administrator privileges
- App deploys to http://localhost:8080
- Check status anytime: `powershell -ExecutionPolicy Bypass -File ./deploy-iis.ps1 -Action Status`
<!-- MANUAL ADDITIONS END -->
