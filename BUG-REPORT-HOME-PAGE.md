# Bug Report — Home Page Enhancement

**Reviewed by:** Claude Code
**Date:** 2026-03-02
**Branch:** `001-secure-messaging`
**Scope:** Home page redesign

---

## Previously Reported (Now Resolved)

The following bugs were found in the initial review and have since been fixed:

- ~~Hardcoded `v1.0.0` in footer~~ — Now uses `v@(AppVersion.Current)`
- ~~Unused `@inject IHttpContextAccessor` in Home.razor~~ — Removed
- ~~Duplicate floating version div in App.razor~~ — Removed

---

## Bugs Fixed (2026-03-02)

### BUG-1: Redundant `@using` Directive ✅ FIXED

**File:** `src/ToledoMessage/Components/Pages/Home.razor` — Line 2

**Fix Applied:** Removed redundant `@using ToledoMessage.Shared.Constants` since it's already in `_Imports.razor`

---

### BUG-2: Dead CSS — `.app-version-floating` ✅ FIXED

**File:** `src/ToledoMessage/wwwroot/app.css` — Around line 344

**Fix Applied:** Removed orphaned `.app-version-floating` CSS block

---

### BUG-3: Incorrect Version Label Text ✅ FIXED

**File:** `src/ToledoMessage/Components/Pages/Home.razor` — Line 105

**Fix Applied:** Changed "API Version" to "App Version"

---

## Summary

| Bug | File | Status |
|-----|------|--------|
| BUG-1 | Home.razor | ✅ FIXED |
| BUG-2 | app.css | ✅ FIXED |
| BUG-3 | Home.razor | ✅ FIXED |

All bugs resolved.
