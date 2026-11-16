# Test Plan — Aroklon POS

Based on `replica/recon.md`. All flows tested with happy path, edge cases, and negative cases.

Numbering format: `FXX-H#` (happy), `FXX-E#` (edge), `FXX-N#` (negative).

---

## F01 — Onboarding (First-run setup)

**Happy path:**
- F01-H1: Enter shop name, admin full name, username, password, confirm password → click "Setup" → database created, admin account created, redirected to login screen.

**Edge cases:**
- F01-E1: Shop name with emoji (🏪 Toko 🍰) and accents (Café, Toko Bahan Kue) → accepted.
- F01-E2: Very long shop name (200 chars) → accepted or truncated gracefully.
- F01-E3: Password mismatch between password and confirm → error shown, setup blocked.
- F01-E4: Empty shop name → validation error, setup blocked.
- F01-E5: Short password (< 8 chars) → validation error.

**Negative cases:**
- F01-N1: Username already exists → error "Username sudah terdaftar", setup blocked.
- F01-N2: Database file path not writable → error shown.

---

## F02 — Login (Authentication)

**Happy path:**
- F02-H1: Enter valid username and password → login successful, redirected to home shell.
- F02-H2: Remember session → app starts directly at home on next launch (no login prompt).

**Edge cases:**
- F02-E1: Username with accents/emoji → accepted.
- F02-E2: Caps Lock on → warning shown.
- F02-E3: Empty password field → validation error.

**Negative cases:**
- F02-N1: Wrong password → error "Username atau kata sandi salah".
- F02-N2: 5 consecutive failed logins → account locked for 15 minutes.
- F02-N3: Expired session → redirected to login screen.

---

## F03 — Cash Sale (POS Core)

**Happy path:**
- F03-H1: Search product by name/barcode → add to cart → adjust quantity → select payment type → complete sale → stock deducted, receipt generated.
- F03-H2: Multi-unit selling (e.g., "dus" vs "pcs") → conversion factor applied correctly.
- F03-H3: Price list override (explicit price per product_unit) → override price used.
- F03-H4: Customer assigned → default price list applied.
- F03-H5: Tax applied per unit → receipt shows tax breakdown.
- F03-H6: Cart discount applied → total adjusted.
- F03-H7: Stock check prevents negative inventory → sale blocked if insufficient stock.

**Edge cases:**
- F03-E1: Barcode scan with special characters → handled.
- F03-E2: Very large quantity (9999) → accepted or limited.
- F03-E3: Product price override during sale → override applied for that line only.
- F03-E4: Partial payment → remaining balance shown.
- F03-E5: Credit payment enabled → sale recorded as credit, customer owes amount.
- F03-E6: Weighing scale input → weight-based pricing applied.
- F03-E7: Age-restricted product → age verification prompt.
- F03-E8: Keyboard shortcuts (F3 search, Enter add, Esc cancel) → all work.
- F03-E9: Split cart item → two lines with different quantities.
- F03-E10: Merge cart items → quantities combined.
- F03-E11: Lock order → order held, cannot be modified by another register.
- F03-E12: Virtual keyboard opened → text input works.
- F03-E13: Multi-register → sale recorded with register ID.

**Negative cases:**
- F03-N1: Insufficient stock → error "Stok tidak mencukupi", sale blocked.
- F03-N2: Product not found by barcode → error "Produk tidak ditemukan".
- F03-N3: Payment amount less than total → error, checkout blocked.
- F03-N4: Credit payment disabled → credit option hidden/disabled.

---

## F04 — Touch Sale (Visual Layout)

**Happy path:**
- F04-H1: Touch grid layout → tap product → added to cart → checkout.
- F04-H2: Category navigation → products filtered by category.

**Edge cases:**
- F04-E1: Mobile width (touch targets) → buttons appropriately sized.
- F04-E2: Screen reader labels → each button has accessible name.

**Negative cases:**
- F04-N1: Empty category → "Produk tidak ada" message shown.

