'use strict';

const { app, BrowserWindow, ipcMain, dialog, Menu, shell, screen } = require('electron');
const path = require('path');
const fs = require('fs');
const crypto = require('crypto');
const { spawn } = require('child_process');
const db = require('./db');

/* -------------------------------------------------------------- channels */

// Two apps ship from this codebase: the stable build reads the newest normal
// release, the test build reads the newest prerelease. Both keep the Aroklon
// name, so the test build also gets its own data folder to avoid sharing a
// database with the stable app.
const PKG = require('../../package.json');
const CHANNEL = PKG.updateChannel || 'main';
const IS_TEST = CHANNEL === 'tes';
const BUILD_TAG = PKG.buildTag || `v${PKG.version}`;

if (IS_TEST) {
  const dataDir = path.join(app.getPath('appData'), 'Aroklon-tes');
  app.setPath('userData', dataDir);
  app.setPath('crashDumps', path.join(dataDir, 'Crashpad'));
}

let mainWindow = null;
let printWindow = null;
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) app.quit();

function createWindow() {
  const display = screen.getPrimaryDisplay();
  const work = display.workArea;
  const iconPath = [
    path.join(process.resourcesPath || '', 'aroklon-icon.png'),
    path.join(__dirname, '..', '..', 'build', 'icon.png'),
    path.join(__dirname, '..', 'renderer', 'icon.png'),
  ].find((p) => {
    try {
      return p && fs.existsSync(p);
    } catch (e) {
      return false;
    }
  });

  mainWindow = new BrowserWindow({
    x: work.x,
    y: work.y,
    width: work.width,
    height: work.height,
    minWidth: 1100,
    minHeight: 700,
    show: false,
    backgroundColor: '#0f172a',
    title: 'Aroklon',
    icon: iconPath,
    autoHideMenuBar: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
      spellcheck: false,
    },
  });

  mainWindow.loadFile(path.join(__dirname, '..', 'renderer', 'index.html'));
  mainWindow.once('ready-to-show', () => {
    mainWindow.show();
    mainWindow.maximize();
    setTimeout(() => {
      if (mainWindow && !mainWindow.isDestroyed()) mainWindow.maximize();
    }, 350);
  });

  if (process.env.AROKLON_SMOKE) {
    const shotDir = process.env.AROKLON_SHOT;
    if (shotDir) fs.mkdirSync(shotDir, { recursive: true });
    const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
    async function js(code, label) {
      try {
        const r = await Promise.race([
          mainWindow.webContents.executeJavaScript(code),
          new Promise((_, rej) => setTimeout(() => rej(new Error('timeout ' + label)), 30000)),
        ]);
        console.log('smoke js ok:', label, JSON.stringify(r));
        return r;
      } catch (e) {
        console.log('smoke js FAIL:', label, e.message);
        return null;
      }
    }
    async function shot(name) {
      if (!shotDir) return;
      await sleep(400);
      try {
        const img = await mainWindow.webContents.capturePage();
        fs.writeFileSync(path.join(shotDir, name + '.png'), img.toPNG());
        console.log('smoke shot:', name);
      } catch (e) {
        console.log('shot failed', name, e.message);
      }
    }

    mainWindow.webContents.on('console-message', (_e, level, message) => {
      if (level >= 2) console.log('[renderer]', message);
    });

    mainWindow.webContents.on('did-finish-load', async () => {
      const out = {};
      try {
        await sleep(800);
        await shot('01-login');
        await js(
          `(async () => {
            const s = (ms) => new Promise(r => setTimeout(r, ms));
            const A = window.App;
            for (let i = 0; i < 120 && !(A.state.users || []).length; i++) await s(50);
            const input = document.getElementById('pin-input');
            if (input) {
              input.value = '1234';
              input.dispatchEvent(new Event('input', { bubbles: true }));
              input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
            }
            for (let i = 0; i < 80 && !A.state.user; i++) await s(50);
            if (!A.state.user) await A.login(A.state.users[0].id, '1234');
            A.state.settings.ask_qty = '0';
            await s(400);
            return { ok: true, products: A.state.products.length, pin: A.state.pin };
          })()`,
          'login'
        );
        out.maximized = mainWindow.isMaximized();
        await shot('02-sales');

        await js(
          `(async () => {
            const s = (ms) => new Promise(r => setTimeout(r, ms));
            const A = window.App;
            const picks = A.state.products.filter(p => p.active).slice(0, 3);
            for (const p of picks) { A.actions['pos-add']({ id: String(p.id) }, null, { preventDefault(){} }); await s(80); }
            return { cart: A.state.cart.items.length };
          })()`,
          'fill'
        );
        await shot('03-sales-filled');

        await js(`(() => { window.App.actions['pos-pay']({}, null, {}); return true; })()`, 'payment');
        await shot('04-payment');

        const paid = await js(
          `(async () => {
            const s = (ms) => new Promise(r => setTimeout(r, ms));
            const A = window.App;
            await A.actions['pay-confirm']({}, null, {});
            await s(700);
            const paper = document.querySelector('.receipt-paper');
            return {
              number: A.state.lastReceipt && A.state.lastReceipt.number,
              total: A.state.lastReceipt && A.state.lastReceipt.total,
              receipt: paper ? { bg: getComputedStyle(paper).backgroundColor, color: getComputedStyle(paper).color } : null,
            };
          })()`,
          'confirm'
        );
        out.sale = paid;
        await shot('05-receipt');

        await js(`(() => { const A = window.App; A.closeModal(); A.go('dashboard'); return true; })()`, 'dashboard');
        await shot('06-dashboard');
        await js(`(() => { window.App.go('products'); return true; })()`, 'products');
        await shot('07-products');
        await js(`(() => { window.App.go('stock'); return true; })()`, 'stock');
        await shot('08-stock');
        await js(`(() => { window.App.go('documents'); return true; })()`, 'documents');
        await shot('09-documents');
        await js(`(() => { window.App.go('reports'); return true; })()`, 'reports');
        await shot('10-reports');
        await js(`(() => { window.App.go('customers'); return true; })()`, 'customers');
        await shot('11-customers');
        await js(`(() => { window.App.go('settings'); return true; })()`, 'settings');
        await shot('12-settings');
        await js(`(() => { window.App.go('day'); return true; })()`, 'day');
        await shot('13-day');

        out.classic = await js(
          `(async () => {
            const s = (ms) => new Promise((r) => setTimeout(r, ms));
            const A = window.App;
            const res = {};
            A.setTheme('classic');
            await s(350);
            res.menu = !!document.querySelector('.classic-menubar');
            res.posInClassic = !!document.querySelector('.pos');
            A.go('products');
            await s(500);
            res.tree = !!document.querySelector('.classic-tree');
            res.productRows = document.querySelectorAll('table.data tbody tr').length;
            A.go('settings', { tab: 'countries' });
            await s(600);
            res.countryTab = !!document.querySelector('[data-tab-panel="set-countries"]');
            res.countryRows = document.querySelectorAll('[data-tab-panel="set-countries"] table.data tbody tr').length;
            A.go('sales');
            await s(300);
            res.salesClassic = !!document.querySelector('.pos');
            res.salesOverflow = document.documentElement.scrollWidth > innerWidth + 1;
            res.menubarH = Math.round((document.querySelector('.classic-menubar') || { getBoundingClientRect: () => ({ height: 0 }) }).getBoundingClientRect().height);
            res.statusH = Math.round((document.querySelector('.classic-status') || { getBoundingClientRect: () => ({ height: 0 }) }).getBoundingClientRect().height);
            res.treeOnSales = !!document.querySelector('.classic-tree');
            A.setTheme('modern');
            await s(250);
            res.modernSidebar = !!document.querySelector('.sidebar');
            return res;
          })()`,
          'classic'
        );

        out.update = await js(`window.api.checkUpdate()`, 'update');

        out.dom = await js(
          `(() => ({
            tiles: document.querySelectorAll('.tile').length,
            kpis: document.querySelectorAll('.kpi').length,
            tables: document.querySelectorAll('table.data').length,
            tabs: document.querySelectorAll('.tab').length,
            bodyText: document.body.innerText.slice(0, 400)
          }))()`,
          'dom'
        );

        out.i18n = await js(
          `(() => ({
            lang: window.I18N && I18N.lang,
            locale: window.I18N && I18N.locale(),
            mode: document.documentElement.dataset.mode,
            layout: document.documentElement.dataset.layout,
            missing: window.I18N ? Array.from(I18N.missing).slice(0, 80) : []
          }))()`,
          'i18n'
        );

        out.theme = await js(
          `(async () => {
            const s = (ms) => new Promise((r) => setTimeout(r, ms));
            const A = window.App;
            const snap = () => ({
              mode: document.documentElement.dataset.mode,
              layout: document.documentElement.dataset.layout,
              bg: getComputedStyle(document.body).backgroundColor,
              text: getComputedStyle(document.body).color
            });
            const dark = snap();
            A.setMode('light'); await s(300);
            const light = snap();
            A.setMode('dark'); await s(250);
            A.setTheme('classic'); await s(350);
            const classicDark = {
              menu: getComputedStyle(document.querySelector('.classic-menubar') || document.body).backgroundColor,
              tree: getComputedStyle(document.querySelector('.classic-tree') || document.body).backgroundColor,
              card: getComputedStyle(document.querySelector('.card') || document.body).backgroundColor
            };
            A.setTheme('modern'); await s(250);
            A.setLanguage('en'); await s(300);
            const en = document.body.innerText.replace(/\\s+/g, ' ').slice(0, 140);
            A.setLanguage('id'); await s(300);
            return { dark, light, classicDark, en, back: document.body.innerText.replace(/\\s+/g, ' ').slice(0, 100) };
          })()`,
          'theme'
        );

        out.texts = await js(
          `(async () => {
            const s = (ms) => new Promise((r) => setTimeout(r, ms));
            const A = window.App;
            const res = {};
            for (const r of ['sales','dashboard','products','stock','documents','customers','reports','users','settings','promotions']) {
              A.go(r);
              await s(450);
              res[r] = document.body.innerText.replace(/\\s+/g, ' ').slice(0, 700);
            }
            return res;
          })()`,
          'texts'
        );

        out.lastError = await js(`window.__lastError`, 'lasterr');

        await js(`(() => { window.App.go('sales'); return true; })()`, 'back-sales');
        out.layout = await js(
          `(() => {
            const r = (s) => { const e = document.querySelector(s); if (!e) return null; const b = e.getBoundingClientRect(); return { x: Math.round(b.x), y: Math.round(b.y), w: Math.round(b.width), h: Math.round(b.height) }; };
            const view = document.querySelector('.view');
            const vr = view ? view.getBoundingClientRect() : null;
            const over = [...document.querySelectorAll('.view *')].filter((e) => {
              const b = e.getBoundingClientRect();
              return b.width > 0 && vr && (b.right > vr.right + 2 || b.left < vr.left - 2);
            }).slice(0, 8).map((e) => ({ c: String(e.className || '').slice(0, 36), t: e.tagName }));
            return {
              win: [innerWidth, innerHeight],
              shell: r('.shell'), pos: r('.pos'), catalog: r('.catalog'), order: r('.order'),
              grid: r('.product-grid'), tile: r('.tile'), orderFoot: r('.order-foot'), view: r('.view'),
              hOverflow: document.documentElement.scrollWidth > innerWidth + 1,
              over
            };
          })()`,
          'layout'
        );

        out.probes = await js(
          `(async () => {
            const s = (ms) => new Promise((r) => setTimeout(r, ms));
            const A = window.App;
            const res = {};
            try {
              A.actions['product-new']({}, null, {}); await s(200);
              res.productEditor = !!document.querySelector('form[data-form="save-product"]');
              res.productFields = document.querySelectorAll('form[data-form="save-product"] input, form[data-form="save-product"] select, form[data-form="save-product"] textarea').length;
              A.closeModal();
            } catch (e) { res.productErr = e.message; }
            try {
              A.actions['doc-new']({}, null, {}); await s(150);
              A.actions['doc-pick-type']({ type: 'purchase' }, null, {}); await s(200);
              res.docEditor = !!document.querySelector('form[data-form="save-doc"]');
              res.docItems = (A.state.docDraft || {}).items ? A.state.docDraft.items.length : -1;
              A.closeModal();
            } catch (e) { res.docErr = e.message; }
            try {
              A.go('customers'); await s(350);
              A.actions['customer-new']({}, null, {}); await s(150);
              res.customerEditor = !!document.querySelector('form[data-form="save-customer"]');
              A.closeModal();
            } catch (e) { res.customerErr = e.message; }
            try {
              A.go('users'); await s(350);
              A.actions['user-new']({}, null, {}); await s(150);
              res.userEditor = !!document.querySelector('form[data-form="save-user"]');
              A.closeModal();
            } catch (e) { res.userErr = e.message; }
            try {
              const ids = ['sales_summary','product_sales','payment_types','tax_summary','customer_sales','stock_valuation','low_stock'];
              res.reports = {};
              for (const id of ids) { const rows = await A.api('report', [id, A.state.reportFilters]); res.reports[id] = Array.isArray(rows) ? rows.length : 0; }
            } catch (e) { res.reportErr = e.message; }
            return res;
          })()`,
          'probes'
        );

        out.flowDiscount = await js(
          `(async () => {
            const s = (ms) => new Promise((r) => setTimeout(r, ms));
            const A = window.App; const res = {};
            try {
              A.state.settings.ask_qty = '0';
              A.go('sales'); await s(300);
              const p = A.state.products.find((x) => x.active);
              A.actions['pos-add']({ id: String(p.id) }, null, { preventDefault(){} }); await s(150);
              res.before = A.computeCart(A.state.cart.items, A.state.cart.discount, A.state.cart.tax_override).total;
              A.actions['pos-discount']({}, null, {}); await s(150);
              const dv = document.getElementById('discount_value'); if (dv) dv.value = '10';
              const df = document.querySelector('form[data-form="discount"]'); if (df) df.requestSubmit();
              await s(350);
              res.after = A.computeCart(A.state.cart.items, A.state.cart.discount, A.state.cart.tax_override).total;
              res.value = A.state.cart.discount.value;
              A.actions['pos-clear']({}, null, {}); await s(120);
              const ok = document.querySelector('[data-modal-ok]'); if (ok) ok.click();
              await s(200);
              res.cleared = A.state.cart.items.length === 0;
            } catch (e) { res.err = e.message; }
            return res;
          })()`,
          'flow-discount'
        );

        out.flowDoc = await js(
          `(async () => {
            const s = (ms) => new Promise((r) => setTimeout(r, ms));
            const A = window.App; const res = {};
            try {
              A.go('documents'); await s(350);
              const beforeDocs = (A.state.documents || []).length;
              const beforeStock = (A.state.products || []).find((x) => /Cola/i.test(x.name))?.quantity;
              A.actions['doc-new']({}, null, {}); await s(150);
              A.actions['doc-pick-type']({ type: 'purchase' }, null, {}); await s(250);
              const search = document.getElementById('doc-search');
              if (search) { search.value = 'Cola'; search.dispatchEvent(new Event('input', { bubbles: true })); }
              await s(350);
              const first = document.querySelector('#doc-results [data-action="doc-add-item"]');
              res.result = !!first;
              if (first) { first.click(); await s(300); }
              res.items = (A.state.docDraft || { items: [] }).items.length;
              const df = document.querySelector('form[data-form="save-doc"]');
              if (df) { df.requestSubmit(); await s(800); }
              res.created = (A.state.documents || []).length > beforeDocs;
              res.stockBefore = beforeStock;
              res.stockAfter = (A.state.products || []).find((x) => /Cola/i.test(x.name))?.quantity;
            } catch (e) { res.err = e.message; }
            return res;
          })()`,
          'flow-doc'
        );

        out.flowCustomer = await js(
          `(async () => {
            const s = (ms) => new Promise((r) => setTimeout(r, ms));
            const A = window.App; const res = {};
            try {
              A.go('sales'); await s(350);
              const p = A.state.products.find((x) => x.active);
              A.actions['pos-add']({ id: String(p.id) }, null, { preventDefault(){} }); await s(150);
              A.actions['pos-customer']({}, null, {}); await s(200);
              const cust = document.querySelectorAll('#cust-list [data-action="pick-customer"]');
              res.listed = cust.length;
              if (cust.length > 1) cust[1].click();
              await s(250);
              res.attached = !!A.state.cart.customer_id;
            } catch (e) { res.err = e.message; }
            return res;
          })()`,
          'flow-customer'
        );
      } catch (e) {
        out.error = e.message;
        console.log('smoke error', e.message);
      }
      console.log('AROKLON_SMOKE_RESULT ' + JSON.stringify(out, null, 2));
      setTimeout(() => app.quit(), 300);
    });
  }

  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url);
    return { action: 'deny' };
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

