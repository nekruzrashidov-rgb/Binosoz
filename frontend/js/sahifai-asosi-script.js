/* Public site, three pages: the landing (home), the catalog with the 3D tower, booking and calculator (apartments),
   and the contact page (contact). */
(function () {
  const S = (window.S = window.S || {});
  const { h, t } = { h: (...a) => S.h(...a), t: (...a) => S.t(...a) };
  S.views = S.views || {};

  const STATUS_VAR = { free: 'var(--good)', booked: 'var(--warn)', sold: 'var(--muted)', onrequest: 'var(--info)' };
  const STATUS_BG = { free: 'var(--good-soft)', booked: 'var(--warn-soft)', sold: 'var(--surface-3)', onrequest: 'var(--info-soft)' };
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
    arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
    camera: '<rect x="3" y="7" width="13" height="10" rx="2"/><path d="m16 11 5-3v8l-5-3"/>',
    lift: '<rect x="5" y="3" width="14" height="18" rx="1"/><path d="M12 3v18M8.5 9 7 7l-1.5 2M15.5 7 17 9l1.5-2"/>',
    shop: '<path d="M4 9h16l-1-4H5zM5 9v11h14V9"/><path d="M9.5 20v-5h5v5"/>',
    box: '<path d="M3.5 7.5 12 3l8.5 4.5v9L12 21l-8.5-4.5z"/><path d="M3.5 7.5 12 12l8.5-4.5M12 12v9"/>',
    access: '<circle cx="12" cy="4.5" r="1.8"/><path d="M12 7v7h5l2 5"/><path d="M8.5 11a5.5 5.5 0 1 0 6.8 7"/>',
    gym: '<path d="M6 8v8M3 10v4M18 8v8M21 10v4M6 12h12"/>',
    pool: '<path d="M3 17c2 0 2-1.5 4.5-1.5S10 17 12 17s2-1.5 4.5-1.5S19 17 21 17M3 21c2 0 2-1.5 4.5-1.5S10 21 12 21s2-1.5 4.5-1.5S19 21 21 21"/><path d="M8 13V6a2 2 0 0 1 4 0M16 13V6a2 2 0 0 0-4 0M8 9h8"/>',
    gate: '<path d="M3 21V8l9-5 9 5v13"/><path d="M7 21v-9h10v9M12 12v9"/>',
    ball: '<circle cx="12" cy="12" r="9"/><path d="M3.5 9.5c5 1 12 1 17 0M3.5 14.5c5-1 12-1 17 0M12 3c-3 5-3 13 0 18"/>',
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
  // Amenity key (BuildingAmenities on the server) → icon.
  const AMEN_ICON = { underground_parking: 'car', guest_parking: 'car', closed_yard: 'gate', playground: 'play', sports_ground: 'ball', security: 'shield',
    cctv: 'camera', concierge: 'users', elevators: 'lift', landscaping: 'leaf', commercial_floor: 'shop', storage_rooms: 'box', barrier_free: 'access', fitness: 'gym', pool: 'pool' };
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
      .then(() => add('/js/modeli-3d-rejim-script.js'))
      .catch((e) => { threeLoading = null; throw e; });
    return threeLoading;
  }
  // The apartment inside (walls, furniture) — only when someone opens «Посмотреть внутри».
  let interiorLoading = null;
  function loadInterior() {
    if (S.House3D && S.House3D.interior) return Promise.resolve();
    if (!interiorLoading) interiorLoading = new Promise((ok, fail) => { const el = document.createElement('script'); el.src = '/js/modeli-3d-kvartira-script.js'; el.onload = ok; el.onerror = () => { interiorLoading = null; fail(); }; document.head.appendChild(el); });
    return interiorLoading;
  }

  // Section headings are plain text: the old "first word in copper" on every title carried no meaning.
  // The name is kept so the pages that call it need no change.
  function accentFirst(text) { return String(text); }
  // "16 этажей", "21 этаж", "71 квартира": the i18n value holds the forms as "one|few|many" (S.plural).
  const plural = (n, key) => S.plural(n, key);
  const counted = (n, key) => S.fmt.n(n) + ' ' + plural(n, key);
  const PRICE_STEP = 10000;
  // Contacts from config.js; the address in the page language (S.brandAddress).
  const contacts = () => { const c = (window.SITORA_CONFIG || {}).CONTACT || {}; return Object.assign({}, c, { address: S.brandAddress(c.address), manager: c.manager === 'Некруз' ? t('contact.manager') : c.manager }); };
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


  // Blocks with .reveal fade in as they scroll into view: the page-wide reveal (routing-va-navigatsiya.js) watches every
  // block, also those drawn later. Kept for the pages that still call it; onShow runs for each block once it is in.
  function revealIn(root, onShow) {
    if (S.revealSections && !onShow) { S.revealSections(root); return () => {}; }
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
    // h2: on pages without section headings (the gallery) an h3 right after the h1 broke the heading order (A-29).
    const col = (title, ...items) => h('div', { class: 'foot-col' }, h('h2', null, title), ...items);
    return h('footer', { class: 'footer lux-footer' }, h('div', { class: 'wrap' },
      h('div', { class: 'foot-grid' },
        h('div', { class: 'foot-brand' }, h('a', { class: 'row', href: '/', style: { gap: '12px' } }, S.towerLogo(), h('b', null, t('brand.name'))), h('p', null, t('foot.tagline')),
          h('div', { class: 'foot-social' },
            c.telegram ? h('a', { class: 'soc tg', href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener', 'aria-label': 'Telegram', title: 'Telegram' }, landIcon('send')) : null,
            c.phone ? h('a', { class: 'soc wa', href: 'https://wa.me/' + phoneDigits(c.phone), target: '_blank', rel: 'noopener', 'aria-label': 'WhatsApp', title: 'WhatsApp' }, landIcon('chat')) : null,
            h('a', { class: 'btn ghost sm', href: '/login' }, t('foot.login'))),
          // Desktop only: a QR code of the site, to open it on a phone during a demo (frontend/tools/qr/make-qr.mjs).
          h('div', { class: 'foot-qr' }, h('img', { src: '/img/qr-binosoz.svg', alt: t('qr.alt'), width: 88, height: 88, loading: 'lazy' }), h('span', null, t('qr.open')))),
        col(t('foot.apts'), h('a', { href: '/apartments' }, t('foot.catalog')), h('a', { href: '/apartments#calc' }, t('foot.calc')), h('a', { href: '/apartments' }, t('foot.3d'))),
        col(t('foot.info'), h('a', { href: '/#why' }, t('foot.why')), h('a', { href: '/#steps' }, t('foot.steps')), h('a', { href: '/login' }, t('foot.cabinet'))),
        col(t('foot.contacts'), h('span', null, c.address || ''), c.phone ? h('a', { href: 'tel:+' + phoneDigits(c.phone) }, c.phone) : null, c.telegram ? h('a', { href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, c.telegram) : null, h('a', { href: '/contact' }, t('foot.write')))),
      h('div', { class: 'foot-bottom' }, h('span', null, '© ' + new Date().getFullYear() + ' ' + t('brand.name') + '. ' + t('footer.rights') + ' · ' + t('footer.platform')),
        h('a', { class: 'foot-privacy', href: '/privacy' }, t('privacy.link')),
        h('span', { class: 'foot-dev' }, t('foot.dev') + ' ', h('a', { href: '/register' }, t('foot.devLink')))),
      h('p', { class: 'foot-demo' }, t('foot.demo'), frontendVersion())));
  }

  // Which builds are served: the site (version.txt — the short commit SHA written by the Docker build and by the Vercel
  // build command; «dev» in the repository) and the API (/health/live of the API's own origin). On Vercel they are two
  // deploys and can differ for a while, so both are shown: «сайт v 1e394f0 · API v c29bfc1».
  function frontendVersion() {
    const el = h('span', { class: 'foot-ver' });
    const clean = (v) => { v = String(v || '').trim().toLowerCase(); return /^([0-9a-f]{7}|dev)$/.test(v) ? v : 'dev'; };
    const site = fetch('/version.txt', { cache: 'no-store' }).then((r) => (r.ok ? r.text() : '')).then(clean).catch(() => 'dev');
    let apiOrigin = '';
    try { apiOrigin = new URL(S.apiBase ? S.apiBase() : '/api/v1', location.href).origin; } catch (e) { /* no API address */ }
    const api = apiOrigin ? fetch(apiOrigin + '/health/live', { cache: 'no-store' }).then((r) => (r.ok ? r.json() : null)).then((j) => (j && j.version ? clean(j.version) : null)).catch(() => null) : Promise.resolve(null);
    Promise.all([site, api]).then(([sv, av]) => {
      el.textContent = ' · ' + t('foot.verSite') + ' v ' + sv + (av ? ' · API v ' + av : '');
      console.info('Binosoz: ' + t('foot.verSite') + ' ' + sv + (av ? ', API ' + av : ''));
    });
    return el;
  }

  // Page head of the inner pages: a way back to the landing and the title.
  const pageHead = (title, sub) => h('section', { class: 'lux-pagehead' }, h('div', { class: 'wrap' },
    h('a', { class: 'back-link', href: '/' }, '← ' + t('nav.home')), h('h1', null, accentFirst(title)), sub ? h('p', null, sub) : null));

  /* ---------- catalog (design docs/design/Apartments.dc.html): the 3D model in a frame with the floors on its right,
     the panel of the chosen floor and apartment, the calculator over the developer's terms, then the layouts, the eco
     passport and the whole list with filters. Every number comes from the API. ---------- */
  S.views.apartments = function (root) {
    const cfg = window.SITORA_CONFIG || {};
    const st = { building: null, floors: [], apts: [], rate: null, sel: null, floor: null, floorRooms: 'all', rooms: 'all', maxPrice: 0, priceCap: 0, sort: 'price', onlyFree: true, shown: 12, time: 'dusk', calcApt: null };
    let house = null, alive = true, full = null, wantFull = location.hash === '#3d', pushedFull = false;
    const page = h('div', { class: 'page-in nr nr-cat' });
    root.appendChild(page);

    // 1. Page head: breadcrumbs, the title, live numbers, the ways to reach the manager.
    const cc = contacts();
    const topStats = h('dl', { class: 'nr-cat-stats' });
    page.appendChild(h('section', { class: 'nr-pagehead' }, h('div', { class: 'nr-wrap nr-pagehead-in' },
      h('div', null,
        h('nav', { class: 'nr-crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.apartments'))),
        (() => { const el = fromShell('catTitle') || h('h1', { class: 'nr-page-title' }); el.textContent = t('cat.title'); return el; })(), h('p', { class: 'nr-page-sub' }, t('nr.cat.sub')), topStats),
      (cc.phone || cc.telegram) ? h('div', { class: 'nr-pagehead-contact' },
        cc.phone ? h('a', { class: 'nr-phone', href: 'tel:+' + phoneDigits(cc.phone) }, landIcon('phone'), cc.phone) : null,
        cc.telegram ? h('a', { class: 'btn nr-btn nr-btn--primary', href: telegramUrl(cc.telegram), target: '_blank', rel: 'noopener' }, landIcon('send'), 'Telegram') : null) : null)));

    // 2. The model and the floor panel.
    // Until the scene draws its first frame (3–5 s on a phone), a render of the same model instead of a black box.
    const housePoster = h('img', { class: 'nr-3d-poster', src: '/img/renders/tower-dusk-tall-800.webp', srcset: '/img/renders/tower-dusk-tall-800.webp 800w, /img/renders/tower-dusk-tall-1600.webp 1600w', sizes: '(max-width: 900px) 100vw, 60vw', alt: '', 'aria-hidden': 'true', decoding: 'async' });
    const houseWait = h('div', { class: 'nr-3d-wait', role: 'status' }, t('hero.loading3d'));
    const houseBox = h('div', { class: 'house3d nr-house', id: 'house3d' }, housePoster, houseWait);
    const tip = h('div', { class: 'tip', hidden: true });
    const loading = h('div', { class: 'nr-stage-loading' }, t('hero.loading'));
    const rail = h('div', { class: 'nr-rail', role: 'group', 'aria-label': t('hero.floors') });
    const timeSeg = h('div', { class: 'nr-seg', role: 'group', 'aria-label': t('hero.time') }, ['day', 'dusk', 'night'].map((k) => h('button', { type: 'button', 'aria-pressed': String(st.time === k), 'data-k': k,
      onclick: () => { st.time = k; house && house.setTime(k); S.$$('button', timeSeg).forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.k === k))); } }, t('time.' + k))));
    const tools = h('div', { class: 'nr-model-tools' },
      h('button', { class: 'nr-icon-btn', type: 'button', title: t('hero.zoomIn'), 'aria-label': t('hero.zoomIn'), onclick: () => house && house.zoom(0.8) }, S.icon('zin')),
      h('button', { class: 'nr-icon-btn', type: 'button', title: t('hero.zoomOut'), 'aria-label': t('hero.zoomOut'), onclick: () => house && house.zoom(1.25) }, S.icon('zout')),
      h('button', { class: 'nr-icon-btn', type: 'button', title: t('hero.reset'), 'aria-label': t('hero.reset'), onclick: () => { house && (house.clearSelection(), house.resetView()); } }, S.icon('rotate')));
    // «Открыть 3D»: the same model on the whole screen (/apartments#3d) with the views, the time of day and the apartments.
    const open3d = h('button', { class: 'btn nr-btn nr-btn--dark nr-open3d', type: 'button', onclick: () => openFull(true) }, landIcon('cube'), t('h3d.open'));
    const legend = h('div', { class: 'nr-legend' }, ['free', 'booked', 'sold', 'onrequest'].map((k) => h('span', null, h('i', { class: 'nr-dot ' + k }), t('status.' + k))));
    const panel = h('div', { class: 'nr-floorpanel', 'aria-live': 'polite' });
    const stage = h('section', { class: 'nr-sec nr-stage-sec', id: 'hero-top' }, h('div', { class: 'nr-wrap nr-stage-grid' },
      h('div', { class: 'nr-model' },
        h('div', { class: 'nr-model-head' }, h('span', { class: 'nr-model-title' }, t('nr.cat.model')), h('div', { class: 'nr-model-acts' }, timeSeg, open3d)),
        h('div', { class: 'nr-stage' }, houseBox, tip, loading, rail, tools),
        legend),
      panel));
    page.appendChild(stage);

    const calcSection = h('section', { class: 'nr-sec nr-dark', id: 'calc' });
    const plansSection = h('section', { class: 'nr-sec', id: 'plans' });
    const ecoSection = h('section', { class: 'nr-sec nr-sec--tight', id: 'eco', hidden: true });
    const aptSection = h('section', { class: 'nr-sec nr-band', id: 'apartments' });
    page.append(calcSection, plansSection, ecoSection, aptSection, siteFooter());

    // ---------- data ----------
    // No building, no apartments, or the API did not answer: one honest card instead of the model, the calculator and the
    // lists (with the phone and a request link). Nothing invented is shown.
    function showEmpty(state) {
      [stage, calcSection, plansSection, ecoSection].forEach((s) => { s.hidden = true; });
      topStats.replaceChildren();
      aptSection.replaceChildren(h('div', { class: 'nr-wrap' }, soonCard(state, () => {
        [stage, calcSection, plansSection].forEach((s) => { s.hidden = false; });
        aptSection.replaceChildren(); load();
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
        // The slider's ends are multiples of its step: max rounded up, min down — the right end keeps the dearest apartments.
        st.maxPrice = Math.ceil(Math.max(...apts.map((a) => a.price), 0) / PRICE_STEP) * PRICE_STEP;
        st.priceCap = st.maxPrice;
      } catch (e) {
        if (alive) showEmpty('error');
        return;
      }
      if (!alive) return;
      loading.remove();
      // The floor with free apartments, the highest first (the best views); without free ones — the top floor.
      const residential = st.floors.filter((f) => !f.commercial).map((f) => f.number).sort((a, b) => b - a);
      st.floor = residential.find((n) => st.apts.some((a) => a.floor === n && a.status === 'free')) || residential[0] || null;
      st.sel = firstFree(st.floor);
      const askedRooms = new URLSearchParams(location.search).get('rooms');
      if (askedRooms && roomOptions(st.apts).some(([v]) => v !== 'all' && v === askedRooms)) st.rooms = askedRooms;
      renderAll();
      initHouse();
      if (st.rooms !== 'all') setTimeout(() => scrollTo('apartments'), 100);
      // /apartments#calc from the landing: the calculator exists only now, after the data.
      if (location.hash === '#calc') setTimeout(() => scrollTo('calc'), 100);
    }
    const firstFree = (n) => st.apts.filter((a) => a.floor === n).sort((a, b) => a.number - b.number).find((a) => a.status === 'free') || null;

    function initHouse() {
      // The 3D engine is loaded now (once): when the stage comes near the screen.
      if (!S.House3D || !S.House3DMode) {
        const go = () => loadHouse3D().then(() => { if (alive && S.House3D) initHouse(); }).catch(() => { houseWait.remove(); open3d.hidden = true; /* no 3D: the render stays, the floor list and the apartments still work */ });
        // A-06: Three.js and the model are the heaviest part of the page. On a phone the render stays with a button —
        // the 3D loads only on a tap (or with «Открыть 3D» / #3d); on a desktop — when the stage is near the screen and
        // the browser is idle, so the first screen and the catalog are ready first.
        const phone = !!(window.matchMedia && window.matchMedia('(max-width: 900px), (pointer: coarse)').matches);
        const idle = (fn) => ('requestIdleCallback' in window ? window.requestIdleCallback(fn, { timeout: 3000 }) : setTimeout(fn, 1200));
        if (wantFull) go();
        else if (phone) {
          const tap = h('button', { class: 'btn nr-btn nr-btn--primary nr-3d-load', type: 'button', onclick: () => { tap.remove(); houseWait.hidden = false; go(); } }, landIcon('cube'), t('nr.cat.load3d'));
          houseWait.hidden = true;
          houseBox.append(tap);
        } else if ('IntersectionObserver' in window) {
          const io = new IntersectionObserver((es) => { if (es.some((e) => e.isIntersecting)) { io.disconnect(); idle(go); } }, { rootMargin: '200px' });
          io.observe(stage);
        } else idle(go);
        return;
      }
      try {
        house = S.House3D.create(houseBox, {
          floors: Math.max(...st.floors.map((f) => f.number), 1), time: st.time,
          onReady: () => setTimeout(() => { housePoster.remove(); houseWait.remove(); }, 400),
          onSelectApartment: modelSelect,
          onSelectFloor: (n) => { if (n === null) return; selectFloor(n, true); },
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
      window.__house3d = house;
      const tapLoad = houseBox.querySelector('.nr-3d-load'); if (tapLoad) tapLoad.remove();
      pushToHouse();
      if (wantFull) openFull(false);
    }
    // ---------- 3D on the whole screen ----------
    // A filter of the catalog (rooms, price, only free) — the same in the list below and in the 3D mode.
    function passes(a) {
      if (st.onlyFree && a.status !== 'free') return false;
      if (!S.roomsKind.match(a, st.rooms)) return false;
      return !st.priceCap || a.price <= st.priceCap;
    }
    function openFull(push) {
      if (full) return;
      if (!house) {
        wantFull = true;
        if (!S.House3D) initHouse();
        // No WebGL: the catalog says so and stays as it is.
        else if (houseBox.querySelector('.empty')) S.toast(t('house.no3d'), 'err');
        return;
      }
      wantFull = false;
      if (push && location.hash !== '#3d') { history.pushState({ h3d: 1 }, '', location.pathname + location.search + '#3d'); pushedFull = true; }
      full = S.House3DMode.open({
        house, box: houseBox, st, building: st.building, rate: st.rate, phone: cc.phone, selected: st.sel,
        apts: () => st.apts, matches: passes, roomOptions,
        onFilter: () => { st.shown = 12; renderApartments(); },
        onPick: (a) => pickApt(a, true),
        onModelSelect: modelSelect,
        book: (a) => bookModal(a),
        loadInterior,
        requestClose: () => { if (pushedFull) history.back(); else { closeFull(); history.replaceState(null, '', location.pathname + location.search); } },
      });
    }
    function closeFull() {
      if (!full) return;
      full.close(); full = null; pushedFull = false;
      house.setTime(st.time, true);
      if (st.sel) house.selectApartment(st.sel.id); else house.resetView();
    }
    const onPop = () => { if (location.hash === '#3d') openFull(false); else closeFull(); };
    window.addEventListener('popstate', onPop);
    function modelSelect(a) { const apt = st.apts.find((x) => x.id === a.id); if (apt) pickApt(apt, true); }
    function pushToHouse() {
      if (!house) return;
      house.setData(st.apts.map((a) => ({ id: a.id, floor: a.floor, planSlot: a.planSlot, number: a.number, status: a.status, rooms: a.rooms, area: a.area, price: a.price })));
      if (st.sel) house.selectApartment(st.sel.id); else if (st.floor !== null) house.selectFloor(st.floor);
    }

    // ---------- render ----------
    // Each block on its own: an error in one (logged) must not leave the calculator, layouts and list empty.
    function renderAll() {
      [renderTopStats, renderRail, renderPanel, renderCalc, renderPlans, renderEco, renderApartments].forEach((fn) => {
        try { fn(); } catch (e) { console.error('Каталог: блок ' + fn.name + ' не отрисован', e); if (S.logError) S.logError(e); }
      });
    }
    function renderTopStats() {
      const free = st.apts.filter((a) => a.status === 'free');
      const prices = (free.length ? free : st.apts).map((a) => a.price).filter((p) => p > 0);
      const stat = (v, l) => h('div', null, h('dt', null, l), h('dd', null, v));
      topStats.replaceChildren(stat(S.fmt.n(free.length), t('wow.cat.free')),
        prices.length ? stat(S.fmt.money(Math.min(...prices), 'TJS'), t('wow.cat.from')) : null,
        stat(S.fmt.n(st.building.floorsCount || st.floors.length), t('wow.cat.floors')));
    }
    function renderEco() {
      const card = S.eco ? S.eco.block(st.building.eco, { demo: st.building.isDemo }) : null;
      ecoSection.hidden = !card;
      ecoSection.replaceChildren(card ? h('div', { class: 'nr-wrap' }, card) : '');
    }

    // Floors on the right of the model, the top floor first; the number of free apartments under each.
    function renderRail() {
      const free = (n) => st.apts.filter((a) => a.floor === n && a.status === 'free').length;
      rail.replaceChildren(...st.floors.slice().sort((a, b) => b.number - a.number).map((f) => {
        const c = free(f.number);
        return h('button', { type: 'button', class: 'nr-rail-btn' + (c ? '' : ' zero'), 'aria-pressed': String(st.floor === f.number),
          'aria-label': f.commercial ? t('floor.commercial') : t('floor.n', { n: f.number }) + ', ' + t('floor.free', { n: c }), onclick: () => selectFloor(f.number) },
        h('span', null, f.commercial ? t('nr.cat.comShort') : f.number));
      }));
      const on = S.$('[aria-pressed="true"]', rail);
      if (on && on.scrollIntoView && rail.scrollHeight > rail.clientHeight) on.scrollIntoView({ block: 'nearest' });
    }
    function selectFloor(n, fromModel) {
      st.floor = n; st.floorRooms = 'all';
      st.sel = firstFree(n);
      if (house && !fromModel) { if (st.sel) house.selectApartment(st.sel.id); else house.selectFloor(n); }
      renderRail(); renderPanel(); syncCalc();
    }
    function pickApt(a, fromModel) {
      st.sel = a; st.floor = a.floor;
      if (house && !fromModel) house.selectApartment(a.id);
      renderRail(); renderPanel(); syncCalc();
    }

    // The developer's terms for «от … в месяц»: the minimum down payment over the longest term.
    const terms = () => siteTerms(st.building);
    function cheapestMonthly(price) {
      const x = terms(); if (!x || !x.months) return null;
      const down = Math.round(price * (x.down || 0) / 100);
      return { monthly: Math.round((price - down) / x.months), down: x.down || 0, months: x.months };
    }

    function renderPanel() {
      const f = st.floor, fl = st.floors.find((x) => x.number === f);
      if (f === null || !fl) { panel.replaceChildren(h('p', { class: 'nr-panel-hint' }, t('nr.cat.pickHint'))); return; }
      if (fl.commercial) {
        panel.replaceChildren(h('div', { class: 'nr-panel-head' }, h('div', null, h('div', { class: 'nr-kicker' }, t('nr.cat.selFloor')), h('h2', { class: 'nr-panel-floor' }, t('floor.commercial')))),
          h('p', { class: 'nr-lead' }, t('floor.commercialText')),
          h('button', { class: 'btn nr-btn nr-btn--primary', type: 'button', onclick: () => leadModal(t('lead.commercial')) }, t('lead.send')));
        return;
      }
      const onFloor = st.apts.filter((a) => a.floor === f).sort((a, b) => a.number - b.number);
      const free = onFloor.filter((a) => a.status === 'free').length;
      const match = (a) => S.roomsKind.match(a, st.floorRooms);
      const units = onFloor.filter(match);
      const pills = h('div', { class: 'nr-pills', role: 'group', 'aria-label': t('filter.rooms') }, roomOptions(onFloor).map(([v, l]) =>
        h('button', { type: 'button', class: 'nr-pill', 'aria-pressed': String(st.floorRooms === v), onclick: () => { st.floorRooms = v; renderPanel(); } }, l)));
      const list = h('div', { class: 'nr-units' }, units.map((a) => h('button', { type: 'button', class: 'nr-unit st-' + a.status, 'aria-pressed': String(!!st.sel && st.sel.id === a.id), onclick: () => pickApt(a) },
        h('span', { class: 'nr-unit-top' }, h('b', null, t('nr.cat.aptN', { n: a.number })), h('span', { class: 'nr-unit-st' }, h('i', { class: 'nr-dot ' + a.status }), t('status.' + a.status))),
        h('span', { class: 'nr-unit-type' }, S.roomsKind.label(a.rooms)),
        h('span', { class: 'nr-unit-meta' }, S.fmt.area(a.area)),
        h('span', { class: 'nr-unit-price' }, S.fmt.money(a.price, 'TJS')))));
      panel.replaceChildren(
        h('div', { class: 'nr-panel-head' },
          h('div', null, h('div', { class: 'nr-kicker' }, t('nr.cat.selFloor')), h('h2', { class: 'nr-panel-floor' }, t('floor.n', { n: f }))),
          h('div', { class: 'nr-panel-free' }, t('nr.cat.freeOf', { n: S.fmt.n(free), m: S.fmt.n(onFloor.length) }))),
        pills, units.length ? list : h('p', { class: 'nr-panel-hint' }, t('nr.cat.noUnits')),
        st.sel && st.sel.floor === f ? detail(st.sel) : h('p', { class: 'nr-panel-hint' }, t('nr.cat.pickHint')));
    }
    // The chosen apartment: its plan, the facts, the price, «от … в месяц» by the developer's terms, booking.
    function detail(a) {
      const plan = h('canvas', { class: 'nr-detail-plan', width: 300, height: 200, role: 'img', 'aria-label': S.roomsKind.label(a.rooms) });
      drawFloorPlan(plan, S.roomsKind.group(a.rooms));
      const eq = st.rate ? Math.round(a.price / st.rate.rate) : null;
      const m = cheapestMonthly(a.price);
      const badge = S.eco && S.eco.badge(st.building.eco);
      return h('div', { class: 'nr-detail' }, h('div', { class: 'nr-detail-pic' }, plan),
        h('div', { class: 'nr-detail-body' },
          h('div', { class: 'nr-detail-kicker' }, t('nr.cat.aptN', { n: a.number }) + ' · ' + t('floor.n', { n: a.floor })),
          h('h3', { class: 'nr-detail-type' }, S.roomsKind.label(a.rooms)),
          h('dl', { class: 'nr-detail-facts' }, h('div', null, h('dt', null, t('nr.cat.area')), h('dd', null, S.fmt.area(a.area))), h('div', null, h('dt', null, t('nr.cat.sqm')), h('dd', null, S.fmt.money(sqmPrice(a), 'TJS')))),
          h('div', { class: 'nr-detail-label' }, t('nr.cat.price')),
          h('div', { class: 'nr-detail-price' }, S.fmt.money(a.price, 'TJS')),
          eq ? h('div', { class: 'nr-detail-note' }, '≈ $' + S.fmt.n(eq) + ' · ' + t('price.estimate')) : null,
          m ? h('div', { class: 'nr-detail-month' }, t('nr.cat.perMonth', { s: S.fmt.money(m.monthly, 'TJS') }), h('small', null, t('nr.cat.perMonthNote', { d: S.fmt.n(m.down), t: termText(m.months) }))) : null,
          badge ? h('a', { href: '#eco', class: 'eco-badge-link', onclick: (e) => { e.preventDefault(); scrollTo('eco'); } }, badge) : null,
          a.status === 'free'
            ? h('button', { class: 'btn nr-btn nr-btn--primary block', type: 'button', onclick: () => bookModal(a) }, t('book.cta'))
            : h('p', { class: 'nr-detail-off' }, t('book.unavailable')),
          h('div', { class: 'nr-detail-links' },
            h('button', { class: 'nr-link', type: 'button', onclick: () => { st.calcApt = a; renderCalc(); scrollTo('calc'); } }, t('calc.open'), arrow()),
            h('button', { class: 'nr-link', type: 'button', onclick: () => leadModal(t('lead.aboutApt', { n: a.number }), a.id) }, t('lead.ask'), arrow()))));
    }

    // ---------- booking ----------
    function bookModal(a) {
      const name = S.input('name', { placeholder: t('f.namePh'), required: true, autocomplete: 'name' });
      const phone = S.input('phone', { placeholder: '+992 9XX XX XX XX', type: 'tel', value: '+992', autocomplete: 'tel' });
      const form = h('form', { class: 'stack', novalidate: true }, S.field(t('f.name'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), h('p', { class: 'hint' }, t('book.note')), S.privacyNote());
      const btn = h('button', { class: 'btn gold', type: 'submit' }, t('book.confirm'));
      form.id = 'bookform'; btn.setAttribute('form', 'bookform');
      const md = S.modal({ title: t('book.title', { n: a.number }), body: [h('div', { class: 'card flat', style: { marginBottom: '14px' } }, h('div', { class: 'row between' }, h('b', null, t('apt.title', { n: a.number })), S.fmt.money(a.price, 'TJS')), h('div', { class: 'muted' }, t('floor.n', { n: a.floor }) + ' · ' + S.fmt.rooms(a.rooms) + ' · ' + S.fmt.area(a.area))), form], actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        if (!namePhoneOk(form, name, phone)) return;
        await S.busy(btn, async () => {
          try {
            const r = await S.api.book(a.id, { name: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, '') });
            md.close(); a.status = 'booked'; pushToHouse(); renderPanel(); renderRail(); renderApartments(); renderTopStats(); if (full) full.refresh();
            S.modal({ title: t('book.doneTitle'), body: h('div', { class: 'stack' }, h('p', null, t('book.doneText', { n: a.number })), r && r.expiresAt ? h('div', { class: 'next' }, h('span', null, t('book.until')), h('b', null, S.fmt.dt(r.expiresAt))) : null, h('p', { class: 'muted' }, t('book.doneNote'))), actions: [h('button', { class: 'btn', onclick: (ev) => ev.target.closest('.overlay').querySelector('.x').click() }, t('common.ok'))] });
          } catch (er) { S.showErrors(form, er); if (er.status === 409) { refreshApts(); } }
        });
      });
    }
    async function refreshApts() { try { st.apts = await S.api.allApartments(st.floors); pushToHouse(); renderPanel(); renderRail(); renderApartments(); renderTopStats(); if (full) full.refresh(); } catch (e) { /* ignore */ } }

    function leadModal(preset, aptId) {
      const name = S.input('name', { placeholder: t('f.namePh'), autocomplete: 'name' });
      const phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel' });
      const cm = h('textarea', { class: 'inp', name: 'comment', id: 'lead-comment', maxlength: 400 }); cm.value = preset || '';
      const form = h('form', { class: 'stack', id: 'leadform', novalidate: true }, S.field(t('f.name'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), S.field(t('f.comment'), cm), S.privacyNote());
      const btn = h('button', { class: 'btn', type: 'submit', form: 'leadform' }, t('lead.send'));
      const md = S.modal({ title: t('lead.title'), body: form, actions: [h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')), btn] });
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        if (!namePhoneOk(form, name, phone)) return;
        await S.busy(btn, async () => { try { await S.api.createLead({ name: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, ''), comment: cm.value.trim(), apartmentId: aptId }); md.close(); S.toast(t('lead.ok'), 'ok'); } catch (er) { S.showErrors(form, er); } });
      });
    }

    // ---------- calculator (dark): the chosen apartment, the developer's terms ----------
    function syncCalc() { if (!st.calcApt) renderCalc(); }
    function renderCalc() {
      const x = terms();
      const a = st.calcApt || st.sel;
      const free = st.apts.filter((z) => z.status === 'free');
      const price = a ? a.price : free.length ? Math.round(free.reduce((s, z) => s + z.price, 0) / free.length / 10000) * 10000 : 0;
      if (!price) { calcSection.hidden = true; return; }
      calcSection.hidden = false;
      const minDown = Math.max(0, Math.ceil(x && x.down != null ? x.down : 0));
      const maxYears = x && x.months ? Math.max(1, Math.floor(x.months / 12)) : 10;
      const cs = { down: Math.min(90, Math.max(minDown, 20)), years: Math.min(maxYears, 5) };
      const out = { down: h('b'), years: h('b'), monthly: h('span', { class: 'nr-calc-sum' }), note: h('p', { class: 'nr-calc-note' }) };
      const range = (id, label, min, max, step, key) => {
        const inp = h('input', { id, type: 'range', min, max, step, value: cs[key] });
        inp.addEventListener('input', () => { cs[key] = Number(inp.value); draw(); });
        return h('div', { class: 'nr-range' }, h('div', { class: 'nr-range-top' }, h('label', { for: id }, label), out[key]), inp);
      };
      let monthlyNow = 0;
      function draw() {
        const down = Math.round(price * cs.down / 100), months = cs.years * 12;
        monthlyNow = Math.round((price - down) / months);
        out.down.textContent = cs.down + '% · ' + S.fmt.money(down, 'TJS');
        out.years.textContent = counted(cs.years, 'nr.yearsN');
        out.monthly.textContent = S.fmt.money(monthlyNow, 'TJS');
        out.note.textContent = (a ? t('nr.cat.calcFor', { n: a.number, p: S.fmt.money(price, 'TJS') }) + ' · ' : '') + (x && x.zero ? t('nr.cat.zero') : t('nr.calc.totalAsk'));
      }
      const utilHost = h('div', { class: 'util-host' });
      const send = h('button', { class: 'btn nr-btn nr-btn--primary', type: 'button', onclick: () => leadModal(t('lead.calc', { p: S.fmt.n(price), d: cs.down, m: cs.years * 12, mo: S.fmt.n(monthlyNow) }), a && a.id) }, t('calc.send'));
      calcSection.replaceChildren(h('div', { class: 'nr-wrap nr-split nr-split--top' },
        h('div', null, kicker(t('nr.plan.kicker')), h('h2', { class: 'nr-h2' }, t('nr.cat.calcTitle')), h('p', { class: 'nr-lead' }, t(x && x.zero ? 'nr.cat.calcSubZero' : 'nr.cat.calcSub')), utilHost),
        h('div', { class: 'nr-calc nr-calc--dark' },
          range('c-down', t('plan.down'), minDown, 90, 5, 'down'),
          range('c-years', t('plan.term'), 1, maxYears, 1, 'years'),
          h('div', { class: 'nr-calc-out' }, h('span', { class: 'nr-calc-label' }, t('nr.calc.monthly')), out.monthly, out.note), send)));
      draw();
      // Energy costs of the chosen apartment (S.eco.utility): only with the building's estimated use and the developer's tariff.
      if (a && S.eco) S.api.utility(a.id).then((r) => { if (alive && r && utilHost.isConnected) utilHost.replaceChildren(S.eco.utility(r, a)); }).catch(() => {});
    }

    // ---------- layouts: one card per type, the button filters the list ----------
    function renderPlans() {
      const cards = PLAN_TYPES.map((type) => {
        const list = st.apts.filter((a) => S.roomsKind.group(a.rooms) === type);
        if (!list.length) return null;
        const prices = (list.some((a) => a.status === 'free') ? list.filter((a) => a.status === 'free') : list).map((a) => a.price).filter((p) => p > 0);
        const lo = Math.floor(Math.min(...list.map((a) => a.area))), hi = Math.ceil(Math.max(...list.map((a) => a.area)));
        const typeLabel = S.roomsKind.groupLabel(type, list);
        const canvas = h('canvas', { class: 'nr-layout-plan', width: 320, height: 210, role: 'img', 'aria-label': typeLabel });
        drawFloorPlan(canvas, type);
        return h('button', { class: 'nr-layout', type: 'button', onclick: () => { st.rooms = String(type); st.shown = 12; renderApartments(); scrollTo('apartments'); } },
          h('div', { class: 'nr-layout-pic' }, canvas),
          h('div', { class: 'nr-layout-body' },
            h('div', { class: 'nr-layout-top' }, h('span', { class: 'nr-layout-type' }, typeLabel), h('span', { class: 'nr-layout-area' }, (lo === hi ? S.fmt.n(lo) : S.fmt.n(lo) + '–' + S.fmt.n(hi)) + ' ' + t('u.m2'))),
            h('div', { class: 'nr-layout-bottom' }, prices.length ? h('span', { class: 'nr-layout-price' }, t('home.chip.from', { p: S.fmt.money(Math.min(...prices), 'TJS') })) : h('span'),
              h('span', { class: 'nr-layout-more' }, t('nr.look'), arrow()))));
      }).filter(Boolean);
      plansSection.hidden = !cards.length;
      plansSection.replaceChildren(h('div', { class: 'nr-wrap' },
        h('div', { class: 'nr-head-row' }, h('div', null, kicker(t('nr.layouts.kicker')), h('h2', { class: 'nr-h2' }, t('plans.title'))), h('p', { class: 'nr-head-note' }, t('nr.cat.plansSub'))),
        h('div', { class: 'nr-layouts', style: { '--n': String(Math.max(1, cards.length)) } }, cards)));
    }

    // ---------- the whole list with filters ----------
    // Room buttons: «Все» and only the types that exist (no «Студия» in a building without studios).
    function roomOptions(list) {
      // One pill per type that has apartments: studio, 1, 2, 3, 4+ (S.roomsKind).
      return [['all', t('filter.all')]].concat(S.roomsKind.types.filter((g) => list.some((a) => S.roomsKind.group(a.rooms) === g)).map((g) => [String(g), S.roomsKind.pill(g)]));
    }
    function filtered() {
      let l = st.apts.slice();
      if (st.onlyFree) l = l.filter((a) => a.status === 'free');
      if (st.rooms !== 'all') l = l.filter((a) => S.roomsKind.match(a, st.rooms));
      if (st.priceCap) l = l.filter((a) => a.price <= st.priceCap);
      const cls = st.building && st.building.eco && st.building.eco.energyClass;
      if (cls && st.minClass && st.minClass !== 'any' && S.eco.rank(cls) > S.eco.rank(st.minClass)) l = [];
      const s = { price: (a, b) => a.price - b.price, priceDesc: (a, b) => b.price - a.price, area: (a, b) => b.area - a.area, floor: (a, b) => b.floor - a.floor }[st.sort];
      return l.sort(s);
    }
    function renderApartments() {
      const rooms = roomOptions(st.apts);
      const minP = Math.floor(Math.min(...st.apts.map((a) => a.price)) / PRICE_STEP) * PRICE_STEP;
      const price = h('input', { type: 'range', id: 'f-price', min: minP, max: st.maxPrice, step: PRICE_STEP, value: st.priceCap, oninput: (e) => { st.priceCap = +e.target.value; priceVal.textContent = S.fmt.money(st.priceCap, 'TJS'); st.shown = 12; drawList(); } });
      const priceVal = h('b', null, S.fmt.money(st.priceCap, 'TJS'));
      const sort = S.select('sort', [['price', t('sort.price')], ['priceDesc', t('sort.priceDesc')], ['area', t('sort.area')], ['floor', t('sort.floor')]], st.sort); sort.addEventListener('change', () => { st.sort = sort.value; drawList(); });
      const grid = h('div', { class: 'nr-unit-grid', id: 'apt-grid' });
      const more = h('div', { class: 'nr-more' });
      const count = h('p', { class: 'nr-head-note' });
      function drawList() {
        const l = filtered(); count.textContent = t('list.found', { n: l.length });
        grid.replaceChildren(...(l.length ? l.slice(0, st.shown).map(renderCard) : [h('div', { style: { gridColumn: '1/-1' } }, S.empty(t('list.none'), 'building'))]));
        more.replaceChildren(l.length > st.shown ? h('button', { class: 'btn nr-btn nr-btn--dark', type: 'button', onclick: () => { st.shown += 12; drawList(); } }, t('list.more', { n: l.length - st.shown })) : '');
      }
      aptSection.replaceChildren(h('div', { class: 'nr-wrap' },
        h('div', { class: 'nr-head-row' }, h('div', null, kicker(t('nr.cat.allKicker')), h('h2', { class: 'nr-h2' }, t('apts.title'))), count),
        h('div', { class: 'nr-filters' },
          h('div', { class: 'nr-filter' }, h('span', { class: 'nr-filter-label', id: 'f-rooms' }, t('filter.rooms')), h('div', { class: 'nr-pills', role: 'group', 'aria-labelledby': 'f-rooms' }, rooms.map(([v, l]) => h('button', { type: 'button', class: 'nr-pill', 'aria-pressed': String(st.rooms === v), onclick: (e) => { st.rooms = v; st.shown = 12; S.$$('.nr-pill', e.currentTarget.parentNode).forEach((p) => p.setAttribute('aria-pressed', String(p === e.currentTarget))); drawList(); } }, l)))),
          h('div', { class: 'nr-filter nr-range' }, h('div', { class: 'nr-range-top' }, h('label', { for: 'f-price' }, t('filter.priceTo')), priceVal), price),
          h('div', { class: 'nr-filter' }, S.field(t('filter.sort'), sort)),
          ecoFilter(),
          h('label', { class: 'check nr-filter nr-check' }, h('input', { type: 'checkbox', checked: st.onlyFree, onchange: (e) => { st.onlyFree = e.target.checked; st.shown = 12; drawList(); } }), t('filter.onlyFree'))),
        grid, more));
      drawList();
    }
    function ecoFilter() {
      const cls = st.building && st.building.eco && st.building.eco.energyClass;
      if (!cls || !S.eco) return null;
      const s = S.select('minClass', [['any', t('filter.all')]].concat(S.eco.classes.map(([k, l]) => [k, l])), st.minClass || 'any');
      s.addEventListener('change', () => { st.minClass = s.value; st.shown = 12; renderApartments(); });
      return h('div', { class: 'nr-filter' }, S.field(t('eco.filter'), s));
    }
    // A card of the list: the plan of its type, status and floor, number, type, area, price; it opens the apartment on the model.
    function renderCard(a) {
      const plan = h('canvas', { class: 'nr-unit-plan', width: 320, height: 180, 'aria-hidden': 'true' });
      drawFloorPlan(plan, S.roomsKind.group(a.rooms));
      return h('button', { class: 'nr-unit-card', type: 'button', onclick: () => { pickApt(a); scrollTo('home'); } },
        h('span', { class: 'nr-unit-card-pic' }, plan),
        h('span', { class: 'nr-unit-card-body' },
          h('span', { class: 'nr-unit-top' }, h('b', null, t('nr.cat.aptN', { n: a.number })), h('span', { class: 'nr-unit-st' }, h('i', { class: 'nr-dot ' + a.status }), t('status.' + a.status))),
          h('span', { class: 'nr-unit-type' }, S.roomsKind.label(a.rooms)),
          h('span', { class: 'nr-unit-meta' }, t('floor.n', { n: a.floor }) + ' · ' + S.fmt.area(a.area) + ' · ' + S.fmt.n(sqmPrice(a)) + ' TJS / ' + t('u.m2')),
          h('span', { class: 'nr-unit-price' }, S.fmt.money(a.price, 'TJS'))));
    }

    function scrollTo(id) { const el = document.getElementById(id === 'home' ? 'hero-top' : id); if (el) el.scrollIntoView({ behavior: S.motionOk === false ? 'auto' : 'smooth', block: 'start' }); }
    S.scrollTo = scrollTo;

    load();
    return () => { alive = false; window.removeEventListener('popstate', onPop); if (full) { full.close(); full = null; } if (house) { house.destroy(); if (window.__house3d === house) delete window.__house3d; } };
  };

  /* ---------- landing (design «Некруз Резиденс», docs/design/Main.dc.html) ----------
     Everything that is a fact about the house comes from the API: name, floors, apartments, free ones, prices, year,
     description, amenities, eco passport, installment terms. Without the data a block is not shown; without a building —
     one honest card «Продажи скоро откроются». The pictures are renders of the site's 3D model, signed as such. */
  // A render of the 3D model (frontend/tools/renders): WebP 800/1600/2400.
  function renderImg(name, sizes, alt, eager) {
    const src = (w) => '/img/renders/' + name + '-' + w + '.webp';
    const tall = /tall/.test(name);
    return h('img', { src: src(1600), srcset: [800, 1600, 2400].map((w) => src(w) + ' ' + w + 'w').join(', '), sizes,
      width: 2400, height: tall ? 3000 : 1350, alt, loading: eager ? 'eager' : 'lazy', decoding: 'async', fetchpriority: eager ? 'high' : null });
  }
  // The hero: the wide frame, on a phone the tall one — a wide frame cut to a phone shows only a grid of windows.
  function heroPicture() {
    const src = (w) => '/img/renders/tower-dusk-tall-' + w + '.webp';
    return h('picture', null, h('source', { media: '(max-width: 700px)', srcset: [800, 1600, 2400].map((w) => src(w) + ' ' + w + 'w').join(', '), sizes: '100vw' }),
      renderImg('tower-dusk-wide', '100vw', t('nr.render.alt'), true));
  }
  // Installment terms of the developer (GET /buildings → installment); null when the developer has set none.
  function siteTerms(b) {
    const x = b && b.installment;
    if (!x || (x.downPaymentMinPct == null && x.maxMonths == null && !x.noOverpayment)) return null;
    return { down: x.downPaymentMinPct == null ? null : Number(x.downPaymentMinPct), months: x.maxMonths == null ? null : Number(x.maxMonths), zero: !!x.noOverpayment };
  }
  // «до 10 лет» / «до 18 месяцев»: whole years when the months divide by 12.
  function termText(months) {
    return months % 12 === 0 ? counted(months / 12, 'plan.years') : counted(months, 'nr.months');
  }
  // Elements of the static first paint (index.html, apartments.html), taken while they are still in the page.
  const SHELL = {
    heroImg: document.querySelector('[data-shell] ~ #view .nr-hero-media picture, #app .nr-hero-media picture, [data-shell] ~ #view .nr-hero-media img, #app .nr-hero-media img'),
    catTitle: document.querySelector('#app .nr-cat .nr-page-title'),
    heroTitle: document.querySelector('#app .nr-hero-title'),
    heroSub: document.querySelector('#app .nr-hero-sub'),
  };
  const fromShell = (key) => { const el = SHELL[key]; SHELL[key] = null; return el; };
  const kicker = (text) => h('div', { class: 'nr-kicker' }, h('span', { class: 'nr-kicker-line', 'aria-hidden': 'true' }), text);
  const arrow = () => { const s = landIcon('arrow'); s.classList.add('nr-arrow'); return s; };

  S.views.home = function (root) {
    const c = contacts();
    // state: 'loading' → 'ready' (a building with apartments) | 'empty' (no building or no apartments yet) | 'error'.
    const d = { state: 'loading', b: null, apts: [], types: [] };
    let alive = true;
    const page = h('div', { class: 'page-in nr nr-home' });
    root.appendChild(page);

    // 1. Hero: the render of the 3D model on the whole width, the name, two actions and a band of live numbers.
    const heroKicker = h('div', { class: 'nr-kicker nr-kicker--light' });
    const heroTitle = fromShell('heroTitle') || h('h1', { class: 'nr-hero-title' });
    const heroSub = fromShell('heroSub') || h('p', { class: 'nr-hero-sub' });
    heroSub.textContent = t('nr.hero.sub');
    const heroStats = h('dl', { class: 'nr-hero-stats', hidden: true });
    // A demo building (isDemo): its numbers are marked as demo data right under the buttons (A-04).
    const heroDemo = h('p', { class: 'nr-hero-demo', hidden: true }, h('span', { class: 'demo-badge' }, t('demo.badge')), ' ', t('nr.hero.demoNote'));
    const hero = h('section', { class: 'nr-hero nr-dark', id: 'top' },
      h('figure', { class: 'nr-hero-media' }, fromShell('heroImg') || heroPicture(),
        h('figcaption', { class: 'nr-render-note' }, t('nr.render'))),
      h('div', { class: 'nr-hero-shade', 'aria-hidden': 'true' }),
      h('div', { class: 'nr-wrap nr-hero-in' },
        h('div', { class: 'nr-hero-text' }, heroKicker, heroTitle, heroSub,
          h('div', { class: 'nr-actions' },
            h('a', { class: 'btn nr-btn nr-btn--primary', href: '/apartments' }, t('wow.hero.pick'), arrow()),
            h('a', { class: 'btn nr-btn nr-btn--light', href: '/apartments#3d' }, t('wow.hero.3d'))), heroDemo),
        heroStats));

    // 2. About the house: the description, amenities and the eco passport from the API; the render on the right.
    const aboutText = h('p', { class: 'nr-lead' });
    const amenList = h('ul', { class: 'nr-amen' });
    const ecoHost = h('div', { class: 'nr-eco' });
    const about = h('section', { class: 'nr-sec', id: 'about' }, h('div', { class: 'nr-wrap nr-split' },
      h('div', { class: 'reveal' }, kicker(t('about.kicker')), h('h2', { class: 'nr-h2' }, t('nr.about.title')), aboutText, amenList, ecoHost),
      h('figure', { class: 'nr-figure reveal' }, renderImg('tower-dusk-tall', '(max-width: 900px) 100vw, 600px', t('nr.render.alt')),
        h('figcaption', { class: 'nr-figure-cap' }, t('nr.render') + ' · ' + t('nr.render.dusk')))));

    // 3. Dark band: what the platform really gives the buyer (no claims about the developer that the data does not hold).
    const whyGrid = h('div', { class: 'nr-why-grid' });
    const why = h('section', { class: 'nr-sec nr-dark nr-why', id: 'why' }, h('div', { class: 'nr-wrap' },
      kicker(t('nr.why.kicker')), h('h2', { class: 'nr-h2' }, t('why.sub')), whyGrid));

    // 4. Layouts: one card per apartment type of the house (areas and prices from the apartments).
    const layoutGrid = h('div', { class: 'nr-layouts' });
    const layouts = h('section', { class: 'nr-sec', id: 'layouts', hidden: true }, h('div', { class: 'nr-wrap' },
      h('div', { class: 'nr-head-row' }, h('div', null, kicker(t('nr.layouts.kicker')), h('h2', { class: 'nr-h2' }, t('layouts.title'))),
        h('p', { class: 'nr-head-note' }, t('layouts.sub'))),
      layoutGrid));

    // 5. What the house looks like: three renders of the 3D model (not photos), each signed.
    const shot = (name, key, cls) => h('figure', { class: 'nr-shot ' + (cls || '') },
      renderImg(name, cls ? '(max-width: 900px) 100vw, 760px' : '(max-width: 900px) 100vw, 520px', t('nr.render.alt') + ' — ' + t(key)),
      h('figcaption', null, t(key)));
    const visuals = h('section', { class: 'nr-sec nr-sec--tight', id: 'visuals' }, h('div', { class: 'nr-wrap' },
      h('div', { class: 'nr-head-row' }, h('div', null, kicker(t('nr.vis.kicker')), h('h2', { class: 'nr-h2' }, t('nr.vis.title'))),
        h('a', { class: 'nr-link', href: '/gallery' }, t('nr.vis.all'), arrow())),
      h('div', { class: 'nr-shots' }, shot('tower-day-tall', 'nr.render.day', 'nr-shot--big'), shot('tower-night-tall', 'nr.render.night'), shot('tower-dusk-tall', 'nr.render.dusk')),
      h('p', { class: 'nr-note' }, t('nr.vis.note'))));

    // 6. Installment: the developer's terms and the calculator over the real prices; hidden without terms or apartments.
    const planHost = h('div', { class: 'nr-wrap nr-split nr-split--top' });
    const plan = h('section', { class: 'nr-sec nr-band', id: 'installment', hidden: true }, planHost);

    // 7. How to buy.
    const steps = h('section', { class: 'nr-sec', id: 'steps' }, h('div', { class: 'nr-wrap' },
      kicker(t('nr.steps.kicker')), h('h2', { class: 'nr-h2' }, t('nr.steps.title')),
      h('ol', { class: 'nr-steps' }, [1, 2, 3].map((n) => h('li', { class: 'reveal' },
        h('span', { class: 'nr-step-no', 'aria-hidden': 'true' }, '0' + n), h('h3', null, t('steps.' + n)), h('p', null, t('steps.' + n + '.d')))))));

    // 8. Contacts and a call-back form (dark).
    const callBox = h('div', { class: 'nr-call' });
    const contactBand = h('section', { class: 'nr-sec nr-dark nr-contact', id: 'contact' }, h('div', { class: 'nr-wrap nr-split' },
      h('div', null, kicker(t('nr.contact.kicker')), h('h2', { class: 'nr-h2' }, t('cta.title')), h('p', { class: 'nr-lead' }, t('cta.sub')),
        h('ul', { class: 'nr-contact-lines' },
          c.address ? h('li', null, landIcon('pin'), h('span', null, c.address)) : null,
          c.phone ? h('li', null, landIcon('phone'), h('a', { href: 'tel:+' + phoneDigits(c.phone) }, c.phone)) : null,
          c.telegram ? h('li', null, landIcon('send'), h('a', { href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, c.telegram)) : null)),
      callBox));

    const soonHost = h('div', { class: 'nr-wrap nr-soon', hidden: true });
    // «Зелёный эффект Binosoz» — real counters from the API; hidden when there is nothing to show.
    const green = S.eco ? S.eco.impactSection() : null;
    page.append(hero, soonHost, about, why, layouts, visuals, green, plan, steps, contactBand, siteFooter());
    renderData(); renderCall();
    const stopReveal = revealIn(page);

    function renderData() {
      const b = d.b, ready = d.state === 'ready';
      const year = b && b.completionYear;
      heroKicker.replaceChildren(h('span', { class: 'nr-kicker-line', 'aria-hidden': 'true' }), year ? t('nr.hero.kicker', { y: year }) : t('nr.hero.city'));
      heroTitle.textContent = S.brandName(b && b.name);
      // Live numbers: floors, apartments, free now, year of completion — each only when the API has it.
      const free = d.apts.filter((a) => a.status === 'free').length;
      const stat = (v, label) => h('div', { class: 'nr-stat' }, h('dt', null, label), h('dd', null, v));
      const stats = ready ? [
        b.floorsCount ? stat(S.fmt.n(b.floorsCount), plural(b.floorsCount, 'nr.floors')) : null,
        stat(S.fmt.n(d.apts.length), plural(d.apts.length, 'nr.apts')),
        stat(S.fmt.n(free), t('nr.freeNow')),
        // Without construction stages with progress the year is only the plan (A-04).
        year ? stat(String(year), t(d.planned ? 'nr.yearPlanned' : 'nr.yearDone')) : null,
      ].filter(Boolean) : [];
      // While loading: four empty cells keep the band's height, so nothing below jumps when the numbers arrive.
      const wait = d.state === 'loading';
      heroStats.replaceChildren(...(wait ? [0, 1, 2, 3].map(() => h('div', { class: 'nr-stat is-wait', 'aria-hidden': 'true' }, h('dt', null, ' '), h('dd', null, ' '))) : stats));
      heroStats.hidden = !wait && !stats.length;
      heroDemo.hidden = !(ready && b.isDemo);

      // No building or no apartments: one card instead of the house blocks.
      const none = d.state === 'empty' || d.state === 'error';
      soonHost.hidden = !none;
      soonHost.replaceChildren(none ? soonCard(d.state, () => { d.state = 'loading'; renderData(); load(); }) : '');
      // Shown while loading too (the render keeps its height): hidden only when there is no building at all.
      about.hidden = d.state === 'empty' || d.state === 'error';
      aboutText.textContent = S.localText(b, 'description');
      aboutText.hidden = !aboutText.textContent;
      amenList.replaceChildren(...((b && b.amenities) || []).map((k) => h('li', null, landIcon(AMEN_ICON[k] || 'check'), h('span', null, t('amen.' + k)))));
      amenList.hidden = !amenList.children.length;
      ecoHost.replaceChildren((b && S.eco && S.eco.block(b.eco, { compact: true, demo: b.isDemo })) || '');

      const terms = siteTerms(b);
      const feats = [
        ['cube', 'nr.why.3d'], ['key', 'nr.why.booking'],
        terms ? ['percent', 'nr.why.plan', termsLine(terms)] : null,
        ['screen', 'nr.why.cabinet'], ['chat', 'nr.why.chat'], ['clipboard', 'nr.why.prices'],
      ].filter(Boolean);
      whyGrid.replaceChildren(...feats.map(([ic, k, d2]) => h('article', { class: 'nr-why-item reveal' },
        h('span', { class: 'nr-why-ic' }, landIcon(ic)), h('h3', null, t(k)), h('p', null, d2 || t(k + '.d')))));
      // The cards are rebuilt when the data arrives: the page-wide reveal picks them up (MutationObserver).

      layouts.hidden = !d.types.length;
      layoutGrid.replaceChildren(...d.types.map(layoutCard)); layoutGrid.style.setProperty('--n', String(Math.max(1, d.types.length)));
      renderPlan(terms);
    }
    // «Взнос от 10%, срок до 10 лет, без переплаты» — only the terms the developer has set.
    function termsLine(x) {
      return [x.down != null ? t('nr.terms.down', { n: S.fmt.n(x.down) }) : null, x.months ? t('nr.terms.term', { n: termText(x.months) }) : null,
        x.zero ? t('nr.terms.zero') : null].filter(Boolean).join(', ');
    }
    // A layout card as in the design: the plan drawing, the type, the area range, the price from.
    function layoutCard(x) {
      const area = x.areaFrom === x.areaTo ? S.fmt.n(x.areaFrom) : S.fmt.n(x.areaFrom) + '–' + S.fmt.n(x.areaTo);
      const planCanvas = h('canvas', { class: 'nr-layout-plan', width: 320, height: 210, 'aria-hidden': 'true' });
      drawFloorPlan(planCanvas, x.rooms);
      return h('a', { class: 'nr-layout', href: '/apartments?rooms=' + x.rooms }, h('div', { class: 'nr-layout-pic' }, planCanvas),
        h('div', { class: 'nr-layout-body' },
          h('div', { class: 'nr-layout-top' }, h('span', { class: 'nr-layout-type' }, x.label), h('span', { class: 'nr-layout-area' }, area + ' ' + t('u.m2'))),
          h('div', { class: 'nr-layout-bottom' }, h('span', { class: 'nr-layout-price' }, t('home.chip.from', { p: S.fmt.money(x.priceFrom, 'TJS') })),
            h('span', { class: 'nr-layout-more' }, t('nr.look'), arrow()))));
    }
    // Installment: the terms on the left, on the right the calculator over the prices of the free apartments.
    function renderPlan(terms) {
      const prices = d.apts.filter((a) => a.status === 'free').map((a) => a.price).filter((p) => p > 0);
      plan.hidden = !terms || !prices.length;
      if (plan.hidden) { planHost.replaceChildren(); return; }
      const big = (v, l) => h('div', { class: 'nr-term' }, h('b', null, v), h('span', null, l));
      const left = h('div', null, kicker(t('nr.plan.kicker')), h('h2', { class: 'nr-h2' }, t(terms.zero ? 'nr.plan.titleZero' : 'nr.plan.title')),
        h('p', { class: 'nr-lead' }, t(terms.zero ? 'nr.plan.subZero' : 'nr.plan.sub')),
        h('div', { class: 'nr-terms' },
          terms.down != null ? big(t('plan.downV', { n: S.fmt.n(terms.down) }), t('plan.down')) : null,
          terms.months ? big(termText(terms.months), t('plan.term')) : null,
          terms.zero ? big('0%', t('plan.over')) : null));
      const step = 10000;
      const lo = Math.floor(Math.min(...prices) / step) * step, hi = Math.ceil(Math.max(...prices) / step) * step;
      const minDown = Math.max(0, Math.ceil(terms.down != null ? terms.down : 0));
      const maxYears = terms.months ? Math.max(1, Math.floor(terms.months / 12)) : 10;
      const st = { price: Math.min(hi, Math.max(lo, Math.round((lo + hi) / 2 / step) * step)), down: Math.min(90, Math.max(minDown, 20)), years: Math.min(maxYears, 5) };
      const out = { price: h('b'), down: h('b'), years: h('b'), monthly: h('span', { class: 'nr-calc-sum' }), total: h('p', { class: 'nr-calc-note' }) };
      const range = (id, label, min, max, stp, key) => {
        const inp = h('input', { id, type: 'range', min, max, step: stp, value: st[key] });
        inp.addEventListener('input', () => { st[key] = Number(inp.value); draw(); });
        return h('div', { class: 'nr-range' }, h('div', { class: 'nr-range-top' }, h('label', { for: id }, label), out[key]), inp);
      };
      function draw() {
        const down = Math.round(st.price * st.down / 100), months = st.years * 12;
        out.price.textContent = S.fmt.money(st.price, 'TJS');
        out.down.textContent = st.down + '% · ' + S.fmt.money(down, 'TJS');
        out.years.textContent = counted(st.years, 'plan.years');
        out.monthly.textContent = S.fmt.money(Math.round((st.price - down) / months), 'TJS');
        out.total.textContent = terms.zero ? t('nr.calc.totalZero', { s: S.fmt.money(st.price, 'TJS') }) : t('nr.calc.totalAsk');
      }
      const calc = h('div', { class: 'nr-calc', id: 'calc' }, h('div', { class: 'nr-calc-title' }, t('nr.calc.title')),
        range('nr-price', t('nr.calc.price'), lo, hi, step, 'price'),
        range('nr-down', t('plan.down'), minDown, 90, 5, 'down'),
        range('nr-years', t('plan.term'), 1, maxYears, 1, 'years'),
        h('div', { class: 'nr-calc-out nr-dark' }, h('span', { class: 'nr-calc-label' }, t('nr.calc.monthly')), out.monthly, out.total),
        h('a', { class: 'btn nr-btn nr-btn--primary', href: '/apartments#calc' }, t('nr.calc.pick'), arrow()));
      draw();
      planHost.replaceChildren(left, calc);
    }

    function renderCall() {
      const phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel', id: 'nr-cb-phone' });
      const btn = h('button', { class: 'btn nr-btn nr-btn--primary block', type: 'submit' }, t('cta.call'));
      const form = h('form', { class: 'nr-call-form', novalidate: true }, h('h3', null, t('cta.formTitle')),
        h('label', { class: 'nr-field-label', for: 'nr-cb-phone' }, t('f.phone')), phone,
        // The error goes under the field (S.showErrors uses this box instead of putting one above the heading).
        h('div', { class: 'form-err', role: 'alert', hidden: true }), btn, h('small', { class: 'nr-micro' }, t('cta.micro')), S.privacyNote());
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
        const prog = await S.api.publicProgress(id).catch(() => null);
        if (!alive) return;
        d.b = b; d.apts = apts;
        d.planned = !prog || !prog.stages || !prog.stages.length || !prog.overallPercent;
        d.state = apts.length ? 'ready' : 'empty';
        // Types from the real apartments (S.roomsKind: 4 — four rooms and more): area range of all, price from the free ones (else from all).
        d.types = S.roomsKind.types.map((rooms) => {
          const all = apts.filter((a) => S.roomsKind.group(a.rooms) === rooms);
          if (!all.length) return null;
          const priced = all.filter((a) => a.status === 'free').length ? all.filter((a) => a.status === 'free') : all;
          return { rooms, label: S.roomsKind.groupLabel(rooms, all), areaFrom: Math.floor(Math.min(...all.map((a) => a.area))), areaTo: Math.ceil(Math.max(...all.map((a) => a.area))), priceFrom: Math.min(...priced.map((a) => a.price)) };
        }).filter(Boolean);
        renderData();
      } catch (e) { if (alive) { d.state = 'error'; renderData(); } }
    }
    load();
    return () => { alive = false; stopReveal(); };
  };
  S.siteTerms = siteTerms;

  // A drawn check mark with a message: the end state of the forms.
  function successBlock(text, again) {
    return h('div', { class: 'success-box', role: 'status' },
      h('div', { class: 'success-ic', html: '<svg viewBox="0 0 52 52" aria-hidden="true"><circle cx="26" cy="26" r="24"/><path d="m15 27 7.5 7.5L38 19"/></svg>' }),
      h('b', null, t('ok.title')), h('p', null, text), again || null);
  }

  /* ---------- contact page ---------- */
  S.views.contact = function (root) {
    const c = contacts();
    const page = h('div', { class: 'page-in nr contact-page' });
    root.appendChild(page);
    const head = h('section', { class: 'nr-pagehead' }, h('div', { class: 'nr-wrap' },
      h('nav', { class: 'nr-crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.contact'))),
      h('h1', { class: 'nr-page-title' }, t('ct.title')), h('p', { class: 'nr-page-sub' }, t('ct.sub'))));

    const line = (ic, label, ...value) => h('div', { class: 'ct-card' }, h('span', { class: 'ct-ic' }, landIcon(ic)), h('div', null, h('small', null, label), h('div', { class: 'ct-val' }, ...value)));
    const schedule = c.schedule || [];
    const left = h('div', { class: 'ct-left' },
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
      // Not role="img": the card holds a link (axe nested-interactive, A-18); the address is read as text, the pin is decoration.
      h('div', { class: 'ct-map' },
        h('div', { class: 'ct-map-pin', 'aria-hidden': 'true' }, landIcon('pin')), h('b', null, t('ct.city') + ', ' + (c.address || '').replace(/^(г\.\s*|ш\.\s*)?Душанбе,\s*/, '').replace(/,\s*Dushanbe$/, '')),
        h('a', { class: 'btn nr-btn nr-btn--primary', href: 'https://www.openstreetmap.org/search?query=' + encodeURIComponent(c.address || ''), target: '_blank', rel: 'noopener' }, t('ct.route'), arrow())));

    // Above the form — straight to the messengers; under it — why it is safe to write.
    const formHost = h('div');
    const right = h('div', { class: 'ct-right' }, h('div', { class: 'ct-form-wrap' },
      (c.telegram || c.phone) ? h('div', { class: 'ct-direct' }, h('span', null, t('ct.direct')), h('div', { class: 'ct-buttons' },
        c.telegram ? h('a', { class: 'nr-link', href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, landIcon('send'), 'Telegram') : null,
        c.phone ? h('a', { class: 'nr-link', href: 'https://wa.me/' + phoneDigits(c.phone), target: '_blank', rel: 'noopener' }, landIcon('chat'), 'WhatsApp') : null)) : null,
      formHost,
      h('ul', { class: 'trust' }, [['lock', 'ct.trust1'], ['check', 'ct.trust2'], ['clipboard', 'ct.trust3']].map(([ic, k]) => h('li', null, landIcon(ic), t(k))))));
    // FAQ: one answer open at a time. The contacts come from config.js; the down payment, the term and the delivery year
    // only from the API (the developer's installment terms and the building) — without them that question is not shown.
    const vars = { phone: c.phone || '', tg: c.telegram || '' };
    const faqList = h('div', { class: 'faq-list' });
    const drawFaq = (b) => {
      const x = siteTerms(b);
      if (x && x.down != null) { vars.down = S.fmt.n(x.down); vars.term = x.months ? t('faq.down.term', { t: termText(x.months) }) : ''; vars.zero = x.zero ? t('faq.down.zero') : ''; }
      if (b && b.completionYear) vars.year = b.completionYear;
      const keys = FAQ.filter((k) => (k !== 'down' || vars.down != null) && (k !== 'delivery' || vars.year));
      faqList.replaceChildren(...keys.map(faqItem));
    };
    const faqItem = (k, i) => {
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
    };
    drawFaq(null);
    const faq = h('section', { class: 'nr-sec nr-band', id: 'faq' }, h('div', { class: 'nr-wrap nr-faq-wrap' },
      kicker(t('nr.faq.kicker')), h('h2', { class: 'nr-h2' }, t('faq.title')), faqList,
      h('p', { class: 'nr-note' }, t('faq.sub'))));
    page.append(head, h('section', { class: 'nr-sec nr-ct' }, h('div', { class: 'nr-wrap ct-split' }, left, right)), faq, siteFooter());

    // Floating labels: the label sits in the field and moves up when the field has focus or a value.
    const fl = (field, label, req) => h('label', { class: 'fl' }, field, h('span', null, label + (req ? ' *' : '')), h('em', { class: 'fl-err' }));
    function renderForm() {
      const name = h('input', { class: 'inp', name: 'name', placeholder: ' ', autocomplete: 'name', maxlength: 200 });
      const phone = h('input', { class: 'inp', name: 'phone', type: 'tel', placeholder: ' ', value: '+992', autocomplete: 'tel' });
      const kind = h('select', { class: 'inp', name: 'kind' }, ['buy', 'plan', 'other'].map((k) => h('option', { value: k }, t('ct.kind.' + k))));
      const when = h('select', { class: 'inp', name: 'when' }, ['morning', 'day', 'evening'].map((k) => h('option', { value: k }, t('ct.when.' + k))));
      const cm = h('textarea', { class: 'inp', name: 'comment', placeholder: ' ', maxlength: 800, rows: 4 });
      const btn = h('button', { class: 'btn nr-btn nr-btn--primary block', type: 'submit' }, t('ct.send'));
      const form = h('form', { class: 'ct-form', novalidate: true },
        h('h2', { class: 'nr-form-title' }, t('ct.formTitle')),
        fl(name, t('f.name'), true), fl(phone, t('f.phone'), true),
        h('div', { class: 'fl-row' }, h('label', { class: 'fl fixed' }, kind, h('span', null, t('ct.kind'))), h('label', { class: 'fl fixed' }, when, h('span', null, t('ct.when')))),
        fl(cm, t('f.comment')),
        btn, S.privacyNote());
      const fail = (el, msg) => { const box = el.closest('label'); box.classList.add('err'); box.querySelector('.fl-err').textContent = msg; };
      form.addEventListener('input', (e) => { const box = e.target.closest('label'); if (box) box.classList.remove('err'); });
      form.addEventListener('submit', async (e) => {
        e.preventDefault(); S.showErrors(form, null);
        S.$$('label.err', form).forEach((l) => l.classList.remove('err'));
        const ph = phone.value.replace(/[\s()-]/g, '');
        let ok = true;
        if (!name.value.trim()) { fail(name, t('ct.errName')); ok = false; }
        if (!/^\+992\d{9}$/.test(ph)) { fail(phone, t('ct.errPhone')); ok = false; }
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
    let alive = true;
    siteBuilding().then((id) => (id ? S.api.building(id) : null)).then((b) => { if (alive && b) drawFaq(b); }).catch(() => {});
    return () => { alive = false; };
  };

  /* ---------- register: a developer's application to join the platform ---------- */
  // "+992 98 363 70 70" as the person types; 9 digits without the country code are taken as a Tajik number.
  const formatPhone = (v) => {
    const d = phoneDigits(v).replace(/^992/, '').slice(0, 9);
    const parts = [d.slice(0, 2), d.slice(2, 5), d.slice(5, 7), d.slice(7, 9)].filter(Boolean);
    return '+992' + (parts.length ? ' ' + parts.join(' ') : '');
  };
  const validPhone = (v) => { const d = phoneDigits(v); return /^992\d{9}$/.test(d) || /^\d{9}$/.test(d); };
  // The booking and question forms check the name and the phone before sending, like the contact form (A-17):
  // the errors appear under the fields, the server is not asked with an empty form.
  const namePhoneOk = (form, name, phone) => {
    const errors = {};
    if (name.value.trim().length < 2) errors.name = [t('ct.errName')];
    if (!validPhone(phone.value)) errors.phone = [t('ct.errPhone')];
    if (!Object.keys(errors).length) return true;
    S.showErrors(form, { errors, message: t('ct.errPhone') });
    (errors.name ? name : phone).focus();
    return false;
  };

  S.views.register = function (root) {
    // Design docs/design/Register.dc.html: on the left a dark column with what the platform gives, on the right the form.
    const page = h('div', { class: 'page-in nr reg-page' });
    root.appendChild(page);
    const card = h('div', { class: 'reg-card' });
    const perks = [['cube', 'reg2.perk1'], ['clipboard', 'reg2.perk2'], ['screen', 'reg2.perk3']];
    const aside = h('aside', { class: 'reg-aside nr-dark' },
      h('div', { class: 'nr-kicker' }, h('span', { class: 'nr-kicker-line', 'aria-hidden': 'true' }), 'BINOSOZ'),
      h('h1', { class: 'reg-title' }, t('reg2.title')), h('p', { class: 'reg-lead' }, t('reg2.sub')),
      h('ul', { class: 'reg-perks' }, perks.map(([ic, k]) => h('li', null, h('span', { class: 'reg-perk-ic' }, landIcon(ic)), h('div', null, h('b', null, t(k)), h('span', null, t(k + '.d')))))));
    page.append(h('section', { class: 'reg-split' }, aside, h('div', { class: 'reg-main' }, card)), siteFooter());

    const fl = (field, label, req) => h('label', { class: 'fl' }, field, h('span', null, label + (req ? ' *' : '')), h('em', { class: 'fl-err' }));
    const company = h('input', { class: 'inp', name: 'companyName', placeholder: ' ', maxlength: 200, autocomplete: 'organization' });
    const owner = h('input', { class: 'inp', name: 'ownerFullName', placeholder: ' ', maxlength: 200, autocomplete: 'name' });
    const email = h('input', { class: 'inp', name: 'contactEmail', type: 'email', placeholder: ' ', maxlength: 256, autocomplete: 'email' });
    const phone = h('input', { class: 'inp', name: 'contactPhone', type: 'tel', placeholder: ' ', value: '+992', autocomplete: 'tel' });
    const comment = h('textarea', { class: 'inp', name: 'comment', placeholder: ' ', maxlength: 1000, rows: 4 });
    phone.addEventListener('input', () => { phone.value = formatPhone(phone.value); });
    const btn = h('button', { class: 'btn nr-btn nr-btn--primary block', type: 'submit' }, t('reg2.send'));
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

    card.append(h('h2', { class: 'nr-form-title' }, t('reg2.formTitle')), form,
      h('p', { class: 'reg-have' }, t('reg2.have') + ' ', h('a', { href: '/login' }, t('nav.login'))));
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
  const PLAN_TYPES = [0, 1, 2, 3, 4];
  // The price of a square metre the buyer actually pays: price / area, rounded to a somoni (A-27). The stored price per m²
  // can differ from it when the total price was rounded; without an area or a price — the stored one.
  const sqmPrice = (a) => (a.area > 0 && a.price > 0 ? Math.round(a.price / a.area) : a.pricePerSqm);
  // An architect's plan as in the design: walls in the ink colour on the paper of the card, windows and the balcony in copper,
  // doors as gaps with a thin swing, room names in the middle. The rooms, doors and windows come from S.plans
  // (planirovki-data.js) — the same layout the 3D model builds inside; with the area the plan is scaled to it.
  // Colours come from the page tokens, so the plan follows the light and the dark theme; sharp on high-density screens.
  function drawFloorPlan(canvas, type, area) {
    const W = +canvas.getAttribute('width'), H = +canvas.getAttribute('height'), dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = W * dpr; canvas.height = H * dpr;
    const g = canvas.getContext('2d'); if (!g || !S.plans) return;
    g.scale(dpr, dpr);
    const css = getComputedStyle(canvas.isConnected ? canvas : document.documentElement);
    const tok = (n, f) => (css.getPropertyValue(n) || '').trim() || f;
    const paper = tok('--surface', '#fbf8f2'), room = tok('--bg', '#f4efe7'), ink = tok('--ink', '#1b1815'), copper = tok('--accent-text', '#8a4e2a');
    g.fillStyle = paper; g.fillRect(0, 0, W, H);
    const lay = S.plans.layout(type, area);
    const top = lay.balcony.y, spanY = lay.d - top, pad = 12;
    const s = Math.min((W - pad * 2) / lay.w, (H - pad * 2) / spanY);
    const ox = (W - lay.w * s) / 2, oy = (H - spanY * s) / 2 - top * s;
    const X = (x) => ox + x * s, Y = (y) => oy + y * s;
    g.font = '600 10.5px Manrope, system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
    lay.rooms.forEach((r) => { g.fillStyle = room; g.fillRect(X(r.x), Y(r.y), r.w * s, r.h * s); });
    // The balcony: a dashed copper outline, as in the design.
    const b = lay.balcony;
    g.setLineDash([5, 4]); g.strokeStyle = copper; g.lineWidth = 1.5; g.strokeRect(X(b.x) + 1, Y(b.y) + 1, b.w * s - 2, b.h * s - 1); g.setLineDash([]);
    g.strokeStyle = ink; g.lineWidth = 2.5; g.lineCap = 'square';
    S.plans.walls(lay).forEach(([x1, y1, x2, y2]) => { g.beginPath(); g.moveTo(X(x1), Y(y1)); g.lineTo(X(x2), Y(y2)); g.stroke(); });
    // Door swings: a quarter circle from one side of the opening.
    g.lineWidth = 0.8; g.lineCap = 'butt';
    lay.doors.forEach((d) => {
      if (d.open) return;
      const r = (d.to - d.from) * s;
      g.beginPath();
      if (d.axis === 'x') { g.moveTo(X(d.from), Y(d.at)); g.arc(X(d.from), Y(d.at), r, 0, d.entrance ? -Math.PI / 2 : Math.PI / 2, d.entrance); }
      else { g.moveTo(X(d.at), Y(d.from)); g.arc(X(d.at), Y(d.from), r, Math.PI / 2, 0, true); }
      g.stroke();
    });
    // Windows: thick copper strokes on the facade.
    g.strokeStyle = copper; g.lineWidth = 4;
    lay.windows.forEach((w) => { g.beginPath(); g.moveTo(X(w.from), Y(0)); g.lineTo(X(w.to), Y(0)); g.stroke(); });
    const fit = (label, ww) => (label.length * 6 > ww - 6 ? label.slice(0, Math.max(3, Math.floor((ww - 6) / 6) - 1)) + '.' : label);
    lay.rooms.forEach((r) => {
      if (r.part) return;
      const ww = r.w * s, hh = r.h * s;
      g.fillStyle = ink;
      if (ww > 30 && hh > 18) g.fillText(fit(t('plans.room.' + r.key), ww), X(r.x) + ww / 2, Y(r.y) + hh / 2);
    });
    if (b.w * s > 36 && b.h * s > 12) { g.fillStyle = copper; g.fillText(fit(t('plans.room.balcony'), b.w * s), X(b.x) + b.w * s / 2, Y(b.y) + b.h * s / 2); }
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
  S.site = { footer: siteFooter, reveal: revealIn, icon: landIcon, accentFirst, contacts, drawFloorPlan, building: siteBuilding, galleryPhotos: GALLERY_PHOTOS, galleryImg, galleryCredit, renderImg, kicker: (x) => kicker(x), arrow: () => arrow(), amenIcon: (k) => AMEN_ICON[k] || 'check' };
})();
