/* Page boot for the multi-page site (index, apartments, contact, login, cabinet, crm, platform .html); spa.html still uses app.js.
   The page names itself in <body data-page="...">. Views keep calling S.go('crm') as in the SPA:
   here that is an ordinary link to /crm. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  const PAGES = { home: '/', apartments: '/apartments', gallery: '/gallery', about: '/about', contact: '/contact', register: '/register', 'accept-invite': '/accept-invite', login: '/login', cabinet: '/cabinet', crm: '/crm', platform: '/platform', invite: '/accept-invite' };
  const page = document.body.dataset.page;
  const guarded = page === 'cabinet' || page === 'crm' || page === 'platform';
  // The buyer-facing pages are always dark: the design of the showroom is built for it.
  const isPublic = page === 'home' || page === 'apartments' || page === 'gallery' || page === 'about' || page === 'contact' || page === 'register' || page === 'accept-invite' || page === 'notfound';
  const app = document.getElementById('app');
  const header = h('header', { class: 'top' });
  const main = h('div', { id: 'view' });
  let cleanup = null;

  // The pages are served by the API itself and carry no demo backend (mock.js), so the API is on this origin.
  const cfg = window.SITORA_CONFIG || (window.SITORA_CONFIG = {});
  if (!cfg.API_BASE) cfg.API_BASE = '/api/v1';
  if (!S.mock && S.store.get('sitora.api') === '') S.store.del('sitora.api');

  S.go = (r) => {
    if (r === page) render();
    else location.href = PAGES[r] || '/';
  };

  // The API address dialog of app.js, without the demo backend: an empty address means this site's own /api/v1.
  S.openSettings = () => {
    const inp = S.input('api', { value: S.store.get('sitora.api') || '', placeholder: cfg.API_BASE });
    const res = h('div', { class: 'hint' });
    const save = (v) => { v = String(v || '').trim(); if (v) S.setApiBase(v); else S.store.del('sitora.api'); };
    const test = h('button', { class: 'btn ghost', type: 'button', onclick: async () => { const old = S.store.get('sitora.api'); save(inp.value); try { const r = await fetch(S.apiBase() + '/buildings'); res.replaceChildren(r.ok ? h('span', { style: { color: 'var(--good)', fontWeight: 700 } }, t('settings.ok')) : h('span', { style: { color: 'var(--bad)', fontWeight: 700 } }, t('settings.http', { s: r.status }))); } catch (e) { res.replaceChildren(h('span', { style: { color: 'var(--bad)', fontWeight: 700 } }, t('settings.cors'))); } save(old); } }, t('settings.test'));
    const ok = h('button', { class: 'btn', onclick: () => { save(inp.value); S.api.clearAuth(); md.close(); S.toast(t('settings.saved'), 'ok'); render(); } }, t('common.save'));
    const md = S.modal({ title: t('settings.title'), body: h('div', { class: 'stack' }, h('p', { class: 'muted' }, t('settings.nowLive', { u: S.apiBase() })), S.field(t('settings.api'), inp, t('settings.apiHint')), test, res, h('p', { class: 'hint' }, t('settings.cors2'))), actions: [ok] });
  };

  function applyTheme() {
    // Public pages: default light (warm showroom), but allow manual dark toggle saved in localStorage
    if (isPublic || page === 'login') {
      const v = S.store.get('sitora.theme');
      document.documentElement.dataset.theme = (v === 'dark') ? 'dark' : 'light';
      return;
    }
    const v = S.store.get('sitora.theme'); if (v === 'dark' || v === 'light') document.documentElement.dataset.theme = v; else delete document.documentElement.dataset.theme;
  }
  function isDark() { const v = document.documentElement.dataset.theme; return v ? v === 'dark' : window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches; }

  const PHONE = '+992 98 363 70 70';
  const PHONE_HREF = 'tel:+992983637070';

  // Gold bar across the top while the page boots, then it fades out.
  function startProgress() {
    const bar = h('div', { class: 'top-bar', 'aria-hidden': 'true' });
    document.body.prepend(bar);
    requestAnimationFrame(() => { bar.style.width = '35%'; });
    setTimeout(() => {
      bar.style.width = '100%';
      setTimeout(() => { bar.classList.add('done'); setTimeout(() => bar.remove(), 900); }, 400);
    }, 300);
  }

  // RU · TJ · EN (S.LANGS).
  function langSwitch() {
    return h('div', { class: 'seg', role: 'group', 'aria-label': 'Language' }, S.LANGS.map(([k, l]) => h('button', { class: S.lang === k ? 'on' : '', onclick: () => { if (S.lang !== k) S.setLang(k); } }, l)));
  }

  // Staff pages: weekday, date and time, refreshed every 30 seconds; one timer for the page however often the header is redrawn.
  let clockTimer = null;
  function liveClock() {
    const el = h('span', { class: 'top-clock desk-only' });
    const tick = () => {
      const d = new Date();
      const days = t('top.days').split(','), months = t('top.months').split(',');
      el.textContent = `${days[d.getDay()]}, ${d.getDate()} ${months[d.getMonth()]} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    };
    tick();
    if (clockTimer) clearInterval(clockTimer);
    clockTimer = setInterval(() => { if (el.isConnected) tick(); else { clearInterval(clockTimer); clockTimer = null; } }, 30000);
    return el;
  }

  // Phone and tablet menu: a dark full-screen layer with large links, the languages and the phone. Closes on ×, Escape or a link.
  let overlay = null;
  // extra: an optional row under the languages (the public site puts the theme switch there: on phones it leaves the header).
  function buildMobileOverlay(navItems, extra) {
    if (overlay) overlay.remove();
    const close = () => {
      overlay.classList.remove('open');
      document.body.classList.remove('menu-open');
      btn.setAttribute('aria-expanded', 'false');
    };
    const navEls = navItems.map(([href, label, active]) => h('a', { href, class: active ? 'on' : '', 'aria-current': active ? 'page' : null, onclick: close }, label));
    const langRow = h('div', { class: 'mob-lang', role: 'group', 'aria-label': 'Language' },
      S.LANGS.map(([k, l]) => h('button', { class: 'mob-lang-btn' + (S.lang === k ? ' on' : ''), type: 'button', onclick: () => { close(); if (S.lang !== k) S.setLang(k); } }, l)));
    const el = h('div', { class: 'mob-overlay', role: 'dialog', 'aria-modal': 'true', 'aria-label': t('nav.menu') },
      h('button', { class: 'mob-close', type: 'button', 'aria-label': t('common.close'), onclick: close }, '×'),
      h('nav', { class: 'mob-nav', 'aria-label': t('nav.main') }, ...navEls),
      langRow,
      extra || null,
      h('div', { class: 'mob-contact' }, h('a', { href: PHONE_HREF, class: 'mob-phone' }, PHONE)));
    el.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); });
    overlay = el;
    document.body.appendChild(el);
    document.body.classList.remove('menu-open');
    const btn = h('button', { class: 'icon-btn menu-btn', type: 'button', 'aria-label': t('nav.menu'), 'aria-expanded': 'false',
      onclick: () => { el.classList.add('open'); document.body.classList.add('menu-open'); btn.setAttribute('aria-expanded', 'true'); setTimeout(() => el.querySelector('.mob-close').focus(), 30); } }, S.icon('menu'));
    return { btn, overlay: el };
  }

  function drawHeader() {
    if (page === 'login' || page === 'accept-invite') return;
    if (isPublic) { drawPublicHeader(); return; }
    const u = S.api.user;
    const link = (href, label, on) => h('a', { class: on ? 'on' : '', href }, label);
    const menu = h('nav', { class: 'nav', 'aria-label': t('nav.main') }, link('/', t('nav.home')),
        S.api.isCustomer() ? link('/cabinet', t('nav.cabinet'), page === 'cabinet') : null,
        S.api.isStaff() ? link('/crm', t('nav.crm'), page === 'crm') : null,
        S.api.isPlatform() ? link('/platform', t('nav.platform'), page === 'platform') : null);
    const staffNavItems = [
      ['/', t('nav.home'), false],
      S.api.isCustomer() ? ['/cabinet', t('nav.cabinet'), page === 'cabinet'] : null,
      S.api.isStaff() ? ['/crm', t('nav.crm'), page === 'crm'] : null,
      S.api.isPlatform() ? ['/platform', t('nav.platform'), page === 'platform'] : null,
    ].filter(Boolean);
    const { btn: menuBtn } = buildMobileOverlay(staffNavItems);
    const lang = langSwitch();
    const theme = h('button', { class: 'icon-btn', title: t('nav.theme'), 'aria-label': t('nav.theme'), onclick: () => { S.store.set('sitora.theme', isDark() ? 'light' : 'dark'); applyTheme(); drawHeader(); S.bus.emit('theme'); } }, S.icon(isDark() ? 'sun' : 'moon'));
    const who = !u
      ? h('a', { class: 'btn sm', href: '/login' }, t('nav.login'))
      : h('div', { class: 'row', style: { gap: '8px', flexWrap: 'nowrap' } },
        h('span', { class: 'userchip', title: [u.email || u.phone, t('role.' + u.role, null, u.role), u.organizationName].filter(Boolean).join(' · ') }, h('span', { class: 'avatar' }, S.fmt.initials(u.name)), h('span', { class: 'nm' }, u.name, u.organizationName ? h('small', { class: 'org' }, u.organizationName) : null)),
        h('button', { class: 'icon-btn', title: t('nav.logout'), 'aria-label': t('nav.logout'), onclick: async () => { await S.api.logout(); location.href = '/'; } }, S.icon('out')));
    header.replaceChildren(impBanner(), h('div', { class: 'wrap top-in' }, h('a', { class: 'brand', href: '/', 'aria-label': 'Binosoz' }, S.logo(), h('span', null, 'BINOSOZ')), menu, h('span', { class: 'spacer' }),
      S.api.isCustomer() ? null : liveClock(), lang, theme, u && !S.isDemo() ? S.notifBell() : null, who, menuBtn));
  }

  // Buyer-facing pages: the complex, its apartments and contacts; on a phone the menu opens over the whole screen.
  function drawPublicHeader() {
    const link = (href, label, key) => h('a', { class: page === key ? 'on' : '', href, 'aria-current': page === key ? 'page' : null }, label);
    const menu = h('nav', { class: 'nav lux-nav', 'aria-label': t('nav.main') },
      link('/', t('nav.home'), 'home'), link('/apartments', t('nav.apartments'), 'apartments'), link('/gallery', t('nav.gallery'), 'gallery'),
      link('/about', t('nav.project'), 'about'), link('/contact', t('nav.contact'), 'contact'));
    const lang = langSwitch();
    const phone = h('a', { class: 'top-phone desk-only', href: PHONE_HREF }, PHONE);
    const toggleTheme = () => {
      S.store.set('sitora.theme', isDark() ? 'light' : 'dark');
      applyTheme();
      drawPublicHeader();
      S.bus && S.bus.emit('theme');
    };
    const themeLabel = isDark() ? 'Дневной режим' : 'Ночной режим'; // TODO: i18n
    const themeRow = h('div', { class: 'mob-lang' }, h('button', { class: 'mob-lang-btn', type: 'button', 'aria-label': themeLabel, onclick: toggleTheme }, (isDark() ? '☀️ ' : '🌙 ') + t('nav.theme')));
    const { btn: menuBtn } = buildMobileOverlay([
      ['/', t('nav.home'), page === 'home'],
      ['/apartments', t('nav.apartments'), page === 'apartments'],
      ['/gallery', t('nav.gallery'), page === 'gallery'],
      ['/about', t('nav.project'), page === 'about'],
      ['/contact', t('nav.contact'), page === 'contact'],
    ], themeRow);
    lang.classList.add('top-lang');
    const themeToggle = h('button', {
      class: 'icon-btn theme-toggle top-theme',
      title: t('nav.theme'),
      'aria-label': themeLabel,
      onclick: toggleTheme
    }, isDark()
      ? h('span', { style: { fontSize: '18px', lineHeight: 1 } }, '☀️')
      : h('span', { style: { fontSize: '18px', lineHeight: 1 } }, '🌙'));
    header.className = 'top lux-top' + (S.api.isAuthed ? ' authed' : '');
    header.replaceChildren(h('div', { class: 'wrap top-in' }, h('a', { class: 'brand', href: '/', 'aria-label': 'Binosoz' }, S.logo(), h('span', null, 'BINOSOZ')), menu, h('span', { class: 'spacer' }),
      phone, lang, themeToggle, accountLink(), menuBtn));
  }

  // Signed in: the gold button leads to the person's own workspace instead of the login page.
  function accountLink() {
    const u = S.api.isAuthed && S.api.user;
    if (!u) return h('a', { class: 'btn sm gold', href: '/login' }, t('nav.login'));
    const home = u.role === 'SuperAdmin' ? 'platform' : u.role === 'Customer' ? 'cabinet' : 'crm';
    return h('a', { class: 'btn sm gold', href: PAGES[home], title: u.name || '' }, t('nav.' + home));
  }

  // Working "as owner" of an organization: a strip that cannot be missed, with a way back.
  function impBanner() {
    if (isPublic || !S.api.impersonating) return null;
    const u = S.api.user;
    return h('div', { class: 'imp-banner', role: 'status' }, h('div', { class: 'wrap row between' }, h('span', null, t('imp.banner', { o: (u && u.organizationName) || '' })),
      h('button', { class: 'btn sm', onclick: (e) => S.busy(e.currentTarget, async () => { await S.api.stopImpersonation(); S.toast(t('imp.stopped'), 'ok'); location.href = '/platform'; }) }, t('imp.stop'))));
  }

  function view() {
    if (page === 'login') return S.views.login;
    if (page === 'accept-invite') return S.views.acceptInvite;
    if (page === 'notfound') return (root) => { root.appendChild(S.errorCard()); };
    return S.views[page];
  }

  // The tab title in the chosen language: the page's name and the brand (the landing keeps the complex's name).
  const TITLES = { apartments: 'nav.apartments', gallery: 'nav.gallery', about: 'nav.project', contact: 'nav.contact', login: 'nav.login', cabinet: 'nav.cabinet', crm: 'nav.crm', platform: 'nav.platform' };
  const htmlTitle = document.title;
  function setTitle() {
    if (S.lang === 'ru') { document.title = htmlTitle; return; }
    // The complex's own name is translated too ("Nekruz Residence" in English); Binosoz stays as it is.
    const last = htmlTitle.split(' — ').pop();
    const brand = last === 'Некруз Резиденс' ? t('login.heroName') : last;
    document.title = page === 'home' ? t('login.heroName') + ' — ' + t('home.tagline') : TITLES[page] ? t(TITLES[page]) + ' — ' + brand : document.title;
  }

  // Sections marked .section-reveal (and the site's .reveal blocks) slide up once they come into view. They are hidden
  // beforehand only under html.motion-ok: an observer exists, the page is not driven by a robot (audit tools and
  // screenshot services take the page without scrolling) and the person has not asked for less motion. Otherwise
  // everything is simply shown; what is already on screen is shown at once, without waiting for the observer.
  const motionOk = 'IntersectionObserver' in window && !navigator.webdriver
    && !(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  if (motionOk) document.documentElement.classList.add('motion-ok');
  S.motionOk = motionOk;
  const revealer = motionOk
    ? new IntersectionObserver((entries) => entries.forEach((e) => { if (e.isIntersecting) { e.target.classList.add('visible'); revealer.unobserve(e.target); } }), { threshold: 0.05, rootMargin: '0px 0px -50px 0px' })
    : null;
  S.revealSections = (root) => S.$$('.section-reveal:not(.visible)', root || document).forEach((el) => {
    if (!revealer || el.getBoundingClientRect().top < window.innerHeight) el.classList.add('visible');
    else revealer.observe(el);
  });
  window.addEventListener('beforeprint', () => S.$$('.section-reveal, .reveal').forEach((el) => el.classList.add('visible', 'in')));

  // Button ripple: a circle spreads from the point of the press (styles.css, .ripple-active). One listener for every
  // .btn, also those drawn later.
  document.addEventListener('pointerdown', (e) => {
    const b = e.target && e.target.closest ? e.target.closest('.btn') : null;
    if (!b || b.disabled) return;
    const r = b.getBoundingClientRect();
    b.style.setProperty('--rx', e.clientX - r.left + 'px');
    b.style.setProperty('--ry', e.clientY - r.top + 'px');
    b.classList.remove('ripple-active'); void b.offsetWidth; b.classList.add('ripple-active');
    clearTimeout(b._ripple); b._ripple = setTimeout(() => b.classList.remove('ripple-active'), 650);
  }, { passive: true });

  function render() {
    setTitle();
    if (cleanup) { try { cleanup(); } catch (e) { console.error(e); } cleanup = null; }
    S.clear(main); drawHeader();
    window.scrollTo(0, 0);
    try { const c = view()(main); cleanup = typeof c === 'function' ? c : null; } catch (e) { console.error(e); main.replaceChildren(S.errorCard(e)); }
    S.revealSections(main);
  }

  // Links of the SPA (#crm, #invite=...) still arrive at the site root, e.g. from invitation letters.
  function oldLink() {
    const r = (location.hash || '').replace(/^#\/?/, '').split(/[?/=]/)[0];
    return r !== 'home' && PAGES[r] ? PAGES[r] + (r === 'invite' ? location.hash : '') : null;
  }

  // An API error nobody caught (a section loading its data): 401 sends to the login page; a section that is still
  // only placeholders gets an error card in its place; otherwise a short message is enough.
  window.addEventListener('unhandledrejection', (ev) => {
    const e = ev.reason;
    if (!e || typeof e.status !== 'number') return;
    ev.preventDefault();
    if (e.status === 401) { if (!isPublic && page !== 'login') { S.api.clearAuth(); location.replace('/login'); } return; }
    const area = document.getElementById('ws-main') || main;
    const loading = S.$('.skel', area) && !S.$('.tbl, form, .card:not(.skel)', area);
    if (loading && (e.status === 0 || e.status === 404 || e.status >= 500)) area.replaceChildren(S.errorCard(e));
    else S.err(e);
  });

  async function boot() {
    if (page === 'home' && oldLink()) { location.replace(oldLink()); return; }
    S.initLang();
    applyTheme();
    startProgress();
    // Login and invitation pages draw no top bar (drawHeader skips them): an empty <header> left a blank 68px strip.
    if (page === 'login' || page === 'accept-invite') app.replaceChildren(main);
    else app.replaceChildren(header, main);
    // The real height of the top bar (taller with the "as owner" banner): sticky bars under it (CRM tabs) start there.
    if ('ResizeObserver' in window) new ResizeObserver(() => document.documentElement.style.setProperty('--hdr-h', header.offsetHeight + 'px')).observe(header);
    await S.api.restore();
    if (guarded && !S.api.isAuthed) { location.replace('/login'); return; }
    // Invitation links of older letters (/login#invite=…) now open their own page.
    if (page === 'login' && /invite=/.test(location.hash)) { location.replace(PAGES.invite + location.hash); return; }
    if (page === 'login' && S.api.isAuthed) { location.replace(PAGES[S.homeFor(S.api.user)]); return; }
    render();
    // /#why from the footer of another page: the section exists only after render.
    if (location.hash.length > 1) { const el = document.getElementById(location.hash.slice(1)); if (el) setTimeout(() => el.scrollIntoView(), 50); }
    S.bus.on('lang', render);
    S.bus.on('auth', () => { if (guarded && !S.api.isAuthed) location.replace('/login'); else drawHeader(); });
  }
  if (document.readyState === 'loading') window.addEventListener('DOMContentLoaded', boot); else boot();
})();
