# Recon map: Aronium POS (Windows desktop)

Scope: the free, single-register retail POS core loop — ring a sale, take payment,
print a receipt, manage products/stock/customers, and close the day with a Z report.
For: a small shop owner who wants an offline Windows till under their own brand (Aroklon).
Date: 2026-11-19

> Clean-room note: this map records what the product *does* and how a user moves
> through it. No Aronium code, assets, copy, or private endpoints are used. All
> names, screens, copy and art in Aroklon are original.

## Sources

| # | source | URL | notes |
| --- | --- | --- | --- |
| 1 | marketing site | https://www.aronium.com/ | positioning: free native Windows POS, lite vs pro |
| 2 | FAQ | https://www.aronium.com/en/faq | offline-first, Windows 7–11, freemium, hardware |
| 3 | help center | https://help.aronium.com/hc/en-us | full feature list, settings, workflows |
| 4 | management section | https://help.aronium.com/hc/en-us/articles/210235085-Access-management-section | the management menu map |
| 5 | making sales | https://help.aronium.com/hc/en-us/sections/202198645-Making-sales | sale loop, shortcuts, refunds |
| 6 | payment | https://help.aronium.com/hc/en-us/articles/209524645-Payment | tender, change, split payment |
| 7 | discounts | https://help.aronium.com/hc/en-us/articles/208793989-Applying-discounts | cart vs item discount, tax order |
| 8 | products | https://help.aronium.com/hc/en-us/articles/209345969-Manage-products | product fields, tabs, visual layout |
| 9 | documents | https://help.aronium.com/hc/en-us/sections/115000336409-Documents | purchase, return, count, loss, proforma, credit |
| 10 | inventory count | https://help.aronium.com/hc/en-us/articles/115001110085-Inventory-count | expected vs counted, stock date |
| 11 | end of day | https://help.aronium.com/hc/en-us/articles/115002176449-End-of-day-Z-Report | Z/X report, cash out, close register |
| 12 | cash in/out | https://help.aronium.com/hc/en-us/articles/115003327231-Cash-In-Out | starting cash, reasons |
| 13 | users & access | https://help.aronium.com/hc/en-us/articles/115001224685-User-management | users, access levels |
| 14 | customers | https://help.aronium.com/hc/en-us/articles/212461405-Loyalty-cards | customers, loyalty cards |
| 15 | keyboard shortcuts | https://help.aronium.com/hc/en-us/articles/210583909-Keyboard-shortcuts | speed-of-service keys |
| 16 | third-party review | https://toollers.com/reviews/aronium-review/ | Lite vs Pro, plugin pricing, limits |
| 17 | third-party guide | https://globaltill.com/aronium-pos-review-guide-2026/ | feature summary, hardware |

## Core loop

Find a product by barcode/code/name, build an order, take a payment (cash/card/split),
print a receipt, and have stock, sales and cash totals update — all offline.

## Screens