function buildMenu() {
  const isMac = process.platform === 'darwin';
  const template = [
    ...(isMac ? [{ role: 'appMenu' }] : []),
    {
      label: 'File',
      submenu: [
        { label: 'Backup database…', click: () => doBackup() },
        { label: 'Restore database…', click: () => doRestore() },
        { type: 'separator' },
        { label: 'Print preview…', click: () => mainWindow && mainWindow.webContents.send('menu', 'print-preview') },
        { type: 'separator' },
        isMac ? { role: 'close' } : { role: 'quit' },
      ],
    },
    {
      label: 'View',
      submenu: [
        { role: 'reload' },
        { role: 'forceReload' },
        { role: 'toggleDevTools' },
        { type: 'separator' },
        { role: 'resetZoom' },
        { role: 'zoomIn' },
        { role: 'zoomOut' },
        { type: 'separator' },
        { role: 'togglefullscreen' },
      ],
    },
    {
      label: 'Help',
      submenu: [
        {
          label: 'Keyboard shortcuts',
          click: () => mainWindow && mainWindow.webContents.send('menu', 'shortcuts'),
        },
        {
          label: 'Check for updates',
          click: () => mainWindow && mainWindow.webContents.send('menu', 'check-update'),
        },
        {
          label: 'About Aroklon',
          click: () =>
            dialog.showMessageBox(mainWindow, {
              type: 'info',
              title: 'About Aroklon',
              message: 'Aroklon 1.0.0',
              detail:
                'Offline point of sale for Windows.\n\nData is stored locally at:\n' +
                db.meta().db_path +
                '\n\nOriginal software. Not affiliated with any other POS product.',
              buttons: ['OK'],
            }),
        },
      ],
    },
  ];
  Menu.setApplicationMenu(Menu.buildFromTemplate(template));
}

