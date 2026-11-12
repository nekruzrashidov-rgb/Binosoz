/* The test bank's card page (/payment/mock-card?id=…&amount=…&currency=…&returnUrl=…&description=…).
   Stands in for Alif / Korti Milli while there is no real gateway: the card is checked here and never leaves the page,
   the "bank" decision goes to /payment/mock/simulate (Development only), then the buyer returns to returnUrl. */
(function () {
  const S = window.S;
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  const cfg = window.SITORA_CONFIG || (window.SITORA_CONFIG = {});
  if (!cfg.API_BASE) cfg.API_BASE = '/api/v1';

  const q = new URLSearchParams(location.search);
  const id = q.get('id') || '';
  const amount = Number(q.get('amount'));
  const currency = q.get('currency') === 'USD' ? 'USD' : 'TJS';
  const description = q.get('description') || '';
  // Only a path on this site: the link must not send the buyer anywhere else.
  const back = (() => { const r = q.get('returnUrl') || ''; return r.startsWith('/') && !r.startsWith('//') && !r.startsWith('/\\') ? r : '/cabinet?success=1'; })();
  const sum = () => S.fmt.money(amount, currency);
  const app = document.getElementById('app');
  const wait = (ms) => new Promise((r) => setTimeout(r, ms));

  // Field checks: the number as XXXX XXXX XXXX XXXX, MM/YY not in the past, CVV of 3–4 digits, the name in capital Latin letters.
  const digits = (v) => v.replace(/\D/g, '');
  const checks = {
    number: (v) => (digits(v).length === 16 ? null : t('pay.err.number')),
    expiry: (v) => {
      const m = /^(\d{2})\/(\d{2})$/.exec(v);
      if (!m || +m[1] < 1 || +m[1] > 12) return t('pay.err.expiry');
      const now = new Date(); const y = 2000 + +m[2];
      return y < now.getFullYear() || (y === now.getFullYear() && +m[1] < now.getMonth() + 1) ? t('pay.err.expired') : null;
    },
    cvv: (v) => (/^\d{3,4}$/.test(v) ? null : t('pay.err.cvv')),
    holder: (v) => (/^[A-Z]+(?: [A-Z]+)+$/.test(v.trim()) ? null : t('pay.err.holder')),
  };
  const brand = (n) => (/^4/.test(n) ? 'VISA' : /^(5[1-5]|2[2-7])/.test(n) ? 'Mastercard' : /^9762/.test(n) ? 'Корти Миллӣ' : '');

  function shell(...children) {
    app.replaceChildren(h('div', { class: 'pay-wrap' },
      h('div', { class: 'pay-head' }, h('a', { class: 'brand', href: '/', 'aria-label': 'Binosoz' }, S.logo(), h('span', null, 'BINOSOZ')),
        h('span', { class: 'pay-secure' }, S.icon('lock'), t('pay.secure'))),
      ...children));
  }

  function form() {
    const card = {
      number: h('div', { class: 'pc-number' }), holder: h('div', { class: 'pc-holder' }), expiry: h('div', { class: 'pc-exp' }),
      brand: h('div', { class: 'pc-brand' }), cvv: h('div', { class: 'pc-cvv' }),
    };
    const flip = h('div', { class: 'pc-inner' },
      h('div', { class: 'pc-face pc-front' }, h('div', { class: 'row between' }, h('div', { class: 'pc-chip', 'aria-hidden': 'true' }), card.brand), card.number,
        h('div', { class: 'row between pc-bottom' }, h('div', null, h('small', null, 'CARD HOLDER'), card.holder), h('div', null, h('small', null, 'VALID THRU'), card.expiry))),
      h('div', { class: 'pc-face pc-back' }, h('div', { class: 'pc-stripe' }), h('div', { class: 'pc-sign' }, card.cvv), h('small', { class: 'pc-note' }, t('pay.backNote'))));
    const cardEl = h('div', { class: 'pay-card', 'aria-hidden': 'true' }, flip);

    // autocomplete off: this is a test page, the browser should not offer a real saved card.
    const inp = (name, attrs) => h('input', Object.assign({ name, id: 'pay-' + name, autocomplete: 'off', spellcheck: 'false' }, attrs));
    const f = {
      number: inp('number', { inputmode: 'numeric', maxlength: 19, placeholder: '0000 0000 0000 0000' }),
      expiry: inp('expiry', { inputmode: 'numeric', maxlength: 5, placeholder: 'MM/YY' }),
      cvv: inp('cvv', { inputmode: 'numeric', maxlength: 4, placeholder: '•••', type: 'password' }),
      holder: inp('holder', { maxlength: 26, placeholder: 'IVAN IVANOV' }),
    };
    const errs = {};
    const field = (name, label, extra) => { errs[name] = h('div', { class: 'pay-err', id: 'pay-' + name + '-err', 'aria-live': 'polite' }); f[name].setAttribute('aria-describedby', errs[name].id);
      return h('label', { class: 'pay-field' + (extra ? ' ' + extra : ''), for: f[name].id }, h('span', null, label), f[name], errs[name]); };

    function draw() {
      const n = digits(f.number.value);
      card.number.textContent = (n + '•'.repeat(16 - n.length)).replace(/(.{4})(?=.)/g, '$1 ');
      card.holder.textContent = f.holder.value.trim() || 'YOUR NAME';
      card.expiry.textContent = f.expiry.value || 'MM/YY';
      card.cvv.textContent = f.cvv.value.replace(/./g, '•') || 'CVV';
      card.brand.textContent = brand(n);
    }
    const check = (name) => { const e = checks[name](f[name].value); errs[name].textContent = e || ''; f[name].classList.toggle('bad', !!e); return !e; };

    f.number.addEventListener('input', () => { f.number.value = digits(f.number.value).slice(0, 16).replace(/(\d{4})(?=\d)/g, '$1 '); draw(); });
    f.expiry.addEventListener('input', (e) => {
      let d = digits(f.expiry.value).slice(0, 4);
      if (d.length === 1 && +d > 1) d = '0' + d;
      f.expiry.value = d.length > 2 || (d.length === 2 && e.inputType !== 'deleteContentBackward') ? d.slice(0, 2) + '/' + d.slice(2) : d;
      draw();
    });
    f.cvv.addEventListener('input', () => { f.cvv.value = digits(f.cvv.value).slice(0, 4); draw(); });
    f.holder.addEventListener('input', () => { const p = f.holder.selectionStart; f.holder.value = f.holder.value.toUpperCase().replace(/[^A-Z ]/g, '').replace(/ {2,}/g, ' '); f.holder.setSelectionRange(p, p); draw(); });
    f.cvv.addEventListener('focus', () => cardEl.classList.add('flipped'));
    f.cvv.addEventListener('blur', () => cardEl.classList.remove('flipped'));
    // A field marked wrong is checked again as it is corrected, so the message goes away at once.
    Object.keys(f).forEach((k) => { f[k].addEventListener('blur', () => { if (f[k].value) check(k); }); f[k].addEventListener('input', () => { if (f[k].classList.contains('bad')) check(k); }); });

    const pay = h('button', { class: 'btn gold pay-submit', type: 'submit' }, t('pay.submit', { sum: sum() }));
    const decline = h('button', { class: 'btn ghost sm', type: 'button', onclick: () => submit(false) }, t('pay.decline'));
    const el = h('form', { class: 'pay-form', novalidate: true, onsubmit: (e) => { e.preventDefault(); submit(true); } },
      field('number', t('pay.number'), 'wide'), field('expiry', t('pay.expiry')), field('cvv', t('pay.cvv')), field('holder', t('pay.holder'), 'wide'),
      h('div', { class: 'wide' }, pay), h('div', { class: 'wide row between pay-links' }, h('a', { href: back.replace(/([?&])success=1/, '$1payment=cancelled') }, t('pay.cancel')), decline));

    async function submit(success) {
      if (success) { const bad = Object.keys(f).filter((k) => !check(k)); if (bad.length) { f[bad[0]].focus(); return; } }
      processing();
      try {
        // The bank answers at once; the pause shows what a real bank looks like while it talks to the card issuer.
        const [r] = await Promise.all([S.api.simulatePayment(id, success), wait(2000)]);
        if (r && r.status === 'Paid') done(); else failed();
      } catch (e) { failed(e && e.message); }
    }

    draw();
    shell(h('div', { class: 'pay-grid' },
      h('section', { class: 'pay-side' }, cardEl, summary(), h('p', { class: 'pay-test' }, t('pay.testHint'))),
      h('section', { class: 'pay-panel' }, h('h1', null, t('pay.title')), el)));
    f.number.focus();
  }

  function summary() {
    return h('div', { class: 'pay-sum' }, h('small', null, t('pay.amount')), h('b', null, sum()), description ? h('p', null, h('span', null, t('pay.purpose') + ': '), description) : null);
  }

  function processing() {
    shell(h('div', { class: 'pay-state', role: 'status' }, h('div', { class: 'pay-spinner', 'aria-hidden': 'true' }), h('h2', null, t('pay.processing')), h('p', { class: 'muted' }, sum())));
  }

  function done() {
    let n = 3;
    const left = h('p', { class: 'muted' }, t('pay.successText', { n }));
    shell(h('div', { class: 'pay-state ok', role: 'status' }, h('div', { class: 'pay-mark', 'aria-hidden': 'true' }, '✓'), h('h2', null, t('pay.successTitle')), h('p', null, sum()),
      left, h('a', { class: 'btn gold', href: back }, t('pay.back'))));
    const timer = setInterval(() => { n--; left.textContent = t('pay.successText', { n }); if (n <= 0) { clearInterval(timer); location.href = back; } }, 1000);
  }

  function failed(message) {
    shell(h('div', { class: 'pay-state bad', role: 'alert' }, h('div', { class: 'pay-mark', 'aria-hidden': 'true' }, '✕'), h('h2', null, t('pay.failTitle')),
      h('p', { class: 'muted' }, message || t('pay.failText')), h('a', { class: 'btn gold', href: back.replace(/([?&])success=1/, '$1payment=failed') }, t('pay.back'))));
  }

  async function boot() {
    await S.initLang();
    // The tab title in the page language (the HTML holds the Russian one).
    if (S.lang !== 'ru') document.title = t('pay.title') + ' — ' + t('brand.name');
    document.documentElement.dataset.theme = S.store.get('sitora.theme') === 'dark' ? 'dark' : 'light';
    if (!id || !(amount > 0)) { shell(h('div', { class: 'pay-state bad', role: 'alert' }, h('div', { class: 'pay-mark', 'aria-hidden': 'true' }, '!'), h('h1', null, t('pay.title')), h('p', { class: 'muted' }, t('pay.err.link')), h('a', { class: 'btn gold', href: '/cabinet' }, t('pay.back')))); return; }
    form();
  }
  if (document.readyState === 'loading') window.addEventListener('DOMContentLoaded', boot); else boot();
})();
