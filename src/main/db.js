'use strict';

const path = require('path');
const fs = require('fs');
const initSqlJs = require('sql.js');

const WASM_DIR = path.join(__dirname, '..', '..', 'node_modules', 'sql.js', 'dist');

let SQL = null;
let db = null;
let dbPath = null;
let saveTimer = null;
let txDepth = 0;
let lastRecovery = null;

/* ------------------------------------------------------------------ utils */

const nowIso = () => new Date().toISOString();
const pad2 = (n) => String(n).padStart(2, '0');
const nowLocal = () => {
  const d = new Date();
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}T${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(
    d.getSeconds()
  )}`;
};
const today = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
const round = (n) => Math.round(Number(n) + Number.EPSILON);
const cents = (v) => round((Number(v) || 0) * 100);
const money = (c) => (Number(c) || 0) / 100;
const num = (v, d = 0) => {
  const n = Number(v);
  return Number.isFinite(n) ? n : d;
};

const SCHEMA = `
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS setting (
  key TEXT PRIMARY KEY,
  value TEXT
);

CREATE TABLE IF NOT EXISTS company (
  id INTEGER PRIMARY KEY CHECK (id = 1),
  name TEXT, address TEXT, phone TEXT, email TEXT, tax_id TEXT,
  logo TEXT, receipt_header TEXT, receipt_footer TEXT,
  currency TEXT DEFAULT 'IDR', symbol TEXT DEFAULT 'Rp', timezone TEXT
);

CREATE TABLE IF NOT EXISTS access_level (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  rank INTEGER NOT NULL,
  permissions TEXT
);

CREATE TABLE IF NOT EXISTS user (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  pin TEXT NOT NULL,
  access_level_id INTEGER,
  active INTEGER DEFAULT 1,
  register_id INTEGER,
  permissions TEXT DEFAULT '{}',
  created_at TEXT
);

