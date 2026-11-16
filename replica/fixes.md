# Fix Plan — Aroklon POS vs Aronium POS

Source: `replica/reviews.csv` (9 reviews from 3 sources: reddit, softwaresuggest, sourceforge)
Generated: `replica/feedback.md`

---

## List 1 — What They Hate

Complaints about things the original app does.

| # | Problem | Reviews | Sources | Evidence |
|---|---------|---------|---------|----------|
| 1 | App is slow/laggy, especially with SQL server backend; can't handle multiple locations | 2 | 1 | "Slow and irresponsive." (reddit, [Aronium POS Bundles](https://www.reddit.com/r/POS/comments/1n4ohd3/aronium_pos_bundles/))<br>"The current system is laggy (SQL server) and can't handle multiple locations or separate…" (reddit, [Help to find good pos](https://www.reddit.com/r/POS/comments/1opzpbo/help_to_find_good_pos/)) |
| 2 | Paywalls / paid features lock useful functionality behind "Pro" version; remote stock updates not possible in free version | 1 | 1 | "Im currently using the free version which is not possible." (reddit, [Aronium Pro](https://www.reddit.com/r/POS/comments/1qkf3vz/aronium_pro/)) |

**Theme quality:** Both themes are "thin" (< 3 reviews or single source). Collect more reviews before ranking definitively.

---

## List 2 — What Is Missing

Features people ask for by name, verbatim from reviews.

| # | Missing Feature | Reviews | Sources | Evidence |
|---|-----------------|---------|---------|----------|
| 1 | Better stock control / product management detail | 1 | 1 | "Stock control and product management need more details." (softwaresuggest, 4★, [Aronium reviews](https://www.softwaresuggest.com/aronium)) |
| 2 | Dispatch/delivery option | 1 | 1 | "It needs a dispatch/delivery option, and customers and suppliers need to be separated." (softwaresuggest, 4★) |
| 3 | Separate customer and supplier records | 1 | 1 | "customers and suppliers need to be separated." (softwaresuggest, 4★) |
| 4 | HR section (employee records, salaries, hours, payslips) | 1 | 1 | "Additionally, it needs an HR section to manage employee records, salaries, hours worked, and payslips." (softwaresuggest, 4★) |
| 5 | Remote stock/inventory synchronization | 1 | 1 | "I want to be able to connect to remotely to update stocks/inventory." (reddit, [Aronium Pro](https://www.reddit.com/r/POS/comments/1qkf3vz/aronium_pro/)) |
| 6 | Android/mobile app | 1 | 1 | Cons list mentions "No Mobile Android" (softwaresuggest review summary) |
| 7 | Simple "just open and sell" workflow (no forced product entry first) | 1 | 1 | "I want to be able to just open the software and start ringing up transactions." (reddit, [Need help with aronium](https://www.reddit.com/r/POS/comments/1msqjcs/need_help_with_aronium/)) |
| 8 | Kitchen ticket printing control (don't print on table save) | 1 | 1 | "I had one issue with it printing kitchen tickets whenever I saved a table." (reddit, [offline POS for restaurant](https://www.reddit.com/r/POS/comments/1p2e3gj/i_need_an_free_offline_pos_system_for_a_small/)) |

**Review summary cons from Softwaresuggest:** Missing HR Functionality, Limited Stock Control, No Mobile Android.

---

## List 3 — What Is Unsolved

Whole jobs or groups the original ignores — positioning opportunities.

| # | Unsolved Job / Group | Reviews | Sources | Evidence |
|---|---------------------|---------|---------|----------|
| 1 | Small retail shops that need simple, fast, offline-first POS without SQL server complexity | 2 | 1 | "Slow and irresponsive." + "can't handle multiple locations" (reddit) |
| 2 | Small restaurants/kitchen operations needing controlled ticket printing | 1 | 1 | "printing kitchen tickets whenever I saved a table" (reddit) |
| 3 | Businesses needing HR/payroll features alongside POS | 1 | 1 | "needs an HR section to manage employee records, salaries, hours worked, and payslips" (softwaresuggest) |

---

## Fix Plan — Top 8 by Evidence × Cheapness

| # | What to Build / Change | Size | Skill | Evidence |
|---|----------------------|------|-------|----------|
| 1 | **Local-first SQLite (no SQL server)** — single-file database, no server setup, fast on modest hardware | S | replica-backend | "laggy (SQL server)" + "Slow and irresponsive" (2 reddit reviews) |
| 2 | **Remote stock/inventory sync toggle** — optional cloud sync for multi-location, off by default | M | replica-backend | "connect to remotely to update stocks/inventory" (reddit) |
| 3 | **Separate customer and supplier records** — distinct record types with separate views | S | replica-backend | "customers and suppliers need to be separated" (softwaresuggest) |
| 4 | **Dispatch/delivery option** — delivery mode toggle in sale workflow | S | replica-backend | "It needs a dispatch/delivery option" (softwaresuggest) |
| 5 | **Improved stock control** — detailed stock movements, low-stock alerts, per-unit stock | M | replica-backend | "Stock control and product management need more details" (softwaresuggest) |
| 6 | **"Quick sell" mode** — open app and start ringing up without forced product pre-entry | S | replica-build | "just open the software and start ringing up transactions" (reddit) |
| 7 | **Kitchen ticket printing control** — option to print tickets only when explicitly triggered, not on table save | S | replica-build | "printing kitchen tickets whenever I saved a table" (reddit) |
| 8 | **Android/mobile companion** — or at least responsive design for tablet use | L | replica-build | "No Mobile Android" (softwaresuggest cons) |

Features marked `original: no` will be added to `features.csv`.

---

## Positioning Angle

**Option A — "Simple, fast, local-first POS for small shops"**

> For small retail shop owners who hate that their POS is slow, laggy, and locked behind paywalls, Aroklon POS is a free, local-first Windows POS that opens and sells immediately — no SQL server, no Pro tier, no forced product entry.

Evidence: "Slow and irresponsive" + "laggy (SQL server)" + "free version which is not possible" (3 reddit reviews)

**Option B — "POS with HR and stock control for growing small businesses"**

> For small businesses that need POS plus HR, payroll, and detailed stock control in one app, Aroklon POS combines sales, inventory, and employee management — no separate systems needed.

Evidence: "needs an HR section" + "Stock control need more details" (softwaresuggest)

**Recommendation: Option A.** The strongest, most consistent complaint across sources is performance and paywalls. "Simple, fast, local-first" directly addresses both. It's also cheaper to build (no cloud sync, no HR module) and differentiates clearly from Aronium's SQL-server architecture.

---

## New Features for `features.csv`

The following rows have `original` set to `no` (not in the original app) and should be added to `replica/features.csv`:

| Feature | Area | Priority | Original | Clone | Notes |
|---------|------|----------|----------|-------|-------|
| Local-first SQLite (no server) | Database | must | no | yes | Single-file DB, no SQL server lag |
| Remote stock sync toggle | Inventory | should | no | toggle | Optional cloud sync for multi-location |
| Separate customer/supplier records | Catalog | should | no | yes | Distinct record types |
| Dispatch/delivery option | Sales | should | no | toggle | Delivery mode in sale workflow |
| Quick sell mode | Sales | should | no | toggle | Open and sell without pre-entry |
| Kitchen ticket printing control | Reports | should | no | toggle | Print tickets only when triggered |
| Android/mobile companion | Platforms | could | no | toggle | Tablet/mobile app |

**Sample size:** 9 reviews from 3 sources. Under 30 reviews — supports a direction, not a definitive ranking. Collect more reviews before betting the roadmap on it.
