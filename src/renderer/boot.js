'use strict';

/* ==================================================================== */
/*  Aroklon — boot: event wiring and application start                   */
/* ==================================================================== */

(function () {
  const A = window.App;
  const U = window.U;
  const { esc, icon } = U;

  window.__lastError = null;
  window.addEventListener('error', (e) => {
    const text = 'RENDER ERROR: ' + e.message + '\n' + (e.error && e.error.stack ? e.error.stack : '');
    window.__lastError = text;
    console.error(text);
  });
  window.addEventListener('unhandledrejection', (e) => {
    const r = e.reason;
    const text = 'PROMISE ERROR: ' + ((r && (r.stack || r.message)) || r);
    window.__lastError = text;
    console.error(text);
  });

  /* --------------------------------------------------------------- click */

  document.addEventListener('click', async (e) => {
    const menu = e.target.closest('details.cmb-menu');
    if (menu) {
      const btn = e.target.closest('button');
      if (btn) menu.open = false;
    }
    document.querySelectorAll('details.cmb-menu[open]').forEach((d) => {
      if (!d.contains(e.target)) d.open = false;
    });

    const el = e.target.closest('[data-action]');
    if (!el) return;
    const name = el.dataset.action;
    const fn = A.actions[name];
    if (!fn) {
      console.warn('No action handler:', name);
      return;
    }
    e.preventDefault();
    try {
      await fn(el.dataset, el, e);
    } catch (err) {
      console.error(err);
      A.toast(err && err.message ? err.message : String(err), 'bad', 4800);
    }
  });

  /* -------------------------------------------------------------- submit */

  document.addEventListener('submit', async (e) => {
    const form = e.target.closest('form[data-form]');
    if (!form) return;
    e.preventDefault();
    const name = form.dataset.form;
    const fn = A.actions['form:' + name];
    if (!fn) {
      console.warn('No form handler:', name);
      return;
    }
    const submit = form.querySelector('button[type="submit"]');
    if (submit) submit.disabled = true;
    try {
      await fn(form);
    } catch (err) {
      console.error(err);
      A.toast(err && err.message ? err.message : String(err), 'bad', 5000);
    } finally {
      if (submit && document.body.contains(submit)) submit.disabled = false;
    }
  });

  /* --------------------------------------------------------------- input */

  document.addEventListener('input', (e) => {
    const el = e.target.closest('[data-input]');
    if (!el) return;
    const fn = A.actions['input:' + el.dataset.input];
    if (fn) {
      try {
        fn(el.dataset, el, e);
      } catch (err) {
        console.error(err);
      }
    }
  });

  /* -------------------------------------------------------------- change */

  document.addEventListener('change', async (e) => {
    const el = e.target.closest('[data-change]');
    if (el) {
      const fn = A.actions['change:' + el.dataset.change];
      if (fn) {
        try {
          await fn(el.dataset, el, e);
        } catch (err) {
          console.error(err);
          A.toast(err.message || String(err), 'bad');
        }
      }
    }

    const file = e.target.closest('[data-file="image"]');
    if (file && file.files && file.files[0]) {
      const f = file.files[0];
      if (f.size > 1.2 * 1024 * 1024) {
        A.toast('Image is too large (max ~1 MB)', 'warn');
        return;
      }
      const reader = new FileReader();
      reader.onload = () => {
        const hidden = document.getElementById('product-image-value');
        if (hidden) hidden.value = String(reader.result);
        const prev = document.getElementById('image-preview');
        if (prev) prev.innerHTML = `<img src="${reader.result}" style="width:100%;height:100%;object-fit:cover" />`;
      };
      reader.readAsDataURL(f);
    }
  });

  /* --------------------------------------------------------------- login keyboard */

  document.addEventListener('keydown', (e) => {
    if (A.state.user) return;
    if (document.getElementById('modal-root').children.length) return;
    const tag = (document.activeElement && document.activeElement.tagName) || '';
    if (tag === 'INPUT' || tag === 'TEXTAREA') return;
    if (/^[0-9]$/.test(e.key)) {
      e.preventDefault();
      if (A.actions['pin']) A.actions['pin']({ key: e.key }, null, e);
    } else if (e.key === 'Backspace') {
      e.preventDefault();
      if (A.actions['pin-fn']) A.actions['pin-fn']({ key: 'back' }, null, e);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      A._submitPin();
    } else if (e.key === 'Escape') {
      A.state.pin = '';
      A._paintPin();
    }
  });

  /* ---------------------------------------------------------------- menu */

  window.api.onMenu((action) => {
    if (action === 'day') A.go('day');
    else if (action === 'cash') A.actions['cash'] && A.actions['cash']();
    else if (action === 'sales') A.go('sales');
    else if (action === 'shortcuts') {
      A.toast('F2 discount · F3 search · F4 quantity · F8 hold · F10 payment · F12 quick pay · Del remove', 'good', 7000);
    } else if (action === 'check-update') {
      if (A.state.route !== 'settings' || (A.state.params && A.state.params.tab) !== 'data') {
        A.go('settings', { tab: 'data' });
      }
      setTimeout(() => {
        if (A.actions['check-update']) A.actions['check-update']();
      }, 500);
    } else if (action === 'print-preview') {
      A.toast('Open Documents or Reports and press Print to preview.', 'good', 4000);
    }
  });

  /* ---------------------------------------------------------------- init */

  A.init = async function () {
    try {
      const [meta, users, company, settings, day, paymentTypes, taxRates, registers, groups, products, customers, allCustomers, accessLevels] =
        await Promise.all([
          window.api.meta(),
          A.api('listUsers'),
          A.api('getCompany'),
          A.api('getSettings'),
          A.api('getDayState'),
          A.api('listPaymentTypes'),
          A.api('listTaxRates'),
          A.api('listRegisters'),
          A.api('listGroups'),
          A.api('listProducts', {}),
          A.api('listCustomers', { type: 'customer' }),
          A.api('listCustomers', {}),
          A.api('listAccessLevels'),
        ]);

      Object.assign(A.state, {
        meta,
        users,
        company,
        settings,
        day,
        paymentTypes,
        taxRates,
        registers,
        groups,
        products,
        customers,
        allCustomers,
        accessLevels,
      });

      try {
        const last = localStorage.getItem('aroklon.user');
        if (last) A.state.loginUser = Number(last);
      } catch (e) {}

      A.render();
    } catch (err) {
      console.error(err);
      document.getElementById('app').innerHTML = `<div style="padding:48px;font-family:'Segoe UI',sans-serif">
        <h1 style="font-size:22px">Aroklon could not start</h1>
        <p style="color:#64748b">The local database could not be opened.</p>
        <pre style="background:#f1f5f9;padding:16px;border-radius:10px;white-space:pre-wrap">${esc(err.message)}</pre>
      </div>`;
    }
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => A.init());
  } else {
    A.init();
  }
})();