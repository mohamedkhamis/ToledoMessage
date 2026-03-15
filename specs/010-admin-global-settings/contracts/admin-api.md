# Admin API Contracts

**Branch**: `010-admin-global-settings` | **Date**: 2026-03-14

All admin endpoints are prefixed with `/api/admin`. All require `[Authorize(Roles = "admin")]` except login.

## Authentication

### POST /api/admin/auth/login

**Request**:
```json
{
  "username": "string (required)",
  "password": "string (required)"
}
```

**Response 200** (success):
```json
{
  "token": "string (JWT)",
  "mustChangePassword": true
}
```

**Response 401**: Invalid credentials

**Notes**: If `mustChangePassword` is true, client must call change-password before any other API call. JWT includes `role: admin` claim. Token TTL: 60 minutes (admins have longer sessions).

### POST /api/admin/auth/change-password

**Request** (requires auth):
```json
{
  "currentPassword": "string (required)",
  "newPassword": "string (required, min 12 chars)"
}
```

**Response 204**: Password changed successfully
**Response 400**: Validation error (password too short)
**Response 401**: Current password incorrect

## Global Settings

### GET /api/admin/settings

Returns all settings grouped by category.

**Response 200**:
```json
[
  {
    "category": "Security",
    "settings": [
      {
        "id": "string",
        "key": "security.encryptionKeyLength",
        "displayName": "Encryption Key Length",
        "description": "AES key size in bits",
        "valueType": "selection",
        "currentValue": "256",
        "defaultValue": "256",
        "validationRules": { "options": ["128", "192", "256"] },
        "lastModifiedAt": "2026-03-14T10:00:00Z"
      }
    ]
  }
]
```

### PUT /api/admin/settings/{key}

Update a single setting value.

**Request**:
```json
{
  "value": "string (required)"
}
```

**Response 204**: Setting updated
**Response 400**: Validation failed (value outside allowed range/options)
**Response 404**: Setting key not found

### POST /api/admin/settings/reset/{key}

Reset a setting to its default value.

**Response 204**: Setting reset to default
**Response 404**: Setting key not found

## Logs

### GET /api/admin/logs

Query log entries with filtering and pagination.

**Query Parameters**:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| level | string? | null | Filter by severity (e.g., "Error") |
| from | DateTimeOffset? | null | Start of date range |
| to | DateTimeOffset? | null | End of date range |
| search | string? | null | Keyword search in message |
| page | int | 1 | Page number (1-based) |
| pageSize | int | 50 | Items per page (max 200) |

**Response 200**:
```json
{
  "items": [
    {
      "id": 12345,
      "timestamp": "2026-03-14T10:30:00Z",
      "level": "Error",
      "message": "Login failed for user 'john'",
      "source": "ToledoVault.Controllers.AuthController",
      "exception": "null or exception string"
    }
  ],
  "totalCount": 5432,
  "page": 1,
  "pageSize": 50,
  "totalPages": 109
}
```

### DELETE /api/admin/logs

Delete logs older than a specified date.

**Query Parameters**:
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| olderThan | DateTimeOffset | Yes | Delete entries before this date |

**Response 200**:
```json
{
  "deletedCount": 1500
}
```

## Localization

### GET /api/admin/localization

List all localization entries (merged: .resx baseline + DB overrides).

**Query Parameters**:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| language | string? | null | Filter by language code (e.g., "ar") |
| search | string? | null | Search in key or value |
| missingOnly | bool | false | Show only keys missing translations |

**Response 200**:
```json
{
  "entries": [
    {
      "resourceKey": "Login.Title",
      "values": {
        "en": { "value": "Sign In", "source": "resx" },
        "ar": { "value": "تسجيل الدخول", "source": "override" }
      },
      "isNewKey": false,
      "lastModifiedAt": "2026-03-14T10:00:00Z"
    }
  ],
  "totalKeys": 165,
  "languages": ["en", "ar"]
}
```

### PUT /api/admin/localization/{resourceKey}

Update or create a localization entry.

**Request**:
```json
{
  "languageCode": "string (required)",
  "value": "string (required)"
}
```

**Response 204**: Entry saved
**Response 400**: Validation error

### POST /api/admin/localization

Add a new localization key with values for all languages.

**Request**:
```json
{
  "resourceKey": "string (required)",
  "values": {
    "en": "English text",
    "ar": "Arabic text"
  }
}
```

**Response 201**: Key created
**Response 409**: Key already exists

### DELETE /api/admin/localization/{resourceKey}/{languageCode}

Revert an override back to .resx baseline (or delete a new key).

**Response 204**: Override removed
**Response 404**: No override exists for this key/language