async function doBackup() {
  const res = await dialog.showSaveDialog(mainWindow, {
    title: 'Backup Aroklon database',
    defaultPath: `aroklon-backup-${new Date().toISOString().slice(0, 10)}.db`,
    filters: [{ name: 'Aroklon database', extensions: ['db'] }],
  });
  if (res.canceled || !res.filePath) return;
  try {
    db.backup(res.filePath);
    dialog.showMessageBox(mainWindow, {
      type: 'info',
      message: 'Backup complete',
      detail: `Saved to ${res.filePath}`,
    });
  } catch (e) {
    dialog.showErrorBox('Backup failed', e.message);
  }
}

async function doRestore() {
  const res = await dialog.showOpenDialog(mainWindow, {
    title: 'Restore Aroklon database',
    properties: ['openFile'],
    filters: [{ name: 'Aroklon database', extensions: ['db'] }],
  });
  if (res.canceled || !res.filePaths[0]) return;
  const choice = await dialog.showMessageBox(mainWindow, {
    type: 'warning',
    buttons: ['Cancel', 'Restore'],
    defaultId: 1,
    cancelId: 0,
    message: 'Restore this database?',
    detail: 'Current data will be replaced. A backup of the current file will be made first.',
  });
  if (choice.response !== 1) return;
  try {
    const safe = path.join(app.getPath('userData'), `aroklon-before-restore-${Date.now()}.db`);
    db.backup(safe);
    db.restore(res.filePaths[0]);
    dialog.showMessageBox(mainWindow, {
      type: 'info',
      message: 'Database restored',
      detail: 'Aroklon will now reload.',
    });
    if (mainWindow) mainWindow.reload();
  } catch (e) {
    dialog.showErrorBox('Restore failed', e.message);
  }
}

