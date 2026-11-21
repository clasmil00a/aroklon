# Aroklon

Aroklon is an offline point-of-sale (POS) application for Windows, rebuilt
clean-room from a public feature study of a Windows POS product. It is original
software: its own name, interface, copy, icons and code. It is not affiliated
with, derived from, or endorsed by any other POS product.

Aroklon turns a normal Windows PC into a working till: sell by barcode, SKU or
name, take cash/card/split payments, print a receipt, and track stock. Everything
runs locally — no account, no cloud, no subscription.

## What it does

- **Till** — product tiles, barcode/SKU/name search, quantity, line and cart
  discounts, tax, customer attach, hold/resume, refunds, quick pay.
- **Payments** — configurable tenders, cash with automatic change, split
  payments, cash rounding to 100/500/1,000, numeric keypad and quick-cash
  buttons.
- **Day & cash** — open the register with a starting float, cash in/out with a
  reason, X report during the day, and an End of Day Z report with counted cash
  and the variance. Selling is blocked while the register is closed.
- **Promotions** — automatic happy-hour and cart/product/group discounts with
  day, time, date-range and minimum-sale rules, applied at the till.
- **Receipts** — company header/footer, logo-friendly layout, print to any
  Windows printer or save as PDF from the print dialog.
- **Products** — groups, barcodes, cost/price, tax rates, low-stock alerts,
  service items, age restrictions, images and colours; CSV import from a file
  or pasted spreadsheet rows.
- **Stock** — quantities on hand, average cost, stock movements, adjustments,
  purchases, returns, loss & damage, inventory counts.
- **Customers & suppliers** — contacts, loyalty cards, preset discounts,
  balances.
- **Documents** — purchase, stock return, inventory count, loss & damage,
  proforma; searchable document list.
- **Reports** — sales by day, product sales, payment mix, tax, customer sales,
  stock valuation and low stock; X/Z register reports; print or export CSV.
- **Users** — PIN sign-in with access levels (Owner / Manager / Cashier).
- **Themes** — Classic (Aronium-style top menu bar + Management tree, default)
  and Modern (sidebar) layout, each with **Dark** (default), **Light** or
  **System** colour mode.
- **Languages** — Bahasa Indonesia (default) and English, switchable at login or in Settings.
- **Receipts** — white paper with dark text in every colour mode.
- **Updates** — app-only patches (~1 MB) via Settings → Data → Check for updates, or `Update-Aroklon.bat`.
- **Data** — local SQLite file, backup/restore, reset.

### Keyboard shortcuts (till)

| Key | Action |
| --- | --- |
| F2 | Discount |
| F3 | Focus product search |
| F4 | Change quantity of selected line |
| F8 | Hold sale |
| F10 | Payment |
| F12 | Quick pay with default tender |
| Ctrl+M | Management (Dashboard) |
| Del | Remove selected line |
| Esc | Deselect / close dialog |

### Daily routine

1. **Open register** — enter the float on the End of Day screen.
2. **Sell** — the register status chip in the top bar stays green while open.
3. **Cash in/out** — Menu → Cash in / out; every entry needs a reason.
4. **X report** — Menu → X report prints the current takings without closing.
5. **End of Day** — Menu → End of Day, count the drawer, review the variance,
   then **Continue** to save the Z report and close the register.

### Promotions and CSV import

- **Promotions & Actions** (Management) creates automatic discounts for the
  entire sale, one product or a product group. Set a percentage or fixed amount,
  optional weekdays, a time window, a date range and a minimum sale total.
- **Products → Import CSV** accepts a `.csv` file or pasted rows. Columns:
  `name, code, barcode, group, unit, cost, price, stock, low_stock, tax, active,
  service`. `name` is required; existing products match on code, barcode or name.

## First run

The app seeds a small demo catalogue so you can try it immediately.

1. Sign in (Owner / `1234`).
2. On the **End of Day** screen, enter the opening cash (float) and choose
   **Open register**.
3. Go to **Sales** and start ringing. Cash in/out, X report and close register
   are in the top **Menu**.

| User | PIN |
| --- | --- |
| Owner | `1234` |
| Cashier | `1111` |

Change these in **Users**, and the store identity in **Settings → Company**.

## Run from source

Requires Node.js 18+.

```bash
npm install
npm start
```

Data is written to Electron's user-data folder (on Windows:
`%APPDATA%\Aroklon\aroklon.db`).

## Build the Windows executable

On a machine with Node.js (Windows, macOS or Linux):

```bash
npm install
npm run package
```

Output:

```
dist/Aroklon-win32-x64/Aroklon.exe      <- portable app, no installer
dist/Aroklon-win32-x64/                 <- keep the whole folder together
dist/Aroklon-win32-x64.zip              <- ready to distribute
```

The build script extracts the official Electron runtime, packs `src/` and
`sql.js` into `resources/app.asar`, renames the executable to `Aroklon.exe`,
adds the Aroklon icon and drops a short `Baca dulu.txt` next to it.

To also stamp the icon/version into the `.exe` file itself (needs more memory):

```bash
STAMP=1 npm run package
```

## Project layout

```
src/main/        Electron main process, SQLite data layer, printing
src/renderer/    UI (plain HTML/CSS/JS, no framework)
replica/         recon map and feature matrix (reference only, never shipped)
scripts/         Windows packaging helpers
build/           icon source + generated icon.ico/png
```

## Data & backup

All data lives in a single SQLite file. **Settings → Data** can back it up,
restore it, open the folder, or reset everything. Back up before moving the app
to another PC.

## Updates (small patches)

The full download includes the Electron runtime. App code is packed separately
as `resources/app.asar` (~1 MB). Each fix is published as a small patch:

- **In-app:** Settings → Data → **Check for updates**. It verifies the SHA-256,
  downloads `app-<version>.asar`, then restarts after swapping the file.
- **Manual fallback:** close Aroklon and run `Update-Aroklon.bat` in the app
  folder. It fetches the latest patch from GitHub Releases, checks the hash and
  replaces `resources/app.asar`.

The updater refuses a patch that needs a different Electron major version; in
that case download the new full base.

## Build outputs

```
dist/Aroklon-win32-x64/Aroklon.exe
dist/Aroklon-win32-x64.zip
dist/releases/app-1.0.5.asar
dist/releases/latest.json
```

## License

MIT — see `LICENSE`.