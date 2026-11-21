'use strict';

/* ==================================================================== */
/*  Aroklon — screens: sales, day, dashboard, products, stock           */
/* ==================================================================== */

(function () {
  const A = window.App;
  const U = window.U;
  const { esc, attr, icon, money, moneyShort, qty, fmtDate, fmtTime, initials, t, locale } = U;

  /* ------------------------------------------------------------- helpers */

  const SH = (window.SH = {});

  SH.empty = (opts = {}) => `
    <div class="empty">
      <div class="ico">${icon(opts.icon || 'products', 26)}</div>
      <h3>${esc(opts.title || 'Nothing here yet')}</h3>
      ${opts.text ? `<p>${esc(opts.text)}</p>` : ''}
      ${opts.action || ''}
    </div>`;

  SH.field = (label, control, opts = {}) => `
    <div class="field ${opts.span === 2 ? 'span2' : ''}">
      <label>${label}</label>
      ${control}
      ${opts.hint ? `<div class="hint">${esc(opts.hint)}</div>` : ''}
    </div>`;

  SH.input = (name, value, opts = {}) => `
    <input class="input" name="${attr(name)}" id="${attr(opts.id || name)}" type="${opts.type || 'text'}"
      value="${attr(value == null ? '' : value)}"
      ${opts.required ? 'required' : ''}
      ${opts.disabled ? 'disabled' : ''}
      ${opts.placeholder ? `placeholder="${attr(opts.placeholder)}"` : ''}
      ${opts.step ? `step="${attr(opts.step)}"` : ''}
      ${opts.min != null ? `min="${attr(opts.min)}"` : ''}
      ${opts.max != null ? `max="${attr(opts.max)}"` : ''}
      ${opts.autofocus ? 'autofocus' : ''} />`;

  SH.select = (name, options, value, opts = {}) => {
    const optsHtml = options
      .map((o) => {
        const v = typeof o === 'object' ? o.value : o;
        const l = typeof o === 'object' ? o.label : o;
        const sel = String(v) === String(value == null ? '' : value) ? ' selected' : '';
        return `<option value="${attr(v)}"${sel}>${esc(l)}</option>`;
      })
      .join('');
    return `<select class="input" name="${attr(name)}" id="${attr(opts.id || name)}" ${opts.disabled ? 'disabled' : ''} ${
      opts.dataChange ? `data-change="${attr(opts.dataChange)}"` : ''
    } ${opts.dataKey ? `data-key="${attr(opts.dataKey)}"` : ''}>${optsHtml}</select>`;
  };

  SH.checkbox = (name, label, checked) => `
    <label class="switch"><input type="checkbox" name="${attr(name)}" value="1" ${checked ? 'checked' : ''} />
      <span class="track"></span><span>${label}</span></label>`;

  SH.modalHead = (title, sub = '', right = '') => `
    <div class="modal-head">
      <div><h2>${esc(title)}</h2>${sub ? `<div class="sub">${esc(sub)}</div>` : ''}</div>
      <div class="right">${right || `<button type="button" class="icon-btn" data-modal-cancel>${icon('close', 18)}</button>`}</div>
    </div>`;

  SH.modalFoot = (primary, opts = {}) => `
    <div class="modal-foot">
      <button type="button" class="btn danger" data-modal-cancel>${esc(opts.cancel || 'Cancel')}</button>
      ${opts.extra || ''}
      <button type="submit" class="btn ${opts.danger ? 'danger' : opts.okClass || 'good'}">${primary}</button>
    </div>`;

  SH.badge = (text, kind = 'gray') => `<span class="badge ${kind}">${text}</span>`;

  SH.docTypeLabel = (type) =>
    t(
      {
        sale: 'Sale',
        refund: 'Refund',
        purchase: 'Purchase',
        stock_return: 'Stock return',
        count: 'Inventory count',
        loss: 'Loss & damage',
        proforma: 'Proforma',
        credit: 'Credit payment',
      }[type] || type
    );

  SH.docTypeBadge = (t) => {
    const map = {
      sale: 'good',
      refund: 'warn',
      purchase: 'info',
      stock_return: 'info',
      count: 'info',
      loss: 'bad',
      proforma: 'gray',
      credit: 'gray',
    };
    return SH.badge(SH.docTypeLabel(t), map[t] || 'gray');
  };

  /* ------------------------------------------------------------ receipt */

  SH.receiptPaper = (doc, company) => {
    const c = company || {};
    const items = doc.items || [];
    const payments = doc.payments || [];
    const lines = items
      .map((it) => {
        const price = (it.unit_price || 0) / 100;
        const total = (it.total || 0) / 100;
        const disc = (it.discount_total || 0) / 100;
        return `<div style="margin:3px 0">
          <div>${esc(it.name_snapshot)}</div>
          <div class="r-line r-muted"><span>${U.qty(it.quantity)} × ${price.toLocaleString(locale())}</span><span>${total.toLocaleString(
            locale()
          )}</span></div>
          ${disc ? `<div class="r-line r-muted" style="font-size:10px"><span>${esc(t('Discount'))}</span><span>-${disc.toLocaleString(locale())}</span></div>` : ''}
        </div>`;
      })
      .join('');

    const payLines = payments
      .map(
        (p) => `<div class="r-line"><span>${esc(p.payment_name || 'Payment')}</span><span>${(
          p.amount / 100
        ).toLocaleString(locale())}</span></div>`
      )
      .join('');

    const money0 = (v) => (v / 100).toLocaleString(locale());

    return `<div class="receipt-paper">
      <h3>${esc(c.name || 'Aroklon')}</h3>
      ${c.address ? `<div class="r-center r-muted">${esc(c.address)}</div>` : ''}
      ${c.phone ? `<div class="r-center r-muted">${esc(c.phone)}</div>` : ''}
      <hr />
      ${c.receipt_header ? `<div class="r-center">${esc(c.receipt_header)}</div>` : ''}
      <div class="r-line"><span>${esc(t('Receipt'))}</span><span>${esc(doc.number || '')}</span></div>
      <div class="r-line"><span>${esc(t('Date'))}</span><span>${esc(fmtDate(doc.doc_date, true))}</span></div>
      ${doc.user ? `<div class="r-line"><span>${esc(t('Cashier'))}</span><span>${esc(doc.user.name)}</span></div>` : ''}
      ${doc.customer && !['Walk-in Customer', 'Pelanggan Umum'].includes(doc.customer.name) ? `<div class="r-line"><span>${esc(t('Customer'))}</span><span>${esc(doc.customer.name)}</span></div>` : ''}
      <hr />
      ${lines || `<div class="r-center r-muted">${esc(t('No items'))}</div>`}
      <hr />
      <div class="r-line"><span>${esc(t('Subtotal'))}</span><span>${money0(doc.subtotal)}</span></div>
      ${doc.discount_total ? `<div class="r-line"><span>${esc(t('Discount'))}</span><span>-${money0(doc.discount_total)}</span></div>` : ''}
      ${doc.tax_total ? `<div class="r-line"><span>${esc(t('Tax'))}</span><span>${money0(doc.tax_total)}</span></div>` : ''}
      ${doc.rounding_total ? `<div class="r-line"><span>${esc(t('Rounding'))}</span><span>${doc.rounding_total > 0 ? '+' : '-'}${money0(Math.abs(doc.rounding_total))}</span></div>` : ''}
      <div class="r-line r-total"><span>${esc(t('TOTAL'))}</span><span>${money0(doc.total)}</span></div>
      <hr />
      ${payLines}
      ${doc.change_total ? `<div class="r-line"><span>${esc(t('Change'))}</span><span>${money0(doc.change_total)}</span></div>` : ''}
      <hr />
      <div class="r-center r-muted">${esc(c.receipt_footer || t('Thank you for your purchase!'))}</div>
      <div class="r-center r-muted" style="margin-top:6px">${esc(t('Powered by Aroklon'))}</div>
    </div>`;
  };

  SH.receiptHtml = (doc, company) => {
    const c = company || {};
    const items = doc.items || [];
    const payments = doc.payments || [];
    const row = (l, r) => `<div class="r-line"><span>${l}</span><span>${r}</span></div>`;
    const money0 = (v) => (v / 100).toLocaleString(locale());
    const body = `
      <div class="r-center"><h2>${esc(c.name || 'Aroklon')}</h2>
        ${c.address ? `<div>${esc(c.address)}</div>` : ''}
        ${c.phone ? `<div>${esc(c.phone)}</div>` : ''}
      </div>
      <hr/>
      ${c.receipt_header ? `<div class="r-center">${esc(c.receipt_header)}</div>` : ''}
      ${row(esc(t('Receipt')), esc(doc.number || ''))}
      ${row(esc(t('Date')), esc(fmtDate(doc.doc_date, true)))}
      ${doc.user ? row(esc(t('Cashier')), esc(doc.user.name)) : ''}
      ${doc.customer ? row(esc(t('Customer')), esc(doc.customer.name)) : ''}
      <hr/>
      ${items
        .map(
          (it) =>
            `<div style="margin:4px 0"><div>${esc(it.name_snapshot)}</div>
             <div class="r-line muted"><span>${U.qty(it.quantity)} x ${money0(it.unit_price)}</span><span>${money0(it.total)}</span></div>
             ${it.discount_total ? `<div class="r-line muted"><span>${esc(t('Discount'))}</span><span>-${money0(it.discount_total)}</span></div>` : ''}
            </div>`
        )
        .join('')}
      <hr/>
      ${row(esc(t('Subtotal')), money0(doc.subtotal))}
      ${doc.discount_total ? row(esc(t('Discount')), '-' + money0(doc.discount_total)) : ''}
      ${doc.tax_total ? row(esc(t('Tax')), money0(doc.tax_total)) : ''}
      ${doc.rounding_total ? row(esc(t('Rounding')), `${doc.rounding_total > 0 ? '+' : '-'}${money0(Math.abs(doc.rounding_total))}`) : ''}
      <div class="r-line total"><span>${esc(t('TOTAL'))}</span><span>${money0(doc.total)}</span></div>
      <hr/>
      ${payments.map((p) => row(esc(p.payment_name || t('Payment')), money0(p.amount))).join('')}
      ${doc.change_total ? row(esc(t('Change')), money0(doc.change_total)) : ''}
      <hr/>
      <div class="r-center muted">${esc(c.receipt_footer || t('Thank you for your purchase!'))}</div>
      <div class="r-center muted" style="margin-top:8px">${esc(t('Powered by Aroklon'))}</div>`;
    return `<!doctype html><html><head><meta charset="utf-8"><style>
      @page { size: 80mm auto; margin: 4mm; }
      * { box-sizing: border-box; }
      body { font-family: "Consolas", "Courier New", monospace; font-size: 11.5px; color:#111; margin:0; padding:6px 4px; width:72mm; }
      h2 { font-size:15px; margin:0 0 2px; letter-spacing:1px; }
      .r-center { text-align:center; }
      .r-line { display:flex; justify-content:space-between; gap:8px; }
      .muted { color:#555; }
      .total { font-weight:700; font-size:13.5px; }
      hr { border:0; border-top:1px dashed #888; margin:7px 0; }
      table { width:100%; border-collapse:collapse; }
    </style></head><body>${body}</body></html>`;
  };

  SH.print = async (html, silent = true) => {
    try {
      const r = await window.api.print(html, { silent });
      if (!r.ok) A.toast('Print failed: ' + (r.reason || 'unknown'), 'bad');
      return r;
    } catch (e) {
      A.toast('Print failed: ' + e.message, 'bad');
      return { ok: false };
    }
  };

  /* -------------------------------------------------------------- sales */

  function cartCalc() {
    const c = A.state.cart;
    return U.computeCart(c.items, activeDiscount(), c.tax_override, c.rounding);
  }

  function activeDiscount() {
    const c = A.state.cart;
    const cust = (A.state.customers || []).find((x) => String(x.id) === String(c.customer_id));
    if (cust && cust.discount_type && U.num(cust.discount_value) > 0) {
      return { type: cust.discount_type, value: cust.discount_value, customer: cust.name };
    }
    return c.discount;
  }

  function filteredProducts() {
    let list = A.state.products || [];
    if (A.state.groupFilter) {
      const gid = Number(A.state.groupFilter);
      const childIds = A.state.groups.filter((g) => String(g.parent_id) === String(gid)).map((g) => g.id);
      list = list.filter((p) => p.group_id === gid || childIds.includes(p.group_id));
    }
    const q = (A.state.catalogSearch || '').trim().toLowerCase();
    if (q) {
      list = list.filter(
        (p) =>
          String(p.name).toLowerCase().includes(q) ||
          String(p.code || '').toLowerCase().includes(q) ||
          (p.barcodes || []).some((b) => String(b).toLowerCase().includes(q))
      );
    }
    return list;
  }

  function tileHtml(p) {
    const stock = p.is_service ? null : U.num(p.quantity);
    const low = !p.is_service && p.low_stock > 0 && stock <= p.low_stock;
    const swatch = p.image
      ? `<img src="${attr(p.image)}" alt="" />`
      : icon('products', 24);
    return `<button class="tile ${stock != null && stock <= 0 ? 'out' : ''}" data-action="pos-add" data-id="${p.id}" title="${attr(
      p.name
    )}">
      <div class="swatch" style="background:${attr(p.color || '#e2e8f0')}">${swatch}</div>
      <span class="t-stock">${p.is_service ? 'service' : `${U.qty(stock)} ${esc(p.unit || '')}`}</span>
      <div class="tile-body">
        <div class="t-name">${esc(p.name)}</div>
        <div class="t-code">${esc(p.code || '')}${low ? ' · LOW' : ''}</div>
        <div class="t-price">${money(p.price)}</div>
      </div>
    </button>`;
  }

  A.screens.sales = {
    meta: { title: t('Sales'), sub: t('Ring up a sale') },
    flush: true,
    render() {
      const cart = A.state.cart;
      const calc = cartCalc();
      const disc = activeDiscount();
      const customer = (A.state.customers || []).find((c) => String(c.id) === String(cart.customer_id));
      const products = filteredProducts();
      const groups = A.state.groups || [];
      const held = localStorage.getItem('aroklon.held');

      return `<div class="pos">
        <section class="catalog">
          <div class="catalog-head">
            <div class="search-row">
              <div class="search-box">
                ${icon('search', 18)}
                <input id="pos-search" class="input" data-input="pos-search" placeholder="${esc(
                  t('Search name, code or barcode…')
                )}" value="${attr(A.state.catalogSearch)}" autocomplete="off" spellcheck="false" />
              </div>
              <div class="search-modes">
                ${['name', 'code', 'barcode']
                  .map(
                    (m) =>
                      `<button class="${A.state.catalogMode === m ? 'active' : ''}" data-action="pos-mode" data-mode="${m}">${esc(
                        t(m[0].toUpperCase() + m.slice(1))
                      )}</button>`
                  )
                  .join('')}
              </div>
              <button class="btn icon-only" data-action="pos-focus" title="Search (F3)">${icon('search', 18)}</button>
            </div>
            <div class="chips">
              <button class="chip-filter ${!A.state.groupFilter ? 'active' : ''}" data-action="pos-group" data-id="">${esc(t('All'))}</button>
              ${groups
                .map(
                  (g) =>
                    `<button class="chip-filter ${String(A.state.groupFilter) === String(g.id) ? 'active' : ''}" data-action="pos-group" data-id="${
                      g.id
                    }">${esc(g.name)}</button>`
                )
                .join('')}
            </div>
          </div>
          <div class="catalog-body">
            ${
              products.length
                ? `<div class="product-grid" id="product-grid">${products.map(tileHtml).join('')}</div>`
                : SH.empty({
                    icon: 'search',
                    title: t('No products found'),
                    text: A.state.catalogSearch
                      ? t('Nothing matches “{q}”.', { q: A.state.catalogSearch })
                      : t('Add your first product to start selling.'),
                    action: `<button class="btn primary" data-action="nav" data-route="products">${icon('plus', 15)} ${esc(
                      t('Add product')
                    )}</button>`,
                  })
            }
          </div>
        </section>

        <aside class="order">
          <div class="order-head">
            <div class="title">${esc(t('Current sale'))}<small>${esc(t('New sale'))} · ${esc(
              (A.state.registers || []).find((r) => r.is_primary)?.name || t('Register')
            )}</small></div>
            <div class="right">
              ${
                held
                  ? `<button class="btn sm" data-action="pos-resume">${icon('refresh', 14)} ${esc(t('Resume'))}</button>`
                  : `<button class="btn sm" data-action="pos-hold" title="${esc(t('Hold'))}">${icon('save', 14)} ${esc(t('Hold'))}</button>`
              }
              <button class="icon-btn" data-action="pos-clear" title="${esc(t('Clear'))}">${icon('trash', 17)}</button>
            </div>
          </div>

          <div class="order-lines" id="order-lines">
            ${
              cart.items.length
                ? cart.items
                    .map((it, i) => {
                      const c = calc.lines[i] || {};
                      return `<div class="line ${A.state.selectedLine === i ? 'selected' : ''}" data-action="pos-select-line" data-index="${i}">
                        <div>
                          <div class="l-name">${esc(it.name)}</div>
                          <div class="l-meta">
                            <span class="qty-badge">${U.qty(it.quantity)} × ${money(it.price)}</span>
                            ${it.discount_type ? `<span class="badge good" style="padding:1px 6px;font-size:10px">${esc(it.discount_type)} discount</span>` : ''}
                          </div>
                        </div>
                        <div class="l-total">${money(c.total != null ? c.total : Math.round(it.quantity * it.price))}<small>${esc(it.unit || '')}</small></div>
                      </div>`;
                    })
                    .join('')
                : `<div class="cart-empty">${icon('sales', 30)}<div>${esc(t('No items yet'))}<br /><span class="small">${esc(
                    t('Scan, search or tap a product')
                  )}</span></div></div>`
            }
          </div>

          ${
            A.state.selectedLine != null && cart.items[A.state.selectedLine]
              ? `<div class="row gap8" style="padding:8px 16px;border-top:1px solid var(--line)">
                  <span class="small muted">${esc(t('Qty'))}</span>
                  <button class="btn sm icon-only" data-action="pos-qty" data-delta="-1" title="${esc(t('Decrease'))}">${icon('minus', 14)}</button>
                  <span class="strong mono">${U.qty(cart.items[A.state.selectedLine].quantity)}</span>
                  <button class="btn sm icon-only" data-action="pos-qty" data-delta="1" title="${esc(t('Increase'))}">${icon('plus', 14)}</button>
                  <button class="btn sm" data-action="pos-item-discount">${icon('tag', 14)} ${esc(t('Discount'))}</button>
                  <span class="spacer"></span>
                  <button class="btn sm" data-action="pos-remove-line" style="color:var(--bad)">${icon('trash', 14)} ${esc(t('Remove'))}</button>
                </div>`
              : ''
          }

          <div class="order-foot">
            <div class="sum-row"><span>${esc(t('Subtotal'))}</span><span class="mono">${money(calc.subtotal)}</span></div>
            ${
              calc.promotion_name
                ? `<div class="sum-row discount promo"><span>${icon('heart', 13)} ${esc(calc.promotion_name)}</span><span class="mono">-${money(
                    calc.promotion_total
                  )}</span></div>`
                : ''
            }
            ${
              disc.value > 0
                ? `<div class="sum-row discount"><span>${esc(t('Discount'))} (${esc(disc.type)}${
                    disc.customer ? ' · ' + esc(disc.customer) : ''
                  })</span><span class="mono">-${money(calc.discount_total - calc.promotion_total)}</span></div>`
                : ''
            }
            <div class="sum-row"><span>${esc(t('Tax'))}</span><span class="mono">${money(calc.tax_total)}</span></div>
            ${
              calc.rounding_total
                ? `<div class="sum-row"><span>${esc(t('Rounding'))}</span><span class="mono">${calc.rounding_total > 0 ? '+' : '-'}${money(
                    Math.abs(calc.rounding_total)
                  )}</span></div>`
                : ''
            }
            <div class="sum-row total"><span>${esc(t('Total'))}</span><span class="mono">${money(calc.total)}</span></div>
            <div class="row gap8" style="margin-top:10px">
              <button class="btn sm" data-action="pos-customer" style="max-width:190px;overflow:hidden;text-overflow:ellipsis">
                ${icon('customers', 14)} ${customer ? esc(customer.name) : esc(t('Add customer'))}
              </button>
              <span class="spacer"></span>
              <button class="btn sm" data-action="pos-discount" title="${esc(t('Discount'))} (F2)">${icon('tag', 14)} ${esc(t('Discount'))}</button>
              <button class="btn sm" data-action="pos-refund" title="${esc(t('Refund'))}">${icon('back', 14)} ${esc(t('Refund'))}</button>
            </div>
            <button class="charge-btn" data-action="pos-pay" ${cart.items.length ? '' : 'disabled'}>
              ${icon('cash', 20)} ${esc(t('Charge'))} ${money(calc.total)} <span class="k">F10</span>
            </button>
          </div>
        </aside>
      </div>`;
    },
  };

  /* --------------------------------------------------------- cart actions */

  async function addToCart(product, quantity) {
    const cart = A.state.cart;
    const existing = cart.items.find((i) => String(i.product_id) === String(product.id));
    if (existing) {
      existing.quantity = U.num(existing.quantity) + quantity;
      A.state.selectedLine = cart.items.indexOf(existing);
    } else {
      cart.items.push({
        product_id: product.id,
        group_id: product.group_id || null,
        name: product.name,
        code: product.code,
        price: product.price,
        cost: product.cost,
        unit: product.unit,
        color: product.color,
        image: product.image,
        is_service: !!product.is_service,
        price_incl_tax: !!product.price_incl_tax,
        taxes: product.taxes || [],
        quantity,
        discount_type: null,
        discount_value: 0,
      });
      A.state.selectedLine = cart.items.length - 1;
    }
    A.render();
  }

  A.action('nav', (ds) => {
    A.go(ds.route, ds.params ? JSON.parse(ds.params) : {});
  });

  A.action('nav-settings', (ds) => {
    A.state.settingsTab = ds.tab || 'company';
    A.go('settings', { tab: ds.tab || 'company' });
  });

  A.action('toggle-theme', () => {
    A.setTheme(A.state.theme === 'classic' ? 'modern' : 'classic');
  });

  A.action('toggle-mode', () => {
    A.toggleMode();
  });

  A.action('change:login-lang', (ds, el) => {
    A.setLanguage(el.value);
  });

  A.action('change:pref-language', (ds, el) => {
    A.setLanguage(el.value);
  });

  A.action('change:pref-layout', (ds, el) => {
    A.setTheme(el.value);
  });

  A.action('change:pref-mode', (ds, el) => {
    A.setMode(el.value);
  });

  A.action('shortcuts', () => {
    A.toast('F2 discount · F3 search · F4 quantity · F8 hold · F10 payment · F12 quick pay · Del remove', 'good', 7000);
  });

  A.action('about', () => {
    A.toast(
      'Aroklon v' + ((A.state.meta && A.state.meta.version) || '1.0.0') + ' — offline Windows POS. Data lokal, tanpa akun.',
      'good',
      5000
    );
  });

  A.action('logout', async () => {
    const ok = await A.confirm({
      title: 'Sign out?',
      message: 'The current sale will be kept on this till.',
      confirmLabel: 'Sign out',
      danger: true,
    });
    if (ok) A.logout();
  });

  A.action('pos-mode', (ds) => {
    A.state.catalogMode = ds.mode;
    A.render();
  });

  A.action('pos-group', (ds) => {
    A.state.groupFilter = ds.id ? Number(ds.id) : null;
    A.render();
  });

  A.action('pos-focus', () => {
    const el = document.getElementById('pos-search');
    if (el) {
      el.focus();
      el.select();
    }
  });

  A.action('input:pos-search', (ds, el) => {
    A.state.catalogSearch = el.value;
    const caret = el.selectionStart;
    A.render();
    const next = document.getElementById('pos-search');
    if (next) {
      next.focus();
      try {
        next.setSelectionRange(caret, caret);
      } catch (e) {}
    }
  });

  A.action('pos-add', async (ds) => {
    const p = (A.state.products || []).find((x) => String(x.id) === String(ds.id));
    if (!p) return;
    if (!p.active) return A.toast('This product is inactive', 'warn');
    if (p.age_restricted) {
      const ok = await A.confirm({
        title: 'Age restricted item',
        message: `<p class="small">Confirm the customer is old enough to buy <b>${esc(p.name)}</b>.</p>`,
        confirmLabel: 'Confirmed',
      });
      if (!ok) return;
    }
    let quantity = 1;
    if (!p.default_qty && A.state.settings.ask_qty !== '0') {
      const res = await A.prompt({
        title: 'Quantity',
        label: p.name,
        value: '1',
        type: 'number',
        confirmLabel: 'Add to sale',
      });
      if (res == null) return;
      quantity = U.num(res, 0);
      if (quantity <= 0) return A.toast('Quantity must be greater than zero', 'bad');
    }
    await addToCart(p, quantity);
  });

  A.action('pos-select-line', (ds) => {
    A.state.selectedLine = Number(ds.index);
    A.render();
  });

  A.action('pos-qty', (ds) => {
    const i = A.state.selectedLine;
    if (i == null || !A.state.cart.items[i]) return;
    const delta = Number(ds.delta);
    const next = U.num(A.state.cart.items[i].quantity) + delta;
    if (next <= 0) A.state.cart.items.splice(i, 1);
    else A.state.cart.items[i].quantity = next;
    if (A.state.selectedLine >= A.state.cart.items.length) A.state.selectedLine = null;
    A.render();
  });

  A.action('pos-remove-line', () => {
    const i = A.state.selectedLine;
    if (i == null) return;
    A.state.cart.items.splice(i, 1);
    A.state.selectedLine = null;
    A.render();
  });

  A.action('pos-clear', async () => {
    if (!A.state.cart.items.length) return;
    const ok = await A.confirm({ title: 'Clear this sale?', message: 'All items will be removed.', confirmLabel: 'Clear', danger: true });
    if (!ok) return;
    A.state.cart = { items: [], customer_id: null, discount: { type: 'percent', value: 0 }, rounding: 0, notes: '' };
    A.state.selectedLine = null;
    A.render();
    A.toast('Sale cleared');
  });

  A.action('pos-hold', () => {
    if (!A.state.cart.items.length) return A.toast('Nothing to hold', 'warn');
    localStorage.setItem('aroklon.held', JSON.stringify(A.state.cart));
    A.state.cart = { items: [], customer_id: null, discount: { type: 'percent', value: 0 }, rounding: 0, notes: '' };
    A.state.selectedLine = null;
    A.render();
    A.toast('Sale put on hold');
  });

  A.action('pos-resume', () => {
    const raw = localStorage.getItem('aroklon.held');
    if (!raw) return;
    try {
      A.state.cart = JSON.parse(raw);
      localStorage.removeItem('aroklon.held');
      A.state.selectedLine = null;
      A.render();
      A.toast('Sale resumed');
    } catch (e) {
      localStorage.removeItem('aroklon.held');
      A.toast('Could not resume the held sale', 'bad');
    }
  });

  A.action('pos-customer', () => openCustomerPicker());
  A.action('pos-discount', () => openDiscount());
  A.action('pos-item-discount', () => openDiscount(true));
  A.action('pos-pay', () => startPayment());
  A.action('pos-refund', () => openRefund());

  /* --------------------------------------------------------- customer picker */

  function openCustomerPicker() {
    const customers = A.state.customers || [];
    A.modal(
      `<div class="modal-head"><div><h2>${esc(t('Select customer'))}</h2><div class="sub">${esc(
        t('Attach a customer to this sale')
      )}</div></div><div class="right"><button type="button" class="icon-btn" data-modal-cancel>${icon(
        'close',
        18
      )}</button></div></div>
      <div class="modal-body">
        <div class="row gap8 mb12">
          <div class="search-box">${icon('search', 16)}<input id="cust-search" class="input" placeholder="${esc(
            t('Search name or phone…')
          )}" /></div>
          ${
            A.state.cart.customer_id
              ? `<button class="btn sm" data-action="pos-remove-customer">${icon('close', 14)} ${esc(t('Walk-in'))}</button>`
              : ''
          }
        </div>
        <div id="cust-list" style="max-height:360px;overflow:auto">
          ${customers
            .map(
              (c) => `<button type="button" class="tree-item" data-action="pick-customer" data-id="${c.id}" style="width:100%;margin-bottom:2px">
                <span class="avatar" style="width:28px;height:28px;border-radius:8px;font-size:11px">${esc(initials(c.name))}</span>
                <span><span class="strong">${esc(c.name)}</span><br /><span class="small muted">${esc(c.phone || c.email || '')}</span></span>
                ${c.card_count ? `<span class="badge info" style="margin-left:auto">${esc(t('{n} cards', { n: c.card_count }))}</span>` : ''}
              </button>`
            )
            .join('')}
        </div>
      </div>`,
      { size: '' }
    );
  }

  A.action('pick-customer', (ds) => {
    A.state.cart.customer_id = Number(ds.id);
    A.closeModal();
    A.render();
    A.toast('Customer added to sale');
    if (A.state.pay) drawPayment();
  });

  A.action('pos-remove-customer', () => {
    A.state.cart.customer_id = null;
    A.closeModal();
    A.render();
    if (A.state.pay) drawPayment();
  });

  /* ------------------------------------------------------------- discount */

  function openDiscount(itemMode = false) {
    const cart = A.state.cart;
    if (!cart.items.length) return A.toast(t('Add items first'), 'warn');
    const ad = activeDiscount();
    if (ad.customer) {
      return A.toast(
        t('{customer} has a preset {value}{suffix} discount. Remove the customer to add another.', {
          customer: ad.customer,
          value: ad.value,
          suffix: ad.type === 'percent' ? '%' : '',
        }),
        'warn',
        4200
      );
    }
    const selectedIndex = A.state.selectedLine;
    const lines = cart.items.map((it, i) => ({ value: i, label: `${it.name} — ${money(it.price)}` }));
    A.modal(
      `<form data-form="discount">
        ${SH.modalHead(t('Discount'))}
        <div class="modal-body">
          <div class="field mb12"><label>${esc(t('Apply to'))}</label>
            <div class="row gap8">
              <label class="switch"><input type="radio" name="scope" value="cart" ${!itemMode ? 'checked' : ''} style="display:none"><span class="track"></span><span>${esc(
                t('Whole sale')
              )}</span></label>
              <label class="switch"><input type="radio" name="scope" value="item" ${itemMode ? 'checked' : ''} style="display:none"><span class="track"></span><span>${esc(
                t('Selected item')
              )}</span></label>
            </div>
          </div>
          <div class="form-grid">
            ${SH.field(t('Item'), SH.select('line_index', lines, selectedIndex != null ? selectedIndex : 0), { span: 2 })}
            ${SH.field(t('Type'), SH.select('discount_type', [
              { value: 'percent', label: t('Percentage (%)') },
              { value: 'fixed', label: t('Fixed amount') },
            ], cart.discount.type))}
            ${SH.field(t('Value'), SH.input('discount_value', cart.discount.value, { type: 'number', step: '0.01', min: '0', placeholder: '0' }))}
            ${SH.field(t('Tax override (%, blank = normal)'), SH.input('tax_override', cart.tax_override != null ? cart.tax_override : '', { type: 'number', step: '0.01', min: '0', placeholder: t('Use product tax') }), { span: 2, hint: t('Applies to the whole sale. Leave blank to use each product’s tax rate.') })}
          </div>
          <p class="hint mt12">${esc(t('Tax is calculated after discounts. A customer with a preset discount cannot receive an extra discount.'))}</p>
        </div>
        ${SH.modalFoot(t('Apply discount'))}
      </form>`,
      { size: '' }
    );
  }

  A.action('tab', (ds) => {
    if (String(ds.target).startsWith('set-')) A.state.settingsTab = ds.target.slice(4);
    document.querySelectorAll('.tab').forEach((t) => t.classList.remove('active'));
    const btn = document.querySelector(`.tab[data-target="${ds.target}"]`);
    if (btn) btn.classList.add('active');
    document.querySelectorAll('[data-tab-panel]').forEach((p) => {
      p.style.display = p.dataset.tabPanel === ds.target ? '' : 'none';
    });
  });

  /* ------------------------------------------------------------- payment */

  function activeTender() {
    return (A.state.paymentTypes || []).find((p) => String(p.id) === String(A.state.pay.tender_id));
  }

  function payCalc() {
    const calc = cartCalc();
    const total = calc.total;
    const pay = A.state.pay;
    const splitTotal = pay.split.reduce((s, x) => s + x.amount, 0);
    const remaining = Math.max(total - splitTotal, 0);
    return { total, splitTotal, remaining, rounding: calc.rounding_total };
  }

  function startPayment() {
    if (!A.state.day || !A.state.day.register_open) {
      A.toast(t('Register is closed. Open the register before selling.'), 'warn', 4200);
      A.go('day');
      return;
    }
    const calc = cartCalc();
    if (!calc.total) return A.toast('Add items first', 'warn');
    const cash = (A.state.paymentTypes || []).find((p) => p.kind === 'cash') || (A.state.paymentTypes || [])[0];
    A.state.pay = {
      tender_id: cash ? cash.id : null,
      tendered: calc.total,
      split: [],
      mode: 'single',
    };
    drawPayment();
  }

  A.drawPayment = drawPayment;

  function drawPayment() {
    const pay = A.state.pay;
    const { total, remaining, rounding } = payCalc();
    const tender = activeTender();
    const due = pay.mode === 'split' ? remaining : total;
    const tendered = pay.tendered;
    const change = Math.max(tendered - due, 0);
    const canCharge = pay.mode === 'split' ? remaining <= 0 : tendered >= due;

    const tenderButtons = (A.state.paymentTypes || [])
      .filter((p) => p.active)
      .map(
        (p) => `<button type="button" class="tender ${String(p.id) === String(pay.tender_id) ? 'active' : ''}" data-action="pay-tender" data-id="${p.id}">
          <span class="dot" style="background:${attr(p.color || '#64748b')}"></span>${esc(p.name)}
        </button>`
      )
      .join('');

    const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '00', '0', 'back'];
    const keypad = keys
      .map((k) =>
        k === 'back'
          ? `<button type="button" class="key" data-action="pay-key" data-key="back">${icon('back', 16)}</button>`
          : `<button type="button" class="key" data-action="pay-key" data-key="${k}">${k}</button>`
      )
      .join('');

    const quick = [due, Math.ceil(total / 10000) * 10000, Math.ceil(total / 50000) * 50000, Math.ceil(total / 100000) * 100000]
      .filter((v, i, arr) => v > 0 && arr.indexOf(v) === i)
      .slice(0, 4)
      .map((v) => `<button type="button" data-action="pay-quick" data-amount="${v}">${money(v)}</button>`)
      .join('');

    const splitList = pay.split.length
      ? `<div class="split-list">${pay.split
          .map(
            (s, i) =>
              `<div class="split-row"><span>${esc(
                ((A.state.paymentTypes || []).find((p) => String(p.id) === String(s.payment_type_id)) || {}).name || t('Payment')
              )}</span><span class="strong mono">${money(s.amount)} <button type="button" class="rm" data-action="pay-split-remove" data-index="${i}">${esc(
                t('Remove')
              )}</button></span></div>`
          )
          .join('')}</div>`
      : '';

    A.modal(
      `<div class="modal-head pay-head">
        <div><h2>${esc(t('Actions'))}</h2><div class="sub">${esc(pay.mode === 'split' ? t('Split payment') : t('Take payment'))}</div></div>
        <div class="right"><button type="button" class="icon-btn" data-modal-cancel>${icon('close', 18)}</button></div>
      </div>
      <div class="modal-body pay-body">
        <div class="pay-actions">
          <button type="button" class="pay-action" data-modal-cancel>${icon('close', 18)}<span>${esc(t('Cancel'))}</span></button>
          <button type="button" class="pay-action" data-action="pay-discount">${icon('percent', 18)}<span>${esc(t('Discount'))}</span></button>
          <button type="button" class="pay-action" data-action="pay-rounds">${icon('refresh', 18)}<span>${esc(t('Rounds'))}</span></button>
          <button type="button" class="pay-action" data-action="pos-customer">${icon('customers', 18)}<span>${esc(t('Customer'))}</span></button>
        </div>
        <div class="pay2">
          <div class="pay2-types">
            <div class="label">${esc(t('Payment type'))}</div>
            ${tenderButtons}
            <button type="button" class="tender split-tile ${pay.mode === 'split' ? 'active' : ''}" data-action="${
        pay.mode === 'split' ? 'pay-unsplit' : 'pay-split'
      }">
              ${icon('split', 17)} ${esc(pay.mode === 'split' ? t('Single payment') : t('Split payments'))}
            </button>
            ${splitList}
          </div>
          <div class="pay2-sheet">
            <div class="label">${esc(t('Payment'))}</div>
            ${rounding ? `<div class="pay2-row"><span>${esc(t('Rounding'))}:</span><b class="${rounding > 0 ? 'green' : 'blue'}">${rounding > 0 ? '+' : '-'}${money(Math.abs(rounding))}</b></div>` : ''}
            <div class="pay2-row"><span>${esc(t('Total'))}:</span><b class="blue">${money(total)}</b></div>
            <div class="pay2-row"><span>${esc(t('Paid'))}:</span><b class="paid">${money(tendered)}</b></div>
            <div class="pay2-row"><span>${esc(t('Change'))}:</span><b class="green">${money(change)}</b></div>
            <div class="quick-cash mt12">${quick}</div>
            <div class="keypad mt12">${keypad}</div>
            ${
              pay.mode === 'split'
                ? `<button type="button" class="btn sm primary block mt12" data-action="pay-split-add">${icon('plus', 14)} ${esc(
                    t('Add tender')
                  )}</button>`
                : ''
            }
          </div>
        </div>
      </div>
      <div class="modal-foot pay-foot">
        <button type="button" class="btn good lg" data-action="pay-confirm" ${canCharge ? '' : 'disabled'}>
          ${icon('check', 16)} ${esc(t('Continue'))}
        </button>
        <button type="button" class="btn danger lg" data-modal-cancel>${icon('close', 16)} ${esc(t('Cancel'))}</button>
      </div>`,
      { size: 'wide' }
    );
  }

  A.action('pay-tender', (ds) => {
    A.state.pay.tender_id = Number(ds.id);
    drawPayment();
  });

  A.action('pay-discount', () => {
    if (!A.state.pay) return;
    A.closeModal();
    openDiscount();
  });

  function openRounding() {
    const cart = A.state.cart;
    if (!cart.items.length) return A.toast(t('Add items first'), 'warn');
    const calc = cartCalc();
    const inc = Math.max(Math.round(U.num(cart.rounding)), 0);
    const rounded = inc
      ? Math.max(inc, Math.round(calc.total_before_rounding / inc) * inc)
      : calc.total_before_rounding;
    const diff = rounded - calc.total_before_rounding;
    const options = [
      { value: '0', label: t('No rounding') },
      { value: '10000', label: t('Nearest {amount}', { amount: money(10000) }) },
      { value: '50000', label: t('Nearest {amount}', { amount: money(50000) }) },
      { value: '100000', label: t('Nearest {amount}', { amount: money(100000) }) },
    ];
    A.modal(
      `<form data-form="rounding">
        ${SH.modalHead(t('Rounds'), t('Round the cash total to a simpler amount'))}
        <div class="modal-body">
          <div class="form-grid">
            ${SH.field(t('Round to'), SH.select('round_to', options, String(inc), { dataChange: 'round-preview' }), { span: 2 })}
          </div>
          <div class="round-summary">
            <div class="round-row"><span>${esc(t('Current total'))}</span><b class="mono">${money(calc.total_before_rounding)}</b></div>
            <div class="round-row"><span>${esc(t('Rounded total'))}</span><b class="blue mono" id="round-preview">${money(rounded)}</b></div>
            <div class="round-row"><span>${esc(t('Adjustment'))}</span><b class="${diff > 0 ? 'green' : diff < 0 ? 'blue' : 'muted'} mono" id="round-adjust">${diff > 0 ? '+' : diff < 0 ? '-' : ''}${money(
        Math.abs(diff)
      )}</b></div>
          </div>
          <p class="hint mt12">${esc(
            t('The adjustment is saved with the sale and shows on the receipt. Change is calculated from the rounded total.')
          )}</p>
        </div>
        ${SH.modalFoot(t('Apply rounding'))}
      </form>`,
      { size: '' }
    );
  }

  A.action('pay-rounds', () => {
    if (!A.state.pay) return;
    A.closeModal();
    openRounding();
  });

  A.action('change:round-preview', (ds, el) => {
    if (A.state.route !== 'sales') return;
    const base = cartCalc().total_before_rounding;
    const inc = Math.max(Math.round(U.num(el.value)), 0);
    const rounded = inc ? Math.max(inc, Math.round(base / inc) * inc) : base;
    const diff = rounded - base;
    const preview = document.getElementById('round-preview');
    const adjust = document.getElementById('round-adjust');
    if (preview) preview.textContent = money(rounded);
    if (adjust) {
      adjust.textContent = `${diff > 0 ? '+' : diff < 0 ? '-' : ''}${money(Math.abs(diff))}`;
      adjust.className = `${diff > 0 ? 'green' : diff < 0 ? 'blue' : 'muted'} mono`;
    }
  });

  A.action('pay-key', (ds) => {
    const pay = A.state.pay;
    let s = String(Math.round(pay.tendered / 100));
    const k = ds.key;
    if (k === 'back') s = s.slice(0, -1);
    else if (k === '00') s = (s || '0') + '00';
    else s = (s === '0' ? '' : s) + k;
    if (s.length > 9) s = s.slice(0, 9);
    pay.tendered = Math.round(U.num(s, 0) * 100);
    drawPayment();
  });

  A.action('pay-quick', (ds) => {
    A.state.pay.tendered = Math.round(U.num(ds.amount));
    drawPayment();
  });

  A.action('pay-clear', () => {
    A.state.pay.tendered = 0;
    drawPayment();
  });

  A.action('pay-split', () => {
    A.state.pay.mode = 'split';
    const { remaining } = payCalc();
    A.state.pay.tendered = remaining;
    drawPayment();
  });

  A.action('pay-unsplit', () => {
    A.state.pay.mode = 'single';
    A.state.pay.split = [];
    A.state.pay.tendered = payCalc().total;
    drawPayment();
  });

  A.action('pay-split-remove', (ds) => {
    A.state.pay.split.splice(Number(ds.index), 1);
    A.state.pay.tendered = payCalc().remaining;
    drawPayment();
  });

  A.action('pay-split-add', () => {
    const pay = A.state.pay;
    const { remaining } = payCalc();
    if (remaining <= 0) return;
    let amt = pay.tendered;
    if (amt <= 0) amt = remaining;
    if (amt > remaining) amt = remaining;
    pay.split.push({ payment_type_id: pay.tender_id, amount: amt });
    const left = payCalc().remaining;
    pay.tendered = left;
    if (left <= 0) pay.tender_id = (A.state.paymentTypes || []).find((p) => p.kind === 'cash')?.id || pay.tender_id;
    drawPayment();
  });

  A.action('pay-confirm', async () => {
    const pay = A.state.pay;
    const { total, remaining } = payCalc();
    const cart = A.state.cart;
    if (!cart.items.length) {
      A.closeModal();
      return;
    }
    let payments;
    if (pay.mode === 'split') {
      if (remaining > 0) return A.toast('Split payment is not complete', 'warn');
      payments = pay.split.map((s) => ({ payment_type_id: s.payment_type_id, amount: s.amount / 100, tendered: s.amount / 100 }));
    } else {
      payments = [{ payment_type_id: pay.tender_id, amount: total / 100, tendered: pay.tendered / 100 }];
    }

    try {
      const doc = await A.api('createSale', {
        user_id: A.state.user.id,
        register_id: (A.state.registers || []).find((r) => r.is_primary)?.id,
        customer_id: cart.customer_id,
        items: cart.items.map((it) => ({
          product_id: it.product_id,
          quantity: it.quantity,
          unit_price: it.price / 100,
          discount_type: it.discount_type,
          discount_value: it.discount_value,
        })),
        discount: cart.discount,
        tax_override_percent: cart.tax_override != null && cart.tax_override !== '' ? cart.tax_override : null,
        round_to: cart.rounding || 0,
        payments,
        notes: cart.notes || '',
      });
      A.state.cart = { items: [], customer_id: null, discount: { type: 'percent', value: 0 }, rounding: 0, notes: '' };
      A.state.selectedLine = null;
      A.state.pay = null;
      A.state.lastReceipt = doc;
      A.closeModal();
      A.render();
      A.toast(`${t('Payment complete')} · ${money(doc.total)}`);
      showReceipt(doc);
      await A.reloadCatalog();
    } catch (e) {
      A.toast(e.message, 'bad', 4500);
    }
  });

  function showReceipt(doc) {
    A.modal(
      `<div class="modal-head">
        <div style="display:flex;align-items:center;gap:11px">
          <div style="width:36px;height:36px;border-radius:10px;background:var(--good-bg);color:var(--good);display:grid;place-items:center">${icon(
            'check',
            20
          )}</div>
          <div><h2>Payment complete</h2><div class="sub">${esc(doc.number)} · ${money(doc.total)}</div></div>
        </div>
        <div class="right"><button type="button" class="icon-btn" data-modal-cancel>${icon('close', 18)}</button></div>
      </div>
      <div class="modal-body"><div class="receipt-preview">${SH.receiptPaper(doc, A.state.company)}</div></div>
      <div class="modal-foot">
        <button type="button" class="btn" data-modal-cancel>Close</button>
        <button type="button" class="btn" data-action="receipt-preview">${icon('receipt', 15)} Preview</button>
        <button type="button" class="btn primary" data-action="receipt-print">${icon('print', 15)} Print receipt</button>
      </div>`
    );
  }

  A.action('receipt-print', async () => {
    const doc = A.state.lastReceipt;
    if (!doc) return;
    A.toast('Sending to printer…', 'good', 1600);
    await SH.print(SH.receiptHtml(doc, A.state.company), true);
  });

  A.action('receipt-preview', async () => {
    const doc = A.state.lastReceipt;
    if (!doc) return;
    await window.api.preview(SH.receiptHtml(doc, A.state.company));
  });

  /* --------------------------------------------------------------- refund */

  async function openRefund() {
    const recent = await A.tryApi('listDocuments', { type: 'sale', limit: 10 }, 'Could not load sales');
    A.modal(
      `<div class="modal-head"><div><h2>${esc(t('Refund / return'))}</h2><div class="sub">${esc(
        t('Return items to stock and refund the customer')
      )}</div></div><div class="right"><button type="button" class="icon-btn" data-modal-cancel>${icon(
        'close',
        18
      )}</button></div></div>
      <div class="modal-body">
        <div class="field"><label>${esc(t('Receipt number'))}</label>
          <div class="row gap8"><input id="refund-number" class="input" placeholder="e.g. S-000001" />
          <button class="btn" data-action="refund-find">${esc(t('Find'))}</button></div>
        </div>
        <div id="refund-body" class="mt16"></div>
        <div class="divider"></div>
        <div class="label mb8">${esc(t('Recent sales'))}</div>
        ${
          recent && recent.length
            ? `<div style="max-height:240px;overflow:auto">${recent
                .map(
                  (d) => `<button type="button" class="tree-item" data-action="refund-pick" data-id="${d.id}" style="width:100%">
                    <span>${SH.docTypeBadge('sale')}</span>
                    <span class="strong">${esc(d.number)}</span>
                    <span class="spacer"></span>
                    <span class="mono">${money(d.total)}</span>
                    <span class="small muted">${esc(fmtDate(d.doc_date, true))}</span>
                  </button>`
                )
                .join('')}</div>`
            : `<p class="small muted">${esc(t('No sales yet.'))}</p>`
        }
      </div>`,
      { size: '' }
    );
  }

  A.action('refund-find', async () => {
    const num = (document.getElementById('refund-number') || {}).value || '';
    if (!num.trim()) return A.toast('Enter a receipt number', 'warn');
    const docs = await A.tryApi('listDocuments', { number: num.trim(), type: 'sale', limit: 5 }, 'Lookup failed');
    const match = docs && docs.find((d) => String(d.number).toLowerCase() === num.trim().toLowerCase()) || (docs && docs[0]);
    if (!match) {
      const body = document.getElementById('refund-body');
      if (body) body.innerHTML = '<div class="badge bad">Receipt not found</div>';
      return;
    }
    refundPick(match.id);
  });

  A.action('refund-pick', (ds) => refundPick(Number(ds.id)));

  async function refundPick(id) {
    const doc = await A.tryApi('getDocument', id, t('Could not load receipt'));
    if (!doc) return;
    A.state.refundDoc = doc;
    const payTypes = (A.state.paymentTypes || []).filter((p) => p.active);
    A.modal(
      `<form data-form="do-refund">
        <input type="hidden" name="doc_id" value="${doc.id}" />
        ${SH.modalHead(t('Refund') + ' ' + doc.number, fmtDate(doc.doc_date, true))}
        <div class="modal-body">
          <div class="label mb8">${esc(t('Select items to refund'))}</div>
          <div style="border:1px solid var(--line);border-radius:10px;overflow:hidden;max-height:280px;overflow-y:auto">
            ${doc.items
              .map(
                (it) => `<label class="row" style="padding:9px 12px;border-bottom:1px solid var(--line);cursor:pointer">
                  <input type="checkbox" name="item_ids" value="${it.id}" checked />
                  <span style="flex:1"><span class="strong">${esc(it.name_snapshot)}</span><br /><span class="small muted">${U.qty(
                    it.quantity
                  )} × ${money(it.unit_price)}</span></span>
                  <span class="strong mono">${money(it.total)}</span>
                </label>`
              )
              .join('')}
          </div>
          <div class="form-grid mt16">
            ${SH.field(t('Refund to'), SH.select('payment_type_id', payTypes.map((p) => ({ value: p.id, label: p.name })), payTypes[0] && payTypes[0].id))}
            ${SH.field(t('Reason'), SH.input('reason', '', { placeholder: t('Optional') }))}
          </div>
          <p class="hint mt12">${esc(t('Returned items are added back to stock automatically.'))}</p>
        </div>
        ${SH.modalFoot(t('Refund'), { danger: false })}
      </form>`,
      { size: '' }
    );
  }

  /* ---------------------------------------------------------------- cash */

  function openCash(direction = 'in') {
    const moves = A.state.cashMoves || [];
    const day = A.state.day || {};
    const initial = direction === 'out' ? 'out' : 'in';
    if (!day.register_open) {
      A.toast(t('Open the register before recording cash'), 'warn');
      A.go('day');
      return;
    }
    A.modal(
      `<form data-form="cash-move" class="eod-form">
        ${SH.modalHead(t('Cash in / out'), t('Record cash added to or removed from the drawer'))}
        <div class="modal-body">
          <div class="eod-tabs mb16">
            <button type="button" class="eod-tab ${initial === 'in' ? 'active' : ''}" data-action="cash-direction" data-kind="in">${esc(
        t('Cash in')
      )}</button>
            <button type="button" class="eod-tab ${initial === 'out' ? 'active' : ''}" data-action="cash-direction" data-kind="out">${esc(
        t('Cash out')
      )}</button>
          </div>
          <input type="hidden" name="kind" value="${initial}" id="cash-kind" />
          <div class="form-grid">
            ${SH.field(t('Amount'), SH.input('amount', '', { type: 'number', step: '0.01', min: '0', placeholder: '0', required: true }), { span: 2 })}
            ${SH.field(t('Reason'), SH.input('reason', '', { placeholder: t('e.g. change fund, supplier payment'), required: true }), { span: 2 })}
          </div>
          <div class="divider"></div>
          <div class="label mb8">${esc(t('Cash movements today'))}</div>
          ${
            moves.length
              ? `<div class="eod-moves">${moves
                  .map(
                    (m) => `<div class="split-row"><span>${esc(m.reason)}<span class="small muted"> · ${esc(m.kind === 'in' ? t('Cash in') : t('Cash out'))}</span></span><span class="strong ${
                      m.kind === 'in' ? 'pos' : 'neg'
                    }">${m.kind === 'in' ? '+' : '-'}${money(m.amount)}</span></div>`
                  )
                  .join('')}</div>`
              : `<p class="small muted">${esc(t('No cash movements today.'))}</p>`
          }
        </div>
        ${SH.modalFoot(t('Save entry'), { danger: false, okClass: 'good' })}
      </form>`,
      { size: '' }
    );
  }

  function cashWithDirection(direction) {
    A.api('listCashMovements', [])
      .then((m) => {
        A.state.cashMoves = m;
        openCash(direction);
      })
      .catch((e) => A.toast(e.message, 'bad'));
  }

  A.action('cash', () => cashWithDirection('in'));
  A.action('cash-out', () => cashWithDirection('out'));

  A.action('cash-direction', (ds) => {
    const kind = ds.kind === 'out' ? 'out' : 'in';
    const input = document.getElementById('cash-kind');
    if (input) input.value = kind;
    document.querySelectorAll('[data-cash-direction]').forEach((b) => {
      b.classList.toggle('active', b.getAttribute('data-cash-direction') === kind);
    });
  });

  /* ---------------------------------------------------------------- day */

  A.loader('day', async () => {
    A.state.day = await A.api('getDayState');
    A.state.cashMoves = await A.api('listCashMovements', []);
    A.state.daySummary = A.state.day.register_open ? await A.api('xReport') : null;
  });

  A.screens.day = {
    meta: { title: t('End of Day'), sub: t('Cash drawer, X report and register close') },
    render() {
      const day = A.state.day || {};
      if (!day.register_open) {
        return `<div class="card autocard" style="max-width:760px;margin:7vh auto">
          <div class="card-head"><div class="head-ic">${icon('day', 22)}</div><div><h2>${esc(
          t('Open the register')
        )}</h2><div class="sub muted small">${esc(t('Enter the starting cash in the drawer to begin a business day'))}</div></div></div>
          <form data-form="open-day">
            <div class="card-body">
              <div class="form-grid">
                ${SH.field(t('Opening cash (float)'), SH.input('opening_cash', '0', { type: 'number', step: '0.01', min: '0', required: true }), {
                  span: 2,
                  hint: t('The cash you are putting into the drawer at the start of the day.'),
                })}
                ${SH.field(t('Business date'), SH.input('business_date', day.business_date || U.todayKey(), { type: 'date', required: true }))}
                ${SH.field(
                  t('Register'),
                  SH.select(
                    'register_id',
                    (A.state.registers || []).map((r) => ({ value: r.id, label: r.name })),
                    (A.state.registers || []).find((r) => r.is_primary)?.id
                  )
                )}
              </div>
              <div class="row mt20 eod-actions"><span class="spacer"></span><button class="btn ghost" type="button" data-action="nav" data-route="sales">${esc(
                t('Cancel')
              )}</button><button class="btn good lg" type="submit">${icon('day', 17)} ${esc(t('Open register'))}</button></div>
            </div>
          </form>
        </div>`;
      }

      const s = A.state.daySummary;
      return `<div class="eod-head">
        <div>
          <h2>${esc(t('End of Day'))}</h2>
          <div class="small muted">${esc(t('Business date'))} <b data-notranslate>${esc(day.business_date || '')}</b> · ${esc(
        t('Opened')
      )} ${day.opened_at ? esc(fmtDate(day.opened_at, true)) : '—'} · ${esc(t('Float'))} ${money(day.opening_cash)}</div>
        </div>
        <div class="eod-actions">
          <button class="btn" data-action="x-report">${icon('receipt', 15)} ${esc(t('X report'))}</button>
          <button class="btn" data-action="cash">${icon('cash', 15)} ${esc(t('Cash in / out'))}</button>
          <button class="btn danger" data-action="close-day">${icon('lock', 15)} ${esc(t('Close register'))}</button>
        </div>
      </div>
      <div class="eod-tabs mb16">
        <button class="eod-tab active" type="button">${esc(t('Day total'))}</button>
        <button class="eod-tab" type="button" data-action="day-refresh">${icon('refresh', 13)} ${esc(t('Refresh'))}</button>
      </div>
      <div id="day-live">${dayLive(s)}</div>`;
    },
  };

  function dayLive(s) {
    if (!s) return `<div class="card"><div class="card-body muted">${esc(t('Loading…'))}</div></div>`;
    return `<div class="stat-strip eod-stats">
      <div class="stat"><div class="sl">${esc(t('Sales'))}</div><div class="sv">${money(s.sales_total)}</div></div>
      <div class="stat"><div class="sl">${esc(t('Refunds'))}</div><div class="sv neg">${money(Math.abs(s.refunds_total))}</div></div>
      <div class="stat"><div class="sl">${esc(t('Net'))}</div><div class="sv pos">${money(s.net_total)}</div></div>
      <div class="stat"><div class="sl">${esc(t('Transactions'))}</div><div class="sv">${s.sales_count}</div></div>
      <div class="stat"><div class="sl">${esc(t('Discounts'))}</div><div class="sv">${money(s.discount_total)}</div></div>
      <div class="stat"><div class="sl">${esc(t('Cash expected'))}</div><div class="sv">${money(s.cash_expected)}</div></div>
    </div>
    <div class="grid mt16" style="grid-template-columns:1fr 1fr">
      <div class="card"><div class="card-head"><h2>${esc(t('Tender breakdown'))}</h2></div><div class="card-body">
        ${
          s.tenders
            .map(
              (t) => `<div class="split-row"><span class="row gap8"><span class="badge gray">${esc(
                t.name || '—'
              )}</span></span><span class="strong mono">${money(t.net)}</span></div>`
            )
            .join('') || `<p class="muted small">${esc(t('No payments yet.'))}</p>`
        }
      </div></div>
      <div class="card"><div class="card-head"><h2>${esc(t('Top items today'))}</h2></div><div class="card-body">
        ${
          s.items
            .slice(0, 6)
            .map(
              (i) => `<div class="split-row"><span>${esc(i.name)} <span class="small muted">× ${U.qty(
                i.qty_sold
              )}</span></span><span class="strong mono">${money(i.sales_total)}</span></div>`
            )
            .join('') || `<p class="muted small">${esc(t('No items sold yet.'))}</p>`
        }
      </div></div>
    </div>
    <div class="card mt16"><div class="card-head"><h2>${esc(t('Cash movements today'))}</h2><div class="right"><button class="btn sm" data-action="cash">${icon(
      'plus',
      14
    )} ${esc(t('Add entry'))}</button></div></div><div class="card-body">
      ${
        (A.state.cashMoves || [])
          .map(
            (m) =>
              `<div class="split-row"><span>${esc(m.reason)} <span class="small muted">· ${esc(
                fmtDate(m.occurred_at, true)
              )}</span></span><span class="strong mono ${m.kind === 'in' ? 'pos' : 'neg'}">${
                m.kind === 'in' ? '+' : '-'
              }${money(m.amount)}</span></div>`
          )
          .join('') || `<p class="muted small">${esc(t('No cash movements today.'))}</p>`
      }
    </div></div>`;
  }

  A.action('day-refresh', async () => {
    A.state.day = await A.api('getDayState');
    A.state.cashMoves = await A.api('listCashMovements', []);
    A.state.daySummary = await A.api('xReport');
    A.render();
  });

  A.action('x-report', async () => {
    const s = await A.tryApi('xReport', {}, 'Could not build X report');
    if (!s) return;
    const html = reportHtml(
      t('X report') + ' — ' + t('Status'),
      [
        [t('Date'), s.date],
        [t('Sales'), money(s.sales_total)],
        [t('Refunds'), money(s.refunds_total)],
        [t('Net'), money(s.net_total)],
        [t('Transactions'), s.sales_count],
        [t('Cash expected'), money(s.cash_expected)],
      ],
      ''
    );
    await window.api.preview(html);
  });

  A.action('close-day', async () => {
    const s = A.state.daySummary || (await A.api('xReport'));
    A.state.daySummary = s;
    const expected = s ? s.cash_expected : 0;
    A.modal(
      `<form data-form="close-day" class="eod-form">
        ${SH.modalHead(t('End of day'))}
        <div class="modal-body">
          <div class="eod-tiles">
            <button type="button" class="eod-tile" data-action="cash-out">
              <span class="eod-tile-ic">${icon('users', 26)}</span>
              <span>${esc(t('Cash out'))}</span>
            </button>
            <button type="button" class="eod-tile" data-action="cash-out" disabled>
              <span class="eod-tile-ic">${icon('customers', 26)}</span>
              <span>${esc(t('Cash out all users'))}</span>
            </button>
            <button type="button" class="eod-tile active">
              <span class="eod-tile-ic">${icon('logout', 26)}</span>
              <span>${esc(t('Close register'))}</span>
            </button>
            <span class="spacer"></span>
            <button type="button" class="eod-report" data-action="x-report">
              <b>X</b><span>${esc(t('Report'))}</span>
            </button>
          </div>
          <div class="eod-tabs mb12">
            <span class="eod-tab">${esc(t('Open transactions'))} 0</span>
            <span class="eod-tab active">${esc(t('Day total'))} ${esc(U.todayKey())}</span>
          </div>
          <div class="eod-grid">
            <div class="eod-panel">
              <div class="eod-summary">
                <div><span>${esc(t('Sales'))}</span><b>${money(s ? s.sales_total : 0)}</b></div>
                <div><span>${esc(t('Refunds'))}</span><b>${money(s ? Math.abs(s.refunds_total) : 0)}</b></div>
                <div><span>${esc(t('Cash in'))}</span><b>${money(s ? s.cash_in : 0)}</b></div>
                <div><span>${esc(t('Cash out'))}</span><b>${money(s ? s.cash_out : 0)}</b></div>
                <div class="total"><span>${esc(t('Cash expected'))}</span><b>${money(expected)}</b></div>
              </div>
            </div>
            <div class="eod-side">
              <div class="label">${esc(t('Counted cash'))}</div>
              <input class="input eod-count" name="counted_cash" type="number" step="0.01" min="0" required value="${(expected / 100).toFixed(
                2
              )}" />
              <p class="hint">${esc(t('Enter the cash you counted in the drawer. A Z report will be saved.'))}</p>
              <div class="eod-tenders">
                ${(s ? s.tenders : [])
                  .map(
                    (t) => `<div class="split-row"><span>${esc(t.name || '—')}</span><span class="mono">${money(t.net)}</span></div>`
                  )
                  .join('')}
              </div>
            </div>
          </div>
        </div>
        <div class="modal-foot eod-foot">
          <button type="submit" class="btn good">${icon('check', 15)} ${esc(t('Continue'))}</button>
          <button type="button" class="btn danger" data-modal-cancel>${icon('close', 15)} ${esc(t('Cancel'))}</button>
        </div>
      </form>`,
      { size: 'wide' }
    );
  });

  function showZReport(z) {
    A.modal(
      `<div class="modal-head"><div><h2>${esc(t('Z report'))} ${esc(z.number)}</h2><div class="sub">${esc(
        t('Closed')
      )} ${esc(fmtDate(z.closed_at, true))}</div></div>
        <div class="right"><button type="button" class="icon-btn" data-modal-cancel>${icon('close', 18)}</button></div></div>
      <div class="modal-body">
        <div class="receipt-preview" style="display:block;background:var(--surface-2)">
          <div class="card z-card" style="max-width:560px;margin:0 auto"><div class="card-body">
            <div class="stat-strip">
              <div class="stat"><div class="sl">${esc(t('Net sales'))}</div><div class="sv">${money(z.total)}</div></div>
              <div class="stat"><div class="sl">${esc(t('Transactions'))}</div><div class="sv">${z.sales_count}</div></div>
              <div class="stat"><div class="sl">${esc(t('Cash expected'))}</div><div class="sv">${money(z.cash_expected)}</div></div>
              <div class="stat"><div class="sl">${esc(t('Counted cash'))}</div><div class="sv">${money(
                z.counted_cash == null ? z.cash_expected : z.counted_cash
              )}</div></div>
              <div class="stat"><div class="sl">${esc(t('Difference'))}</div><div class="sv ${
                z.cash_difference < 0 ? 'neg' : z.cash_difference > 0 ? 'pos' : ''
              }">${money(z.cash_difference || 0)}</div></div>
            </div>
            <div class="label mb8">${esc(t('Tenders'))}</div>
            ${(z.tenders || [])
              .map(
                (t) => `<div class="split-row"><span>${esc(t.name || '—')}</span><span class="strong mono">${money(t.net)}</span></div>`
              )
              .join('')}
          </div></div>
        </div>
      </div>
      <div class="modal-foot">
        <button type="button" class="btn" data-modal-cancel>${esc(t('Close'))}</button>
        <button type="button" class="btn primary" data-action="z-print">${icon('print', 15)} ${esc(t('Print Z report'))}</button>
      </div>`,
      { size: '' }
    );
  }

  A.showZReport = showZReport;

  A.action('z-print', async () => {
    const z = A.state.lastZ;
    if (!z) return;
    const lines = (z.tenders || [])
      .map((t) => `<div class="r-line"><span>${esc(t.name)}</span><span>${(t.net / 100).toLocaleString(locale())}</span></div>`)
      .join('');
    const html = `<!doctype html><html><head><meta charset="utf-8"><style>
      @page{size:80mm auto;margin:4mm} body{font-family:Consolas,monospace;font-size:11.5px;width:72mm;margin:0;padding:6px 4px}
      .r-center{text-align:center}.r-line{display:flex;justify-content:space-between}.muted{color:#555}hr{border:0;border-top:1px dashed #888;margin:7px 0}
      .total{font-weight:700;font-size:13px}
    </style></head><body>
      <div class="r-center"><h2>${esc(A.state.company.name)}</h2><div>${esc(z.number)}</div><div>${esc(fmtDate(z.closed_at, true))}</div></div>
      <hr/>
      <div class="r-line"><span>Opening cash</span><span>${(z.opening_cash / 100).toLocaleString(locale())}</span></div>
      <div class="r-line"><span>Cash in</span><span>${(z.cash_in / 100).toLocaleString(locale())}</span></div>
      <div class="r-line"><span>Cash out</span><span>${(z.cash_out / 100).toLocaleString(locale())}</span></div>
      <div class="r-line"><span>Sales</span><span>${z.sales_count}</span></div>
      <div class="r-line"><span>Discounts</span><span>${(z.discount_total / 100).toLocaleString(locale())}</span></div>
      <div class="r-line"><span>Tax</span><span>${(z.tax_total / 100).toLocaleString(locale())}</span></div>
      <div class="r-line total"><span>NET SALES</span><span>${(z.total / 100).toLocaleString(locale())}</span></div>
      <hr/>${lines}
      <hr/>
      <div class="r-line total"><span>CASH EXPECTED</span><span>${(z.cash_expected / 100).toLocaleString(locale())}</span></div>
      <div class="r-line"><span>COUNTED</span><span>${((z.counted_cash == null ? z.cash_expected : z.counted_cash) / 100).toLocaleString(locale())}</span></div>
      <div class="r-line"><span>DIFFERENCE</span><span>${((z.cash_difference || 0) / 100).toLocaleString(locale())}</span></div>
      <hr/><div class="r-center muted">Powered by Aroklon</div>
    </body></html>`;
    await SH.print(html, true);
  });

  /* ----------------------------------------------------------- dashboard */

  A.screens.dashboard = {
    meta: { title: t('Dashboard'), sub: t('How the store is doing') },
    render() {
      const d = A.state.dashboard;
      if (!d) return `<div class="card"><div class="card-body muted">Loading dashboard…</div></div>`;
      const max = Math.max(...d.week.map((w) => w.total), 1);
      const today = d.today;
      const tenderTotal = d.tenders.reduce((s, t) => s + t.net, 0) || 1;
      let acc = 0;
      const stops = d.tenders
        .map((t) => {
          const start = (acc / tenderTotal) * 100;
          acc += Math.abs(t.net);
          const end = (acc / tenderTotal) * 100;
          return `${t.color || '#64748b'} ${start}% ${end}%`;
        })
        .join(',');
      return `<div class="kpis">
        <div class="kpi"><div class="label">${icon('cash', 14)} ${esc(t("Today's sales"))}</div><div class="value">${money(today.sales_total)}</div><div class="meta">${esc(
          t('{n} transactions', { n: today.sales_count })
        )}</div></div>
        <div class="kpi"><div class="label">${icon('trend', 14)} ${esc(t('Net of refunds'))}</div><div class="value">${money(today.net_total)}</div><div class="meta">${esc(
          t('{n} returns', { n: today.returns_count })
        )}</div></div>
        <div class="kpi"><div class="label">${icon('stock', 14)} ${esc(t('Stock value'))}</div><div class="value">${money(d.stock_value)}</div><div class="meta">${esc(
          t('{products} products · {documents} documents', { products: d.product_count, documents: d.doc_count })
        )}</div></div>
        <div class="kpi"><div class="label">${icon('customers', 14)} ${esc(t('Customers'))}</div><div class="value">${d.customer_count}</div><div class="meta">${esc(
          t('{n} low-stock items', { n: d.low.length })
        )}</div></div>
      </div>

      <div class="grid mt16" style="grid-template-columns:1.5fr 1fr">
        <div class="card"><div class="card-head"><h2>Sales, last 7 days</h2><div class="right"><span class="chip">${icon('trend', 14)} ${money(d.week.reduce((s, w) => s + w.total, 0))}</span></div></div>
          <div class="card-body">
            <div class="chart-bars">
              ${d.week
                .map(
                  (w) => `<div class="chart-bar">
                    <div class="bar" style="height:${Math.max((w.total / max) * 100, 2)}%"><span>${moneyShort(w.total)}</span></div>
                    <div class="lbl">${esc(new Date(w.date + 'T12:00:00').toLocaleDateString(locale(), { weekday: 'short' }))}</div>
                  </div>`
                )
                .join('')}
            </div>
          </div>
        </div>
        <div class="card"><div class="card-head"><h2>Payment mix today</h2></div><div class="card-body">
          <div class="donut-wrap">
            <div style="width:132px;height:132px;border-radius:50%;background:conic-gradient(${stops || '#e2e8f0 0% 100%'});position:relative;display:grid;place-items:center;flex:0 0 auto">
              <div style="width:92px;height:92px;border-radius:50%;background:var(--surface);display:grid;place-items:center;text-align:center">
                <div><div class="small muted">Total</div><div class="strong" style="font-size:13px">${moneyShort(today.sales_total)}</div></div>
              </div>
            </div>
            <div class="legend">
              ${d.tenders
                .map(
                  (t) => `<div class="legend-row"><span class="sw" style="background:${attr(t.color || '#64748b')}"></span>${esc(t.name || '—')}<b>${moneyShort(
                    t.net
                  )}</b></div>`
                )
                .join('') || '<div class="small muted">No payments today.</div>'}
            </div>
          </div>
        </div>
      </div>
      </div>

      <div class="grid mt16" style="grid-template-columns:1fr 1fr">
        <div class="card"><div class="card-head"><h2>Best sellers today</h2></div><div class="card-body">
          ${d.top
            .map(
              (item, i) => `<div class="row between" style="padding:8px 0;border-bottom:1px solid var(--line)">
                <span class="row gap12"><span class="badge info">${i + 1}</span><span class="strong">${esc(item.name)}</span></span>
                <span class="small muted">${esc(t('{qty} sold', { qty: U.qty(item.qty) }))} · <span class="mono">${money(item.total)}</span></span>
              </div>`
            )
            .join('') || `<p class="muted small">${esc(t('No sales today yet.'))}</p>`}
        </div></div>
        <div class="card"><div class="card-head"><h2>${esc(t('Low stock'))}</h2><div class="right"><button class="btn sm" data-action="nav" data-route="stock">${esc(t('Open stock'))}</button></div></div><div class="card-body">
          ${d.low
            .map(
              (p) => `<div class="row between" style="padding:8px 0;border-bottom:1px solid var(--line)">
                <span class="strong">${esc(p.name)}</span>
                <span class="badge ${p.quantity <= 0 ? 'bad' : 'warn'}">${esc(t('{qty} left', { qty: U.qty(p.quantity) }))}</span>
              </div>`
            )
            .join('') || `<p class="muted small">${esc(t('Everything is well stocked.'))}</p>`}
        </div></div>
      </div>`;
    },
  };

  A.action('dashboard-refresh', async () => {
    A.state.dashboard = await A.api('dashboard');
    A.render();
  });

  /* ------------------------------------------------------------ products */

  A.screens.products = {
    meta: { title: t('Products'), sub: t('Your catalogue') },
    render() {
      const groups = A.state.groups || [];
      const products = A.state.products || [];
      const gid = A.state.productGroup;
      const list = gid ? products.filter((p) => String(p.group_id) === String(gid)) : products;
      return `<div class="split-2">
        <div class="card" style="flex:0 0 230px;display:flex;flex-direction:column">
          <div class="card-head"><h2>Groups</h2><div class="right"><button class="icon-btn" data-action="group-new" title="New group">${icon('plus', 16)}</button></div></div>
          <div class="card-body" style="padding:8px;overflow:auto">
            <button class="tree-item ${!gid ? 'active' : ''}" data-action="product-group" data-id="">${icon('products', 15)} All products <span class="cnt">${products.length}</span></button>
            ${groups
              .map(
                (g) => `<button class="tree-item ${String(gid) === String(g.id) ? 'active' : ''}" data-action="product-group" data-id="${g.id}" style="${g.parent_id ? 'padding-left:22px' : ''}">
                  <span style="width:8px;height:8px;border-radius:2px;background:${attr(g.color || '#6366f1')}"></span>${esc(g.name)}
                  <span class="cnt">${g.product_count}</span>
                </button>`
              )
              .join('')}
          </div>
        </div>
        <div class="card" style="flex:1;min-width:0;display:flex;flex-direction:column">
          <div class="card-head"><div class="row gap8"><h2>${gid ? esc((groups.find((g) => String(g.id) === String(gid)) || {}).name || 'Products') : 'All products'}</h2><span class="badge gray">${list.length}</span></div>
            <div class="right">
              <button class="btn sm" data-action="products-refresh">${icon('refresh', 14)} Refresh</button>
              <button class="btn sm" data-action="import-products">${icon('upload', 14)} ${esc(t('Import CSV'))}</button>
              <button class="btn primary sm" data-action="product-new">${icon('plus', 15)} New product</button>
            </div>
          </div>
          <div class="table-wrap" style="flex:1">
            ${
              list.length
                ? `<table class="data"><thead><tr><th>Product</th><th>Code</th><th>Group</th><th class="num">Cost</th><th class="num">Price</th><th class="num">Stock</th><th></th></tr></thead><tbody>
                  ${list
                    .map(
                      (p) => `<tr>
                        <td><div class="row gap8"><span style="width:26px;height:26px;border-radius:7px;background:${attr(p.color || '#e2e8f0')};display:inline-block"></span><div><div class="strong">${esc(
                          p.name
                        )}</div><div class="small muted">${esc(p.unit || '')}${p.is_service ? ' · service' : ''}</div></div></div></td>
                        <td class="mono muted">${esc(p.code || '—')}</td>
                        <td><span class="badge gray">${esc(p.group_name || '—')}</span></td>
                        <td class="num mono">${money(p.cost)}</td>
                        <td class="num mono strong">${money(p.price)}</td>
                        <td class="num">${p.is_service ? '<span class="muted">—</span>' : `<span class="badge ${p.quantity <= p.low_stock && p.low_stock > 0 ? 'bad' : 'good'}">${U.qty(
                          p.quantity
                        )}</span>`}</td>
                        <td class="right"><div class="row gap8" style="justify-content:flex-end">
                          <button class="icon-btn" data-action="product-edit" data-id="${p.id}" title="Edit">${icon('edit', 16)}</button>
                          <button class="icon-btn" data-action="product-delete" data-id="${p.id}" title="Delete" style="color:var(--bad)">${icon('trash', 16)}</button>
                        </div></td>
                      </tr>`
                    )
                    .join('')}
                  </tbody></table>`
                : SH.empty({ icon: 'products', title: 'No products', text: 'Create your first product.', action: `<button class="btn primary" data-action="product-new">${icon('plus', 15)} New product</button>` })
            }
          </div>
        </div>
      </div>`;
    },
  };

  A.action('product-group', (ds) => {
    A.state.productGroup = ds.id ? Number(ds.id) : null;
    A.render();
  });

  A.action('products-refresh', async () => {
    await A.reloadCatalog();
    A.render();
    A.toast('Catalogue refreshed');
  });

  /* --------------------------------------------------------- CSV import */

  function parseCsv(text) {
    const src = String(text || '').replace(/^\uFEFF/, '');
    if (!src.trim()) return [];
    const firstLine = src.split(/\r?\n/, 1)[0] || '';
    const delimiter =
      (firstLine.match(/;/g) || []).length > (firstLine.match(/,/g) || []).length ? ';' : ',';
    const rows = [];
    let row = [];
    let cur = '';
    let quoted = false;
    for (let i = 0; i < src.length; i++) {
      const ch = src[i];
      if (quoted) {
        if (ch === '"' && src[i + 1] === '"') {
          cur += '"';
          i++;
        } else if (ch === '"') quoted = false;
        else cur += ch;
      } else if (ch === '"') quoted = true;
      else if (ch === delimiter) {
        row.push(cur);
        cur = '';
      } else if (ch === '\n' || ch === '\r') {
        if (ch === '\r' && src[i + 1] === '\n') i++;
        row.push(cur);
        cur = '';
        if (row.some((c) => String(c).trim() !== '')) rows.push(row);
        row = [];
      } else cur += ch;
    }
    row.push(cur);
    if (row.some((c) => String(c).trim() !== '')) rows.push(row);
    if (rows.length < 2) return [];
    const headers = rows.shift().map((h) => String(h).trim().toLowerCase().replace(/\s+/g, '_'));
    return rows
      .map((cells) => {
        const obj = {};
        headers.forEach((h, i) => {
          if (h) obj[h] = cells[i] == null ? '' : String(cells[i]).trim();
        });
        return obj;
      })
      .filter((o) => Object.values(o).some((v) => String(v).trim() !== ''));
  }

  function csvTemplate() {
    const csv =
      'name,code,barcode,group,unit,cost,price,stock,low_stock,tax,active,service\\n' +
      'Mineral Water 600ml,MW600,8991002100011,Drinks,pcs,1800,3000,48,10,11,1,0\\n' +
      'Coffee,COF-01,,Drinks,pcs,5000,12000,20,5,11,1,0\\n';
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'aroklon-products-template.csv';
    document.body.appendChild(a);
    a.click();
    setTimeout(() => {
      URL.revokeObjectURL(a.href);
      a.remove();
    }, 800);
  }

  A.action('import-products', () => {
    A.modal(
      `<form data-form="import-products">
        ${SH.modalHead(t('Import products from CSV'), t('Create products in bulk from a spreadsheet'))}
        <div class="modal-body">
          <p class="small muted">${esc(
            t(
              'Columns: name, code, barcode, group, unit, cost, price, stock, low_stock, tax, active, service. The name column is required; existing products are matched by code, barcode or name.'
            )
          )}</p>
          <div class="field mt12"><label>${esc(t('CSV file'))}</label>
            <input class="input" type="file" id="csv-file" accept=".csv,text/csv,text/plain" />
          </div>
          <div class="divider"></div>
          <div class="field"><label>${esc(t('Or paste CSV text'))}</label>
            <textarea class="input" name="csv_text" id="csv-text" rows="8" spellcheck="false" placeholder="name,code,price,stock&#10;Mineral Water,MW600,3000,48"></textarea>
          </div>
          <p class="hint">${esc(t('Comma and semicolon separated files both work. Stock is set to the value in the file.'))}</p>
        </div>
        <div class="modal-foot">
          <button type="button" class="btn" data-modal-cancel>${esc(t('Cancel'))}</button>
          <button type="button" class="btn" data-action="csv-template">${icon('download', 15)} ${esc(t('Download template'))}</button>
          <button type="submit" class="btn good">${icon('upload', 15)} ${esc(t('Import'))}</button>
        </div>
      </form>`,
      { size: '' }
    );
  });

  A.action('csv-template', () => csvTemplate());

  function readFileText(file) {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result || ''));
      reader.onerror = () => reject(new Error(t('Could not read the file')));
      reader.readAsText(file);
    });
  }

  A.action('form:import-products', async (form) => {
    const fd = new FormData(form);
    const fileInput = document.getElementById('csv-file');
    let text = String(fd.get('csv_text') || '');
    if (fileInput && fileInput.files && fileInput.files[0]) {
      text = await readFileText(fileInput.files[0]);
    }
    const rows = parseCsv(text);
    if (!rows.length) throw new Error(t('Choose a CSV file or paste at least one product row'));
    const res = await A.api('importProducts', [rows]);
    if (!res.ok) throw new Error(res.error || t('Import failed'));
    A.closeModal();
    await A.reloadCatalog();
    A.render();
    const summary = t('{created} created · {updated} updated · {skipped} skipped', {
      created: res.created,
      updated: res.updated,
      skipped: res.skipped,
    });
    A.toast(summary, res.skipped ? 'warn' : 'good', 6000);
    if (res.errors && res.errors.length) {
      A.modal(
        `${SH.modalHead(t('Import warnings'))}
        <div class="modal-body"><p class="small muted">${esc(summary)}</p><div class="import-errors">${res.errors
          .map((e) => `<div class="split-row"><span>${esc(e)}</span></div>`)
          .join('')}</div></div>
        <div class="modal-foot"><button class="btn primary" data-modal-cancel>${esc(t('Close'))}</button></div>`
      );
    }
  });

  A.action('group-new', async () => {
    const name = await A.prompt({ title: 'New product group', label: 'Group name', confirmLabel: 'Create' });
    if (!name) return;
    await A.api('saveGroup', { name, color: '#6366f1' });
    A.state.groups = await A.api('listGroups');
    A.render();
    A.toast('Group created');
  });

  function openProductEditor(id) {
    const p = id ? (A.state.products || []).find((x) => String(x.id) === String(id)) : null;
    if (id && !p) {
      A.api('getProduct', id).then((full) => openProductEditorWith(full));
      return;
    }
    openProductEditorWith(
      p || {
        name: '',
        code: '',
        unit: 'pcs',
        group_id: A.state.productGroup || (A.state.groups[0] || {}).id,
        active: true,
        is_service: false,
        cost: 0,
        price: 0,
        price_incl_tax: false,
        price_change_allowed: true,
        default_qty: false,
        age_restricted: false,
        color: '#e2e8f0',
        image: null,
        description: '',
        low_stock: 0,
        barcodes: [],
        comments: [],
        tax_rate_ids: (A.state.taxRates || []).filter((t) => t.is_default).map((t) => t.id),
        quantity: 0,
      }
    );
  }

  function openProductEditorWith(p) {
    A.state.productDraft = p;
    const groups = (A.state.groups || []).map((g) => ({ value: g.id, label: g.name + (g.parent_id ? '' : '') }));
    const taxBoxes = (A.state.taxRates || [])
      .map(
        (t) => `<label class="row gap8" style="font-size:12.5px;font-weight:500">
          <input type="checkbox" name="tax_rate_id" value="${t.id}" ${(p.tax_rate_ids || []).map(String).includes(String(t.id)) ? 'checked' : ''} />
          ${esc(t.name)} <span class="muted">(${U.num(t.percent)}%)</span>
        </label>`
      )
      .join('');

    const colors = ['#e2e8f0', '#dbeafe', '#dcfce7', '#fef3c7', '#fee2e2', '#ede9fe', '#cffafe', '#fce7f3'];
    const barcodeRows = (p.barcodes && p.barcodes.length ? p.barcodes : [''])
      .map((b) => `<div class="row gap8 barcode-row"><input class="input barcode-row-input" value="${attr(b)}" placeholder="Barcode or SKU" /><button type="button" class="icon-btn" data-action="barcode-remove" style="color:var(--bad)">${icon(
        'trash',
        15
      )}</button></div>`)
      .join('');
    const commentRows = (p.comments && p.comments.length ? p.comments.map((c) => (typeof c === 'string' ? c : c.text)) : [''])
      .map((c) => `<div class="row gap8 comment-row"><input class="input comment-row-input" value="${attr(c)}" placeholder="e.g. Rare, Medium, Well done" /><button type="button" class="icon-btn" data-action="comment-remove" style="color:var(--bad)">${icon(
        'trash',
        15
      )}</button></div>`)
      .join('');

    A.modal(
      `<form data-form="save-product">
        <input type="hidden" name="id" value="${attr(p.id || '')}" />
        <input type="hidden" name="image" id="product-image-value" value="${attr(p.image || '')}" />
        <input type="hidden" name="color" id="product-color-value" value="${attr(p.color || '#e2e8f0')}" />
        ${SH.modalHead(p.id ? 'Edit product' : 'New product', 'Define what you sell and how it behaves at the till')}
        <div class="tabs">
          <button type="button" class="tab active" data-action="tab" data-target="pt-details">Details</button>
          <button type="button" class="tab" data-action="tab" data-target="pt-price">Price & tax</button>
          <button type="button" class="tab" data-action="tab" data-target="pt-barcodes">Barcodes</button>
          <button type="button" class="tab" data-action="tab" data-target="pt-comments">Comments</button>
          <button type="button" class="tab" data-action="tab" data-target="pt-look">Image & colour</button>
        </div>
        <div class="modal-body">
          <div data-tab-panel="pt-details">
            <div class="form-grid">
              ${SH.field('Name', SH.input('name', p.name, { required: true, placeholder: 'e.g. Mineral Water 600ml' }), { span: 2 })}
              ${SH.field('Code / SKU', SH.input('code', p.code, { placeholder: 'Auto if blank' }))}
              ${SH.field('Unit', SH.input('unit', p.unit || 'pcs'))}
              ${SH.field('Group', SH.select('group_id', [{ value: '', label: '— none —' }].concat(groups), p.group_id))}
              ${SH.field('Low stock alert at', SH.input('low_stock', p.low_stock || 0, { type: 'number', min: '0' }), { hint: 'Show a warning when quantity on hand reaches this number.' })}
              <div class="span2 row wrap gap12">
                ${SH.checkbox('active', 'Active (available for sale)', p.active !== false)}
                ${SH.checkbox('is_service', 'Service / no stock tracking', !!p.is_service)}
                ${SH.checkbox('default_qty', 'Add with quantity 1 (no prompt)', !!p.default_qty)}
                ${SH.checkbox('age_restricted', 'Age restricted', !!p.age_restricted)}
              </div>
              ${SH.field('Description', `<textarea class="input" name="description" placeholder="Internal note">${esc(p.description || '')}</textarea>`, { span: 2 })}
            </div>
          </div>

          <div data-tab-panel="pt-price" style="display:none">
            <div class="form-grid">
              ${SH.field('Cost', SH.input('cost', (p.cost || 0) / 100, { type: 'number', step: '0.01', min: '0' }))}
              ${SH.field('Sell price', SH.input('price', (p.price || 0) / 100, { type: 'number', step: '0.01', min: '0', required: true }))}
              <div class="span2 row wrap gap12">
                ${SH.checkbox('price_incl_tax', 'Price includes tax', !!p.price_incl_tax)}
                ${SH.checkbox('price_change_allowed', 'Allow price change at sale', p.price_change_allowed !== false)}
              </div>
              <div class="span2">
                <div class="label mb8">Tax rates</div>
                <div class="row wrap gap12">${taxBoxes || '<span class="small muted">No tax rates configured.</span>'}</div>
              </div>
            </div>
          </div>

          <div data-tab-panel="pt-barcodes" style="display:none">
            <div class="label mb8">Barcodes</div>
            <div id="barcode-rows">${barcodeRows}</div>
            <button type="button" class="btn sm mt8" data-action="barcode-add">${icon('plus', 14)} Add barcode</button>
            <p class="hint mt8">Scan or type a barcode. Multiple barcodes can point to one product.</p>
          </div>

          <div data-tab-panel="pt-comments" style="display:none">
            <div class="label mb8">Quick comments</div>
            <div id="comment-rows">${commentRows}</div>
            <button type="button" class="btn sm mt8" data-action="comment-add">${icon('plus', 14)} Add comment</button>
            <p class="hint mt8">Shown when selling this product, e.g. doneness or milk options.</p>
          </div>

          <div data-tab-panel="pt-look" style="display:none">
            <div class="field mb12"><label>Tile colour</label>
              <div class="row wrap gap8" id="color-swatches">
                ${colors
                  .map(
                    (c) => `<button type="button" data-action="pick-color" data-color="${c}" style="width:34px;height:34px;border-radius:9px;background:${c};border:${
                      String(p.color) === c ? '2px solid var(--primary)' : '1px solid var(--line-strong)'
                    }"></button>`
                  )
                  .join('')}
              </div>
            </div>
            <div class="field mb12"><label>Product image</label>
              <div class="row gap12" style="align-items:flex-start">
                <div id="image-preview" style="width:84px;height:84px;border-radius:12px;overflow:hidden;background:${attr(p.color || '#e2e8f0')};display:grid;place-items:center;border:1px solid var(--line)">
                  ${p.image ? `<img src="${attr(p.image)}" style="width:100%;height:100%;object-fit:cover" />` : icon('products', 26)}
                </div>
                <div>
                  <input type="file" accept="image/*" data-file="image" class="input" style="max-width:260px" />
                  <div class="hint mt8">PNG or JPG, roughly 150×150 px. Stored locally.</div>
                  <button type="button" class="btn sm mt8" data-action="image-clear">Remove image</button>
                </div>
              </div>
            </div>
            <p class="hint">The colour and image appear on the product tile in the visual sales layout.</p>
          </div>
        </div>
        ${SH.modalFoot(p.id ? 'Save changes' : 'Create product')}
      </form>`,
      { size: 'wide' }
    );
  }

  A.action('product-new', () => openProductEditor(null));
  A.action('product-edit', (ds) => openProductEditor(Number(ds.id)));

  A.action('barcode-add', () => {
    const wrap = document.getElementById('barcode-rows');
    if (!wrap) return;
    const div = document.createElement('div');
    div.className = 'row gap8 barcode-row mt8';
    div.innerHTML = `<input class="input barcode-row-input" placeholder="Barcode or SKU" /><button type="button" class="icon-btn" data-action="barcode-remove" style="color:var(--bad)">${icon(
      'trash',
      15
    )}</button>`;
    wrap.appendChild(div);
    div.querySelector('input').focus();
  });

  A.action('barcode-remove', (ds, el) => {
    const row = el.closest('.barcode-row');
    if (row) row.remove();
  });

  A.action('comment-add', () => {
    const wrap = document.getElementById('comment-rows');
    if (!wrap) return;
    const div = document.createElement('div');
    div.className = 'row gap8 comment-row mt8';
    div.innerHTML = `<input class="input comment-row-input" placeholder="Comment" /><button type="button" class="icon-btn" data-action="comment-remove" style="color:var(--bad)">${icon(
      'trash',
      15
    )}</button>`;
    wrap.appendChild(div);
    div.querySelector('input').focus();
  });

  A.action('comment-remove', (ds, el) => {
    const row = el.closest('.comment-row');
    if (row) row.remove();
  });

  A.action('pick-color', (ds) => {
    document.querySelectorAll('#color-swatches button').forEach((b) => (b.style.border = '1px solid var(--line-strong)'));
    const el = document.querySelector(`#color-swatches button[data-color="${ds.color}"]`);
    if (el) el.style.border = '2px solid var(--primary)';
    const hidden = document.getElementById('product-color-value');
    if (hidden) hidden.value = ds.color;
    const prev = document.getElementById('image-preview');
    if (prev) prev.style.background = ds.color;
  });

  A.action('image-clear', () => {
    const hidden = document.getElementById('product-image-value');
    if (hidden) hidden.value = '';
    const prev = document.getElementById('image-preview');
    if (prev) prev.innerHTML = icon('products', 26);
  });

  A.action('product-delete', async (ds) => {
    const p = (A.state.products || []).find((x) => String(x.id) === String(ds.id));
    const ok = await A.confirm({
      title: `Delete ${p ? p.name : 'product'}?`,
      message: '<p class="small">Products that appear in any document cannot be deleted. Deactivate them instead.</p>',
      confirmLabel: 'Delete',
      danger: true,
    });
    if (!ok) return;
    const res = await A.tryApi('deleteProduct', Number(ds.id), 'Could not delete');
    if (res && res.ok) {
      await A.reloadCatalog();
      A.render();
      A.toast('Product deleted');
    }
  });

  /* --------------------------------------------------------------- stock */

  A.screens.stock = {
    meta: { title: t('Stock'), sub: t('Quantities on hand') },
    render() {
      const stock = A.state.stock || [];
      return `<div class="card">
        <div class="card-head"><div class="row gap8"><h2>${esc(t('Quantities on hand'))}</h2><span class="badge gray">${esc(t('{n} items', { n: stock.length }))}</span></div>
          <div class="right"><button class="btn sm" data-action="stock-refresh">${icon('refresh', 14)} Refresh</button></div>
        </div>
        <div class="table-wrap">
          ${
            stock.length
              ? `<table class="data"><thead><tr><th>Product</th><th>Group</th><th class="num">On hand</th><th class="num">Avg cost</th><th class="num">Value</th><th>Status</th><th></th></tr></thead><tbody>
                ${stock
                  .map(
                    (s) => `<tr>
                      <td><div class="strong">${esc(s.name)}</div><div class="small muted mono">${esc(s.code || '')}</div></td>
                      <td><span class="badge gray">${esc(s.group_name || '—')}</span></td>
                      <td class="num mono strong">${U.qty(s.quantity)}</td>
                      <td class="num mono">${money(s.avg_cost)}</td>
                      <td class="num mono">${money(Math.round(s.quantity * s.avg_cost))}</td>
                      <td>${s.is_service ? '<span class="badge info">service</span>' : `<span class="badge ${
                        s.quantity <= 0 ? 'bad' : s.low_stock > 0 && s.quantity <= s.low_stock ? 'warn' : 'good'
                      }">${s.quantity <= 0 ? 'Out of stock' : s.low_stock > 0 && s.quantity <= s.low_stock ? 'Low' : 'In stock'}</span>`}</td>
                      <td class="right"><div class="row gap8" style="justify-content:flex-end">
                        <button class="btn sm" data-action="stock-adjust" data-id="${s.id}">${icon('edit', 14)} Adjust</button>
                        <button class="btn sm" data-action="stock-history" data-id="${s.id}">${icon('clock', 14)} History</button>
                      </div></td>
                    </tr>`
                  )
                  .join('')}
              </tbody></table>`
              : SH.empty({ icon: 'stock', title: 'No stock records', text: 'Add products to start tracking stock.' })
          }
        </div>
      </div>`;
    },
  };

  A.action('stock-refresh', async () => {
    A.state.stock = await A.api('listStock', {});
    A.render();
  });

  A.action('stock-adjust', (ds) => {
    const s = (A.state.stock || []).find((x) => String(x.id) === String(ds.id));
    if (!s) return;
    A.modal(
      `<form data-form="stock-adjust">
        <input type="hidden" name="product_id" value="${s.id}" />
        ${SH.modalHead('Adjust stock', s.name)}
        <div class="modal-body">
          <div class="form-grid">
            ${SH.field('Quantity on hand', SH.input('quantity', U.num(s.quantity), { type: 'number', step: '0.001', required: true }), { hint: `Currently ${U.qty(s.quantity)} ${esc(s.unit || '')}. Enter the real counted quantity.` })}
            ${SH.field('Reason', SH.input('reason', '', { placeholder: 'e.g. stock count, breakage', required: true }), { span: 2 })}
          </div>
        </div>
        ${SH.modalFoot('Save adjustment')}
      </form>`,
      { size: '' }
    );
  });

  A.action('stock-history', async (ds) => {
    const moves = await A.tryApi('stockMovements', Number(ds.id), 'Could not load history');
    if (!moves) return;
    A.modal(
      `${SH.modalHead('Stock history', 'Last 100 movements')}
      <div class="modal-body">
        ${
          moves.length
            ? `<table class="data"><thead><tr><th>When</th><th>Movement</th><th class="num">Qty</th><th class="num">Unit cost</th></tr></thead><tbody>
              ${moves
                .map(
                  (m) => `<tr><td class="small muted">${esc(fmtDate(m.occurred_at, true))}</td><td><span class="badge ${m.movement === 'in' ? 'good' : m.movement === 'out' ? 'bad' : 'info'}">${esc(
                    m.movement
                  )}</span></td><td class="num mono">${m.quantity > 0 ? '+' : ''}${U.qty(m.quantity)}</td><td class="num mono">${money(m.unit_cost)}</td></tr>`
                )
                .join('')}
              </tbody></table>`
            : '<p class="muted small">No movements yet.</p>'
        }
      </div>
      <div class="modal-foot"><button type="button" class="btn" data-modal-cancel>Close</button></div>`,
      { size: 'wide' }
    );
  });

  /* -------------------------------------------------------- login actions */

  A.action('pick-user', (ds) => {
    A.state.loginUser = Number(ds.id);
    A.state.pin = '';
    document.querySelectorAll('#user-select .user-pill').forEach((p) => p.classList.remove('active'));
    const el = document.querySelector(`#user-select .user-pill[data-id="${ds.id}"]`);
    if (el) el.classList.add('active');
    A._paintPin();
  });

  A.action('pin', (ds) => {
    const pin = A.state.pin || '';
    if (pin.length >= 8) return;
    A.state.pin = pin + String(ds.key);
    A._paintPin();
  });

  A.action('pin-fn', (ds) => {
    if (ds.key === 'clear') A.state.pin = '';
    else if (ds.key === 'back') A.state.pin = (A.state.pin || '').slice(0, -1);
    A._paintPin();
  });

  A.action('pin-submit', () => A._submitPin());

  /* ------------------------------------------------------------- keyboard */

  document.addEventListener('keydown', (e) => {
    if (!A.state.user) return;
    if ((e.ctrlKey || e.metaKey) && String(e.key).toLowerCase() === 'm') {
      e.preventDefault();
      if (!document.getElementById('modal-root').children.length) A.go('dashboard');
      return;
    }
    if (A.state.route !== 'sales') return;
    if (document.getElementById('modal-root').children.length) return;
    const tag = (document.activeElement && document.activeElement.tagName) || '';
    const typing = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';

    if (e.key === 'F3') {
      e.preventDefault();
      const el = document.getElementById('pos-search');
      if (el) {
        el.focus();
        el.select();
      }
      return;
    }
    if (e.key === 'F2') {
      e.preventDefault();
      openDiscount();
      return;
    }
    if (e.key === 'F10') {
      e.preventDefault();
      if (A.state.cart.items.length) startPayment();
      return;
    }
    if (e.key === 'F12') {
      e.preventDefault();
      quickPay();
      return;
    }
    if (typing) return;
    if (e.key === 'F4') {
      e.preventDefault();
      if (A.state.selectedLine != null) {
        const it = A.state.cart.items[A.state.selectedLine];
        if (it) {
          A.prompt({ title: 'Quantity', label: it.name, value: String(it.quantity), type: 'number', confirmLabel: 'Set' }).then((v) => {
            if (v == null) return;
            const n = U.num(v, 0);
            if (n > 0) {
              it.quantity = n;
              A.render();
            }
          });
        }
      }
      return;
    }
    if (e.key === 'F8') {
      e.preventDefault();
      if (A.state.cart.items.length) A.actions['pos-hold']();
      return;
    }
    if (e.key === 'Delete') {
      if (A.state.selectedLine != null) {
        e.preventDefault();
        A.state.cart.items.splice(A.state.selectedLine, 1);
        A.state.selectedLine = null;
        A.render();
      }
    }
  });

  async function quickPay() {
    if (!A.state.day || !A.state.day.register_open) {
      A.toast(t('Register is closed. Open the register before selling.'), 'warn', 4200);
      return A.go('day');
    }
    if (!A.state.cart.items.length) return A.toast('Add items first', 'warn');
    const cash = (A.state.paymentTypes || []).find((p) => p.kind === 'cash' && p.active) || (A.state.paymentTypes || []).find((p) => p.active);
    if (!cash) return A.toast('No active payment type', 'bad');
    const calc = cartCalc();
    try {
      const doc = await A.api('createSale', {
        user_id: A.state.user.id,
        register_id: (A.state.registers || []).find((r) => r.is_primary)?.id,
        customer_id: A.state.cart.customer_id,
        items: A.state.cart.items.map((it) => ({
          product_id: it.product_id,
          quantity: it.quantity,
          unit_price: it.price / 100,
          discount_type: it.discount_type,
          discount_value: it.discount_value,
        })),
        discount: A.state.cart.discount,
        tax_override_percent:
          A.state.cart.tax_override != null && A.state.cart.tax_override !== '' ? A.state.cart.tax_override : null,
        round_to: A.state.cart.rounding || 0,
        payments: [{ payment_type_id: cash.id, amount: calc.total / 100, tendered: calc.total / 100 }],
      });
      A.state.cart = { items: [], customer_id: null, discount: { type: 'percent', value: 0 }, rounding: 0, notes: '' };
      A.state.selectedLine = null;
      A.state.lastReceipt = doc;
      A.render();
      A.toast(`Paid with ${cash.name} · ${money(doc.total)}`);
      showReceipt(doc);
      await A.reloadCatalog();
    } catch (err) {
      A.toast(err.message, 'bad', 4500);
    }
  }

  /* ------------------------------------------------------------ shortcuts */

  A.screens.shortcuts = {
    meta: { title: 'Keyboard shortcuts' },
    render() {
      return SH.empty({ icon: 'info', title: 'Keyboard shortcuts', text: 'Use the help menu.' });
    },
  };

  /* ------------------------------------------------------ report helpers */

  function reportHtml(title, rows, note) {
    return `<!doctype html><html><head><meta charset="utf-8"><style>
      body{font-family:"Segoe UI",Arial,sans-serif;padding:28px;color:#0f172a}
      h1{font-size:19px;margin:0 0 2px} .sub{color:#64748b;font-size:12px;margin-bottom:18px}
      table{width:100%;border-collapse:collapse;font-size:12.5px}
      th{text-align:left;background:#f1f5f9;border-bottom:1px solid #e2e8f0;padding:7px 9px;font-size:11px;text-transform:uppercase;letter-spacing:.4px}
      td{padding:7px 9px;border-bottom:1px solid #eef2f7;vertical-align:top}
      .r{text-align:right;font-variant-numeric:tabular-nums}
      .muted{color:#64748b}
    </style></head><body>
      <h1>${esc(title)}</h1><div class="sub">${esc(A.state.company.name || 'Aroklon')} · ${esc(fmtDate(new Date().toISOString(), true))}</div>
      <table><thead><tr>${rows[0].map((h) => `<th>${esc(h)}</th>`).join('')}</tr></thead><tbody>
        ${rows
          .slice(1)
          .map((r) => `<tr>${r.map((c) => `<td>${esc(c)}</td>`).join('')}</tr>`)
          .join('')}
      </tbody></table>
      ${note ? `<p class="muted" style="font-size:11px;margin-top:16px">${esc(note)}</p>` : ''}
    </body></html>`;
  }

  SH.reportHtml = reportHtml;
})();