CREATE TABLE IF NOT EXISTS register (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  code TEXT,
  printer TEXT,
  drawer INTEGER DEFAULT 1,
  is_primary INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS payment_type (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  kind TEXT DEFAULT 'other',
  color TEXT DEFAULT '#64748b',
  icon TEXT,
  active INTEGER DEFAULT 1,
  is_default INTEGER DEFAULT 0,
  sort_order INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS tax_rate (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  percent REAL NOT NULL DEFAULT 0,
  compound INTEGER DEFAULT 0,
  active INTEGER DEFAULT 1,
  is_default INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS country (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  code TEXT,
  active INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS product_group (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  parent_id INTEGER,
  color TEXT DEFAULT '#6366f1',
  sort_order INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS product (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  code TEXT,
  unit TEXT DEFAULT 'pcs',
  group_id INTEGER,
  active INTEGER DEFAULT 1,
  is_service INTEGER DEFAULT 0,
  cost INTEGER DEFAULT 0,
  price INTEGER DEFAULT 0,
  price_incl_tax INTEGER DEFAULT 0,
  price_change_allowed INTEGER DEFAULT 1,
  default_qty INTEGER DEFAULT 0,
  age_restricted INTEGER DEFAULT 0,
  color TEXT DEFAULT '#e2e8f0',
  image TEXT,
  description TEXT,
  low_stock INTEGER DEFAULT 0,
  wholesale_price INTEGER DEFAULT 0,
  wholesale_min_qty REAL DEFAULT 0,
  sort_order INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS barcode (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL,
  value TEXT NOT NULL,
  unit_id INTEGER
);

CREATE TABLE IF NOT EXISTS product_comment (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL,
  text TEXT NOT NULL,
  sort_order INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS product_tax (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL,
  tax_rate_id INTEGER NOT NULL,
  sort_order INTEGER DEFAULT 0
);

-- Additional selling units for a product. The product's own unit stays the
-- base unit (factor 1); rows here are conversions such as box, kg or litre.
CREATE TABLE IF NOT EXISTS product_unit (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL,
  name TEXT NOT NULL,
  factor REAL NOT NULL DEFAULT 1,
  price INTEGER,
  is_default INTEGER DEFAULT 0,
  sort_order INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS customer (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  type TEXT DEFAULT 'customer',
  name TEXT NOT NULL,
  company TEXT, email TEXT, phone TEXT, address TEXT,
  country_id INTEGER, tax_id TEXT, notes TEXT,
  active INTEGER DEFAULT 1,
  discount_type TEXT, discount_value REAL DEFAULT 0,
  balance INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS loyalty_card (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  customer_id INTEGER NOT NULL,
  number TEXT NOT NULL,
  active INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS document (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  number TEXT,
  type TEXT NOT NULL,
  status TEXT DEFAULT 'closed',
  external_number TEXT,
  customer_id INTEGER,
  supplier_id INTEGER,
  user_id INTEGER,
  original_document_id INTEGER,
  register_id INTEGER,
  doc_date TEXT,
  stock_date TEXT,
  subtotal INTEGER DEFAULT 0,
  item_discount INTEGER DEFAULT 0,
  cart_discount INTEGER DEFAULT 0,
  discount_total INTEGER DEFAULT 0,
  tax_override_percent REAL,
  tax_total INTEGER DEFAULT 0,
  tax_breakdown TEXT,
  total INTEGER DEFAULT 0,
  paid_total INTEGER DEFAULT 0,
  change_total INTEGER DEFAULT 0,
  notes TEXT,
  void_reason TEXT,
  created_at TEXT,
  closed_at TEXT
);

CREATE TABLE IF NOT EXISTS document_item (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  document_id INTEGER NOT NULL,
  product_id INTEGER,
  name_snapshot TEXT,
  sku_snapshot TEXT,
  quantity REAL NOT NULL DEFAULT 1,
  unit_price INTEGER DEFAULT 0,
  unit_snapshot TEXT,
  unit_factor REAL DEFAULT 1,
  cost INTEGER DEFAULT 0,
  discount_type TEXT,
  discount_value REAL DEFAULT 0,
  discount_total INTEGER DEFAULT 0,
  tax_total INTEGER DEFAULT 0,
  total INTEGER DEFAULT 0,
  voided INTEGER DEFAULT 0,
  sort_order INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS payment (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  document_id INTEGER NOT NULL,
  payment_type_id INTEGER,
  amount INTEGER DEFAULT 0,
  tendered INTEGER DEFAULT 0,
  change INTEGER DEFAULT 0,
  reference TEXT
);

CREATE TABLE IF NOT EXISTS stock_item (
  product_id INTEGER PRIMARY KEY,
  quantity REAL DEFAULT 0,
  avg_cost INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS stock_movement (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER,
  document_id INTEGER,
  movement TEXT,
  quantity REAL,
  unit_cost INTEGER,
  occurred_at TEXT
);

CREATE TABLE IF NOT EXISTS cash_movement (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  register_id INTEGER,
  user_id INTEGER,
  kind TEXT,
  amount INTEGER DEFAULT 0,
  reason TEXT,
  occurred_at TEXT,
  zreport_id INTEGER
);

CREATE TABLE IF NOT EXISTS zreport (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  number TEXT,
  register_id INTEGER,
  opened_at TEXT,
  closed_at TEXT,
  opened_by INTEGER,
  closed_by INTEGER,
  opening_cash INTEGER DEFAULT 0,
  cash_in INTEGER DEFAULT 0,
  cash_out INTEGER DEFAULT 0,
  tendered_json TEXT,
  sales_count INTEGER DEFAULT 0,
  returns_count INTEGER DEFAULT 0,
  discount_total INTEGER DEFAULT 0,
  tax_total INTEGER DEFAULT 0,
  total INTEGER DEFAULT 0,
  items_json TEXT,
  cash_expected INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS deleted_document (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  number TEXT,
  type TEXT NOT NULL,
  total INTEGER DEFAULT 0,
  user_id INTEGER,
  reason TEXT,
  items_json TEXT,
  payments_json TEXT,
  deleted_at TEXT
);

CREATE TABLE IF NOT EXISTS number_seq (
  key TEXT PRIMARY KEY,
  next INTEGER DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_doc_type_date ON document(type, doc_date);
CREATE INDEX IF NOT EXISTS idx_doc_customer ON document(customer_id);
CREATE INDEX IF NOT EXISTS idx_item_doc ON document_item(document_id);
CREATE INDEX IF NOT EXISTS idx_pay_doc ON payment(document_id);
CREATE INDEX IF NOT EXISTS idx_barcode_value ON barcode(value);
CREATE INDEX IF NOT EXISTS idx_stock_movement ON stock_movement(product_id);
CREATE INDEX IF NOT EXISTS idx_product_unit ON product_unit(product_id);
`;

/* -------------------------------------------------------------- migrations */

function addColumn(table, column, decl) {
  const cols = all(`PRAGMA table_info(${table})`).map((c) => c.name);
  if (!cols.includes(column)) run(`ALTER TABLE ${table} ADD COLUMN ${column} ${decl}`);
}

// Older databases predate multi-unit support. Add the new columns and stamp
// existing lines with the base unit so reports and refunds keep working.
function migrate() {
  addColumn('barcode', 'unit_id', 'INTEGER');
  addColumn('document_item', 'unit_snapshot', 'TEXT');
  addColumn('document_item', 'unit_factor', 'REAL DEFAULT 1');
  addColumn('product', 'wholesale_price', 'INTEGER DEFAULT 0');
  addColumn('product', 'wholesale_min_qty', 'REAL DEFAULT 0');
  addColumn('user', 'permissions', "TEXT DEFAULT '{}'");
  addColumn('document', 'original_document_id', 'INTEGER');
  run(`UPDATE document_item SET unit_factor = 1 WHERE unit_factor IS NULL`);
  run(
    `UPDATE document_item SET unit_snapshot = COALESCE(
       (SELECT p.unit FROM product p WHERE p.id = document_item.product_id), '')
     WHERE unit_snapshot IS NULL`
  );
}

/* ------------------------------------------------------- database checks */

function sqliteColumnNames(src, table) {
  const stmt = src.prepare(`SELECT * FROM "${String(table).replace(/"/g, '""')}" LIMIT 0`);
  const names = stmt.getColumnNames().map((n) => String(n).toLowerCase());
  stmt.free();
  return names;
}

function schemaIsAroklon(src) {
  try {
    if (!sqliteTable(src, 'setting') || !sqliteTable(src, 'user') || !sqliteTable(src, 'product')) return false;
    const productCols = sqliteColumnNames(src, 'product');
    if (!productCols.includes('unit') || !productCols.includes('price_incl_tax') || !productCols.includes('price_change_allowed')) {
      return false;
    }
    const userCols = sqliteColumnNames(src, 'user');
    if (!userCols.includes('pin') || !userCols.includes('access_level_id')) return false;
    return true;
  } catch (e) {
    return false;
  }
}

function isAroklonBuffer(buffer) {
  let probe;
  try {
    probe = new SQL.Database(buffer);
  } catch (e) {
    return false;
  }
  const ok = schemaIsAroklon(probe);
  try {
    probe.close();
  } catch (e) {}
  return ok;
}

// Checks a file before it is allowed to replace the live database.
function inspectDatabaseFile(filePath) {
  if (!filePath || !fs.existsSync(filePath)) return { ok: false, kind: 'missing', error: 'File not found' };
  let src;
  try {
    src = new SQL.Database(fs.readFileSync(filePath));
  } catch (e) {
    return { ok: false, kind: 'unknown', error: 'Cannot read the file as a SQLite database.' };
  }
  try {
    const productCols = sqliteTable(src, 'product') ? sqliteColumnNames(src, 'product') : [];
    const looksAronium =
      productCols.includes('measurementunit') || sqliteTable(src, 'vatrate') || sqliteTable(src, 'documenttype');
    if (looksAronium) {
      return {
        ok: false,
        kind: 'aronium',
        error: 'This is an Aronium database. Use Settings → Data → Import Aronium database instead.',
      };
    }
    if (schemaIsAroklon(src)) return { ok: true, kind: 'aroklon' };
    return {
      ok: false,
      kind: 'unknown',
      error: 'This file is not an Aroklon database. Choose a backup created by Aroklon (Settings → Data → Backup now).',
    };
  } catch (e) {
    return { ok: false, kind: 'unknown', error: 'Cannot read the database file: ' + (e.message || String(e)) };
  } finally {
    try {
      src.close();
    } catch (e) {}
  }
}

/* --------------------------------------------------------------- lifecycle */

async function init(userDataDir) {
  SQL = await initSqlJs({ locateFile: (f) => path.join(WASM_DIR, f) });
  dbPath = path.join(userDataDir, 'aroklon.db');
  let buffer = null;

  if (fs.existsSync(dbPath)) {
    buffer = fs.readFileSync(dbPath);
    // A file that is not an Aroklon database (for example an Aronium pos.db
    // chosen by mistake) would make every query fail. Move it aside and try
    // the newest backup made before a restore.
    if (!isAroklonBuffer(buffer)) {
      const stamp = new Date().toISOString().replace(/[:.]/g, '-');
      const quarantine = path.join(userDataDir, `aroklon-invalid-${stamp}.db`);
      try {
        fs.renameSync(dbPath, quarantine);
      } catch (e) {
        throw new Error('The database file is not valid and could not be moved aside: ' + (e.message || String(e)));
      }
      buffer = null;
      lastRecovery = { path: quarantine, restoredFrom: null };

      let backups = [];
      try {
        backups = fs
          .readdirSync(userDataDir)
          .filter((f) => /^aroklon-before-restore-.*\.db$/.test(f))
          .sort()
          .reverse();
      } catch (e) {}
      for (const name of backups) {
        const candidate = path.join(userDataDir, name);
        let alt = null;
        try {
          alt = fs.readFileSync(candidate);
        } catch (e) {
          continue;
        }
        if (!isAroklonBuffer(alt)) continue;
        try {
          fs.copyFileSync(candidate, dbPath);
        } catch (e) {
          continue;
        }
        buffer = alt;
        lastRecovery = { path: quarantine, restoredFrom: candidate };
        break;
      }
    }
  }

  db = buffer ? new SQL.Database(buffer) : new SQL.Database();
  db.run(SCHEMA);
  migrate();
  seed();
  persist();
}

function persist() {
  if (!db || txDepth > 0) return;
  try {
    const data = db.export();
    fs.writeFileSync(dbPath, Buffer.from(data));
  } catch (e) {
    console.error('persist failed', e);
  }
}

function schedulePersist() {
  if (txDepth > 0) return;
  clearTimeout(saveTimer);
  saveTimer = setTimeout(persist, 250);
}

function all(sql, params = []) {
  const stmt = db.prepare(sql);
  try {
    stmt.bind(params);
    const out = [];
    while (stmt.step()) out.push(stmt.getAsObject());
    return out;
  } finally {
    stmt.free();
  }
}

function get(sql, params = []) {
  const rows = all(sql, params);
  return rows.length ? rows[0] : null;
}

function run(sql, params = []) {
  db.run(sql, params);
  schedulePersist();
}

function tx(fn) {
  txDepth++;
  db.run('BEGIN');
  try {
    const out = fn();
    db.run('COMMIT');
    txDepth--;
    schedulePersist();
    return out;
  } catch (e) {
    db.run('ROLLBACK');
    txDepth--;
    throw e;
  }
}

/* -------------------------------------------------------------------- seed */

function seed() {
  const count = (t) => Number(get(`SELECT COUNT(*) AS c FROM ${t}`).c);
  // A reset asks for an empty database; do not put the demo catalogue back
  // on the next start.
  const withSamples = getSetting('sample_data') !== '0';

  if (count('company') === 0) {
    run(
      `INSERT INTO company (id, name, address, phone, email, tax_id, logo,
        receipt_header, receipt_footer, currency, symbol, timezone)
       VALUES (1,?,?,?,?,?,?,?,?,?,?,?)`,
      [
        'Aroklon Store',
        'Jl. Merdeka No. 10, Jakarta',
        '021-555-0100',
        'hello@aroklon.local',
        '',
        null,
        'Aroklon Store',
        'Thank you for your purchase!',
        'IDR',
        'Rp',
        Intl.DateTimeFormat().resolvedOptions().timeZone || 'Asia/Jakarta',
      ]
    );
  }

  if (count('access_level') === 0) {
    run(`INSERT INTO access_level (name, rank, permissions) VALUES (?,?,?)`, ['Owner', 100, '{}']);
    run(`INSERT INTO access_level (name, rank, permissions) VALUES (?,?,?)`, ['Manager', 75, '{}']);
    run(`INSERT INTO access_level (name, rank, permissions) VALUES (?,?,?)`, ['Cashier', 40, '{}']);
  }

  if (count('register') === 0) {
    run(`INSERT INTO register (name, code, printer, drawer, is_primary) VALUES (?,?,?,?,?)`, [
      'Kasir Depan',
      'REG-01',
      '',
      1,
      1,
    ]);
  }

  if (count('user') === 0) {
    run(`INSERT INTO user (name, pin, access_level_id, active, register_id, created_at) VALUES (?,?,?,?,?,?)`, [
      'Owner',
      '1234',
      get(`SELECT id FROM access_level WHERE rank=100`).id,
      1,
      get(`SELECT id FROM register WHERE is_primary=1`).id,
      nowIso(),
    ]);
    run(`INSERT INTO user (name, pin, access_level_id, active, register_id, created_at) VALUES (?,?,?,?,?,?)`, [
      'Cashier',
      '1111',
      get(`SELECT id FROM access_level WHERE rank=40`).id,
      1,
      get(`SELECT id FROM register WHERE is_primary=1`).id,
      nowIso(),
    ]);
  }

  if (count('payment_type') === 0) {
    run(`INSERT INTO payment_type (name, kind, color, icon, active, is_default, sort_order) VALUES (?,?,?,?,?,?,?)`, [
      'Kas',
      'cash',
      '#10b981',
      'cash',
      1,
      1,
      1,
    ]);
    run(`INSERT INTO payment_type (name, kind, color, icon, active, is_default, sort_order) VALUES (?,?,?,?,?,?,?)`, [
      'Kartu',
      'card',
      '#6366f1',
      'card',
      1,
      0,
      2,
    ]);
    run(`INSERT INTO payment_type (name, kind, color, icon, active, is_default, sort_order) VALUES (?,?,?,?,?,?,?)`, [
      'QRIS',
      'other',
      '#f59e0b',
      'qr',
      1,
      0,
      3,
    ]);
  }

  if (count('tax_rate') === 0) {
    run(`INSERT INTO tax_rate (name, percent, compound, active, is_default) VALUES (?,?,?,?,?)`, [
      'No tax',
      0,
      0,
      1,
      0,
    ]);
    run(`INSERT INTO tax_rate (name, percent, compound, active, is_default) VALUES (?,?,?,?,?)`, [
      'VAT 11%',
      11,
      0,
      1,
      1,
    ]);
  }

  if (count('country') === 0) {
    for (const [n, c] of [
      ['Indonesia', 'ID'],
      ['Malaysia', 'MY'],
      ['Singapore', 'SG'],
      ['United States', 'US'],
      ['United Kingdom', 'GB'],
    ]) {
      run(`INSERT INTO country (name, code, active) VALUES (?,?,1)`, [n, c]);
    }
  }

  if (withSamples && count('product_group') === 0) {
    run(`INSERT INTO product_group (name, parent_id, color, sort_order) VALUES (?,?,?,?)`, [
      'Umum',
      null,
      '#6366f1',
      1,
    ]);
    run(`INSERT INTO product_group (name, parent_id, color, sort_order) VALUES (?,?,?,?)`, [
      'Minuman',
      get(`SELECT id FROM product_group WHERE name='Umum'`).id,
      '#0ea5e9',
      2,
    ]);
    run(`INSERT INTO product_group (name, parent_id, color, sort_order) VALUES (?,?,?,?)`, [
      'Camilan',
      get(`SELECT id FROM product_group WHERE name='Umum'`).id,
      '#f59e0b',
      3,
    ]);
  }

  if (withSamples && count('product') === 0) {
    const groupId = get(`SELECT id FROM product_group WHERE name='Minuman'`).id;
    const snackId = get(`SELECT id FROM product_group WHERE name='Camilan'`).id;
    const samples = [
      ['Mineral Water 600ml', '1000000000001', 'Minuman', 3000, 5000, 12, groupId],
      ['Cola Can 330ml', '1000000000002', 'Minuman', 4500, 7000, 24, groupId],
      ['Iced Tea Bottle', '1000000000003', 'Minuman', 5000, 8000, 18, groupId],
      ['Potato Chips', '1000000000004', 'Camilan', 6000, 10000, 9, snackId],
      ['Chocolate Bar', '1000000000005', 'Camilan', 7000, 12000, 6, snackId],
      ['Instant Noodles', '1000000000006', 'Camilan', 3500, 5500, 30, snackId],
      ['Garbage Bag 20L', '1000000000007', 'Umum', 8000, 15000, 2, get(`SELECT id FROM product_group WHERE name='Umum'`).id],
    ];
    for (let i = 0; i < samples.length; i++) {
      const [name, code, , cost, price, qty, gid] = samples[i];
      const res = run(
        `INSERT INTO product (name, code, unit, group_id, active, is_service, cost, price,
          price_incl_tax, price_change_allowed, default_qty, color, low_stock, sort_order)
         VALUES (?,?,?,?,1,0,?,?,0,1,0,?, ?,?)`,
        [name, code, 'pcs', gid, cents(cost), cents(price), i % 3 === 0 ? '#dbeafe' : '#fef3c7', 5, i + 1]
      );
      const pid = Number(res && res.lastID ? res.lastID : get(`SELECT last_insert_rowid() AS id`).id);
      run(`INSERT INTO barcode (product_id, value) VALUES (?,?)`, [pid, code]);
      run(`INSERT INTO product_tax (product_id, tax_rate_id, sort_order) VALUES (?,?,?)`, [
        pid,
        get(`SELECT id FROM tax_rate WHERE is_default=1`).id,
        1,
      ]);
      run(`INSERT INTO stock_item (product_id, quantity, avg_cost) VALUES (?,?,?)`, [pid, qty, cents(cost)]);
    }
  }

  if (withSamples && count('customer') === 0) {
    run(`INSERT INTO customer (type, name, company, email, phone, active) VALUES (?,?,?,?,?,1)`, [
      'customer',
      'Pelanggan Umum',
      '',
      '',
      '',
    ]);
    run(`INSERT INTO customer (type, name, company, email, phone, active) VALUES (?,?,?,?,?,1)`, [
      'supplier',
      'CV Sumber Rezeki',
      'CV Sumber Rezeki',
      'order@sumberrezeki.co.id',
      '021-555-0199',
    ]);

    run(
      `INSERT INTO customer (type, name, company, email, phone, address, active, discount_type, discount_value)
       VALUES ('customer','Budi Santoso','','budi@example.com','0812-5550-1122','Jl. Melati 12, Jakarta',1,'percent',5)`
    );
    run(`INSERT INTO loyalty_card (customer_id, number, active) VALUES (?,?,1)`, [
      get(`SELECT last_insert_rowid() AS id`).id,
      'LP-0001',
    ]);

    run(
      `INSERT INTO customer (type, name, company, email, phone, address, active)
       VALUES ('customer','Siti Aminah','','siti@example.com','0813-5550-3344','Jl. Kenanga 8, Jakarta',1)`
    );
    run(`INSERT INTO loyalty_card (customer_id, number, active) VALUES (?,?,1)`, [
      get(`SELECT last_insert_rowid() AS id`).id,
      'LP-0002',
    ]);

    run(
      `INSERT INTO customer (type, name, company, email, phone, address, active)
       VALUES ('supplier','CV Maju Bersama','CV Maju Bersama','sales@majubersama.co.id','021-555-0177','Kawasan Industri Pulogadung',1)`
    );
  }

  const settings = get(`SELECT value FROM setting WHERE key='seeded'`);
  if (!settings) {
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('seeded','1')`);
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('business_date',?)`, [today()]);
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('register_open','0')`);
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('opening_cash','0')`);
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('allow_negative_stock','0')`);
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('ask_qty','1')`);
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('layout','grid')`);
  }
}

/* ----------------------------------------------------------------- settings */

function getSetting(key, def = null) {
  const row = get(`SELECT value FROM setting WHERE key=?`, [key]);
  return row ? row.value : def;
}

function setSetting(key, value) {
  run(`INSERT INTO setting (key, value) VALUES (?,?) ON CONFLICT(key) DO UPDATE SET value=excluded.value`, [
    key,
    String(value),
  ]);
}

function getSettings() {
  const rows = all(`SELECT key, value FROM setting`);
  const out = {};
  for (const r of rows) out[r.key] = r.value;
  return out;
}

/* -------------------------------------------------------------------- auth */

function parsePermissions(value) {
  if (!value) return {};
  if (typeof value === 'object') return value && !Array.isArray(value) ? value : {};
  try {
    const p = JSON.parse(value);
    return p && typeof p === 'object' && !Array.isArray(p) ? p : {};
  } catch (e) {
    return {};
  }
}

function login(userId, pin) {
  const u = get(
    `SELECT u.*, a.name AS access_name, a.rank AS access_rank
     FROM user u LEFT JOIN access_level a ON a.id = u.access_level_id
     WHERE u.id=? AND u.active=1`,
    [userId]
  );
  if (!u || String(u.pin) !== String(pin)) return { ok: false, error: 'Invalid user or PIN' };
  return {
    ok: true,
    user: {
      id: u.id,
      name: u.name,
      access_level_id: u.access_level_id,
      access_name: u.access_name,
      access_rank: u.access_rank,
      register_id: u.register_id,
      permissions: parsePermissions(u.permissions),
    },
  };
}

function listUsers() {
  return all(
    `SELECT u.*, a.name AS access_name, a.rank AS access_rank, r.name AS register_name
     FROM user u
     LEFT JOIN access_level a ON a.id=u.access_level_id
     LEFT JOIN register r ON r.id=u.register_id
     ORDER BY a.rank DESC, u.name`
  ).map((u) => ({ ...u, permissions: parsePermissions(u.permissions) }));
}

function hasPermission(userId, key) {
  if (!userId) return false;
  const u = get(
    `SELECT u.permissions, a.rank
     FROM user u LEFT JOIN access_level a ON a.id=u.access_level_id
     WHERE u.id=?`,
    [userId]
  );
  if (!u) return false;
  if (num(u.rank) >= 100) return true;
  return parsePermissions(u.permissions)[key] === true;
}

function listAccessLevels() {
  return all(`SELECT * FROM access_level ORDER BY rank DESC`);
}

function saveUser(payload) {
  const id = payload.id;
  let permissions;
  if (payload.permissions !== undefined) {
    permissions = parsePermissions(payload.permissions);
  } else {
    const existing = id ? get(`SELECT permissions FROM user WHERE id=?`, [id]) : null;
    permissions = parsePermissions(existing ? existing.permissions : null);
    if (payload.can_edit_documents !== undefined) permissions['documents.edit'] = !!payload.can_edit_documents;
    if (payload.can_delete_documents !== undefined) permissions['documents.delete'] = !!payload.can_delete_documents;
  }
  const permissionsJson = JSON.stringify(permissions);

  if (payload.pin) {
    if (id) {
      run(
        `UPDATE user SET name=?, pin=?, access_level_id=?, active=?, register_id=?, permissions=? WHERE id=?`,
        [
          payload.name,
          String(payload.pin),
          payload.access_level_id,
          payload.active ? 1 : 0,
          payload.register_id || null,
          permissionsJson,
          id,
        ]
      );
    } else {
      run(
        `INSERT INTO user (name, pin, access_level_id, active, register_id, permissions, created_at)
         VALUES (?,?,?,?,?,?,?)`,
        [
          payload.name,
          String(payload.pin),
          payload.access_level_id,
          payload.active ? 1 : 0,
          payload.register_id || null,
          permissionsJson,
          nowIso(),
        ]
      );
    }
  } else if (id) {
    run(`UPDATE user SET name=?, access_level_id=?, active=?, register_id=?, permissions=? WHERE id=?`, [
      payload.name,
      payload.access_level_id,
      payload.active ? 1 : 0,
      payload.register_id || null,
      permissionsJson,
      id,
    ]);
  } else {
    return { ok: false, error: 'A PIN is required for a new user' };
  }
  persist();
  return { ok: true };
}

function deleteUser(id) {
  const used = get(`SELECT COUNT(*) AS c FROM document WHERE user_id=?`, [id]);
  if (used.c > 0) return { ok: false, error: 'User has documents and cannot be deleted. Deactivate instead.' };
  run(`DELETE FROM user WHERE id=?`, [id]);
  persist();
  return { ok: true };
}

/* ----------------------------------------------------------------- company */

function getCompany() {
  return get(`SELECT * FROM company WHERE id=1`);
}

function saveCompany(p) {
  run(
    `UPDATE company SET name=?, address=?, phone=?, email=?, tax_id=?, logo=?,
      receipt_header=?, receipt_footer=?, currency=?, symbol=?, timezone=? WHERE id=1`,
    [
      p.name,
      p.address,
      p.phone,
      p.email,
      p.tax_id,
      p.logo || null,
      p.receipt_header,
      p.receipt_footer,
      p.currency,
      p.symbol,
      p.timezone,
    ]
  );
  persist();
  return { ok: true };
}

/* --------------------------------------------------------------- tax & pay */

function listTaxRates() {
  return all(`SELECT * FROM tax_rate ORDER BY is_default DESC, name`);
}

function saveTaxRate(p) {
  if (p.id) {
    run(`UPDATE tax_rate SET name=?, percent=?, compound=?, active=?, is_default=? WHERE id=?`, [
      p.name,
      num(p.percent),
      p.compound ? 1 : 0,
      p.active ? 1 : 0,
      p.is_default ? 1 : 0,
      p.id,
    ]);
  } else {
    run(`INSERT INTO tax_rate (name, percent, compound, active, is_default) VALUES (?,?,?,?,?)`, [
      p.name,
      num(p.percent),
      p.compound ? 1 : 0,
      p.active ? 1 : 0,
      p.is_default ? 1 : 0,
    ]);
  }
  if (p.is_default) run(`UPDATE tax_rate SET is_default=0 WHERE id<>` + (p.id || get(`SELECT MAX(id) AS id FROM tax_rate`).id), []);
  persist();
  return { ok: true };
}

function deleteTaxRate(id) {
  const used = get(`SELECT COUNT(*) AS c FROM product_tax WHERE tax_rate_id=?`, [id]);
  if (used.c > 0) return { ok: false, error: 'Tax rate is used by products.' };
  run(`DELETE FROM tax_rate WHERE id=?`, [id]);
  persist();
  return { ok: true };
}

function listPaymentTypes() {
  return all(`SELECT * FROM payment_type ORDER BY sort_order, name`);
}

function savePaymentType(p) {
  if (p.id) {
    run(`UPDATE payment_type SET name=?, kind=?, color=?, icon=?, active=?, is_default=?, sort_order=? WHERE id=?`, [
      p.name,
      p.kind,
      p.color,
      p.icon,
      p.active ? 1 : 0,
      p.is_default ? 1 : 0,
      num(p.sort_order),
      p.id,
    ]);
  } else {
    run(
      `INSERT INTO payment_type (name, kind, color, icon, active, is_default, sort_order) VALUES (?,?,?,?,?,?,?)`,
      [p.name, p.kind, p.color, p.icon, p.active ? 1 : 0, p.is_default ? 1 : 0, num(p.sort_order)]
    );
  }
  if (p.is_default) {
    run(`UPDATE payment_type SET is_default=0 WHERE id<>?`, [p.id || get(`SELECT MAX(id) AS id FROM payment_type`).id]);
  }
  persist();
  return { ok: true };
}

function deletePaymentType(id) {
  const used = get(`SELECT COUNT(*) AS c FROM payment WHERE payment_type_id=?`, [id]);
  if (used.c > 0) return { ok: false, error: 'Payment type is used by transactions.' };
  run(`DELETE FROM payment_type WHERE id=?`, [id]);
  persist();
  return { ok: true };
}

/* ---------------------------------------------------------------- products */

function listGroups() {
  return all(`SELECT g.*, (SELECT COUNT(*) FROM product p WHERE p.group_id=g.id) AS product_count
              FROM product_group g ORDER BY g.sort_order, g.name`);
}

function saveGroup(p) {
  if (p.id) {
    run(`UPDATE product_group SET name=?, parent_id=?, color=?, sort_order=? WHERE id=?`, [
      p.name,
      p.parent_id || null,
      p.color,
      num(p.sort_order),
      p.id,
    ]);
  } else {
    run(`INSERT INTO product_group (name, parent_id, color, sort_order) VALUES (?,?,?,?)`, [
      p.name,
      p.parent_id || null,
      p.color,
      num(p.sort_order),
    ]);
  }
  persist();
  return { ok: true };
}

function deleteGroup(id) {
  const used = get(`SELECT COUNT(*) AS c FROM product WHERE group_id=?`, [id]);
  if (used.c > 0) return { ok: false, error: 'Group has products.' };
  const children = get(`SELECT COUNT(*) AS c FROM product_group WHERE parent_id=?`, [id]);
  if (children.c > 0) return { ok: false, error: 'Group has sub-groups.' };
  run(`DELETE FROM product_group WHERE id=?`, [id]);
  persist();
  return { ok: true };
}

function listProducts(opts = {}) {
  const where = [];
  const params = [];
  if (opts.search) {
    where.push(`(p.name LIKE ? OR p.code LIKE ? OR EXISTS (SELECT 1 FROM barcode b WHERE b.product_id=p.id AND b.value LIKE ?))`);
    const q = `%${opts.search}%`;
    params.push(q, q, q);
  }
  if (opts.groupId) {
    where.push(`(p.group_id=? OR p.group_id IN (SELECT id FROM product_group WHERE parent_id=?))`);
    params.push(opts.groupId, opts.groupId);
  }
  if (opts.activeOnly) where.push(`p.active=1`);
  if (opts.lowOnly) where.push(`p.is_service=0 AND p.active=1 AND COALESCE(s.quantity,0) <= p.low_stock AND p.low_stock > 0`);
  const sql =
    `SELECT p.*, g.name AS group_name, g.color AS group_color,
        COALESCE(s.quantity,0) AS quantity, COALESCE(s.avg_cost,0) AS avg_cost
     FROM product p
     LEFT JOIN product_group g ON g.id=p.group_id
     LEFT JOIN stock_item s ON s.product_id=p.id
     ${where.length ? 'WHERE ' + where.join(' AND ') : ''}
     ORDER BY p.sort_order, p.name`;
  return hydrateProducts(all(sql, params));
}

/* ----------------------------------------------------------- product units */

// product.unit is the base unit (factor 1). product_unit rows are extra units
// such as box or kg, each with a conversion factor and an optional own price.
function listUnitRows(productId) {
  return all(`SELECT * FROM product_unit WHERE product_id=? ORDER BY sort_order, factor, name`, [productId]);
}

function unitList(p) {
  const rows = listUnitRows(p.id).map((u) => ({
    id: u.id,
    name: u.name,
    factor: num(u.factor, 1),
    price: u.price == null ? null : num(u.price),
    is_default: !!u.is_default,
    sort_order: u.sort_order,
  }));
  rows.push({
    id: null,
    name: p.unit || 'pcs',
    factor: 1,
    price: null,
    is_default: !rows.some((u) => u.is_default),
    sort_order: 0,
    base: true,
  });
  return rows;
}

function resolveUnit(p, name) {
  const rows = unitList(p);
  const base = rows[rows.length - 1];
  if (name == null || String(name).trim() === '') {
    return rows.find((u) => u.is_default) || base;
  }
  const key = String(name).trim().toLowerCase();
  return rows.find((u) => String(u.name).trim().toLowerCase() === key) || base;
}

function unitPriceFor(p, unit) {
  const u = unit || resolveUnit(p, null);
  if (u.price != null) return Math.round(num(u.price));
  return Math.round(num(p.price) * num(u.factor, 1));
}

// Wholesale price per base unit. The master switch lives in settings and
// defaults to off, so nothing changes until the shop turns it on.
function wholesalePriceFor(p, unit, quantity) {
  if (getSetting('wholesale_enabled') !== '1') return null;
  if (!(num(p.wholesale_price) > 0) || !(num(p.wholesale_min_qty) > 0)) return null;
  const u = unit || resolveUnit(p, null);
  const factor = num(u.factor, 1) > 0 ? num(u.factor, 1) : 1;
  const baseQty = num(quantity) * factor;
  if (baseQty + 1e-9 < num(p.wholesale_min_qty)) return null;
  return Math.round(num(p.wholesale_price) * factor);
}

function unitFactor(p, name) {
  const f = num(resolveUnit(p, name).factor, 1);
  return f > 0 ? f : 1;
}

function hydrateProduct(p) {
  if (!p) return p;
  p.barcodes = all(`SELECT value FROM barcode WHERE product_id=?`, [p.id]).map((b) => b.value);
  p.taxes = all(
    `SELECT t.* FROM tax_rate t JOIN product_tax pt ON pt.tax_rate_id=t.id WHERE pt.product_id=? ORDER BY pt.sort_order`,
    [p.id]
  );
  p.comments = all(`SELECT * FROM product_comment WHERE product_id=? ORDER BY sort_order`, [p.id]);
  p.barcode_rows = all(
    `SELECT b.value, COALESCE(u.name, '') AS unit FROM barcode b
     LEFT JOIN product_unit u ON u.id = b.unit_id WHERE b.product_id=? ORDER BY b.id`,
    [p.id]
  );
  p.stock = get(`SELECT * FROM stock_item WHERE product_id=?`, [p.id]) || { quantity: 0, avg_cost: 0 };
  p.units = unitList(p);
  p.default_unit = (p.units.find((u) => u.is_default) || {}).name || p.unit;
  return p;
}

// Bulk version for lists. Thousands of products would otherwise mean thousands
// of small queries; this loads related rows in four statements.
function hydrateProducts(rows) {
  if (!rows || !rows.length) return rows || [];
  const ids = rows.map((r) => r.id);
  // Chunk the IN lists: SQLite limits the number of bound parameters.
  const chunks = [];
  for (let i = 0; i < ids.length; i += 400) chunks.push(ids.slice(i, i + 400));
  const barcodeRows = [];
  const unitRows = [];
  const taxRows = [];
  const commentRows = [];
  for (const part of chunks) {
    const ph = part.map(() => '?').join(',');
    barcodeRows.push(
      ...all(
        `SELECT id, product_id, value, unit_id FROM barcode WHERE product_id IN (${ph}) ORDER BY product_id, id`,
        part
      )
    );
    unitRows.push(
      ...all(
        `SELECT * FROM product_unit WHERE product_id IN (${ph}) ORDER BY product_id, sort_order, factor`,
        part
      )
    );
    taxRows.push(
      ...all(
        `SELECT pt.product_id, t.* FROM product_tax pt JOIN tax_rate t ON t.id=pt.tax_rate_id
         WHERE pt.product_id IN (${ph}) ORDER BY pt.product_id, pt.sort_order`,
        part
      )
    );
    commentRows.push(
      ...all(
        `SELECT * FROM product_comment WHERE product_id IN (${ph}) ORDER BY product_id, sort_order`,
        part
      )
    );
  }

  const unitName = new Map();
  for (const u of unitRows) unitName.set(u.id, u.name);

  const byProduct = { barcodes: {}, barcode_rows: {}, units: {}, taxes: {}, comments: {} };
  for (const b of barcodeRows) {
    (byProduct.barcodes[b.product_id] = byProduct.barcodes[b.product_id] || []).push(b.value);
    (byProduct.barcode_rows[b.product_id] = byProduct.barcode_rows[b.product_id] || []).push({
      value: b.value,
      unit: b.unit_id ? unitName.get(b.unit_id) || '' : '',
    });
  }
  for (const u of unitRows) {
    (byProduct.units[u.product_id] = byProduct.units[u.product_id] || []).push({
      id: u.id,
      name: u.name,
      factor: num(u.factor, 1),
      price: u.price == null ? null : num(u.price),
      is_default: !!u.is_default,
      sort_order: u.sort_order,
    });
  }
  for (const t of taxRows) {
    (byProduct.taxes[t.product_id] = byProduct.taxes[t.product_id] || []).push(t);
  }
  for (const c of commentRows) {
    (byProduct.comments[c.product_id] = byProduct.comments[c.product_id] || []).push(c);
  }

  return rows.map((r) => {
    const units = (byProduct.units[r.id] || []).slice();
    units.push({
      id: null,
      name: r.unit || 'pcs',
      factor: 1,
      price: null,
      is_default: !units.some((u) => u.is_default),
      sort_order: 0,
      base: true,
    });
    return {
      ...r,
      barcodes: byProduct.barcodes[r.id] || [],
      barcode_rows: byProduct.barcode_rows[r.id] || [],
      taxes: byProduct.taxes[r.id] || [],
      comments: byProduct.comments[r.id] || [],
      units,
      default_unit: (units.find((u) => u.is_default) || {}).name || r.unit,
      stock: { quantity: num(r.quantity), avg_cost: num(r.avg_cost) },
    };
  });
}

function getProduct(id) {
  return hydrateProduct(get(`SELECT p.*, g.name AS group_name FROM product p LEFT JOIN product_group g ON g.id=p.group_id WHERE p.id=?`, [id]));
}

function getProductByBarcode(code) {
  const byBarcode = get(
    `SELECT p.*, b.unit_id AS barcode_unit_id FROM product p JOIN barcode b ON b.product_id=p.id WHERE b.value=? LIMIT 1`,
    [String(code)]
  );
  if (byBarcode) {
    const unitId = byBarcode.barcode_unit_id;
    delete byBarcode.barcode_unit_id;
    const p = hydrateProduct(byBarcode);
    p.barcode_unit = unitId
      ? (get(`SELECT name FROM product_unit WHERE id=?`, [unitId]) || {}).name || null
      : null;
    return p;
  }
  return hydrateProduct(get(`SELECT * FROM product WHERE code=? LIMIT 1`, [String(code)]));
}

function saveProduct(p) {
  const code = (p.code || '').trim();
  if (code) {
    const dup = get(`SELECT id FROM product WHERE code=? AND id<>?`, [code, p.id || 0]);
    if (dup) return { ok: false, error: `Product code "${code}" already exists` };
  }
  const values = {
    name: p.name,
    code,
    unit: p.unit || 'pcs',
    group_id: p.group_id || null,
    active: p.active ? 1 : 0,
    is_service: p.is_service ? 1 : 0,
    cost: cents(p.cost),
    price: cents(p.price),
    price_incl_tax: p.price_incl_tax ? 1 : 0,
    price_change_allowed: p.price_change_allowed ? 1 : 0,
    default_qty: p.default_qty ? 1 : 0,
    age_restricted: p.age_restricted ? 1 : 0,
    color: p.color || '#e2e8f0',
    image: p.image || null,
    description: p.description || '',
    low_stock: num(p.low_stock),
    wholesale_price: cents(p.wholesale_price),
    wholesale_min_qty: num(p.wholesale_min_qty),
    sort_order: num(p.sort_order),
  };

  const baseName = String(values.unit || 'pcs').trim().toLowerCase();
  const unitRows = [];
  const unitNames = new Set();
  let defaultTaken = false;
  for (const raw of p.units || []) {
    const name = String(raw.name || '').trim();
    if (!name) continue;
    const factor = num(raw.factor, 1);
    if (!(factor > 0)) return { ok: false, error: `Unit "${name}" needs a factor greater than zero` };
    if (name.toLowerCase() === baseName) return { ok: false, error: `Unit "${name}" is already the base unit` };
    const key = name.toLowerCase();
    if (unitNames.has(key)) return { ok: false, error: `Unit "${name}" is listed twice` };
    unitNames.add(key);
    let price = null;
    if (raw.price != null && String(raw.price).trim() !== '') {
      price = cents(raw.price);
      if (price < 0) return { ok: false, error: `Price for "${name}" cannot be negative` };
    }
    const isDefault = !!raw.is_default && !defaultTaken ? 1 : 0;
    if (isDefault) defaultTaken = 1;
    unitRows.push({ name, factor, price, is_default: isDefault, sort_order: unitRows.length + 1 });
  }

  let id = p.id;
  if (id) {
    run(
      `UPDATE product SET name=?, code=?, unit=?, group_id=?, active=?, is_service=?, cost=?, price=?,
        price_incl_tax=?, price_change_allowed=?, default_qty=?, age_restricted=?, color=?, image=?,
        description=?, low_stock=?, wholesale_price=?, wholesale_min_qty=?, sort_order=? WHERE id=?`,
      [
        values.name,
        values.code,
        values.unit,
        values.group_id,
        values.active,
        values.is_service,
        values.cost,
        values.price,
        values.price_incl_tax,
        values.price_change_allowed,
        values.default_qty,
        values.age_restricted,
        values.color,
        values.image,
        values.description,
        values.low_stock,
        values.wholesale_price,
        values.wholesale_min_qty,
        values.sort_order,
        id,
      ]
    );
  } else {
    const res = run(
      `INSERT INTO product (name, code, unit, group_id, active, is_service, cost, price,
        price_incl_tax, price_change_allowed, default_qty, age_restricted, color, image,
        description, low_stock, wholesale_price, wholesale_min_qty, sort_order)
       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`,
      [
        values.name,
        values.code,
        values.unit,
        values.group_id,
        values.active,
        values.is_service,
        values.cost,
        values.price,
        values.price_incl_tax,
        values.price_change_allowed,
        values.default_qty,
        values.age_restricted,
        values.color,
        values.image,
        values.description,
        values.low_stock,
        values.wholesale_price,
        values.wholesale_min_qty,
        values.sort_order,
      ]
    );
    id = get(`SELECT last_insert_rowid() AS id`).id;
  }

  // units
  run(`DELETE FROM product_unit WHERE product_id=?`, [id]);
  const unitIdByName = {};
  for (const u of unitRows) {
    run(`INSERT INTO product_unit (product_id, name, factor, price, is_default, sort_order) VALUES (?,?,?,?,?,?)`, [
      id,
      u.name,
      u.factor,
      u.price,
      u.is_default,
      u.sort_order,
    ]);
    unitIdByName[u.name.toLowerCase()] = get(`SELECT last_insert_rowid() AS id`).id;
  }

  // barcodes (optionally tied to one unit)
  const wanted = [];
  for (const b of p.barcodes || []) {
    if (typeof b === 'string') {
      const v = b.trim();
      if (v) wanted.push({ value: v, unit: '' });
    } else if (b && String(b.value == null ? '' : b.value).trim()) {
      wanted.push({ value: String(b.value).trim(), unit: b.unit ? String(b.unit).trim().toLowerCase() : '' });
    }
  }
  if (!wanted.length && values.code) wanted.push({ value: values.code, unit: '' });
  const bars = wanted.map((b) => {
    let unit_id = null;
    if (b.unit && b.unit !== baseName) unit_id = unitIdByName[b.unit] || null;
    return { value: b.value, unit_id };
  });
  const existing = all(`SELECT id, value, unit_id FROM barcode WHERE product_id=?`, [id]);
  for (const e of existing) {
    const w = bars.find((x) => x.value === e.value);
    if (!w) run(`DELETE FROM barcode WHERE id=?`, [e.id]);
    else if (num(w.unit_id) !== num(e.unit_id)) run(`UPDATE barcode SET unit_id=? WHERE id=?`, [w.unit_id, e.id]);
  }
  for (const w of bars) {
    if (!existing.some((e) => e.value === w.value))
      run(`INSERT INTO barcode (product_id, value, unit_id) VALUES (?,?,?)`, [id, w.value, w.unit_id]);
  }

  // taxes
  run(`DELETE FROM product_tax WHERE product_id=?`, [id]);
  for (let i = 0; i < (p.tax_rate_ids || []).length; i++) {
    run(`INSERT INTO product_tax (product_id, tax_rate_id, sort_order) VALUES (?,?,?)`, [id, p.tax_rate_ids[i], i + 1]);
  }

  // comments
  run(`DELETE FROM product_comment WHERE product_id=?`, [id]);
  (p.comments || []).forEach((c, i) => {
    if (c && String(c).trim()) run(`INSERT INTO product_comment (product_id, text, sort_order) VALUES (?,?,?)`, [id, String(c).trim(), i + 1]);
  });

  if (!get(`SELECT 1 FROM stock_item WHERE product_id=?`, [id])) {
    run(`INSERT INTO stock_item (product_id, quantity, avg_cost) VALUES (?,?,?)`, [id, 0, values.cost]);
  }
  persist();
  return { ok: true, id };
}

function saveWholesalePrices(items) {
  tx(() => {
    for (const it of items || []) {
      const price = Math.max(0, cents(it.price));
      const minQty = Math.max(0, num(it.min_qty));
      run(`UPDATE product SET wholesale_price=?, wholesale_min_qty=? WHERE id=?`, [price, minQty, Number(it.id)]);
    }
  });
  persist();
  return { ok: true };
}

function deleteProduct(id) {
  const used = get(`SELECT COUNT(*) AS c FROM document_item WHERE product_id=?`, [id]);
  if (used.c > 0) return { ok: false, error: 'Product appears in documents and cannot be deleted. Deactivate it.' };
  tx(() => {
    run(`DELETE FROM product_tax WHERE product_id=?`, [id]);
    run(`DELETE FROM product_unit WHERE product_id=?`, [id]);
    run(`DELETE FROM barcode WHERE product_id=?`, [id]);
    run(`DELETE FROM product_comment WHERE product_id=?`, [id]);
    run(`DELETE FROM stock_item WHERE product_id=?`, [id]);
    run(`DELETE FROM stock_movement WHERE product_id=?`, [id]);
    run(`DELETE FROM product WHERE id=?`, [id]);
  });
  return { ok: true };
}

/* ------------------------------------------------------------------- stock */

function listStock(opts = {}) {
  return all(
    `SELECT p.id, p.name, p.code, p.unit, p.active, p.is_service, p.low_stock, p.color,
        g.name AS group_name, COALESCE(s.quantity,0) AS quantity, COALESCE(s.avg_cost,0) AS avg_cost,
        p.cost, p.price
     FROM product p
     LEFT JOIN product_group g ON g.id=p.group_id
     LEFT JOIN stock_item s ON s.product_id=p.id
     ${opts.lowOnly ? 'WHERE p.is_service=0 AND p.active=1 AND p.low_stock>0 AND COALESCE(s.quantity,0)<=p.low_stock' : ''}
     ORDER BY p.name`
  );
}

function adjustStock(productId, quantity, reason) {
  tx(() => {
    const item = get(`SELECT * FROM stock_item WHERE product_id=?`, [productId]) || { quantity: 0, avg_cost: 0 };
    const delta = num(quantity) - num(item.quantity);
    run(`UPDATE stock_item SET quantity=? WHERE product_id=?`, [quantity, productId]);
    if (delta !== 0) {
      run(`INSERT INTO stock_movement (product_id, document_id, movement, quantity, unit_cost, occurred_at) VALUES (?,?,?,?,?,?)`, [
        productId,
        null,
        'adjust',
        delta,
        item.avg_cost,
        nowIso(),
      ]);
    }
  });
  persist();
  return { ok: true };
}

function stockMovements(productId) {
  return all(`SELECT * FROM stock_movement WHERE product_id=? ORDER BY id DESC LIMIT 100`, [productId]);
}

/* --------------------------------------------------------------- customers */

function listCustomers(opts = {}) {
  const where = [];
  const params = [];
  if (opts.type) {
    where.push('type=?');
    params.push(opts.type);
  }
  if (opts.search) {
    where.push('(name LIKE ? OR company LIKE ? OR phone LIKE ? OR email LIKE ?)');
    const q = `%${opts.search}%`;
    params.push(q, q, q, q);
  }
  const sql = `SELECT c.*, co.name AS country_name,
      (SELECT COUNT(*) FROM loyalty_card lc WHERE lc.customer_id=c.id) AS card_count
    FROM customer c LEFT JOIN country co ON co.id=c.country_id
    ${where.length ? 'WHERE ' + where.join(' AND ') : ''}
    ORDER BY c.name`;
  return all(sql, params);
}

function getCustomer(id) {
  const c = get(`SELECT c.*, co.name AS country_name FROM customer c LEFT JOIN country co ON co.id=c.country_id WHERE c.id=?`, [id]);
  if (c) c.cards = all(`SELECT * FROM loyalty_card WHERE customer_id=?`, [id]);
  return c;
}

function saveCustomer(p) {
  const vals = {
    type: p.type || 'customer',
    name: p.name,
    company: p.company || '',
    email: p.email || '',
    phone: p.phone || '',
    address: p.address || '',
    country_id: p.country_id || null,
    tax_id: p.tax_id || '',
    notes: p.notes || '',
    active: p.active ? 1 : 0,
    discount_type: p.discount_type || null,
    discount_value: num(p.discount_value),
    balance: cents(p.balance),
  };
  let id = p.id;
  if (id) {
    run(
      `UPDATE customer SET type=?, name=?, company=?, email=?, phone=?, address=?, country_id=?,
        tax_id=?, notes=?, active=?, discount_type=?, discount_value=?, balance=? WHERE id=?`,
      [
        vals.type,
        vals.name,
        vals.company,
        vals.email,
        vals.phone,
        vals.address,
        vals.country_id,
        vals.tax_id,
        vals.notes,
        vals.active,
        vals.discount_type,
        vals.discount_value,
        vals.balance,
        id,
      ]
    );
  } else {
    run(
      `INSERT INTO customer (type, name, company, email, phone, address, country_id, tax_id, notes,
        active, discount_type, discount_value, balance) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)`,
      [
        vals.type,
        vals.name,
        vals.company,
        vals.email,
        vals.phone,
        vals.address,
        vals.country_id,
        vals.tax_id,
        vals.notes,
        vals.active,
        vals.discount_type,
        vals.discount_value,
        vals.balance,
      ]
    );
    id = get(`SELECT last_insert_rowid() AS id`).id;
  }

  run(`DELETE FROM loyalty_card WHERE customer_id=?`, [id]);
  (p.cards || []).forEach((n) => {
    if (n && String(n).trim()) run(`INSERT INTO loyalty_card (customer_id, number, active) VALUES (?,?,1)`, [id, String(n).trim()]);
  });
  persist();
  return { ok: true, id };
}

function deleteCustomer(id) {
  const used = get(`SELECT COUNT(*) AS c FROM document WHERE customer_id=?`, [id]);
  if (used.c > 0) return { ok: false, error: 'Customer has documents. Deactivate instead.' };
  run(`DELETE FROM loyalty_card WHERE customer_id=?`, [id]);
  run(`DELETE FROM customer WHERE id=?`, [id]);
  persist();
  return { ok: true };
}

function listCountries() {
  return all(`SELECT * FROM country ORDER BY name`);
}

function saveCountry(p) {
  if (p.id) run(`UPDATE country SET name=?, code=?, active=? WHERE id=?`, [p.name, p.code, p.active ? 1 : 0, p.id]);
  else run(`INSERT INTO country (name, code, active) VALUES (?,?,?)`, [p.name, p.code, p.active ? 1 : 0]);
  persist();
  return { ok: true };
}

/* --------------------------------------------------------------- registers */

function listRegisters() {
  return all(`SELECT * FROM register ORDER BY is_primary DESC, name`);
}

function saveRegister(p) {
  if (p.id) {
    run(`UPDATE register SET name=?, code=?, printer=?, drawer=?, is_primary=? WHERE id=?`, [
      p.name,
      p.code,
      p.printer,
      p.drawer ? 1 : 0,
      p.is_primary ? 1 : 0,
      p.id,
    ]);
  } else {
    run(`INSERT INTO register (name, code, printer, drawer, is_primary) VALUES (?,?,?,?,?)`, [
      p.name,
      p.code,
      p.printer,
      p.drawer ? 1 : 0,
      p.is_primary ? 1 : 0,
    ]);
  }
  if (p.is_primary) run(`UPDATE register SET is_primary=0 WHERE id<>?`, [p.id || get(`SELECT MAX(id) AS id FROM register`).id]);
  persist();
  return { ok: true };
}

/* --------------------------------------------------------------- numbering */

function nextNumber(prefix) {
  const row = get(`SELECT next FROM number_seq WHERE key=?`, [prefix]);
  const n = row ? num(row.next) : 1;
  run(`INSERT INTO number_seq (key, next) VALUES (?,?) ON CONFLICT(key) DO UPDATE SET next=excluded.next`, [prefix, n + 1]);
  return `${prefix}-${String(n).padStart(6, '0')}`;
}

/* ------------------------------------------------------------ sale maths */

function effectiveRate(rates) {
  let factor = 1;
  for (const r of rates) factor *= 1 + num(r.percent) / 100;
  return factor - 1;
}

function computeSale(input) {
  const items = input.items || [];
  if (!items.length) throw new Error('Add at least one product to the sale');

  const allowNegative = getSetting('allow_negative_stock') === '1';
  const productCache = new Map();
  const lines = [];

  // A customer with a preset discount overrides any manual cart discount.
  let discountInput = input.discount;
  if (input.customer_id) {
    const cust = get(`SELECT discount_type, discount_value FROM customer WHERE id=?`, [input.customer_id]);
    if (cust && cust.discount_type && num(cust.discount_value) > 0) {
      discountInput = { type: cust.discount_type, value: cust.discount_value };
    }
  }

  let subtotal = 0;
  let itemDiscount = 0;

  for (let i = 0; i < items.length; i++) {
    const it = items[i];
    const p = getProduct(it.product_id);
    if (!p) throw new Error(`Product ${it.product_id} not found`);
    if (!p.active) throw new Error(`"${p.name}" is not active`);
    productCache.set(p.id, p);

    const qty = num(it.quantity, 1);
    if (qty <= 0) throw new Error(`Invalid quantity for ${p.name}`);

    const unit = resolveUnit(p, it.unit);
    const factor = unitFactor(p, unit.name);
    const baseQty = qty * factor;

    if (!p.is_service && !allowNegative && num(p.stock.quantity) + 1e-9 < baseQty) {
      throw new Error(`Not enough stock for ${p.name} (on hand: ${p.stock.quantity} ${p.unit})`);
    }

    const normalPrice = unitPriceFor(p, unit);
    const wholesale = wholesalePriceFor(p, unit, qty);
    let unitPrice = wholesale != null ? wholesale : normalPrice;
    if (it.unit_price != null) {
      const override = cents(it.unit_price);
      // The till sends either the normal or the wholesale price; anything else
      // is treated as a manual override.
      if (override !== normalPrice && override !== unitPrice) {
        if (!p.price_change_allowed) throw new Error(`Price cannot be changed for ${p.name}`);
        unitPrice = override;
      }
    }

    const gross = Math.round(qty * unitPrice);
    let disc = 0;
    if (it.discount_type === 'percent') disc = (gross * num(it.discount_value)) / 100;
    else if (it.discount_type === 'fixed') disc = cents(it.discount_value);
    disc = Math.min(Math.max(disc, 0), gross);

    subtotal += gross;
    itemDiscount += disc;

    lines.push({
      sort_order: i + 1,
      product_id: p.id,
      name_snapshot: p.name,
      sku_snapshot: p.code,
      quantity: qty,
      unit_snapshot: unit.name,
      unit_factor: factor,
      unit_price: unitPrice,
      cost: p.cost,
      discount_type: it.discount_type || null,
      discount_value: num(it.discount_value),
      discount_total: disc,
      gross,
      net_after_item: gross - disc,
      is_service: p.is_service,
      incl: !!p.price_incl_tax,
      taxRates: p.taxes && p.taxes.length ? p.taxes : [],
    });
  }

  // cart discount
  const afterItem = lines.reduce((s, l) => s + l.net_after_item, 0);
  let cartDisc = 0;
  if (discountInput && num(discountInput.value) > 0) {
    if (discountInput.type === 'percent') cartDisc = (afterItem * num(discountInput.value)) / 100;
    else cartDisc = cents(discountInput.value);
    cartDisc = Math.min(Math.max(cartDisc, 0), afterItem);
  }

  const taxOverride =
    input.tax_override_percent != null && input.tax_override_percent !== ''
      ? num(input.tax_override_percent)
      : null;

  let taxTotal = 0;
  const breakdown = new Map();
  let total = 0;

  for (const l of lines) {
    const share = afterItem > 0 ? (l.net_after_item / afterItem) * cartDisc : 0;
    l.discount_total += share;
    const base = Math.max(l.net_after_item - share, 0);

    let rates;
    if (taxOverride != null) {
      rates = [{ name: 'Tax override', percent: taxOverride }];
    } else {
      rates = l.taxRates.length
        ? l.taxRates
        : [{ name: 'Tax', percent: num(getSetting('default_tax_percent', 0)) }];
    }
    const rate = effectiveRate(rates);
    let tax;
    if (l.incl) {
      tax = base - base / (1 + rate);
    } else {
      tax = (base * rate);
    }
    tax = Math.round(tax);
    l.tax_total = tax;
    // When prices include tax, base already contains it; otherwise add it on top.
    l.total = l.incl ? Math.round(base) : Math.round(base + tax);
    taxTotal += tax;
    total += l.total;

    if (tax > 0) {
      const sumRates = rates.reduce((s, r) => s + num(r.percent), 0) || 1;
      for (const r of rates) {
        const part = Math.round((tax * num(r.percent)) / sumRates);
        const key = `${r.name}|${num(r.percent)}`;
        if (!breakdown.has(key)) breakdown.set(key, { name: r.name, rate: num(r.percent), amount: 0 });
        breakdown.get(key).amount += part;
      }
    }
  }

  return {
    lines,
    subtotal: Math.round(subtotal),
    item_discount: Math.round(itemDiscount),
    cart_discount: Math.round(cartDisc),
    discount_total: Math.round(itemDiscount + cartDisc),
    tax_total: taxTotal,
    total,
    tax_breakdown: [...breakdown.values()],
    allowNegative,
  };
}

/* ------------------------------------------------------------------- sales */

function createSale(input) {
  return tx(() => {
    const calc = computeSale(input);
    const userId = input.user_id;
    const registerId = input.register_id;
    const docDate = input.doc_date || nowLocal();
    const number = nextNumber('S');

    const payments = (input.payments || []).map((p) => ({
      payment_type_id: p.payment_type_id,
      amount: cents(p.amount),
      tendered: p.tendered != null ? cents(p.tendered) : cents(p.amount),
      reference: p.reference || null,
    }));

    let paidTotal = 0;
    let changeTotal = 0;
    for (const p of payments) {
      if (p.tendered > p.amount) p.change = p.tendered - p.amount;
      else p.change = 0;
      paidTotal += p.amount;
      changeTotal += p.change;
    }

    let status = 'closed';
    if (paidTotal < calc.total) status = paidTotal === 0 ? 'open' : 'partial';
    if (paidTotal > calc.total && changeTotal === 0) status = 'closed';

    run(
      `INSERT INTO document (number, type, status, customer_id, user_id, register_id, doc_date,
        stock_date, subtotal, item_discount, cart_discount, discount_total, tax_override_percent,
        tax_total, tax_breakdown, total, paid_total, change_total, notes, created_at, closed_at)
       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`,
      [
        number,
        'sale',
        status,
        input.customer_id || null,
        userId || null,
        registerId || null,
        docDate,
        docDate,
        calc.subtotal,
        calc.item_discount,
        calc.cart_discount,
        calc.discount_total,
        input.tax_override_percent != null && input.tax_override_percent !== ''
          ? num(input.tax_override_percent)
          : null,
        calc.tax_total,
        JSON.stringify(calc.tax_breakdown),
        calc.total,
        paidTotal,
        changeTotal,
        input.notes || '',
        nowIso(),
        nowIso(),
      ]
    );
    const docId = get(`SELECT last_insert_rowid() AS id`).id;

    for (const l of calc.lines) {
      run(
        `INSERT INTO document_item (document_id, product_id, name_snapshot, sku_snapshot, quantity,
          unit_price, unit_snapshot, unit_factor, cost, discount_type, discount_value, discount_total,
          tax_total, total, voided, sort_order)
         VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?)`,
        [
          docId,
          l.product_id,
          l.name_snapshot,
          l.sku_snapshot,
          l.quantity,
          l.unit_price,
          l.unit_snapshot,
          l.unit_factor,
          l.cost,
          l.discount_type,
          l.discount_value,
          Math.round(l.discount_total),
          l.tax_total,
          l.total,
          l.sort_order,
        ]
      );

      if (!l.is_service) {
        applyStock(l.product_id, -l.quantity * l.unit_factor, l.cost, docId, 'out');
      }
    }

    for (const p of payments) {
      run(
        `INSERT INTO payment (document_id, payment_type_id, amount, tendered, change, reference)
         VALUES (?,?,?,?,?,?)`,
        [docId, p.payment_type_id, p.amount, p.tendered, p.change, p.reference]
      );
    }

    return getDocument(docId);
  });
}

function applyStock(productId, delta, unitCost, documentId, movement) {
  const row = get(`SELECT * FROM stock_item WHERE product_id=?`, [productId]) || { quantity: 0, avg_cost: 0 };
  let qty = num(row.quantity);
  let avg = num(row.avg_cost);
  if (delta > 0 && qty + delta > 0) {
    avg = Math.round((qty * avg + delta * num(unitCost)) / (qty + delta));
  }
  qty = qty + delta;
  if (!get(`SELECT 1 FROM stock_item WHERE product_id=?`, [productId])) {
    run(`INSERT INTO stock_item (product_id, quantity, avg_cost) VALUES (?,?,?)`, [productId, qty, avg]);
  } else {
    run(`UPDATE stock_item SET quantity=?, avg_cost=? WHERE product_id=?`, [qty, avg, productId]);
  }
  if (delta !== 0) {
    run(
      `INSERT INTO stock_movement (product_id, document_id, movement, quantity, unit_cost, occurred_at)
       VALUES (?,?,?,?,?,?)`,
      [productId, documentId || null, movement, delta, num(unitCost) || avg, nowLocal()]
    );
  }
}

function getDocument(id) {
  const doc = get(`SELECT * FROM document WHERE id=?`, [id]);
  if (!doc) return null;
  doc.items = all(`SELECT * FROM document_item WHERE document_id=? ORDER BY sort_order, id`, [id]).map((it) => {
    const p = it.product_id ? get(`SELECT id, color, image, unit FROM product WHERE id=?`, [it.product_id]) : null;
    return { ...it, color: p ? p.color : '#e2e8f0', image: p ? p.image : null, unit: p ? p.unit : '' };
  });
  doc.payments = all(
    `SELECT pay.*, pt.name AS payment_name, pt.kind AS payment_kind, pt.color AS payment_color
     FROM payment pay LEFT JOIN payment_type pt ON pt.id=pay.payment_type_id WHERE pay.document_id=?`,
    [id]
  );
  doc.customer = doc.customer_id ? get(`SELECT * FROM customer WHERE id=?`, [doc.customer_id]) : null;
  doc.user = doc.user_id ? get(`SELECT id, name FROM user WHERE id=?`, [doc.user_id]) : null;
  doc.register = doc.register_id ? get(`SELECT id, name FROM register WHERE id=?`, [doc.register_id]) : null;
  doc.tax_breakdown = doc.tax_breakdown ? JSON.parse(doc.tax_breakdown) : [];
  return doc;
}

function listDocuments(filters = {}) {
  const where = [];
  const params = [];
  if (filters.type) {
    where.push('d.type=?');
    params.push(filters.type);
  }
  if (filters.status) {
    where.push('d.status=?');
    params.push(filters.status);
  }
  if (filters.customer_id) {
    where.push('d.customer_id=?');
    params.push(filters.customer_id);
  }
  if (filters.user_id) {
    where.push('d.user_id=?');
    params.push(filters.user_id);
  }
  if (filters.register_id) {
    where.push('d.register_id=?');
    params.push(filters.register_id);
  }
  if (filters.from) {
    where.push('d.doc_date >= ?');
    params.push(filters.from);
  }
  if (filters.to) {
    where.push('d.doc_date <= ?');
    params.push(filters.to + 'T23:59:59');
  }
  if (filters.number) {
    where.push('d.number LIKE ?');
    params.push(`%${filters.number}%`);
  }
  if (filters.product_id) {
    where.push('EXISTS (SELECT 1 FROM document_item di WHERE di.document_id=d.id AND di.product_id=?)');
    params.push(filters.product_id);
  }
  const sql = `SELECT d.*, c.name AS customer_name, u.name AS user_name, r.name AS register_name
     FROM document d
     LEFT JOIN customer c ON c.id=d.customer_id
     LEFT JOIN user u ON u.id=d.user_id
     LEFT JOIN register r ON r.id=d.register_id
     ${where.length ? 'WHERE ' + where.join(' AND ') : ''}
     ORDER BY d.id DESC LIMIT ${Math.min(num(filters.limit, 500), 2000)}`;
  return all(sql, params);
}

/* ------------------------------------------------------------------ refund */

function refundSale(documentId, opts = {}) {
  return tx(() => {
    const original = getDocument(documentId);
    if (!original) throw new Error('Sale not found');
    if (original.type !== 'sale') throw new Error('Only sales can be refunded');

    const selected = (opts.item_ids || original.items.map((i) => i.id)).map(Number);
    const items = original.items.filter((i) => selected.includes(i.id) && !i.voided);
    if (!items.length) throw new Error('Select at least one item to refund');

    let subtotal = 0;
    let tax = 0;
    let total = 0;
    for (const it of items) {
      const share = original.total > 0 ? it.total / original.total : 0;
      subtotal += Math.round(it.quantity * it.unit_price);
      tax += Math.round(it.tax_total * share);
      total += it.total;
    }

    const number = nextNumber('R');
    run(
      `INSERT INTO document (number, type, status, customer_id, user_id, register_id, doc_date,
        stock_date, subtotal, item_discount, cart_discount, discount_total, tax_total, total,
        paid_total, change_total, notes, created_at, closed_at)
       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`,
      [
        number,
        'refund',
        'closed',
        original.customer_id,
        opts.user_id || original.user_id,
        original.register_id,
        nowLocal(),
        nowLocal(),
        -subtotal,
        0,
        0,
        0,
        -tax,
        -total,
        -total,
        0,
        opts.reason || `Refund of ${original.number}`,
        nowIso(),
        nowIso(),
      ]
    );
    const docId = get(`SELECT last_insert_rowid() AS id`).id;
    run(`UPDATE document SET original_document_id=? WHERE id=?`, [original.id, docId]);

    for (const it of items) {
      if (!it.product_id) continue;
      const p = get(`SELECT is_service FROM product WHERE id=?`, [it.product_id]);
      if (p && !p.is_service) {
        applyStock(it.product_id, it.quantity * num(it.unit_factor, 1), it.cost, docId, 'in');
      }
    }

    for (const it of items) {
      run(
        `INSERT INTO document_item (document_id, product_id, name_snapshot, sku_snapshot, quantity,
          unit_price, unit_snapshot, unit_factor, cost, discount_type, discount_value, discount_total,
          tax_total, total, voided, sort_order)
         VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?)`,
        [
          docId,
          it.product_id,
          it.name_snapshot,
          it.sku_snapshot,
          it.quantity,
          it.unit_price,
          it.unit_snapshot,
          num(it.unit_factor, 1),
          it.cost,
          it.discount_type,
          it.discount_value,
          it.discount_total,
          -it.tax_total,
          -it.total,
          it.sort_order,
        ]
      );
    }

    const payType = opts.payment_type_id || (get(`SELECT id FROM payment_type WHERE kind='cash' AND active=1`) || {}).id;
    run(`INSERT INTO payment (document_id, payment_type_id, amount, tendered, change, reference) VALUES (?,?,?,?,?,?)`, [
      docId,
      payType,
      -total,
      -total,
      0,
      opts.reference || null,
    ]);

    return getDocument(docId);
  });
}

/* --------------------------------------------------------------- documents */

const DOC_TYPES = {
  purchase: { label: 'Purchase', prefix: 'PO', stock: 'in' },
  stock_return: { label: 'Stock return', prefix: 'SR', stock: 'out' },
  count: { label: 'Inventory count', prefix: 'IC', stock: 'set' },
  loss: { label: 'Loss & damage', prefix: 'LD', stock: 'out' },
  proforma: { label: 'Proforma', prefix: 'QT', stock: 'none' },
  credit: { label: 'Credit payment', prefix: 'CR', stock: 'none' },
};

function prepareDocumentItems(type, items) {
  let subtotal = 0;
  let total = 0;
  const prepared = [];
  for (const it of items || []) {
    const p = getProduct(it.product_id);
    if (!p) throw new Error(`Product ${it.product_id} not found`);
    const qty = num(it.quantity, 1);
    if (qty <= 0) throw new Error(`Invalid quantity for ${p.name}`);
    const unit = resolveUnit(p, it.unit);
    const factor = unitFactor(p, unit.name);
    const baseQty = qty * factor;
    const sells = type === 'proforma' || type === 'sale' || type === 'refund';
    const defaultPrice = sells ? unitPriceFor(p, unit) : Math.round(num(p.cost) * factor);
    const price = it.unit_price != null ? cents(it.unit_price) : defaultPrice;
    const gross = Math.round(qty * price);
    subtotal += gross;
    total += gross;
    prepared.push({ p, qty, baseQty, unit, factor, unitPrice: price, gross });
  }
  return { prepared, subtotal, total };
}

function stockDirectionFor(type) {
  if (type === 'sale') return 'out';
  if (type === 'refund') return 'in';
  return (DOC_TYPES[type] || {}).stock || 'none';
}

function stockUnitCost(type, p, unitPrice, factor) {
  if (type === 'purchase') return Math.round(num(unitPrice) / (num(factor) || 1));
  return num(p.cost);
}

function applyPreparedStock(docId, prepared, type) {
  const direction = stockDirectionFor(type);
  for (const item of prepared) {
    const { p, baseQty, factor, unitPrice } = item;
    const unitCost = stockUnitCost(type, p, unitPrice, factor);
    if (direction === 'in') applyStock(p.id, baseQty, unitCost, docId, 'in');
    else if (direction === 'out') applyStock(p.id, -baseQty, unitCost, docId, 'out');
    else if (direction === 'set') {
      const before = get(`SELECT quantity FROM stock_item WHERE product_id=?`, [p.id]);
      const delta = baseQty - num(before ? before.quantity : 0);
      applyStock(p.id, delta, p.cost, docId, 'adjust');
    }
  }
}

// Puts back (or takes back) every stock movement recorded for a document.
function reverseDocumentStock(docId) {
  const moves = all(`SELECT * FROM stock_movement WHERE document_id=? ORDER BY id`, [docId]);
  if (moves.length) {
    for (const m of moves) applyStock(m.product_id, -num(m.quantity), m.unit_cost, null, 'void');
    return;
  }
  // Legacy refunds recorded their stock movement without a document id.
  const doc = get(`SELECT type FROM document WHERE id=?`, [docId]);
  if (!doc || doc.type !== 'refund') return;
  const items = all(
    `SELECT product_id, quantity, COALESCE(unit_factor, 1) AS unit_factor
     FROM document_item WHERE document_id=? AND voided=0`,
    [docId]
  );
  for (const it of items) {
    if (!it.product_id) continue;
    applyStock(it.product_id, -num(it.quantity) * num(it.unit_factor, 1), 0, null, 'void');
  }
}

function insertDocumentItems(docId, prepared, type) {
  const direction = stockDirectionFor(type);
  const sign = type === 'refund' ? -1 : 1;
  for (let i = 0; i < prepared.length; i++) {
    const { p, qty, unit, factor, unitPrice, gross } = prepared[i];
    run(
      `INSERT INTO document_item (document_id, product_id, name_snapshot, sku_snapshot, quantity,
        unit_price, unit_snapshot, unit_factor, cost, discount_type, discount_value, discount_total,
        tax_total, total, voided, sort_order)
       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?)`,
      [docId, p.id, p.name, p.code, qty, unitPrice, unit.name, factor, p.cost, null, 0, 0, 0, sign * gross, i + 1]
    );
    if (direction === 'none') continue;
  }
}

function insertSaleLines(docId, lines) {
  for (const l of lines) {
    run(
      `INSERT INTO document_item (document_id, product_id, name_snapshot, sku_snapshot, quantity,
        unit_price, unit_snapshot, unit_factor, cost, discount_type, discount_value, discount_total,
        tax_total, total, voided, sort_order)
       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?)`,
      [
        docId,
        l.product_id,
        l.name_snapshot,
        l.sku_snapshot,
        l.quantity,
        l.unit_price,
        l.unit_snapshot,
        l.unit_factor,
        l.cost,
        l.discount_type,
        l.discount_value,
        Math.round(l.discount_total),
        l.tax_total,
        l.total,
        l.sort_order,
      ]
    );
  }
}

// Keeps the money side consistent when a document total changes. Cash tendered
// stays as it was; the difference becomes change (or an open balance).
function recalculatePayments(docId, type, newTotal) {
  const pays = all(`SELECT * FROM payment WHERE document_id=? ORDER BY id`, [docId]);
  if (!pays.length) return { paidTotal: 0, changeTotal: 0 };

  if (type === 'refund') {
    const p = pays[0];
    run(`UPDATE payment SET amount=?, tendered=?, change=0 WHERE id=?`, [-newTotal, -newTotal, p.id]);
    for (let i = 1; i < pays.length; i++) run(`DELETE FROM payment WHERE id=?`, [pays[i].id]);
    return { paidTotal: -newTotal, changeTotal: 0 };
  }

  if (type !== 'sale') {
    const p = pays[0];
    run(`UPDATE payment SET amount=?, tendered=?, change=0 WHERE id=?`, [newTotal, newTotal, p.id]);
    for (let i = 1; i < pays.length; i++) run(`DELETE FROM payment WHERE id=?`, [pays[i].id]);
    return { paidTotal: newTotal, changeTotal: 0 };
  }

  const tenderedTotal = pays.reduce((s, p) => s + num(p.tendered), 0);
  const oldPaid = pays.reduce((s, p) => s + num(p.amount), 0);

  if (newTotal <= tenderedTotal) {
    let allocated = 0;
    for (let i = 0; i < pays.length; i++) {
      const p = pays[i];
      let amount;
      if (i === pays.length - 1) amount = newTotal - allocated;
      else {
        amount = oldPaid > 0 ? Math.round((num(p.amount) / oldPaid) * newTotal) : 0;
        allocated += amount;
      }
      run(`UPDATE payment SET amount=?, change=0 WHERE id=?`, [amount, p.id]);
    }
    const change = Math.max(tenderedTotal - newTotal, 0);
    if (change > 0) {
      const last = pays[pays.length - 1];
      run(`UPDATE payment SET change=? WHERE id=?`, [change, last.id]);
    }
    return { paidTotal: newTotal, changeTotal: change };
  }

  for (const p of pays) {
    run(`UPDATE payment SET amount=?, change=0 WHERE id=?`, [num(p.tendered), p.id]);
  }
  return { paidTotal: tenderedTotal, changeTotal: 0 };
}

function createDocument(payload) {
  return tx(() => {
    const type = payload.type;
    const def = DOC_TYPES[type];
    if (!def) throw new Error('Unknown document type');

    const docDate = payload.doc_date || nowLocal();
    const stockDate = payload.stock_date || docDate;
    if (type === 'count' && new Date(stockDate).getTime() > Date.now()) {
      throw new Error('Stock date for an inventory count cannot be in the future');
    }

    const items = payload.items || [];
    if (!items.length) throw new Error('Add at least one product');

    const { prepared, subtotal, total } = prepareDocumentItems(type, items);
    const tax = 0;

    const number = nextNumber(def.prefix);
    run(
      `INSERT INTO document (number, type, status, supplier_id, customer_id, user_id, register_id,
        external_number, doc_date, stock_date, subtotal, discount_total, tax_total, total, paid_total,
        notes, created_at, closed_at)
       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`,
      [
        number,
        type,
        'closed',
        payload.supplier_id || null,
        payload.customer_id || null,
        payload.user_id || null,
        payload.register_id || null,
        payload.external_number || null,
        docDate,
        stockDate,
        subtotal,
        0,
        tax,
        total,
        total,
        payload.notes || '',
        nowIso(),
        nowIso(),
      ]
    );
    const docId = get(`SELECT last_insert_rowid() AS id`).id;

    insertDocumentItems(docId, prepared, type);
    applyPreparedStock(docId, prepared, type);

    if (payload.payments && payload.payments.length) {
      for (const pay of payload.payments) {
        run(`INSERT INTO payment (document_id, payment_type_id, amount, tendered, change, reference) VALUES (?,?,?,?,?,?)`, [
          docId,
          pay.payment_type_id,
          cents(pay.amount),
          cents(pay.amount),
          0,
          pay.reference || null,
        ]);
      }
    }

    return getDocument(docId);
  });
}

function deleteDocument(documentId, opts = {}) {
  const doc = getDocument(documentId);
  if (!doc) return { ok: false, error: 'Document not found' };
  if (!hasPermission(opts.user_id, 'documents.delete')) {
    return { ok: false, error: 'You do not have permission to delete documents.' };
  }
  const reason = String(opts.reason || '').trim();

  if (doc.type === 'sale') {
    const refund = get(
      `SELECT id FROM document WHERE type='refund' AND (original_document_id=? OR notes LIKE ?) LIMIT 1`,
      [doc.id, `%${doc.number}%`]
    );
    if (refund) return { ok: false, error: 'This sale already has a refund. Delete the refund first.' };
  }

  let moves = all(`SELECT * FROM stock_movement WHERE document_id=?`, [doc.id]);
  if (!moves.length && doc.type === 'refund') {
    // Legacy refunds recorded their restock without a document id.
    const items = all(
      `SELECT product_id, quantity, COALESCE(unit_factor, 1) AS unit_factor
       FROM document_item WHERE document_id=? AND voided=0`,
      [doc.id]
    );
    moves = items
      .filter((it) => it.product_id)
      .map((it) => ({ product_id: it.product_id, quantity: -num(it.quantity) * num(it.unit_factor, 1), unit_cost: 0 }));
  }
  const allowNegative = getSetting('allow_negative_stock') === '1';
  const deltas = {};
  for (const m of moves) deltas[m.product_id] = num(deltas[m.product_id]) - num(m.quantity);
  for (const key of Object.keys(deltas)) {
    const delta = deltas[key];
    if (delta < 0) {
      const row = get(`SELECT quantity FROM stock_item WHERE product_id=?`, [Number(key)]);
      if (!allowNegative && num(row && row.quantity) + delta < -1e-9) {
        return { ok: false, error: 'Deleting this document would make stock negative. Adjust stock first.' };
      }
    }
  }

  tx(() => {
    for (const m of moves) applyStock(m.product_id, -num(m.quantity), m.unit_cost, null, 'void');
    run(
      `INSERT INTO deleted_document (number, type, total, user_id, reason, items_json, payments_json, deleted_at)
       VALUES (?,?,?,?,?,?,?,?)`,
      [
        doc.number,
        doc.type,
        doc.total,
        opts.user_id || null,
        reason,
        JSON.stringify(doc.items || []),
        JSON.stringify(doc.payments || []),
        nowIso(),
      ]
    );
    run(`DELETE FROM payment WHERE document_id=?`, [doc.id]);
    run(`DELETE FROM document_item WHERE document_id=?`, [doc.id]);
    run(`DELETE FROM document WHERE id=?`, [doc.id]);
  });
  persist();
  return { ok: true };
}

function updateDocument(id, payload) {
  const original = getDocument(id);
  if (!original) return { ok: false, error: 'Document not found' };
  if (!hasPermission(payload.user_id, 'documents.edit')) {
    return { ok: false, error: 'You do not have permission to edit documents.' };
  }
  const type = original.type;
  const items = payload.items || [];
  if (!items.length) throw new Error('Add at least one product');
  const docDate = payload.doc_date || original.doc_date;
  const stockDate = payload.stock_date || docDate;
  if (type === 'count' && new Date(stockDate).getTime() > Date.now()) {
    throw new Error('Stock date for an inventory count cannot be in the future');
  }

  if (type === 'sale') {
    const refund = get(
      `SELECT id FROM document WHERE type='refund' AND (original_document_id=? OR notes LIKE ?) LIMIT 1`,
      [original.id, `%${original.number}%`]
    );
    if (refund) return { ok: false, error: 'This sale already has a refund. Delete the refund first.' };
    const cartDiscount =
      num(original.cart_discount) > 0 ? { type: 'fixed', value: num(original.cart_discount) / 100 } : null;
    // The editor does not show item discounts, so carry them over by product.
    const oldLines = original.items || [];
    const saleItems = items.map((it, index) => {
      const old = oldLines.find((o) => String(o.product_id) === String(it.product_id)) || oldLines[index];
      if (!old) return it;
      return {
        ...it,
        discount_type: old.discount_type,
        discount_value: old.discount_value,
      };
    });
    return tx(() => {
      reverseDocumentStock(id);
      run(`DELETE FROM stock_movement WHERE document_id=?`, [id]);
      run(`DELETE FROM document_item WHERE document_id=?`, [id]);
      const calc = computeSale({
        items: saleItems,
        customer_id: payload.customer_id != null ? payload.customer_id : original.customer_id,
        discount: cartDiscount,
        tax_override_percent: original.tax_override_percent,
      });
      insertSaleLines(id, calc.lines);
      for (const l of calc.lines) {
        if (!l.is_service) applyStock(l.product_id, -l.quantity * l.unit_factor, l.cost, id, 'out');
      }
      const pay = recalculatePayments(id, 'sale', calc.total);
      let status = 'closed';
      if (pay.paidTotal + 1e-9 < calc.total) status = pay.paidTotal === 0 ? 'open' : 'partial';
      run(
        `UPDATE document SET customer_id=?, external_number=?, doc_date=?, stock_date=?, subtotal=?,
          item_discount=?, cart_discount=?, discount_total=?, tax_override_percent=?, tax_total=?,
          tax_breakdown=?, total=?, paid_total=?, change_total=?, notes=?, status=?, closed_at=?
         WHERE id=?`,
        [
          payload.customer_id != null ? payload.customer_id : original.customer_id,
          payload.external_number || null,
          docDate,
          stockDate,
          calc.subtotal,
          calc.item_discount,
          calc.cart_discount,
          calc.discount_total,
          original.tax_override_percent,
          calc.tax_total,
          JSON.stringify(calc.tax_breakdown),
          calc.total,
          pay.paidTotal,
          pay.changeTotal,
          payload.notes || '',
          status,
          nowIso(),
          id,
        ]
      );
      return getDocument(id);
    });
  }

  return tx(() => {
    reverseDocumentStock(id);
    run(`DELETE FROM stock_movement WHERE document_id=?`, [id]);
    run(`DELETE FROM document_item WHERE document_id=?`, [id]);
    const { prepared, subtotal, total } = prepareDocumentItems(type, items);
    const direction = stockDirectionFor(type);
    if (direction === 'in' && getSetting('allow_negative_stock') !== '1') {
      for (const item of prepared) {
        const row = get(`SELECT quantity FROM stock_item WHERE product_id=?`, [item.p.id]);
        if (num(row && row.quantity) < item.baseQty - 1e-9) {
          throw new Error(`Not enough stock to reverse this document for ${item.p.name}`);
        }
      }
    }
    insertDocumentItems(id, prepared, type);
    applyPreparedStock(id, prepared, type);
    const pay = recalculatePayments(id, type, total);
    const signedTotal = type === 'refund' ? -total : total;
    run(
      `UPDATE document SET supplier_id=?, customer_id=?, external_number=?, doc_date=?, stock_date=?,
        subtotal=?, tax_total=0, total=?, paid_total=?, change_total=?, notes=?, status='closed', closed_at=?
       WHERE id=?`,
      [
        payload.supplier_id || null,
        payload.customer_id || null,
        payload.external_number || null,
        docDate,
        stockDate,
        type === 'refund' ? -subtotal : subtotal,
        signedTotal,
        pay.paidTotal,
        pay.changeTotal,
        payload.notes || '',
        nowIso(),
        id,
      ]
    );
    return getDocument(id);
  });
}

/* -------------------------------------------------------------------- cash */

function listCashMovements(registerId) {
  return all(
    `SELECT cm.*, u.name AS user_name FROM cash_movement cm LEFT JOIN user u ON u.id=cm.user_id
     WHERE cm.zreport_id IS NULL ${registerId ? 'AND cm.register_id=?' : ''} ORDER BY cm.id DESC`,
    registerId ? [registerId] : []
  );
}

function addCash(kind, amount, reason, userId, registerId) {
  const amt = cents(amount);
  if (!(amt > 0)) return { ok: false, error: 'Enter an amount greater than zero' };
  if (!reason || !String(reason).trim()) return { ok: false, error: 'A reason is required' };
  run(
    `INSERT INTO cash_movement (register_id, user_id, kind, amount, reason, occurred_at) VALUES (?,?,?,?,?,?)`,
    [registerId || null, userId || null, kind, amt, reason, nowLocal()]
  );
  persist();
  return { ok: true };
}

/* ---------------------------------------------------------------- day / Z */

function getDayState() {
  return {
    business_date: getSetting('business_date', today()),
    register_open: getSetting('register_open') === '1',
    opened_at: getSetting('opened_at'),
    opening_cash: Math.round(num(getSetting('opening_cash', 0))),
    opened_by: getSetting('opened_by'),
  };
}

function openDay(openingCash, userId, businessDate) {
  if (getSetting('register_open') === '1') return { ok: false, error: 'Register is already open' };
  run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('register_open','1')`);
  setSetting('opening_cash', String(cents(openingCash)));
  setSetting('opened_at', nowIso());
  setSetting('opened_by', String(userId || ''));
  setSetting('business_date', businessDate || today());
  persist();
  return { ok: true, state: getDayState() };
}

function closeDayPreview() {
  const state = getDayState();
  const openSales = get(`SELECT COUNT(*) AS c FROM document WHERE type='sale' AND status='open'`);
  return { open_sales: openSales.c, state, summary: daySummary(state.business_date) };
}

function daySummary(date) {
  const sales = get(
    `SELECT COUNT(*) AS n, COALESCE(SUM(total),0) AS total, COALESCE(SUM(discount_total),0) AS discount,
        COALESCE(SUM(tax_total),0) AS tax, COALESCE(SUM(subtotal),0) AS subtotal
     FROM document WHERE type='sale' AND doc_date LIKE ?`,
    [`${date}%`]
  );
  const refunds = get(
    `SELECT COUNT(*) AS n, COALESCE(SUM(total),0) AS total, COALESCE(SUM(tax_total),0) AS tax
     FROM document WHERE type='refund' AND doc_date LIKE ?`,
    [`${date}%`]
  );
  const cashIn = get(
    `SELECT COALESCE(SUM(amount),0) AS s FROM cash_movement WHERE kind='in' AND zreport_id IS NULL AND occurred_at LIKE ?`,
    [`${date}%`]
  );
  const cashOut = get(
    `SELECT COALESCE(SUM(amount),0) AS s FROM cash_movement WHERE kind='out' AND zreport_id IS NULL AND occurred_at LIKE ?`,
    [`${date}%`]
  );

  const tenders = all(
    `SELECT pt.id, pt.name, pt.kind, pt.color,
        COALESCE(SUM(CASE WHEN d.type='sale' THEN p.amount ELSE 0 END),0) AS sales_amount,
        COALESCE(SUM(CASE WHEN d.type='refund' THEN p.amount ELSE 0 END),0) AS refund_amount
     FROM payment p
     JOIN document d ON d.id=p.document_id
     LEFT JOIN payment_type pt ON pt.id=p.payment_type_id
     WHERE d.doc_date LIKE ? AND d.type IN ('sale','refund')
     GROUP BY pt.id`,
    [`${date}%`]
  ).map((t) => ({ ...t, net: t.sales_amount + t.refund_amount }));

  const items = all(
    `SELECT di.name_snapshot AS name,
        SUM(CASE WHEN d.type='sale' THEN di.quantity * COALESCE(di.unit_factor,1) ELSE 0 END) AS qty_sold,
        SUM(CASE WHEN d.type='refund' THEN di.quantity * COALESCE(di.unit_factor,1) ELSE 0 END) AS qty_returned,
        SUM(CASE WHEN d.type='sale' THEN di.total ELSE 0 END) AS sales_total,
        SUM(CASE WHEN d.type='refund' THEN di.total ELSE 0 END) AS refund_total
     FROM document_item di JOIN document d ON d.id=di.document_id
     WHERE d.doc_date LIKE ? AND d.type IN ('sale','refund')
     GROUP BY di.product_id, di.name_snapshot
     ORDER BY sales_total DESC`,
    [`${date}%`]
  );

  const cashSales = tenders.filter((t) => t.kind === 'cash').reduce((s, t) => s + t.net, 0);
  const cashExpected = Math.round(num(getSetting('opening_cash', 0))) + cashSales + num(cashIn.s) - num(cashOut.s);

  return {
    date,
    sales_count: num(sales.n),
    returns_count: num(refunds.n),
    subtotal: num(sales.subtotal),
    discount_total: num(sales.discount),
    tax_total: num(sales.tax) + num(refunds.tax),
    sales_total: num(sales.total),
    refunds_total: num(refunds.total),
    net_total: num(sales.total) + num(refunds.total),
    cash_in: num(cashIn.s),
    cash_out: num(cashOut.s),
    cash_expected: cashExpected,
    tenders,
    items,
  };
}

function closeDay(userId) {
  const state = getDayState();
  if (!state.register_open) return { ok: false, error: 'Register is not open' };
  const openSales = get(`SELECT COUNT(*) AS c FROM document WHERE type='sale' AND status='open'`);
  if (openSales.c > 0) {
    return { ok: false, error: `${openSales.c} open sale(s) must be closed first` };
  }

  return tx(() => {
    const summary = daySummary(state.business_date);
    const number = nextNumber('Z');
    run(
      `INSERT INTO zreport (number, register_id, opened_at, closed_at, opened_by, closed_by,
        opening_cash, cash_in, cash_out, tendered_json, sales_count, returns_count, discount_total,
        tax_total, total, items_json, cash_expected)
       VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`,
      [
        number,
        get(`SELECT id FROM register WHERE is_primary=1`).id,
        state.opened_at,
        nowIso(),
        state.opened_by ? Number(state.opened_by) : null,
        userId || null,
        state.opening_cash,
        summary.cash_in,
        summary.cash_out,
        JSON.stringify(summary.tenders),
        summary.sales_count,
        summary.returns_count,
        summary.discount_total,
        summary.tax_total,
        summary.net_total,
        JSON.stringify(summary.items),
        summary.cash_expected,
      ]
    );
    const zid = get(`SELECT last_insert_rowid() AS id`).id;
    run(`UPDATE cash_movement SET zreport_id=? WHERE zreport_id IS NULL`, [zid]);
    run(`INSERT OR REPLACE INTO setting (key, value) VALUES ('register_open','0')`);
    const next = new Date(`${state.business_date}T12:00:00`);
    next.setDate(next.getDate() + 1);
    setSetting('business_date', `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, '0')}-${String(next.getDate()).padStart(2, '0')}`);
    return { ok: true, zreport: getZReport(zid), summary };
  });
}

function getZReport(id) {
  const z = get(`SELECT * FROM zreport WHERE id=?`, [id]);
  if (z) {
    z.tenders = z.tendered_json ? JSON.parse(z.tendered_json) : [];
    z.items = z.items_json ? JSON.parse(z.items_json) : [];
  }
  return z;
}

function listZReports() {
  return all(`SELECT * FROM zreport ORDER BY id DESC`).map((z) => ({
    ...z,
    tenders: z.tendered_json ? JSON.parse(z.tendered_json) : [],
    items: z.items_json ? JSON.parse(z.items_json) : [],
  }));
}

function xReport() {
  const state = getDayState();
  return daySummary(state.business_date);
}

/* -------------------------------------------------------------- dashboard */

function dashboard() {
  const date = today();
  const todayS = daySummary(date);
  const week = [];
  for (let i = 6; i >= 0; i--) {
    const d = new Date(`${date}T12:00:00`);
    d.setDate(d.getDate() - i);
    const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const s = get(
      `SELECT COALESCE(SUM(total),0) AS total, COUNT(*) AS n FROM document WHERE type='sale' AND doc_date LIKE ?`,
      [`${key}%`]
    );
    week.push({ date: key, total: num(s.total), count: num(s.n) });
  }

  const top = all(
    `SELECT di.name_snapshot AS name, SUM(di.quantity * COALESCE(di.unit_factor,1)) AS qty, SUM(di.total) AS total
     FROM document_item di JOIN document d ON d.id=di.document_id
     WHERE d.type='sale' AND d.doc_date LIKE ?
     GROUP BY di.product_id, di.name_snapshot ORDER BY qty DESC LIMIT 5`,
    [`${date}%`]
  );

  const low = all(
    `SELECT p.id, p.name, p.code, p.low_stock, COALESCE(s.quantity,0) AS quantity
     FROM product p LEFT JOIN stock_item s ON s.product_id=p.id
     WHERE p.active=1 AND p.is_service=0 AND p.low_stock>0 AND COALESCE(s.quantity,0)<=p.low_stock
     ORDER BY (COALESCE(s.quantity,0)*1.0)/(p.low_stock*1.0) ASC LIMIT 8`
  );

  const stockValue = get(
    `SELECT COALESCE(SUM(s.quantity * s.avg_cost),0) AS value FROM stock_item s`
  );

  return {
    today: todayS,
    week,
    top,
    low,
    stock_value: num(stockValue.value),
    tenders: todayS.tenders,
    doc_count: num(get(`SELECT COUNT(*) AS c FROM document`).c),
    product_count: num(get(`SELECT COUNT(*) AS c FROM product`).c),
    customer_count: num(get(`SELECT COUNT(*) AS c FROM customer WHERE type='customer'`).c),
  };
}

/* ---------------------------------------------------------------- reports */

function report(name, filters = {}) {
  const from = filters.from || `${today()}T00:00:00`;
  const to = filters.to ? `${filters.to}T23:59:59` : `${today()}T23:59:59`;
  const dateLikeFrom = from.slice(0, 10);
  const dateLikeTo = to.slice(0, 10);

  if (name === 'sales_summary') {
    return all(
      `SELECT substr(doc_date,1,10) AS date, COUNT(*) AS sales,
          SUM(subtotal) AS subtotal, SUM(discount_total) AS discount,
          SUM(tax_total) AS tax, SUM(total) AS total
       FROM document WHERE type='sale' AND doc_date BETWEEN ? AND ?
       GROUP BY substr(doc_date,1,10) ORDER BY date DESC`,
      [from, to]
    );
  }

  if (name === 'product_sales') {
    return all(
      `SELECT di.name_snapshot AS name, di.sku_snapshot AS sku,
          SUM(CASE WHEN d.type='sale' THEN di.quantity * COALESCE(di.unit_factor,1) ELSE 0 END) AS qty_sold,
          SUM(CASE WHEN d.type='refund' THEN di.quantity * COALESCE(di.unit_factor,1) ELSE 0 END) AS qty_returned,
          SUM(di.total) AS net_total,
          SUM(di.quantity * COALESCE(di.unit_factor,1) * di.cost) AS cost_total
       FROM document_item di JOIN document d ON d.id=di.document_id
       WHERE d.doc_date BETWEEN ? AND ? AND d.type IN ('sale','refund')
       GROUP BY di.product_id, di.name_snapshot
       ORDER BY net_total DESC`,
      [from, to]
    );
  }

  if (name === 'payment_types') {
    return all(
      `SELECT pt.name, pt.kind,
          SUM(CASE WHEN d.type='sale' THEN p.amount ELSE 0 END) AS sales,
          SUM(CASE WHEN d.type='refund' THEN p.amount ELSE 0 END) AS refunds,
          COUNT(*) AS txns
       FROM payment p JOIN document d ON d.id=p.document_id
       LEFT JOIN payment_type pt ON pt.id=p.payment_type_id
       WHERE d.doc_date BETWEEN ? AND ? AND d.type IN ('sale','refund')
       GROUP BY pt.id ORDER BY sales DESC`,
      [from, to]
    );
  }

  if (name === 'tax_summary') {
    return all(
      `SELECT substr(doc_date,1,10) AS date, SUM(tax_total) AS tax, SUM(total) AS total
       FROM document WHERE type='sale' AND doc_date BETWEEN ? AND ?
       GROUP BY substr(doc_date,1,10) ORDER BY date DESC`,
      [from, to]
    );
  }

  if (name === 'customer_sales') {
    return all(
      `SELECT c.name AS customer, COUNT(d.id) AS sales, SUM(d.total) AS total
       FROM document d LEFT JOIN customer c ON c.id=d.customer_id
       WHERE d.type='sale' AND d.doc_date BETWEEN ? AND ?
       GROUP BY d.customer_id ORDER BY total DESC`,
      [from, to]
    );
  }

  if (name === 'stock_valuation') {
    return all(
      `SELECT p.name, p.code, p.unit, g.name AS group_name,
          COALESCE(s.quantity,0) AS quantity, COALESCE(s.avg_cost,0) AS avg_cost,
          COALESCE(s.quantity,0) * COALESCE(s.avg_cost,0) AS value
       FROM product p LEFT JOIN product_group g ON g.id=p.group_id
       LEFT JOIN stock_item s ON s.product_id=p.id
       WHERE p.active=1 ORDER BY value DESC`
    );
  }

  if (name === 'low_stock') {
    return all(
      `SELECT p.name, p.code, p.low_stock, COALESCE(s.quantity,0) AS quantity
       FROM product p LEFT JOIN stock_item s ON s.product_id=p.id
       WHERE p.active=1 AND p.is_service=0 AND p.low_stock>0 AND COALESCE(s.quantity,0)<=p.low_stock
       ORDER BY quantity ASC`
    );
  }

  if (name === 'z_reports') {
    return all(`SELECT id, number, closed_at, opening_cash, total, sales_count, returns_count FROM zreport ORDER BY id DESC`);
  }

  if (name === 'x_report') {
    return daySummary(today());
  }

  return [];
}

/* ----------------------------------------------------------------- backup */

function backup(destPath) {
  persist();
  fs.copyFileSync(dbPath, destPath);
  return { ok: true, path: destPath };
}

function restore(srcPath) {
  const check = inspectDatabaseFile(srcPath);
  if (!check.ok) return { ok: false, error: check.error, kind: check.kind };
  const buf = fs.readFileSync(srcPath);
  db = new SQL.Database(buf);
  db.run(SCHEMA);
  migrate();
  persist();
  return { ok: true };
}

function factoryReset() {
  // Clear business data but keep the app usable: users, settings, company,
  // tax rates, payment types and registers stay.
  tx(() => {
    for (const table of [
      'document_item',
      'payment',
      'document',
      'stock_movement',
      'stock_item',
      'product_unit',
      'product_tax',
      'product_comment',
      'barcode',
      'product',
      'loyalty_card',
      'customer',
      'product_group',
      'zreport',
      'cash_movement',
      'number_seq',
    ]) {
      run(`DELETE FROM ${table}`);
    }
    run(
      `INSERT INTO setting (key, value) VALUES ('sample_data', '0')
       ON CONFLICT(key) DO UPDATE SET value = excluded.value`
    );
  });
  persist();
  return { ok: true };
}

/* ------------------------------------------------------------------ stats */

function meta() {
  const recovery = lastRecovery;
  lastRecovery = null;
  return {
    db_path: dbPath,
    recovery,
    counts: {
      products: num(get(`SELECT COUNT(*) AS c FROM product`).c),
      documents: num(get(`SELECT COUNT(*) AS c FROM document`).c),
      customers: num(get(`SELECT COUNT(*) AS c FROM customer`).c),
      users: num(get(`SELECT COUNT(*) AS c FROM user`).c),
    },
    version: '1.0.0',
  };
}

/* ------------------------------------------------------- Aronium import */

function sqliteTable(src, name) {
  const stmt = src.prepare(`SELECT name FROM sqlite_master WHERE type='table' AND lower(name)=?`);
  stmt.bind([String(name).toLowerCase()]);
  let found = false;
  try {
    while (stmt.step()) {
      found = true;
      break;
    }
  } finally {
    stmt.free();
  }
  return found;
}

function sqliteRows(src, table) {
  const stmt = src.prepare(`SELECT * FROM "${String(table).replace(/"/g, '""')}"`);
  const out = [];
  try {
    while (stmt.step()) out.push(stmt.getAsObject());
  } finally {
    stmt.free();
  }
  return out;
}

function importBlob(value) {
  if (!value) return null;
  if (typeof value === 'string') {
    const s = value.trim();
    return /^(data:|https?:\/\/)/i.test(s) ? s : null;
  }
  const bytes = value instanceof Uint8Array ? value : new Uint8Array(value);
  if (!bytes.length) return null;
  let mime = 'image/png';
  if (bytes[0] === 0xff && bytes[1] === 0xd8) mime = 'image/jpeg';
  else if (bytes[0] === 0x47 && bytes[1] === 0x49) mime = 'image/gif';
  else if (bytes[0] === 0x52 && bytes[1] === 0x49 && bytes[8] === 0x57) mime = 'image/webp';
  return `data:${mime};base64,${Buffer.from(bytes).toString('base64')}`;
}

function importColor(value) {
  const s = String(value == null ? '' : value).trim();
  if (/^#[0-9a-f]{3,8}$/i.test(s)) return s;
  const named = {
    transparent: '#e2e8f0',
    red: '#fee2e2',
    green: '#dcfce7',
    blue: '#dbeafe',
    yellow: '#fef3c7',
    orange: '#ffedd5',
    purple: '#ede9fe',
    gray: '#e2e8f0',
    grey: '#e2e8f0',
  };
  return named[s.toLowerCase()] || '#e2e8f0';
}

function importAddress(row) {
  return [row.StreetName, row.BuildingNumber, row.City, row.PostalCode]
    .map((v) => String(v == null ? '' : v).trim())
    .filter(Boolean)
    .join(', ');
}

// Reads an Aronium Lite database (pos.db, SQLite) and copies the catalogue,
// stock and contacts into Aroklon. Aronium Pro stores data in SQL Server and
// cannot be read by this importer.
function importAronium(filePath, options = {}) {
  const opts = {
    products: options.products !== false,
    customers: options.customers !== false,
    paymentTypes: options.paymentTypes !== false,
    company: options.company !== false,
    replace: options.replace === true,
  };
  const summary = {
    groups: 0,
    taxes: 0,
    products: 0,
    products_skipped: 0,
    customers: 0,
    customers_skipped: 0,
    loyalty: 0,
    payment_types: 0,
    stock: 0,
    company: false,
    replaced: false,
    warnings: [],
  };

  let src;
  try {
    src = new SQL.Database(fs.readFileSync(filePath));
  } catch (e) {
    return { ok: false, error: 'Could not read the database file: ' + (e.message || String(e)) };
  }

  let closed = false;
  const close = () => {
    if (closed) return;
    closed = true;
    try {
      src.close();
    } catch (e) {}
  };

  try {
    if (!sqliteTable(src, 'Product') || !sqliteTable(src, 'ProductGroup')) {
      return {
        ok: false,
        error:
          'This is not an Aronium Lite database. Aronium Pro stores data on SQL Server and cannot be imported directly.',
      };
    }

    const groupIdMap = {};
    const taxIdMap = {};
    const productIdMap = {};
    const customerIdMap = {};

    tx(() => {
      /* optional clean slate: only safe when no sales history exists */
      if (opts.replace) {
        const docCount = num((get(`SELECT COUNT(*) AS c FROM document`) || {}).c);
        if (docCount > 0) {
          throw new Error('The database already has sales documents, so the catalogue cannot be replaced. Use merge instead.');
        }
        for (const table of [
          'document_item',
          'payment',
          'product_tax',
          'barcode',
          'product_comment',
          'stock_movement',
          'stock_item',
          'product',
          'loyalty_card',
          'customer',
          'product_group',
          'tax_rate',
        ]) {
          run(`DELETE FROM ${table}`);
        }
        summary.replaced = true;
      }

      /* groups */
      if (opts.products) {
        const groups = sqliteRows(src, 'ProductGroup');
        const sorted = groups.slice().sort((a, b) => {
          const pa = a.ParentGroupId == null ? 0 : 1;
          const pb = b.ParentGroupId == null ? 0 : 1;
          if (pa !== pb) return pa - pb;
          return num(a.Rank) - num(b.Rank) || String(a.Name || '').localeCompare(String(b.Name || ''));
        });
        sorted.forEach((g, i) => {
          const parentNew = g.ParentGroupId == null ? null : groupIdMap[g.ParentGroupId] || null;
          const existing = all(`SELECT id FROM product_group WHERE lower(name)=lower(?)`, [String(g.Name || '')]).find(
            (r) => (r.parent_id == null ? null : r.parent_id) === (parentNew == null ? null : parentNew)
          );
          if (existing) {
            groupIdMap[g.Id] = existing.id;
            return;
          }
          run(`INSERT INTO product_group (name, parent_id, color, sort_order) VALUES (?,?,?,?)`, [
            String(g.Name || 'Group'),
            parentNew,
            importColor(g.Color),
            i,
          ]);
          groupIdMap[g.Id] = get(`SELECT last_insert_rowid() AS id`).id;
          summary.groups++;
        });
      }

      /* taxes */
      if (opts.products && sqliteTable(src, 'Tax')) {
        for (const t of sqliteRows(src, 'Tax')) {
          if (!t.Name) continue;
          if (num(t.IsFixed)) {
            summary.warnings.push(`Tax "${t.Name}" is a fixed-amount tax and was skipped.`);
            continue;
          }
          let existing = get(`SELECT id FROM tax_rate WHERE lower(name)=lower(?)`, [String(t.Name)]);
          if (!existing) {
            run(`INSERT INTO tax_rate (name, percent, compound, active, is_default) VALUES (?,?,?,?,0)`, [
              String(t.Name),
              num(t.Rate),
              0,
              t.IsEnabled ? 1 : 0,
            ]);
            existing = get(`SELECT last_insert_rowid() AS id`);
            summary.taxes++;
          }
          taxIdMap[t.Id] = existing.id;
        }
      }

      /* products */
      if (opts.products) {
        const existingProducts = all(`SELECT id, name, code FROM product`);
        const byCode = new Map();
        const byName = new Map();
        for (const p of existingProducts) {
          if (p.code) byCode.set(String(p.code).toLowerCase(), p.id);
          if (!byName.has(String(p.name).toLowerCase())) byName.set(String(p.name).toLowerCase(), p.id);
        }
        const existingBarcodes = new Map();
        for (const b of all(`SELECT product_id, value FROM barcode`)) {
          existingBarcodes.set(String(b.value).toLowerCase(), b.product_id);
        }

        const barcodesByProduct = {};
        if (sqliteTable(src, 'Barcode')) {
          for (const b of sqliteRows(src, 'Barcode')) {
            (barcodesByProduct[b.ProductId] = barcodesByProduct[b.ProductId] || []).push(String(b.Value || ''));
          }
        }
        const taxByProduct = {};
        if (sqliteTable(src, 'ProductTax')) {
          for (const pt of sqliteRows(src, 'ProductTax')) {
            (taxByProduct[pt.ProductId] = taxByProduct[pt.ProductId] || []).push(pt.TaxId);
          }
        }
        const commentsByProduct = {};
        if (sqliteTable(src, 'ProductComment')) {
          for (const c of sqliteRows(src, 'ProductComment')) {
            (commentsByProduct[c.ProductId] = commentsByProduct[c.ProductId] || []).push(String(c.Comment || ''));
          }
        }

        let sortIndex = num((get(`SELECT COUNT(*) AS c FROM product`) || {}).c);
        const products = sqliteRows(src, 'Product').sort(
          (a, b) => num(a.Rank) - num(b.Rank) || num(a.Id) - num(b.Id)
        );
        for (const p of products) {
          const name = String(p.Name || '').trim();
          if (!name) continue;
          const code = String(p.Code || '').trim() || (p.PLU != null ? String(p.PLU) : '');
          const bars = (barcodesByProduct[p.Id] || []).map((v) => String(v || '').trim()).filter(Boolean);

          let existingId = null;
          if (code && byCode.has(code.toLowerCase())) existingId = byCode.get(code.toLowerCase());
          if (!existingId && byName.has(name.toLowerCase())) existingId = byName.get(name.toLowerCase());
          if (!existingId) {
            for (const b of bars) {
              if (existingBarcodes.has(b.toLowerCase())) {
                existingId = existingBarcodes.get(b.toLowerCase());
                break;
              }
            }
          }

          if (existingId) {
            productIdMap[p.Id] = existingId;
            summary.products_skipped++;
            for (const b of bars) {
              if (!existingBarcodes.has(b.toLowerCase())) {
                run(`INSERT INTO barcode (product_id, value) VALUES (?,?)`, [existingId, b]);
                existingBarcodes.set(b.toLowerCase(), existingId);
              }
            }
            continue;
          }

          const groupId = p.ProductGroupId == null ? null : groupIdMap[p.ProductGroupId] || null;
          const unit = String(p.MeasurementUnit || 'pcs').trim() || 'pcs';
          const price = cents(p.Price);
          const cost = cents(p.Cost);
          run(
            `INSERT INTO product (name, code, unit, group_id, active, is_service, cost, price,
              price_incl_tax, price_change_allowed, default_qty, age_restricted, color, image,
              description, low_stock, sort_order)
             VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)`,
            [
              name,
              code || null,
              unit,
              groupId,
              p.IsEnabled ? 1 : 0,
              p.IsService ? 1 : 0,
              cost,
              price,
              p.IsTaxInclusivePrice ? 1 : 0,
              p.IsPriceChangeAllowed ? 1 : 0,
              p.IsUsingDefaultQuantity ? 1 : 0,
              num(p.AgeRestriction) > 0 ? 1 : 0,
              importColor(p.Color),
              p.Image ? importBlob(p.Image) : null,
              String(p.Description || ''),
              0,
              sortIndex++,
            ]
          );
          const newId = get(`SELECT last_insert_rowid() AS id`).id;
          productIdMap[p.Id] = newId;
          summary.products++;
          byName.set(name.toLowerCase(), newId);
          if (code) byCode.set(code.toLowerCase(), newId);

          const values = bars.length ? bars : code ? [code] : [];
          for (const b of values) {
            run(`INSERT INTO barcode (product_id, value) VALUES (?,?)`, [newId, b]);
            existingBarcodes.set(b.toLowerCase(), newId);
          }
          const taxIds = (taxByProduct[p.Id] || []).map((id) => taxIdMap[id]).filter(Boolean);
          taxIds.forEach((taxId, i) => {
            run(`INSERT INTO product_tax (product_id, tax_rate_id, sort_order) VALUES (?,?,?)`, [newId, taxId, i + 1]);
          });
          (commentsByProduct[p.Id] || []).forEach((text, i) => {
            const clean = String(text || '').trim();
            if (clean) run(`INSERT INTO product_comment (product_id, text, sort_order) VALUES (?,?,?)`, [newId, clean, i + 1]);
          });
          run(`INSERT INTO stock_item (product_id, quantity, avg_cost) VALUES (?,?,0)`, [newId, 0]);
        }

        /* stock on hand */
        if (sqliteTable(src, 'Stock')) {
          const qtyByProduct = {};
          for (const s of sqliteRows(src, 'Stock')) {
            const pid = productIdMap[s.ProductId];
            if (!pid) continue;
            qtyByProduct[pid] = num(qtyByProduct[pid]) + num(s.Quantity);
          }
          for (const key of Object.keys(qtyByProduct)) {
            const pid = Number(key);
            const qty = qtyByProduct[key];
            const prod = get(`SELECT cost FROM product WHERE id=?`, [pid]) || {};
            run(`UPDATE stock_item SET quantity=?, avg_cost=? WHERE product_id=?`, [qty, num(prod.cost), pid]);
            if (qty !== 0) {
              run(
                `INSERT INTO stock_movement (product_id, document_id, movement, quantity, unit_cost, occurred_at)
                 VALUES (?,?,?,?,?,?)`,
                [pid, null, 'import', qty, num(prod.cost), nowIso()]
              );
            }
            summary.stock++;
          }
        }
      }

      /* customers & suppliers */
      if (opts.customers && sqliteTable(src, 'Customer')) {
        const existingCustomers = all(`SELECT id, name, phone, email FROM customer`);
        const findCustomer = (name, phone, email) => {
          const n = String(name || '').toLowerCase();
          const byName = existingCustomers.find((c) => String(c.name || '').toLowerCase() === n);
          if (byName) return byName.id;
          if (phone) {
            const byPhone = existingCustomers.find((c) => c.phone && String(c.phone) === String(phone));
            if (byPhone) return byPhone.id;
          }
          if (email) {
            const byEmail = existingCustomers.find((c) => c.email && String(c.email).toLowerCase() === String(email).toLowerCase());
            if (byEmail) return byEmail.id;
          }
          return null;
        };
        const cardsByCustomer = {};
        if (sqliteTable(src, 'LoyaltyCard')) {
          for (const lc of sqliteRows(src, 'LoyaltyCard')) {
            (cardsByCustomer[lc.CustomerId] = cardsByCustomer[lc.CustomerId] || []).push(String(lc.CardNumber || ''));
          }
        }

        for (const c of sqliteRows(src, 'Customer')) {
          const name = String(c.Name || c.Code || '').trim();
          if (!name) continue;
          const phone = String(c.PhoneNumber || '');
          const email = String(c.Email || '');
          const type = num(c.IsSupplier) && !num(c.IsCustomer) ? 'supplier' : 'customer';
          let existingId = findCustomer(name, phone, email);
          if (existingId) {
            customerIdMap[c.Id] = existingId;
            summary.customers_skipped++;
          } else {
            run(
              `INSERT INTO customer (type, name, company, email, phone, address, country_id, tax_id, notes,
                active, discount_type, discount_value, balance) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)`,
              [
                type,
                name,
                '',
                email,
                phone,
                importAddress(c),
                null,
                String(c.TaxNumber || ''),
                '',
                c.IsEnabled ? 1 : 0,
                null,
                0,
                0,
              ]
            );
            existingId = get(`SELECT last_insert_rowid() AS id`).id;
            existingCustomers.push({ id: existingId, name, phone, email });
            customerIdMap[c.Id] = existingId;
            summary.customers++;
          }
        }

        for (const oldId of Object.keys(customerIdMap)) {
          const newId = customerIdMap[oldId];
          for (const card of cardsByCustomer[oldId] || []) {
            const value = String(card || '').trim();
            if (!value) continue;
            const dup = get(`SELECT id FROM loyalty_card WHERE number=?`, [value]);
            if (!dup) {
              run(`INSERT INTO loyalty_card (customer_id, number, active) VALUES (?,?,1)`, [newId, value]);
              summary.loyalty++;
            }
          }
        }
      }

      /* payment types */
      if (opts.paymentTypes && sqliteTable(src, 'PaymentType')) {
        const byName = new Map();
        for (const pt of all(`SELECT id, name FROM payment_type`)) byName.set(String(pt.name).toLowerCase(), pt.id);
        let maxSort = num((get(`SELECT MAX(sort_order) AS m FROM payment_type`) || {}).m);
        const rows = sqliteRows(src, 'PaymentType').sort((a, b) => num(a.Ordinal) - num(b.Ordinal));
        for (const pt of rows) {
          const name = String(pt.Name || '').trim();
          if (!name || byName.has(name.toLowerCase())) continue;
          const kind = /cash/i.test(name) ? 'cash' : /card|debit|credit/i.test(name) ? 'card' : 'other';
          run(
            `INSERT INTO payment_type (name, kind, color, icon, active, is_default, sort_order)
             VALUES (?,?,?,?,?,?,?)`,
            [name, kind, kind === 'cash' ? '#047857' : '#4f46e5', null, pt.IsEnabled ? 1 : 0, 0, ++maxSort]
          );
          byName.set(name.toLowerCase(), get(`SELECT last_insert_rowid() AS id`).id);
          summary.payment_types++;
        }
        if (!get(`SELECT id FROM payment_type WHERE is_default=1`)) {
          const cash = get(`SELECT id FROM payment_type WHERE kind='cash' AND active=1 ORDER BY id LIMIT 1`);
          if (cash) run(`UPDATE payment_type SET is_default=1 WHERE id=?`, [cash.id]);
        }
      }

      /* company */
      if (opts.company && sqliteTable(src, 'Company')) {
        const row = sqliteRows(src, 'Company')[0];
        const current = getCompany() || {};
        const isPlaceholder = !current.name || current.name === 'Aroklon' || current.name === 'Aroklon Store';
        if (row && isPlaceholder) {
          const address = importAddress(row) || String(row.Address || '');
          saveCompany({
            ...current,
            name: String(row.Name || current.name || 'Aroklon'),
            address,
            phone: String(row.PhoneNumber || current.phone || ''),
            email: String(row.Email || current.email || ''),
            tax_id: String(row.TaxNumber || current.tax_id || ''),
            logo: row.Logo ? importBlob(row.Logo) : current.logo || null,
            receipt_header: current.receipt_header || '',
            receipt_footer: current.receipt_footer || '',
            currency: current.currency || 'IDR',
            symbol: current.symbol || 'Rp',
            timezone: current.timezone || '',
          });
          summary.company = true;
        }
      }
    });

    if (summary.products_skipped) {
      summary.warnings.push(`${summary.products_skipped} product(s) already existed and were skipped.`);
    }
    if (summary.replaced) {
      summary.warnings.push('The existing catalogue, customers, taxes and stock were replaced.');
    }
    persist();
    return { ok: true, summary };
  } catch (e) {
    return { ok: false, error: e.message || String(e), summary };
  } finally {
    close();
  }
}

module.exports = {
  init,
  persist,
  getSetting,
  setSetting,
  getSettings,
  login,
  listUsers,
  listAccessLevels,
  saveUser,
  deleteUser,
  hasPermission,
  getCompany,
  saveCompany,
  listTaxRates,
  saveTaxRate,
  deleteTaxRate,
  listPaymentTypes,
  savePaymentType,
  deletePaymentType,
  listGroups,
  saveGroup,
  deleteGroup,
  listProducts,
  getProduct,
  getProductByBarcode,
  saveProduct,
  saveWholesalePrices,
  deleteProduct,
  listStock,
  adjustStock,
  stockMovements,
  listCustomers,
  getCustomer,
  saveCustomer,
  deleteCustomer,
  listCountries,
  saveCountry,
  listRegisters,
  saveRegister,
  listDocuments,
  getDocument,
  createSale,
  refundSale,
  createDocument,
  updateDocument,
  deleteDocument,
  listCashMovements,
  addCash,
  getDayState,
  openDay,
  closeDayPreview,
  closeDay,
  xReport,
  getZReport,
  listZReports,
  dashboard,
  report,
  importAronium,
  backup,
  restore,
  factoryReset,
  meta,
  DOC_TYPES,
  // exposed for tests
  _internal: { computeSale, daySummary, unitList, resolveUnit, unitPriceFor, unitFactor },
};