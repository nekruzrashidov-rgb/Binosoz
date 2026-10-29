/* Public site: 3D tower, apartment picker, booking, calculator, contact. */
(function () {
  const S = (window.S = window.S || {});
  const { h, t } = { h: (...a) => S.h(...a), t: (...a) => S.t(...a) };
  S.views = S.views || {};

  const STATUS_VAR = { free: 'var(--good)', booked: 'var(--warn)', sold: 'var(--muted)', onrequest: 'var(--info)' };
  const STATUS_BG = { free: 'var(--good-soft)', booked: 'var(--warn-soft)', sold: 'var(--surface-3)', onrequest: 'var(--info-soft)' };
  // Role cards of the landing: key, icon, where the button leads, button text.
  const ROLES = [['buyer', 'home', '/cabinet', 'land.demo'], ['dev', 'building', '/crm', 'land.enter'], ['acc', 'chart', '/crm', 'land.enter']];
  const SA_ICONS = ['plus', 'users', 'file', 'chart'];

  S.views.home = function (root) {
    const cfg = window.SITORA_CONFIG || {};
    const st = { building: null, floors: [], apts: [], rate: null, sel: null, floor: null, rooms: 'all', maxPrice: 0, sort: 'price', onlyFree: true, shown: 12, overlay: true, time: 'dusk' };
    let house = null, alive = true, io = null;
    const page = h('div', { class: 'page-in' });
    root.appendChild(page);

    // Platform hero above the 3D tower: what Binosoz is, before the showroom of one building.
    // It is on screen at once, so it rises with a CSS animation instead of waiting for the scroll observer.
    const landStats = h('div', { class: 'land-stats glass' });
    page.appendChild(h('section', { class: 'land-hero', id: 'landing' }, h('div', { class: 'wrap' },
      h('span', { class: 'land-eyebrow' }, S.icon('building'), t('land.eyebrow')),
      h('h1', null, t('land.title')),
      h('p', null, t('land.sub')),
      h('div', { class: 'land-cta' },
        h('button', { class: 'btn gold lg', onclick: () => scrollTo('contact') }, t('land.cta1')),
        h('button', { class: 'btn ghost lg', onclick: () => scrollTo('home') }, t('land.cta2'))),
      landStats)));

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
    const rolesSection = h('section', { class: 'section alt', id: 'roles' });
    const superSection = h('section', { class: 'section', id: 'platform-owner' });
    const calcSection = h('section', { class: 'section alt', id: 'calc' });
    const aboutSection = h('section', { class: 'section', id: 'about' });
    const contactSection = h('section', { class: 'section alt', id: 'contact' });
    page.append(aptSection, rolesSection, superSection, calcSection, aboutSection, contactSection, footer());
    // These do not depend on the building's data, so they show even while it loads or when the API is down.
    renderLandStats(); renderRoles(); renderSuper(); reveal();

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
    function renderAll() { renderLandStats(); renderHero(); renderFloors(); renderSheet(); renderApartments(); renderCalc(); renderAbout(); renderContact(); }

    // ---------- landing ----------
    // The numbers from config.js until the building loads (and when the API is unavailable), then the real ones.
    function renderLandStats() {
      const b = st.building;
      const floors = (b && (b.floorsCount || Math.max(0, ...st.floors.map((f) => f.number)))) || cfg.DEFAULT_FLOORS || 16;
      const apts = st.apts.length || cfg.DEFAULT_APTS || 128;
      const ls = (v, l) => h('div', { class: 'ls' }, h('b', null, v), h('span', null, l));
      landStats.replaceChildren(ls(S.fmt.n(floors), t('land.floors')), ls(S.fmt.n(apts), t('land.apts')), ls('3', t('land.roles')));
    }
    function renderRoles() {
      rolesSection.replaceChildren(h('div', { class: 'wrap' },
        h('div', { class: 'sec-head reveal' }, h('div', null, h('h2', null, t('roles.title')), h('p', null, t('roles.sub')))),
        h('div', { class: 'grid g3' }, ROLES.map(([k, ic, href, cta], i) => h('article', { class: 'role-card reveal', 'data-role': k, style: { transitionDelay: i * 90 + 'ms' } },
          h('div', { class: 'role-ic' }, S.icon(ic)),
          h('h3', null, t('roles.' + k)),
          h('ul', { class: 'role-list' }, [1, 2, 3, 4].map((n) => h('li', null, t('roles.' + k + '.' + n)))),
          h('a', { class: 'btn ghost', href }, t(cta)))))));
    }
    function renderSuper() {
      superSection.replaceChildren(h('div', { class: 'wrap' }, h('div', { class: 'sa-card reveal' },
        h('div', { class: 'sa-text' }, h('span', { class: 'sa-badge' }, S.icon('shield'), 'Super Admin'), h('h2', null, t('sa.title')), h('p', null, t('sa.sub')), h('a', { class: 'btn gold lg', href: '/platform' }, t('sa.cta'))),
        h('ul', { class: 'sa-list' }, SA_ICONS.map((ic, i) => h('li', null, h('span', { class: 'sa-ic' }, S.icon(ic)), t('sa.' + (i + 1))))))));
    }
    // Sections fade in as they scroll into view; without IntersectionObserver they are simply shown.
    function reveal() {
      const els = S.$$('.reveal:not(.in)', page);
      if (!('IntersectionObserver' in window)) { els.forEach((el) => el.classList.add('in')); return; }
      io = io || new IntersectionObserver((entries) => entries.forEach((e) => { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } }), { threshold: 0.12 });
      els.forEach((el) => io.observe(el));
    }

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
        h('div', { class: 'sec-head' }, h('div', null, h('h2', null, t('apts.title')), h('p', null, t('apts.sub'))), count),
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
      const a = st.calcApt;
      const free = st.apts.filter((x) => x.status === 'free');
      const basePrice = a ? a.price : free.length ? Math.round(free.reduce((s, x) => s + x.price, 0) / free.length / 10000) * 10000 : 600000;
      const price = S.input('price', { type: 'number', min: 10000, step: 1000, value: basePrice });
      const down = h('input', { class: 'range', type: 'range', id: 'c-down', min: 10, max: 90, step: 5, value: 30, 'aria-label': t('calc.down') });
      const months = h('input', { class: 'range', type: 'range', id: 'c-months', min: 6, max: 120, step: 6, value: 60, 'aria-label': t('calc.months') });
      const dv = h('b', { class: 'num' }), mv = h('b', { class: 'num' });
      const out = h('div', { class: 'calc-out' });
      const upd = () => {
        const p = Math.max(0, +price.value || 0); const r = monthly(p, +down.value, +months.value);
        dv.textContent = down.value + '% · ' + S.fmt.money(r.down, 'TJS'); mv.textContent = months.value + ' ' + t('u.monthsShort');
        out.replaceChildren(h('small', null, t('calc.monthly')), h('div', { class: 'big' }, S.fmt.money(r.monthly, 'TJS')), h('small', null, t('calc.noInterest')), h('div', { style: { marginTop: '14px', position: 'relative', zIndex: 1 } }, h('button', { class: 'btn gold', onclick: () => leadModal(t('lead.calc', { p: S.fmt.n(p), d: down.value, m: months.value, mo: S.fmt.n(r.monthly) }), a && a.id) }, t('calc.send'))));
      };
      [price, down, months].forEach((el) => el.addEventListener('input', upd));
      calcSection.replaceChildren(h('div', { class: 'wrap' }, h('div', { class: 'sec-head' }, h('div', null, h('h2', null, t('calc.title')), h('p', null, a ? t('calc.forApt', { n: a.number }) : t('calc.sub')))),
        h('div', { class: 'calc' }, h('div', { class: 'card stack' }, S.field(t('calc.price') + ' (TJS)', price), h('div', { class: 'field' }, h('label', { for: 'c-down' }, t('calc.down')), down, dv), h('div', { class: 'field' }, h('label', { for: 'c-months' }, t('calc.months')), months, mv)), out)));
      upd();
    }

    // ---------- about / contact ----------
    function renderAbout() {
      const items = [['building', 'about.1'], ['shield', 'about.2'], ['grid', 'about.3'], ['users', 'about.4']];
      aboutSection.replaceChildren(h('div', { class: 'wrap' }, h('div', { class: 'sec-head' }, h('div', null, h('h2', null, t('about.title')), h('p', null, t('about.sub')))),
        h('div', { class: 'grid g4' }, items.map(([ic, k]) => h('div', { class: 'card hover stack' }, h('div', { class: 'avatar', style: { width: '40px', height: '40px', borderRadius: '12px' } }, S.icon(ic, '')), h('h3', null, t(k + '.t')), h('p', { class: 'muted' }, t(k + '.d')))))));
      S.$$('.avatar svg', aboutSection).forEach((s) => { s.style.cssText = 'width:20px;height:20px;stroke:#fff;fill:none;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round'; });
    }
    function renderContact() {
      const c = cfg.CONTACT || {};
      const name = S.input('name', { placeholder: t('f.namePh'), autocomplete: 'name' }), phone = S.input('phone', { type: 'tel', value: '+992', autocomplete: 'tel' });
      const cm = h('textarea', { class: 'inp', name: 'comment', id: 'contact-comment', maxlength: 400, placeholder: t('f.commentPh') });
      const form = h('form', { class: 'stack card', novalidate: true }, S.field(t('f.name'), name), S.field(t('f.phone'), phone, t('f.phoneHint')), S.field(t('f.comment'), cm));
      const btn = h('button', { class: 'btn lg', type: 'submit' }, t('lead.send')); form.appendChild(btn);
      form.addEventListener('submit', async (e) => { e.preventDefault(); S.showErrors(form, null); await S.busy(btn, async () => { try { await S.api.createLead({ name: name.value.trim(), phone: phone.value.replace(/[\s()-]/g, ''), comment: cm.value.trim() }); form.reset(); phone.value = '+992'; S.toast(t('lead.ok'), 'ok'); } catch (er) { S.showErrors(form, er); } }); });
      contactSection.replaceChildren(h('div', { class: 'wrap' }, h('div', { class: 'sec-head' }, h('div', null, h('h2', null, t('contact.title')), h('p', null, t('contact.sub')))),
        h('div', { class: 'calc' }, form, h('div', { class: 'card stack' }, h('h3', null, t('contact.office')),
          h('div', { class: 'row' }, S.icon('pin'), h('span', null, c.address || (st.building && st.building.address) || '')),
          h('div', { class: 'row' }, S.icon('phone'), h('b', null, c.phone || ''), c.phone ? h('button', { class: 'btn ghost sm', onclick: () => S.copy(c.phone) }, t('common.copy')) : null),
          c.telegram ? h('div', { class: 'row' }, S.icon('send'), h('a', { href: 'https://t.me/' + c.telegram.replace('@', ''), target: '_blank', rel: 'noopener' }, c.telegram)) : null,
          h('div', { class: 'row' }, S.icon('clock'), h('span', null, c.hours || t('contact.hours'))),
          h('p', { class: 'muted' }, t('contact.note'))))));
    }
    function footer() { return h('footer', { class: 'footer' }, h('div', { class: 'wrap row between' }, h('div', { class: 'row' }, S.logo(), h('b', { style: { color: '#fff', fontFamily: 'var(--display)' } }, 'Sitora')), h('span', null, t('footer.rights')))); }

    function scrollTo(id) { const el = document.getElementById(id === 'home' ? 'hero-top' : id); if (el) el.scrollIntoView({ behavior: 'smooth', block: id === 'home' ? 'start' : 'start' }); }
    S.scrollTo = scrollTo;

    load();
    return () => { alive = false; if (io) io.disconnect(); if (house) house.destroy(); };
  };
})();
