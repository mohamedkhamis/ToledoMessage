# Research: Admin Panel with Global Settings

**Branch**: `010-admin-global-settings` | **Date**: 2026-03-14

## R-001: Admin Panel Hosting Strategy

**Decision**: Separate Blazor WASM project (`ToledoVault.Admin`) hosted by the existing `ToledoVault` server project at a separate URL (e.g., `admin.chat.khamis.work`).

**Rationale**: The existing server already hosts the client Blazor WASM app via `MapRazorComponents`. A second Blazor WASM app can be hosted alongside it on a separate path prefix (`/admin`) or via reverse proxy routing (Cloudflare rules route `admin.chat.khamis.work` → same server, `/admin` path). This avoids a second deployment while keeping admin UI code isolated from client code.

**Alternatives considered**:
- Embedded in existing client app — rejected: pollutes client bundle with admin code, increases WASM download for all users
- Completely separate server — rejected: doubles infrastructure, requires separate DB connection, more complex deployment

**Implementation approach**: The server project will serve both apps. The admin Blazor WASM app is a separate project (`ToledoVault.Admin`) registered as an additional assembly in `Program.cs`. Admin routes are prefixed with `/admin`. Reverse proxy routes `admin.chat.khamis.work` to the same server.

## R-002: Admin Authentication Model

**Decision**: Config-driven credentials with forced password change on first login. Admin credentials stored in `appsettings.json` with hashed password persisted in a `AdminCredentials` DB table after first password change.

**Rationale**: Config-driven is simple and avoids coupling admin auth to the user system. After first login with default credentials, the admin sets a permanent password stored as a hashed value in the DB. Subsequent logins authenticate against the DB hash. JWT tokens for admin use a separate issuer claim (`admin`) to distinguish from user tokens.

**Alternatives considered**:
- Shared user table with `IsAdmin` flag — rejected per clarification: admin is independent
- Hardcoded password only — rejected: insecure, can't change

**Implementation approach**:
1. `appsettings.json` defines `Admin:Username` and `Admin:DefaultPasswordHash`
2. On first login, if no `AdminCredentials` row exists → authenticate against config hash → force password change → store new hash in DB
3. On subsequent logins, authenticate against DB hash
4. Issue JWT with `role: admin` claim, separate from user JWTs
5. Admin endpoints validate the `admin` role claim

## R-003: Global Settings Storage

**Decision**: `GlobalSettings` database table with key-value pattern and metadata columns (type, category, validation, defaults).

**Rationale**: DB-backed settings allow runtime changes without restart. The key-value pattern with metadata makes the system extensible — adding a new setting is just an INSERT/seed, no schema changes needed. Settings are cached in memory with cache invalidation on write.

**Alternatives considered**:
- `appsettings.json` — rejected: requires restart for changes, not suitable for runtime modification
- Separate settings DB — rejected: unnecessary complexity, same SQL Server instance is sufficient

## R-004: Serilog Database Sink

**Decision**: Add `Serilog.Sinks.MSSqlServer` NuGet package to write structured logs to a `LogEntries` table in the existing database.

**Rationale**: The existing Serilog setup uses console + file sinks. Adding the SQL Server sink is a one-line configuration change. The `LogEntries` table gets automatic structured columns (Timestamp, Level, Message, Exception, Properties) with efficient indexing for the admin log viewer queries.

**Alternatives considered**:
- Parse text log files — rejected: poor performance for filtering/pagination at scale
- Separate logging database — rejected: unnecessary for the expected log volume

**Implementation approach**:
1. Add `Serilog.Sinks.MSSqlServer` NuGet package to server project
2. Configure in `Program.cs` Serilog setup: `.WriteTo.MSSqlServer(connectionString, "LogEntries", autoCreateSqlTable: true)`
3. Add indexes on `TimeStamp`, `Level` columns for efficient filtering
4. Admin log viewer queries this table via EF Core or raw SQL

## R-005: Localization Editor Storage

**Decision**: Store localization overrides in a `LocalizationOverrides` database table. The .resx files remain as the baseline; DB entries override them at runtime.

**Rationale**: Directly modifying .resx files at runtime is fragile — they're compiled resources, changes require app restart or custom resource manager reload logic. A DB override table allows instant changes: the custom `IStringLocalizer` checks DB first, falls back to .resx. New keys added via admin go to DB immediately.

**Alternatives considered**:
- Direct .resx file editing — rejected: requires app restart, filesystem write permissions, complex reload
- Replace .resx entirely with DB — rejected: loses compile-time checking, existing code uses `IStringLocalizer` with .resx

**Implementation approach**:
1. `LocalizationOverrides` table: Key, LanguageCode, Value, LastModifiedAt
2. Custom `IStringLocalizer` decorator that checks DB overrides first, falls back to .resx
3. In-memory cache with invalidation on admin writes
4. Admin UI shows merged view: .resx baseline + DB overrides (highlighted)

## R-006: Admin API Controllers

**Decision**: New admin-specific controllers in the server project under a `/api/admin/` route prefix, protected by `[Authorize(Roles = "admin")]`.

**Rationale**: Keeps admin endpoints in the same server process (same DB context, same services) while clearly separating them from user-facing endpoints. The existing `BaseApiController` pattern can be reused.

**Controllers needed**:
- `AdminAuthController` — login, change-password, token refresh
- `AdminSettingsController` — CRUD for global settings
- `AdminLogsController` — query/filter/paginate log entries
- `AdminLocalizationController` — CRUD for localization overrides
