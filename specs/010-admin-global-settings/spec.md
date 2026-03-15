# Feature Specification: Admin Panel with Global Settings

**Feature Branch**: `010-admin-global-settings`
**Created**: 2026-03-14
**Status**: Draft
**Input**: User description: "I want to add a new project for the admin, it will contain a global setting for this solution, like digits of encryption, show log, and can filter logs, and also in future settings (global) like Select app color. Also can modify the resx file localization and make it open to add new settings and scale up."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage Global Application Settings (Priority: P1)

As an administrator, I want to view and modify global application settings (such as encryption key length, default theme color, and feature toggles) from a dedicated admin panel, so that I can control the behavior of the entire application without redeploying.

**Why this priority**: Global settings are the core purpose of the admin panel. Without them, the admin project has no value. These settings affect all users and control critical system behavior like encryption parameters.

**Independent Test**: Can be fully tested by navigating to the admin panel, changing a setting (e.g., encryption key length from 256 to 512), saving, and verifying the new value is reflected across the application.

**Acceptance Scenarios**:

1. **Given** an authenticated admin user, **When** they navigate to the admin panel, **Then** they see a list of all global settings organized by category with their current values.
2. **Given** an admin viewing the settings page, **When** they modify a setting value and click save, **Then** the change is persisted and takes effect for all users.
3. **Given** a non-admin user, **When** they attempt to access the admin panel, **Then** they are denied access with a clear "not authorized" message.
4. **Given** an admin modifying an encryption-related setting, **When** they enter an invalid value (e.g., negative key length), **Then** the system rejects the change with a validation error.

---

### User Story 2 - View and Filter Application Logs (Priority: P2)

As an administrator, I want to view application logs in the admin panel with the ability to filter by severity level, date range, and keyword search, so that I can diagnose issues and monitor system health without accessing the server directly.

**Why this priority**: Log visibility is essential for production operations. Admins need real-time insight into system behavior, errors, and security events to maintain the SaaS product.

**Independent Test**: Can be fully tested by generating some log entries (e.g., triggering a login failure), then opening the log viewer, applying filters (severity = "Warning", last 1 hour), and confirming the relevant entries appear.

**Acceptance Scenarios**:

1. **Given** an admin on the log viewer page, **When** the page loads, **Then** the most recent log entries are displayed in reverse chronological order (newest first).
2. **Given** log entries exist at multiple severity levels, **When** the admin filters by "Error" severity, **Then** only error-level entries are shown.
3. **Given** the admin enters a keyword in the search box, **When** they submit the search, **Then** only log entries containing that keyword are displayed.
4. **Given** the admin selects a date range, **When** they apply the filter, **Then** only entries within that date range are shown.
5. **Given** thousands of log entries exist, **When** the admin views logs, **Then** results are paginated to prevent performance degradation.

---

### User Story 3 - Manage Localization Strings (Priority: P3)

As an administrator, I want to view, edit, and add localization strings (resource file entries) from the admin panel, so that I can update translations or add new languages without requiring a code change or redeployment.

**Why this priority**: Localization management enables rapid iteration on translations and supports adding new languages. It removes the developer bottleneck for text changes, but is lower priority than core settings and logs which are needed for day-to-day operations.

**Independent Test**: Can be fully tested by opening the localization editor, selecting a language (e.g., Arabic), modifying a string value, saving, and confirming the updated string appears in the user-facing application.

**Acceptance Scenarios**:

1. **Given** an admin on the localization page, **When** they select a language, **Then** all localization keys and their translated values for that language are displayed.
2. **Given** a list of localization entries, **When** the admin edits a translation value and saves, **Then** the updated value is persisted and reflected in the application.
3. **Given** the admin wants to add a new localization key, **When** they fill in the key name and values for each language and save, **Then** the new key is added across all language files.
4. **Given** a localization key exists in English but not in Arabic, **When** the admin views the Arabic language, **Then** the missing key is highlighted as "untranslated."

---

### User Story 4 - Extensible Settings Architecture (Priority: P4)

As an administrator, I want the settings system to support adding new setting categories and individual settings without requiring structural changes, so that the admin panel can scale up as the application grows.

**Why this priority**: Future-proofing the architecture ensures new settings (like app color themes, rate limits, maintenance mode) can be added with minimal effort. This is an architectural concern that enables all future admin features.

**Independent Test**: Can be fully tested by defining a new setting category (e.g., "Notifications") with a new setting (e.g., "Max push retries") and confirming it appears in the admin panel without any structural code changes — only a new setting definition.

**Acceptance Scenarios**:

1. **Given** the existing settings system, **When** a new setting is defined with a name, category, type, default value, and validation rules, **Then** it automatically appears in the admin panel under its category.
2. **Given** settings are organized by category, **When** the admin views the settings page, **Then** settings are grouped by category with clear section headers.
3. **Given** a setting has a defined value type (boolean, number, text, selection), **When** the admin edits it, **Then** an appropriate input control is rendered (toggle, number input, text field, dropdown).

---

### Edge Cases