ipcMain.handle('app:invoke', async (_e, action, payload) => {
  try {
    const fn = db[action];
    if (typeof fn !== 'function') throw new Error(`Unknown action: ${action}`);
    const result = Array.isArray(payload) ? await fn(...payload) : await fn(payload);
    return { ok: true, result };
  } catch (e) {
    return { ok: false, error: e.message || String(e) };
  }
});

ipcMain.handle('app:meta', () => ({ ...db.meta(), channel: CHANNEL, build: BUILD_TAG }));

/* ------------------------------------------------------------- updates */

const REPO = 'clasmil00a/aroklon';
const UPDATE_HEADERS = { 'User-Agent': 'Aroklon-Updater', Accept: 'application/vnd.github+json' };
// The stable build tracks the newest normal release; the test build tracks the
// newest prerelease. GitHub's "latest" endpoint skips prereleases, so the test
// channel has to list releases and pick the first prerelease itself.
const RELEASE_LIST_API = `https://api.github.com/repos/${REPO}/releases?per_page=20`;
const RELEASE_LATEST_API = `https://api.github.com/repos/${REPO}/releases/latest`;

function compareVersions(a, b) {
  const pa = String(a || '0').split('.').map((n) => parseInt(n, 10) || 0);
  const pb = String(b || '0').split('.').map((n) => parseInt(n, 10) || 0);
  const len = Math.max(pa.length, pb.length);
  for (let i = 0; i < len; i++) {
    const d = (pa[i] || 0) - (pb[i] || 0);
    if (d !== 0) return d > 0 ? 1 : -1;
  }
  return 0;
}

