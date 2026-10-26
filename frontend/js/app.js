/* App shell: header, theme, language, routing, settings. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  const ROUTES = ['home', 'cabinet', 'crm', 'platform', 'invite'];
  let cleanup = null, token = 0;
  const header = h('header', { class: 'top' });
  const main = h('div', { id: 'view' });
  const app = document.getElementById('app');

  // #invite=<token> is the link from an invitation letter.
  function route() { const r = (location.hash || '').replace(/^#\/?/, '').split(/[?/=]/)[0]; return ROUTES.includes(r) ? r : 'home'; }
  S.go = (r) => { if (location.hash === '#' + r) render(); else location.hash = r; };

  function applyTheme() { const v = S.store.get('sitora.theme'); if (v === 'dark' || v === 'light') document.documentElement.dataset.theme = v; else delete document.documentElement.dataset.theme; }
  function isDark() { const v = document.documentElement.dataset.theme; return v ? v === 'dark' : window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches; }

  function drawHeader() {
    const r = route(); const u = S.api.user; let open = false;
    const goHome = (id) => () => { menu.classList.remove('open'); if (route() !== 'home') { S.go('home'); setTimeout(() => S.scrollTo && S.scrollTo(id), 700); } else S.scrollTo(id); };
    const menu = h('nav', { class: 'nav', 'aria-label': t('nav.main') },
      h('button', { class: r === 'home' ? 'on' : '', onclick: goHome('home') }, t('nav.home')),
      h('button', { onclick: goHome('apartments') }, t('nav.apartments')),
      h('button', { onclick: goHome('calc') }, t('nav.calc')),
      h('button', { onclick: goHome('about') }, t('nav.about')),
      h('button', { onclick: goHome('contact') }, t('nav.contact')),
      h('button', { class: r === 'cabinet' ? 'on' : '', onclick: () => { menu.classList.remove('open'); S.go('cabinet'); } }, t('nav.cabinet')),
      S.api.isStaff() ? h('button', { class: r === 'crm' ? 'on' : '', onclick: () => { menu.classList.remove('open'); S.go('crm'); } }, t('nav.crm')) : null,
      S.api.isPlatform() ? h('button', { class: r === 'platform' ? 'on' : '', onclick: () => { menu.classList.remove('open'); S.go('platform'); } }, t('nav.platform')) : null);
    const lang = h('div', { class: 'seg', role: 'group', 'aria-label': 'Language' }, [['ru', 'RU'], ['tg', 'TJ']].map(([k, l]) => h('button', { class: S.lang === k ? 'on' : '', onclick: () => { if (S.lang !== k) { S.setLang(k); } } }, l)));
    const theme = h('button', { class: 'icon-btn', title: t('nav.theme'), 'aria-label': t('nav.theme'), onclick: () => { S.store.set('sitora.theme', isDark() ? 'light' : 'dark'); applyTheme(); drawHeader(); S.bus.emit('theme'); } }, S.icon(isDark() ? 'sun' : 'moon'));
    const who = u ? h('div', { class: 'row', style: { gap: '8px', flexWrap: 'nowrap' } }, h('span', { class: 'userchip', title: [u.email || u.phone, t('role.' + u.role, null, u.role), u.organizationName].filter(Boolean).join(' · ') }, h('span', { class: 'avatar' }, S.fmt.initials(u.name)), h('span', { class: 'nm' }, u.name, u.organizationName ? h('small', { class: 'org' }, u.organizationName) : null)), h('button', { class: 'icon-btn', title: t('nav.logout'), 'aria-label': t('nav.logout'), onclick: async () => { await S.api.logout(); S.toast(t('nav.loggedOut')); S.go('home'); } }, S.icon('out'))) : h('button', { class: 'btn sm', onclick: () => S.go('cabinet') }, t('nav.login'));
    header.replaceChildren(impBanner(), h('div', { class: 'wrap top-in' }, h('button', { class: 'brand', onclick: () => { S.go('home'); window.scrollTo({ top: 0, behavior: 'smooth' }); }, 'aria-label': 'Sitora' }, S.logo(), h('span', null, 'SITORA')), menu, h('span', { class: 'spacer' }),
      lang, theme, who,
      h('button', { class: 'icon-btn menu-btn', 'aria-label': t('nav.menu'), onclick: () => menu.classList.toggle('open') }, S.icon('menu'))));
    void open;
  }

  // Working "as owner" of an organization: a strip that cannot be missed, with a way back.
  function impBanner() {
    if (!S.api.impersonating) return null;
    const u = S.api.user;
    return h('div', { class: 'imp-banner', role: 'status' }, h('div', { class: 'wrap row between' }, h('span', null, t('imp.banner', { o: (u && u.organizationName) || '' })),
      h('button', { class: 'btn sm', onclick: (e) => S.busy(e.currentTarget, async () => { await S.api.stopImpersonation(); S.toast(t('imp.stopped'), 'ok'); S.go('platform'); }) }, t('imp.stop'))));
  }

  function render() {
    const my = ++token;
    if (cleanup) { try { cleanup(); } catch (e) { console.error(e); } cleanup = null; }
    S.clear(main); drawHeader();
    const r = route(); const view = S.views[r];
    window.scrollTo(0, 0);
    try { const c = view(main); cleanup = typeof c === 'function' ? c : null; } catch (e) { console.error(e); main.appendChild(h('div', { class: 'wrap', style: { paddingTop: '40px' } }, h('div', { class: 'form-err' }, e.message))); }
    void my;
  }
  S.render = render;
  S.bus.on('lang', render);
  S.bus.on('auth', () => drawHeader());
  window.addEventListener('hashchange', render);

  S.openSettings = () => {
    const base = S.apiBase();
    const inp = S.input('api', { value: base, placeholder: 'http://localhost:5194/api/v1' });
    const res = h('div', { class: 'hint' });
    const test = h('button', { class: 'btn ghost', type: 'button', onclick: async () => { const old = S.apiBase(); S.setApiBase(inp.value); try { const r = await fetch(inp.value.replace(/\/+$/, '') + '/buildings'); res.replaceChildren(r.ok ? h('span', { style: { color: 'var(--good)', fontWeight: 700 } }, t('settings.ok')) : h('span', { style: { color: 'var(--bad)', fontWeight: 700 } }, t('settings.http', { s: r.status }))); } catch (e) { res.replaceChildren(h('span', { style: { color: 'var(--bad)', fontWeight: 700 } }, t('settings.cors'))); } S.setApiBase(old); } }, t('settings.test'));
    const save = h('button', { class: 'btn', onclick: () => { S.setApiBase(inp.value); S.api.clearAuth(); if (S.mock) S.mock.reset(); md.close(); S.toast(t('settings.saved'), 'ok'); render(); } }, t('common.save'));
    const demo = h('button', { class: 'btn ghost', onclick: () => { S.setApiBase(''); S.api.clearAuth(); S.mock.reset(); md.close(); S.toast(t('settings.demoOn'), 'ok'); render(); } }, t('settings.useDemo'));
    const md = S.modal({ title: t('settings.title'), body: h('div', { class: 'stack' }, h('p', { class: 'muted' }, S.isDemo() ? t('settings.nowDemo') : t('settings.nowLive', { u: base })), S.field(t('settings.api'), inp, t('settings.apiHint')), test, res, h('p', { class: 'hint' }, t('settings.cors2'))), actions: [demo, save] });
  };

  function boot() {
    S.initLang();
    applyTheme();
    app.replaceChildren(header, main, h('button', { class: 'icon-btn settings-fab', title: t('settings.title'), 'aria-label': t('settings.title'), onclick: () => S.openSettings() }, S.icon('gear')));
    S.api.restore().finally(() => { if (!location.hash) history.replaceState(null, '', '#home'); render(); });
  }
  window.addEventListener('DOMContentLoaded', boot);
  if (document.readyState !== 'loading') boot();
})();