- What happens when an admin changes an encryption setting while messages are in transit? The setting change must only apply to new operations, not retroactively affect existing encrypted data.
- What happens when two admins edit the same setting simultaneously? The last save wins, but the second admin should see a warning if the value changed since they loaded it.
- What happens when a localization key is deleted while it's actively used in the UI? The system should fall back to the default language (English) value.
- What happens when the log volume is extremely high (millions of entries)? The log viewer must support server-side pagination and filtering — never load all logs into memory.
- What happens when the admin sets an invalid encryption digit value? Validation must prevent saving values outside the allowed range.
- What happens when the admin tries to navigate to any admin page before changing the default password? The system must redirect to the forced password-change screen until a new password is set.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a separate admin panel accessible only to users with admin role.
- **FR-002**: System MUST support a global settings store where each setting has a unique key, display name, category, value type (boolean, integer, string, selection), default value, validation rules, and current value.
- **FR-003**: Admins MUST be able to view all global settings organized by category.
- **FR-004**: Admins MUST be able to modify any global setting value, with validation enforced before saving.
- **FR-005**: System MUST provide a log viewer that displays application log entries with timestamp, severity level, message, and source.
- **FR-006**: Admins MUST be able to filter logs by severity level (Trace, Debug, Information, Warning, Error, Critical).
- **FR-007**: Admins MUST be able to filter logs by date range (from/to).
- **FR-008**: Admins MUST be able to search logs by keyword.
- **FR-009**: Log results MUST be paginated with a configurable page size (default: 50 entries per page).
- **FR-010**: System MUST provide a localization editor that lists all resource keys and their values per language.
- **FR-011**: Admins MUST be able to edit existing localization string values for any supported language.
- **FR-012**: Admins MUST be able to add new localization keys with values for all supported languages.
- **FR-013**: System MUST highlight localization keys that are missing translations for any supported language.
- **FR-014**: Setting changes MUST take effect immediately for new operations without requiring application restart.
- **FR-015**: System MUST provide predefined global settings including: encryption key length, log visibility toggle, default app theme/color, and feature toggles.
- **FR-016**: System MUST support adding new settings and new setting categories without structural code changes — only a new setting definition is needed.
- **FR-017**: Admin panel MUST support both English and Arabic languages (matching the main application's localization).
- **FR-018**: Admin panel MUST be responsive for both desktop and mobile browsers.
- **FR-019**: Admin credentials (username and default password) MUST be defined in server configuration.
- **FR-020**: On first login with the default password, the system MUST force the admin to change their password before granting access to any admin functionality.
- **FR-021**: The admin panel MUST have its own authentication flow, independent from the main application's user authentication.

### Key Entities

- **GlobalSetting**: Represents a single application-wide configuration value. Attributes: unique key, display name, description, category, value type (boolean/integer/string/selection), current value, default value, validation constraints (min, max, allowed values), sort order.
- **SettingCategory**: A logical grouping for related settings. Attributes: name, display name, description, icon, sort order.
- **LogEntry**: A structured log record stored in the database. Attributes: id, timestamp, severity level, message, source/logger name, exception details (if any), properties (structured key-value data).
- **LocalizationEntry**: A single translatable string. Attributes: resource key, language code, translated value, last modified timestamp.

## Clarifications

### Session 2026-03-14

- Q: Is the admin panel embedded in the existing app or a separate project/URL? → A: Separate Blazor WASM project hosted at a different URL (e.g., admin.chat.khamis.work) with its own authentication.
- Q: How does a user become an admin? → A: Admin usernames and default passwords are hardcoded in server configuration (appsettings). On first login, the admin is forced to change their password before accessing the panel.
- Q: How are logs stored for the admin log viewer? → A: Database log sink — Serilog writes to a `LogEntries` DB table; admin queries the table directly for filtering, search, and pagination.

## Assumptions

- Admin credentials (username + default password) are defined in server configuration (appsettings). No admin user table or registration flow exists — authentication is config-driven.
- On first login with a default password, the admin is forced to change their password before accessing any admin functionality. The changed password is persisted (in config or a lightweight admin credentials store).
- Only one admin role exists (no granular admin permissions like "settings admin" vs "log viewer admin").
- Log entries are written to a `LogEntries` database table via a Serilog SQL sink (in addition to existing file logging). The admin log viewer queries this table for filtering, search, and pagination.
- Localization entries are stored in the existing `.resx` resource files. The admin editor reads and writes these files. Runtime changes to `.resx` require the application to reload resource bundles (acceptable latency).
- The admin panel is a new Blazor WASM project (`ToledoVault.Admin`) hosted at a separate URL (e.g., `admin.chat.khamis.work`) with its own layout, authentication, and deployment. It shares the existing `ToledoVault.Shared` project for DTOs and constants.
- Global settings are stored in the database (a new `GlobalSettings` table), not in configuration files, so they can be changed at runtime.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admins can view and modify any global setting in under 30 seconds (navigate to setting, change value, save, confirm).
- **SC-002**: Log viewer returns filtered results within 2 seconds for up to 100,000 log entries.
- **SC-003**: Admins can update a localization string and see the change reflected in the user-facing application within 60 seconds (without redeployment).
- **SC-004**: Adding a new global setting requires only defining the setting metadata (key, type, default, validation) — no UI or structural code changes needed.
- **SC-005**: The admin panel correctly restricts access — 100% of unauthenticated or non-admin access attempts are blocked.
- **SC-006**: All admin panel pages are fully functional on both desktop (1920x1080) and mobile (375px width) viewports.