---

## F05 — Advanced Product Search (F3)

**Happy path:**
- F05-H1: Press F3 → modal opens → search by name or code → keyboard navigation (arrow keys) → Enter selects product → added to cart.
- F05-H2: Search with accents (e.g., "Café" matches "cafe") → results shown.

**Edge cases:**
- F05-E1: Empty search → all products shown.
- F05-E2: Very long search string (100 chars) → handled.
- F05-E3: No matching products → "Tidak ditemukan" message.
- F05-E4: Keyboard navigation wraps → last to first, first to last.

**Negative cases:**
- F05-N1: Escape key → modal closes without selection.

---

## F06 — Sales History

**Happy path:**
- F06-H1: Search by date range, customer, or transaction number → transactions listed → select transaction → view details → reprint receipt.
- F06-H2: Export to CSV/TXT → file generated.

**Edge cases:**
- F06-E1: Empty date range → all transactions shown.
- F06-E2: Future date → no results, not error.
- F06-E3: Large result set (1000+ transactions) → paginated.

**Negative cases:**
- F06-N1: Invalid date format → validation error.
- F06-N2: Transaction not found → "Transaksi tidak ditemukan".

---

## F07 — Open Orders

**Happy path:**
- F07-H1: List held orders → select order → resume → continue editing → complete sale.
- F07-H2: Order locked by another register → "Dikunci oleh register lain" shown.

**Edge cases:**
- F07-E1: No open orders → empty state message.
- F07-E2: Order with deleted products → warning shown.

**Negative cases:**
- F07-N1: Try to resume locked order → error, blocked.

---

## F08 — Receipt View

**Happy path:**
- F08-H1: View receipt → print → saved as PDF/TXT.
- F08-H2: Company logo shown on receipt (if configured).

**Edge cases:**
- F08-E1: Logo path invalid → receipt prints without logo.
- F08-E2: Long receipt (many items) → paginated correctly.

**Negative cases:**
- F08-N1: Printer not configured → error, but receipt still viewable.

---

## F09 — Unit Master

**Happy path:**
- F09-H1: Create unit (e.g., "dus", "kg") → set conversion factor → save.
- F09-H2: Edit unit → changes reflected.
- F09-H3: Toggle unit active/inactive → inactive units hidden from selection.

**Edge cases:**
- F09-E1: Factor to base = 0 → validation error.
- F09-E2: Unit code with special chars → accepted.

**Negative cases:**
- F09-N1: Delete unit used by products → blocked, warning shown.
- F09-N2: Duplicate unit code → error.

---

## F10 — Product List

**Happy path:**
- F10-H1: List all products → select product → edit units.
- F10-H2: Filter by group → products filtered.

**Edge cases:**
- F10-E1: No products → empty state.
- F10-E2: Product with image → image shown.
- F10-E3: Product with color → color chip shown.
- F10-E4: Age-restricted product → age icon shown.

**Negative cases:**
- F10-N1: Product group deleted → products in group shown with warning.

---

## F11 — Price Lists

**Happy path:**
- F11-H1: Create price list (retail, wholesale) → set active/inactive.
- F11-H2: Add explicit price override for product → override shown with "!" indicator.
- F11-H3: No override → inherited price shown with "!".

**Edge cases:**
- F11-E1: Multiple price lists → each shows correct price.
- F11-E2: Price with many decimals → rounded to 2 decimals.

**Negative cases:**
- F11-N1: Duplicate price list name → error.
- F11-N2: Delete price list used by sales → blocked.

---

## F12 — Product Price

**Happy path:**
- F12-H1: View all price lists for one product → prices listed.

**Edge cases:**
- F12-E1: Product has no explicit prices → all show "inherited (!)".

**Negative cases:**
- F12-N1: Product not found → error.

---

## F13 — Customer Assignment

**Happy path:**
- F13-H1: Select customer during sale → default price list applied.
- F13-H2: Create new customer → saved, available for selection.