async function fetchJson(url) {
  const res = await fetch(url, { headers: UPDATE_HEADERS, signal: AbortSignal.timeout(20000) });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return res.json();
}

// v1.0.5-tes.3 -> { base: [1,0,5], build: 3 }; "v1.0.5" -> { base:[1,0,5], build: 0 }
function parseTestTag(tag) {
  const m = /^v?(\d+(?:\.\d+)*)(?:-tes\.(\d+))?/i.exec(String(tag || ''));
  if (!m) return null;
  return { base: m[1].split('.').map((n) => parseInt(n, 10) || 0), build: m[2] ? parseInt(m[2], 10) : 0 };
}

function compareTestTags(a, b) {
  const pa = parseTestTag(a);
  const pb = parseTestTag(b);
  if (!pa || !pb) return 0;
  const base = compareVersions(pa.base.join('.'), pb.base.join('.'));
  if (base !== 0) return base;
  return pa.build === pb.build ? 0 : pa.build > pb.build ? 1 : -1;
}

async function getLatestMeta() {
  let rel;
  if (IS_TEST) {
    const list = await fetchJson(RELEASE_LIST_API);
    rel = (Array.isArray(list) ? list : []).find((r) => r && r.prerelease === true && r.draft !== true);
    if (!rel) throw new Error('Belum ada rilis tes (prerelease) di GitHub');
  } else {
    rel = await fetchJson(RELEASE_LATEST_API);
  }
  const metaAsset = (rel.assets || []).find((a) => a.name === 'latest.json');
  if (!metaAsset) throw new Error('latest.json tidak ditemukan di rilis terbaru');
  const meta = await fetchJson(metaAsset.browser_download_url);
  const asarAsset = (rel.assets || []).find((a) => a.name === meta.asar);
  if (!asarAsset) throw new Error(`aset ${meta.asar} tidak ditemukan`);
  return {
    ...meta,
    tag: rel.tag_name,
    prerelease: rel.prerelease === true,
    body: rel.body || '',
    asarUrl: asarAsset.browser_download_url,
    size: asarAsset.size,
  };
}

