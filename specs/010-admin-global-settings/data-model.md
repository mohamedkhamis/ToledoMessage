# Data Model: Admin Panel with Global Settings

**Branch**: `010-admin-global-settings` | **Date**: 2026-03-14

## New Entities

### AdminCredential

Stores the admin's hashed password after first login password change.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | long | PK, ValueGeneratedNever | IdGenerator.GetNewId() |
| Username | string | Required, MaxLength(32), Unique | Admin username (matches config) |
| PasswordHash | string | Required | ASP.NET Identity PasswordHasher output |
| MustChangePassword | bool | Required, Default: true | Force password change on next login |
| CreatedAt | DateTimeOffset | Required | First password change timestamp |
| LastLoginAt | DateTimeOffset? | Nullable | Last successful login |

**Lifecycle**:
- Created on first successful login with config default password
- `MustChangePassword` set to `false` after admin changes password
- `PasswordHash` updated on password change

### GlobalSetting

Stores a single application-wide configuration value.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | long | PK, ValueGeneratedNever | IdGenerator.GetNewId() |
| Key | string | Required, MaxLength(128), Unique | Setting identifier (e.g., "encryption.keyLength") |
| DisplayName | string | Required, MaxLength(100) | Human-readable name |
| Description | string? | MaxLength(500) | Help text shown in admin UI |
| Category | string | Required, MaxLength(64) | Grouping key (e.g., "Security", "Appearance") |
| ValueType | string | Required, MaxLength(20) | "boolean", "integer", "string", "selection" |
| CurrentValue | string | Required, MaxLength(1000) | JSON-serialized current value |
| DefaultValue | string | Required, MaxLength(1000) | JSON-serialized default value |
| ValidationRules | string? | MaxLength(1000) | JSON: { "min": 128, "max": 4096, "options": [...] } |
| SortOrder | int | Required, Default: 0 | Display order within category |
| LastModifiedAt | DateTimeOffset | Required | Last update timestamp |

**Lifecycle**:
- Seeded via EF Core migration with predefined settings
- Updated by admin via API — only `CurrentValue` and `LastModifiedAt` change
- Never deleted — settings are permanent

**Indexes**:
- Unique on `Key`
- Index on `Category` for grouped queries

### LogEntry

Structured log record written by Serilog SQL sink.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | int | PK, Identity | Auto-increment (Serilog manages) |
| Message | string? | | Rendered log message |
| MessageTemplate | string? | | Serilog message template |
| Level | string | MaxLength(16) | Severity: Verbose, Debug, Information, Warning, Error, Fatal |
| TimeStamp | DateTimeOffset | Required | When the log was written |
| Exception | string? | | Exception details if any |
| Properties | string? | | XML/JSON structured properties |

**Note**: This table is auto-created by `Serilog.Sinks.MSSqlServer`. Schema follows the sink's default. Admin queries against this table are read-only.

**Indexes**:
- Clustered on `Id`
- Index on `TimeStamp DESC` for reverse-chronological queries
- Index on `Level` for severity filtering
- Composite index on `(Level, TimeStamp DESC)` for filtered + sorted queries

### LocalizationOverride

Runtime overrides for .resx localization strings.

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| Id | long | PK, ValueGeneratedNever | IdGenerator.GetNewId() |
| ResourceKey | string | Required, MaxLength(256) | Matches .resx `name` attribute |
| LanguageCode | string | Required, MaxLength(10) | "en", "ar", etc. |
| Value | string | Required, MaxLength(4000) | Translated text |
| IsNewKey | bool | Required, Default: false | True if key doesn't exist in .resx baseline |
| LastModifiedAt | DateTimeOffset | Required | Last update timestamp |

**Constraints**:
- Unique composite on `(ResourceKey, LanguageCode)`

**Lifecycle**:
- Created when admin overrides an existing .resx value or adds a new key
- Updated when admin modifies the override value
- Deleted when admin reverts to .resx baseline value (for overrides, not new keys)

## Relationships

- `AdminCredential` — standalone, no FK to Users table (independent auth)
- `GlobalSetting` — standalone, no FK relationships
- `LogEntry` — standalone, managed by Serilog sink (read-only from admin perspective)
- `LocalizationOverride` — standalone, no FK relationships

## Predefined Global Settings (Seed Data)

| Key | Category | ValueType | Default | Validation |
|-----|----------|-----------|---------|------------|
| `security.encryptionKeyLength` | Security | selection | "256" | options: ["128", "192", "256"] |
| `security.pbkdf2Iterations` | Security | integer | "600000" | min: 100000, max: 1000000 |
| `appearance.defaultTheme` | Appearance | selection | "default" | options: ["default", "default-dark", "whatsapp", "whatsapp-dark", "telegram", "telegram-dark", "signal", "signal-dark"] |
| `appearance.defaultFontSize` | Appearance | selection | "medium" | options: ["small", "medium", "large"] |
| `features.readReceipts` | Features | boolean | "true" | |
| `features.typingIndicators` | Features | boolean | "true" | |
| `features.linkPreviews` | Features | boolean | "true" | |
| `features.voiceMessages` | Features | boolean | "true" | |
| `logging.minLevel` | Logging | selection | "Information" | options: ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"] |
| `logging.retentionDays` | Logging | integer | "30" | min: 1, max: 365 |
