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
  const GALLERY = ['facade', 'living', 'playground', 'view'];
  // Example reviews (key, stars): the section says they are examples until real buyers write theirs.
  const REVIEWS = [['r1', 5], ['r2', 5], ['r3', 4]];
  const FAQ = ['visit', 'down', 'delivery', 'booking', 'fees'];
  const AMENITIES = [['car', 'about.parking'], ['play', 'about.playground'], ['shield', 'about.guard'], ['leaf', 'about.green']];
  function landIcon(name) {
    const s = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    s.setAttribute('viewBox', '0 0 24 24'); s.setAttribute('fill', 'none'); s.setAttribute('stroke', 'currentColor'); s.setAttribute('stroke-width', '1.8');
    s.setAttribute('stroke-linecap', 'round'); s.setAttribute('stroke-linejoin', 'round'); s.setAttribute('aria-hidden', 'true');
    s.innerHTML = LAND_ICONS[name];
    return s;
  }
  // Section headings: the first word in the accent colour; a short one («Для», «О») takes the next word with it.
  function accentFirst(text) {
    const words = String(text).split(' ');
    const n = words[0].length <= 3 && words.length > 1 ? 2 : 1;
    const head = words.slice(0, n).join(' '), rest = words.slice(n).join(' ');
    return rest ? [h('span', { class: 'accent-word' }, head), ' ' + rest] : h('span', { class: 'accent-word' }, head);
  }
  // "16 этажей", "21 этаж", "71 квартира": the i18n value holds the forms as "one|few|many" (Tajik has one form).
  function plural(n, key) {
    const f = t(key).split('|');
    if (f.length < 3) return f[0];
    const m10 = n % 10, m100 = n % 100;
    return m10 === 1 && m100 !== 11 ? f[0] : m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14) ? f[1] : f[2];
  }
  const counted = (n, key) => S.fmt.n(n) + ' ' + plural(n, key);
  const contacts = () => (window.SITORA_CONFIG || {}).CONTACT || {};
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
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) { show(1); return; }
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
    if (!('IntersectionObserver' in window)) { els.forEach(show); return () => {}; }
    const io = new IntersectionObserver((entries) => entries.forEach((e) => { if (e.isIntersecting) { show(e.target); io.unobserve(e.target); } }), { threshold: 0.12 });
    els.forEach((el) => io.observe(el));
    return () => io.disconnect();
  }

  function siteFooter() {
    const c = contacts();
    const col = (title, ...items) => h('div', { class: 'foot-col' }, h('h4', null, title), ...items);
    return h('footer', { class: 'footer lux-footer' }, h('div', { class: 'wrap' },
      h('div', { class: 'foot-grid' },
        h('div', { class: 'foot-brand' }, h('a', { class: 'row', href: '/', style: { gap: '10px' } }, S.logo(), h('b', null, 'Sitora Residence')), h('p', null, t('foot.tagline')),
          h('div', { class: 'foot-social' },
            c.telegram ? h('a', { class: 'soc tg', href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener', 'aria-label': 'Telegram', title: 'Telegram' }, landIcon('send')) : null,
            c.phone ? h('a', { class: 'soc wa', href: 'https://wa.me/' + phoneDigits(c.phone), target: '_blank', rel: 'noopener', 'aria-label': 'WhatsApp', title: 'WhatsApp' }, landIcon('chat')) : null,
            h('a', { class: 'btn ghost sm', href: '/login' }, t('foot.login'))),
          h('p', { class: 'foot-dev' }, t('foot.dev') + ' ', h('a', { href: '/register' }, t('foot.devLink')))),
        col(t('foot.apts'), h('a', { href: '/apartments' }, t('foot.catalog')), h('a', { href: '/apartments#calc' }, t('foot.calc')), h('a', { href: '/apartments' }, t('foot.3d'))),
        col(t('foot.info'), h('a', { href: '/#why' }, t('foot.why')), h('a', { href: '/#steps' }, t('foot.steps')), h('a', { href: '/login' }, t('foot.cabinet'))),
        col(t('foot.contacts'), h('span', null, c.address || ''), c.phone ? h('a', { href: 'tel:+' + phoneDigits(c.phone) }, c.phone) : null, c.telegram ? h('a', { href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, c.telegram) : null, h('a', { href: '/contact' }, t('foot.write')))),
      h('div', { class: 'foot-bottom' }, h('span', null, '© ' + new Date().getFullYear() + ' Sitora Residence. ' + t('footer.rights')), h('a', { href: '/login' }, t('foot.login')))));
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
    page.appendChild(h('section', { class: 'cat-hero' }, h('div', { class: 'wrap cat-hero-in' },
      h('div', { class: 'cat-hero-text' },
        h('nav', { class: 'crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.apartments'))),
        h('h1', null, accentFirst(t('cat.title'))), h('p', null, t('cat.sub'))),
      (cc.phone || cc.telegram) ? h('div', { class: 'cat-hero-contact' },
        cc.phone ? h('a', { class: 'cat-call', href: 'tel:+' + phoneDigits(cc.phone) }, h('span', { class: 'ct-ic' }, landIcon('phone')), h('span', null, h('small', null, t('home.call')), h('b', null, cc.phone))) : null,
        cc.telegram ? h('a', { class: 'btn gold', href: telegramUrl(cc.telegram), target: '_blank', rel: 'noopener' }, landIcon('send'), 'Telegram') : null) : null)));

    const houseBox = h('div', { class: 'house3d', id: 'house3d' });
    const tip = h('div', { class: 'tip', hidden: true });
    const sheetHost = h('div');
    const floorbar = h('div', { class: 'floorbar glass pe', role: 'group', 'aria-label': t('hero.floors') });
    const heroTitle = h('div', { class: 'hero-title' });
    const stats = h('div', { class: 'stat-strip pe' });
    const loading = h('div', { class: 'hero-loading', style: { position: 'absolute', inset: 0, zIndex: 3, display: 'grid', placeItems: 'center', color: '#c8d1f0', fontWeight: 700, background: '#0a1228' } }, t('hero.loading'));

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
    page.append(aptSection, calcSection, siteFooter());

    // ---------- data ----------
    async function load() {
      try {
        const bs = await S.api.buildings();
        if (!bs.length) throw new Error(t('err.noBuildings'));
        st.building = bs.find((b) => String(b.id) === String(cfg.DEFAULT_BUILDING_ID)) || bs[0];
        S.siteBuildingId = st.building.id;
        const full = await S.api.building(st.building.id).catch(() => st.building);
        st.building = Object.assign({}, st.building, full);
        st.floors = await S.api.floors(st.building.id);
        const [apts, rate] = await Promise.all([S.api.allApartments(st.floors), S.api.rate().catch(() => null)]);
        st.apts = apts; st.rate = rate && rate.rate ? rate : null;
        st.maxPrice = Math.max(...apts.map((a) => a.price), 0);
        st.priceCap = st.maxPrice;
      } catch (e) {
        if (!alive) return;
        loading.replaceChildren(h('div', { class: 'card', style: { maxWidth: '460px', background: 'rgba(255,255,255,.08)', color: '#fff' } }, h('h3', null, t('err.loadTitle')), h('p', { style: { margin: '8px 0 14px' } }, e.message), h('button', { class: 'btn', onclick: () => { loading.replaceChildren(t('hero.loading')); load(); } }, t('common.retry')), h('button', { class: 'btn ghost', style: { marginLeft: '8px', color: '#fff' }, onclick: () => S.openSettings() }, t('settings.title'))));
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
      pushToHouse();
      void floors;
    }
    function pushToHouse() { if (!house) return; house.setData(st.apts.map((a) => ({ id: a.id, floor: a.floor, planSlot: a.planSlot, number: a.number, status: a.status, rooms: a.rooms, area: a.area, price: a.price }))); if (st.sel) house.selectApartment(st.sel.id); else if (st.floor !== null) house.selectFloor(st.floor); }

    // ---------- render ----------
    function renderAll() { renderHero(); renderFloors(); renderSheet(); renderApartments(); renderCalc(); }

    function minPrice() { const f = st.apts.filter((a) => a.status === 'free'); return f.length ? Math.min(...f.map((a) => a.price)) : 0; }
    function renderHero() {
      const b = st.building, free = st.apts.filter((a) => a.status === 'free').length;
      heroTitle.replaceChildren(
        h('span', { class: 'eyebrow' }, S.icon('pin'), b.address || t('hero.city')),
        h('h1', null, b.name || 'Sitora Residence'),
        h('p', null, S.lang === 'tg' && b.descriptionTg ? b.descriptionTg : (b.description || t('hero.sub'))),
        h('div', { class: 'hero-cta pe' }, h('button', { class: 'btn gold lg', onclick: () => scrollTo('apartments') }, t('hero.cta1')), h('button', { class: 'btn ghost lg', style: { color: '#fff', borderColor: 'rgba(255,255,255,.4)' }, onclick: () => scrollTo('calc') }, t('hero.cta2'))));
      stats.replaceChildren(
        stat(S.fmt.n(free), t('stat.free')), stat(S.fmt.money(minPrice(), 'TJS'), t('stat.from')), stat(b.deliveryDate ? S.fmt.date(b.deliveryDate).slice(3) : '—', t('stat.delivery')));
    }
    const stat = (v, l) => h('div', { class: 'stat glass' }, h('b', null, v), h('span', null, l));

    function renderFloors() {
      const list = st.floors.slice().sort((a, b) => b.number - a.number);
      const free = (n) => st.apts.filter((a) => a.floor === n && a.status === 'free').length;
      floorbar.replaceChildren(h('h4', null, t('hero.floors')), list.map((f) => { const c = free(f.number); return h('button', { class: 'fbtn' + (st.floor === f.number ? ' on' : '') + (c ? '' : ' zero'), title: f.commercial ? t('floor.commercial') : t('floor.n', { n: f.number }) + ', ' + t('floor.free', { n: c }), onclick: () => selectFloor(f.number) }, h('span', null, f.commercial ? 'К' : f.number), h('small', null, f.commercial ? '' : c)); }));
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
      const form = h('form', { class: 'stack', id: 'leadform', novalidate: true }, S.field(t('f.name'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), S.field(t('f.comment'), cm));
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
    function renderCard(a) {
      return h('button', { class: 'card hover apt-card', onclick: () => { scrollTo('home'); setTimeout(() => pickApt(a), 350); }, 'aria-label': t('apt.title', { n: a.number }) },
        h('div', { class: 'top2' }, h('span', { class: 'no' }, '№ ' + a.number), S.statusChip(a.status)),
        h('div', { class: 'meta' }, h('span', null, t('floor.n', { n: a.floor })), h('span', null, S.fmt.rooms(a.rooms)), h('span', null, S.fmt.area(a.area))),
        h('div', null, h('div', { style: { fontFamily: 'var(--display)', fontWeight: 600, fontSize: '19px' } }, S.fmt.money(a.price, 'TJS')), h('div', { class: 'muted', style: { fontSize: '12.5px' } }, S.fmt.n(a.pricePerSqm) + ' TJS / ' + t('u.m2'))),
        h('span', { class: 'row', style: { color: 'var(--accent)', fontWeight: 800, fontSize: '13.5px' } }, t('card.show'), ' →'));
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
    // config.js numbers until the building loads, and when the API is unavailable.
    const d = {
      floors: cfg.DEFAULT_FLOORS || 16, apts: cfg.DEFAULT_APTS || 128, year: cfg.DEFAULT_YEAR || 2026, minPrice: cfg.DEFAULT_MIN_PRICE || 325700, free: null, list: null,
      // Apartment types: { rooms, areaFrom, areaTo, priceFrom }.
      types: (cfg.DEFAULT_TYPES || []).map(([rooms, areaFrom, areaTo, priceFrom]) => ({ rooms, areaFrom, areaTo, priceFrom })),
    };
    const plan = installment();
    let alive = true;
    const page = h('div', { class: 'page-in lux lux-home' });
    root.appendChild(page);
    const rnd = (n) => Math.random() * n;

    // 1. Fullscreen hero: night sky, Tajik mountains, the name of the complex.
    const stars = h('div', { class: 'stars', 'aria-hidden': 'true' }, Array.from({ length: 60 }, () => { const s = (1 + rnd(1.6)).toFixed(1) + 'px'; return h('i', { style: { left: rnd(100).toFixed(1) + '%', top: rnd(62).toFixed(1) + '%', width: s, height: s, animationDelay: rnd(4).toFixed(2) + 's', animationDuration: (2.4 + rnd(3)).toFixed(2) + 's' } }); }));
    const mountains = h('div', { class: 'mountains', 'aria-hidden': 'true', html: '<svg viewBox="0 0 1440 320" preserveAspectRatio="none"><path fill="currentColor" opacity=".55" d="M0 230 120 150l70 40 110-110 90 70 80-50 120 120 90-60 140 90 100-80 120 70 110-100 130 110 80-40 80 50v120H0z"/><path fill="currentColor" d="M0 270 90 220l90 30 120-70 100 60 90-40 130 70 110-50 120 60 100-30 120 50 110-60 120 50 140-20v130H0z"/></svg>' });
    const chips = h('div', { class: 'hero-chips' });
    const proof = h('p', { class: 'hero-proof', hidden: true });
    const freeCard = h('div', { class: 'float-card glass' });
    const hero = h('section', { class: 'lux-hero', id: 'top' }, stars, mountains,
      h('div', { class: 'wrap lux-hero-in' },
        h('div', { class: 'lux-hero-text' },
          h('span', { class: 'lux-pill' }, landIcon('pin'), t('home.place')),
          h('h1', { class: 'lux-title' }, h('span', { class: 'gold-text' }, 'Sitora'), h('br'), 'Residence'),
          h('i', { class: 'gold-line', 'aria-hidden': 'true' }),
          h('p', { class: 'lux-tagline' }, t('home.tagline')),
          chips, proof,
          h('div', { class: 'lux-cta' }, h('a', { class: 'btn gold lg', href: '/apartments' }, t('home.cta1')), h('a', { class: 'btn ghost lg', href: '/apartments#calc' }, t('home.cta2')))),
        h('div', { class: 'lux-hero-side' }, freeCard,
          c.phone ? h('a', { class: 'float-card glass second', href: 'tel:+' + phoneDigits(c.phone) }, landIcon('phone'), h('div', null, h('small', null, t('home.call')), h('b', null, c.phone))) : null,
          c.telegram ? h('a', { class: 'float-card glass tg', href: telegramUrl(c.telegram), target: '_blank', rel: 'noopener' }, landIcon('send'), h('div', null, h('small', null, t('home.tgCard')), h('b', null, c.telegram))) : null)),
      h('button', { class: 'scroll-hint', type: 'button', 'aria-label': t('home.scroll'), onclick: () => document.getElementById('numbers').scrollIntoView({ behavior: 'smooth' }) }, landIcon('down')));

    // 2. Numbers.
    const counters = [['floors', (v) => S.fmt.n(v)], ['apts', (v) => S.fmt.n(v)], ['year', (v) => String(v)], ['zero', (v) => v + '%']]
      .map(([k, fmt]) => ({ k, fmt, el: h('b', null, '0') }));
    const numbers = h('section', { class: 'lux-numbers', id: 'numbers' }, h('div', { class: 'wrap nums-grid reveal' },
      counters.map((x) => h('div', { class: 'num-cell' }, x.el, h('span', null, t('home.num.' + x.k))))));
    const valueOf = (k) => ({ floors: d.floors, apts: d.apts, year: d.year, zero: 0 }[k]);
    const count = () => counters.forEach((x) => countUp(x.el, valueOf(x.k), x.fmt));

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
        h('h2', null, h('span', { class: 'gold-text' }, 'Sitora'), ' Residence'),
        h('p', null, t('about.text')),
        h('ul', { class: 'amenities' }, AMENITIES.map(([ic, k]) => h('li', null, h('span', { class: 'am-ic' }, landIcon(ic)), t(k)))))));

    // Layouts: one card per apartment type.
    const layoutGrid = h('div', { class: 'layout-grid reveal' });
    const layouts = h('section', { class: 'section lux-sec alt-band', id: 'layouts' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('layouts.title'))), h('p', null, t('layouts.sub'))),
      layoutGrid));

    // Installment terms.
    const term = (ic, label, value) => h('div', { class: 'plan-cell' }, h('span', { class: 'plan-ic' }, landIcon(ic)), h('small', null, label), h('b', null, value));
    // Gallery: places for the renders and photos, honest about not having them yet.
    const gallery = h('section', { class: 'section lux-sec', id: 'gallery' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('gal.title'))), h('p', null, t('gal.sub'))),
      h('div', { class: 'gal-grid' }, GALLERY.map((k, i) => h('figure', { class: 'gal-card reveal', style: { transitionDelay: (i % 2) * 90 + 'ms' } },
        h('span', { class: 'gal-ic' }, landIcon('image')), h('figcaption', null, t('gal.' + k))))),
      h('p', { class: 'gal-note' }, t('gal.note')),
      h('div', { class: 'teaser-more' }, h('a', { class: 'btn ghost lg', href: '/apartments' }, t('gal.cta')))));

    // Reviews: example texts, marked as examples.
    const reviews = h('section', { class: 'section lux-sec alt-band', id: 'reviews' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('rev.title'))), h('p', null, t('rev.sub'))),
      h('div', { class: 'rev-grid' }, REVIEWS.map(([k, stars], i) => h('article', { class: 'rev-card reveal', style: { transitionDelay: i * 90 + 'ms' } },
        h('span', { class: 'rev-badge' }, t('rev.example')),
        h('div', { class: 'rev-stars', 'aria-label': t('rev.stars', { n: stars }) }, '★'.repeat(stars) + '☆'.repeat(5 - stars)),
        h('p', { class: 'rev-text' }, t('rev.' + k)),
        h('footer', null, h('b', null, t('rev.' + k + '.who')), h('small', null, t('rev.' + k + '.when'))))))));

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
    const teaser = h('section', { class: 'section lux-sec', id: 'teaser' }, h('div', { class: 'wrap' },
      h('div', { class: 'lux-head reveal' }, h('h2', null, accentFirst(t('teaser.title'))), h('p', null, t('teaser.sub'))),
      aptGrid, h('div', { class: 'teaser-more' }, allBtn)));

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

    page.append(hero, numbers, why, steps, about, layouts, gallery, planBand, reviews, teaser, contactBand, siteFooter());
    renderData(); renderCall();
    const stopReveal = revealIn(page, (el) => { if (el.closest('#numbers')) count(); });

    // Mountains and stars drift a little slower than the page.
    let ticking = false;
    const onScroll = () => { if (ticking) return; ticking = true; requestAnimationFrame(() => { ticking = false; hero.style.setProperty('--py', Math.min(window.scrollY, 900) + 'px'); }); };
    window.addEventListener('scroll', onScroll, { passive: true });

    function renderData() {
      chips.replaceChildren(
        h('span', { class: 'chip-lux' }, counted(d.floors, 'home.chip.floors')),
        h('span', { class: 'chip-lux' }, counted(d.apts, 'home.chip.apts')),
        h('span', { class: 'chip-lux' }, t('home.chip.year', { y: d.year })),
        h('span', { class: 'chip-lux' }, t('home.chip.from', { p: S.fmt.money(d.minPrice, 'TJS') })));
      freeCard.replaceChildren(h('i', { class: 'live-dot' }), h('div', null, h('small', null, t('home.freeNow')), h('b', null, d.free === null ? t('home.freeSome') : counted(d.free, 'home.chip.apts'))));
      allBtn.textContent = t('teaser.all', { n: S.fmt.n(d.apts) });
      aptGrid.replaceChildren(...(d.list ? d.list.map(aptCard) : [0, 1, 2].map(typeCard)));
      // Social proof from the real data: apartments already sold or booked. Hidden until it is known.
      proof.replaceChildren(...(d.taken ? [h('span', { class: 'proof-ic' }, landIcon('users')), t('home.proof', { n: counted(d.taken, 'home.families') })] : []));
      proof.hidden = !d.taken;
      const big = (v, l) => h('div', { class: 'about-num' }, h('b', null, v), h('span', null, l));
      aboutStats.replaceChildren(big(S.fmt.n(d.floors), plural(d.floors, 'about.floors')), big(S.fmt.n(d.apts), plural(d.apts, 'about.apts')),
        big(String(d.types.length || 4), plural(d.types.length || 4, 'about.types')), big(String(d.year), t('about.year')));
      layoutGrid.replaceChildren(...d.types.map(layoutCard));
      if (numbers.querySelector('.reveal.in')) count();
    }
    // A layout card: type, area range, price from; the bed icon shows the number of rooms.
    function layoutCard(x) {
      const beds = Math.max(1, Math.min(3, x.rooms || 1));
      const area = x.areaFrom === x.areaTo ? S.fmt.n(x.areaFrom) : S.fmt.n(x.areaFrom) + '–' + S.fmt.n(x.areaTo);
      return h('a', { class: 'layout-card', href: '/apartments' },
        h('span', { class: 'layout-ic', 'aria-hidden': 'true' }, Array.from({ length: beds }, () => landIcon('bed'))),
        h('b', { class: 'layout-type' }, t('layouts.type' + Math.min(3, x.rooms))),
        h('span', { class: 'layout-area' }, area + ' ' + t('u.m2')),
        h('span', { class: 'layout-price' }, t('home.chip.from', { p: S.fmt.money(x.priceFrom, 'TJS') })),
        h('span', { class: 'apt-lux-more' }, t('layouts.cta')));
    }
    function aptCard(a) {
      return h('a', { class: 'apt-lux', href: '/apartments' },
        h('div', { class: 'apt-lux-top' }, S.statusChip(a.status), h('span', { class: 'apt-no' }, '№ ' + a.number)),
        h('div', { class: 'apt-lux-meta' }, t('floor.n', { n: a.floor }) + ' · ' + S.fmt.rooms(a.rooms) + ' · ' + S.fmt.area(a.area)),
        h('div', { class: 'apt-lux-price' }, S.fmt.money(a.price, 'TJS')),
        h('div', { class: 'apt-lux-sqm' }, S.fmt.n(a.pricePerSqm) + ' TJS / ' + t('u.m2')),
        h('span', { class: 'apt-lux-more' }, t('teaser.more')));
    }
    // Without the API: apartment types instead of made-up apartments.
    function typeCard(i) {
      return h('a', { class: 'apt-lux', href: '/apartments' },
        h('div', { class: 'apt-lux-top' }, S.statusChip('free')),
        h('div', { class: 'apt-lux-meta' }, t('teaser.type' + i)),
        h('div', { class: 'apt-lux-price' }, i === 0 ? t('home.chip.from', { p: S.fmt.money(d.minPrice, 'TJS') }) : t('teaser.ask')),
        h('div', { class: 'apt-lux-sqm' }, t('teaser.typeSub')),
        h('span', { class: 'apt-lux-more' }, t('teaser.more')));
    }

    function renderCall() {
      const phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel', 'aria-label': t('f.phone') });
      const btn = h('button', { class: 'btn gold lg block', type: 'submit' }, t('cta.call'));
      const form = h('form', { class: 'stack', novalidate: true }, h('h3', null, t('cta.formTitle')), phone, btn, h('small', { class: 'micro' }, t('cta.micro')));
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
        if (!id) return;
        const b = await S.api.building(id);
        const floors = await S.api.floors(id);
        const apts = await S.api.allApartments(floors);
        if (!alive) return;
        const free = apts.filter((a) => a.status === 'free').sort((x, y) => x.price - y.price);
        d.floors = b.floorsCount || Math.max(0, ...floors.map((f) => f.number)) || d.floors;
        d.apts = apts.length || d.apts;
        if (b.deliveryDate) d.year = new Date(b.deliveryDate).getFullYear();
        if (free.length) d.minPrice = free[0].price;
        d.free = free.length;
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
      } catch (e) { /* the page stays on the numbers from config.js */ }
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
        // Day by day when the schedule is set, otherwise the one line of hours.
        schedule.length
          ? h('div', { class: 'ct-card ct-hours' }, h('span', { class: 'ct-ic' }, landIcon('clock')), h('div', null, h('small', null, t('ct.hours')),
            h('dl', null, schedule.map(([day, time]) => [h('dt', null, t('day.' + day)), h('dd', { class: time ? '' : 'off' }, time || t('day.off'))]))))
          : line('clock', t('ct.hours'), c.hours || t('ct.hoursVal'))),
      // A map card: the office address on a stylised city plan.
      h('div', { class: 'ct-map', role: 'img', 'aria-label': c.address || '' },
        h('div', { class: 'ct-map-pin' }, landIcon('pin')), h('b', null, t('ct.city') + ', ' + (c.address || '').replace(/^Душанбе,\s*/, '')),
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
        h('label', { class: 'check ct-agree' }, agree, h('span', null, t('ct.agree')), h('em', { class: 'fl-err' })), btn);
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
      btn);

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
})();
