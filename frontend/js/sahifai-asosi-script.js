/* Public site, three pages: the landing (home), the catalog with the 3D tower, booking and calculator (apartments),
   and the contact page (contact). */
(function () {
  const S = (window.S = window.S || {});
  const { h, t } = { h: (...a) => S.h(...a), t: (...a) => S.t(...a) };
  S.views = S.views || {};

  const STATUS_VAR = { free: 'var(--good)', booked: 'var(--warn)', sold: 'var(--muted)', onrequest: 'var(--info)' };
  const STATUS_BG = { free: 'var(--good-soft)', booked: 'var(--warn-soft)', sold: 'var(--surface-3)', onrequest: 'var(--info-soft)' };
  // Landing features: icon and the i18n key of the card.
  const FEATURES = [['mountain', 'why.view'], ['screen', 'why.cabinet'], ['card', 'why.plan'], ['crane', 'why.dev'], ['pin', 'why.center'], ['key', 'why.keys']];
  // Public-site icons (24×24, stroke).
  const LAND_ICONS = {
    cube: '<path d="M12 2 3 7v10l9 5 9-5V7z"/><path d="m3 7 9 5 9-5M12 12v10"/>',
    crane: '<path d="M4 21h9"/><path d="M7 21V3l12 4H7"/><path d="M15 7v5"/><path d="M13.5 12h3v2.5h-3z"/><path d="M7 7 4 3h3"/><path d="M7 11h3M7 15h3"/>',
    check: '<path d="m5 12.5 4.5 4.5L19 7.5"/>',
    mountain: '<path d="m2 20 7-11 4 6 3-4 6 9z"/><path d="m7.5 11.5 1.5 1 1.5-1"/>',
    screen: '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/><path d="m8 11 2.5-2.5L13 11l3-3"/>',
    card: '<rect x="2.5" y="5" width="19" height="14" rx="2.5"/><path d="M2.5 10h19M6.5 15h4"/>',
    pin: '<path d="M12 21s-7-6.2-7-11.5A7 7 0 0 1 19 9.5C19 14.8 12 21 12 21z"/><circle cx="12" cy="9.5" r="2.5"/>',
    key: '<circle cx="8" cy="15" r="4.5"/><path d="m11.2 11.8 8.3-8.3M16.5 6.5l2.5 2.5M14 9l2 2"/>',
    phone: '<path d="M5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5 5l1.5-2 4.5 1.5v3A2 2 0 0 1 17 21 15 15 0 0 1 3 5.5a2 2 0 0 1 2-2z"/>',
    send: '<path d="m21.5 3-19 7.5 7 2.5 2.5 7z"/><path d="m9.5 13 5-5"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
    chat: '<path d="M4 20l1.4-3.6A8 8 0 1 1 8.5 19z"/><path d="M9 10.5h6M9 13.5h4"/>',
    down: '<path d="m6 9 6 6 6-6"/>',
    bed: '<path d="M3 18V6M21 18v-5a3 3 0 0 0-3-3H10v8"/><path d="M3 14h18"/><circle cx="6.5" cy="10.5" r="1.8"/>',
    car: '<path d="M5 16V11l2-5h10l2 5v5"/><path d="M3 16h18v2H3z"/><circle cx="7.5" cy="13" r="1"/><circle cx="16.5" cy="13" r="1"/>',
    play: '<path d="M4 21V9l5-5 5 5v12"/><path d="M14 13h4a3 3 0 0 1 3 3v5"/><path d="M7.5 21v-4h3v4"/>',
    shield: '<path d="M12 3 5 6v5c0 4.5 3 8.3 7 10 4-1.7 7-5.5 7-10V6z"/><path d="m9 12 2 2 4-4"/>',
    leaf: '<path d="M5 19c0-8 5-13 15-14-1 10-6 15-14 15"/><path d="M5 19 13 11"/>',
    lock: '<rect x="4.5" y="10.5" width="15" height="10" rx="2"/><path d="M8 10.5V7.5a4 4 0 0 1 8 0v3"/>',
    clipboard: '<rect x="5" y="4" width="14" height="17" rx="2"/><path d="M9 4V3h6v1M9 10h6M9 14h6M9 18h3"/>',
    percent: '<circle cx="7" cy="7" r="2.5"/><circle cx="17" cy="17" r="2.5"/><path d="M19 5 5 19"/>',
    calendar: '<rect x="3.5" y="5" width="17" height="15" rx="2"/><path d="M3.5 10h17M8 3v4M16 3v4"/>',
    image: '<rect x="3" y="4.5" width="18" height="15" rx="2.5"/><circle cx="9" cy="10" r="1.8"/><path d="m21 16-5-5-8 8.5"/>',
    users: '<circle cx="9" cy="8" r="3.2"/><path d="M3 20a6 6 0 0 1 12 0"/><path d="M16 4.6a3.2 3.2 0 0 1 0 6.3M18 20a6 6 0 0 0-3-5.2"/>',
  };
  // Gallery photos: views of Dushanbe from Wikimedia Commons, not the complex (a demo project; there are no photos of it).
  // Files: /img/gallery/<file>-400|800|1200.webp, cropped to 3:2. Author, licence and the file page — under every photo
  // and in img/gallery/CREDITS.md. Licences allowed: CC0, public domain, CC BY, CC BY-SA (no NC, no ND).
  const COMMONS = 'https://commons.wikimedia.org/wiki/File:';
  const SAADIEV = 'Шухрат Саъдиев';
  const GALLERY_PHOTOS = [
    // First — the most striking one: it also opens the landing's gallery.
    { id: 'plaza', file: 'dushanbe-plaza', author: SAADIEV, license: 'CC BY-SA 3.0', page: 'Dushanbe-Plaza.JPG' },
    { id: 'highrise', file: 'dushanbe-highrise', author: SAADIEV, license: 'CC BY-SA 4.0', page: 'Buildings_in_Dushanbe_03.jpg' },
    { id: 'towers', file: 'dushanbe-towers', author: SAADIEV, license: 'CC BY-SA 4.0', page: 'Buildings_in_the_city_of_Dushanbe_04.jpg' },
    { id: 'newstreet', file: 'dushanbe-newstreet', author: SAADIEV, license: 'CC BY-SA 4.0', page: 'Buildings_in_Dushanbe_25.jpg' },
    { id: 'tower', file: 'dushanbe-tower', author: SAADIEV, license: 'CC BY-SA 4.0', page: 'Buildings_in_Dushanbe_31.jpg' },
    { id: 'center', file: 'dushanbe-center', author: SAADIEV, license: 'CC BY-SA 4.0', page: 'Buildings_in_Dushanbe_22.jpg' },
    { id: 'newbuilds', file: 'dushanbe-newbuilds', author: SAADIEV, license: 'CC BY 4.0', page: 'Buildings_in_Dushanbe_06.jpg' },
    { id: 'street', file: 'dushanbe-street', author: SAADIEV, license: 'CC BY-SA 4.0', page: 'Buildings_in_Dushanbe_23.jpg' },
  ];
  function galleryImg(p, sizes, cls) {
    const src = (w) => '/img/gallery/' + p.file + '-' + w + '.webp';
    return h('img', { class: cls, src: src(800), srcset: [400, 800, 1200].map((w) => src(w) + ' ' + w + 'w').join(', '),
      sizes, width: 1200, height: 800, alt: t('galpg.p.' + p.id), loading: 'lazy', decoding: 'async' });
  }
  // «Фото: автор, лицензия» — a link to the file page on Commons (attribution required by CC BY / CC BY-SA).
  const galleryCredit = (p) => h('a', { class: 'photo-credit', href: COMMONS + p.page, target: '_blank', rel: 'noopener' },
    t('galpg.photo') + ': ' + p.author + ', ' + (p.license || t('galpg.pd')));
  const FAQ = ['visit', 'down', 'delivery', 'booking', 'fees'];
  const AMENITIES = [['car', 'about.parking'], ['play', 'about.playground'], ['shield', 'about.guard'], ['leaf', 'about.green']];
  function landIcon(name) {
    const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    s.setAttribute('viewBox', '0 0 24 24'); s.setAttribute('fill', 'none'); s.setAttribute('stroke', 'currentColor'); s.setAttribute('stroke-width', '1.8');
    s.setAttribute('stroke-linecap', 'round'); s.setAttribute('stroke-linejoin', 'round'); s.setAttribute('aria-hidden', 'true');
    s.innerHTML = LAND_ICONS[name];
    return s;
  }
  // No apartments to show (no building yet, an empty catalog, or the API did not answer): one honest card with the
  // phone and a way to leave a request, instead of invented numbers or two error messages.
  function soonCard(state, retry) {
    const c = contacts();
    return h('div', { class: 'soon-block card' },
      h('h3', null, t(state === 'error' ? 'home.soon.error' : 'home.soon.title')),
      h('p', { class: 'muted' }, t('home.soon.text')),
      h('div', { class: 'row' },
        c.phone ? h('a', { class: 'btn gold', href: 'tel:+' + phoneDigits(c.phone) }, landIcon('phone'), c.phone) : null,
        h('a', { class: 'btn ghost', href: '/contact' }, t('home.soon.lead')),
        state === 'error' && retry ? h('button', { class: 'btn ghost', type: 'button', onclick: retry }, t('common.retry')) : null));
  }

  // Three.js and the house model are loaded only when the catalog has a building and its 3D stage is near the screen.
  let threeLoading = null;
  function loadHouse3D() {
    if (window.S && S.House3D) return Promise.resolve();
    if (threeLoading) return threeLoading;
    const add = (src) => new Promise((ok, fail) => { const el = document.createElement('script'); el.src = src; el.onload = ok; el.onerror = fail; document.head.appendChild(el); });
    threeLoading = (window.THREE ? Promise.resolve() : add('https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js'))
      .then(() => add('/js/modeli-3d-bino-script.js'))
      .catch((e) => { threeLoading = null; throw e; });
    return threeLoading;
  }

  // Section headings are plain text: the old "first word in copper" on every title carried no meaning.
  // The name is kept so the pages that call it need no change.
  function accentFirst(text) { return String(text); }
  // "16 этажей", "21 этаж", "71 квартира": the i18n value holds the forms as "one|few|many" (Tajik has one form).
  function plural(n, key) {
    const f = t(key).split('|');
    if (f.length < 3) return f[0];
    if (S.lang === 'en') return n === 1 ? f[0] : f[2];
    const m10 = n % 10, m100 = n % 100;
    return m10 === 1 && m100 !== 11 ? f[0] : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? f[1] : f[2];
  }
  const counted = (n, key) => S.fmt.n(n) + ' ' + plural(n, key);
  // Contacts from config.js; the address in the page language (S.brandAddress).
  const contacts = () => { const c = (window.SITORA_CONFIG || {}).CONTACT || {}; return Object.assign({}, c, { address: S.brandAddress(c.address), manager: c.manager === 'Некруз' ? t('contact.manager') : c.manager }); };
  // Installment terms, one source for the landing's text and the calculator's limits.
  const installment = () => Object.assign({ downFrom: 10, years: 10 }, (window.SITORA_CONFIG || {}).INSTALLMENT);
  const telegramUrl = (tg) => 'https://t.me/' + String(tg || '').replace('@', '');
  const phoneDigits = (p) => String(p || '').replace(/\D/g, '');

  // The organization's first building: leads without an apartment need it to know whose they are.
  async function siteBuilding() {
    if (S.siteBuildingId) return S.siteBuildingId;
    const cfg = window.SITORA_CONFIG || {};
    const bs = await S.api.buildings();
    const b = bs.find((x) => String(x.id) === String(cfg.DEFAULT_BUILDING_ID)) || bs[0];
    if (b) S.siteBuildingId = b.id;
    return S.siteBuildingId;
  }

  // Numbers count up over ~1 s. A timer puts the final value in place in case animation frames are paused
  // (a background tab), so a counter is never left half-counted. fmt turns the number into text.
  function countUp(el, to, fmt) {
    const token = (el._count = (el._count || 0) + 1), from = el._shown || 0;
    const show = (k) => { if (el._count !== token) return; el._shown = Math.round(from + (to - from) * k); el.textContent = fmt(el._shown); };
    if (!S.motionOk) { show(1); return; }
    const t0 = performance.now();
    const step = (now) => { const p = Math.min(1, (now - t0) / 1000); show(1 - Math.pow(1 - p, 3)); if (p < 1 && el._count === token) requestAnimationFrame(step); };
    show(0); requestAnimationFrame(step);
    setTimeout(() => show(1), 1100);
  }

  // Blocks with .reveal fade in as they scroll into view; without IntersectionObserver they are simply shown.
  // onShow runs for each block once it is in (counters start there). Returns the cleanup.
  function revealIn(root, onShow) {
    const els = S.$$('.reveal', root);
    const show = (el) => { el.classList.add('in'); if (onShow) onShow(el); setTimeout(() => { el.style.transitionDelay = ''; }, 900); };
    // Same rule as page.js (S.motionOk): no observer, a robot or less motion — everything at once.
    if (!('IntersectionObserver' in window) || S.motionOk === false) { els.forEach(show); return () => {}; }
    const io = new IntersectionObserver((entries) => entries.forEach((e) => { if (e.isIntersecting) { show(e.target); io.unobserve(e.target); } }), { threshold: 0.05, rootMargin: '0px 0px -40px 0px' });
    // What is already on screen comes in right away (after the first frame, so the transition still plays).
    requestAnimationFrame(() => els.forEach((el) => { if (el.classList.contains('in')) return; if (el.getBoundingClientRect().top < window.innerHeight) show(el); else io.observe(el); }));
    return () => io.disconnect();
  }

  function siteFooter() {
    const c = contacts();
    const col = (title, ...items) => h('div', { class: 'foot-col' }, h('h4', null, title), ...items);
    return h('footer', { class: 'footer lux-footer' }, h('div', { class: 'wrap' },
      h('div', { class: 'foot-grid' },
        h('div', { class: 'foot-brand' }, h('a', { class: 'row', href: '/', style: { gap: '10px' } }, S.logo(), h('b', null, t('brand.name'))), h('p', null, t('foot.tagline')),
          h('div', { class: 'foot-social' },
            c.telegram ? h('a', { class: 'soc tg', href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener', 'aria-label': 'Telegram', title: 'Telegram' }, landIcon('send')) : null,
            c.phone ? h('a', { class: 'soc wa', href: 'https://wa.me/' + phoneDigits(c.phone), target: '_blank', rel: 'noopener', 'aria-label': 'WhatsApp', title: 'WhatsApp' }, landIcon('chat')) : null,
            h('a', { class: 'btn ghost sm', href: '/login' }, t('foot.login')))),
        col(t('foot.apts'), h('a', { href: '/apartments' }, t('foot.catalog')), h('a', { href: '/apartments#calc' }, t('foot.calc')), h('a', { href: '/apartments' }, t('foot.3d'))),
        col(t('foot.info'), h('a', { href: '/#why' }, t('foot.why')), h('a', { href: '/#steps' }, t('foot.steps')), h('a', { href: '/login' }, t('foot.cabinet'))),
        col(t('foot.contacts'), h('span', null, c.address || ''), c.phone ? h('a', { href: 'tel:+' + phoneDigits(c.phone) }, c.phone) : null, c.telegram ? h('a', { href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, c.telegram) : null, h('a', { href: '/contact' }, t('foot.write')))),
      h('div', { class: 'foot-bottom' }, h('span', null, '© ' + new Date().getFullYear() + ' ' + t('brand.name') + '. ' + t('footer.rights') + ' · ' + t('footer.platform')),
        h('a', { class: 'foot-privacy', href: '/privacy' }, t('privacy.link')),
        h('span', { class: 'foot-dev' }, t('foot.dev') + ' ', h('a', { href: '/register' }, t('foot.devLink')))),
      h('p', { class: 'foot-demo' }, t('foot.demo'), frontendVersion())));
  }

  // Which build of the site is served: version.txt holds the short commit SHA (written by the Docker build and by the
  // Vercel build command; «dev» in the repository). Shown small in the footer and in the console, so it is visible
  // whether Render and Vercel serve the same frontend.
  function frontendVersion() {
    const el = h('span', { class: 'foot-ver' });
    fetch('/version.txt', { cache: 'no-store' }).then((r) => (r.ok ? r.text() : '')).then((v) => {
      v = (v || '').trim().toLowerCase();
      if (!/^([0-9a-f]{7}|dev)$/.test(v)) v = 'dev';
      el.textContent = ' · v ' + v;
      console.info('Binosoz frontend ' + v);
    }).catch(() => {});
    return el;
  }

  // Page head of the inner pages: a way back to the landing and the title.
  const pageHead = (title, sub) => h('section', { class: 'lux-pagehead' }, h('div', { class: 'wrap' },
    h('a', { class: 'back-link', href: '/' }, '← ' + t('nav.home')), h('h1', null, accentFirst(title)), sub ? h('p', null, sub) : null));

  /* ---------- catalog: the 3D tower, the list with filters, the calculator ---------- */
  S.views.apartments = function (root) {
    const cfg = window.SITORA_CONFIG || {};
    const st = { building: null, floors: [], apts: [], rate: null, sel: null, floor: null, rooms: 'all', maxPrice: 0, sort: 'price', onlyFree: true, shown: 12, overlay: true, time: 'dusk' };
    let house = null, alive = true;
    const page = h('div', { class: 'page-in landing-page lux' });
    root.appendChild(page);
    // Page hero: breadcrumbs, title, and the ways to reach the manager on the right.
    const cc = contacts();
    const topStats = h('div', { class: 'apts-hero-stats' });
    page.appendChild(h('section', { class: 'cat-hero apts-hero' }, h('div', { class: 'wrap cat-hero-in' },
      h('div', { class: 'cat-hero-text' },
        h('nav', { class: 'crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.apartments'))),
        h('h1', { class: 'apts-hero-title' }, accentFirst(t('cat.title'))), h('p', { class: 'apts-hero-sub' }, t('cat.sub')), topStats),
      (cc.phone || cc.telegram) ? h('div', { class: 'cat-hero-contact' },
        cc.phone ? h('a', { class: 'cat-call', href: 'tel:+' + phoneDigits(cc.phone) }, h('span', { class: 'ct-ic' }, landIcon('phone')), h('span', null, h('small', null, t('home.call')), h('b', null, cc.phone))) : null,
        cc.telegram ? h('a', { class: 'btn gold', href: telegramUrl(cc.telegram), target: '_blank', rel: 'noopener' }, landIcon('send'), 'Telegram') : null) : null)));


    const houseBox = h('div', { class: 'house3d', id: 'house3d' });
    const tip = h('div', { class: 'tip', hidden: true });
    const sheetHost = h('div');
    const floorbar = h('div', { class: 'floorbar glass pe', role: 'group', 'aria-label': t('hero.floors') });
    const heroTitle = h('div', { class: 'hero-title' });
    const stats = h('div', { class: 'stat-strip pe' });
    const loading = h('div', { class: 'hero-loading' }, t('hero.loading'));

    const legend = h('div', { class: 'legend glass pe' }, [['free', 'status.free'], ['booked', 'status.booked'], ['sold', 'status.sold'], ['onrequest', 'status.onrequest']].map(([k, key]) => h('span', null, h('i', { style: { background: { free: '#2fd6a3', booked: '#ffb030', sold: '#4a5570', onrequest: '#6f8bff' }[k] } }), t(key))));

    const timeSeg = h('div', { class: 'seg', role: 'group', 'aria-label': t('hero.time') }, ['day', 'dusk', 'night'].map((k) => h('button', { class: st.time === k ? 'on' : '', 'data-k': k, onclick: () => { st.time = k; house && house.setTime(k); S.$$('button', timeSeg).forEach((b) => b.classList.toggle('on', b.dataset.k === k)); } }, t('time.' + k))));
    const viewSeg = h('div', { class: 'seg', role: 'group' }, [['1', 'hero.statuses'], ['0', 'hero.photo']].map(([v, key]) => h('button', { class: (st.overlay ? '1' : '0') === v ? 'on' : '', 'data-k': v, onclick: () => { st.overlay = v === '1'; house && house.setOverlay(st.overlay); S.$$('button', viewSeg).forEach((b) => b.classList.toggle('on', b.dataset.k === v)); legend.style.visibility = st.overlay ? 'visible' : 'hidden'; } }, t(key))));
    const ctrls = h('div', { class: 'ctrls pe' }, timeSeg, viewSeg,
      h('button', { class: 'icon-btn', title: t('hero.zoomIn'), 'aria-label': t('hero.zoomIn'), onclick: () => house && house.zoom(0.8) }, S.icon('zin')),
      h('button', { class: 'icon-btn', title: t('hero.zoomOut'), 'aria-label': t('hero.zoomOut'), onclick: () => house && house.zoom(1.25) }, S.icon('zout')),
      h('button', { class: 'icon-btn', title: t('hero.reset'), 'aria-label': t('hero.reset'), onclick: () => { st.floor = null; st.sel = null; house && (house.clearSelection(), house.resetView()); renderSheet(); renderFloors(); } }, S.icon('rotate')));

    const hero = h('section', { class: 'hero', id: 'hero-top' },
      houseBox, h('div', { class: 'hero-shade' }), tip, loading,
      h('div', { class: 'hero-ui' }, h('div', { class: 'wrap hero-uiwrap', style: { height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'space-between', position: 'relative' } },
        h('div', { class: 'hero-top' }, heroTitle),
        h('div', { class: 'hero-bottom' }, h('div', { class: 'stack', style: { gap: '10px' } }, stats, legend), ctrls)),
      ),
      h('div', { class: 'wrap', style: { position: 'absolute', inset: 0, pointerEvents: 'none', zIndex: 3 } }, h('div', { style: { position: 'relative', height: '100%' } }, floorbar, sheetHost)));
    page.appendChild(hero);

    const aptSection = h('section', { class: 'section', id: 'apartments' });
    const calcSection = h('section', { class: 'section alt', id: 'calc' });
    // Floor plans: one drawn plan per apartment type, with the areas and prices of this building.
    const plansSection = h('section', { class: 'section lux-sec', id: 'plans' });
    page.append(plansSection, aptSection, calcSection, siteFooter());

    // ---------- data ----------
    // No building, no apartments, or the API did not answer: the 3D stage, plans and calculator are hidden and one
    // card says so (with the phone and a request link). Nothing invented is shown.
    function showEmpty(state) {
      hero.hidden = true; plansSection.hidden = true; calcSection.hidden = true; topStats.replaceChildren();
      aptSection.replaceChildren(h('div', { class: 'wrap' }, soonCard(state, () => {
        hero.hidden = false; plansSection.hidden = false; calcSection.hidden = false;
        aptSection.replaceChildren(); loading.replaceChildren(t('hero.loading')); load();
      })));
    }
    async function load() {
      try {
        const bs = await S.api.buildings();
        if (!alive) return;
        if (!bs.length) { showEmpty('empty'); return; }
        st.building = bs.find((b) => String(b.id) === String(cfg.DEFAULT_BUILDING_ID)) || bs[0];
        S.siteBuildingId = st.building.id;
        const full = await S.api.building(st.building.id).catch(() => st.building);
        st.building = Object.assign({}, st.building, full);
        st.floors = await S.api.floors(st.building.id);
        const [apts, rate] = await Promise.all([S.api.allApartments(st.floors), S.api.rate().catch(() => null)]);
        if (!alive) return;
        if (!apts.length) { showEmpty('empty'); return; }
        st.apts = apts; st.rate = rate && rate.rate ? rate : null;
        st.maxPrice = Math.max(...apts.map((a) => a.price), 0);
        st.priceCap = st.maxPrice;
      } catch (e) {
        if (alive) showEmpty('error');
        return;
      }
      if (!alive) return;
      loading.remove();
      renderAll();
      initHouse();
      // /apartments#calc from the landing: the calculator exists only now, after the data.
      if (location.hash === '#calc') setTimeout(() => scrollTo('calc'), 100);
    }

    function initHouse() {
      const floors = st.floors.filter((f) => !f.commercial).map((f) => f.number);
      // The 3D engine is loaded now (once): when the stage comes near the screen.
      if (!S.House3D) {
        const go = () => loadHouse3D().then(() => { if (alive && S.House3D) initHouse(); }).catch(() => { /* no 3D: the floor list and the apartments below still work */ });
        if ('IntersectionObserver' in window) {
          const io = new IntersectionObserver((es) => { if (es.some((e) => e.isIntersecting)) { io.disconnect(); go(); } }, { rootMargin: '200px' });
          io.observe(hero);
        } else go();
        return;
      }
      try {
      house = S.House3D.create(houseBox, {
        floors: Math.max(...st.floors.map((f) => f.number), 1), time: st.time,
        onSelectApartment: (a) => { st.sel = st.apts.find((x) => x.id === a.id) || null; st.floor = st.sel ? st.sel.floor : st.floor; renderSheet(); renderFloors(); },
        onSelectFloor: (n) => { st.floor = n; if (st.sel && st.sel.floor !== n) st.sel = null; renderSheet(); renderFloors(); },
        onHover: (a, x, y) => {
          if (!a) { tip.hidden = true; return; }
          const apt = st.apts.find((z) => z.id === a.id) || a;
          tip.replaceChildren(h('b', null, '№ ' + apt.number), ' · ' + S.fmt.rooms(apt.rooms) + ' · ' + S.fmt.area(apt.area) + ' · ' + t('status.' + apt.status));
          tip.style.left = Math.min(x, houseBox.clientWidth - 260) + 'px'; tip.style.top = y + 'px'; tip.hidden = false;
        },
      });
      } catch (e) {
        S.logError(e); house = null;
        houseBox.replaceChildren(h('div', { class: 'empty', style: { padding: '60px 16px', textAlign: 'center' } }, t('house.no3d')));
        return;
      }
      pushToHouse();
      void floors;
    }
    function pushToHouse() { if (!house) return; house.setData(st.apts.map((a) => ({ id: a.id, floor: a.floor, planSlot: a.planSlot, number: a.number, status: a.status, rooms: a.rooms, area: a.area, price: a.price }))); if (st.sel) house.selectApartment(st.sel.id); else if (st.floor !== null) house.selectFloor(st.floor); }

    // ---------- render ----------
    function renderAll() { renderHero(); renderTopStats(); renderFloors(); renderSheet(); renderPlans(); renderApartments(); renderCalc(); }

    // Numbers in the page head: free apartments, the lowest price of a free one, floors.
    function renderTopStats() {
      const free = st.apts.filter((a) => a.status === 'free');
      const prices = (free.length ? free : st.apts).map((a) => a.price).filter((p) => p > 0);
      const stat = (v, l) => h('div', { class: 'apts-hero-stat' }, h('b', { class: 'apts-hero-stat-num' }, v), h('span', { class: 'apts-hero-stat-label' }, l));
      topStats.replaceChildren(stat(S.fmt.n(free.length), t('wow.cat.free')),
        prices.length ? stat(S.fmt.money(Math.min(...prices), 'TJS'), t('wow.cat.from')) : null,
        stat(S.fmt.n(st.building.floorsCount || st.floors.length), t('wow.cat.floors')));
    }

    // Studio, 1-, 2- and 3-room: the smallest area and the price range of that type in this building (the type's typical
    // area when the building has none). The button filters the list below by that type and scrolls to it.
    function renderPlans() {
      const cards = PLAN_TYPES.map((type) => {
        const list = st.apts.filter((a) => (type === 3 ? a.rooms >= 3 : a.rooms === type));
        const area = list.length ? Math.min(...list.map((a) => a.area)) : PLAN_AREAS[type];
        const prices = list.map((a) => a.price).filter((p) => p > 0);
        const lo = Math.min(...prices), hi = Math.max(...prices);
        const range = !prices.length ? t('plans.onRequest') : lo === hi ? S.fmt.money(lo, 'TJS') : S.fmt.money(lo, 'TJS') + ' – ' + S.fmt.money(hi, 'TJS');
        const canvas = h('canvas', { class: 'plan-canvas', width: 320, height: 220, role: 'img', 'aria-label': t('layouts.type' + type) });
        drawFloorPlan(canvas, type);
        return h('article', { class: 'plan-card' }, canvas,
          h('div', { class: 'plan-card-body' }, h('h3', null, t('layouts.type' + type)),
            h('div', { class: 'plan-badges' }, h('span', { class: 'plan-badge' }, t('plans.from') + ' ' + S.fmt.area(area)), h('span', { class: 'plan-badge gold' }, range)),
            h('button', { class: 'btn gold block', type: 'button', onclick: () => { st.rooms = String(type); st.shown = 12; renderApartments(); scrollTo('apartments'); } }, t('plans.pick'))));
      });
      plansSection.replaceChildren(h('div', { class: 'wrap' },
        h('div', { class: 'lux-head' }, h('h2', null, accentFirst(t('plans.title'))), h('p', null, t('plans.sub'))),
        h('div', { class: 'plan-grid' }, cards)));
    }

    function minPrice() { const f = st.apts.filter((a) => a.status === 'free'); return f.length ? Math.min(...f.map((a) => a.price)) : 0; }
    function renderHero() {
      const b = st.building, free = st.apts.filter((a) => a.status === 'free').length;
      heroTitle.replaceChildren(
        h('span', { class: 'eyebrow' }, S.icon('pin'), S.brandAddress(b.address) || t('hero.city')),
        h('h1', null, S.brandName(b.name)),
        h('p', null, S.lang === 'tg' && b.descriptionTg ? b.descriptionTg : (b.description || t('hero.sub'))),
        h('div', { class: 'hero-cta pe' }, h('button', { class: 'btn gold lg', onclick: () => scrollTo('apartments') }, t('hero.cta1')), h('button', { class: 'btn ghost lg', style: { color: '#fff', borderColor: 'rgba(255,255,255,.4)' }, onclick: () => scrollTo('calc') }, t('hero.cta2'))));
      stats.replaceChildren(
        stat(S.fmt.n(free), t('stat.free')), stat(S.fmt.money(minPrice(), 'TJS'), t('stat.from')), stat(b.deliveryDate ? S.fmt.date(b.deliveryDate).slice(3) : b.completionYear ? String(b.completionYear) : '—', t('stat.delivery')));
    }
    const stat = (v, l) => h('div', { class: 'stat glass' }, h('b', null, v), h('span', null, l));

    function renderFloors() {
      const list = st.floors.slice().sort((a, b) => b.number - a.number);
      const free = (n) => st.apts.filter((a) => a.floor === n && a.status === 'free').length;
      floorbar.replaceChildren(h('h4', null, t('hero.floors')), list.map((f) => { const c = free(f.number); return h('button', { class: 'fbtn' + (st.floor === f.number ? ' on' : '') + (c ? '' : ' zero'), title: f.commercial ? t('floor.commercial') : t('floor.n', { n: f.number }) + ', ' + t('floor.free', { n: c }), onclick: () => selectFloor(f.number) }, h('span', null, f.commercial ? 'К' : f.number), h('small', null, f.commercial ? '' : c)); })); // TODO: i18n
    }
    function selectFloor(n) {
      st.floor = st.floor === n ? null : n; st.sel = null;
      if (house) { if (st.floor === null) { house.clearSelection(); house.resetView(); } else house.selectFloor(n); }
      renderFloors(); renderSheet();
    }

    function planSvg(floorNo, selId) {
      const apts = st.apts.filter((a) => a.floor === floorNo);
      const bySlot = {}; apts.forEach((a, i) => { bySlot[a.planSlot !== null && a.planSlot !== undefined ? a.planSlot : i] = a; });
      const pos = { 0: [14, 164, 126, 46], 1: [160, 164, 126, 46], 2: [244, 112, 46, 46], 3: [244, 62, 46, 46], 4: [160, 10, 126, 46], 5: [14, 10, 126, 46], 6: [10, 62, 46, 46], 7: [10, 112, 46, 46] };
      const ns = 'http://www.w3.org/2000/svg';
      const svg = document.createElementNS(ns, 'svg'); svg.setAttribute('viewBox', '0 0 300 220'); svg.setAttribute('class', 'plan'); svg.setAttribute('role', 'img'); svg.setAttribute('aria-label', t('plan.label', { n: floorNo }));
      const core = document.createElementNS(ns, 'rect'); core.setAttribute('x', 70); core.setAttribute('y', 66); core.setAttribute('width', 160); core.setAttribute('height', 88); core.setAttribute('rx', 8); core.setAttribute('fill', 'rgba(150,165,210,.14)'); core.setAttribute('stroke', 'rgba(150,165,210,.4)'); svg.appendChild(core);
      const ct = document.createElementNS(ns, 'text'); ct.setAttribute('x', 150); ct.setAttribute('y', 114); ct.setAttribute('text-anchor', 'middle'); ct.setAttribute('fill', 'rgba(170,182,224,.8)'); ct.textContent = t('plan.core'); svg.appendChild(ct);
      for (let s = 0; s < 8; s++) {
        const [x, y, w, hh] = pos[s]; const a = bySlot[s];
        const g = document.createElementNS(ns, 'g'); g.setAttribute('class', 'slot' + (a && a.id === selId ? ' on' : ''));
        const r = document.createElementNS(ns, 'rect'); r.setAttribute('x', x); r.setAttribute('y', y); r.setAttribute('width', w); r.setAttribute('height', hh); r.setAttribute('rx', 9);
        const st2 = a ? a.status : 'sold';
        r.setAttribute('fill', { free: 'rgba(47,214,163,.28)', booked: 'rgba(255,176,48,.3)', sold: 'rgba(120,134,170,.28)', onrequest: 'rgba(111,139,255,.3)' }[st2]);
        r.setAttribute('stroke', { free: '#2fd6a3', booked: '#ffb030', sold: '#6a7696', onrequest: '#6f8bff' }[st2]); r.setAttribute('stroke-width', 1.5);
        g.appendChild(r);
        const tx = document.createElementNS(ns, 'text'); tx.setAttribute('x', x + w / 2); tx.setAttribute('y', y + hh / 2 + 4.5); tx.setAttribute('text-anchor', 'middle'); tx.setAttribute('fill', 'currentColor'); tx.textContent = a ? '№' + a.number : '—'; g.appendChild(tx);
        if (a) { g.addEventListener('click', () => pickApt(a)); g.setAttribute('tabindex', 0); g.setAttribute('role', 'button'); g.setAttribute('aria-label', '№ ' + a.number + ', ' + t('status.' + a.status)); g.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); pickApt(a); } }); }
        svg.appendChild(g);
      }
      return svg;
    }
    function pickApt(a) { st.sel = a; st.floor = a.floor; house && house.selectApartment(a.id); renderSheet(); renderFloors(); }

    function monthly(price, down, months) { const d = Math.round((price * down) / 100); return { down: d, monthly: Math.round((price - d) / months) }; }

    function renderSheet() {
      sheetHost.replaceChildren();
      if (st.floor === null && !st.sel) return;
      const f = st.floor;
      const fl = st.floors.find((x) => x.number === f);
      const a = st.sel;
      const body = [h('button', { class: 'icon-btn x', 'aria-label': t('common.close'), onclick: () => { st.sel = null; st.floor = null; house && (house.clearSelection(), house.resetView()); renderSheet(); renderFloors(); } }, S.icon('x'))];
      if (fl && fl.commercial) {
        body.push(h('div', { class: 'apt-head' }, h('h3', null, t('floor.commercial')), h('p', { class: 'muted', style: { marginTop: '6px' } }, t('floor.commercialText'))), h('button', { class: 'btn', onclick: () => leadModal(t('lead.commercial')) }, t('lead.send')));
      } else {
        const free = st.apts.filter((x) => x.floor === f && x.status === 'free').length;
        body.push(h('div', { class: 'apt-head' }, h('div', { class: 'muted', style: { fontSize: '12px', fontWeight: 800, letterSpacing: '.1em', textTransform: 'uppercase' } }, t('floor.n', { n: f })), h('h3', null, a ? t('apt.title', { n: a.number }) : t('floor.free', { n: free }))), planSvg(f, a && a.id));
        if (!a) body.push(h('p', { class: 'muted', style: { textAlign: 'center' } }, t('sheet.pick')));
        if (a) {
          const eq = st.rate ? Math.round(a.price / st.rate.rate) : null;
          const m = monthly(a.price, 30, 60);
          body.push(
            h('div', { class: 'row between' }, S.statusChip(a.status), h('span', { class: 'muted', style: { fontWeight: 700 } }, S.fmt.rooms(a.rooms) + ' · ' + S.fmt.area(a.area))),
            h('div', null, h('div', { class: 'price' }, S.fmt.money(a.price, 'TJS')), eq ? h('div', { class: 'muted', style: { fontWeight: 600, fontSize: '13px' } }, '≈ $' + S.fmt.n(eq) + ' · ' + t('price.estimate')) : null, h('div', { class: 'muted', style: { fontSize: '13px' } }, S.fmt.n(a.pricePerSqm) + ' TJS / ' + t('u.m2'))),
            h('div', { class: 'card flat', style: { padding: '12px', background: 'rgba(255,255,255,.06)' } }, h('div', { class: 'muted', style: { fontSize: '12px', fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.06em' } }, t('calc.example')), h('div', { style: { fontWeight: 800, fontSize: '17px', marginTop: '2px' } }, S.fmt.money(m.monthly, 'TJS') + ' / ' + t('u.month')), h('div', { class: 'muted', style: { fontSize: '12.5px' } }, t('calc.exampleSub', { d: S.fmt.money(m.down, 'TJS') }))),
            a.status === 'free' ? h('button', { class: 'btn gold lg block', onclick: () => bookModal(a) }, t('book.cta')) : h('div', { class: 'form-err', style: { background: 'rgba(255,255,255,.08)', color: 'inherit' } }, t('book.unavailable')),
            h('div', { class: 'row' }, h('button', { class: 'btn ghost grow', onclick: () => { st.calcApt = a; renderCalc(); scrollTo('calc'); } }, t('calc.open')), h('button', { class: 'btn ghost grow', onclick: () => leadModal(t('lead.aboutApt', { n: a.number }), a.id) }, t('lead.ask'))));
        }
      }
      sheetHost.appendChild(h('aside', { class: 'sheet glass pe', style: { position: 'absolute' }, 'aria-live': 'polite' }, body));
    }

    // ---------- booking ----------
    function bookModal(a) {
      const name = S.input('name', { placeholder: t('f.namePh'), required: true, autocomplete: 'name' });
      const phone = S.input('phone', { placeholder: '+992 9XX XX XX XX', type: 'tel', value: '+992', autocomplete: 'tel' });
      const form = h('form', { class: 'stack', novalidate: true }, S.field(t('f.name'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), h('p', { class: 'hint' }, t('book.note')));
      const btn = h('button', { class: 'btn gold', type: 'submit' }, t('book.confirm'));
      form.id = 'bookform'; btn.setAttribute('form', 'bookform');
      const md = S.modal({ title: t('book.title', { n: a.number }), body: [h('div', { class: 'card flat', style: { marginBottom: '14px' } }, h('div', { class: 'row between' }, h('b', null, t('apt.title', { n: a.number })), S.fmt.money(a.price, 'TJS')), h('div', { class: 'muted' }, t('floor.n', { n: a.floor }) + ' · ' + S.fmt.rooms(a.rooms) + ' · ' + S.fmt.area(a.area))), form], actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        await S.busy(btn, async () => {
          try {
            const r = await S.api.book(a.id, { name: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, '') });
            md.close(); a.status = 'booked'; pushToHouse(); renderSheet(); renderApartments(); renderHero();
            S.modal({ title: t('book.doneTitle'), body: h('div', { class: 'stack' }, h('p', null, t('book.doneText', { n: a.number })), r && r.expiresAt ? h('div', { class: 'next' }, h('span', null, t('book.until')), h('b', null, S.fmt.dt(r.expiresAt))) : null, h('p', { class: 'muted' }, t('book.doneNote'))), actions: [h('button', { class: 'btn', onclick: (ev) => ev.target.closest('.overlay').querySelector('.x').click() }, t('common.ok'))] });
          } catch (er) { S.showErrors(form, er); if (er.status === 409) { refreshApts(); } }
        });
      });
    }
    async function refreshApts() { try { st.apts = await S.api.allApartments(st.floors); pushToHouse(); renderSheet(); renderApartments(); renderHero(); renderFloors(); } catch (e) { /* ignore */ } }

    function leadModal(preset, aptId) {
      const name = S.input('name', { placeholder: t('f.namePh'), autocomplete: 'name' });
      const phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel' });
      const cm = h('textarea', { class: 'inp', name: 'comment', id: 'lead-comment', maxlength: 400 }); cm.value = preset || '';
      const form = h('form', { class: 'stack', id: 'leadform', novalidate: true }, S.field(t('f.name'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), S.field(t('f.comment'), cm), S.privacyNote());
      const btn = h('button', { class: 'btn', type: 'submit', form: 'leadform' }, t('lead.send'));
      const md = S.modal({ title: t('lead.title'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        await S.busy(btn, async () => { try { await S.api.createLead({ name: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, ''), comment: cm.value.trim(), apartmentId: aptId }); md.close(); S.toast(t('lead.ok'), 'ok'); } catch (er) { S.showErrors(form, er); } });
      });
    }

    // ---------- apartments list ----------
    function filtered() {
      let l = st.apts.slice();
      if (st.onlyFree) l = l.filter((a) => a.status === 'free');
      if (st.rooms !== 'all') l = l.filter((a) => (st.rooms === '3' ? a.rooms >= 3 : a.rooms === +st.rooms));
      if (st.priceCap) l = l.filter((a) => a.price <= st.priceCap);
      const s = { price: (a, b) => a.price - b.price, priceDesc: (a, b) => b.price - a.price, area: (a, b) => b.area - a.area, floor: (a, b) => b.floor - a.floor }[st.sort];
      return l.sort(s);
    }
    function renderApartments() {
      const list = filtered();
      const rooms = [['all', t('filter.all')], ['0', t('apt.studio')], ['1', '1'], ['2', '2'], ['3', '3+']];
      const price = h('input', { class: 'range', type: 'range', id: 'f-price', min: Math.floor(Math.min(...st.apts.map((a) => a.price)) / 10000) * 10000, max: st.maxPrice, step: 10000, value: st.priceCap, 'aria-label': t('filter.price'), oninput: (e) => { st.priceCap = +e.target.value; priceVal.textContent = S.fmt.money(st.priceCap, 'TJS'); st.shown = 12; drawList(); } });
      const priceVal = h('span', { class: 'num', style: { fontWeight: 800 } }, S.fmt.money(st.priceCap, 'TJS'));
      const sort = S.select('sort', [['price', t('sort.price')], ['priceDesc', t('sort.priceDesc')], ['area', t('sort.area')], ['floor', t('sort.floor')]], st.sort); sort.addEventListener('change', () => { st.sort = sort.value; drawList(); });
      const grid = h('div', { class: 'grid g4', id: 'apt-grid' });
      const more = h('div', { class: 'row', style: { justifyContent: 'center', marginTop: '18px' } });
      const count = h('div', { class: 'muted', style: { fontWeight: 700 } });
      function drawList() {
        const l = filtered(); count.textContent = t('list.found', { n: l.length });
        grid.replaceChildren(...(l.length ? l.slice(0, st.shown).map(renderCard) : [h('div', { style: { gridColumn: '1/-1' } }, S.empty(t('list.none'), 'building'))]));
        more.replaceChildren(l.length > st.shown ? h('button', { class: 'btn ghost', onclick: () => { st.shown += 12; drawList(); } }, t('list.more', { n: l.length - st.shown })) : '');
      }
      aptSection.replaceChildren(h('div', { class: 'wrap' },
        h('div', { class: 'sec-head' }, h('div', null, h('h2', null, accentFirst(t('apts.title'))), h('p', null, t('apts.sub'))), count),
        h('div', { class: 'filters' },
          h('div', { class: 'field' }, h('label', null, t('filter.rooms')), h('div', { class: 'pill-group' }, rooms.map(([v, l]) => h('button', { class: 'pill' + (st.rooms === v ? ' on' : ''), onclick: (e) => { st.rooms = v; st.shown = 12; S.$$('.pill', e.target.parentNode).forEach((p) => p.classList.remove('on')); e.target.classList.add('on'); drawList(); } }, l)))),
          h('div', { class: 'field' }, h('label', { for: 'f-price' }, t('filter.priceTo')), price, priceVal),
          S.field(t('filter.sort'), sort),
          h('div', { class: 'field' }, h('label', null, t('filter.status')), h('label', { class: 'check' }, h('input', { type: 'checkbox', checked: st.onlyFree, onchange: (e) => { st.onlyFree = e.target.checked; st.shown = 12; drawList(); } }), t('filter.onlyFree'))), h('span')),
        grid, more));
      drawList();
    }
    // Catalog card: the floor plan of its type on top with the status and floor badges; number, rooms, area, price.
    function renderCard(a) {
      const plan = h('canvas', { width: 320, height: 180, 'aria-hidden': 'true' });
      drawFloorPlan(plan, Math.min(3, a.rooms));
      return h('button', { class: 'apt-card wow-apt', type: 'button', onclick: () => { scrollTo('home'); setTimeout(() => pickApt(a), 350); }, 'aria-label': t('apt.title', { n: a.number }) },
        h('div', { class: 'apt-card-top' }, plan,
          h('span', { class: 'apt-status-badge ' + a.status }, t('status.' + a.status)),
          h('span', { class: 'apt-floor-badge' }, t('floor.n', { n: a.floor }))),
        h('div', { class: 'apt-card-body' },
          h('div', { class: 'apt-card-num' }, '№ ' + a.number),
          h('div', { class: 'apt-card-rooms' }, S.fmt.rooms(a.rooms)),
          h('div', { class: 'apt-card-area' }, S.fmt.area(a.area)),
          h('div', { class: 'apt-card-price' }, S.fmt.money(a.price, 'TJS'), h('span', { class: 'apt-card-price-label' }, S.fmt.n(a.pricePerSqm) + ' TJS / ' + t('u.m2'))),
          h('span', { class: 'apt-card-cta' }, t('card.show') + ' →')));
    }

    // ---------- calculator ----------
    function renderCalc() {
      const plan = installment();
      const a = st.calcApt;
      const free = st.apts.filter((x) => x.status === 'free');
      const basePrice = a ? a.price : free.length ? Math.round(free.reduce((s, x) => s + x.price, 0) / free.length / 10000) * 10000 : 600000;
      const price = S.input('price', { type: 'number', min: 10000, step: 1000, value: basePrice });
      const down = h('input', { class: 'range', type: 'range', id: 'c-down', min: plan.downFrom, max: 90, step: 5, value: Math.max(30, plan.downFrom), 'aria-label': t('calc.down') });
      const months = h('input', { class: 'range', type: 'range', id: 'c-months', min: 6, max: plan.years * 12, step: 6, value: Math.min(60, plan.years * 12), 'aria-label': t('calc.months') });
      const dv = h('b', { class: 'num' }), mv = h('b', { class: 'num' });
      const out = h('div', { class: 'calc-out' });
      const upd = () => {
        const p = Math.max(0, +price.value || 0); const r = monthly(p, +down.value, +months.value);
        dv.textContent = down.value + '% · ' + S.fmt.money(r.down, 'TJS'); mv.textContent = months.value + ' ' + t('u.monthsShort');
        out.replaceChildren(h('small', null, t('calc.monthly')), h('div', { class: 'big' }, S.fmt.money(r.monthly, 'TJS')), h('small', null, t('calc.noInterest')), h('div', { style: { marginTop: '14px', position: 'relative', zIndex: 1 } }, h('button', { class: 'btn gold', onclick: () => leadModal(t('lead.calc', { p: S.fmt.n(p), d: down.value, m: months.value, mo: S.fmt.n(r.monthly) }), a && a.id) }, t('calc.send'))));
      };
      [price, down, months].forEach((el) => el.addEventListener('input', upd));
      calcSection.replaceChildren(h('div', { class: 'wrap' }, h('div', { class: 'sec-head' }, h('div', null, h('h2', null, accentFirst(t('calc.title'))), h('p', null, a ? t('calc.forApt', { n: a.number }) : t('calc.sub')))),
        h('div', { class: 'calc' }, h('div', { class: 'card stack' }, S.field(t('calc.price') + ' (TJS)', price), h('div', { class: 'field' }, h('label', { for: 'c-down' }, t('calc.down')), down, dv), h('div', { class: 'field' }, h('label', { for: 'c-months' }, t('calc.months')), months, mv)), out)));
      upd();
    }

    function scrollTo(id) { const el = document.getElementById(id === 'home' ? 'hero-top' : id); if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
    S.scrollTo = scrollTo;

    load();
    return () => { alive = false; if (house) house.destroy(); };
  };

  /* ---------- landing: hero, numbers, why us, how to buy, apartments teaser, contact teaser ---------- */
  S.views.home = function (root) {
    const cfg = window.SITORA_CONFIG || {};
    const c = contacts();
    // Numbers come only from the API. The config.js DEFAULT_* numbers are for the demo mode (no API_BASE) only:
    // a visitor must never see "160 apartments" or prices that the catalog does not have.
    // state: 'loading' → 'ready' (a building with apartments) | 'empty' (no building or no apartments yet) | 'error'.
    const demo = S.isDemo();
    const d = {
      state: demo ? 'ready' : 'loading',
      floors: demo ? cfg.DEFAULT_FLOORS || 16 : null, apts: demo ? cfg.DEFAULT_APTS || 128 : null, year: demo ? cfg.DEFAULT_YEAR || 2026 : null,
      free: null, list: null,
      // Apartment types: { rooms, areaFrom, areaTo, priceFrom }.
      types: demo ? (cfg.DEFAULT_TYPES || []).map(([rooms, areaFrom, areaTo, priceFrom]) => ({ rooms, areaFrom, areaTo, priceFrom })) : [],
    };
    const known = () => d.state === 'ready';
    const plan = installment();
    let alive = true;
    const page = h('div', { class: 'page-in lux lux-home' });
    root.appendChild(page);

    // 1. Hero: a slowly drifting gradient mesh, the name on two lines with a gold underline, two actions and three counters;
    //    on the right a drawn poster of the building (the interactive 3D model is in the catalog).
    const heroTitle = h('h1', { class: 'wow-title' });
    const aboutTitle = h('h2');
    const nameWords = () => S.brandName(d.name).split(/\s+/);
    const drawName = () => {
      const w = nameWords();
      // A space between the two lines: screen readers and search engines read «Некруз Резиденс», not «НекрузРезиденс».
      heroTitle.replaceChildren(h('span', { class: 'wow-l1' }, w[0]), w.length > 1 ? ' ' : null,
        w.length > 1 ? h('span', { class: 'wow-l2' }, w.slice(1).join(' '), h('i', { class: 'wow-underline', 'aria-hidden': 'true' })) : null);
      aboutTitle.replaceChildren(h('span', { class: 'gold-text' }, w[0]), w.length > 1 ? ' ' + w.slice(1).join(' ') : null);
    };
    drawName();
    const badge = h('span', { class: 'wow-badge' });
    const proof = h('p', { class: 'hero-proof', hidden: true });
    const freeCard = h('div', { class: 'float-card glass' });
    const heroCounters = [['floors', (v) => S.fmt.n(v)], ['apts', (v) => S.fmt.n(v)], ['zero', (v) => v + '%']]
      .map(([k, fmt]) => ({ k, fmt, el: h('b', { class: 'wow-stat-num' }) }));
    const heroStats = h('div', { class: 'wow-stats', hidden: true }, heroCounters.map((x) => h('div', { class: 'wow-stat' }, x.el, h('span', null, t('wow.stat.' + x.k)))));
    const valueOf = (k) => ({ floors: d.floors, apts: d.apts, zero: 0 }[k]);
    const countHero = () => { heroStats.hidden = !known(); if (known()) heroCounters.forEach((x) => countUp(x.el, valueOf(x.k), x.fmt)); };
    const frame = heroModel();
    const hero = h('section', { class: 'lux-hero wow-hero', id: 'top' },
      h('div', { class: 'hero-mesh', 'aria-hidden': 'true' }),
      h('div', { class: 'wrap wow-hero-in' },
        h('div', { class: 'wow-hero-text' },
          badge, heroTitle, h('p', { class: 'wow-sub' }, t('wow.hero.sub')), proof,
          h('div', { class: 'lux-cta' },
            h('a', { class: 'btn gold lg wow-cta', href: '/apartments' }, t('wow.hero.pick'), h('span', { class: 'wow-arrow', 'aria-hidden': 'true' }, '→')),
            h('a', { class: 'btn ghost lg', href: '/apartments#hero-top' }, landIcon('cube'), t('wow.hero.3d'))),
          heroStats),
        h('div', { class: 'wow-hero-side' }, frame, h('p', { class: 'hero-3d-label' }, t('wow.3d.hint')), freeCard)),
      h('button', { class: 'scroll-hint', type: 'button', 'aria-label': t('home.scroll'), onclick: () => document.getElementById('why').scrollIntoView({ behavior: 'smooth' }) }, landIcon('down')));

    // (The dark numbers band repeated the hero's floors and apartments a second time: removed.)

    // 3. Why us.
    const why = h('section', { class: 'section lux-sec', id: 'why' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('why.title'))), h('p', null, t('why.sub'))),
      h('div', { class: 'feat-grid' }, FEATURES.map(([ic, k], i) => h('article', { class: 'feat-card reveal', style: { transitionDelay: (i % 3) * 90 + 'ms' } },
        h('div', { class: 'feat-ic' }, landIcon(ic)), h('h3', null, t(k)), h('p', null, t(k + '.d')))))));

    // 4. How to buy.
    const steps = h('section', { class: 'section lux-sec alt-band', id: 'steps' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('steps.title')))),
      h('ol', { class: 'steps' }, [1, 2, 3].map((n) => h('li', { class: 'step reveal', style: { transitionDelay: (n - 1) * 120 + 'ms' } },
        h('span', { class: 'step-no' }, '0' + n), h('h3', null, t('steps.' + n)), h('p', null, t('steps.' + n + '.d')),
        n === 1 ? h('a', { class: 'btn gold', href: '/apartments' }, t('steps.cta')) : null)))));

    // About the complex: the numbers on the left, the description and amenities on the right.
    const aboutStats = h('div', { class: 'about-stats reveal' });
    const about = h('section', { class: 'section lux-sec', id: 'about' }, h('div', { class: 'wrap about-grid' },
      aboutStats,
      h('div', { class: 'about-text reveal' },
        h('span', { class: 'lux-kicker' }, t('about.kicker')),
        aboutTitle,
        h('p', null, t('about.text')),
        h('ul', { class: 'amenities' }, AMENITIES.map(([ic, k]) => h('li', null, h('span', { class: 'am-ic' }, landIcon(ic)), t(k)))))));

    // Layouts: one card per apartment type.
    const layoutGrid = h('div', { class: 'layout-grid reveal' });
    const layouts = h('section', { class: 'section lux-sec alt-band', id: 'layouts', hidden: true }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('layouts.title'))), h('p', null, t('layouts.sub'))),
      layoutGrid));

    // Installment terms.
    const term = (ic, label, value) => h('div', { class: 'plan-cell' }, h('span', { class: 'plan-ic' }, landIcon(ic)), h('small', null, label), h('b', null, value));
    // Gallery: six views of Dushanbe from Wikimedia Commons, each with its author and licence (not the complex).
    // An even 3×2 grid on a desktop, 2×3 on a tablet, one column on a phone.
    const gallery = h('section', { class: 'section lux-sec', id: 'gallery' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, t('gal.title')), h('p', null, t('gal.sub'))),
      h('div', { class: 'gal-grid gal-grid--real' }, GALLERY_PHOTOS.slice(0, 6).map((p, i) =>
        h('figure', { class: 'gal-card gal-card--photo reveal', style: { transitionDelay: (i % 3) * 80 + 'ms' } },
          galleryImg(p, '(max-width: 540px) 100vw, (max-width: 900px) 50vw, 400px'),
          h('figcaption', null, h('span', null, t('galpg.p.' + p.id)), galleryCredit(p))))),
      h('div', { class: 'teaser-more' }, h('a', { class: 'btn ghost lg', href: '/gallery' }, t('nav.gallery') + ' →'))));

    // No reviews section: invented example reviews undermine trust; it comes back with real buyers' reviews.

    const planBand = h('section', { class: 'lux-plan', id: 'installment' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('plan.title'))), h('p', null, t('plan.sub'))),
      h('div', { class: 'plan-grid reveal' },
        term('card', t('plan.down'), t('plan.downV', { n: plan.downFrom })),
        term('calendar', t('plan.term'), t('plan.termV', { n: counted(plan.years, 'plan.years') })),
        term('percent', t('plan.over'), '0%')),
      h('div', { class: 'teaser-more' }, h('a', { class: 'btn gold lg', href: '/apartments#calc' }, t('plan.cta')))));

    // 5. Apartments teaser.
    const aptGrid = h('div', { class: 'teaser-grid' });
    const allBtn = h('a', { class: 'btn ghost lg', href: '/apartments' });
    const teaserHead = h('div', { class: 'lux-head reveal' }, h('h2', null, t('teaser.title')), h('p', null, t('teaser.sub')));
    const teaserMore = h('div', { class: 'teaser-more' }, allBtn);
    const teaser = h('section', { class: 'section lux-sec', id: 'teaser' }, h('div', { class: 'wrap' }, teaserHead, aptGrid, teaserMore));

    // 6. Contact teaser with a call-back form.
    const callBox = h('div', { class: 'call-box glass reveal' });
    const contactBand = h('section', { class: 'lux-contact', id: 'contact' }, h('div', { class: 'wrap contact-band' },
      h('div', { class: 'reveal' },
        h('h2', null, accentFirst(t('cta.title'))), h('p', { class: 'muted-lux' }, t('cta.sub')),
        h('ul', { class: 'contact-lines' },
          c.address ? h('li', null, landIcon('pin'), c.address) : null,
          c.phone ? h('li', null, landIcon('phone'), h('a', { href: 'tel:+' + phoneDigits(c.phone) }, c.phone)) : null,
          c.telegram ? h('li', null, landIcon('send'), h('a', { href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, c.telegram)) : null),
        c.telegram ? h('a', { class: 'btn gold lg', href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, landIcon('send'), t('cta.tg')) : null),
      callBox));

    page.append(hero, why, steps, about, layouts, gallery, planBand, teaser, contactBand, siteFooter());
    S.$$('section', page).forEach((sec) => { if (sec !== hero) sec.classList.add('section-reveal'); });
    renderData(); renderCall();
    const stopReveal = revealIn(page);

    // The hero background drifts a little slower than the page (--py).
    let ticking = false;
    const onScroll = () => { if (ticking) return; ticking = true; requestAnimationFrame(() => { ticking = false; hero.style.setProperty('--py', Math.min(window.scrollY, 900) + 'px'); }); };
    window.addEventListener('scroll', onScroll, { passive: true });

    function renderData() {
      drawName();
      badge.textContent = '✦ ' + (d.year ? t('wow.hero.badge', { y: d.year }) : t('wow.hero.badge', { y: '' }).replace(/\s*·\s*$/, ''));
      countHero();
      // «Свободно 47 из 160» from the API; without the numbers the card is not shown at all.
      freeCard.hidden = d.free === null || !d.apts;
      freeCard.replaceChildren(h('i', { class: 'live-dot' }), h('div', null, h('small', null, t('home.freeNow')), h('b', null, d.free === null ? '' : t('home.freeOf', { n: S.fmt.n(d.free), m: S.fmt.n(d.apts || 0) }))));
      allBtn.textContent = known() && d.apts ? t('teaser.all', { n: S.fmt.n(d.apts) }) : t('home.toCatalog');
      if (d.list) aptGrid.replaceChildren(...d.list.map(aptCard));
      else if (demo && d.types.length) aptGrid.replaceChildren(...d.types.slice(0, 3).map(typeCard));
      else if (d.state === 'loading') aptGrid.replaceChildren(S.skeleton ? S.skeleton(3, 120) : '');
      else aptGrid.replaceChildren(soonBlock());
      teaserHead.hidden = !d.list && !(demo && d.types.length);
      teaserMore.hidden = d.state !== 'ready';
      layouts.hidden = !d.types.length;
      // Social proof from the real data: apartments already sold or booked. Hidden until it is known.
      proof.replaceChildren(...(d.taken ? [h('span', { class: 'proof-ic' }, landIcon('users')), t('home.proof', { n: counted(d.taken, 'home.families') })] : []));
      proof.hidden = !d.taken;
      const big = (v, l) => h('div', { class: 'about-num' }, h('b', null, v), h('span', null, l));
      // The hero already shows floors and apartments: here the installment terms (config.js INSTALLMENT, set by the
      // developer) and, when the API has them, the number of apartment types and the delivery year.
      aboutStats.replaceChildren(...[
        big(t('plan.downV', { n: plan.downFrom }), t('plan.down')),
        big(t('plan.termV', { n: counted(plan.years, 'plan.years') }), t('plan.term')),
        big('0%', t('plan.over')),
        d.types.length ? big(String(d.types.length), plural(d.types.length, 'about.types')) : null,
      ].filter(Boolean));
      layoutGrid.replaceChildren(...d.types.map(layoutCard));
    }
    // A layout card: type, area range, price from; the bed icon shows the number of rooms.
    function layoutCard(x) {
      const beds = Math.max(1, Math.min(3, x.rooms || 1));
      const area = x.areaFrom === x.areaTo ? S.fmt.n(x.areaFrom) : S.fmt.n(x.areaFrom) + '–' + S.fmt.n(x.areaTo);
      // Preview: the floor plan of this type, drawn like the plans in the catalog.
      const plan = h('canvas', { class: 'layout-plan', width: 260, height: 160, 'aria-hidden': 'true' });
      drawFloorPlan(plan, Math.min(3, x.rooms || 0));
      return h('a', { class: 'layout-card', href: '/apartments' },
        plan,
        h('span', { class: 'layout-ic', 'aria-hidden': 'true' }, Array.from({ length: beds }, () => landIcon('bed'))),
        h('b', { class: 'layout-type' }, t('layouts.type' + Math.min(3, x.rooms))),
        h('span', { class: 'layout-area' }, area + ' ' + t('u.m2')),
        h('span', { class: 'layout-price' }, t('home.chip.from', { p: S.fmt.money(x.priceFrom, 'TJS') })),
        h('span', { class: 'apt-lux-more' }, t('layouts.cta')));
    }
    // A free apartment: its floor plan type drawn on top, the status badge, rooms, area and price.
    function aptCard(a) {
      const plan = h('canvas', { width: 320, height: 200, 'aria-hidden': 'true' });
      drawFloorPlan(plan, Math.min(3, a.rooms));
      return h('a', { class: 'apt-showcase-card stagger', href: '/apartments' },
        h('div', { class: 'apt-card-visual' }, plan, h('span', { class: 'apt-card-badge' }, t('status.' + a.status))),
        h('div', { class: 'apt-card-body' },
          h('div', { class: 'apt-card-num' }, '№ ' + a.number + ' · ' + t('floor.n', { n: a.floor })),
          h('div', { class: 'apt-card-rooms' }, S.fmt.rooms(a.rooms)),
          h('div', { class: 'apt-card-area' }, S.fmt.area(a.area) + ' · ' + S.fmt.n(a.pricePerSqm) + ' TJS / ' + t('u.m2')),
          h('div', { class: 'apt-card-price' }, S.fmt.money(a.price, 'TJS')),
          h('span', { class: 'apt-card-btn' }, t('teaser.more'))));
    }
    // Without the API: apartment types instead of made-up apartments.
    function typeCard(i) {
      return h('a', { class: 'apt-lux', href: '/apartments' },
        h('div', { class: 'apt-lux-top' }, S.statusChip('free')),
        h('div', { class: 'apt-lux-meta' }, t('layouts.type' + Math.min(3, i.rooms))),
        h('div', { class: 'apt-lux-price' }, t('home.chip.from', { p: S.fmt.money(i.priceFrom, 'TJS') })),
        h('div', { class: 'apt-lux-sqm' }, t('teaser.typeSub')),
        h('span', { class: 'apt-lux-more' }, t('teaser.more')));
    }

    // The API did not answer (Render asleep longer than the retries, no connection): «Повторить» loads everything again.
    const soonBlock = () => soonCard(d.state, () => { d.state = 'loading'; renderData(); load(); });

    function renderCall() {
      const phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel', 'aria-label': t('f.phone') });
      const btn = h('button', { class: 'btn gold lg block', type: 'submit' }, t('cta.call'));
      const form = h('form', { class: 'stack', novalidate: true }, h('h3', null, t('cta.formTitle')), phone, btn, h('small', { class: 'micro' }, t('cta.micro')), S.privacyNote());
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        await S.busy(btn, async () => {
          try {
            await siteBuilding();
            await S.api.createLead({ name: t('cta.leadName'), phone: phone.value.replace(/[\s()-]/g, ''), comment: t('cta.leadComment') });
            callBox.replaceChildren(successBlock(t('cta.done')));
          } catch (er) { S.showErrors(form, er); }
        });
      });
      callBox.replaceChildren(form);
    }

    async function load() {
      try {
        const id = await siteBuilding();
        if (!alive) return;
        if (!id) { d.state = 'empty'; renderData(); return; }
        const b = await S.api.building(id);
        const floors = await S.api.floors(id);
        const apts = await S.api.allApartments(floors);
        if (!alive) return;
        const free = apts.filter((a) => a.status === 'free').sort((x, y) => x.price - y.price);
        d.floors = b.floorsCount || Math.max(0, ...floors.map((f) => f.number)) || null;
        d.apts = apts.length || null;
        if (b.deliveryDate) d.year = new Date(b.deliveryDate).getFullYear();
        else if (b.completionYear) d.year = b.completionYear;
        if (b.name) d.name = b.name;
        d.state = apts.length ? 'ready' : 'empty';
        d.free = apts.length ? free.length : null;
        d.taken = apts.filter((a) => a.status === 'sold' || a.status === 'booked').length;
        // One apartment of each room count, cheapest first.
        const seen = new Set();
        d.list = free.filter((a) => (seen.has(a.rooms) ? false : seen.add(a.rooms))).slice(0, 3);
        if (d.list.length < 3) d.list = d.list.concat(free.filter((a) => !d.list.includes(a))).slice(0, 3);
        if (!d.list.length) d.list = null;
        // Types from the real apartments: area range of all, price from the free ones (else from all). 3 — three rooms and more.
        const types = [0, 1, 2, 3].map((rooms) => {
          const all = apts.filter((a) => (rooms === 3 ? a.rooms >= 3 : a.rooms === rooms));
          if (!all.length) return null;
          const priced = all.filter((a) => a.status === 'free').length ? all.filter((a) => a.status === 'free') : all;
          return { rooms, areaFrom: Math.floor(Math.min(...all.map((a) => a.area))), areaTo: Math.ceil(Math.max(...all.map((a) => a.area))), priceFrom: Math.min(...priced.map((a) => a.price)) };
        }).filter(Boolean);
        if (types.length) d.types = types;
        renderData();
      } catch (e) { if (alive && !demo) { d.state = 'error'; renderData(); } }
    }
    load();
    return () => { alive = false; stopReveal(); window.removeEventListener('scroll', onScroll); };
  };

  // A drawn check mark with a message: the end state of the forms.
  function successBlock(text, again) {
    return h('div', { class: 'success-box', role: 'status' },
      h('div', { class: 'success-ic', html: '<svg viewBox="0 0 52 52" aria-hidden="true"><circle cx="26" cy="26" r="24"/><path d="m15 27 7.5 7.5L38 19"/></svg>' }),
      h('b', null, t('ok.title')), h('p', null, text), again || null);
  }

  /* ---------- contact page ---------- */
  S.views.contact = function (root) {
    const c = contacts();
    const page = h('div', { class: 'page-in lux contact-page' });
    root.appendChild(page);

    const line = (ic, label, ...value) => h('div', { class: 'ct-card' }, h('span', { class: 'ct-ic' }, landIcon(ic)), h('div', null, h('small', null, label), h('div', { class: 'ct-val' }, ...value)));
    const schedule = c.schedule || [];
    const left = h('div', { class: 'ct-left' },
      h('a', { class: 'back-link', href: '/' }, '← ' + t('nav.home')),
      h('h1', null, accentFirst(t('ct.title'))), h('p', { class: 'muted-lux' }, t('ct.sub')),
      c.manager ? h('div', { class: 'ct-manager' }, h('span', { class: 'ct-avatar', 'aria-hidden': 'true' }, S.fmt.initials(c.manager)),
        h('div', null, h('b', null, t('ct.manager', { n: c.manager })), h('small', null, h('i', { class: 'live-dot' }), t('ct.managerSub')))) : null,
      h('div', { class: 'ct-cards' },
        line('pin', t('ct.address'), c.address || ''),
        line('phone', t('ct.phone'), c.phone ? h('a', { href: 'tel:+' + phoneDigits(c.phone) }, c.phone) : '', c.phone ? h('button', { class: 'btn ghost sm', type: 'button', onclick: () => S.copy(c.phone) }, t('common.copy')) : null),
        c.telegram ? line('send', 'Telegram', h('a', { href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, c.telegram)) : null,
        // Office hours come only from CONTACT.schedule (one source); without it the card is not shown.
        schedule.length
          ? h('div', { class: 'ct-card ct-hours' }, h('span', { class: 'ct-ic' }, landIcon('clock')), h('div', null, h('small', null, t('ct.hours')),
            h('dl', null, schedule.map(([day, time]) => [h('dt', null, t('day.' + day)), h('dd', { class: time ? '' : 'off' }, time || t('day.off'))]))))
          : null),
      // A map card: the office address on a stylised city plan.
      h('div', { class: 'ct-map', role: 'img', 'aria-label': c.address || '' },
        h('div', { class: 'ct-map-pin' }, landIcon('pin')), h('b', null, t('ct.city') + ', ' + (c.address || '').replace(/^(г\.\s*|ш\.\s*)?Душанбе,\s*/, '').replace(/,\s*Dushanbe$/, '')),
        h('a', { class: 'btn ghost sm', href: 'https://www.openstreetmap.org/search?query=' + encodeURIComponent(c.address || ''), target: '_blank', rel: 'noopener' }, t('ct.route'))));

    // Above the form — straight to the messengers; under it — why it is safe to write.
    const formHost = h('div');
    const right = h('div', { class: 'ct-right' }, h('div', { class: 'ct-form-wrap' },
      (c.telegram || c.phone) ? h('div', { class: 'ct-direct' }, h('span', null, t('ct.direct')), h('div', { class: 'ct-buttons' },
        c.telegram ? h('a', { class: 'btn gold', href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, landIcon('send'), 'Telegram') : null,
        c.phone ? h('a', { class: 'btn wa', href: 'https://wa.me/' + phoneDigits(c.phone), target: '_blank', rel: 'noopener' }, landIcon('chat'), 'WhatsApp') : null)) : null,
      formHost,
      h('ul', { class: 'trust' }, [['lock', 'ct.trust1'], ['check', 'ct.trust2'], ['clipboard', 'ct.trust3']].map(([ic, k]) => h('li', null, landIcon(ic), t(k))))));
    // FAQ: one answer open at a time; the answers take the contacts and terms from config.js.
    const cfg = window.SITORA_CONFIG || {};
    const plan = installment();
    const vars = { phone: c.phone || '', tg: c.telegram || '', down: plan.downFrom, years: counted(plan.years, 'plan.years'), year: cfg.DEFAULT_YEAR || 2026 };
    const faqList = h('div', { class: 'faq-list' }, FAQ.map((k, i) => {
      const id = 'faq-' + k;
      const item = h('div', { class: 'faq-item' });
      const q = h('button', { class: 'faq-q', type: 'button', 'aria-expanded': 'false', 'aria-controls': id, onclick: () => {
        const open = !item.classList.contains('open');
        S.$$('.faq-item.open', faqList).forEach((x) => { x.classList.remove('open'); x.querySelector('.faq-q').setAttribute('aria-expanded', 'false'); });
        item.classList.toggle('open', open); q.setAttribute('aria-expanded', String(open));
      } }, h('span', null, t('faq.' + k)), landIcon('down'));
      item.append(q, h('div', { class: 'faq-a', id, role: 'region' }, h('div', null, h('p', null, t('faq.' + k + '.a', vars)))));
      if (i === 0) { item.classList.add('open'); q.setAttribute('aria-expanded', 'true'); }
      return item;
    }));
    const faq = h('section', { class: 'section lux-sec alt-band', id: 'faq' }, h('div', { class: 'wrap faq-wrap' },
      h('div', { class: 'lux-head' }, h('h2', null, accentFirst(t('faq.title'))), h('p', null, t('faq.sub'))), faqList));
    page.append(h('section', { class: 'ct-split' }, left, right), faq, siteFooter());

    // Floating labels: the label sits in the field and moves up when the field has focus or a value.
    const fl = (field, label, req) => h('label', { class: 'fl' }, field, h('span', null, label + (req ? ' *' : '')), h('em', { class: 'fl-err' }));
    function renderForm() {
      const name = h('input', { class: 'inp', name: 'name', placeholder: ' ', autocomplete: 'name', maxlength: 200 });
      const phone = h('input', { class: 'inp', name: 'phone', type: 'tel', placeholder: ' ', value: '+992', autocomplete: 'tel' });
      const kind = h('select', { class: 'inp', name: 'kind' }, ['buy', 'plan', 'other'].map((k) => h('option', { value: k }, t('ct.kind.' + k))));
      const when = h('select', { class: 'inp', name: 'when' }, ['morning', 'day', 'evening'].map((k) => h('option', { value: k }, t('ct.when.' + k))));
      const cm = h('textarea', { class: 'inp', name: 'comment', placeholder: ' ', maxlength: 800, rows: 4 });
      const agree = h('input', { type: 'checkbox', name: 'agree' });
      const btn = h('button', { class: 'btn gold lg block', type: 'submit' }, t('ct.send'));
      const form = h('form', { class: 'ct-form', novalidate: true },
        h('h2', null, t('ct.formTitle')),
        fl(name, t('f.name'), true), fl(phone, t('f.phone'), true),
        h('div', { class: 'fl-row' }, h('label', { class: 'fl fixed' }, kind, h('span', null, t('ct.kind'))), h('label', { class: 'fl fixed' }, when, h('span', null, t('ct.when')))),
        fl(cm, t('f.comment')),
        h('label', { class: 'check ct-agree' }, agree, h('span', null, t('ct.agree')), h('em', { class: 'fl-err' })), btn, S.privacyNote());
      const fail = (el, msg) => { const box = el.closest('label'); box.classList.add('err'); box.querySelector('.fl-err').textContent = msg; };
      form.addEventListener('input', (e) => { const box = e.target.closest('label'); if (box) box.classList.remove('err'); });
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        S.$$('label.err', form).forEach((l) => l.classList.remove('err'));
        const ph = phone.value.replace(/[\s()-]/g, '');
        let ok = true;
        if (!name.value.trim()) { fail(name, t('ct.errName')); ok = false; }
        if (!/^\+992\d{9}$/.test(ph)) { fail(phone, t('ct.errPhone')); ok = false; }
        if (!agree.checked) { fail(agree, t('ct.errAgree')); ok = false; }
        if (!ok) return;
        await S.busy(btn, async () => {
          try {
            await siteBuilding();
            await S.api.createLead({ name: name.value.trim(), phone: ph, comment: [t('ct.kind.' + kind.value), t('ct.when') + ': ' + t('ct.when.' + when.value), cm.value.trim()].filter(Boolean).join('. ') });
            formHost.replaceChildren(successBlock(t('ct.done'), h('button', { class: 'btn ghost', type: 'button', onclick: renderForm }, t('ct.again'))));
          } catch (er) { S.showErrors(form, er); }
        });
      });
      formHost.replaceChildren(form);
    }
    renderForm();
    siteBuilding().catch(() => {});
    return () => {};
  };

  /* ---------- register: a developer's application to join the platform ---------- */
  // "+992 98 363 70 70" as the person types; 9 digits without the country code are taken as a Tajik number.
  const formatPhone = (v) => {
    const d = phoneDigits(v).replace(/^992/, '').slice(0, 9);
    const parts = [d.slice(0, 2), d.slice(2, 5), d.slice(5, 7), d.slice(7, 9)].filter(Boolean);
    return '+992' + (parts.length ? ' ' + parts.join(' ') : '');
  };
  const validPhone = (v) => { const d = phoneDigits(v); return /^992\d{9}$/.test(d) || /^\d{9}$/.test(d); };

  S.views.register = function (root) {
    const page = h('div', { class: 'page-in lux reg-page' });
    root.appendChild(page);
    const card = h('div', { class: 'reg-card' });
    page.append(h('section', { class: 'reg-wrap' }, card), siteFooter());

    const fl = (field, label, req) => h('label', { class: 'fl' }, field, h('span', null, label + (req ? ' *' : '')), h('em', { class: 'fl-err' }));
    const company = h('input', { class: 'inp', name: 'companyName', placeholder: ' ', maxlength: 200, autocomplete: 'organization' });
    const owner = h('input', { class: 'inp', name: 'ownerFullName', placeholder: ' ', maxlength: 200, autocomplete: 'name' });
    const email = h('input', { class: 'inp', name: 'contactEmail', type: 'email', placeholder: ' ', maxlength: 256, autocomplete: 'email' });
    const phone = h('input', { class: 'inp', name: 'contactPhone', type: 'tel', placeholder: ' ', value: '+992', autocomplete: 'tel' });
    const comment = h('textarea', { class: 'inp', name: 'comment', placeholder: ' ', maxlength: 1000, rows: 4 });
    phone.addEventListener('input', () => { phone.value = formatPhone(phone.value); });
    const btn = h('button', { class: 'btn gold lg block', type: 'submit' }, t('reg2.send'));
    const form = h('form', { class: 'ct-form reg-form', novalidate: true },
      fl(company, t('reg2.company'), true), h('small', { class: 'fl-hint' }, t('reg2.companyPh')),
      fl(owner, t('reg2.owner'), true),
      fl(email, t('reg2.email'), true),
      fl(phone, t('reg2.phone'), true),
      fl(comment, t('reg2.comment')), h('small', { class: 'fl-hint' }, t('reg2.commentPh')),
      btn, S.privacyNote());

    const fail = (el, msg) => { const box = el.closest('label'); box.classList.add('err'); box.querySelector('.fl-err').textContent = msg; };
    form.addEventListener('input', (e) => { const box = e.target.closest('label'); if (box) box.classList.remove('err'); });
    form.addEventListener('submit', async (e) => {
      e.preventDefault(); S.showErrors(form, null);
      S.$$('label.err', form).forEach((l) => l.classList.remove('err'));
      let ok = true;
      if (company.value.trim().length < 2) { fail(company, t('reg2.errCompany')); ok = false; }
      if (owner.value.trim().length < 2) { fail(owner, t('reg2.errOwner')); ok = false; }
      if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim())) { fail(email, t('reg2.errEmail')); ok = false; }
      if (!validPhone(phone.value)) { fail(phone, t('ct.errPhone')); ok = false; }
      if (!ok) return;
      await S.busy(btn, async () => {
        try {
          await S.api.submitApplication({ companyName: company.value.trim(), ownerFullName: owner.value.trim(), contactEmail: email.value.trim(), contactPhone: phone.value, comment: comment.value.trim() });
          card.replaceChildren(h('div', { class: 'reg-done' }, successBlock(t('reg2.done', { e: email.value.trim() }), h('a', { class: 'btn ghost', href: '/' }, '← ' + t('reg2.back')))));
        } catch (er) { S.showErrors(form, er); }
      });
    });

    card.append(
      h('a', { class: 'back-link', href: '/' }, '← ' + t('nav.home')),
      h('div', { class: 'reg-brand' }, S.logo(), h('b', null, 'BINOSOZ')),
      h('h1', null, accentFirst(t('reg2.title'))),
      h('p', { class: 'muted-lux' }, t('reg2.sub')),
      h('ul', { class: 'reg-perks' }, ['reg2.perk1', 'reg2.perk2', 'reg2.perk3'].map((k) => h('li', null, landIcon('check'), t(k)))),
      form);
    return () => {};
  };

  /* ---------- accept-invite: a staff member opens the link from the invitation letter ---------- */
  // The letter links to /accept-invite#token=…; older letters carry #invite=…, and ?token=… is accepted too.
  // The token stays in the hash where it can: the hash never reaches server logs.
  const inviteToken = () => {
    const m = location.hash.match(/(?:token|invite)=([^&]+)/);
    return (m && decodeURIComponent(m[1])) || new URLSearchParams(location.search).get('token') || '';
  };
  // Same rules as the server (ASP.NET Identity): 8+ characters with a digit, a lowercase and an uppercase letter.
  const strongPassword = (v) => v.length >= 8 && /\d/.test(v) && /\p{Ll}/u.test(v) && /\p{Lu}/u.test(v);

  S.views.acceptInvite = function (root) {
    const page = h('div', { class: 'page-in lux reg-page' });
    root.appendChild(page);
    const card = h('div', { class: 'reg-card inv-card' }, S.skeleton(4, 38));
    page.append(h('section', { class: 'reg-wrap' }, card), siteFooter());
    const brand = () => h('div', { class: 'reg-brand' }, S.logo(), h('b', null, 'BINOSOZ'));
    const token = inviteToken();

    const invalid = () => card.replaceChildren(brand(), h('div', { class: 'inv-bad' }, landIcon('lock'), h('h1', null, t('inv.badTitle')), h('p', { class: 'muted-lux' }, t('inv.badText')),
      h('a', { class: 'btn gold', href: '/' }, '← ' + t('nav.home'))));

    const fl = (field, label, extra) => h('label', { class: 'fl' }, field, h('span', null, label), extra || null, h('em', { class: 'fl-err' }));
    const secret = (name, autocomplete) => {
      const inp = h('input', { class: 'inp', name, type: 'password', placeholder: ' ', autocomplete, maxlength: 128 });
      const eye = h('button', { class: 'pw-eye', type: 'button', 'aria-label': t('inv.show'), 'aria-pressed': 'false', onclick: () => {
        const show = inp.type === 'password'; inp.type = show ? 'text' : 'password';
        eye.setAttribute('aria-pressed', String(show)); eye.setAttribute('aria-label', t(show ? 'inv.hide' : 'inv.show')); eye.replaceChildren(S.icon(show ? 'eyeOff' : 'eye'));
      } }, S.icon('eye'));
      return [inp, eye];
    };

    function form(info) {
      const role = t('inv.role.' + info.role, null, info.role);
      const name = h('input', { class: 'inp', name: 'fullName', placeholder: ' ', value: info.fullName || '', readonly: !!info.fullName, autocomplete: 'name' });
      const email = h('input', { class: 'inp', name: 'email', placeholder: ' ', value: info.email, readonly: true });
      const [pass, eye1] = secret('password', 'new-password');
      const [again, eye2] = secret('confirm', 'new-password');
      const btn = h('button', { class: 'btn gold lg block', type: 'submit' }, t('inv.accept'));
      const f = h('form', { class: 'ct-form reg-form', novalidate: true },
        fl(name, t('inv.name')), fl(email, t('reg2.email')),
        fl(pass, t('inv.password') + ' *', eye1), h('small', { class: 'fl-hint' }, t('inv.passHint')),
        fl(again, t('inv.confirm') + ' *', eye2),
        btn);
      const fail = (el, msg) => { const box = el.closest('label'); box.classList.add('err'); box.querySelector('.fl-err').textContent = msg; };
      f.addEventListener('input', (e) => { const box = e.target.closest('label'); if (box) box.classList.remove('err'); });
      f.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(f, null);
        S.$$('label.err', f).forEach((l) => l.classList.remove('err'));
        let ok = true;
        if (!strongPassword(pass.value)) { fail(pass, t('inv.errPass')); ok = false; }
        if (again.value !== pass.value) { fail(again, t('inv.errMatch')); ok = false; }
        if (!ok) return;
        await S.busy(btn, async () => {
          try {
            const r = await S.api.acceptInvitation(token, pass.value);
            S.toast(t('login.welcome'), 'ok');
            location.replace(S.homeFor ? '/' + S.homeFor(r.user) : '/crm');
          } catch (er) {
            // Used or revoked while the page was open: the same screen as for a bad link.
            if (er.status === 401) invalid(); else S.showErrors(f, er);
          }
        });
      });
      card.replaceChildren(
        brand(),
        h('h1', null, accentFirst(t('inv.title'))),
        h('p', { class: 'inv-lead' }, t('inv.invited', { o: info.organizationName, r: role })),
        h('p', { class: 'muted-lux' }, t('inv.sub')),
        f);
      setTimeout(() => pass.focus(), 30);
    }

    if (!token) invalid();
    else S.api.invitationInfo(token).then(form, (er) => { if (er.status === 404 || er.status === 401 || er.status === 400) invalid(); else card.replaceChildren(brand(), h('div', { class: 'form-err' }, er.message)); });
    return () => {};
  };
  /* ---------- floor plans drawn on a canvas (catalog) ---------- */
  const PLAN_TYPES = [0, 1, 2, 3];
  const PLAN_AREAS = { 0: 28, 1: 42, 2: 65, 3: 88 };
  // Rooms as parts of the plan (x, y, w, h from 0 to 1) and the i18n key of their name.
  const PLAN_ROOMS = {
    0: [[0, 0, 0.62, 0.72, 'living'], [0.62, 0, 0.38, 0.42, 'kitchen'], [0.62, 0.42, 0.38, 0.3, 'bath'], [0, 0.72, 0.62, 0.28, 'hall'], [0.62, 0.72, 0.38, 0.28, 'balcony']],
    1: [[0, 0, 0.5, 0.62, 'bedroom'], [0.5, 0, 0.5, 0.45, 'kitchen'], [0.5, 0.45, 0.25, 0.27, 'bath'], [0.75, 0.45, 0.25, 0.27, 'wc'], [0, 0.62, 0.5, 0.38, 'hall'], [0.5, 0.72, 0.5, 0.28, 'balcony']],
    2: [[0, 0, 0.42, 0.58, 'living'], [0.42, 0, 0.3, 0.58, 'bedroom'], [0.72, 0, 0.28, 0.42, 'kitchen'], [0.72, 0.42, 0.28, 0.3, 'bath'], [0, 0.58, 0.72, 0.42, 'hall'], [0.72, 0.72, 0.28, 0.28, 'balcony']],
    3: [[0, 0, 0.36, 0.55, 'living'], [0.36, 0, 0.32, 0.55, 'bedroom'], [0.68, 0, 0.32, 0.55, 'bedroom2'], [0, 0.55, 0.3, 0.45, 'kitchen'], [0.3, 0.55, 0.3, 0.45, 'hall'], [0.6, 0.55, 0.2, 0.25, 'bath'], [0.8, 0.55, 0.2, 0.25, 'wc'], [0.6, 0.8, 0.4, 0.2, 'balcony']]
  };
  // Walls in gold on the night navy, room names in the middle, a window on the top wall; sharp on high-density screens.
  function drawFloorPlan(canvas, type) {
    const W = +canvas.getAttribute('width'), H = +canvas.getAttribute('height'), dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = W * dpr; canvas.height = H * dpr;
    const g = canvas.getContext('2d'); if (!g) return;
    g.scale(dpr, dpr);
    g.fillStyle = '#0b1430'; g.fillRect(0, 0, W, H);
    const pad = 16, x0 = pad, y0 = pad, w = W - pad * 2, ht = H - pad * 2;
    g.font = '600 11px Inter, system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
    (PLAN_ROOMS[type] || PLAN_ROOMS[1]).forEach(([rx, ry, rw, rh, key]) => {
      const x = x0 + rx * w, y = y0 + ry * ht, ww = rw * w, hh = rh * ht;
      g.fillStyle = key === 'balcony' ? 'rgba(53, 214, 161, 0.08)' : key === 'bath' || key === 'wc' ? 'rgba(111, 139, 255, 0.10)' : 'rgba(255, 255, 255, 0.04)';
      g.fillRect(x, y, ww, hh);
      g.strokeStyle = 'rgba(201, 162, 39, 0.75)'; g.lineWidth = 2; g.strokeRect(x, y, ww, hh);
      const label = t('plans.room.' + key);
      g.fillStyle = '#b9c3e3';
      if (ww > 30 && hh > 18) g.fillText(label.length * 6.2 > ww - 6 ? label.slice(0, Math.max(3, Math.floor((ww - 6) / 6.2) - 1)) + '.' : label, x + ww / 2, y + hh / 2);
    });
    g.strokeStyle = '#c9a227'; g.lineWidth = 4; g.strokeRect(x0, y0, w, ht);
    g.strokeStyle = '#8fb3ff'; g.beginPath(); g.moveTo(x0 + w * 0.12, y0); g.lineTo(x0 + w * 0.3, y0); g.stroke();
  }

  /* ---------- hero picture ---------- */
  // The landing hero: a drawn poster of the building only, no 3D engine (it froze slow phones).
  function heroModel() {
    const poster = h('canvas', { class: 'hero-poster', role: 'img', 'aria-label': t('wow.3d.title') });
    const box = h('div', { class: 'hero-3d-frame' }, poster);
    // Only the drawn poster: no 3D engine on the landing (it froze slow phones). The interactive model with real
    // apartment statuses is in the catalog, behind the «Смотреть 3D-модель» button.
    drawHeroBuilding(poster, () => !box.isConnected);
    return box;
  }

  // Реалистичный рисованный дом-постер. Пока 3D модель загружается, показывает красивое здание с окнами.
  // done() — остановить когда возвращает true. Возвращает функцию stop.
  function drawHeroBuilding(canvas, done) {
    let timer = null, raf = 0, lit = [], time = 0;
    const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const FLOORS = 22, COLS = 7, SIDE_FLOORS = 14, SIDE_COLS = 4;
    const shuffle = () => {
      lit = Array.from({ length: (FLOORS * COLS) + (SIDE_FLOORS * SIDE_COLS * 2) }, (_, i) => {
        const isSide = i >= FLOORS * COLS;
        return Math.random() > (isSide ? 0.45 : 0.28) ? 0.35 + Math.random() * 0.5 : 0;
      });
    };
    const size = () => {
      const r = canvas.getBoundingClientRect(), dpr = Math.min(window.devicePixelRatio || 1, 2);
      const W = Math.max(260, Math.round(r.width || 420)), H = Math.max(300, Math.round(r.height || 560));
      if (canvas.width !== W * dpr || canvas.height !== H * dpr) { canvas.width = W * dpr; canvas.height = H * dpr; }
      return { W, H, dpr };
    };
    const draw = () => {
      if (done()) return stop();
      time += 0.016;
      const { W, H, dpr } = size(), g = canvas.getContext('2d');
      if (!g) return;
      g.setTransform(dpr, 0, 0, dpr, 0, 0);

      // Небо — градиент ночи с оттенком золота
      const sky = g.createLinearGradient(0, 0, 0, H * 0.7);
      sky.addColorStop(0, '#0b0806'); sky.addColorStop(0.5, '#130e0a'); sky.addColorStop(1, '#24190f');
      g.fillStyle = sky; g.fillRect(0, 0, W, H);

      // Звёзды
      if (!reduce) {
        const starSeed = [[0.12,0.08],[0.28,0.04],[0.45,0.12],[0.62,0.06],[0.78,0.09],[0.9,0.15],
          [0.07,0.22],[0.35,0.18],[0.55,0.03],[0.82,0.25],[0.18,0.3],[0.72,0.19]];
        starSeed.forEach(([sx, sy], i) => {
          const a = 0.3 + 0.4 * Math.abs(Math.sin(time * 0.7 + i * 1.3));
          g.fillStyle = 'rgba(255,255,255,' + a.toFixed(2) + ')';
          g.beginPath(); g.arc(sx * W, sy * H, 0.8 + (i % 3) * 0.4, 0, Math.PI * 2); g.fill();
        });
      }

      // Золотое свечение за зданием
      const glow = g.createRadialGradient(W / 2, H * 0.5, 20, W / 2, H * 0.5, W * 0.65);
      glow.addColorStop(0, 'rgba(176,90,32,0.18)'); glow.addColorStop(0.5, 'rgba(176,90,32,0.06)'); glow.addColorStop(1, 'rgba(176,90,32,0)');
      g.fillStyle = glow; g.fillRect(0, 0, W, H);

      const ground = H * 0.88;

      // ── Боковые здания (фон) ──
      const drawSide = (sx, sw, sh, opacity, litOffset) => {
        g.globalAlpha = opacity;
        g.fillStyle = '#120d09'; g.fillRect(sx, ground - sh, sw, sh);
        // фасад
        g.fillStyle = '#1a120b'; g.fillRect(sx, ground - sh, sw, sh);
        // горизонтальные линии этажей
        const sfH = sh / SIDE_FLOORS;
        for (let i = 0; i <= SIDE_FLOORS; i++) {
          g.strokeStyle = 'rgba(176,90,32,' + (i % 4 === 0 ? 0.4 : 0.1) + ')';
          g.lineWidth = 0.5; g.beginPath();
          g.moveTo(sx, ground - sh + i * sfH); g.lineTo(sx + sw, ground - sh + i * sfH); g.stroke();
        }
        // окна
        const scw = (sw - 10) / SIDE_COLS, swW = scw - 4, swH = Math.max(2, sfH - 5);
        for (let r = 0; r < SIDE_FLOORS; r++) for (let c = 0; c < SIDE_COLS; c++) {
          const a = lit[litOffset + r * SIDE_COLS + c];
          g.fillStyle = a ? 'rgba(242,196,106,' + (a * 0.7).toFixed(2) + ')' : 'rgba(255,255,255,0.02)';
          g.fillRect(sx + 5 + c * scw, ground - sh + r * sfH + 3, swW, swH);
        }
        g.globalAlpha = 1;
      };
      const mainW = Math.min(W * 0.42, 200), mainH = ground - H * 0.08, mainX = (W - mainW) / 2;
      const sideW = Math.min(W * 0.22, 100), sideH = mainH * 0.65;
      drawSide(mainX - sideW - 8, sideW, sideH, 0.55, FLOORS * COLS);
      drawSide(mainX + mainW + 8, sideW, sideH, 0.5, FLOORS * COLS + SIDE_FLOORS * SIDE_COLS);

      // ── Главное здание ──
      const bX = mainX, bY = ground - mainH, bW = mainW, bH = mainH;
      const fH = bH / FLOORS;

      // Основа здания — гранитный цоколь (нижние 2 этажа)
      const plinthH = fH * 2.2;
      g.fillStyle = '#16100b'; g.fillRect(bX, bY, bW, bH);
      const gran = g.createLinearGradient(bX, ground - plinthH, bX + bW, ground);
      gran.addColorStop(0, '#1a1612'); gran.addColorStop(1, '#0f100c');
      g.fillStyle = gran; g.fillRect(bX - 4, ground - plinthH, bW + 8, plinthH + 4);

      // Вертикальные пилястры
      const pilW = 6, pilCount = 5;
      for (let p = 0; p < pilCount; p++) {
        const px = bX + (bW / (pilCount - 1)) * p - pilW / 2;
        g.fillStyle = 'rgba(176,90,32,0.12)'; g.fillRect(px, bY, pilW, bH);
      }

      // Горизонтальные линии — карнизы
      for (let i = 0; i <= FLOORS; i++) {
        const isCornice = i % 5 === 0 || i === 0 || i === FLOORS;
        const isBelt = i % 3 === 0;
        if (isCornice) {
          g.fillStyle = 'rgba(176,90,32,0.5)'; g.fillRect(bX - 3, bY + i * fH - 1, bW + 6, 3);
        } else if (isBelt) {
          g.fillStyle = 'rgba(176,90,32,0.22)'; g.fillRect(bX, bY + i * fH, bW, 1.5);
        } else {
          g.strokeStyle = 'rgba(176,90,32,0.1)'; g.lineWidth = 0.5;
          g.beginPath(); g.moveTo(bX, bY + i * fH); g.lineTo(bX + bW, bY + i * fH); g.stroke();
        }
      }

      // Окна главного здания
      const cw = (bW - 16) / COLS, wW = Math.max(2, cw - 5), wH = Math.max(3, fH - 7);
      for (let r = 0; r < FLOORS; r++) for (let c = 0; c < COLS; c++) {
        const a = lit[r * COLS + c];
        const wx = bX + 8 + c * cw, wy = bY + r * fH + 4;
        // Тёмная рама окна
        g.fillStyle = 'rgba(0,0,0,0.5)'; g.fillRect(wx - 1, wy - 1, wW + 2, wH + 2);
        // Стекло / свет
        if (a) {
          const warm = g.createLinearGradient(wx, wy, wx + wW, wy + wH);
          warm.addColorStop(0, 'rgba(255,215,100,' + (a * 0.95).toFixed(2) + ')');
          warm.addColorStop(1, 'rgba(242,180,60,' + (a * 0.65).toFixed(2) + ')');
          g.fillStyle = warm;
        } else {
          g.fillStyle = 'rgba(90,50,25,0.25)';
        }
        g.fillRect(wx, wy, wW, wH);
        // Оконная рама (крест)
        if (wW > 6 && wH > 6) {
          g.strokeStyle = 'rgba(0,0,0,0.5)'; g.lineWidth = 0.8;
          g.beginPath(); g.moveTo(wx + wW / 2, wy); g.lineTo(wx + wW / 2, wy + wH); g.stroke();
        }
      }

      // Вертикальные золотые фины на углах
      g.fillStyle = 'rgba(176,90,32,0.6)';
      g.fillRect(bX - 3, bY, 3, bH);
      g.fillRect(bX + bW, bY, 3, bH);

      // Парапет верхний
      g.fillStyle = '#120d09'; g.fillRect(bX - 6, bY - 12, bW + 12, 14);
      g.fillStyle = 'rgba(176,90,32,0.6)'; g.fillRect(bX - 6, bY - 14, bW + 12, 3);

      // Антенна
      g.strokeStyle = 'rgba(176,90,32,0.75)'; g.lineWidth = 2;
      g.beginPath(); g.moveTo(W / 2, bY - 12); g.lineTo(W / 2, bY - 48); g.stroke();
      g.lineWidth = 1; g.strokeStyle = 'rgba(176,90,32,0.4)';
      g.beginPath(); g.moveTo(W / 2 - 8, bY - 36); g.lineTo(W / 2 + 8, bY - 36); g.stroke();
      // Мигающий красный огонь
      const blink = reduce ? 0.85 : (0.4 + Math.abs(Math.sin(time * 1.6)) * 0.6);
      g.fillStyle = 'rgba(255,70,70,' + blink.toFixed(2) + ')';
      g.beginPath(); g.arc(W / 2, bY - 50, 2.5, 0, Math.PI * 2); g.fill();
      if (!reduce) {
        g.fillStyle = 'rgba(255,70,70,' + (blink * 0.3).toFixed(2) + ')';
        g.beginPath(); g.arc(W / 2, bY - 50, 6, 0, Math.PI * 2); g.fill();
      }

      // Вход — колонны и козырёк
      const entW = bW * 0.42, entX = bX + (bW - entW) / 2;
      g.fillStyle = 'rgba(176,90,32,0.08)'; g.fillRect(entX, ground - plinthH, entW, plinthH);
      // 3 колонны
      [0.15, 0.5, 0.85].forEach((t) => {
        const cx = entX + entW * t;
        g.fillStyle = '#1a160e'; g.fillRect(cx - 3.5, ground - plinthH + 4, 7, plinthH - 4);
        g.fillStyle = 'rgba(176,90,32,0.5)'; g.fillRect(cx - 4, ground - plinthH, 8, 5);
        g.fillRect(cx - 4, ground - 10, 8, 10);
      });
      // Козырёк входа
      g.fillStyle = 'rgba(176,90,32,0.35)'; g.fillRect(entX - 4, ground - plinthH - 2, entW + 8, 4);

      // Земля
      const groundGrad = g.createLinearGradient(0, ground, 0, H);
      groundGrad.addColorStop(0, '#100b08'); groundGrad.addColorStop(1, '#0b0806');
      g.fillStyle = groundGrad; g.fillRect(0, ground, W, H - ground);
      // Золотая линия земли
      g.fillStyle = 'rgba(176,90,32,0.4)'; g.fillRect(0, ground, W, 1.5);

      // Отражение в земле
      if (!reduce) {
        g.save(); g.globalAlpha = 0.08; g.translate(0, ground * 2); g.scale(1, -1);
        for (let r = Math.max(0, FLOORS - 6); r < FLOORS; r++) for (let c = 0; c < COLS; c++) {
          const a = lit[r * COLS + c];
          if (a) { g.fillStyle = 'rgba(242,196,106,' + a + ')'; g.fillRect(bX + 8 + c * cw, bY + r * fH + 4, wW, wH); }
        }
        g.restore();
      }

      // Метка «интерактивная 3D»
      if (!canvas.dataset.labelDrawn) {
        canvas.dataset.labelDrawn = '1';
      }
    };
    // The twinkle runs at ~12 frames a second and only while the poster is on screen and the tab is visible.
    let last = 0, visible = true;
    const loop = (now) => {
      raf = 0;
      if (done()) return stop();
      if (!visible || document.hidden) return;
      const dt = now - last;
      if (dt > 80) { last = now; time += dt / 1000; draw(); }
      raf = requestAnimationFrame(loop);
    };
    const resume = () => { if (!reduce && !raf && visible && !document.hidden) raf = requestAnimationFrame(loop); };
    const io = 'IntersectionObserver' in window ? new IntersectionObserver((es) => { visible = es[es.length - 1].isIntersecting; resume(); }) : null;
    if (io) io.observe(canvas);
    document.addEventListener('visibilitychange', resume);
    const stop = () => {
      clearInterval(timer); cancelAnimationFrame(raf); timer = null; raf = 0;
      if (io) io.disconnect();
      document.removeEventListener('visibilitychange', resume);
    };
    shuffle();
    requestAnimationFrame(draw);
    if (!reduce) { timer = setInterval(shuffle, 1800); resume(); }
    return stop;
  }

  // For the other public pages (about.js, gallery.js): the same footer, reveal on scroll, icons and headings.
  // Search engines: Schema.org data about the complex, from the config.js contacts (one source with the contact page).
  // A JSON data block, not code: the Content-Security-Policy does not apply to it, and nothing runs it.
  (function structuredData() {
    const page = document.body && document.body.dataset.page;
    if (!['home', 'apartments', 'about', 'gallery', 'contact'].includes(page) || document.querySelector('script[type="application/ld+json"]')) return;
    const c = contacts();
    const name = t('brand.name');
    const address = c.address ? { '@type': 'PostalAddress', streetAddress: c.address, addressLocality: 'Душанбе', addressCountry: 'TJ' } : undefined;
    const data = { '@context': 'https://schema.org', '@graph': [
      { '@type': 'Organization', '@id': location.origin + '/#org', name, url: location.origin + '/', logo: location.origin + '/favicon.svg', telephone: c.phone || undefined, address },
      { '@type': 'ApartmentComplex', name, url: location.origin + '/', telephone: c.phone || undefined, address, containedInPlace: { '@type': 'City', name: 'Душанбе' } },
    ] };
    const el = document.createElement('script'); el.type = 'application/ld+json'; el.textContent = JSON.stringify(data);
    document.head.appendChild(el);
  })();
  S.site = { footer: siteFooter, reveal: revealIn, icon: landIcon, accentFirst, contacts, drawFloorPlan, building: siteBuilding, galleryPhotos: GALLERY_PHOTOS, galleryImg, galleryCredit };
})();