// Stable: compare the app version. Test: compare prerelease tags, because test
// builds intentionally reuse the stable version number.
function isNewerRelease(meta) {
  if (IS_TEST) {
    if (!meta.tag || meta.tag === BUILD_TAG) return false;
    return compareTestTags(meta.tag, BUILD_TAG) > 0;
  }
  return compareVersions(meta.version, app.getVersion()) > 0;
}

function currentVersionLabel() {
  return IS_TEST && BUILD_TAG !== `v${app.getVersion()}` ? `${app.getVersion()} (${BUILD_TAG})` : app.getVersion();
}

async function downloadFile(url, dest) {
  const res = await fetch(url, { headers: { 'User-Agent': 'Aroklon-Updater' }, signal: AbortSignal.timeout(60000) });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  const buf = Buffer.from(await res.arrayBuffer());
  fs.writeFileSync(dest, buf);
  return buf;
}

ipcMain.handle('app:checkUpdate', async () => {
  try {
    const meta = await getLatestMeta();
    const current = app.getVersion();
    const electronMajor = String(process.versions.electron).split('.')[0];
    const compatible = String(meta.electron || '').split('.')[0] === electronMajor;
    return {
      ok: true,
      channel: CHANNEL,
      current: currentVersionLabel(),
      currentTag: IS_TEST ? BUILD_TAG : `v${app.getVersion()}`,
      latest: meta.version,
      latestTag: meta.tag,
      available: isNewerRelease(meta),
      compatible,
      size: meta.size,
      notes: meta.body,
    };
  } catch (e) {
    return { ok: false, error: e.message || String(e) };
  }
});

