/* UI helpers: DOM builder, formatting, modals, toasts, forms. */
(function () {
  const S = (window.S = window.S || {});
  const t = (...a) => S.t(...a);

  // Accept arrays and skip null/false in append/prepend/replaceChildren, like the h() builder does.
  const flat = (args) => { const out = []; const add = (c) => { if (c === null || c === undefined || c === false) return; if (Array.isArray(c)) c.forEach(add); else out.push(c); }; args.forEach(add); return out; };
  ['append', 'prepend', 'replaceChildren'].forEach((m) => { const orig = Element.prototype[m]; Element.prototype[m] = function (...a) { return orig.apply(this, flat(a)); }; });

  function h(tag, attrs, ...kids) {
    const el = document.createElement(tag);
    if (attrs) {
      for (const k of Object.keys(attrs)) {
        const v = attrs[k];
        if (v === undefined || v === null || v === false) continue;
        if (k === 'class') el.className = v;
        else if (k === 'html') el.innerHTML = v;
        else if (k === 'text') el.textContent = v;
        else if (k === 'style' && typeof v === 'object') Object.assign(el.style, v);
        else if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v);
        else if (k === 'dataset') Object.assign(el.dataset, v);
        else if (v === true) el.setAttribute(k, '');
        else el.setAttribute(k, v);
      }
    }
    const add = (c) => {
      if (c === null || c === undefined || c === false) return;
      if (Array.isArray(c)) c.forEach(add);
      else el.appendChild(c.nodeType ? c : document.createTextNode(String(c)));
    };
    kids.forEach(add);
    return el;
  }
  S.h = h;
  S.$ = (s, r) => (r || document).querySelector(s);
  S.$$ = (s, r) => Array.from((r || document).querySelectorAll(s));
  S.clear = (el) => { while (el.firstChild) el.removeChild(el.firstChild); return el; };
  S.debounce = (fn, ms) => { let id; return (...a) => { clearTimeout(id); id = setTimeout(() => fn(...a), ms); }; };

  // ---------- icons ----------
  const ICONS = {
    sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
    moon: '<path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z"/>',
    menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
    x: '<path d="M6 6l12 12M18 6L6 18"/>',
    home: '<path d="M3 11l9-8 9 8M5 10v10h14V10"/>',
    grid: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
    kanban: '<rect x="3" y="3" width="5" height="18" rx="1.5"/><rect x="10" y="3" width="5" height="12" rx="1.5"/><rect x="17" y="3" width="4" height="8" rx="1.5"/>',
    inbox: '<path d="M3 13l3-8h12l3 8v6H3zM3 13h5l1 3h6l1-3h5"/>',
    file: '<path d="M14 3H6v18h12V7zM14 3v4h4M9 13h6M9 17h6"/>',
    users: '<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20a6.5 6.5 0 0 1 13 0M16 4.5a3.5 3.5 0 0 1 0 7M18 20a6 6 0 0 0-3-5.2"/>',
    wallet: '<path d="M3 7h16a2 2 0 0 1 2 2v10H5a2 2 0 0 1-2-2zM3 7l2-3h12M16 14h2"/>',
    building: '<path d="M5 21V4h9v17M14 9h5v12M3 21h18M8 8h3M8 12h3M8 16h3"/>',
    hammer: '<path d="M14 6l4 4-8 8-4-4zM12 4l2 2M3 21l4-4"/>',
    chart: '<path d="M4 20V4M4 20h16M8 16v-5M12 16V8M16 16v-8"/>',
    shield: '<path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z"/>',
    out: '<path d="M10 4H5v16h5M15 8l4 4-4 4M19 12H9"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    rotate: '<path d="M4 12a8 8 0 0 1 14-5.3L20 9M20 4v5h-5M20 12a8 8 0 0 1-14 5.3L4 15M4 20v-5h5"/>',
    zin: '<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3M11 8v6M8 11h6"/>',
    zout: '<circle cx="11" cy="11" r="7"/><path d="M21 21l-4.3-4.3M8 11h6"/>',
    eye: '<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
    eyeOff: '<path d="M9.9 5.2A10.6 10.6 0 0 1 12 5c6 0 10 7 10 7a17.7 17.7 0 0 1-3.2 3.9M6.6 6.6C3.9 8.4 2 12 2 12s4 7 10 7a9.9 9.9 0 0 0 5.4-1.6"/><path d="M9.9 9.9a3 3 0 0 0 4.2 4.2"/><path d="M3 3l18 18"/>',
    gear: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>',
    send: '<path d="M22 2L11 13M22 2l-7 20-4-9-9-4z"/>',
    clip: '<path d="M21 12l-8.5 8.5a5 5 0 0 1-7-7L14 5a3.5 3.5 0 0 1 5 5l-8.5 8.5a2 2 0 0 1-3-3L15 8"/>',
    phone: '<path d="M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2z"/>',
    pin: '<path d="M12 21s7-6.5 7-12a7 7 0 0 0-14 0c0 5.5 7 12 7 12z"/><circle cx="12" cy="9" r="2.5"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  };
  S.icon = (n, cls) => { const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); s.setAttribute('viewBox', '0 0 24 24'); s.setAttribute('width', 18); s.setAttribute('height', 18); s.setAttribute('fill', 'none'); s.setAttribute('stroke', 'currentColor'); s.setAttribute('stroke-width', 1.8); s.setAttribute('stroke-linecap', 'round'); s.setAttribute('stroke-linejoin', 'round'); s.style.flex = 'none'; if (cls) s.setAttribute('class', cls); s.setAttribute('aria-hidden', 'true'); s.innerHTML = ICONS[n] || ''; return s; };
  S.logo = () => { const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); s.setAttribute('viewBox', '0 0 32 32'); s.setAttribute('width', 28); s.setAttribute('height', 28); s.setAttribute('aria-hidden', 'true'); s.innerHTML = '<defs><linearGradient id="lg1" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#6f8bff"/><stop offset="1" stop-color="#1d4ad0"/></linearGradient></defs><path d="M16 1.5l4 9.3 10 1.2-7.4 6.9 2 10L16 23.7 7.4 28.9l2-10L2 12l10-1.2z" fill="url(#lg1)"/><path d="M16 1.5l4 9.3 10 1.2-7.4 6.9 2 10L16 23.7z" fill="#f2c46a" opacity=".92"/>'; return s; };

  // ---------- formatting ----------
  const nf = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 });
  S.fmt = {
    n: (v) => (v === null || v === undefined || v === '' || Number.isNaN(+v) ? '—' : nf.format(Math.round(+v)).replace(/ /g, ' ')),
    money(v, cur) { if (v === null || v === undefined || v === '') return '—'; const s = S.fmt.n(v); return (cur || 'TJS') === 'USD' ? '$' + s : s + ' TJS'; },
    date(d) { if (!d) return '—'; const x = new Date(d); if (Number.isNaN(x.getTime())) return String(d); return x.toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' }); },
    dt(d) { if (!d) return '—'; const x = new Date(d); if (Number.isNaN(x.getTime())) return String(d); return x.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }); },
    area: (v) => (v ? String(Math.round(v * 10) / 10).replace('.', ',') + ' ' + t('u.m2') : '—'),
    rooms: (r) => (r === 0 ? t('apt.studio') : r + t('apt.roomsShort')),
    size(b) { if (!b) return ''; return b > 1048576 ? (b / 1048576).toFixed(1) + ' МБ' : Math.max(1, Math.round(b / 1024)) + ' КБ'; },
    initials(n) { return String(n || '?').split(/\s+/).filter(Boolean).slice(0, 2).map((x) => x[0]).join('').toUpperCase(); },
    left(iso) { const ms = new Date(iso).getTime() - Date.now(); if (ms <= 0) return t('book.expired'); const h = Math.floor(ms / 3600000), m = Math.floor((ms % 3600000) / 60000); return h + t('u.h') + ' ' + m + t('u.min'); },
  };
  // Local calendar date (toISOString would give the UTC date: in Dushanbe, UTC+5, "today" before 5 a.m. would be yesterday).
  const localDay = (d) => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  S.today = () => localDay(new Date());
  S.addDays = (n) => localDay(new Date(Date.now() + n * 86400000));

  // ---------- chips ----------
  S.chip = (kind, text) => h('span', { class: 'chip ' + kind }, text);
  S.statusChip = (s) => S.chip(s, t('status.' + s));
  S.stageChip = (s) => S.chip(s, t('stage.' + s));

  // ---------- toast ----------
  let toastBox;
  S.toast = (msg, kind) => {
    if (!toastBox) { toastBox = h('div', { class: 'toasts', role: 'status', 'aria-live': 'polite' }); document.body.appendChild(toastBox); }
    const el = h('div', { class: 'toast ' + (kind || '') }, msg);
    toastBox.appendChild(el); setTimeout(() => { el.style.transition = 'opacity .3s'; el.style.opacity = '0'; setTimeout(() => el.remove(), 320); }, kind === 'bad' ? 5200 : 3200);
  };
  S.err = (e) => S.toast((e && e.message) || t('err.unknown'), 'bad');

  // ---------- modal ----------
  S.modal = (opts) => {
    const close = () => { ov.remove(); document.removeEventListener('keydown', onKey); if (opts.onClose) opts.onClose(); };
    const onKey = (e) => { if (e.key === 'Escape') close(); };
    const x = h('button', { class: 'icon-btn x', 'aria-label': t('common.close'), onclick: close }, S.icon('x'));
    const body = h('div', { class: 'modal-body' }, opts.body);
    const m = h('div', { class: 'modal' + (opts.wide ? ' wide' : ''), role: 'dialog', 'aria-modal': 'true' }, x, opts.title ? h('h2', null, opts.title) : null, body, opts.actions ? h('div', { class: 'actions' }, opts.actions) : null);
    const ov = h('div', { class: 'overlay' + (opts.side ? ' right' : ''), onmousedown: (e) => { if (e.target === ov) close(); } }, m);
    document.body.appendChild(ov); document.addEventListener('keydown', onKey);
    setTimeout(() => { const f = m.querySelector('input:not([type=hidden]),select,textarea'); if (f && !opts.noFocus) f.focus(); }, 60);
    return { close, el: m, body, overlay: ov };
  };

  S.confirm = (text, okLabel, danger) => new Promise((res) => {
    let done = false;
    const fin = (v) => { if (!done) { done = true; res(v); } };
    const md = S.modal({ title: t('common.confirm'), body: h('p', null, text), onClose: () => fin(false), actions: [h('button', { class: 'btn ghost', onclick: () => { fin(false); md.close(); } }, t('common.cancel')), h('button', { class: 'btn' + (danger ? ' danger' : ''), onclick: () => { fin(true); md.close(); } }, okLabel || t('common.ok'))] });
  });
  S.askReason = (title, label, required) => new Promise((res) => {
    let done = false; const fin = (v) => { if (!done) { done = true; res(v); } };
    const ta = h('textarea', { class: 'inp', id: 'reason-input', placeholder: label, maxlength: 300 });
    const er = h('div', { class: 'ferr', style: { color: 'var(--bad)', fontWeight: 600, fontSize: '12.5px' } });
    const md = S.modal({ title, body: [ta, er], onClose: () => fin(null), actions: [h('button', { class: 'btn ghost', onclick: () => { fin(null); md.close(); } }, t('common.cancel')), h('button', { class: 'btn', onclick: () => { if (required !== false && !ta.value.trim()) { er.textContent = t('val.reason'); return; } fin(ta.value.trim()); md.close(); } }, t('common.ok'))] });
  });
  S.bus.on('demoDoc', (d) => { S.modal({ title: d.title, body: [h('div', { class: 'paper' }, (d.lines || []).join('\n')), h('p', { class: 'hint', style: { marginTop: '10px' } }, t('demo.docHint'))], wide: false }); });

  // ---------- forms ----------
  S.field = (label, input, hint, id) => { if (id && !input.id) input.id = id; return h('div', { class: 'field', dataset: { name: input.name || '' } }, h('label', { for: input.id || null }, label), input, hint ? h('div', { class: 'hint' }, hint) : null, h('div', { class: 'ferr' })); };
  S.input = (name, o) => h('input', Object.assign({ class: 'inp', name, id: 'f-' + name + '-' + Math.random().toString(36).slice(2, 6), autocomplete: 'off' }, o || {}));
  S.select = (name, options, val, o) => { const s = h('select', Object.assign({ class: 'inp', name, id: 'f-' + name + '-' + Math.random().toString(36).slice(2, 6) }, o || {}), options.map(([v, l]) => h('option', { value: v }, l))); if (val !== undefined) s.value = val; return s; };
  S.showErrors = (form, e) => {
    S.$$('.field.err', form).forEach((f) => { f.classList.remove('err'); S.$('.ferr', f).textContent = ''; });
    let box = S.$('.form-err', form); if (!box) { box = h('div', { class: 'form-err', role: 'alert' }); form.insertBefore(box, form.firstChild); }
    box.hidden = true; if (!e) return;
    let used = 0;
    if (e.errors) {
      Object.keys(e.errors).forEach((k) => {
        const fld = S.$$('.field', form).find((f) => String(f.dataset.name).toLowerCase() === k.toLowerCase().replace(/^\$\./, ''));
        const msg = [].concat(e.errors[k]).join(' ');
        if (fld) { fld.classList.add('err'); S.$('.ferr', fld).textContent = msg; used++; }
      });
    }
    if (!used || !e.errors) { box.textContent = e.message; box.hidden = false; }
  };
  S.busy = async (btn, fn) => {
    if (btn.disabled) return; const old = btn.innerHTML; btn.disabled = true; btn.prepend(h('span', { class: 'spin' }));
    try { return await fn(); } finally { btn.disabled = false; btn.innerHTML = old; }
  };
  S.formData = (form) => { const o = {}; S.$$('[name]', form).forEach((el) => { o[el.name] = el.type === 'checkbox' ? el.checked : el.value; }); return o; };

  // ---------- misc ----------
  S.skeleton = (n, hgt) => h('div', { class: 'stack' }, Array.from({ length: n || 3 }, () => h('div', { class: 'skel', style: { height: (hgt || 18) + 'px' } })));
  S.empty = (title, icon) => h('div', { class: 'empty' }, (() => { const i = S.icon(icon || 'inbox'); i.setAttribute('width', 54); i.setAttribute('height', 54); i.setAttribute('stroke-width', 1.2); return i; })(), h('div', null, title));
  S.ring = (pct, size) => {
    const r = 21, c = 2 * Math.PI * r;
    const el = h('div', { class: 'ring' });
    el.innerHTML = '<svg viewBox="0 0 54 54"><circle class="bg" cx="27" cy="27" r="' + r + '"/><circle class="fg" cx="27" cy="27" r="' + r + '" stroke-dasharray="' + c + '" stroke-dashoffset="' + c + '"/></svg><b>' + Math.round(pct) + '%</b>';
    requestAnimationFrame(() => requestAnimationFrame(() => { S.$('.fg', el).style.strokeDashoffset = c * (1 - Math.min(100, pct) / 100); }));
    return el;
  };
  S.bar = (pct, cls) => { const i = h('i'); const b = h('div', { class: 'bar ' + (cls || '') }, i); requestAnimationFrame(() => requestAnimationFrame(() => { i.style.width = Math.min(100, Math.max(0, pct)) + '%'; })); return b; };
  S.tbl = (cols, rows, o) => {
    o = o || {};
    return h('div', { class: 'tbl-wrap' }, h('table', { class: 'tbl' }, h('thead', null, h('tr', null, cols.map((c) => h('th', { class: c.r ? 'r' : '' }, c.label)))),
      h('tbody', null, rows.length ? rows.map((r) => { const tr = h('tr', { class: (o.onRow ? 'click ' : '') + (o.rowClass ? o.rowClass(r) : ''), onclick: o.onRow ? () => o.onRow(r) : null }, cols.map((c) => { const v = c.render ? c.render(r) : r[c.key]; return h('td', { class: (c.r ? 'r ' : '') + (c.num ? 'num' : '') }, v === undefined || v === null || v === '' ? '—' : v); })); return tr; }) : [h('tr', null, h('td', { colspan: cols.length }, S.empty(o.empty || t('common.nothing'), o.emptyIcon)))])));
  };
  S.copy = (text) => { try { navigator.clipboard.writeText(text).then(() => S.toast(t('common.copied'), 'ok'), () => S.toast(text)); } catch (e) { S.toast(text); } };
  S.cd = (iso) => { const ms = new Date(iso).getTime() - Date.now(); return h('span', { class: 'cd-pill' + (ms < 6 * 3600000 ? ' red' : '') }, S.fmt.left(iso)); };
})();
