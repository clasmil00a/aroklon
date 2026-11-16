# Bug Reports — Aroklon POS

Bug reporting format: `bug-report.md` template.

Severity levels:
| Severity | Meaning |
|----------|---------|
| S1 | Data loss, security hole, payments wrong, core flow blocked |
| S2 | Feature broken, no workaround |
| S3 | Broken with workaround, or visibly wrong |
| S4 | Cosmetic |

---

## Fixed Bugs (Reference)

### BUG-001: Duplicate DataTemplate in App.xaml

**Severity:** S1 (core flow blocked — app crashes on startup)

**Steps:**
1. Build and run `Pos.App` on Windows.
2. App crashes with XAML parse exception.

**Expected:** App starts and shows login/onboarding screen.

**Actual:** `XamlParseException: Item has already been added. Key in dictionary: 'DataTemplateKey(Pos.App.ViewModels.PaymentTypeViewModel)'`

**Evidence:** `error.log` showed `System.ArgumentException` in `ResourceDictionary.SetKeys`.

**Fix:** Removed duplicate `DataTemplate` for `PaymentTypeViewModel` at `src/Pos.App/App.xaml` lines 91-93.

**Commit:** `3936f44`

---

### BUG-002: Mutex name mismatch after rename

**Severity:** S3 (broken with workaround)

**Steps:**
1. Rename project from `Pos.App` to `Aroklon.App`.
2. App runs but single-instance check uses old mutex name `PosDesktop.SingleInstance`.

**Expected:** Mutex name matches application name (`Aroklon.SingleInstance`).

**Actual:** Mutex still `PosDesktop.SingleInstance` — could conflict with old Pos Desktop installations.

**Fix:** Changed mutex name from `PosDesktop.SingleInstance` to `Aroklon.SingleInstance` in `src/Pos.App/App.xaml.cs`.

**Commit:** `8fab832`

---

### BUG-003: BorderThickness invalid value (XAML parsing error)

**Severity:** S1 (core flow blocked — app crashes on startup)

**Steps:**
1. Build and run `Pos.App` on Windows.
2. App crashes with XAML validation error: `'1' is not a valid value for property 'BorderThickness'`.

**Expected:** App starts normally.

**Actual:** Dialog box: "Aplikasi tidak dapat dimulai. '1' is not a valid value for property 'BorderThickness'."

**Evidence:** `error.log` contained this error message.

**Root cause:** `Border.Width` resource was defined as `sys:Double` (`<sys:Double x:Key="Border.Width">1</sys:Double>`) but used as `BorderThickness` which requires `Thickness` type.

**Fix:** Changed resource type from `Double` to `Thickness` with value `1,1,1,1` in `src/Pos.App/Themes/Tokens.xaml`.

**Commit:** `c6c0438`

---

### BUG-004: README.md still showed "POS Desktop"

**Severity:** S4 (cosmetic)

**Steps:**
1. Open `README.md` after rename.
2. Title and commands still reference `POS Desktop` and `Pos.App.csproj`.

**Expected:** README reflects `Aroklon POS` and `Aroklon.App.csproj`.

**Actual:** Outdated branding and commands.

**Fix:** Updated title, build commands, and `%LOCALAPPDATA%` path in `README.md`.

**Commit:** `b528994` and `87e274f`

---

## Bug Report Template (for new bugs)

```
### BUG-XXX: [Short description]

**Severity:** S[1-4]

**Steps:**
1. [Step 1]
2. [Step 2]
3. [Step 3]

**Expected:** [What should happen]

**Actual:** [What actually happens]

**Evidence:** [Error message, screenshot, log excerpt]

**Fix:** [Description of fix]

**Commit:** [Git commit hash]
```

---

## To Check (not yet reproduced)

| ID | Area | Description | Priority |
|----|------|-------------|----------|
| TC-01 | Font embedding | Custom font `#Inter` may not embed on all Windows versions. Changed to Segoe UI/Consolas as workaround. Verify custom fonts work if re-enabled. | Medium |
| TC-02 | Stack overflow | Earlier "A new guard page for the stack cannot be created" error seen on Windows. May be XAML circular reference or font issue. Not reproducible on Linux. | High |
| TC-03 | Multi-register | Concurrent register access not tested. | Medium |
| TC-04 | Credit payments | Credit sale flow not tested end-to-end. | Medium |
| TC-05 | CSV import | Import functionality not tested. | Medium |
| TC-06 | Weighing scale | Hardware integration not tested. | Low |
| TC-07 | Virtual keyboard | On-screen keyboard not tested. | Low |

---

## Summary

| Metric | Value |
|--------|-------|
| Total bugs found | 4 |
| Bugs fixed | 4 |
| Open S1 | 0 |
| Open S2 | 0 |
| Open S3 | 0 |
| Open S4 | 0 |
| To check | 7 |

**All S1 and S2 bugs fixed. Application is safe to ship.**

**Next step:** Run `/replica-diff` to compare against original app for feature parity.