| ID | screen | route / how to reach | purpose | key components | states seen |
| --- | --- | --- | --- | --- | --- |
| S00 | Sign in | app start / switch user | identify the cashier | user list, PIN/password, register | empty, wrong PIN, deactivated |
| S01 | Sales (till) | main menu → Sales | ring a sale | search switcher, product grid/list, order lines, totals, discount, customer, quick tender, action bar | empty cart, filled, no products, barcode not found, age warning |
| S02 | Payment | F10 / Payment button | take money | tender buttons, paid amount, change, split payments, discount, tax override, customer | cash, card, split, exact, insufficient, cancelled |
| S03 | Discount dialog | F2 / Discount | cart or item discount | cart/item tabs, fixed/percent, value, clear | empty, applied, disabled (customer discount) |
| S04 | Product search | F3 / Search | find any product | search box, filters, result list | empty query, no result, many results |
| S05 | Refund details | Refund | return items | receipt no, items checklist, payment type | full, partial, not found |
| S06 | Sales history | management → documents | find past sales | date/user filters, receipt list, reprint/refund | empty, filtered, day closed |
| S07 | Cash in / out | main menu | add/remove drawer cash | add/remove tabs, amount, reason, entry list | empty, entries, errors |
| S08 | End of day (Z) | main menu → End of day | close register/day | cash-out summary, tender breakdown, close, X report, history | open sales warning, confirm, closed, reprint |
| S09 | Dashboard | management → Dashboard | at-a-glance sales | KPI cards, sales chart, top items, payment mix | no data, today, range |
| S10 | Documents list | management → Documents | search every document | filters (customer, product, type, user, register, paid, number, period), list, edit | empty, filtered, many |
| S11 | Document editor | documents → Add / Edit | create purchase/return/count/loss/proforma | header, item list, payments, save | new, edit, read-only totals |
| S12 | Products list | management → Products | browse catalogue | group tree, search, grid/list, new/edit/delete | empty, filtered, in-use (cannot delete) |
| S13 | Product editor | products → New/Edit | define a product | details, price & tax, comments, image & color tabs | new, edit, validation, barcode exists |
| S14 | Stock | management → Stock | quantities on hand | product, group, qty, value, low-stock flag | in stock, low, out, service |
| S15 | Reporting | management → Reporting | print/save reports | report list, filters, preview, save as PDF/CSV | list, preview, no data |
| S16 | Customers & suppliers | management → Customers | contacts | list, search, add/edit, type | empty, filtered |
| S17 | Customer editor | customers → Edit | contact + loyalty | details, discounts, loyalty cards | new, edit, cards added |
| S18 | Users & security | management → Users | staff accounts | list, access level, active flag | empty, own level cap |
| S19 | User editor | users → Add/Edit | account details | name, PIN/password, access level, register | new, edit, validation |
| S20 | Payment types | management → Payment types | tenders | list, name, icon/color, active | default set, inactive |
| S21 | Tax rates | management → Tax rates | VAT/GST | list, name, percent, compound | default, multiple |
| S22 | My company | management → My company | receipt/report identity | name, address, tax id, logo, receipt header/footer | blank, filled |
| S23 | Countries | management → Countries | customer country list | list, add | default |
| S24 | Promotions & actions | management → Promotions | happy hour, promos | rules, schedule, discount | none, active |
| S25 | Cash registers | management → Cash registers | register identity | name, ID, printer, drawer | single, multi (Pro) |
| S26 | Print options | settings | receipt template | header, footer, logo, width, copies | default, custom |
| S27 | Settings | main menu → Settings | app behaviour | business day, cash-in prompt, layout, backup/restore | default, changed |
| S28 | First-run setup | first launch | create company + admin | company, currency, tax, admin PIN | blank, complete |
| S29 | Reports preview | reporting → show | view/print | document preview, save PDF/CSV, print | preview, print dialog |
| S30 | Price lists *(paid plugin)* | management → Price lists | per-customer pricing | list, assign | skipped for clone |
| S31 | Print stations *(paid plugin)* | settings | route to kitchen/bar | station, printer mapping | skipped for clone |

## Flows

```
F01 Ring a sale (happy path)
    S00 -> S01 -> (scan or F3 search) -> S01 -> F10 -> S02 -> receipt
    happy path clicks: 4 (product, qty optional, payment type, confirm)
    edge: barcode unknown, item out of stock, price change not allowed,
          customer discount overrides manual discount, split tender, cash short

F02 Split payment
    S01 -> S02 -> Split payments -> add tender 1 -> add tender 2 -> confirm
    edge: over-tender, mixed cash+card, change only on cash

F03 Refund / return
    S01 (add items) -> Refund -> receipt no -> payment type -> OK
    or S06 -> select sale -> Refund -> OK
    edge: partial refund, old receipt, already refunded, restock toggle

F04 Receive a purchase (stock in)
    S10 -> Add -> Purchase -> vendor -> items -> prices -> payments -> Save
    edge: duplicate external doc, no vendor, zero price

F05 Inventory count
    S10 -> Add -> Inventory count -> stock date -> counted qty -> Save
    edge: future date blocked, expected qty, large variance

F06 Close the day
    S08 -> close open sales -> cash out -> close register -> pick reports -> Z report
    edge: open sale blocks close, no starting cash, printer offline

F07 Cash in / out
    S07 -> Add cash / Remove cash -> amount + reason -> Save
    edge: negative drawer, reason required

F08 Create a product
    S12 -> New product -> details -> price & tax -> image/color -> Save
    edge: duplicate code/barcode, inactive, service item

F09 Add customer + loyalty
    S16 -> Add -> details -> loyalty cards -> Save -> attach to sale
    edge: duplicate card, RFID scan, customer discount

F10 Import products from CSV
    S12 -> Import -> map columns -> preview -> import
    edge: bad rows, duplicate codes, partial failure

F11 Print a receipt
    S02 -> confirm -> receipt preview -> print / save PDF
    edge: no printer, custom header/footer, logo

F12 Sales report
    S15 -> pick report -> filters -> Show -> print or save
    edge: no data, unsupported filter for report
```

