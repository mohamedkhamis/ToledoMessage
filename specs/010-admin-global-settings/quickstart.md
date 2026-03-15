# Quickstart: Admin Panel with Global Settings

**Branch**: `010-admin-global-settings` | **Date**: 2026-03-14

## Prerequisites

- .NET 11.0 SDK (preview) — pinned in `global.json`
- SQL Server 2022 running locally
- Existing ToledoVault solution builds and tests pass

## New NuGet Packages

| Package | Project | Purpose |
|---------|---------|---------|
| `Serilog.Sinks.MSSqlServer` | ToledoVault (server) | Write structured logs to SQL Server |

## New Projects

| Project | Type | Description |
|---------|------|-------------|
| `ToledoVault.Admin` | Blazor WASM (class library) | Admin panel UI — pages, components, services |
| `ToledoVault.Admin.Tests` | MSTest | Admin panel tests |

## Project References

```
ToledoVault.Admin → ToledoVault.Shared (DTOs, constants)
ToledoVault (server) → ToledoVault.Admin (host the WASM app)
ToledoVault.Admin.Tests → ToledoVault.Admin, ToledoVault (for controller tests)
```

## Configuration (appsettings.json)

```json
{
  "Admin": {
    "Username": "admin",
    "DefaultPasswordHash": "MUST-BE-REPLACED-ON-FIRST-DEPLOY"
  },
  "Serilog": {
    "WriteTo": [
      { "Name": "MSSqlServer", "Args": { "connectionString": "DefaultConnection", "tableName": "LogEntries", "autoCreateSqlTable": true } }
    ]
  }
}
```

## Database Changes

New tables (via EF Core migration):
1. `AdminCredentials` — admin password storage
2. `GlobalSettings` — key-value settings store (seeded with defaults)
3. `LocalizationOverrides` — runtime localization overrides
4. `LogEntries` — auto-created by Serilog sink

## Key Files to Create

### Server (src/ToledoVault/)
- `Controllers/Admin/AdminAuthController.cs`
- `Controllers/Admin/AdminSettingsController.cs`
- `Controllers/Admin/AdminLogsController.cs`
- `Controllers/Admin/AdminLocalizationController.cs`
- `Models/AdminCredential.cs`
- `Models/GlobalSetting.cs`
- `Models/LocalizationOverride.cs`
- `Data/Configurations/AdminCredentialConfiguration.cs`
- `Data/Configurations/GlobalSettingConfiguration.cs`
- `Data/Configurations/LocalizationOverrideConfiguration.cs`
- `Services/AdminAuthService.cs`
- `Services/GlobalSettingsService.cs`
- `Services/LocalizationOverrideService.cs`
- `Services/AdminLocalizationStringLocalizer.cs` (custom IStringLocalizer decorator)

### Admin Client (src/ToledoVault.Admin/)
- `Pages/Login.razor`
- `Pages/ChangePassword.razor`
- `Pages/Dashboard.razor`
- `Pages/Settings.razor`
- `Pages/Logs.razor`
- `Pages/Localization.razor`
- `Components/Layout/AdminLayout.razor`
- `Services/AdminAuthService.cs`
- `Services/AdminApiService.cs`
- `wwwroot/admin.css`

### Shared DTOs (src/ToledoVault.Shared/DTOs/)
- `AdminLoginRequest.cs`
- `AdminLoginResponse.cs`
- `AdminChangePasswordRequest.cs`
- `GlobalSettingResponse.cs`
- `UpdateSettingRequest.cs`
- `LogEntryResponse.cs`
- `LogQueryRequest.cs`
- `PaginatedResponse.cs`
- `LocalizationEntryResponse.cs`
- `UpdateLocalizationRequest.cs`
- `CreateLocalizationKeyRequest.cs`

## Build & Run

```bash
# Build entire solution
dotnet build ToledoVault.slnx

# Run (serves both main app and admin panel)
dotnet run --project src/ToledoVault

# Main app: https://localhost:7159
# Admin panel: https://localhost:7159/admin

# Run tests
dotnet test ToledoVault.slnx
```

## Verification Checklist

1. [ ] `dotnet build ToledoVault.slnx` — 0 errors
2. [ ] `dotnet test ToledoVault.slnx` — all tests pass
3. [ ] Navigate to `/admin` → redirects to admin login
4. [ ] Login with default credentials → forced to change password
5. [ ] After password change → can access settings page
6. [ ] Modify a setting → change persists on page reload
7. [ ] View logs → entries appear with correct filtering
8. [ ] Edit a localization string → reflected in main app
9. [ ] Non-admin user navigating to `/admin` → 401/403
