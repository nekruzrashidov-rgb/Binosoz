/* Page boot for the multi-page site (index, apartments, contact, login, cabinet, crm, platform .html); spa.html still uses app.js.
   The page names itself in <body data-page="...">. Views keep calling S.go('crm') as in the SPA:
   here that is an ordinary link to /crm. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  const PAGES = { home: '/', apartments: '/apartments', contact: '/contact', register: '/register', 'accept-invite': '/accept-invite', login: '/login', cabinet: '/cabinet', crm: '/crm', platform: '/platform', invite: '/accept-invite' };
  const page = document.body.dataset.page;
  const guarded = page === 'cabinet' || page === 'crm' || page === 'platform';
  // The buyer-facing pages are always dark: the design of the showroom is built for it.
  const isPublic = page === 'home' || page === 'apartments' || page === 'contact' || page === 'register' || page === 'accept-invite';
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
    if (isPublic) { document.documentElement.dataset.theme = 'dark'; return; }
    const v = S.store.get('sitora.theme'); if (v === 'dark' || v === 'light') document.documentElement.dataset.theme = v; else delete document.documentElement.dataset.theme;
  }
  function isDark() { const v = document.documentElement.dataset.theme; return v ? v === 'dark' : window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches; }

  function drawHeader() {
    if (page === 'login' || page === 'accept-invite') return;
    if (isPublic) { drawPublicHeader(); return; }
    const u = S.api.user;
    const link = (href, label, on) => h('a', { class: on ? 'on' : '', href }, label);
    const menu = h('nav', { class: 'nav', 'aria-label': t('nav.main') }, link('/', t('nav.home')),
        S.api.isCustomer() ? link('/cabinet', t('nav.cabinet'), page === 'cabinet') : null,
        S.api.isStaff() ? link('/crm', t('nav.crm'), page === 'crm') : null,
        S.api.isPlatform() ? link('/platform', t('nav.platform'), page === 'platform') : null);
    const lang = h('div', { class: 'seg', role: 'group', 'aria-label': 'Language' }, [['ru', 'RU'], ['tg', 'TJ']].map(([k, l]) => h('button', { class: S.lang === k ? 'on' : '', onclick: () => { if (S.lang !== k) S.setLang(k); } }, l)));
    const theme = h('button', { class: 'icon-btn', title: t('nav.theme'), 'aria-label': t('nav.theme'), onclick: () => { S.store.set('sitora.theme', isDark() ? 'light' : 'dark'); applyTheme(); drawHeader(); S.bus.emit('theme'); } }, S.icon(isDark() ? 'sun' : 'moon'));
    const who = !u
      ? h('a', { class: 'btn sm', href: '/login' }, t('nav.login'))
      : h('div', { class: 'row', style: { gap: '8px', flexWrap: 'nowrap' } },
        h('span', { class: 'userchip', title: [u.email || u.phone, t('role.' + u.role, null, u.role), u.organizationName].filter(Boolean).join(' · ') }, h('span', { class: 'avatar' }, S.fmt.initials(u.name)), h('span', { class: 'nm' }, u.name, u.organizationName ? h('small', { class: 'org' }, u.organizationName) : null)),
        h('button', { class: 'icon-btn', title: t('nav.logout'), 'aria-label': t('nav.logout'), onclick: async () => { await S.api.logout(); location.href = '/'; } }, S.icon('out')));
    header.replaceChildren(impBanner(), h('div', { class: 'wrap top-in' }, h('a', { class: 'brand', href: '/', 'aria-label': 'Binosoz' }, S.logo(), h('span', null, 'BINOSOZ')), menu, h('span', { class: 'spacer' }),
      lang, theme, who,
      h('button', { class: 'icon-btn menu-btn', 'aria-label': t('nav.menu'), onclick: () => menu.classList.toggle('open') }, S.icon('menu'))));
  }

  // Buyer-facing pages: the complex, its apartments and contacts; on a phone the menu opens over the whole screen.
  function drawPublicHeader() {
    const link = (href, label, key) => h('a', { class: page === key ? 'on' : '', href, 'aria-current': page === key ? 'page' : null }, label);
    const menu = h('nav', { class: 'nav lux-nav', 'aria-label': t('nav.main') },
      link('/', t('nav.home'), 'home'), link('/apartments', t('nav.apartments'), 'apartments'), link('/contact', t('nav.contact'), 'contact'));
    const lang = h('div', { class: 'seg', role: 'group', 'aria-label': 'Language' }, [['ru', 'RU'], ['tg', 'TJ']].map(([k, l]) => h('button', { class: S.lang === k ? 'on' : '', onclick: () => { if (S.lang !== k) S.setLang(k); } }, l)));
    const toggle = h('button', { class: 'icon-btn menu-btn', 'aria-label': t('nav.menu'), 'aria-expanded': 'false', onclick: () => { const open = menu.classList.toggle('open'); toggle.setAttribute('aria-expanded', String(open)); document.body.classList.toggle('menu-open', open); } }, S.icon('menu'));
    header.className = 'top lux-top';
    header.replaceChildren(h('div', { class: 'wrap top-in' }, h('a', { class: 'brand', href: '/', 'aria-label': 'Binosoz' }, S.logo(), h('span', null, 'BINOSOZ')), menu, h('span', { class: 'spacer' }),
      lang, h('a', { class: 'btn sm gold', href: '/login' }, t('nav.login')), toggle));
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
    return S.views[page];
  }

  function render() {
    if (cleanup) { try { cleanup(); } catch (e) { console.error(e); } cleanup = null; }
    S.clear(main); drawHeader();
    window.scrollTo(0, 0);
    try { const c = view()(main); cleanup = typeof c === 'function' ? c : null; } catch (e) { console.error(e); main.appendChild(h('div', { class: 'wrap', style: { paddingTop: '40px' } }, h('div', { class: 'form-err' }, e.message))); }
  }

  // Links of the SPA (#crm, #invite=...) still arrive at the site root, e.g. from invitation letters.
  function oldLink() {
    const r = (location.hash || '').replace(/^#\/?/, '').split(/[?/=]/)[0];
    return r !== 'home' && PAGES[r] ? PAGES[r] + (r === 'invite' ? location.hash : '') : null;
  }

  async function boot() {
    if (page === 'home' && oldLink()) { location.replace(oldLink()); return; }
    S.initLang();
    applyTheme();
    app.replaceChildren(header, main);
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
