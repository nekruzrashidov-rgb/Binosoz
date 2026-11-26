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
        // CSS variables (--k, --p) need setProperty: Object.assign on style silently skips them.
        else if (k === 'style' && typeof v === 'object') Object.entries(v).forEach(([p, val]) => { if (p.startsWith('--')) el.style.setProperty(p, String(val)); else el.style[p] = val; });
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
    bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9"/><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0"/>',
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
    star: '<path d="M12 3.5l2.6 5.3 5.8.8-4.2 4.1 1 5.8L12 16.8l-5.2 2.7 1-5.8-4.2-4.1 5.8-.8z"/>',
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
  // Each copy of the logo gets its own gradient id (lg1, lg2…): the same id twice on a page is invalid HTML, and Safari
  // drops the gradient when the first copy is hidden (the mobile menu, the footer off screen).
  let logoN = 0;
  // The complex's logo of the design (docs/design): a tower, the outline in the ink colour, the floors in copper.
  S.towerLogo = () => {
    const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    s.setAttribute('viewBox', '0 0 34 34'); s.setAttribute('width', '34'); s.setAttribute('height', '34'); s.setAttribute('aria-hidden', 'true'); s.setAttribute('class', 'tower-logo');
    s.innerHTML = '<rect x="9" y="6" width="16" height="26" fill="none" stroke="currentColor" stroke-width="1.6"/><path d="M13 11h8M13 16h8M13 21h8M13 26h8" stroke="var(--accent-text)" stroke-width="1.4"/><path d="M17 2v4" stroke="currentColor" stroke-width="1.6"/>';
    return s;
  };
  S.logo = () => { const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); const id = 'lg' + (++logoN); s.setAttribute('viewBox', '0 0 32 32'); s.setAttribute('width', 28); s.setAttribute('height', 28); s.setAttribute('aria-hidden', 'true'); s.innerHTML = '<defs><linearGradient id="' + id + '" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#e0874a"/><stop offset="1" stop-color="#9a4f1c"/></linearGradient></defs><path d="M16 1.5l4 9.3 10 1.2-7.4 6.9 2 10L16 23.7 7.4 28.9l2-10L2 12l10-1.2z" fill="url(#' + id + ')"/><path d="M16 1.5l4 9.3 10 1.2-7.4 6.9 2 10L16 23.7z" fill="#f0c89a" opacity=".92"/>'; return s; };
  // The complex's name comes from the API (the organization's name, GET /public/brand → S.brandInfo); until it answers
  // and without the API — «Rich Tower» (brand.name). The address of config.js is shown in the page language
  // (contact.address), any other as it is.
  const RU_ADDRESS = 'г. Душанбе, пр. Борбад, 15';
  // «Отправляя, вы соглашаетесь с политикой конфиденциальности» — под формами заявки и регистрации.
  S.privacyNote = () => S.h('p', { class: 'consent-note' }, S.t('privacy.consent') + ' ', S.h('a', { href: '/privacy' }, S.t('privacy.consentLink')), '.');
  S.brandName = () => { const v = String((S.brandInfo && S.brandInfo.name) || '').trim(); return v || S.t('brand.name'); };
  S.brandAddress = (address) => { const v = String(address || '').trim().replace(/\s+/g, ' '); return v === RU_ADDRESS ? S.t('contact.address') : v; };

  // ---------- formatting ----------
  const nf = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 0 });
  S.fmt = {
    n: (v) => (v === null || v === undefined || v === '' || Number.isNaN(+v) ? '—' : nf.format(Math.round(+v)).replace(/ /g, ' ')),
    money(v, cur) { if (v === null || v === undefined || v === '') return '—'; const s = S.fmt.n(v); return (cur || 'TJS') === 'USD' ? '$' + s : s + ' TJS'; },
    date(d) { if (!d) return '—'; const x = new Date(d); if (Number.isNaN(x.getTime())) return String(d); return x.toLocaleDateString('ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric' }); },
    dt(d) { if (!d) return '—'; const x = new Date(d); if (Number.isNaN(x.getTime())) return String(d); return x.toLocaleString('ru-RU', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }); },
    area: (v) => (v ? String(Math.round(v * 10) / 10).replace('.', ',') + ' ' + t('u.m2') : '—'),
    rooms: (r) => (r === 0 ? t('apt.studio') : r + t('apt.roomsShort')),
    size(b) { if (!b) return ''; return b > 1048576 ? (b / 1048576).toFixed(1) + ' ' + t('unit.mb', null, 'МБ') : Math.max(1, Math.round(b / 1024)) + ' ' + t('unit.kb', null, 'КБ'); },
    initials(n) { return String(n || '?').split(/\s+/).filter(Boolean).slice(0, 2).map((x) => x[0]).join('').toUpperCase(); },
    left(iso) { const ms = new Date(iso).getTime() - Date.now(); if (ms <= 0) return t('book.expired'); const h = Math.floor(ms / 3600000), m = Math.floor((ms % 3600000) / 60000); if (h >= 48) return Math.floor(h / 24) + t('u.d') + ' ' + (h % 24) + t('u.h'); return h + t('u.h') + ' ' + m + t('u.min'); },
  };
  // A text of the developer's data in the page language: field + 'Ru' | 'Tg' | 'En' (A-10). Without a translation — the
  // Russian text, and in English it says so: «… (in Russian)». The plain field (`key`) is the API's pick, the last resort.
  S.localText = (o, key) => {
    if (!o) return '';
    const ru = o[key + 'Ru'] || o[key] || '';
    if (S.lang === 'tg') return o[key + 'Tg'] || ru;
    if (S.lang === 'en') return o[key + 'En'] || (ru ? ru + ' (' + t('i18n.ruOnly') + ')' : '');
    return ru;
  };
  // Apartment types by rooms — one rule for the catalog, the layouts, the gallery and the 3D card, so the labels agree:
  // groups 0 (studio), 1, 2, 3, 4 (four rooms and more); the label names the real number of rooms.
  S.roomsKind = {
    types: [0, 1, 2, 3, 4],
    group: (n) => Math.max(0, Math.min(4, Math.floor(+n || 0))),
    label: (n) => { const r = Math.floor(+n || 0); return r > 4 ? t('layouts.typeN', { n: r }) : t('layouts.type' + S.roomsKind.group(r)); },
    // The label of a group card: «4 комнаты и больше», when the group holds apartments with more than four rooms.
    groupLabel: (g, list) => (g === 4 && (list || []).some((a) => a.rooms > 4) ? t('layouts.type4plus') : t('layouts.type' + g)),
    pill: (g) => (g === 0 ? t('apt.studio') : g === 4 ? '4+' : String(g)),
    match: (a, v) => v === 'all' || v === undefined || S.roomsKind.group(a.rooms) === +v,
  };
  // Local calendar date (toISOString would give the UTC date: in Dushanbe, UTC+5, "today" before 5 a.m. would be yesterday).
  const localDay = (d) => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
  S.today = () => localDay(new Date());
  S.addDays = (n) => localDay(new Date(Date.now() + n * 86400000));

  // ---------- chips ----------
  S.chip = (kind, text) => h('span', { class: 'chip ' + kind }, text);

  // Bell in the header: unread count, a list of the latest notifications; asks the API every 60 seconds.
  let bellTimer = null;
  S.notifBell = () => {
    clearInterval(bellTimer);
    const badge = h('span', { class: 'bell-badge', hidden: true });
    const list = h('div', { class: 'bell-list', role: 'list' });
    const panel = h('div', { class: 'bell-panel', hidden: true }, h('div', { class: 'row between bell-head' }, h('b', null, t('notif.title')),
      h('button', { class: 'btn ghost sm', onclick: async () => { await S.api.readAllNotifications().catch(S.err); refresh(); } }, t('notif.readAll'))), list);
    const btn = h('button', { class: 'icon-btn bell-btn', title: t('notif.title'), 'aria-label': t('notif.title'), 'aria-expanded': 'false', onclick: (e) => { e.stopPropagation(); toggle(); } }, S.icon('bell'), badge);
    const box = h('div', { class: 'bell' }, btn, panel);
    const icon = (n) => (n.kind === 'overdue_payment' ? '🔴' : n.kind === 'payment_reminder' ? '🟡' : '🔔');
    function draw(r) {
      const unread = r.unreadCount || 0;
      badge.hidden = !unread; badge.textContent = unread > 99 ? '99+' : String(unread);
      btn.setAttribute('aria-label', t('notif.title') + (unread ? ' (' + unread + ')' : ''));
      list.replaceChildren(...(r.items.length ? r.items.map((n) => h('button', { class: 'bell-item' + (n.isRead ? '' : ' unread'), role: 'listitem', onclick: () => open(n) },
        h('span', { class: 'bell-ic', 'aria-hidden': 'true' }, icon(n)),
        h('span', { class: 'bell-txt' }, n.title ? h('b', null, n.title) : null, h('span', null, n.text), h('small', { class: 'muted' }, S.fmt.date(n.createdAt))))) : [h('p', { class: 'muted bell-empty' }, t('notif.empty'))]));
    }
    async function refresh() {
      if (!box.isConnected && bellTimer) { clearInterval(bellTimer); return; }
      try { draw(await S.api.notifications()); } catch (e) { /* the bell is optional: a failed poll leaves the old list */ }
    }
    async function open(n) {
      if (!n.isRead) await S.api.readNotification(n.id).catch(() => {});
      close();
      if (n.dealId) S.go(S.homeFor(S.api.user)); else refresh();
    }
    const onDoc = (e) => { if (!box.contains(e.target)) close(); };
    const onKey = (e) => { if (e.key === 'Escape') close(); };
    function close() { panel.hidden = true; btn.setAttribute('aria-expanded', 'false'); document.removeEventListener('click', onDoc); document.removeEventListener('keydown', onKey); }
    function toggle() {
      if (!panel.hidden) { close(); return; }
      panel.hidden = false; btn.setAttribute('aria-expanded', 'true'); refresh();
      document.addEventListener('click', onDoc); document.addEventListener('keydown', onKey);
    }
    refresh();
    bellTimer = setInterval(() => { if (!document.hidden) refresh(); }, 60000);
    return box;
  };
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
  // Focus stays inside an open dialog (WCAG 2.4.3, A-07): the rest of the page becomes inert, Tab and Shift+Tab go round
  // the dialog, and when it closes the focus returns to the element that opened it. `root` — the dialog's element that is
  // a direct child of <body> (an overlay); dialogs may open over dialogs: each one restores only what it changed.
  const FOCUSABLE = 'a[href],button:not([disabled]),input:not([disabled]):not([type=hidden]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"]),summary';
  S.trapFocus = (root, box) => {
    box = box || root;
    const opener = document.activeElement;
    // Toasts stay outside: their messages must still be announced while the dialog is open.
    const made = [...document.body.children].filter((el) => el !== root && !el.inert && el.tagName !== 'SCRIPT' && !el.classList.contains('toasts'));
    made.forEach((el) => { el.inert = true; });
    const onKey = (e) => {
      if (e.key !== 'Tab') return;
      const items = [...box.querySelectorAll(FOCUSABLE)].filter((el) => el.offsetParent !== null || el === document.activeElement);
      if (!items.length) { e.preventDefault(); return; }
      const first = items[0], last = items[items.length - 1];
      if (!box.contains(document.activeElement)) { e.preventDefault(); first.focus(); }
      else if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    };
    document.addEventListener('keydown', onKey, true);
    return () => {
      document.removeEventListener('keydown', onKey, true);
      made.forEach((el) => { el.inert = false; });
      if (opener && opener.isConnected && typeof opener.focus === 'function') opener.focus({ preventScroll: true });
    };
  };

  let modalN = 0;
  S.modal = (opts) => {
    let release = null;
    const close = () => { if (!ov.isConnected) return; ov.remove(); document.removeEventListener('keydown', onKey); if (release) release(); if (opts.onClose) opts.onClose(); };
    const onKey = (e) => { if (e.key === 'Escape') close(); };
    const x = h('button', { class: 'icon-btn x', 'aria-label': t('common.close'), onclick: close }, S.icon('x'));
    const body = h('div', { class: 'modal-body' }, opts.body);
    const titleId = opts.title ? 'modal-title-' + (++modalN) : null;
    const m = h('div', { class: 'modal' + (opts.wide ? ' wide' : ''), role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': titleId }, x, opts.title ? h('h2', { id: titleId }, opts.title) : null, body, opts.actions ? h('div', { class: 'actions' }, opts.actions) : null);
    const ov = h('div', { class: 'overlay' + (opts.side ? ' right' : ''), onmousedown: (e) => { if (e.target === ov) close(); } }, m);
    document.body.appendChild(ov); document.addEventListener('keydown', onKey);
    release = S.trapFocus(ov, m);
    setTimeout(() => { if (!ov.isConnected) return; const f = m.querySelector('input:not([type=hidden]),select,textarea'); (f && !opts.noFocus ? f : x).focus(); }, 60);
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
    if (btn.disabled) return; const old = [...btn.childNodes]; btn.disabled = true; btn.prepend(h('span', { class: 'spin' }));
    try { return await fn(); } finally { btn.disabled = false; btn.replaceChildren(...old); }
  };
  S.formData = (form) => { const o = {}; S.$$('[name]', form).forEach((el) => { o[el.name] = el.type === 'checkbox' ? el.checked : el.value; }); return o; };

  // ---------- misc ----------
  S.skeleton = (n, hgt) => h('div', { class: 'stack' }, Array.from({ length: n || 3 }, () => h('div', { class: 'skel', style: { height: (hgt || 18) + 'px' } })));
  // A failed page instead of a blank one: not found (404), the server failed (5xx) or there is no connection.
  // e — the API error (its status picks the text); without it the card is "not found".
  S.errorCard = (e) => {
    const status = e && e.status;
    const kind = !e || status === 404 ? 'notFound' : status === 0 ? 'network' : 'server';
    // In the language of the design: a kicker («Ошибка 404»), a large heading, plain text and the nr-btn buttons.
    const actions = kind === 'notFound'
      ? [h('a', { class: 'btn nr-btn nr-btn--primary', href: '/' }, t('errp.home')), h('a', { class: 'btn nr-btn nr-btn--dark', href: '/apartments' }, t('errp.catalog'))]
      : [h('button', { class: 'btn nr-btn nr-btn--primary', type: 'button', onclick: () => location.reload() }, t('errp.reload')), h('a', { class: 'btn nr-btn nr-btn--dark', href: '/' }, t('errp.home'))];
    return h('section', { class: 'nr nr-sec nr-err page-in', role: 'alert' }, h('div', { class: 'nr-wrap' },
      h('div', { class: 'nr-kicker' }, h('span', { class: 'nr-kicker-line', 'aria-hidden': 'true' }), t('errp.k.' + kind)),
      h('h1', { class: 'nr-page-title' }, t('errp.' + kind)),
      h('p', { class: 'nr-err-text' }, kind === 'notFound' && e && e.message ? e.message : t('errp.' + kind + 'Text')),
      h('div', { class: 'nr-err-actions' }, actions)));
  };
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
  // Journal (CRM «Журнал» and platform «Активность»), C-06 / R-04: readable action, object, author and a short summary;
  // the raw JSON only inside «Подробнее». Unknown codes are shown as they are.
  const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  const REQ = /^(POST|PUT|PATCH|DELETE) (\/\S*)$/;
  const reqPath = (path) => path.replace(/^\/api\/v1/, '').split('/').map((p) => (GUID.test(p) || /^\d+$/.test(p) ? '{id}' : p)).join('/') || '/';
  const json = (v) => { if (v === null || v === undefined || v === '') return null; if (typeof v === 'object') return v; try { return JSON.parse(v); } catch (e) { return v; } };
  const scalar = (v) => (v === null || v === undefined ? '—' : typeof v === 'object' ? JSON.stringify(v) : String(v));
  S.audit = {
    action(code) {
      const m = REQ.exec(code || '');
      if (m) { const path = reqPath(m[2]); return t('audit.req.' + m[1] + ' ' + path, null, t('audit.req.' + m[1], { path }, code)); }
      return t('audit.act.' + code, null, code);
    },
    entity(x) {
      const kind = t('audit.ent.' + x.entity, null, x.entity);
      const parts = [x.apartmentNumber != null ? t('audit.apt', { n: x.apartmentNumber }) : null, x.entityName || null].filter(Boolean);
      if (parts.length) return kind + ' · ' + parts.join(' · ');
      const id = typeof x.entityId === 'string' && x.entityId.length > 12 ? x.entityId.replace(/-/g, '').slice(-8).toUpperCase() : '';
      return id && !/^0+$/.test(id) ? kind + ' #' + id : kind;
    },
    who(x) {
      if (x.bySystem && !x.userEmail) return h('span', { class: 'muted' }, t('audit.system', null, 'система'));
      const name = x.userName && x.userName !== x.userEmail ? x.userName : null;
      return name ? h('span', { title: x.userEmail || '' }, name, x.userEmail ? h('small', { class: 'muted' }, ' · ' + x.userEmail) : null) : (x.userEmail || '—');
    },
    // A short text instead of {"Method":"Password"}: a known phrase for the action (and its method), the server status
    // for platform actions, or «field: before → after» for the fields that changed.
    summary(x) {
      const before = json(x.before), after = json(x.after);
      const a = after && typeof after === 'object' ? after : {}, b = before && typeof before === 'object' ? before : {};
      if (a.Method) { const s = t('audit.sum.' + x.action + '.' + a.Method, null, ''); if (s) return s; }
      if (x.action === 'DemoBookingsRenewed') return t('audit.sum.DemoBookingsRenewed', { n: a.bookings, h: a.hours });
      if (x.action === 'NameSetFromSeedSecret') return t('audit.sum.NameSetFromSeedSecret', { before: b.FullName || '—', after: a.FullName || '—' });
      const fixed = t('audit.sum.' + x.action, null, '');
      if (fixed) return fixed;
      if (REQ.test(x.action || '') && a.Status) return t(a.Status >= 400 ? 'audit.sum.statusFail' : 'audit.sum.status', { s: a.Status });
      const keys = Object.keys(a).filter((k) => typeof a[k] !== 'object' || a[k] === null);
      const changed = keys.filter((k) => JSON.stringify(a[k]) !== JSON.stringify(b[k])).slice(0, 3);
      return changed.map((k) => (k in b ? k + ': ' + scalar(b[k]) + ' → ' + scalar(a[k]) : k + ': ' + scalar(a[k]))).join('; ');
    },
    change(x) {
      const text = S.audit.summary(x);
      const raw = [x.before ? scalar(json(x.before)) : '', x.before && x.after ? ' → ' : '', x.after ? scalar(json(x.after)) : ''].join('');
      if (!raw) return text || '';
      return h('div', null, text ? h('div', null, text) : null,
        h('details', { class: 'audit-raw' }, h('summary', null, t('audit.details')), h('code', null, raw)));
    },
  };
  S.cd = (iso) => { const ms = new Date(iso).getTime() - Date.now(); return h('span', { class: 'cd-pill' + (ms < 6 * 3600000 ? ' red' : '') }, S.fmt.left(iso)); };
})();