ipcMain.handle('app:applyUpdate', async () => {
  try {
    const meta = await getLatestMeta();
    if (!isNewerRelease(meta)) return { ok: false, error: 'Sudah versi terbaru' };
    if (String(meta.electron || '').split('.')[0] !== String(process.versions.electron).split('.')[0])
      return { ok: false, error: 'Patch ini butuh runtime Electron yang berbeda' };

    const appDir = path.dirname(app.getPath('exe'));
    try {
      fs.accessSync(appDir, fs.constants.W_OK);
    } catch (e) {
      return {
        ok: false,
        error: 'Folder aplikasi tidak bisa ditulis. Jalankan Aroklon dari folder portable atau sebagai administrator.',
      };
    }

    const updatesDir = path.join(app.getPath('userData'), 'updates');
    fs.mkdirSync(updatesDir, { recursive: true });
    const finalPath = path.join(updatesDir, meta.asar || `app-${meta.version}.asar`);
    // Electron patches fs for .asar paths; download to a plain extension first.
    const tmpPath = finalPath + '.download';
    const buf = await downloadFile(meta.asarUrl, tmpPath);
    const hash = crypto.createHash('sha256').update(buf).digest('hex');
    if (meta.sha256 && hash !== meta.sha256) {
      try { fs.rmSync(tmpPath); } catch (e) {}
      return { ok: false, error: 'SHA-256 tidak cocok. Update dibatalkan.' };
    }
    fs.renameSync(tmpPath, finalPath);

    const bat = path.join(updatesDir, 'apply-update.bat');
    const script = [
      '@echo off',
      'setlocal enabledelayedexpansion',
      `set "APP=${appDir}"`,
      `set "NEW=${finalPath}"`,
      'powershell -NoProfile -Command "$p=Join-Path $env:APP \'Aroklon.exe\'; $d=(Get-Date).AddSeconds(30); while((Get-Process -Name Aroklon -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $p }) -and (Get-Date) -lt $d){Start-Sleep -Milliseconds 300}" >nul 2>&1',
      'set /a tries=0',
      ':retry',
      'copy /Y "%NEW%" "%APP%\\resources\\app.asar" >nul',
      'if not errorlevel 1 goto done',
      'set /a tries+=1',
      'if !tries! GEQ 20 goto fail',
      'ping -n 2 127.0.0.1 >nul',
      'goto retry',
      ':done',
      'del "%NEW%"',
      'start "" "%APP%\\Aroklon.exe"',
      'del "%~f0"',
      'exit /b 0',
      ':fail',
      'echo Gagal menyalin app.asar. & pause',
      'exit /b 1',
      '',
    ].join('\r\n');
    fs.writeFileSync(bat, script);
    return await new Promise((resolve) => {
      const child = spawn('cmd.exe', ['/c', bat], { detached: true, stdio: 'ignore', windowsHide: true });
      child.once('error', (err) => {
        resolve({ ok: false, error: 'Gagal menjalankan updater: ' + err.message });
      });
      child.once('spawn', () => {
        try { db.persist(); } catch (e) {}
        setTimeout(() => app.quit(), 450);
        setTimeout(() => {
          try { app.exit(0); } catch (e) {}
        }, 2500);
        resolve({ ok: true });
      });
    });
  } catch (e) {
    return { ok: false, error: e.message || String(e) };
  }
});

