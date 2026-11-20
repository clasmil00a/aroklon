'use strict';

/* ==================================================================== */
/*  Aroklon — screens: documents, customers, reports, users, settings   */
/* ==================================================================== */

(function () {
  const A = window.App;
  const U = window.U;
  const { esc, attr, icon, money, qty, fmtDate, fmtTime, initials, t, locale } = U;
  const SH = window.SH;

  /* ------------------------------------------------------------- loaders */

  A.loader('products', async () => {
    await A.reloadCatalog();
  });

  A.loader('stock', async () => {
    A.state.stock = await A.api('listStock', {});
  });

  A.loader('dashboard', async () => {
    A.state.dashboard = await A.api('dashboard');
  });

  A.loader('documents', async () => {
    A.state.documents = await A.api('listDocuments', A.state.docFilters || {});
  });

  A.loader('customers', async () => {
    A.state.allCustomers = await A.api('listCustomers', {});
  });

  A.loader('users', async () => {
    A.state.users = await A.api('listUsers');
  });

  A.loader('settings', async () => {
    await A.reloadReference();
    A.state.settings = await A.api('getSettings');
  });

  A.loader('reports', async () => {
    if (A.state.reportId) await loadReport(A.state.reportId);
  });

  A.screens.promotions = {
    meta: { title: t('Promotions'), sub: t('Happy hour, bundles and rules') },
    render() {
      return `<div class="card">
        <div class="card-head"><h2>Promotions &amp; actions</h2><div class="right"><span class="badge gray">Soon</span></div></div>
        <div class="card-body">${SH.empty({
          icon: 'tag',
          title: 'Not included in this edition',
          text: 'Promotions and happy-hour rules are planned for a future release. Cart discounts, item discounts, customer discounts and tax overrides are available today.',
        })}</div>
      </div>`;
    },
  };

  /* ------------------------------------------------------------ documents */

  A.state.docFilters = A.state.docFilters || { type: '', status: '', from: '', to: '', number: '' };

  const DOC_TYPES = [
    { id: 'purchase', label: 'Purchase', desc: 'Receive stock from a supplier', icon: 'stock' },
    { id: 'stock_return', label: 'Stock return', desc: 'Return goods to a supplier', icon: 'back' },
    { id: 'count', label: 'Inventory count', desc: 'Set stock to a counted quantity', icon: 'products' },
    { id: 'loss', label: 'Loss & damage', desc: 'Write off damaged or lost stock', icon: 'warning' },
    { id: 'proforma', label: 'Proforma / quote', desc: 'Quotation without stock movement', icon: 'receipt' },
  ];

  A.screens.documents = {
    meta: { title: t('Documents'), sub: t('Purchases, counts and stock movements') },
    render() {
      const f = A.state.docFilters;
      const docs = A.state.documents || [];
      return `<div class="card">
        <div class="card-head"><div class="row gap8"><h2>All documents</h2><span class="badge gray">${docs.length}</span></div>
          <div class="right">
            <button class="btn sm" data-action="docs-refresh">${icon('refresh', 14)} Refresh</button>
            <button class="btn primary sm" data-action="doc-new">${icon('plus', 15)} New document</button>
          </div>
        </div>
        <div class="card-body" style="padding:14px 18px">
          <div class="filters">
            <div class="field"><label>Type</label>${SH.select(
              'doc-type',
              [{ value: '', label: t('All types') }].concat(
                ['sale', 'refund'].concat(DOC_TYPES.map((d) => d.id)).map((t) => ({ value: t, label: SH.docTypeLabel(t) }))
              ),
              f.type,
              { dataChange: 'doc-filter', dataKey: 'type' }
            )}</div>
            <div class="field"><label>${esc(t('Status'))}</label>${SH.select('doc-status', [
              { value: '', label: t('Any') },
              { value: 'closed', label: t('Closed') },
              { value: 'partial', label: t('Partial') },
              { value: 'open', label: t('Open') },
            ], f.status, { dataChange: 'doc-filter', dataKey: 'status' })}</div>
            <div class="field"><label>From</label><input class="input" type="date" data-change="doc-filter" data-key="from" value="${attr(f.from)}" /></div>
            <div class="field"><label>To</label><input class="input" type="date" data-change="doc-filter" data-key="to" value="${attr(f.to)}" /></div>
            <div class="field" style="flex:1;min-width:150px"><label>Number</label><input class="input" data-change="doc-filter" data-key="number" value="${attr(f.number)}" placeholder="Search number" /></div>
            <button class="btn" data-action="docs-refresh">${icon('filter', 14)} Apply</button>
            ${f.type || f.status || f.from || f.to || f.number ? '<button class="btn ghost" data-action="doc-clear-filters">Clear</button>' : ''}
          </div>
          <div class="table-wrap">
            ${
              docs.length
                ? `<table class="data"><thead><tr><th>Number</th><th>Type</th><th>Date</th><th>Party</th><th>User</th><th class="num">Total</th><th>Status</th><th></th></tr></thead><tbody>
                  ${docs
                    .map(
                      (d) => `<tr>
                        <td class="strong mono">${esc(d.number || '—')}${d.external_number ? `<div class="small muted">ref ${esc(d.external_number)}</div>` : ''}</td>
                        <td>${SH.docTypeBadge(d.type)}</td>
                        <td class="small">${esc(fmtDate(d.doc_date, true))}</td>
                        <td>${esc(d.customer_name || '—')}</td>
                        <td class="small muted">${esc(d.user_name || '—')}</td>
                        <td class="num mono strong">${money(d.total)}</td>
                        <td>${SH.badge(t(d.status || 'closed'), d.status === 'open' ? 'warn' : d.status === 'partial' ? 'info' : 'good')}</td>
                        <td class="right"><button class="btn sm" data-action="doc-view" data-id="${d.id}">View</button></td>
                      </tr>`
                    )
                    .join('')}
                </tbody></table>`
                : SH.empty({ icon: 'documents', title: 'No documents', text: 'Create a purchase, stock count or write-off.', action: `<button class="btn primary" data-action="doc-new">${icon('plus', 15)} New document</button>` })
            }
          </div>
        </div>
      </div>`;
    },
  };

  A.action('docs-refresh', async () => {
    A.state.documents = await A.api('listDocuments', A.state.docFilters);
    A.render();
  });

  A.action('doc-clear-filters', () => {
    A.state.docFilters = { type: '', status: '', from: '', to: '', number: '' };
    A.go('documents');
  });

  A.action('change:doc-filter', (ds, el) => {
    A.state.docFilters[ds.key] = el.value;
    A.go('documents');
  });

  A.action('doc-new', () => {
    A.modal(
      `${SH.modalHead('New document', 'Choose what happened')}
      <div class="modal-body">
        <div class="grid" style="grid-template-columns:1fr 1fr">
          ${DOC_TYPES.map(
            (t) => `<button type="button" class="card" data-action="doc-pick-type" data-type="${t.id}" style="text-align:left;padding:16px;display:flex;gap:12px;align-items:center">
              <div style="width:40px;height:40px;border-radius:11px;background:var(--primary-50);color:var(--primary);display:grid;place-items:center;flex:0 0 auto">${icon(t.icon, 20)}</div>
              <div><div class="strong" style="font-size:13.5px">${esc(t.label)}</div><div class="small muted">${esc(t.desc)}</div></div>
            </button>`
          ).join('')}
        </div>
      </div>`,
      { size: 'wide' }
    );
  });

  A.action('doc-pick-type', (ds) => {
    const now = new Date();
    const local = new Date(now.getTime() - now.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
    A.state.docDraft = {
      id: null,
      type: ds.type,
      supplier_id: null,
      customer_id: null,
      external_number: '',
      doc_date: local,
      stock_date: local,
      notes: '',
      items: [],
    };
    A.state.docResults = [];
    renderDocEditor();
  });

  function docItemsLabel(type) {
    if (type === 'count') return 'Counted quantity';
    if (type === 'proforma') return 'Unit price';
    return 'Unit cost';
  }

  function docProduct(d, it) {
    return (A.state.products || []).find((x) => String(x.id) === String(it.product_id)) || null;
  }

  function docUnitCell(d, it, i) {
    const p = docProduct(d, it);
    const baseName = (p && p.unit) || it.unit || '';
    if (d.type === 'count') return `<span class="small muted">${esc(it.unit || baseName)}</span>`;
    const extras = p ? SH.units.extraUnits(p) : [];
    if (!extras.length) return `<span class="small muted">${esc(baseName)}</span>`;
    const opts = [{ name: baseName }].concat(extras);
    return `<select class="input doc-unit" data-index="${i}" style="max-width:140px;padding:4px 7px">${opts
      .map(
        (o) =>
          `<option value="${attr(o.name)}" ${
            String(o.name) === String(it.unit || baseName) ? 'selected' : ''
          }>${esc(o.name)}</option>`
      )
      .join('')}</select>`;
  }

  function docPartyField(d) {
    const suppliers = (A.state.allCustomers || []).filter((c) => c.type === 'supplier');
    const customers = (A.state.allCustomers || []).filter((c) => c.type === 'customer');
    if (d.type === 'purchase' || d.type === 'stock_return' || d.type === 'loss') {
      return SH.field('Supplier', SH.select('supplier_id', [{ value: '', label: '— select —' }].concat(suppliers.map((c) => ({ value: c.id, label: c.name }))), d.supplier_id));
    }
    if (d.type === 'proforma') {
      return SH.field('Customer', SH.select('customer_id', [{ value: '', label: '— walk-in —' }].concat(customers.map((c) => ({ value: c.id, label: c.name }))), d.customer_id));
    }
    return '';
  }

  function renderDocEditor() {
    const d = A.state.docDraft;
    if (!d) return;
    const title = SH.docTypeLabel(d.type);
    const priceLabel = docItemsLabel(d.type);
    const showPrice = d.type !== 'count';
    const results = A.state.docResults || [];

    A.modal(
      `<form data-form="save-doc">
        <input type="hidden" name="type" value="${d.type}" />
        ${SH.modalHead(d.id ? `Document ${d.type}` : `New ${title.toLowerCase()}`, 'Stock updates when you save')}
        <div class="modal-body">
          <div class="form-grid mb16">
            ${docPartyField(d)}
            ${SH.field('External reference', SH.input('external_number', d.external_number, { placeholder: 'Supplier invoice no.' }))}
            ${SH.field('Document date', `<input class="input" type="datetime-local" data-change="doc-header" data-key="doc_date" value="${attr(d.doc_date)}" />`)}
            ${
              d.type === 'count'
                ? SH.field('Stock date (when counted)', `<input class="input" type="datetime-local" data-change="doc-header" data-key="stock_date" value="${attr(d.stock_date)}" />`, { hint: 'Expected quantity is calculated as of this date. Cannot be in the future.' })
                : ''
            }
            ${SH.field('Notes', SH.input('notes', d.notes, { placeholder: 'Optional' }), { span: 2 })}
          </div>

          <div class="label mb8">Add products</div>
          <div class="row gap8 mb8">
            <div class="search-box" style="flex:1">${icon('search', 16)}<input id="doc-search" class="input" data-input="doc-search" placeholder="Search name, code or barcode…" autocomplete="off" /></div>
          </div>
          <div id="doc-results"></div>

          <div class="label mb8">Items</div>
          ${
            d.items.length
              ? `<table class="data" style="border:1px solid var(--line);border-radius:10px;overflow:hidden"><thead><tr><th>Product</th><th>Unit</th><th class="num">Qty</th>${showPrice ? `<th class="num">${priceLabel}</th>` : ''}<th class="num">Total</th><th></th></tr></thead><tbody>
                ${d.items
                  .map(
                    (it, i) => `<tr>
                      <td><span class="strong">${esc(it.name)}</span><div class="small muted mono">${esc(it.code || '')}</div></td>
                      <td>${docUnitCell(d, it, i)}</td>
                      <td class="num"><input class="input doc-qty" data-index="${i}" type="number" step="0.001" min="0" value="${U.num(it.quantity)}" style="width:80px;padding:5px 7px;text-align:right" /></td>
                      ${showPrice ? `<td class="num"><input class="input doc-price" data-index="${i}" type="number" step="0.01" min="0" value="${(U.num(it.unit_price) / 100).toFixed(2)}" style="width:100px;padding:5px 7px;text-align:right" /></td>` : ''}
                      <td class="num mono strong" data-line-total="${i}">${money(Math.round(U.num(it.quantity) * U.num(it.unit_price)))}</td>
                      <td class="right"><button type="button" class="icon-btn" data-action="doc-remove-item" data-index="${i}" style="color:var(--bad)">${icon('trash', 15)}</button></td>
                    </tr>`
                  )
                  .join('')}
              </tbody></table>
              <div class="row between mt8"><span class="small muted">${d.items.length} item(s)</span><span class="strong">Total <span id="doc-grand-total">${money(
                d.items.reduce((s, it) => s + Math.round(U.num(it.quantity) * U.num(it.unit_price)), 0)
              )}</span></span></div>`
              : SH.empty({ icon: 'products', title: 'No items yet', text: 'Search above and tap a product to add it.' })
          }
        </div>
        ${SH.modalFoot(d.id ? 'Save document' : 'Create document')}
      </form>`,
      { size: 'wide' }
    );
  }

  A.action('change:doc-header', (ds, el) => {
    if (!A.state.docDraft) return;
    A.state.docDraft[ds.key] = el.value;
  });

  function syncDocInputs() {
    const form = document.querySelector('form[data-form="save-doc"]');
    if (!form || !A.state.docDraft) return;
    form.querySelectorAll('.doc-qty').forEach((inp) => {
      const i = Number(inp.dataset.index);
      if (A.state.docDraft.items[i]) A.state.docDraft.items[i].quantity = U.num(inp.value, 0);
    });
    form.querySelectorAll('.doc-price').forEach((inp) => {
      const i = Number(inp.dataset.index);
      if (A.state.docDraft.items[i]) A.state.docDraft.items[i].unit_price = Math.round(U.num(inp.value, 0) * 100);
    });
    form.querySelectorAll('.doc-unit').forEach((sel) => {
      const i = Number(sel.dataset.index);
      if (A.state.docDraft.items[i]) A.state.docDraft.items[i].unit = sel.value;
    });
  }

  A.action('doc-add-item', (ds) => {
    syncDocInputs();
    const p = (A.state.products || []).find((x) => String(x.id) === String(ds.id));
    if (!p || !A.state.docDraft) return;
    const type = A.state.docDraft.type;
    const unitName = type === 'count' ? p.unit : p.default_unit || p.unit;
    const unit = SH.units.resolveUnit(p, unitName);
    const factor = U.num(unit.factor, 1) || 1;
    const existing = A.state.docDraft.items.find(
      (it) => String(it.product_id) === String(p.id) && String(it.unit || '') === String(unit.name)
    );
    if (existing) existing.quantity = U.num(existing.quantity) + 1;
    else
      A.state.docDraft.items.push({
        product_id: p.id,
        name: p.name,
        code: p.code,
        unit: unit.name,
        unit_factor: factor,
        quantity: 1,
        unit_price:
          type === 'proforma' ? SH.units.unitPrice(p, unit) : Math.round(U.num(p.cost) * factor),
      });
    A.state.docResults = [];
    renderDocEditor();
  });

  A.action('doc-remove-item', (ds) => {
    syncDocInputs();
    A.state.docDraft.items.splice(Number(ds.index), 1);
    renderDocEditor();
  });

  A.action('change:doc-unit', (ds, el) => {
    if (!A.state.docDraft) return;
    syncDocInputs();
    const i = Number(el.dataset.index);
    const it = A.state.docDraft.items[i];
    if (!it) return;
    const p = docProduct(A.state.docDraft, it);
    if (!p) return;
    const unit = SH.units.resolveUnit(p, el.value);
    const factor = U.num(unit.factor, 1) || 1;
    it.unit = unit.name;
    it.unit_factor = factor;
    it.unit_price =
      A.state.docDraft.type === 'proforma'
        ? SH.units.unitPrice(p, unit)
        : Math.round(U.num(p.cost) * factor);
    renderDocEditor();
  });

  A.action('doc-view', async (ds) => {
    const doc = await A.tryApi('getDocument', Number(ds.id), 'Could not load document');
    if (!doc) return;
    A.state.viewDoc = doc;
    const canPrint = doc.type === 'sale' || doc.type === 'refund';
    A.modal(
      `<div class="modal-head"><div><h2>${esc(doc.number || 'Document')}</h2><div class="sub">${esc(SH.docTypeLabel(doc.type))} · ${esc(
        fmtDate(doc.doc_date, true)
      )}</div></div><div class="right"><button type="button" class="icon-btn" data-modal-cancel>${icon('close', 18)}</button></div></div>
      <div class="modal-body">
        <div class="stat-strip">
          <div class="stat"><div class="sl">Total</div><div class="sv">${money(doc.total)}</div></div>
          <div class="stat"><div class="sl">Items</div><div class="sv">${(doc.items || []).length}</div></div>
          <div class="stat"><div class="sl">Status</div><div class="sv">${esc(doc.status || 'closed')}</div></div>
          ${doc.customer ? `<div class="stat"><div class="sl">Customer</div><div class="sv" style="font-size:13px">${esc(doc.customer.name)}</div></div>` : ''}
        </div>
        <table class="data"><thead><tr><th>Product</th><th class="num">Qty</th><th class="num">Price</th><th class="num">Total</th></tr></thead><tbody>
          ${(doc.items || [])
            .map(
              (it) => `<tr><td class="strong">${esc(it.name_snapshot)}</td><td class="num mono">${U.qty(it.quantity)} ${esc(
                it.unit_snapshot || it.unit || ''
              )}</td><td class="num mono">${money(it.unit_price)}</td><td class="num mono">${money(it.total)}</td></tr>`
            )
            .join('')}
        </tbody></table>
        ${(doc.payments || []).length ? `<div class="divider"></div><div class="label mb8">Payments</div>${doc.payments
          .map((p) => `<div class="row between small" style="padding:3px 0"><span>${esc(p.payment_name || 'Payment')}</span><span class="mono">${money(p.amount)}</span></div>`)
          .join('')}` : ''}
      </div>
      <div class="modal-foot"><button type="button" class="btn" data-modal-cancel>Close</button>
        ${
          canPrint
            ? `<button type="button" class="btn primary" data-action="doc-print" data-id="${doc.id}">${icon('print', 15)} Print</button>`
            : `<button type="button" class="btn primary" data-action="doc-print-report">${icon('print', 15)} Print</button>`
        }
      </div>`,
      { size: 'wide' }
    );
  });

  A.action('doc-print', async (ds) => {
    const doc = await A.tryApi('getDocument', Number(ds.id), 'Could not load document');
    if (!doc) return;
    await SH.print(SH.receiptHtml(doc, A.state.company), true);
  });

  A.action('doc-print-report', async () => {
    const d = A.state.viewDoc;
    if (!d) return;
    const rows = [['Product', 'Qty', 'Price', 'Total']].concat(
      (d.items || []).map((it) => [
        it.name_snapshot,
        `${U.qty(it.quantity)} ${it.unit_snapshot || it.unit || ''}`.trim(),
        money(it.unit_price),
        money(it.total),
      ])
    );
    await window.api.preview(SH.reportHtml(SH.docTypeLabel(d.type) + ' ' + (d.number || ''), rows, 'Total ' + money(d.total)));
  });

  /* ------------------------------------------------------------- customers */

  A.screens.customers = {
    meta: { title: t('Customers'), sub: t('Contacts, suppliers and loyalty') },
    render() {
      const all = A.state.allCustomers || [];
      const type = A.state.customerType || 'customer';
      const list = all.filter((c) => c.type === type);
      const search = (A.state.customerSearch || '').toLowerCase();
      const filtered = search ? list.filter((c) => (c.name + ' ' + (c.phone || '') + ' ' + (c.email || '')).toLowerCase().includes(search)) : list;
      return `<div class="card">
        <div class="card-head">
          <div class="row gap8">
            <h2>${type === 'customer' ? 'Customers' : 'Suppliers'}</h2>
            <div class="search-modes">
              <button class="${type === 'customer' ? 'active' : ''}" data-action="customer-type" data-type="customer">Customers</button>
              <button class="${type === 'supplier' ? 'active' : ''}" data-action="customer-type" data-type="supplier">Suppliers</button>
            </div>
          </div>
          <div class="right">
            <div class="search-box" style="width:220px">${icon('search', 15)}<input id="customer-search" class="input" data-input="customer-search" placeholder="Search…" value="${attr(
              A.state.customerSearch || ''
            )}" /></div>
            <button class="btn primary sm" data-action="customer-new">${icon('plus', 15)} New ${type}</button>
          </div>
        </div>
        <div class="table-wrap">
          ${
            filtered.length
              ? `<table class="data"><thead><tr><th>Name</th><th>Company</th><th>Phone</th><th>Email</th><th>Loyalty</th><th class="num">Balance</th><th></th></tr></thead><tbody>
                ${filtered
                  .map(
                    (c) => `<tr>
                      <td><div class="row gap8"><span class="avatar" style="width:30px;height:30px;border-radius:8px;font-size:11px">${esc(initials(c.name))}</span><div><div class="strong">${esc(
                      c.name
                    )}</div>${c.discount_type ? `<div class="small muted">${esc(c.discount_type === 'percent' ? t('Percentage') : t('Fixed amount'))} ${U.num(c.discount_value)}</div>` : ''}</div></div></td>
                      <td>${esc(c.company || '—')}</td>
                      <td class="muted">${esc(c.phone || '—')}</td>
                      <td class="muted">${esc(c.email || '—')}</td>
                      <td>${c.card_count ? `<span class="badge info">${esc(t('{n} cards', { n: c.card_count }))}</span>` : '<span class="muted">—</span>'}</td>
                      <td class="num mono">${money(c.balance)}</td>
                      <td class="right"><div class="row gap8" style="justify-content:flex-end">
                        <button class="icon-btn" data-action="customer-edit" data-id="${c.id}" title="Edit">${icon('edit', 16)}</button>
                        <button class="icon-btn" data-action="customer-delete" data-id="${c.id}" title="Delete" style="color:var(--bad)">${icon('trash', 16)}</button>
                      </div></td>
                    </tr>`
                  )
                  .join('')}
              </tbody></table>`
              : SH.empty({ icon: 'customers', title: 'No contacts', text: 'Add a customer or supplier.', action: `<button class="btn primary" data-action="customer-new">${icon('plus', 15)} New ${type}</button>` })
          }
        </div>
      </div>`;
    },
  };

  A.action('customer-type', (ds) => {
    A.state.customerType = ds.type;
    A.render();
  });

  A.action('customer-new', () => openCustomerEditor(null));

  A.action('customer-edit', async (ds) => {
    const c = await A.tryApi('getCustomer', Number(ds.id), 'Could not load customer');
    if (c) openCustomerEditor(c);
  });

  function openCustomerEditor(c) {
    const p =
      c || {
        type: A.state.customerType || 'customer',
        name: '',
        company: '',
        email: '',
        phone: '',
        address: '',
        country_id: null,
        tax_id: '',
        notes: '',
        active: true,
        discount_type: '',
        discount_value: 0,
        balance: 0,
        cards: [],
      };
    const countries = (A.state.countries || []).map((x) => ({ value: x.id, label: x.name }));
    A.modal(
      `<form data-form="save-customer">
        <input type="hidden" name="id" value="${attr(p.id || '')}" />
        ${SH.modalHead(p.id ? 'Edit ' + p.type : 'New ' + (A.state.customerType || 'customer'), 'Contact details and loyalty')}
        <div class="modal-body">
          <div class="form-grid">
            ${SH.field('Type', SH.select('type', [{ value: 'customer', label: 'Customer' }, { value: 'supplier', label: 'Supplier' }], p.type))}
            ${SH.field('Name', SH.input('name', p.name, { required: true }), {})}
            ${SH.field('Company', SH.input('company', p.company))}
            ${SH.field('Phone', SH.input('phone', p.phone))}
            ${SH.field('Email', SH.input('email', p.email, { type: 'email' }))}
            ${SH.field('Country', SH.select('country_id', [{ value: '', label: '—' }].concat(countries), p.country_id))}
            ${SH.field('Tax ID', SH.input('tax_id', p.tax_id))}
            ${SH.field('Discount type', SH.select('discount_type', [{ value: '', label: 'None' }, { value: 'percent', label: 'Percentage' }, { value: 'fixed', label: 'Fixed amount' }], p.discount_type))}
            ${SH.field('Discount value', SH.input('discount_value', p.discount_value, { type: 'number', step: '0.01', min: '0' }))}
            ${SH.field('Account balance', SH.input('balance', (p.balance || 0) / 100, { type: 'number', step: '0.01' }), { hint: 'Outstanding credit, in currency units.' })}
            ${SH.field('Address', `<textarea class="input" name="address" placeholder="Street, city">${esc(p.address || '')}</textarea>`, { span: 2 })}
            ${SH.field('Loyalty cards (one per line)', `<textarea class="input" name="cards" placeholder="Scan or type card numbers">${esc(
              (p.cards || []).map((x) => (typeof x === 'string' ? x : x.number)).join('\n')
            )}</textarea>`, { span: 2, hint: 'Cards let you attach a customer to a sale with a reader.' })}
            ${SH.field('Notes', `<textarea class="input" name="notes" placeholder="Internal note">${esc(p.notes || '')}</textarea>`, { span: 2 })}
            <div class="span2">${SH.checkbox('active', 'Active', p.active !== false)}</div>
          </div>
        </div>
        ${SH.modalFoot(p.id ? 'Save changes' : 'Create contact')}
      </form>`,
      { size: 'wide' }
    );
  }

  A.action('customer-delete', async (ds) => {
    const ok = await A.confirm({ title: 'Delete this contact?', message: '<p class="small">Contacts with documents cannot be deleted.</p>', confirmLabel: 'Delete', danger: true });
    if (!ok) return;
    const res = await A.tryApi('deleteCustomer', Number(ds.id), 'Could not delete');
    if (res && res.ok) {
      A.state.allCustomers = await A.api('listCustomers', {});
      A.render();
      A.toast('Contact deleted');
    }
  });

  /* --------------------------------------------------------------- reports */

  const REPORTS = [
    { id: 'sales_summary', name: 'Sales summary', desc: 'Totals by day' },
    { id: 'product_sales', name: 'Product sales', desc: 'What sold and what it earned' },
    { id: 'payment_types', name: 'Payment types', desc: 'Cash, card and other tenders' },
    { id: 'tax_summary', name: 'Tax summary', desc: 'Tax collected by day' },
    { id: 'customer_sales', name: 'Customer sales', desc: 'Spend by customer' },
    { id: 'stock_valuation', name: 'Stock valuation', desc: 'On hand × average cost' },
    { id: 'low_stock', name: 'Low stock', desc: 'Items at or below alert level' },
  ];

  A.state.reportId = A.state.reportId || 'sales_summary';
  const d = new Date();
  d.setDate(d.getDate() - 30);
  A.state.reportFilters = A.state.reportFilters || {
    from: d.toISOString().slice(0, 10),
    to: U.todayKey(),
  };

  A.screens.reports = {
    meta: { title: t('Reports'), sub: t('Print or export your numbers') },
    render() {
      const rep = A.state.reportId;
      const rows = A.state.reportRows || [];
      return `<div class="split-2">
        <div class="report-list">
          ${REPORTS.map(
            (r) => `<button class="report-item ${rep === r.id ? 'active' : ''}" data-action="report-pick" data-id="${r.id}">
              <div class="rt">${esc(r.name)}</div><div class="rd">${esc(r.desc)}</div>
            </button>`
          ).join('')}
        </div>
        <div class="card" style="flex:1;min-width:0">
          <div class="card-head">
            <h2>${esc((REPORTS.find((r) => r.id === rep) || {}).name || 'Report')}</h2>
            <div class="right">
              ${rep !== 'stock_valuation' && rep !== 'low_stock' && rep !== 'z_reports' ? `<input class="input" type="date" data-change="report-filter" data-key="from" value="${attr(A.state.reportFilters.from)}" style="width:145px" />
              <input class="input" type="date" data-change="report-filter" data-key="to" value="${attr(A.state.reportFilters.to)}" style="width:145px" />
              <button class="btn sm" data-action="report-load">${icon('refresh', 14)} ${esc(t('Run'))}</button>` : ''}
              <button class="btn sm" data-action="report-print">${icon('print', 14)} Print</button>
              <button class="btn sm" data-action="report-csv">${icon('download', 14)} CSV</button>
            </div>
          </div>
          <div class="table-wrap" style="max-height:calc(100vh - 260px)">
            ${
              rows.length
                ? rowsToTable(rows)
                : SH.empty({ icon: 'reports', title: 'No data for this period', text: 'Try a wider date range or run a different report.' })
            }
          </div>
        </div>
      </div>`;
    },
  };

  A.action('report-pick', (ds) => {
    A.state.reportId = ds.id;
    A.state.reportRows = null;
    A.go('reports');
  });

  A.action('change:report-filter', (ds, el) => {
    A.state.reportFilters[ds.key] = el.value;
  });

  A.action('report-load', async () => {
    await loadReport(A.state.reportId);
    A.render();
  });

  async function loadReport(id) {
    const rows = await A.tryApi('report', [id, A.state.reportFilters], 'Could not build report');
    if (rows) A.state.reportRows = rows;
  }

  function isMoneyKey(k) {
    return /^(total|subtotal|discount|discount_total|tax|tax_total|sales_total|refunds_total|net_total|cash_in|cash_out|cash_expected|opening_cash|avg_cost|cost_total|value|amount|unit_cost|price|cost|net|balance|sales|refunds)$/i.test(
      k
    );
  }
  function isCountKey(k) {
    return /^(sales_count|returns_count|qty_sold|qty_returned|quantity|count|txns|qty)$/i.test(k);
  }
  function labelKey(k) {
    return k
      .replace(/_/g, ' ')
      .replace(/\b\w/g, (m) => m.toUpperCase())
      .replace('ID', 'ID');
  }
  function fmtCell(k, v) {
    if (v == null || v === '') return '—';
    if (/date|time/i.test(k) && typeof v === 'string') return fmtDate(v, true);
    if (isCountKey(k)) return String(v);
    if (isMoneyKey(k)) return money(Math.round(U.num(v)));
    return v;
  }

  function rowsToTable(rows) {
    if (!rows.length) return '';
    const cols = Object.keys(rows[0]);
    return `<table class="data"><thead><tr>${cols
      .map((c) => `<th class="${isMoneyKey(c) || isCountKey(c) ? 'num' : ''}">${esc(labelKey(c))}</th>`)
      .join('')}</tr></thead><tbody>
      ${rows
        .map(
          (r) => `<tr>${cols
            .map((c) => `<td class="${isMoneyKey(c) || isCountKey(c) ? 'num mono' : ''}">${esc(fmtCell(c, r[c]))}</td>`)
            .join('')}</tr>`
        )
        .join('')}
    </tbody></table>`;
  }

  A.action('report-print', async () => {
    const rows = A.state.reportRows || [];
    if (!rows.length) return A.toast('Nothing to print', 'warn');
    const cols = Object.keys(rows[0]);
    const table = [['', ...cols]].concat(rows.map((r) => ['', ...cols.map((c) => fmtCell(c, r[c]))]));
    const head = [cols.map(labelKey)];
    const body = rows.map((r) => cols.map((c) => fmtCell(c, r[c])));
    const html = SH.reportHtml(
      (REPORTS.find((r) => r.id === A.state.reportId) || {}).name || 'Report',
      [cols.map(labelKey)].concat(body),
      `${A.state.reportFilters.from || ''} → ${A.state.reportFilters.to || ''}`
    );
    await window.api.preview(html);
  });

  A.action('report-csv', () => {
    const rows = A.state.reportRows || [];
    if (!rows.length) return A.toast('Nothing to export', 'warn');
    const cols = Object.keys(rows[0]);
    const csv = [cols.map(labelKey).join(',')].concat(rows.map((r) => cols.map((c) => String(r[c] == null ? '' : r[c]).replace(/"/g, '""')).join(','))).join('\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `aroklon-${A.state.reportId}-${U.todayKey()}.csv`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 2000);
  });

  /* ----------------------------------------------------------------- users */

  A.screens.users = {
    meta: { title: t('Users'), sub: t('Staff accounts and access levels') },
    render() {
      const users = A.state.users || [];
      const levels = A.state.accessLevels || [];
      return `<div class="card">
        <div class="card-head"><div class="row gap8"><h2>Users</h2><span class="badge gray">${users.length}</span></div>
          <div class="right"><button class="btn primary sm" data-action="user-new">${icon('plus', 15)} Add user</button></div>
        </div>
        <div class="table-wrap">
          ${
            users.length
              ? `<table class="data"><thead><tr><th>Name</th><th>Access level</th><th>Register</th><th>Status</th><th></th></tr></thead><tbody>
                ${users
                  .map(
                    (u) => `<tr>
                      <td><div class="row gap8"><span class="avatar" style="width:30px;height:30px;border-radius:8px;font-size:11px">${esc(initials(u.name))}</span>
                        <div><div class="strong">${esc(u.name)}</div><div class="small muted">PIN ····${esc(String(u.pin || '').slice(-2))}</div></div></div></td>
                      <td><span class="badge info">${esc(u.access_name || '—')}</span></td>
                      <td>${esc(u.register_name || '—')}</td>
                      <td>${u.active ? SH.badge('Active', 'good') : SH.badge('Deactivated', 'gray')}</td>
                      <td class="right"><div class="row gap8" style="justify-content:flex-end">
                        <button class="icon-btn" data-action="user-edit" data-id="${u.id}" title="Edit">${icon('edit', 16)}</button>
                        <button class="icon-btn" data-action="user-delete" data-id="${u.id}" title="Delete" style="color:var(--bad)">${icon('trash', 16)}</button>
                      </div></td>
                    </tr>`
                  )
                  .join('')}
              </tbody></table>`
              : SH.empty({ icon: 'users', title: 'No users', text: 'Add the first staff account.' })
          }
        </div>
      </div>`;
    },
  };

  A.action('user-new', () => openUserEditor(null));

  A.action('user-edit', (ds) => {
    const u = (A.state.users || []).find((x) => String(x.id) === String(ds.id));
    if (u) openUserEditor(u);
  });

  async function openUserEditor(u) {
    if (!A.state.accessLevels) A.state.accessLevels = await A.api('listAccessLevels');
    const p = u || { name: '', pin: '', access_level_id: (A.state.accessLevels || [])[0]?.id, register_id: A.state.user.register_id, active: true };
    const levels = (A.state.accessLevels || []).map((l) => ({ value: l.id, label: l.name }));
    const regs = (A.state.registers || []).map((r) => ({ value: r.id, label: r.name }));
    A.modal(
      `<form data-form="save-user">
        <input type="hidden" name="id" value="${attr(p.id || '')}" />
        ${SH.modalHead(p.id ? 'Edit user' : 'Add user', 'Access level controls what this person can see and do')}
        <div class="modal-body">
          <div class="form-grid">
            ${SH.field('Name', SH.input('name', p.name, { required: true, autofocus: true }))}
            ${SH.field('PIN', SH.input('pin', '', { type: 'password', placeholder: p.id ? 'Leave blank to keep current PIN' : '4–8 digits', required: !p.id }), { hint: 'Used to sign in at the till.' })}
            ${SH.field('Access level', SH.select('access_level_id', levels, p.access_level_id))}
            ${SH.field('Default register', SH.select('register_id', [{ value: '', label: '—' }].concat(regs), p.register_id))}
            <div class="span2">${SH.checkbox('active', 'Active (can sign in)', p.active !== false)}</div>
          </div>
          <div class="hint mt12">You can only manage users with the same or a lower access level than your own.</div>
        </div>
        ${SH.modalFoot(p.id ? 'Save changes' : 'Create user')}
      </form>`,
      { size: '' }
    );
  }

  A.action('user-delete', async (ds) => {
    const ok = await A.confirm({ title: 'Delete this user?', message: '<p class="small">Users with documents cannot be deleted. Deactivate them instead.</p>', confirmLabel: 'Delete', danger: true });
    if (!ok) return;
    const res = await A.tryApi('deleteUser', Number(ds.id), 'Could not delete');
    if (res && res.ok) {
      A.state.users = await A.api('listUsers');
      A.render();
      A.toast('User deleted');
    }
  });

  /* -------------------------------------------------------------- settings */

  A.screens.settings = {
    meta: { title: t('Settings'), sub: t('Company, tax, tenders and data') },
    render() {
      const s = A.state.settings || {};
      const c = A.state.company || {};
      const tab = (A.state.params && A.state.params.tab) || A.state.settingsTab || 'company';
      A.state.settingsTab = tab;
      const ps = (t) => (tab === t ? '' : 'style="display:none"');
      return `<div class="card">
        <div class="tabs">
          ${[
            ['company', 'Company'],
            ['tax', 'Tax rates'],
            ['tenders', 'Payment types'],
            ['countries', 'Countries'],
            ['registers', 'Registers'],
            ['prefs', 'Preferences'],
            ['data', 'Data'],
          ]
            .map((t) => `<button class="tab ${tab === t[0] ? 'active' : ''}" data-action="tab" data-target="set-${t[0]}">${t[1]}</button>`)
            .join('')}
        </div>
        <div class="card-body">
          <div data-tab-panel="set-company" ${ps('company')}>
            <form data-form="save-company">
              <div class="form-grid" style="max-width:820px">
                ${SH.field('Store name', SH.input('name', c.name, { required: true }), { span: 2 })}
                ${SH.field('Address', SH.input('address', c.address), { span: 2 })}
                ${SH.field('Phone', SH.input('phone', c.phone))}
                ${SH.field('Email', SH.input('email', c.email))}
                ${SH.field('Tax ID', SH.input('tax_id', c.tax_id))}
                ${SH.field('Currency', SH.input('currency', c.currency || 'IDR'))}
                ${SH.field('Currency symbol', SH.input('symbol', c.symbol || 'Rp'))}
                ${SH.field('Timezone', SH.input('timezone', c.timezone || ''))}
                ${SH.field('Receipt header', SH.input('receipt_header', c.receipt_header || '', { placeholder: 'Printed at the top of every receipt' }), { span: 2 })}
                ${SH.field('Receipt footer', SH.input('receipt_footer', c.receipt_footer || '', { placeholder: 'e.g. Thank you for your purchase!' }), { span: 2 })}
              </div>
              <div class="row mt16"><span class="spacer"></span><button class="btn primary" type="submit">${icon('save', 15)} Save company</button></div>
            </form>
          </div>

          <div data-tab-panel="set-tax" ${ps('tax')}>
            <div class="row mb12"><div><div class="strong">Tax rates</div><div class="small muted">VAT, GST or sales tax. Multiple rates can apply to one product.</div></div><span class="spacer"></span>
              <button class="btn primary sm" data-action="tax-new">${icon('plus', 15)} Add tax rate</button></div>
            ${miniTable(
              ['Name', 'Percent', 'Compound', 'Default', ''],
              (A.state.taxRates || []).map(
                (t) => [
                  esc(t.name),
                  U.num(t.percent) + '%',
                  t.compound ? 'Yes' : 'No',
                  t.is_default ? SH.badge('Default', 'info') : '',
                  `<div class="row gap8" style="justify-content:flex-end"><button class="icon-btn" data-action="tax-edit" data-id="${t.id}">${icon('edit', 15)}</button><button class="icon-btn" data-action="tax-delete" data-id="${t.id}" style="color:var(--bad)">${icon('trash', 15)}</button></div>`,
                ]
              )
            )}
          </div>

          <div data-tab-panel="set-tenders" ${ps('tenders')}>
            <div class="row mb12"><div><div class="strong">Payment types</div><div class="small muted">Cash, card and any other tender you accept.</div></div><span class="spacer"></span>
              <button class="btn primary sm" data-action="tender-new">${icon('plus', 15)} Add payment type</button></div>
            ${miniTable(
              ['Name', 'Kind', 'Colour', 'Default', 'Active', ''],
              (A.state.paymentTypes || []).map(
                (t) => [
                  `<span class="row gap8"><span style="width:10px;height:10px;border-radius:3px;background:${attr(t.color || '#64748b')}"></span><span class="strong">${esc(t.name)}</span></span>`,
                  esc(t.kind),
                  attr(t.color),
                  t.is_default ? SH.badge('Default', 'info') : '',
                  t.active ? SH.badge('Active', 'good') : SH.badge('Off', 'gray'),
                  `<div class="row gap8" style="justify-content:flex-end"><button class="icon-btn" data-action="tender-edit" data-id="${t.id}">${icon('edit', 15)}</button><button class="icon-btn" data-action="tender-delete" data-id="${t.id}" style="color:var(--bad)">${icon('trash', 15)}</button></div>`,
                ]
              )
            )}
          </div>

          <div data-tab-panel="set-countries" ${ps('countries')}>
            <div class="row mb12"><div><div class="strong">${esc(t('Countries'))}</div><div class="small muted">${esc(t('Used on customers and suppliers.'))}</div></div><span class="spacer"></span>
              <button class="btn primary sm" data-action="country-new">${icon('plus', 15)} Add country</button></div>
            ${miniTable(
              ['Country', 'Code', 'Active', ''],
              (A.state.countries || []).map(
                (c) => [
                  esc(c.name),
                  esc(c.code || '—'),
                  c.active ? SH.badge('Active', 'good') : SH.badge('Off', 'gray'),
                  `<div class="row gap8" style="justify-content:flex-end"><button class="icon-btn" data-action="country-edit" data-id="${c.id}">${icon('edit', 15)}</button></div>`,
                ]
              )
            )}
          </div>

          <div data-tab-panel="set-registers" ${ps('registers')}>
            <div class="row mb12"><div><div class="strong">Cash registers</div><div class="small muted">One till per PC in the free edition.</div></div><span class="spacer"></span>
              <button class="btn primary sm" data-action="register-new">${icon('plus', 15)} Add register</button></div>
            ${miniTable(
              ['Name', 'Code', 'Printer', 'Drawer', 'Primary', ''],
              (A.state.registers || []).map(
                (r) => [
                  esc(r.name),
                  esc(r.code || '—'),
                  esc(r.printer || 'Default'),
                  r.drawer ? 'Yes' : 'No',
                  r.is_primary ? SH.badge('Primary', 'info') : '',
                  `<div class="row gap8" style="justify-content:flex-end"><button class="icon-btn" data-action="register-edit" data-id="${r.id}">${icon('edit', 15)}</button></div>`,
                ]
              )
            )}
          </div>

          <div data-tab-panel="set-prefs" ${ps('prefs')}>
            <div class="card mb16"><div class="card-body">
              <div class="strong mb8">${esc(t('Appearance'))}</div>
              <div class="grid" style="grid-template-columns:repeat(3,minmax(0,1fr));gap:12px">
                <div class="field">
                  <label>${esc(t('Layout'))}</label>
                  ${SH.select('layout', [
                    { value: 'modern', label: t('Modern') },
                    { value: 'classic', label: t('Classic') },
                  ], A.state.theme, { dataChange: 'pref-layout' })}
                </div>
                <div class="field">
                  <label>${esc(t('Color mode'))}</label>
                  ${SH.select('mode', [
                    { value: 'dark', label: t('Dark') },
                    { value: 'light', label: t('Light') },
                    { value: 'system', label: t('System') },
                  ], A.state.mode || 'dark', { dataChange: 'pref-mode' })}
                </div>
                <div class="field">
                  <label>${esc(t('Language'))}</label>
                  ${SH.select('lang', [
                    { value: 'id', label: 'Bahasa Indonesia' },
                    { value: 'en', label: 'English' },
                  ], window.I18N ? I18N.lang : 'id', { dataChange: 'pref-language' })}
                </div>
              </div>
              <p class="hint mt8">${esc(t('Classic = classic desktop POS layout with a top menu and Management tree. All assets remain Aroklon’s.'))}</p>
            </div></div>
            <form data-form="save-prefs">
              <div class="grid" style="grid-template-columns:1fr 1fr;max-width:860px">
                <div class="card"><div class="card-body">
                  <div class="strong mb8">Till behaviour</div>
                  ${SH.checkbox('allow_negative_stock', 'Allow selling when stock is zero or negative', s.allow_negative_stock === '1')}
                  ${SH.checkbox('ask_qty', 'Ask for quantity when adding a product', s.ask_qty !== '0')}
                  <div class="field mt16"><label>Product layout</label>${SH.select('layout', [
                    { value: 'grid', label: 'Grid (visual tiles)' },
                    { value: 'list', label: 'List (compact rows)' },
                  ], s.layout || 'grid')}</div>
                </div></div>
                <div class="card"><div class="card-body">
                  <div class="strong mb8">Receipt & tax</div>
                  <div class="field"><label>Default tax percent (fallback)</label>${SH.input('default_tax_percent', s.default_tax_percent || '0', { type: 'number', step: '0.01', min: '0' })}</div>
                  <p class="hint mt8">Used only for products that have no tax rate assigned.</p>
                </div></div>
              </div>
              <div class="row mt16"><span class="spacer"></span><button class="btn primary" type="submit">${icon('save', 15)} Save preferences</button></div>
            </form>
          </div>

          <div data-tab-panel="set-data" ${ps('data')}>
            <div class="card mb16"><div class="card-body">
              <div class="row wrap gap8">
                <div>
                  <div class="strong">Updates</div>
                  <div class="small muted">Aroklon v${esc((A.state.meta && A.state.meta.version) || '1.0.0')}${(A.state.meta && A.state.meta.channel === 'tes') ? ` · <span class="tag-warn">tes ${esc(A.state.meta.build || '')}</span>` : ''} · app-only patch, bukan download runtime penuh.</div>
                </div>
                <span class="spacer"></span>
                <button class="btn" data-action="check-update">${icon('refresh', 15)} Check for updates</button>
              </div>
              <div id="update-status" class="small muted mt8"></div>
            </div></div>
            <div class="grid" style="grid-template-columns:1fr 1fr;max-width:900px">
              <div class="card"><div class="card-body">
                <div class="strong mb8">Backup & restore</div>
                <p class="small muted mb12">Your data lives in a local SQLite file. Copy it somewhere safe — this is not a cloud service.</p>
                <div class="row gap8">
                  <button class="btn" data-action="data-backup">${icon('download', 15)} Backup now</button>
                  <button class="btn" data-action="data-restore">${icon('upload', 15)} Restore</button>
                  <button class="btn" data-action="data-folder">${icon('products', 15)} Open data folder</button>
                </div>
                <div class="hint mt12">${esc((A.state.meta && A.state.meta.db_path) || '')}</div>
              </div></div>
              <div class="card"><div class="card-body">
                <div class="strong mb8">Database</div>
                <div class="stat-strip" style="margin-bottom:12px">
                  ${Object.entries((A.state.meta && A.state.meta.counts) || {})
                    .map((e) => `<div class="stat"><div class="sl">${esc(e[0])}</div><div class="sv">${e[1]}</div></div>`)
                    .join('')}
                </div>
                <button class="btn danger" data-action="data-reset">${icon('warning', 15)} Reset all data</button>
                <p class="hint mt8">Deletes every product, sale and setting, then restores the sample catalogue.</p>
              </div></div>
            </div>
          </div>
        </div>
      </div>`;
    },
  };

  function miniTable(headers, rows) {
    return `<div style="border:1px solid var(--line);border-radius:10px;overflow:hidden"><table class="data"><thead><tr>${headers
      .map((h) => `<th>${h}</th>`)
      .join('')}</tr></thead><tbody>${rows
      .map((r) => `<tr>${r.map((c) => `<td>${c}</td>`).join('')}</tr>`)
      .join('')}</tbody></table></div>`;
  }

  A.action('tax-new', () => openTaxEditor(null));
  A.action('tax-edit', (ds) => openTaxEditor((A.state.taxRates || []).find((t) => String(t.id) === String(ds.id))));
  function openTaxEditor(t) {
    const p = t || { name: '', percent: 0, compound: false, active: true, is_default: false };
    A.modal(
      `<form data-form="save-tax">
        <input type="hidden" name="id" value="${attr(p.id || '')}" />
        ${SH.modalHead(p.id ? 'Edit tax rate' : 'New tax rate')}
        <div class="modal-body"><div class="form-grid">
          ${SH.field('Name', SH.input('name', p.name, { required: true, placeholder: 'e.g. VAT 11%' }))}
          ${SH.field('Percent', SH.input('percent', p.percent, { type: 'number', step: '0.01', min: '0', required: true }))}
          <div class="span2 row wrap gap12">
            ${SH.checkbox('compound', 'Compound (apply on top of other taxes)', !!p.compound)}
            ${SH.checkbox('is_default', 'Default rate for new products', !!p.is_default)}
            ${SH.checkbox('active', 'Active', p.active !== false)}
          </div>
        </div></div>
        ${SH.modalFoot(p.id ? 'Save changes' : 'Create tax rate')}
      `,
      { size: '' }
    );
  }

  A.action('tender-new', () => openTenderEditor(null));
  A.action('tender-edit', (ds) => openTenderEditor((A.state.paymentTypes || []).find((t) => String(t.id) === String(ds.id))));
  function openTenderEditor(t) {
    const p = t || { name: '', kind: 'other', color: '#64748b', active: true, is_default: false, sort_order: (A.state.paymentTypes || []).length + 1 };
    const colors = ['#10b981', '#6366f1', '#f59e0b', '#ef4444', '#0ea5e9', '#8b5cf6', '#64748b'];
    A.modal(
      `<form data-form="save-tender">
        <input type="hidden" name="id" value="${attr(p.id || '')}" />
        ${SH.modalHead(p.id ? 'Edit payment type' : 'New payment type')}
        <div class="modal-body"><div class="form-grid">
          ${SH.field('Name', SH.input('name', p.name, { required: true, placeholder: 'e.g. QRIS, Card, Cash' }))}
          ${SH.field('Kind', SH.select('kind', [{ value: 'cash', label: 'Cash' }, { value: 'card', label: 'Card' }, { value: 'other', label: 'Other' }], p.kind))}
          <div class="span2"><div class="label mb8">Colour</div><div class="row wrap gap8">
            ${colors
              .map(
                (c) => `<label style="cursor:pointer"><input type="radio" name="color" value="${c}" ${p.color === c ? 'checked' : ''} style="display:none" />
                <span style="display:block;width:32px;height:32px;border-radius:9px;background:${c};border:${p.color === c ? '2px solid var(--primary)' : '1px solid var(--line)'}"></span></label>`
              )
              .join('')}
          </div></div>
          ${SH.field('Sort order', SH.input('sort_order', p.sort_order, { type: 'number', min: '0' }))}
          <div class="span2 row wrap gap12">
            ${SH.checkbox('is_default', 'Default tender', !!p.is_default)}
            ${SH.checkbox('active', 'Active', p.active !== false)}
          </div>
        </div></div>
        ${SH.modalFoot(p.id ? 'Save changes' : 'Create payment type')}
      `,
      { size: '' }
    );
  }

  A.action('register-new', () => openRegisterEditor(null));
  A.action('register-edit', (ds) => openRegisterEditor((A.state.registers || []).find((r) => String(r.id) === String(ds.id))));
  function openRegisterEditor(r) {
    const p = r || { name: '', code: '', printer: '', drawer: true, is_primary: (A.state.registers || []).length === 0 };
    A.modal(
      `<form data-form="save-register">
        <input type="hidden" name="id" value="${attr(p.id || '')}" />
        ${SH.modalHead(p.id ? 'Edit register' : 'New register')}
        <div class="modal-body"><div class="form-grid">
          ${SH.field('Name', SH.input('name', p.name, { required: true, placeholder: 'e.g. Front Counter' }))}
          ${SH.field('Code', SH.input('code', p.code, { placeholder: 'REG-01' }))}
          ${SH.field('Printer', SH.input('printer', p.printer, { placeholder: 'Windows printer name (optional)' }), { span: 2 })}
          <div class="span2 row wrap gap12">
            ${SH.checkbox('drawer', 'Cash drawer connected', p.drawer !== false)}
            ${SH.checkbox('is_primary', 'Primary register', !!p.is_primary)}
          </div>
        </div></div>
        ${SH.modalFoot(p.id ? 'Save changes' : 'Create register')}
      `,
      { size: '' }
    );
  }

  A.action('country-new', () => openCountryEditor(null));
  A.action('country-edit', (ds) =>
    openCountryEditor((A.state.countries || []).find((c) => String(c.id) === String(ds.id)))
  );
  function openCountryEditor(c) {
    const p = c || { name: '', code: '', active: true };
    A.modal(
      `<form data-form="save-country">
        <input type="hidden" name="id" value="${attr(p.id || '')}" />
        ${SH.modalHead(p.id ? 'Edit country' : 'Add country')}
        <div class="modal-body"><div class="form-grid">
          ${SH.field('Name', SH.input('name', p.name, { required: true, placeholder: 'e.g. Indonesia' }))}
          ${SH.field('Code', SH.input('code', p.code, { placeholder: 'e.g. ID' }))}
          <div class="span2">${SH.checkbox('active', 'Active', p.active !== false)}</div>
        </div></div>
        ${SH.modalFoot(p.id ? 'Save changes' : 'Create country')}
      </form>`
    );
  }

  A.action('data-backup', () => window.api.backup());
  A.action('data-restore', () => window.api.restore());
  A.action('data-folder', () => window.api.openDataDir());
  A.action('data-reset', async () => {
    const ok = await A.confirm({
      title: 'Reset all data?',
      sub: 'This cannot be undone.',
      message: '<p class="small">Every product, customer, sale and setting will be deleted and the sample catalogue restored. Back up first.</p>',
      confirmLabel: 'Reset everything',
      danger: true,
    });
    if (!ok) return;
    const res = await A.tryApi('factoryReset', {}, 'Reset failed');
    if (res && res.ok) {
      await A.reloadReference();
      await A.reloadCatalog();
      A.state.settings = await A.api('getSettings');
      A.state.meta = await window.api.meta();
      A.toast('Data reset');
      A.go('dashboard');
    }
  });

  /* --------------------------------------------------------- form handlers */

  A.action('form:save-product', async (form) => {
    const fd = new FormData(form);
    const id = fd.get('id') ? Number(fd.get('id')) : null;
    const barcodes = [...form.querySelectorAll('.barcode-row')]
      .map((row) => ({
        value: row.querySelector('.barcode-row-input').value.trim(),
        unit: row.querySelector('.barcode-row-unit') ? row.querySelector('.barcode-row-unit').value : '',
      }))
      .filter((b) => b.value);
    const units = [...form.querySelectorAll('.unit-row')]
      .map((row) => ({
        name: row.querySelector('.unit-name').value.trim(),
        factor: U.num(row.querySelector('.unit-factor').value, 0),
        price: (() => {
          const v = row.querySelector('.unit-price').value.trim();
          return v === '' ? null : U.num(v);
        })(),
        is_default: !!row.querySelector('input[name="unit_default"]:checked'),
      }))
      .filter((u) => u.name);
    const comments = [...form.querySelectorAll('.comment-row-input')].map((i) => i.value.trim()).filter(Boolean);
    const tax_rate_ids = [...form.querySelectorAll('input[name="tax_rate_id"]:checked')].map((i) => Number(i.value));
    const res = await A.api('saveProduct', {
      id,
      name: String(fd.get('name') || '').trim(),
      code: String(fd.get('code') || '').trim(),
      unit: String(fd.get('unit') || 'pcs'),
      group_id: fd.get('group_id') ? Number(fd.get('group_id')) : null,
      active: fd.get('active') === '1',
      is_service: fd.get('is_service') === '1',
      cost: U.num(fd.get('cost')),
      price: U.num(fd.get('price')),
      price_incl_tax: fd.get('price_incl_tax') === '1',
      price_change_allowed: fd.get('price_change_allowed') === '1',
      default_qty: fd.get('default_qty') === '1',
      age_restricted: fd.get('age_restricted') === '1',
      color: String(fd.get('color') || '#e2e8f0'),
      image: String(fd.get('image') || '') || null,
      description: String(fd.get('description') || ''),
      low_stock: U.num(fd.get('low_stock')),
      barcodes,
      units,
      comments,
      tax_rate_ids,
    });
    if (!res.ok) throw new Error(res.error);
    A.closeModal();
    await A.reloadCatalog();
    A.render();
    A.toast(id ? 'Product updated' : 'Product created');
  });

  A.action('form:discount', (form) => {
    const fd = new FormData(form);
    const scope = fd.get('scope');
    const type = fd.get('discount_type');
    const value = U.num(fd.get('discount_value'));
    const tax = String(fd.get('tax_override') || '').trim();
    if (scope === 'item') {
      const idx = Number(fd.get('line_index'));
      const it = A.state.cart.items[idx];
      if (it) {
        it.discount_type = value > 0 ? type : null;
        it.discount_value = value > 0 ? value : 0;
      }
    } else {
      A.state.cart.discount = { type, value: value > 0 ? value : 0 };
    }
    A.state.cart.tax_override = tax === '' ? null : U.num(tax);
    A.closeModal();
    A.render();
    A.toast('Discount applied');
  });

  A.action('form:do-refund', async (form) => {
    const fd = new FormData(form);
    const docId = Number(fd.get('doc_id'));
    const itemIds = fd.getAll('item_ids').map(Number);
    const paymentType = fd.get('payment_type_id') ? Number(fd.get('payment_type_id')) : undefined;
    const reason = String(fd.get('reason') || '').trim();
    if (!itemIds.length) throw new Error('Select at least one item');
    const doc = await A.api('refundSale', [
      docId,
      { item_ids: itemIds, payment_type_id: paymentType, reason, user_id: A.state.user.id },
    ]);
    A.closeModal();
    A.state.lastReceipt = doc;
    await A.reloadCatalog();
    A.render();
    A.toast(`Refund complete · ${money(Math.abs(doc.total))}`);
    showRefundReceipt(doc);
  });

  function showRefundReceipt(doc) {
    A.modal(
      `<div class="modal-head"><div><h2>Refund complete</h2><div class="sub">${esc(doc.number)} · ${money(Math.abs(doc.total))}</div></div>
        <div class="right"><button type="button" class="icon-btn" data-modal-cancel>${icon('close', 18)}</button></div></div>
      <div class="modal-body"><div class="receipt-preview">${SH.receiptPaper(doc, A.state.company)}</div></div>
      <div class="modal-foot"><button type="button" class="btn" data-modal-cancel>Close</button>
        <button type="button" class="btn primary" data-action="receipt-print">${icon('print', 15)} Print</button></div>`
    );
  }

  A.action('form:save-company', async (form) => {
    const fd = new FormData(form);
    await A.api('saveCompany', {
      name: String(fd.get('name') || '').trim(),
      address: String(fd.get('address') || ''),
      phone: String(fd.get('phone') || ''),
      email: String(fd.get('email') || ''),
      tax_id: String(fd.get('tax_id') || ''),
      logo: (A.state.company || {}).logo || null,
      receipt_header: String(fd.get('receipt_header') || ''),
      receipt_footer: String(fd.get('receipt_footer') || ''),
      currency: String(fd.get('currency') || 'IDR'),
      symbol: String(fd.get('symbol') || 'Rp'),
      timezone: String(fd.get('timezone') || ''),
    });
    A.state.company = await A.api('getCompany');
    A.render();
    A.toast('Company details saved');
  });

  A.action('form:save-prefs', async (form) => {
    const fd = new FormData(form);
    await A.api('setSetting', ['allow_negative_stock', fd.get('allow_negative_stock') === '1' ? '1' : '0']);
    await A.api('setSetting', ['ask_qty', fd.get('ask_qty') === '1' ? '1' : '0']);
    await A.api('setSetting', ['layout', String(fd.get('layout') || 'grid')]);
    await A.api('setSetting', ['default_tax_percent', String(U.num(fd.get('default_tax_percent')))]);
    A.state.settings = await A.api('getSettings');
    A.render();
    A.toast('Preferences saved');
  });

  A.action('form:save-tax', async (form) => {
    const fd = new FormData(form);
    const id = fd.get('id') ? Number(fd.get('id')) : null;
    await A.api('saveTaxRate', {
      id,
      name: String(fd.get('name') || '').trim(),
      percent: U.num(fd.get('percent')),
      compound: fd.get('compound') === '1',
      active: fd.get('active') === '1',
      is_default: fd.get('is_default') === '1',
    });
    A.closeModal();
    A.state.taxRates = await A.api('listTaxRates');
    A.render();
    A.toast('Tax rate saved');
  });

  A.action('form:save-tender', async (form) => {
    const fd = new FormData(form);
    const id = fd.get('id') ? Number(fd.get('id')) : null;
    await A.api('savePaymentType', {
      id,
      name: String(fd.get('name') || '').trim(),
      kind: String(fd.get('kind') || 'other'),
      color: String(fd.get('color') || '#64748b'),
      active: fd.get('active') === '1',
      is_default: fd.get('is_default') === '1',
      sort_order: U.num(fd.get('sort_order')),
    });
    A.closeModal();
    A.state.paymentTypes = await A.api('listPaymentTypes');
    A.render();
    A.toast('Payment type saved');
  });

  A.action('form:save-register', async (form) => {
    const fd = new FormData(form);
    const id = fd.get('id') ? Number(fd.get('id')) : null;
    await A.api('saveRegister', {
      id,
      name: String(fd.get('name') || '').trim(),
      code: String(fd.get('code') || ''),
      printer: String(fd.get('printer') || ''),
      drawer: fd.get('drawer') === '1',
      is_primary: fd.get('is_primary') === '1',
    });
    A.closeModal();
    A.state.registers = await A.api('listRegisters');
    A.render();
    A.toast('Register saved');
  });

  A.action('form:save-customer', async (form) => {
    const fd = new FormData(form);
    const id = fd.get('id') ? Number(fd.get('id')) : null;
    const cards = String(fd.get('cards') || '')
      .split('\n')
      .map((s) => s.trim())
      .filter(Boolean);
    const res = await A.api('saveCustomer', {
      id,
      type: String(fd.get('type') || 'customer'),
      name: String(fd.get('name') || '').trim(),
      company: String(fd.get('company') || ''),
      email: String(fd.get('email') || ''),
      phone: String(fd.get('phone') || ''),
      address: String(fd.get('address') || ''),
      country_id: fd.get('country_id') ? Number(fd.get('country_id')) : null,
      tax_id: String(fd.get('tax_id') || ''),
      notes: String(fd.get('notes') || ''),
      active: fd.get('active') === '1',
      discount_type: String(fd.get('discount_type') || '') || null,
      discount_value: U.num(fd.get('discount_value')),
      balance: U.num(fd.get('balance')),
      cards,
    });
    if (!res.ok) throw new Error(res.error);
    A.closeModal();
    A.state.allCustomers = await A.api('listCustomers', {});
    await A.reloadCatalog();
    A.render();
    A.toast(id ? 'Contact updated' : 'Contact created');
  });

  A.action('form:save-user', async (form) => {
    const fd = new FormData(form);
    const id = fd.get('id') ? Number(fd.get('id')) : null;
    const res = await A.api('saveUser', {
      id,
      name: String(fd.get('name') || '').trim(),
      pin: String(fd.get('pin') || ''),
      access_level_id: Number(fd.get('access_level_id')),
      register_id: fd.get('register_id') ? Number(fd.get('register_id')) : null,
      active: fd.get('active') === '1',
    });
    if (!res.ok) throw new Error(res.error);
    A.closeModal();
    A.state.users = await A.api('listUsers');
    A.render();
    A.toast(id ? 'User updated' : 'User created');
  });

  A.action('form:save-doc', async (form) => {
    const d = A.state.docDraft;
    if (!d) return;
    syncDocInputs();
    if (!d.items.length) throw new Error('Add at least one product');
    const fd = new FormData(form);
    const res = await A.api('createDocument', {
      type: d.type,
      supplier_id: d.supplier_id || null,
      customer_id: d.customer_id || null,
      external_number: String(fd.get('external_number') || '') || null,
      doc_date: d.doc_date || new Date().toISOString(),
      stock_date: d.stock_date || d.doc_date,
      notes: String(fd.get('notes') || ''),
      user_id: A.state.user.id,
      register_id: (A.state.registers || []).find((r) => r.is_primary)?.id,
      items: d.items.map((it) => ({
        product_id: it.product_id,
        quantity: it.quantity,
        unit: it.unit,
        unit_price: it.unit_price / 100,
      })),
    });
    A.closeModal();
    A.state.docDraft = null;
    await A.reloadCatalog();
    A.state.documents = await A.api('listDocuments', A.state.docFilters);
    A.go('documents');
    A.toast('Document saved');
  });

  A.action('check-update', async () => {
    const status = document.getElementById('update-status');
    if (status) status.textContent = 'Checking for updates…';
    try {
      const res = await window.api.checkUpdate();
      if (!res.ok) throw new Error(res.error || 'Check failed');
      A.state.update = res;
      if (res.available) {
        const shown = res.channel === 'tes' && res.latestTag ? res.latestTag : `v${res.latest}`;
        if (status)
          status.innerHTML =
            `Update <b>${esc(shown)}</b> tersedia (${(Number(res.size || 0) / 1048576).toFixed(2)} MB). ` +
            `<button class="btn sm primary" data-action="apply-update">Download &amp; restart</button>`;
        A.toast(`Update ${shown} tersedia`, 'good');
      } else if (!res.compatible) {
        if (status) status.textContent = 'Update butuh runtime Electron baru. Download installer penuh.';
        A.toast('Update butuh versi runtime baru', 'warn');
      } else {
        if (status) status.textContent = `Sudah versi terbaru (${esc(res.current)}).`;
        A.toast('Aroklon sudah versi terbaru', 'good');
      }
    } catch (e) {
      if (status) status.textContent = 'Gagal memeriksa update: ' + e.message;
      A.toast('Gagal memeriksa update: ' + e.message, 'bad');
    }
  });

  A.action('apply-update', async () => {
    const ok = await A.confirm({
      title: 'Update & restart?',
      message:
        'Aplikasi akan ditutup, <b>resources/app.asar</b> diganti dengan versi terbaru, lalu Aroklon dibuka lagi. Data tetap aman.',
      confirmLabel: 'Update & restart',
    });
    if (!ok) return;
    try {
      const res = await window.api.applyUpdate();
      if (!res || !res.ok) throw new Error((res && res.error) || 'Apply failed');
    } catch (e) {
      A.toast('Update gagal: ' + e.message, 'bad', 5000);
    }
  });

  A.action('change:pref-theme', (ds, el) => {
    A.setTheme(el.value);
  });

  /* tax / tender / register delete actions */
  A.action('tax-delete', async (ds) => {
    const ok = await A.confirm({ title: 'Delete tax rate?', message: '', confirmLabel: 'Delete', danger: true });
    if (!ok) return;
    const res = await A.tryApi('deleteTaxRate', Number(ds.id), 'Could not delete');
    if (res && res.ok) {
      A.state.taxRates = await A.api('listTaxRates');
      A.render();
      A.toast('Tax rate deleted');
    }
  });

  A.action('tender-delete', async (ds) => {
    const ok = await A.confirm({ title: 'Delete payment type?', message: '', confirmLabel: 'Delete', danger: true });
    if (!ok) return;
    const res = await A.tryApi('deletePaymentType', Number(ds.id), 'Could not delete');
    if (res && res.ok) {
      A.state.paymentTypes = await A.api('listPaymentTypes');
      A.render();
      A.toast('Payment type deleted');
    }
  });

  /* ---------------------------------------------------------- doc search */

  A.action('input:doc-search', async (ds, el) => {
    const q = el.value.trim();
    if (!q) {
      A.state.docResults = [];
    } else {
      const list = (A.state.products || []).filter(
        (p) =>
          String(p.name).toLowerCase().includes(q.toLowerCase()) ||
          String(p.code || '').toLowerCase().includes(q.toLowerCase()) ||
          (p.barcodes || []).some((b) => String(b).toLowerCase().includes(q.toLowerCase()))
      );
      A.state.docResults = list.slice(0, 8);
    }
    const wrap = document.getElementById('doc-results');
    if (wrap) {
      wrap.innerHTML = A.state.docResults.length
        ? `<div class="card mb12" style="box-shadow:none"><div style="max-height:150px;overflow:auto">${A.state.docResults
            .map(
              (p) => `<button type="button" class="tree-item" data-action="doc-add-item" data-id="${p.id}" style="width:100%">
                <span style="width:22px;height:22px;border-radius:6px;background:${attr(p.color || '#e2e8f0')};display:inline-block"></span>
                <span class="strong">${esc(p.name)}</span><span class="spacer"></span>
                <span class="small muted mono">${money(A.state.docDraft.type === 'proforma' ? p.price : p.cost)}</span>
                <span class="badge gray">${U.qty(p.quantity)}</span></button>`
            )
            .join('')}</div></div>`
        : '';
    }
  });

  /* doc search updates only the results container, keeping the caret */
  A.action('input:doc-search', async (ds, el) => {
    const q = el.value.trim();
    if (!q) {
      A.state.docResults = [];
    } else {
      const list = (A.state.products || []).filter(
        (p) =>
          String(p.name).toLowerCase().includes(q.toLowerCase()) ||
          String(p.code || '').toLowerCase().includes(q.toLowerCase()) ||
          (p.barcodes || []).some((b) => String(b).toLowerCase().includes(q.toLowerCase()))
      );
      A.state.docResults = list.slice(0, 8);
    }
    const wrap = document.getElementById('doc-results');
    if (wrap) {
      const d = A.state.docDraft || { type: 'purchase' };
      wrap.innerHTML = A.state.docResults.length
        ? `<div class="card mb12" style="box-shadow:none"><div style="max-height:150px;overflow:auto">${A.state.docResults
            .map(
              (p) => `<button type="button" class="tree-item" data-action="doc-add-item" data-id="${p.id}" style="width:100%">
                <span style="width:22px;height:22px;border-radius:6px;background:${attr(p.color || '#e2e8f0')};display:inline-block"></span>
                <span class="strong">${esc(p.name)}</span><span class="spacer"></span>
                <span class="small muted mono">${money(d.type === 'proforma' ? p.price : p.cost)}</span>
                <span class="badge gray">${U.qty(p.quantity)}</span></button>`
            )
            .join('')}</div></div>`
        : '';
    }
  });

  /* keep customer search responsive */
  A.action('input:customer-search', (ds, el) => {
    A.state.customerSearch = el.value;
    const caret = el.selectionStart;
    A.render();
    const next = document.getElementById('customer-search');
    if (next) {
      next.focus();
      try {
        next.setSelectionRange(caret, caret);
      } catch (e) {}
    }
  });

  /* product name/code shortcuts in the product editor */
  A.action('form:save-country', async (form) => {
    const fd = new FormData(form);
    await A.api('saveCountry', {
      id: fd.get('id') ? Number(fd.get('id')) : null,
      name: String(fd.get('name') || '').trim(),
      code: String(fd.get('code') || ''),
      active: fd.get('active') === '1',
    });
    A.closeModal();
    A.state.countries = await A.api('listCountries');
    A.render();
    A.toast('Country saved');
  });
})();