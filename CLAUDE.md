# ToledoMessage Development Guidelines

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
ToledoMessage.slnx
src/
  ToledoMessage/              # Server — API, SignalR hub, Blazor host
  ToledoMessage.Client/       # Blazor WASM client
  ToledoMessage.Shared/       # DTOs, enums, constants, converters
  ToledoMessage.Crypto/       # Signal Protocol + hybrid PQ crypto
  Toledo.SharedKernel/        # Cross-cutting (IdGenerator)
tests/
  ToledoMessage.Server.Tests/       # ~152 tests (MSTest, InMemory EF)
  ToledoMessage.Crypto.Tests/       # ~65 tests
  ToledoMessage.Client.Tests/       # ~8 tests
  ToledoMessage.Integration.Tests/  # ~8 tests
  ToledoMessage.Benchmarks/         # BenchmarkDotNet perf tests
```

## Commands

```bash
# Build
dotnet build ToledoMessage.slnx

# Run (dev)
dotnet run --project src/ToledoMessage
# Listens: http://localhost:5005, https://localhost:7159

# Test all
dotnet test ToledoMessage.slnx

# Test specific project
dotnet test tests/ToledoMessage.Server.Tests
dotnet test tests/ToledoMessage.Crypto.Tests
dotnet test tests/ToledoMessage.Client.Tests
dotnet test tests/ToledoMessage.Integration.Tests

# EF Migrations
dotnet ef migrations add <Name> --project src/ToledoMessage
dotnet ef database update --project src/ToledoMessage

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

**Migrations**: InitialCreate, AddDisplayNameSecondary, UpdateFontSizeDefault

## Key Services

**Server**: PreKeyService, MessageRelayService, AccountDeletionService, PresenceService, RateLimitService, LinkPreviewService, MessageCleanupHostedService, AccountDeletionHostedService

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
- **Auth**: JWT HS256, 15-min access token, 30-day refresh token. Claims: sub, unique_name, name, name2, jti.
- **Crypto**: Hybrid approach — every operation combines classical (X25519/Ed25519/AES-256-GCM) + post-quantum (ML-KEM-768/ML-DSA-65). Signal Protocol X3DH + Double Ratchet.
- **TransactionFilter**: Wraps every controller action in a DB transaction. Commits on 2xx/3xx, rolls back on 4xx/5xx.
- **Message limits**: 64 KB text, 16 MB media. Server retention: 90 days undelivered.
- **Device limit**: Max 10 per user. Max 100 group participants.
- **Pre-keys**: Batch size 100, low threshold 10.
- **Account deletion**: 7-day grace period.
- **Logging**: Serilog to console + `logs/toledomessage-.log` (daily rolling).
- **Health check**: GET `/health` (DB connectivity).

## Client JS Interop Files

- `wwwroot/storage.js` — localStorage/sessionStorage/cookie helpers
- `wwwroot/voice-recorder.js` — Browser MediaRecorder API
- `wwwroot/media-helpers.js` — Image/file/blob utilities

## CSS & Theming

- `src/ToledoMessage/wwwroot/app.css` — Base styles, layout, all components
- `src/ToledoMessage/wwwroot/themes.css` — 8 themes: default, default-dark, whatsapp, whatsapp-dark, telegram, telegram-dark, signal, signal-dark

## Code Style

- C# standard conventions, nullable reference types enabled, implicit usings
- Follow existing patterns in the codebase

## Configuration

- **Connection string**: `appsettings.json` → `ConnectionStrings:DefaultConnection` (SQL Server)
- **JWT secret**: `Jwt:SecretKey` (min 32 chars for HS256)
- **Dev URL**: http://localhost:5005
- **Prod URL**: https://chat.khamis.work (Cloudflare)

<!-- MANUAL ADDITIONS START -->
## Commit Workflow
- **NEVER commit any changes without user approval**
- Always ask the user to type "commit" before committing
- After completing any work, summarize changes and ask: "Ready to commit?"
- Wait for user to explicitly type "commit" before running git add/commit/push
- **Bug tracking:** All bugs go in `BUGS.md` (project root) — see its Bug Workflow section for format and rules
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