**Edge cases:**
- F13-E1: Customer with phone number → formatted.
- F13-E2: Customer with no default price list → standard price used.

**Negative cases:**
- F13-N1: Customer not found → error.
- F13-N2: Duplicate customer phone → warning.

---

## F14 — Cash Movement

**Happy path:**
- F14-H1: Record cash in → amount added to drawer.
- F14-H2: Record cash out → amount deducted from drawer.
- F14-H3: View cash movement history → listed with dates.

**Edge cases:**
- F14-E1: Large amount → accepted.
- F14-E2: Negative amount → validation error.

**Negative cases:**
- F14-N1: Cash out exceeds drawer balance → warning shown (not blocked).

---

## F15 — Refund

**Happy path:**
- F15-H1: Search transaction → select → refund → stock returned.
- F15-H2: Partial refund → only specified items refunded.

**Edge cases:**
- F15-E1: Refund with payment type → payment reversed.
- F15-E2: Refund already refunded transaction → blocked.

**Negative cases:**
- F15-N1: Transaction not found → error.
- F15-N2: Refund amount exceeds original → error.

---

## F16 — End of Day

**Happy path:**
- F16-H1: Count cash → count card → compare → print Z report → close register.
- F16-H2: X report without closing → summary shown, register stays open.

**Edge cases:**
- F16-E1: Cash count mismatch → variance shown.
- F16-E2: No sales today → zero report.

**Negative cases:**
- F16-N1: Register already closed → error, cannot reopen.
- F16-N2: Unsaved cash movement → warning before close.

---

## F17 — Product Groups

**Happy path:**
- F17-H1: Create product group → assign products → group shown in catalog.
- F17-H2: Toggle group active/inactive → products affected.

**Edge cases:**
- F17-E1: Group with no products → empty state.
- F17-E2: Group used by many products → all listed.

**Negative cases:**
- F17-N1: Delete group used by products → blocked, warning shown.

---

## F18 — Product Editor

**Happy path:**
- F18-H1: Edit product details (name, code, unit, price) → saved.
- F18-H2: Add product image → shown in product list.
- F18-H3: Set product color → color chip shown.
- F18-H4: Set age restriction → icon shown on product.
- F18-H5: Set per-unit barcode → barcode scanned correctly.
- F18-H6: Edit/delete/toggle variants → reflected.

**Edge cases:**
- F18-E1: Product code with special chars → accepted.
- F18-E2: Image too large → warning or resize.

**Negative cases:**
- F18-N1: Duplicate product code → error.
- F18-N2: Delete product used in sales → blocked.

---

## F19 — Stock Adjustment

**Happy path:**
- F19-H1: Adjust stock (in/out) → reason recorded → stock updated.
- F19-H2: View stock history → changes listed with dates and reasons.

**Edge cases:**
- F19-E1: Negative adjustment → stock reduced.
- F19-E2: Stock goes to zero → allowed.

**Negative cases:**
- F19-N1: Stock adjustment would make negative → blocked (if negative inventory prevented).
- F19-N2: Product not found → error.

---

## F20 — Reports

**Happy path:**
- F20-H1: Sales report (date range) → total sales, tax, payment breakdown.
- F20-H2: Stock report → current stock levels.
- F20-H3: Payment report → by payment type.
- F20-H4: Customer report → by customer.
- F20-H5: Tax report → tax collected.
- F20-H6: Export to CSV/TXT → file generated.
- F20-H7: X report (without closing) → summary shown.

**Edge cases:**
- F20-E1: Empty date range → all data.
- F20-E2: No data in range → zero report.
- F20-E3: Large dataset → paginated/export still works.

**Negative cases:**
- F20-N1: Invalid date → validation error.
- F20-N2: Export path not writable → error.

---

## F21 — Settings

**Happy path:**
- F21-H1: Change theme (dark/light) → applied immediately.
- F21-H2: Configure printer → test print.
- F21-H3: Toggle should-have features → enabled/disabled.
- F21-H4: Set cashier behavior → applied.