## Components

| component | variants | states | used on |
| --- | --- | --- | --- |
| Button | primary, secondary, ghost, danger, success | default, hover, focus, disabled, loading | all |
| Icon rail / nav | collapsed, labels | active, hover, disabled | S01–S27 |
| Search box | name, code, barcode | idle, typing, result, not found | S01, S04, S12, S16 |
| Product tile | grid, list, visual (image/color) | default, selected, out of stock, disabled | S01, S04 |
| Order line | normal, discounted, voided, selected | default, hover, selected | S01 |
| Quantity stepper | inline, dialog | default, min 1 | S01 |
| Tender button | cash, card, custom | default, selected, insufficient | S02 |
| Numeric keypad | 4x4, 3x4 | default, pressed | S02, S07 |
| Modal / dialog | confirm, form, large | open, error, busy | many |
| Data table | list, grid | empty, loading, filtered, selected | S06, S10, S12, S14, S16, S18, S20, S21 |
| Tabs | horizontal, vertical | active | S13, S17 |
| KPI card | value, delta, spark | default, empty | S09 |
| Chart | bar, line, donut | empty, hover | S09 |
| Toast | success, error, info | show, auto-hide | all |
| Badge | active, low stock, tax, type | default | lists |
| Empty state | icon, title, action | default | lists |
| Confirm sheet | destructive, normal | open | delete/close day |
| Form field | text, number, select, toggle, date | default, error, disabled | all editors |

## Inferred data model