ipcMain.handle('app:backup', () => doBackup());
ipcMain.handle('app:restore', () => doRestore());

ipcMain.handle('app:print', async (_e, html, opts = {}) => {
  if (!html) return { ok: false, error: 'Nothing to print' };
  const silent = opts.silent !== false;
  return await new Promise((resolve) => {
    const win = new BrowserWindow({
      show: false,
      width: 820,
      height: 900,
      webPreferences: { contextIsolation: true, nodeIntegration: false, sandbox: true },
    });
    printWindow = win;
    let settled = false;
    const finish = (ok, reason) => {
      if (settled) return;
      settled = true;
      if (!ok) {
        win.show();
        resolve({ ok: false, reason });
        return;
      }
      setTimeout(() => {
        if (!win.isDestroyed()) win.close();
        resolve({ ok: true });
      }, 400);
    };
    win.webContents.on('did-finish-load', () => {
      win.webContents.print({ silent, printBackground: true }, finish);
    });
    win.webContents.on('did-fail-load', (_ev, code, desc) => finish(false, `${code} ${desc}`));
    win.on('closed', () => {
      printWindow = null;
      if (!settled) finish(false, 'window closed');
    });
    win.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(html));
  });
});

ipcMain.handle('app:preview', async (_e, html) => {
  const win = new BrowserWindow({
    width: 900,
    height: 940,
    title: 'Aroklon — Print preview',
    webPreferences: { contextIsolation: true, nodeIntegration: false, sandbox: true },
  });
  win.setMenu(null);
  win.loadURL('data:text/html;charset=utf-8,' + encodeURIComponent(html));
  return { ok: true };
});

ipcMain.handle('app:open-data-dir', () => {
  shell.openPath(app.getPath('userData'));
  return { ok: true };
});

app.whenReady().then(async () => {
  if (process.platform === 'win32') {
    try {
      app.setAppUserModelId('aroklon.pos');
    } catch (e) {}
  }
  await db.init(app.getPath('userData'));
  buildMenu();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});

app.on('before-quit', () => {
  db.persist();
});

process.on('uncaughtException', (err) => {
  console.error(err);
  dialog.showErrorBox('Aroklon error', err && err.message ? err.message : String(err));
});