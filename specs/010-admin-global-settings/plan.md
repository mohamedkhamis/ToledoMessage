# Implementation Plan: Admin Panel with Global Settings

**Branch**: `010-admin-global-settings` | **Date**: 2026-03-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/010-admin-global-settings/spec.md`

## Summary

Add a separate admin panel (Blazor WASM) for managing global application settings, viewing/filtering logs, and editing localization strings at runtime. The admin panel is hosted by the existing server at `/admin`, uses config-driven authentication with forced password change on first login, and stores settings in the database for immediate runtime effect. Logs are written to a SQL Server table via Serilog sink for efficient querying. Localization overrides are stored in DB and merged with .resx baselines at runtime.

## Technical Context

**Language/Version**: C# / .NET 11.0 (preview, pinned in global.json)
**Primary Dependencies**: ASP.NET Core, Blazor WASM, EF Core 11, Serilog, Serilog.Sinks.MSSqlServer, JWT (HS256)
**Storage**: SQL Server 2022 (EF Core Code First) — 4 new tables: AdminCredentials, GlobalSettings, LogEntries, LocalizationOverrides
**Testing**: MSTest 4.1.0 (matching existing test projects)
**Target Platform**: Windows Server (IIS) + Cloudflare reverse proxy
**Project Type**: Web application (Blazor WASM admin panel + ASP.NET Core API)
**Performance Goals**: Log queries < 2s for 100K entries, settings CRUD < 200ms
**Constraints**: Admin panel must not affect main app bundle size or startup time
**Scale/Scope**: Single admin user, ~165 localization keys, ~10 global settings initially

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Zero-Trust Server | PASS | Admin panel manages server-side settings only. No client crypto operations affected. Settings like encryption key length are informational/configuration — actual crypto runs client-side per existing architecture. |
| II. Hybrid Cryptography | PASS | No changes to cryptographic operations. Admin settings that reference encryption parameters affect UI display/defaults, not the actual crypto code. |
| III. Established Libraries Only | PASS | No new crypto code. Serilog.Sinks.MSSqlServer is a well-maintained NuGet package. |
| IV. Signal Protocol Fidelity | PASS | No changes to messaging protocol. |
| V. .NET Ecosystem | PASS | Blazor WASM, ASP.NET Core, EF Core, SQL Server — all within stack. |
| VI. Test-First Development | PASS | Test project planned (`ToledoVault.Admin.Tests`). |
| VII. Open-Source Transparency | PASS | All admin code is open-source, no secrets in code. |

**Re-check after Phase 1**: All principles still satisfied. The admin panel adds no crypto code, introduces no new security primitives, and follows existing patterns.

## Project Structure

### Documentation (this feature)

```text
specs/010-admin-global-settings/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research decisions
├── data-model.md        # Entity definitions
├── quickstart.md        # Setup guide
├── contracts/
│   └── admin-api.md     # REST API contracts
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Task breakdown (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── ToledoVault/                          # Server (existing + new admin endpoints)
│   ├── Controllers/
│   │   └── Admin/                        # NEW: Admin API controllers
│   │       ├── AdminAuthController.cs
│   │       ├── AdminSettingsController.cs
│   │       ├── AdminLogsController.cs
│   │       └── AdminLocalizationController.cs
│   ├── Models/
│   │   ├── AdminCredential.cs            # NEW
│   │   ├── GlobalSetting.cs              # NEW
│   │   └── LocalizationOverride.cs       # NEW
│   ├── Data/
│   │   ├── ApplicationDbContext.cs        # MODIFIED: add 3 new DbSets
│   │   └── Configurations/
│   │       ├── AdminCredentialConfiguration.cs   # NEW
│   │       ├── GlobalSettingConfiguration.cs     # NEW
│   │       └── LocalizationOverrideConfiguration.cs # NEW
│   ├── Services/
│   │   ├── AdminAuthService.cs           # NEW: config + DB hybrid auth
│   │   ├── GlobalSettingsService.cs      # NEW: CRUD + caching
│   │   ├── LocalizationOverrideService.cs # NEW: DB overrides
│   │   └── AdminLocalizationStringLocalizer.cs # NEW: IStringLocalizer decorator
│   └── Program.cs                        # MODIFIED: Serilog SQL sink, admin WASM hosting
│
├── ToledoVault.Admin/                    # NEW: Admin Blazor WASM project
│   ├── ToledoVault.Admin.csproj
│   ├── _Imports.razor
│   ├── Pages/
│   │   ├── Login.razor
│   │   ├── ChangePassword.razor
│   │   ├── Dashboard.razor
│   │   ├── Settings.razor
│   │   ├── Logs.razor
│   │   └── Localization.razor
│   ├── Components/
│   │   └── Layout/
│   │       └── AdminLayout.razor
│   ├── Services/
│   │   ├── AdminAuthService.cs
│   │   └── AdminApiService.cs
│   └── wwwroot/
│       └── admin.css
│
└── ToledoVault.Shared/                   # Shared (existing + new DTOs)
    └── DTOs/
        ├── AdminLoginRequest.cs          # NEW
        ├── AdminLoginResponse.cs         # NEW
        ├── AdminChangePasswordRequest.cs # NEW
        ├── GlobalSettingResponse.cs       # NEW
        ├── UpdateSettingRequest.cs        # NEW
        ├── LogEntryResponse.cs           # NEW
        ├── LogQueryRequest.cs            # NEW
        ├── PaginatedResponse.cs          # NEW
        ├── LocalizationEntryResponse.cs  # NEW
        ├── UpdateLocalizationRequest.cs  # NEW
        └── CreateLocalizationKeyRequest.cs # NEW

tests/
└── ToledoVault.Admin.Tests/              # NEW: Admin test project
    ├── ToledoVault.Admin.Tests.csproj
    ├── Controllers/
    │   ├── AdminAuthControllerTests.cs
    │   ├── AdminSettingsControllerTests.cs
    │   ├── AdminLogsControllerTests.cs
    │   └── AdminLocalizationControllerTests.cs
    └── Services/
        ├── GlobalSettingsServiceTests.cs
        └── LocalizationOverrideServiceTests.cs
```

**Structure Decision**: The admin panel is a new Blazor WASM class library (`ToledoVault.Admin`) hosted by the existing server project alongside the main client app. Admin API endpoints live in the server project under `Controllers/Admin/`. This keeps a single deployment while isolating admin code. The admin WASM app loads only when navigating to `/admin` routes — no impact on main app bundle size.

## Complexity Tracking

No constitution violations to justify. The architecture follows existing patterns:
- New Blazor WASM project follows the same pattern as `ToledoVault.Client`
- New controllers follow the same `BaseApiController` pattern
- New EF Core entities follow existing model/configuration patterns
- New services follow existing DI registration patterns
