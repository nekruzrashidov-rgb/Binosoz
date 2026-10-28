/* Page boot for the multi-page site (index, login, cabinet, crm, platform .html); spa.html still uses app.js.
   The page names itself in <body data-page="...">. Views keep calling S.go('crm') as in the SPA:
   here that is an ordinary link to /crm. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  const PAGES = { home: '/', login: '/login', cabinet: '/cabinet', crm: '/crm', platform: '/platform', invite: '/login' };
  const page = document.body.dataset.page;
  const guarded = page === 'cabinet' || page === 'crm' || page === 'platform';
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

  function applyTheme() { const v = S.store.get('sitora.theme'); if (v === 'dark' || v === 'light') document.documentElement.dataset.theme = v; else delete document.documentElement.dataset.theme; }
  function isDark() { const v = document.documentElement.dataset.theme; return v ? v === 'dark' : window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches; }

  function drawHeader() {
    if (page === 'login') return;
    const u = S.api.user;
    const link = (href, label, on) => h('a', { class: on ? 'on' : '', href }, label);
    const section = (id, label) => h('button', { type: 'button', onclick: () => { menu.classList.remove('open'); S.scrollTo(id); } }, label);
    const menu = page === 'home'
      ? h('nav', { class: 'nav', 'aria-label': t('nav.main') }, section('home', t('nav.home')), section('apartments', t('nav.apartments')), section('calc', t('nav.calc')), section('about', t('nav.about')), section('contact', t('nav.contact')))
      : h('nav', { class: 'nav', 'aria-label': t('nav.main') }, link('/', t('nav.home')),
        S.api.isCustomer() ? link('/cabinet', t('nav.cabinet'), page === 'cabinet') : null,
        S.api.isStaff() ? link('/crm', t('nav.crm'), page === 'crm') : null,
        S.api.isPlatform() ? link('/platform', t('nav.platform'), page === 'platform') : null);
    const lang = h('div', { class: 'seg', role: 'group', 'aria-label': 'Language' }, [['ru', 'RU'], ['tg', 'TJ']].map(([k, l]) => h('button', { class: S.lang === k ? 'on' : '', onclick: () => { if (S.lang !== k) S.setLang(k); } }, l)));
    const theme = h('button', { class: 'icon-btn', title: t('nav.theme'), 'aria-label': t('nav.theme'), onclick: () => { S.store.set('sitora.theme', isDark() ? 'light' : 'dark'); applyTheme(); drawHeader(); S.bus.emit('theme'); } }, S.icon(isDark() ? 'sun' : 'moon'));
    const who = page === 'home' || !u
      ? h('a', { class: 'btn sm', href: '/login' }, t('nav.login'))
      : h('div', { class: 'row', style: { gap: '8px', flexWrap: 'nowrap' } },
        h('span', { class: 'userchip', title: [u.email || u.phone, t('role.' + u.role, null, u.role), u.organizationName].filter(Boolean).join(' · ') }, h('span', { class: 'avatar' }, S.fmt.initials(u.name)), h('span', { class: 'nm' }, u.name, u.organizationName ? h('small', { class: 'org' }, u.organizationName) : null)),
        h('button', { class: 'icon-btn', title: t('nav.logout'), 'aria-label': t('nav.logout'), onclick: async () => { await S.api.logout(); location.href = '/'; } }, S.icon('out')));
    header.replaceChildren(impBanner(), h('div', { class: 'wrap top-in' }, h('a', { class: 'brand', href: '/', 'aria-label': 'Sitora' }, S.logo(), h('span', null, 'SITORA')), menu, h('span', { class: 'spacer' }),
      lang, theme, who,
      h('button', { class: 'icon-btn menu-btn', 'aria-label': t('nav.menu'), onclick: () => menu.classList.toggle('open') }, S.icon('menu'))));
  }

  // Working "as owner" of an organization: a strip that cannot be missed, with a way back.
  function impBanner() {
    if (page === 'home' || !S.api.impersonating) return null;
    const u = S.api.user;
    return h('div', { class: 'imp-banner', role: 'status' }, h('div', { class: 'wrap row between' }, h('span', null, t('imp.banner', { o: (u && u.organizationName) || '' })),
      h('button', { class: 'btn sm', onclick: (e) => S.busy(e.currentTarget, async () => { await S.api.stopImpersonation(); S.toast(t('imp.stopped'), 'ok'); location.href = '/platform'; }) }, t('imp.stop'))));
  }

  function view() {
    if (page === 'login') return /invite=/.test(location.hash) ? S.views.invite : S.views.login;
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
    if (page === 'login' && S.api.isAuthed && !/invite=/.test(location.hash)) { location.replace(PAGES[S.homeFor(S.api.user)]); return; }
    render();
    S.bus.on('lang', render);
    S.bus.on('auth', () => { if (guarded && !S.api.isAuthed) location.replace('/login'); else drawHeader(); });
  }
  if (document.readyState === 'loading') window.addEventListener('DOMContentLoaded', boot); else boot();
})();
