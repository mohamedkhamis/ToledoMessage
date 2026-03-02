# Bug Report — Home Page Enhancement

**Reviewed by:** Claude Code
**Date:** 2026-03-02
**Branch:** `001-secure-messaging`
**Scope:** Minimax home page redesign (3 files changed)

---

## BUG-1: Hardcoded App Version (Critical)

**File:** `src/ToledoMessage/Components/Pages/Home.razor` — Line 106
**Severity:** Critical
**Category:** Incorrect Data Display

**Description:**
The footer version is hardcoded as `v1.0.0` instead of using the dynamic `AppVersion.Current` constant. The app auto-increments its version from the git commit count, so this hardcoded value will always be wrong.

**Current code:**
```razor
<span class="version-value">v1.0.0</span>
```

**Expected code:**
```razor
<span class="version-value">v@(AppVersion.Current)</span>
```

**Impact:** Users see an incorrect version number on the landing page.

---

## BUG-2: Unused Dependency Injection (Minor)

**File:** `src/ToledoMessage/Components/Pages/Home.razor` — Line 2
**Severity:** Minor
**Category:** Dead Code

**Description:**
`IHttpContextAccessor` is injected via `@inject` but is never referenced anywhere in the component. This adds an unnecessary dependency.

**Current code:**
```razor
@inject IHttpContextAccessor HttpContextAccessor
```

**Fix:**
Remove the entire line. The component does not need `IHttpContextAccessor`.

---

## BUG-3: Duplicate Version Display on Landing Page (Medium)

**File:** `src/ToledoMessage/Components/App.razor` — Line 61
**Severity:** Medium
**Category:** UI Overlap / Redundancy

**Description:**
A floating version label was previously added to `App.razor` (server-rendered, visible on ALL pages). Now that the Home page has its own version in the footer, the landing page shows **two** version displays simultaneously — the floating one in the bottom-left corner and the footer one.

**Current code in App.razor (line 61):**
```razor
<div class="app-version-floating">v@(AppVersion.Current)</div>
```

**Fix:**
Remove line 61 from `App.razor` entirely. The Home page footer now handles the version display for the landing page, and other pages show the version in the sidebar.

Also remove the associated CSS for `.app-version-floating` from `src/ToledoMessage/wwwroot/app.css`.

---

## Summary

| Bug | File | Severity | Effort |
|-----|------|----------|--------|
| BUG-1 | Home.razor:106 | Critical | 1 min |
| BUG-2 | Home.razor:2 | Minor | 1 min |
| BUG-3 | App.razor:61 + app.css | Medium | 2 min |

**Instructions for Minimax:** Please fix all three bugs in order (BUG-1 through BUG-3), then verify the landing page renders correctly with the dynamic version number and no duplicate version labels.