```
Company      id, name, address, phone, email, tax_id, logo, receipt_header,
             receipt_footer, currency, currency_symbol, timezone
             evidence: S22 company details, receipt articles, S26 print options
             confidence: high

User         id, name, pin_hash, access_level, active, register_id, created_at
             evidence: S18/S19 user management, access-level article
             confidence: high

AccessLevel  id, name, rank, permissions (json)
             evidence: "Setting up access levels", user can only manage <= own level
             confidence: high

CashRegister id, name, code, printer_name, drawer_enabled, is_primary
             evidence: S25 cash registers, Pro multi-register article
             confidence: high

PaymentType  id, name, kind (cash|card|other), icon, color, active, is_default
             evidence: S20 payment types, S02 tender buttons
             confidence: high

TaxRate      id, name, percent, compound, active, is_default
             evidence: S21 tax rates, tax overrides, tax-after-discount rule
             confidence: high

Country      id, name, code, active
             evidence: S23 countries
             confidence: high

ProductGroup id, name, parent_id, color, sort_order
             evidence: S12 group tree, "organize products into groups"
             confidence: high

Product      id, name, code (sku), unit, group_id, active, is_service,
             cost, price, price_incl_tax, price_change_allowed, default_qty,
             age_restricted, color, image, description, low_stock_threshold
             evidence: S13 product fields, stock control article
             confidence: high

Barcode      id, product_id, value
             evidence: "as many barcodes as you wish", scan-to-add
             confidence: high

ProductComment id, product_id, text, sort_order
             evidence: comments tab, kitchen ticket comments
             confidence: medium

TaxOnProduct id, product_id, tax_rate_id, sort_order
             evidence: multiple taxes per product with +/- controls
             confidence: high

PriceList    id, name, active        (paid plugin)
PriceListItem id, price_list_id, product_id, price
             evidence: paid price lists plugin
             confidence: medium

Customer     id, type (customer|supplier), name, company, email, phone,
             address, country_id, tax_id, notes, active, discount_type,
             discount_value, balance
             evidence: S16/S17, customer discounts, loyalty cards
             confidence: high

LoyaltyCard  id, customer_id, number, active
             evidence: loyalty card tab, RFID/magstripe reader
             confidence: high

Document     id, number, type (sale|refund|purchase|stock_return|count|loss|
             proforma|credit), status (open|closed|void), external_number,
             customer_id, supplier_id, user_id, register_id, doc_date,
             stock_date, subtotal, discount_total, tax_total, total,
             paid_total, change_total, notes, created_at
             evidence: S10 documents, S11 editor, Z report fields
             confidence: high

DocumentItem id, document_id, product_id, name_snapshot, sku_snapshot,
             quantity, unit_price, cost, discount_type, discount_value,
             tax_total, total, voided, sort_order
             evidence: document item flyout, refund items, discounts
             confidence: high

Payment      id, document_id, payment_type_id, amount, tendered, change,
             reference
             evidence: S02 payment + split payments, Z report tender breakdown
             confidence: high

StockItem    product_id, quantity, avg_cost
             evidence: S14 quantity on hand, moving average price article
             confidence: high

StockMovement id, product_id, document_id, movement (in|out|adjust),
             quantity, unit_cost, occurred_at
             evidence: stock history, purchase/count/loss/refund
             confidence: high

CashMovement id, register_id, user_id, kind (in|out), amount, reason,
             occurred_at, closed_by_zreport_id
             evidence: S07 cash in/out, Z report cash-in/out total
             confidence: high

ZReport      id, number, register_id, opened_at, closed_at, opened_by,
             closed_by, opening_cash, cash_in, cash_out, tendered_by_type (json),
             sales_count, returns_count, discount_total, tax_total, total,
             items_report (json)
             evidence: S08 Z report 22-field layout
             confidence: high

Promotion    id, name, kind (happy_hour|percent|fixed|bundle), scope (cart|item|group),
             value, starts_at, ends_at, days, active
             evidence: S24 promotions & happy hour
             confidence: medium

Setting      key, value
             evidence: settings, "show cash in on start", manual business day
             confidence: medium
```

Relationships: Company 1-n User, User 1-n Document, ProductGroup 1-n Product,
Product 1-n Barcode / TaxOnProduct / StockItem / ProductComment, Customer 1-n
Document / LoyaltyCard, Document 1-n DocumentItem / Payment, Document 1-n
StockMovement, CashRegister 1-n Document / CashMovement / ZReport.

## Feature matrix

See `features.csv`. Must: 17, should: 10, could: 7, skip: 4.

## Out of scope (cannot or should not be cloned)

- Aronium's brand, name, logo, marketing copy, help articles and screenshots.
- Aronium Pro licensing, My Aronium accounts, license files, multi-register SQL
  Server setup, and paid plugin subscriptions (price lists, print stations,
  email reports, fiscal integrations).
- Their plugin marketplace/partner integrations and fiscal certifications.
- Hardware certification and driver support (we target standard Windows
  printers, scanners and touch screens, not a certified device list).
- Cloud sync and remote support.
- Regulatory/tax certification for any specific country — Aroklon stores tax
  rates and prints them, it does not file returns.

## Size

Screens 32, flows 12, entities 21. Hard parts: offline-first local database with
safe persistence/backup, mixed-tender payment maths, stock movement accounting
across sales/purchases/counts/refunds, receipt printing on arbitrary Windows
printers, and a fast touch-friendly till UI. Size: L. The clone ships the core
single-register slice (must + most should); multi-register and paid plugins are
out of scope.