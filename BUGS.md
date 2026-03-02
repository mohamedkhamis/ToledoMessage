# ToledoMessage — Bug Tracker

> **Purpose:** Central bug tracking file for all features/components.
> **Process:** See [Bug Workflow](#bug-workflow) at the bottom of this file.

---

## Open Bugs

(None)

---

## Resolved Bugs

### ~~BUG-001: app.css not using structural CSS variables from themes.css~~ FIXED 2026-03-02
Applied CSS variables to:
- `.message-bubble`: border-radius, font-size
- `.msg-textarea`: padding, border-radius
- `.chat-sidebar`: width
- `.chat-messages`: background-image

### ~~BUG-002: Settings.razor has hardcoded strings that should use ThemeLabelSet~~ FIXED 2026-03-02
Added missing ThemeLabelSet properties and updated Settings.razor.

### ~~BUG-HP-001: Hardcoded version `v1.0.0` in Home.razor~~ FIXED 2026-03-02
Changed to `v@(AppVersion.Current)`.

### ~~BUG-HP-002: Unused `@inject IHttpContextAccessor` in Home.razor~~ FIXED 2026-03-02
Removed unused injection.

### ~~BUG-HP-003: Duplicate floating version display~~ FIXED 2026-03-02
Removed `app-version-floating` div from App.razor.

### ~~BUG-HP-004: Redundant `@using` directive in Home.razor~~ FIXED 2026-03-02
Removed — already in `_Imports.razor`.

### ~~BUG-HP-005: Dead CSS `.app-version-floating`~~ FIXED 2026-03-02
Removed orphaned CSS from app.css.

### ~~BUG-HP-006: "API Version" label incorrect~~ FIXED 2026-03-02
Changed to "App Version".

---

## Bug Workflow

### How to Report a Bug

Every bug entry follows this template:

```markdown
### BUG-XXX: Short descriptive title (Severity)

**Date:** YYYY-MM-DD
**Feature:** Feature or component name
**Severity:** Critical / High / Medium / Low
**Category:** CSS / Theme Labels / Logic / UI / Performance / Security

**Description:**
One paragraph explaining what's wrong and why it matters.

**Affected Files & Lines:**

| File | Line | Current | Expected |
|------|------|---------|----------|
| `path/to/file` | 123 | What it says now | What it should say |

**Impact:** What the user sees or what breaks.

**Fix Steps:**
1. Step-by-step instructions to fix
2. Specific enough that any developer/AI can follow
3. Include code snippets if helpful
```

### Severity Levels

| Level | Definition | Examples |
|-------|-----------|----------|
| **Critical** | Feature broken or not working at all | CSS variables defined but not used, crash on load |
| **High** | Feature partially broken, bad UX | Wrong data displayed, layout broken on mobile |
| **Medium** | Feature works but inconsistent or incomplete | Some labels missed, minor style mismatch |
| **Low** | Cosmetic or cleanup issue | Dead code, redundant imports, typos |

### Bug Lifecycle

```
Open → In Progress → Fixed → Verified
                  → Won't Fix (with reason)
```

1. **Open:** Bug reported with full details in this file under `## Open Bugs`
2. **In Progress:** Developer/AI is actively fixing it
3. **Fixed:** Move to `## Resolved Bugs` with fix date and one-line summary
4. **Verified:** Confirmed working after deploy (can be removed from Resolved after 30 days)

### Rules

- **Bug IDs are permanent** — never reuse a BUG-XXX number
- **Numbering:** Use `BUG-001`, `BUG-002`, etc. for global bugs
- **Feature-specific prefix:** Use `BUG-HP-XXX` for Home Page, `BUG-TH-XXX` for Theme, etc. (optional)
- **One bug per entry** — don't combine multiple issues
- **Always include file paths and line numbers** — so the fix can be applied without searching
- **Always include "Fix Steps"** — specific enough for another developer or AI agent to execute
- **When fixed:** Move the entry from "Open" to "Resolved" with strikethrough title and fix date
- **Delete `BUG-REPORT-*.md` files** — all bugs go in this central `BUGS.md` file
