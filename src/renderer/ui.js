'use strict';

/* ==================================================================== */
/*  Aroklon — shared UI utilities and application core                   */
/* ==================================================================== */

(function () {
  const ICONS = {
    sales: '<circle cx="9" cy="20" r="1.4"/><circle cx="18" cy="20" r="1.4"/><path d="M3 4h2l2.2 11.2a1.5 1.5 0 0 0 1.5 1.2h8.6a1.5 1.5 0 0 0 1.5-1.2L21 8H6"/>',
    dashboard: '<rect x="3" y="3" width="7" height="9" rx="1.5"/><rect x="14" y="3" width="7" height="5" rx="1.5"/><rect x="14" y="12" width="7" height="9" rx="1.5"/><rect x="3" y="16" width="7" height="5" rx="1.5"/>',
    products: '<path d="m12 3 8 4.5v9L12 21l-8-4.5v-9L12 3Z"/><path d="m4 7.5 8 4.5 8-4.5"/><path d="M12 12v9"/>',
    stock: '<path d="m12 3 9 5-9 5-9-5 9-5Z"/><path d="m3 13 9 5 9-5"/><path d="m3 17.5 9 5 9-5"/>',
    documents: '<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5Z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h4"/>',
    customers: '<circle cx="9" cy="8" r="3.2"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0"/><path d="M16 5.5a3 3 0 0 1 0 5.8"/><path d="M18 20a6.5 6.5 0 0 0-2-4.7"/>',
    reports: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
    users: '<circle cx="12" cy="8" r="3.5"/><path d="M5 20a7 7 0 0 1 14 0"/>',
    settings:
      '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-1.8-.3 1.6 1.6 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.6 1.6 0 0 0-1-1.5 1.6 1.6 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.6 1.6 0 0 0 .3-1.8 1.6 1.6 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.6 1.6 0 0 0 1.5-1 1.6 1.6 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.6 1.6 0 0 0 1.8.3H9a1.6 1.6 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.6 1.6 0 0 0 1 1.5 1.6 1.6 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0-.3 1.8V9a1.6 1.6 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.6 1.6 0 0 0-1.5 1Z"/>',
    cash: '<rect x="2.5" y="6" width="19" height="12" rx="2"/><circle cx="12" cy="12" r="2.6"/><path d="M6 9v6M18 9v6"/>',
    day: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
    moon: '<path d="M20.5 14.5A8.5 8.5 0 0 1 9.5 3.5a8.5 8.5 0 1 0 11 11Z"/>',
    logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="m16 17 5-5-5-5"/><path d="M21 12H9"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    minus: '<path d="M5 12h14"/>',
    trash: '<path d="M4 7h16"/><path d="M9 7V5h6v2"/><path d="M6 7l1 13h10l1-13"/><path d="M10 11v6M14 11v6"/>',
    edit: '<path d="M4 20h4l10-10-4-4L4 16v4Z"/><path d="m13.5 6.5 4 4"/>',
    close: '<path d="M6 6l12 12M18 6 6 18"/>',
    print: '<path d="M6 9V3h12v6"/><rect x="3" y="9" width="18" height="8" rx="2"/><path d="M6 17h12v4H6z"/>',
    check: '<path d="m5 12 5 5 9-10"/>',
    down: '<path d="m6 9 6 6 6-6"/>',
    right: '<path d="m9 6 6 6-6 6"/>',
    barcode: '<path d="M3 5v14M7 5v14M10 5v14M14 5v14M17 5v14M21 5v14"/>',
    tag: '<path d="M20 13 13 20l-9-9V4h7l9 9Z"/><circle cx="7.5" cy="7.5" r="1.2"/>',
    heart: '<path d="M20.8 5.6a5 5 0 0 0-7.1 0L12 7.3l-1.7-1.7a5 5 0 0 0-7.1 7.1L12 21l8.8-8.3a5 5 0 0 0 0-7.1Z"/>',
    menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
    percent: '<path d="M19 5 5 19"/><circle cx="6.5" cy="6.5" r="2.5"/><circle cx="17.5" cy="17.5" r="2.5"/>',
    pause: '<rect x="6" y="4" width="4" height="16" rx="1"/><rect x="14" y="4" width="4" height="16" rx="1"/>',
    split: '<path d="M8 3 4 7l4 4"/><path d="M4 7h8a6 6 0 0 1 6 6v1"/><path d="m16 21 4-4-4-4"/><path d="M20 17H11"/>',
    warning: '<path d="M12 3 2 21h20L12 3Z"/><path d="M12 9v5M12 17.5v.5"/>',
    save: '<path d="M5 3h11l4 4v14H5z"/><path d="M8 3v6h7V3"/><path d="M8 21v-7h8v7"/>',
    upload: '<path d="M12 16V4"/><path d="m7 9 5-5 5 5"/><path d="M4 16v4h16v-4"/>',
    download: '<path d="M12 4v12"/><path d="m7 11 5 5 5-5"/><path d="M4 20h16"/>',
    receipt: '<path d="M6 3h12v18l-3-2-3 2-3-2-3 2V3Z"/><path d="M9 8h6M9 12h6"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    store: '<path d="M4 9h16l-1-5H5L4 9Z"/><path d="M5 9v11h14V9"/><path d="M9 20v-6h6v6"/>',
    filter: '<path d="M3 5h18l-7 8v6l-4 2v-8L3 5Z"/>',
    refresh: '<path d="M20 11a8 8 0 1 0 .5 4"/><path d="M20 4v7h-7"/>',
    info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v5M12 8v.5"/>',
    globe: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a15 15 0 0 1 0 18M12 3a15 15 0 0 0 0 18"/>',
    card: '<rect x="2.5" y="5" width="19" height="14" rx="2.5"/><path d="M2.5 10h19"/><path d="M6 15h4"/>',
    qr: '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><path d="M14 14h3v3h-3zM20 14v7h-7"/>',
    trend: '<path d="m3 17 6-6 4 4 8-8"/><path d="M15 7h6v6"/>',
    back: '<path d="m14 6-6 6 6 6"/>',
    lock: '<rect x="4" y="10" width="16" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/>',
    mail: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/>',
    phone: '<path d="M5 3h4l2 5-2 2a12 12 0 0 0 5 5l2-2 5 2v4a2 2 0 0 1-2 2A17 17 0 0 1 3 5a2 2 0 0 1 2-2Z"/>',
  };

  function icon(name, size = 18, cls = '') {
    const body = ICONS[name] || ICONS.info;
    return `<svg class="${cls}" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${body}</svg>`;
  }

  const esc = (v) =>
    String(v == null ? '' : v)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');

  const attr = (v) => esc(v);
  const t = (key, vars) => (window.I18N ? window.I18N.t(key, vars) : key);
  const locale = () => (window.I18N ? window.I18N.locale() : 'id-ID');

  function money(c, opts = {}) {
    const st = window.App ? window.App.state : null;
    const symbol = (opts.symbol != null ? opts.symbol : st && st.company && st.company.symbol) || 'Rp';
    const n = (Number(c) || 0) / 100;
    const decimals = Math.abs(n % 1) > 0.0001 ? 2 : 0;
    const s = n.toLocaleString(locale(), { minimumFractionDigits: decimals, maximumFractionDigits: 2 });
    return `${symbol} ${s}`;
  }

  function moneyShort(c) {
    const n = (Number(c) || 0) / 100;
    if (Math.abs(n) >= 1e9) return (n / 1e9).toFixed(1).replace('.', ',') + 'B';
    if (Math.abs(n) >= 1e6) return (n / 1e6).toFixed(1).replace('.', ',') + 'M';
    if (Math.abs(n) >= 1e3) return (n / 1e3).toFixed(0) + 'K';
    return String(n);
  }

  function num(v, d = 0) {
    const n = Number(v);
    return Number.isFinite(n) ? n : d;
  }

  function qty(v) {
    const n = num(v);
    return Number.isInteger(n) ? String(n) : n.toFixed(2).replace(/0+$/, '').replace(/\.$/, '');
  }

  function fmtDate(iso, withTime = false) {
    if (!iso) return '—';
    const d = /^\d{4}-\d{2}-\d{2}$/.test(String(iso)) ? new Date(iso + 'T00:00:00') : new Date(iso);
    if (isNaN(d.getTime())) return String(iso);
    const date = d.toLocaleDateString(locale(), { day: '2-digit', month: 'short', year: 'numeric' });
    if (!withTime) return date;
    return `${date} ${d.toLocaleTimeString(locale(), { hour: '2-digit', minute: '2-digit' })}`;
  }

  function fmtTime(iso) {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleTimeString(locale(), { hour: '2-digit', minute: '2-digit' });
  }

  function todayKey() {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  function initials(name) {
    return String(name || '?')
      .split(/\s+/)
      .slice(0, 2)
      .map((p) => p[0])
      .join('')
      .toUpperCase();
  }

  function effectiveRate(rates) {
    let f = 1;
    (rates || []).forEach((r) => (f *= 1 + num(r.percent) / 100));
    return f - 1;
  }

  /* Client-side mirror of the server sale calculation, for instant totals. */
  function computeCart(items, discount, taxOverride, roundTo) {
    let subtotal = 0;
    let itemDiscTotal = 0;
    const lines = (items || []).map((it, i) => {
      const gross = Math.round(num(it.quantity) * num(it.price));
      let disc = 0;
      if (it.discount_type === 'percent') disc = (gross * num(it.discount_value)) / 100;
      else if (it.discount_type === 'fixed') disc = Math.round(num(it.discount_value) * 100);
      disc = Math.min(Math.max(disc, 0), gross);
      subtotal += gross;
      itemDiscTotal += disc;
      return {
        ...it,
        index: i,
        gross,
        item_disc: disc,
        net_after_item: gross - disc,
        is_service: !!it.is_service,
        incl: !!it.price_incl_tax,
        taxes: it.taxes || [],
      };
    });

    const afterItem = lines.reduce((s, l) => s + l.net_after_item, 0);
    let cartDisc = 0;
    if (discount && num(discount.value) > 0) {
      cartDisc = discount.type === 'percent' ? (afterItem * num(discount.value)) / 100 : Math.round(num(discount.value) * 100);
      cartDisc = Math.min(Math.max(cartDisc, 0), afterItem);
    }

    const promo = bestPromotion(lines, afterItem, Math.max(afterItem - cartDisc, 0));
    if (promo.amount > 0) cartDisc = Math.min(cartDisc + promo.amount, afterItem);

    let taxTotal = 0;
    let total = 0;
    lines.forEach((l) => {
      const share = afterItem > 0 ? (l.net_after_item / afterItem) * cartDisc : 0;
      l.discount_total = l.item_disc + share;
      const base = Math.max(l.net_after_item - share, 0);
      const rates = taxOverride != null && taxOverride !== '' ? [{ percent: num(taxOverride) }] : l.taxes;
      const rate = effectiveRate(rates);
      const tax = l.incl ? base - base / (1 + rate) : base * rate;
      l.tax_total = Math.round(tax);
      l.total = l.incl ? Math.round(base) : Math.round(base + l.tax_total);
      taxTotal += l.tax_total;
      total += l.total;
    });

    const totalBeforeRounding = total;
    const increment = Math.max(Math.round(num(roundTo)), 0);
    let roundingTotal = 0;
    if (increment > 0 && total > 0) {
      const rounded = Math.max(increment, Math.round(total / increment) * increment);
      roundingTotal = rounded - total;
      total = rounded;
    }

    return {
      lines,
      subtotal: Math.round(subtotal),
      item_discount: Math.round(itemDiscTotal),
      cart_discount: Math.round(cartDisc),
      discount_total: Math.round(itemDiscTotal + cartDisc),
      promotion_name: promo.promotion ? promo.promotion.name : null,
      promotion_total: Math.round(promo.amount),
      rounding_total: Math.round(roundingTotal),
      tax_total: taxTotal,
      total_before_rounding: totalBeforeRounding,
      total,
    };
  }

  /* Mirrors the promotion engine in db.js so the till total matches the sale. */
  function bestPromotion(lines, afterItem, base, now = new Date()) {
    const promos = (window.App && window.App.state && window.App.state.promotions) || [];
    const pad = (n) => String(n).padStart(2, '0');
    const dateKey = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
    const timeKey = `${pad(now.getHours())}:${pad(now.getMinutes())}`;
    let best = { promotion: null, amount: 0 };
    for (const p of promos) {
      if (!p || !num(p.active)) continue;
      if (p.date_from && dateKey < String(p.date_from).slice(0, 10)) continue;
      if (p.date_to && dateKey > String(p.date_to).slice(0, 10)) continue;
      const days = String(p.days || '').split(',').map((s) => s.trim()).filter(Boolean);
      if (days.length && !days.includes(String(now.getDay()))) continue;
      if (p.time_from && timeKey < p.time_from) continue;
      if (p.time_to && timeKey > p.time_to) continue;
      if (num(p.min_total) > 0 && afterItem < num(p.min_total)) continue;
      const isCart = String(p.kind || '').startsWith('cart');
      const matched = (lines || []).filter((l) => {
        if (isCart) return true;
        if (p.scope === 'product') return String(l.product_id) === String(p.target_id);
        if (p.scope === 'group') return String(l.group_id) === String(p.target_id);
        return true;
      });
      const matcher = isCart ? afterItem : matched.reduce((s, l) => s + num(l.net_after_item), 0);
      let amount = String(p.kind || '').endsWith('percent') ? (matcher * num(p.value)) / 100 : Math.round(num(p.value) * 100);
      amount = Math.min(Math.max(Math.round(amount), 0), base, matcher);
      if (amount > best.amount) best = { promotion: p, amount };
    }
    return best;
  }

  /* ------------------------------------------------------------- toasts */

  function toast(message, type = 'good', ms = 3200) {
    const root = document.getElementById('toast-root');
    const el = document.createElement('div');
    el.className = `toast ${type}`;
    const ic = type === 'good' ? 'check' : type === 'bad' ? 'warning' : type === 'warn' ? 'warning' : 'info';
    const translated = window.I18N ? window.I18N.exact(message) : null;
    const text = translated || message;
    el.innerHTML = `<span class="ti">${icon(ic, 16)}</span><span>${esc(text)}</span>`;
    root.appendChild(el);
    setTimeout(() => {
      el.style.transition = 'opacity .2s, transform .2s';
      el.style.opacity = '0';
      el.style.transform = 'translateY(6px)';
      setTimeout(() => el.remove(), 220);
    }, ms);
  }

  /* ------------------------------------------------------------- modals */

  function modal(html, opts = {}) {
    const root = document.getElementById('modal-root');
    root.innerHTML = `<div class="overlay" data-overlay><div class="modal ${opts.size || ''}" role="dialog" aria-modal="true">${html}</div></div>`;
    if (window.App && App._translateDom) App._translateDom(root);
    const overlay = root.querySelector('[data-overlay]');
    overlay.addEventListener('mousedown', (e) => {
      if (e.target === overlay && opts.dismissible !== false) closeModal();
    });
    const first = root.querySelector('input:not([type=hidden]), select, textarea, .modal-foot .btn.primary, .modal-foot .btn.success, .modal-foot .btn.danger');
    if (first) setTimeout(() => first.focus(), 30);
    return root;
  }

  function closeModal() {
    const root = document.getElementById('modal-root');
    root.innerHTML = '';
    if (App._confirmResolve) {
      const r = App._confirmResolve;
      App._confirmResolve = null;
      r(false);
    }
    if (App._promptResolve) {
      const r = App._promptResolve;
      App._promptResolve = null;
      r(null);
    }
  }

  function confirm(opts) {
    modal(
      `<div class="modal-head"><div class="ic" style="width:34px;height:34px;border-radius:9px;background:${
        opts.danger ? 'var(--bad-bg)' : 'var(--primary-50)'
      };display:grid;place-items:center;color:${opts.danger ? 'var(--bad)' : 'var(--primary)'}">${icon(
        opts.danger ? 'warning' : 'info',
        18
      )}</div><div><h2>${esc(opts.title || 'Are you sure?')}</h2>${opts.sub ? `<div class="sub">${esc(opts.sub)}</div>` : ''}</div></div>
      <div class="modal-body">${opts.message || ''}</div>
      <div class="modal-foot">
        <button class="btn" data-modal-cancel>${esc(opts.cancelLabel || 'Cancel')}</button>
        <button class="btn ${opts.danger ? 'danger' : 'primary'}" data-modal-ok>${esc(opts.confirmLabel || 'Confirm')}</button>
      </div>`,
      { size: 'sm' }
    );
    return new Promise((resolve) => {
      App._confirmResolve = resolve;
    });
  }

  function prompt(opts) {
    modal(
      `<div class="modal-head"><h2>${esc(opts.title || 'Enter a value')}</h2></div>
       <div class="modal-body">
         ${opts.message ? `<p class="muted small mb12">${esc(opts.message)}</p>` : ''}
         <div class="field"><label>${esc(opts.label || 'Value')}</label>
         <input class="input" id="prompt-input" type="${opts.type || 'text'}" value="${attr(opts.value || '')}" ${
        opts.placeholder ? `placeholder="${attr(opts.placeholder)}"` : ''
      } /></div>
       </div>
       <div class="modal-foot">
         <button class="btn" data-modal-cancel>${esc(opts.cancelLabel || 'Cancel')}</button>
         <button class="btn primary" data-modal-ok>${esc(opts.confirmLabel || 'OK')}</button>
       </div>`,
      { dismissible: opts.dismissible }
    );
    const input = document.getElementById('prompt-input');
    if (input) {
      input.focus();
      input.select();
      input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          const v = input.value;
          closeModal();
          if (App._promptResolve) {
            const r = App._promptResolve;
            App._promptResolve = null;
            r(v);
          }
        }
      });
    }
    return new Promise((resolve) => {
      App._promptResolve = resolve;
      App._promptGet = () => (document.getElementById('prompt-input') || {}).value;
    });
  }

  /* --------------------------------------------------------------- app */

  const App = (window.App = {
    state: {
      user: null,
      route: 'sales',
      params: {},
      company: { symbol: 'Rp', currency: 'IDR', name: 'Aroklon Store' },
      settings: {},
      day: { register_open: false, business_date: '', opening_cash: 0 },
      daySummary: null,
      cashMoves: [],
      lastZ: null,
      promotions: [],
      products: [],
      groups: [],
      customers: [],
      users: [],
      paymentTypes: [],
      taxRates: [],
      registers: [],
      countries: [],
      catalogSearch: '',
      catalogMode: 'name',
      groupFilter: null,
      cart: { items: [], customer_id: null, discount: { type: 'percent', value: 0 }, rounding: 0, notes: '' },
      selectedLine: null,
      pay: null,
      report: null,
      theme: (() => {
        try {
          const saved = localStorage.getItem('aroklon.layout') || localStorage.getItem('aroklon.theme');
          const initial = saved || document.documentElement.dataset.layout || 'classic';
          return initial === 'modern' ? 'modern' : 'classic';
        } catch (e) {
          return 'classic';
        }
      })(),
      mode: (() => {
        try {
          const saved = localStorage.getItem('aroklon.mode');
          return ['dark', 'light', 'system'].includes(saved) ? saved : 'dark';
        } catch (e) {
          return 'dark';
        }
      })(),
      settingsTab: 'company',
      update: null,
    },
    actions: {},
    screens: {},
    loaders: {},
    _confirmResolve: null,
    _promptResolve: null,
    _promptGet: null,

    action(name, fn) {
      this.actions[name] = fn;
    },

    loader(name, fn) {
      this.loaders[name] = fn;
    },

    async api(action, payload) {
      try {
        const res = await window.api.invoke(action, payload);
        if (!res.ok) throw new Error(res.error || 'Request failed');
        return res.result;
      } catch (e) {
        throw e;
      }
    },

    async tryApi(action, payload, failMsg) {
      try {
        return await this.api(action, payload);
      } catch (e) {
        toast(failMsg ? `${failMsg}: ${e.message}` : e.message, 'bad', 4200);
        return null;
      }
    },

    money,
    esc,
    attr,
    icon,
    toast,
    modal,
    closeModal,
    confirm,
    prompt,
    computeCart,

    go(route, params = {}) {
      this.state.route = route;
      this.state.params = params;
      this.render();
      const loader = this.loaders[route];
      if (loader) {
        Promise.resolve(loader(params)).then(() => {
          if (this.state.route === route) this.render();
        });
      }
    },

    render() {
      const root = document.getElementById('app');
      const theme = this.state.theme === 'classic' ? 'classic' : 'modern';
      document.documentElement.dataset.layout = theme;
      this.applyMode();

      if (!this.state.user) {
        root.innerHTML = this._loginHtml();
        this._bindLogin();
        return;
      }
      const active = document.activeElement;
      const focusId = active && active.id ? active.id : null;
      let sel = null;
      try {
        sel = active && active.selectionStart != null ? active.selectionStart : null;
      } catch (e) {
        sel = null;
      }

      const screen = this.screens[this.state.route] || this.screens.sales;
      const meta = screen.meta || { title: 'Aroklon' };
      root.innerHTML = theme === 'classic' ? this._shellClassic(screen, meta) : this._shellModern(screen, meta);

      if (focusId) {
        const el = document.getElementById(focusId);
        if (el) {
          el.focus();
          if (sel != null) {
            try {
              el.setSelectionRange(sel, sel);
            } catch (e) {
              /* not a text field */
            }
          }
        }
      }
      this._translateDom(root);
    },

    _translateDom(root) {
      if (!window.I18N || window.I18N.lang !== 'id' || !root) return;
      const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
        acceptNode(node) {
          const p = node.parentElement;
          if (!p) return NodeFilter.FILTER_REJECT;
          if (['SCRIPT', 'STYLE', 'CODE', 'PRE'].includes(p.tagName)) return NodeFilter.FILTER_REJECT;
          if (p.closest('[data-notranslate]')) return NodeFilter.FILTER_REJECT;
          return NodeFilter.FILTER_ACCEPT;
        },
      });
      const nodes = [];
      while (walker.nextNode()) nodes.push(walker.currentNode);
      for (const node of nodes) {
        const raw = node.nodeValue;
        if (!raw || !raw.trim()) continue;
        const lead = (raw.match(/^\s*/) || [''])[0];
        const tail = (raw.match(/\s*$/) || [''])[0];
        const core = raw.trim();
        const tr = window.I18N.exact(core);
        if (tr && tr !== core) node.nodeValue = lead + tr + tail;
      }
      root.querySelectorAll('[placeholder]').forEach((el) => {
        const tr = window.I18N.exact(el.getAttribute('placeholder'));
        if (tr) el.setAttribute('placeholder', tr);
      });
      root.querySelectorAll('[title]').forEach((el) => {
        const tr = window.I18N.exact(el.getAttribute('title'));
        if (tr) el.setAttribute('title', tr);
      });
    },

    setTheme(theme) {
      this.state.theme = theme === 'classic' ? 'classic' : 'modern';
      try {
        localStorage.setItem('aroklon.layout', this.state.theme);
      } catch (e) {}
      document.documentElement.dataset.layout = this.state.theme;
      this.render();
    },

    applyMode() {
      const mode = this.state.mode || 'dark';
      const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
      const effective = mode === 'system' ? (prefersDark ? 'dark' : 'light') : mode;
      document.documentElement.dataset.mode = effective === 'dark' ? 'dark' : 'light';
      return effective;
    },

    setMode(mode) {
      this.state.mode = ['dark', 'light', 'system'].includes(mode) ? mode : 'dark';
      try {
        localStorage.setItem('aroklon.mode', this.state.mode);
      } catch (e) {}
      this.applyMode();
      this.render();
    },

    toggleMode() {
      const current = document.documentElement.dataset.mode === 'dark' ? 'dark' : 'light';
      this.setMode(current === 'dark' ? 'light' : 'dark');
    },

    setLanguage(lang) {
      if (window.I18N) window.I18N.set(lang === 'en' ? 'en' : 'id');
      this.render();
      if (window.api && window.api.setLang) window.api.setLang(lang === 'en' ? 'en' : 'id');
    },

    _shellModern(screen, meta) {
      return `
        <div class="shell">
          ${this._sidebar()}
          <div class="main">
            ${this._topbar(meta)}
            <div class="view ${screen.flush ? 'flush' : ''}" id="view">${screen.render()}</div>
          </div>
        </div>`;
    },

    _shellClassic(screen, meta) {
      const management = ['dashboard', 'documents', 'products', 'stock', 'reports', 'customers', 'users', 'settings', 'promotions'];
      const showTree = management.includes(this.state.route);
      return `
        <div class="shell shell-classic">
          ${this._classicMenu(meta)}
          <div class="classic-body">
            ${showTree ? this._classicTree() : ''}
            <div class="main">
              <div class="view ${screen.flush ? 'flush' : ''}" id="view">${screen.render()}</div>
              ${this._statusbar()}
            </div>
          </div>
        </div>`;
    },

    _classicMenu(meta = {}) {
      const route = this.state.route;
      const theme = this.state.theme;
      const dark = document.documentElement.dataset.mode === 'dark';
      const day = this.state.day || {};
      const user = this.state.user || {};
      const reg = (this.state.registers || []).find((r) => r.is_primary) || (this.state.registers || [])[0] || {};
      const routeLabel = meta.title || 'Point of Sale';
      return `<header class="classic-menubar">
        <details class="cmb-menu cmb-main">
          <summary class="cmb-menu-toggle" title="${esc(t('Menu'))}">
            ${icon('menu', 20)}<span class="cmb-menu-label">${esc(t('Menu'))}</span>${icon('down', 11)}
          </summary>
          <div class="cmb-popup cmb-main-popup">
            <div class="cmb-popup-head">
              <span class="cmb-popup-avatar">${icon('store', 17)}</span>
              <div><b>${esc(t('Point of Sale'))}</b><span data-notranslate>${esc(user.name || '')}</span></div>
            </div>
            <button class="cmb-item ${route === 'dashboard' ? 'active' : ''}" data-action="nav" data-route="dashboard">${icon('dashboard', 17)}<span>${esc(
              t('Management')
            )}</span><span class="cmb-key">${esc(t('Ctrl+M'))}</span></button>
            <button class="cmb-item" data-action="nav" data-route="documents">${icon('documents', 17)}<span>${esc(
              t('View sales history')
            )}</span></button>
            <button class="cmb-item" data-action="nav" data-route="sales">${icon('sales', 17)}<span>${esc(
              t('New sale')
            )}</span><span class="cmb-key">F1</span></button>
            <div class="cmb-sep"></div>
            <button class="cmb-item" data-action="cash">${icon('cash', 17)}<span>${esc(t('Cash in / out'))}</span></button>
            <button class="cmb-item" data-action="nav" data-route="day">${icon('lock', 17)}<span>${esc(t('End of day'))}</span></button>
            <button class="cmb-item" data-action="x-report">${icon('receipt', 17)}<span>${esc(t('X report'))}</span></button>
            <div class="cmb-sep"></div>
            <button class="cmb-item" data-action="shortcuts">${icon('info', 17)}<span>${esc(t('Keyboard shortcuts'))}</span></button>
            <button class="cmb-item" data-action="about">${icon('info', 17)}<span>${esc(t('User info'))}</span></button>
            <button class="cmb-item" data-action="logout">${icon('logout', 17)}<span>${esc(t('Sign out'))}</span></button>
          </div>
        </details>
        <div class="cmb-brand">
          <span class="cmb-logo">${icon('store', 16)}</span>
          <span class="cmb-name">Aroklon</span>
        </div>
        <div class="cmb-route-title">
          <span>${esc(routeLabel)}</span>
          <small data-notranslate>${esc(user.name || '')}</small>
        </div>
        <div class="cmb-right">
          <button class="cmb-status ${day.register_open ? 'open' : 'closed'}" data-action="nav" data-route="day" title="${esc(
            t('Open / close business day')
          )}">
            <span class="dot"></span>${esc(t(day.register_open ? 'Register open' : 'Register closed'))}
          </button>
          <button class="cmb-icon" data-action="cash" title="${esc(t('Cash in / out'))}">${icon('cash', 16)}</button>
          <span class="cmb-chip">${icon('store', 13)} ${esc(reg.name || t('Register'))}</span>
          <span class="cmb-chip cmb-date" data-notranslate>${icon('day', 13)} ${esc(day.business_date || U.todayKey())}</span>
          <span class="cmb-chip" data-notranslate>${icon('users', 13)} ${esc(user.name || '')}</span>
          <button class="cmb-icon" data-action="toggle-mode" title="${esc(t(dark ? 'Light' : 'Dark'))}">${icon(
            dark ? 'day' : 'moon',
            16
          )}</button>
          <button class="cmb-icon" data-action="toggle-theme" title="${esc(t('Theme'))}">${icon('settings', 16)}</button>
          <button class="cmb-icon" data-action="logout" title="${esc(t('Sign out'))}">${icon('logout', 16)}</button>
        </div>
      </header>`;
    },

    _classicTree() {
      const route = this.state.route;
      const tab = this.state.settingsTab || (this.state.params && this.state.params.tab) || 'company';
      const item = (r, label, ic) =>
        `<button class="ct-item ${route === r ? 'active' : ''}" data-action="nav" data-route="${r}">${icon(
          ic,
          17
        )}<span>${esc(t(label))}</span></button>`;
      const sitem = (key, label, ic) =>
        `<button class="ct-item ${route === 'settings' && tab === key ? 'active' : ''}" data-action="nav-settings" data-tab="${key}">${icon(
          ic,
          17
        )}<span>${esc(t(label))}</span></button>`;
      return `<aside class="classic-tree">
        <button class="ct-head" data-action="nav" data-route="sales" title="${esc(t('Back to sales'))}">
          ${icon('back', 19)}<span>${esc(t('Management'))}</span>
        </button>
        <div class="ct-list">
          ${item('dashboard', 'Dashboard', 'dashboard')}
          ${item('documents', 'Documents', 'documents')}
          ${item('products', 'Products', 'products')}
          ${item('stock', 'Stock', 'stock')}
          ${item('reports', 'Reporting', 'reports')}
          ${item('customers', 'Customers & Suppliers', 'customers')}
          ${item('promotions', 'Promotions & Actions', 'heart')}
          ${item('users', 'Users & Security', 'users')}
          <div class="ct-sep"></div>
          ${sitem('tenders', 'Payment Types', 'cash')}
          ${sitem('countries', 'Countries', 'globe')}
          ${sitem('tax', 'Tax Rates', 'percent')}
          ${sitem('company', 'My Company', 'store')}
          ${sitem('registers', 'Cash Registers', 'products')}
          <div class="ct-sep"></div>
          ${item('day', 'End of Day', 'lock')}
          ${sitem('prefs', 'Options', 'settings')}
        </div>
      </aside>`;
    },

    _statusbar() {
      const reg = (this.state.registers || []).find((r) => r.is_primary) || (this.state.registers || [])[0] || {};
      const d = new Date();
      const dateStr = isNaN(d.getTime())
        ? ''
        : d.toLocaleDateString(locale(), { day: '2-digit', month: 'short', year: 'numeric' });
      return `<footer class="classic-status">
        <span>${icon('store', 13)} ${esc(reg.name || t('Register'))}</span>
        <span data-notranslate>${icon('users', 13)} ${esc(this.state.user.name)}</span>
        <span>${icon('day', 13)} ${esc(dateStr)}</span>
        <span class="spacer"></span>
        <span class="ok">${icon('check', 13)} ${esc(t('Database connected'))}</span>
        <span class="ver">v${esc((this.state.meta && this.state.meta.version) || '1.0.0')}</span>
      </footer>`;
    },

    refresh() {
      this.render();
    },

    async reloadCatalog() {
      const [products, groups, customers] = await Promise.all([
        this.api('listProducts', {}),
        this.api('listGroups', {}),
        this.api('listCustomers', { type: 'customer' }),
      ]);
      this.state.products = products;
      this.state.groups = groups;
      this.state.customers = customers;
    },

    async reloadReference() {
      const [users, paymentTypes, taxRates, registers, countries, day, promotions, company, meta] = await Promise.all([
        this.api('listUsers'),
        this.api('listPaymentTypes'),
        this.api('listTaxRates'),
        this.api('listRegisters'),
        this.api('listCountries'),
        this.api('getDayState'),
        this.api('listPromotions'),
        this.api('getCompany'),
        window.api.meta(),
      ]);
      this.state.users = users;
      this.state.paymentTypes = paymentTypes;
      this.state.taxRates = taxRates;
      this.state.registers = registers;
      this.state.countries = countries;
      this.state.day = day;
      this.state.promotions = promotions;
      this.state.company = company;
      this.state.meta = meta;
    },

    async login(userId, pin) {
      const res = await this.api('login', [userId, pin]);
      if (!res.ok) throw new Error(res.error);
      this.state.user = res.user;
      localStorage.setItem('aroklon.user', String(res.user.id));
      await this.reloadReference();
      await this.reloadCatalog();
      this.state.pay = null;
      this.state.cart = { items: [], customer_id: null, discount: { type: 'percent', value: 0 }, rounding: 0, notes: '' };
      const day = this.state.day || {};
      this.state.route = day.register_open ? 'sales' : 'day';
      this.render();
      if (this.state.route === 'day') {
        const loader = this.loaders.day;
        if (loader) Promise.resolve(loader({})).then(() => this.render());
      }
      setTimeout(() => {
        const el = document.getElementById(this.state.route === 'day' ? 'opening-cash' : 'pos-search');
        if (el) el.focus();
      }, 60);
    },

    logout() {
      this.state.user = null;
      localStorage.removeItem('aroklon.user');
      this.state.cart = { items: [], customer_id: null, discount: { type: 'percent', value: 0 }, rounding: 0, notes: '' };
      this.render();
    },

    _navGroups() {
      return [
        {
          label: 'Workspace',
          items: [
            { route: 'sales', label: 'Sales', icon: 'sales' },
            { route: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
          ],
        },
        {
          label: 'Catalogue',
          items: [
            { route: 'products', label: 'Products', icon: 'products' },
            { route: 'stock', label: 'Stock', icon: 'stock' },
          ],
        },
        {
          label: 'Management',
          items: [
            { route: 'documents', label: 'Documents', icon: 'documents' },
            { route: 'customers', label: 'Customers', icon: 'customers' },
            { route: 'reports', label: 'Reports', icon: 'reports' },
            { route: 'promotions', label: 'Promotions', icon: 'tag' },
          ],
        },
        {
          label: 'System',
          items: [
            { route: 'users', label: 'Users', icon: 'users' },
            { route: 'settings', label: 'Settings', icon: 'settings' },
          ],
        },
      ];
    },

    _sidebar() {
      const lowCount = this.state.products.filter(
        (p) => p.active && !p.is_service && p.low_stock > 0 && Number(p.stock ? p.stock.quantity : p.quantity) <= p.low_stock
      ).length;
      const nav = this._navGroups()
        .map(
          (g) => `<div class="nav-label">${esc(t(g.label))}</div>` +
          g.items
            .map(
              (it) => `<button class="nav-item ${this.state.route === it.route ? 'active' : ''}" data-action="nav" data-route="${it.route}">
                ${icon(it.icon, 18)}<span>${esc(t(it.label))}</span>
                ${it.route === 'stock' && lowCount ? `<span class="nav-badge">${lowCount}</span>` : ''}
              </button>`
            )
            .join('')
        )
        .join('');

      return `<aside class="sidebar">
        <div class="brand">
          <div class="brand-mark">${icon('store', 21)}</div>
          <div><div class="brand-name">Aroklon</div><div class="brand-sub">Point of Sale</div></div>
        </div>
        <nav class="nav">${nav}</nav>
        <div class="side-foot">
          <div class="user-card">
            <div class="avatar">${esc(initials(this.state.user.name))}</div>
            <div style="min-width:0;flex:1">
              <div class="user-name" data-notranslate>${esc(this.state.user.name)}</div>
              <div class="user-role">${esc(this.state.user.access_name || 'Cashier')}</div>
            </div>
            <button class="icon-btn" data-action="logout" title="Sign out">${icon('logout', 17)}</button>
          </div>
        </div>
      </aside>`;
    },

    _topbar(meta) {
      const reg = (this.state.registers || []).find((r) => r.is_primary) || (this.state.registers || [])[0] || {};
      const d = new Date();
      const dateStr = d.toLocaleDateString(locale(), { weekday: 'short', day: '2-digit', month: 'short', year: 'numeric' });
      return `<header class="topbar">
        <div>
          <h1>${esc(meta.title || 'Aroklon')}</h1>
          ${meta.sub ? `<div class="sub">${esc(meta.sub)}</div>` : ''}
        </div>
        <div class="topbar-right">
          <span class="chip">${icon('store', 14)} ${esc(reg.name || t('Register'))}</span>
          <span class="chip">${icon('day', 14)} ${esc(dateStr)}</span>
          <button class="btn sm icon-only" data-action="toggle-mode" title="${document.documentElement.dataset.mode === 'dark' ? t('Light') : t('Dark')}">${icon(
            document.documentElement.dataset.mode === 'dark' ? 'day' : 'moon',
            16
          )}</button>
          <button class="btn sm" data-action="toggle-theme" title="${t('Theme')}">${icon('settings', 14)} ${t(
            this.state.theme === 'classic' ? 'Modern' : 'Classic'
          )}</button>
        </div>
      </header>`;
    },

    _loginHtml() {
      const users = this.state.users || [];
      const sel = this.state.loginUser || (users[0] && users[0].id);
      this.state.loginUser = sel;
      const feats = [
        ['sales', 'Ring sales in seconds', 'Barcode, SKU or name search'],
        ['stock', 'Stock that stays honest', 'Every sale and return moves it'],
        ['reports', 'Reports that add up', 'Sales, tax and stock reports'],
      ];
      return `<div class="login">
        <div class="login-art">
          <div class="login-brand">
            <div class="brand-mark">${icon('store', 24)}</div>
            <div><div class="brand-name">Aroklon</div><div class="brand-sub">Point of Sale</div></div>
          </div>
          <div class="login-hero">
            <h2>${esc(t('Your counter,'))}<br /><span>${esc(t('under control.'))}</span></h2>
            <p>${esc(
              t(
                'An offline point of sale for Windows. Sell, track stock and close the day — no internet, no subscription, your data stays on this PC.'
              )
            )}</p>
            <div class="login-feats">
              ${feats
                .map(
                  (f) => `<div class="login-feat"><div class="ic">${icon(f[0], 16)}</div><div>${esc(t(f[1]))}<br /><span style="opacity:.65">${esc(
                    t(f[2])
                  )}</span></div></div>`
                )
                .join('')}
            </div>
          </div>
          <div class="login-foot">${esc(t('Aroklon 1.1.0 · Local database · No account required'))}</div>
        </div>
        <div class="login-panel">
          <div class="login-card">
            <h1>${esc(t('Sign in to your till'))}</h1>
            <p class="muted">${esc(t('Choose your user and enter your PIN'))}</p>
            <div class="user-select" id="user-select">
              ${users
                .map(
                  (u) => `<button class="user-pill ${String(u.id) === String(sel) ? 'active' : ''}" data-action="pick-user" data-id="${u.id}">
                    <span class="avatar">${esc(initials(u.name))}</span>
                    <span data-notranslate>${esc(u.name)}</span>
                  </button>`
                )
                .join('')}
            </div>
            <div class="field" style="margin:12px 0 4px">
              <label for="pin-input">${esc(t('PIN'))}</label>
              <input id="pin-input" class="input" type="password" inputmode="numeric" maxlength="8" autocomplete="off" placeholder="${esc(
                t('Enter PIN')
              )}" style="height:48px;text-align:center;letter-spacing:10px;font-size:20px;font-weight:600" />
            </div>
            <div class="pin-pad" id="pin-pad">
              ${[1, 2, 3, 4, 5, 6, 7, 8, 9]
                .map((n) => `<button class="pin-key" data-action="pin" data-key="${n}">${n}</button>`)
                .join('')}
              <button class="pin-key fn" data-action="pin-fn" data-key="clear">${esc(t('Clear'))}</button>
              <button class="pin-key" data-action="pin" data-key="0">0</button>
              <button class="pin-key fn" data-action="pin-fn" data-key="back">${icon('back', 16)}</button>
            </div>
            <button class="btn primary block" style="margin-top:12px;height:46px;font-size:14px" data-action="pin-submit">${esc(
              t('Sign in')
            )}</button>
            <div class="login-err" id="login-err"></div>
            <div class="demo-hint">${esc(t('Type your PIN and press Enter'))} · Owner 1234 &nbsp;|&nbsp; Cashier 1111</div>
            <div class="row" style="justify-content:center;margin-top:14px">
              <select class="input" style="width:auto;min-width:200px" data-change="login-lang">
                <option value="id" ${window.I18N && I18N.lang === 'id' ? 'selected' : ''}>Bahasa Indonesia</option>
                <option value="en" ${window.I18N && I18N.lang === 'en' ? 'selected' : ''}>English</option>
              </select>
            </div>
          </div>
        </div>
      </div>`;
    },

    _bindLogin() {
      this.state.pin = '';
      const input = document.getElementById('pin-input');
      if (input) {
        input.value = '';
        setTimeout(() => {
          try {
            input.focus();
          } catch (e) {}
        }, 60);
        input.addEventListener('input', () => {
          const digits = String(input.value).replace(/\D/g, '').slice(0, 8);
          if (input.value !== digits) input.value = digits;
          this.state.pin = digits;
        });
        input.addEventListener('keydown', (e) => {
          if (e.key === 'Enter') {
            e.preventDefault();
            this._submitPin();
          }
        });
      }
      this._paintPin();
    },

    _paintPin() {
      const pin = this.state.pin || '';
      const input = document.getElementById('pin-input');
      if (input && document.activeElement !== input) input.value = pin;
    },

    async _submitPin() {
      const pin = this.state.pin || '';
      if (pin.length < 4) {
        const err = document.getElementById('login-err');
        if (err) {
          err.textContent = 'Enter at least 4 digits';
          err.classList.add('show');
          setTimeout(() => err.classList.remove('show'), 2200);
        }
        return;
      }
      const err = document.getElementById('login-err');
      try {
        await this.login(Number(this.state.loginUser), pin);
      } catch (e) {
        this.state.pin = '';
        this._paintPin();
        const input = document.getElementById('pin-input');
        if (input) {
          input.value = '';
          input.focus();
        }
        if (err) {
          err.textContent = e.message || 'Sign in failed';
          err.classList.add('show');
        }
        setTimeout(() => err && err.classList.remove('show'), 2600);
      }
    },
  });

  window.U = { icon, esc, attr, money, moneyShort, num, qty, fmtDate, fmtTime, todayKey, initials, computeCart, t, locale };

  /* modal control buttons */
  document.addEventListener('click', (e) => {
    const ok = e.target.closest('[data-modal-ok]');
    const cancel = e.target.closest('[data-modal-cancel]');
    if (ok) {
      if (App._promptResolve) {
        const v = App._promptGet ? App._promptGet() : '';
        const r = App._promptResolve;
        App._promptResolve = null;
        App._promptGet = null;
        App.closeModal();
        r(v);
      } else if (App._confirmResolve) {
        const r = App._confirmResolve;
        App._confirmResolve = null;
        App.closeModal();
        r(true);
      } else {
        const form = ok.closest('form');
        if (form) form.requestSubmit();
        else App.closeModal();
      }
    }
    if (cancel) App.closeModal();
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
      const root = document.getElementById('modal-root');
      if (root && root.children.length) {
        e.preventDefault();
        App.closeModal();
        return;
      }
      if (App.state.user) {
        const sel = document.querySelector('.line.selected');
        if (sel) {
          App.state.selectedLine = null;
          App.render();
        }
      }
    }
  });

  App.icon = icon;
  App.esc = esc;
  App.attr = attr;
  App.money = money;
  App.moneyShort = moneyShort;
  App.fmtDate = fmtDate;
  App.fmtTime = fmtTime;
  App.todayKey = todayKey;
  App.initials = initials;
})();