**Edge cases:**
- F21-E1: Invalid printer name → error shown.
- F21-E2: Theme file missing → fallback to default.

**Negative cases:**
- F21-N1: Setting not saved → error shown.

---

## F22 — User Management

**Happy path:**
- F22-H1: Create admin user → saved, can login.
- F22-H2: Toggle user active/inactive → inactive cannot login.
- F22-H3: Change permissions → reflected.

**Edge cases:**
- F22-E1: Username with accents → accepted.
- F22-E2: Password policy enforced → min length, etc.

**Negative cases:**
- F22-N1: Delete last admin → blocked.
- F22-N2: Duplicate username → error.

---

## F23 — Payment Types

**Happy path:**
- F23-H1: Create payment type (cash, card, QRIS) → available at checkout.
- F23-H2: Toggle active/inactive → hidden at checkout.

**Edge cases:**
- F23-E1: Payment type with icon → shown.

**Negative cases:**
- F23-N1: Delete payment type used in sales → blocked.

---

## F24 — Database Check

**Happy path:**
- F24-H1: Integrity check → "OK" or issues listed.
- F24-H2: Backup created → file in backups folder.
- F24-H3: Restore from backup → database restored.

**Edge cases:**
- F24-E1: Corrupted database → error, restore suggested.
- F24-E2: No backup files → "Tidak ada cadangan" message.

**Negative cases:**
- F24-N1: Backup path not writable → error.
- F24-N2: Invalid backup file → error, not restored.

---

## F25 — Home Navigation

**Happy path:**
- F25-H1: Click any menu item → navigated to that screen.
- F25-H2: Status items shown (database, backup, features) → accurate status.

**Edge cases:**
- F25-E1: Feature disabled (toggle off) → menu item hidden or shows "disabled".
- F25-E2: Full screen toggle → window maximized/restored.

**Negative cases:**
- F25-N1: Sign out → redirected to login.

---

## Edge Case Summary (applied to all flows where relevant)

| Case | Description |
|------|-------------|
| Empty input | All text fields validated |
| Very long input | 100+ chars, 200+ chars |
| Emoji and accents | 🏪, Café, Toko Bahan Kue |
| Two tabs at once | Rapid tab switching |
| Double click submit | Debounced, single action |
| Back button mid-flow | State preserved or warning |
| Refresh mid-flow | Session preserved |
| Slow network | Local app, no network dependency |
| Offline | SQLite local, fully functional |
| Expired session | Redirect to login |
| Second user's data | Isolated by session |
| Time zones/DST | Local time used |
| Mobile width | WPF desktop, not applicable |
| Keyboard only | All functions keyboard-accessible |
| Screen reader | Labels on buttons, ARIA-equivalent |

## Negative Case Summary

| Case | Description |
|------|-------------|
| Wrong password | Error, lockout after 5 |
| Card declined | N/A (local payments) |
| Permission denied | Admin-only features blocked |
| Deleted record | Referential integrity blocks |

---

## Automation Note

This is a **WPF desktop application**, not a web application. Playwright e2e tests are **not applicable**. Testing should be done manually using the test cases above, or with WPF UI automation tools (e.g., WinAppDriver, FlaUI).

## Test Results Template

| Flow | Happy Path | Edge Cases | Negative Cases | Status |
|------|-----------|------------|----------------|--------|
| F01 Onboarding | F01-H1 | F01-E1 to E5 | F01-N1 to N2 | Pending |
| F02 Login | F02-H1 to H2 | F02-E1 to E3 | F02-N1 to N3 | Pending |
| F03 Cash Sale | F03-H1 to H7 | F03-E1 to E13 | F03-N1 to N4 | Pending |
| ... | ... | ... | ... | ... |

**Total cases:** ~200+  
**Run on:** Windows 10/11 with .NET 10 Desktop Runtime  
**Build:** `dotnet build Pos.sln -c Release`
