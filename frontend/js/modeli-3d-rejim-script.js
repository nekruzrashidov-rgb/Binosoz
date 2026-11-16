/* «3D на весь экран» (/apartments#3d): the catalog's own 3D model (modeli-3d-bino-script.js — the same scene, moved into a
   full-screen layer) with the camera views, the time of day by the real sun of Dushanbe, the real surroundings from
   OpenStreetMap (only for a building with coordinates), the list of apartments with the catalog filters, the price card with
   booking, the apartment inside and the walk. Every number comes from the API; nothing is invented.
   Loaded with the engine by sahifai-asosi-script.js; S.House3DMode.open(ctx) → { close, refresh, el }. */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (k, p) => S.t(k, p);

  const POI_CATS = ['school', 'kindergarten', 'park', 'medical', 'shop', 'transport', 'mosque'];
  const POI_ICON = { school: '🏫', kindergarten: '🧸', park: '🌳', medical: '✚', shop: '🛒', transport: '🚌', mosque: '🕌' };
  const HINT_KEY = 'sitora.3dHint';
  const store = { get(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }, set(k, v) { try { localStorage.setItem(k, v); } catch (e) { /* private mode */ } } };
  const hhmm = (x) => { const m = Math.round(x * 60) % (24 * 60); return String(Math.floor(m / 60)).padStart(2, '0') + ':' + String(m % 60).padStart(2, '0'); };
  const distText = (m) => (m < 1000 ? S.fmt.n(Math.round(m / 10) * 10) + ' ' + t('h3d.m') : String((m / 1000).toFixed(1)).replace('.', S.lang === 'en' ? '.' : ',') + ' ' + t('h3d.km'));
  const coarse = () => window.matchMedia && window.matchMedia('(pointer: coarse)').matches;

  function open(ctx) {
    const house = ctx.house, st = ctx.st, b = ctx.building || {};
    const home = ctx.box.parentNode, next = ctx.box.nextSibling;
    const scene = h('div', { class: 'h3d-scene' });
    const root = h('div', { class: 'h3d-full nr-dark', role: 'dialog', 'aria-modal': 'true', 'aria-label': t('h3d.title') }, scene);
    document.body.appendChild(root);
    document.documentElement.classList.add('h3d-lock');
    scene.appendChild(ctx.box);
    const lastFocus = document.activeElement;

    // ---------- top bar: back, title, time of day ----------
    const back = h('button', { class: 'h3d-btn h3d-back', type: 'button', onclick: () => ctx.requestClose() }, '← ' + t('h3d.back'));
    const slider = h('input', { type: 'range', min: 5, max: 22, step: 0.25, value: 12, 'aria-label': t('h3d.timeSlider'), class: 'h3d-range' });
    const clock = h('output', { class: 'h3d-clock', 'aria-live': 'off' }, '12:00');
    const play = h('button', { class: 'h3d-icon', type: 'button', 'aria-label': t('h3d.play'), title: t('h3d.play') }, '▶');
    const nowBtn = h('button', { class: 'h3d-chip', type: 'button', 'aria-pressed': 'true', title: t('h3d.nowHint') }, t('h3d.now'));
    const presetSeg = h('div', { class: 'h3d-seg', role: 'group', 'aria-label': t('hero.time') }, ['day', 'dusk', 'night'].map((k) => h('button', { type: 'button', 'data-k': k, onclick: () => preset(k) }, t('time.' + k))));
    const timeBox = h('div', { class: 'h3d-time' }, h('span', { 'aria-hidden': 'true', class: 'h3d-sym' }, '☀'), slider, h('span', { 'aria-hidden': 'true', class: 'h3d-sym' }, '☾'), clock, play, nowBtn, presetSeg);
    const top = h('div', { class: 'h3d-top' }, back, h('div', { class: 'h3d-title' }, h('b', null, b.name || t('brand.name')), h('span', null, t('h3d.kicker'))), timeBox);
    root.appendChild(top);

    let nowTimer = 0, nowOn = true;
    function showClock(x) { clock.textContent = hhmm(x); slider.value = String(Math.max(5, Math.min(22, x))); }
    function setNow(on) {
      nowOn = on; nowBtn.setAttribute('aria-pressed', String(on));
      clearInterval(nowTimer);
      if (on) { stopPlay(); const tick = () => { const x = house.nowHours(); house.setClock(x, true); showClock(x); }; tick(); nowTimer = setInterval(tick, 60000); }
    }
    function stopPlay() { if (house.playing) house.stop(); play.textContent = '▶'; play.setAttribute('aria-label', t('h3d.play')); }
    slider.addEventListener('input', () => { setNow(false); stopPlay(); const x = +slider.value; house.setClock(x, false); showClock(x); markPreset(null); });
    play.addEventListener('click', () => {
      if (house.playing) { stopPlay(); return; }
      setNow(false); markPreset(null);
      const from = +slider.value >= 21.9 ? 5 : +slider.value;
      play.textContent = '❚❚'; play.setAttribute('aria-label', t('h3d.pause'));
      house.play(from, 20 * (22 - from) / 17, (x) => showClock(x), () => { play.textContent = '▶'; play.setAttribute('aria-label', t('h3d.play')); });
    });
    nowBtn.addEventListener('click', () => { setNow(true); markPreset(null); });
    function markPreset(k) { S.$$('button', presetSeg).forEach((x) => x.setAttribute('aria-pressed', String(x.dataset.k === k))); }
    // The day, golden hour and night buttons are points of the same slider: 13:00, the sun 3° above the horizon, 21:30.
    function preset(k) {
      setNow(false); stopPlay();
      const x = k === 'day' ? 13 : k === 'night' ? 21.5 : house.hourAtElevation(3, true);
      house.setClock(x, true); showClock(x); markPreset(k);
    }

    // ---------- labels: the height over the house ----------
    const fh = b.floorHeight ? Number(b.floorHeight) : null;
    const floors = b.floorsCount || house.floors;
    const metres = Math.round(floors * (fh || 3.0));
    const heightLabel = h('div', { class: 'h3d-tag h3d-tag--height' }, h('b', null, S.fmt.n(floors) + ' ' + S.plural(floors, 'nr.floors')), ' · ≈ ' + S.fmt.n(metres) + ' ' + t('h3d.m') + (fh ? '' : ' · ' + t('h3d.estimate')));
    house.addLabel(heightLabel, house.anchors().top, { group: 'house', prio: 10, free: true });

    // ---------- camera views ----------
    const viewBar = h('nav', { class: 'h3d-views', 'aria-label': t('h3d.views') });
    const listToggle = h('button', { class: 'h3d-btn h3d-list-toggle', type: 'button', 'aria-expanded': 'false', onclick: () => toggleList() });
    function drawViews() {
      viewBar.replaceChildren(listToggle, ...house.views.filter((v) => v !== 'area' || house.hasContext).map((v) =>
        h('button', { class: 'h3d-chip', type: 'button', 'data-view': v, 'aria-pressed': String(house.currentView === v), onclick: () => { house.setView(v); } }, t('h3d.view.' + v))));
    }
    house.on('view', (v) => S.$$('[data-view]', viewBar).forEach((x) => x.setAttribute('aria-pressed', String(x.dataset.view === v))));
    root.appendChild(viewBar);

    // ---------- the list of apartments (left; a sheet from below on a phone) ----------
    const listBox = h('div', { class: 'h3d-items', role: 'listbox', 'aria-label': t('h3d.list'), tabindex: '0' });
    const filters = h('div', { class: 'h3d-filters' });
    const list = h('aside', { class: 'h3d-list', 'aria-label': t('h3d.list') }, h('div', { class: 'h3d-list-head' }, h('b', { class: 'h3d-list-title' }), h('button', { class: 'h3d-icon h3d-list-close', type: 'button', 'aria-label': t('common.close'), onclick: () => toggleList(false) }, '×')), filters, listBox);
    root.appendChild(list);
    let listOpen = false;
    function toggleList(v) { listOpen = v == null ? !listOpen : v; root.classList.toggle('list-open', listOpen); listToggle.setAttribute('aria-expanded', String(listOpen)); }
    const match = (a) => ctx.matches(a);
    function drawFilters() {
      const opts = ctx.roomOptions(ctx.apts());
      const pills = h('div', { class: 'h3d-pills', role: 'group', 'aria-label': t('filter.rooms') }, opts.map(([v, l]) => h('button', { type: 'button', class: 'h3d-chip', 'aria-pressed': String(st.rooms === v), onclick: () => { st.rooms = v; changed(); } }, l)));
      const free = h('label', { class: 'h3d-check' }, h('input', { type: 'checkbox', checked: st.onlyFree, onchange: (e) => { st.onlyFree = e.target.checked; changed(); } }), t('filter.onlyFree'));
      const all = ctx.apts();
      const lo = Math.floor(Math.min(...all.map((a) => a.price)) / 10000) * 10000;
      const out = h('b', null, S.fmt.money(st.priceCap, 'TJS'));
      const price = h('input', { type: 'range', class: 'h3d-range', min: lo, max: st.maxPrice, step: 10000, value: st.priceCap, 'aria-label': t('filter.priceTo') });
      price.addEventListener('input', () => { st.priceCap = +price.value; out.textContent = S.fmt.money(st.priceCap, 'TJS'); drawList(); applyFilter(); });
      price.addEventListener('change', () => ctx.onFilter());
      filters.replaceChildren(pills, h('div', { class: 'h3d-price' }, h('span', null, t('filter.priceTo')), out), price, free);
    }
    function changed() { drawFilters(); drawList(); applyFilter(); ctx.onFilter(); }
    function applyFilter() { house.setFilter(match); }
    let rows = [], active = -1;
    function drawList() {
      const l = ctx.apts().filter(match).sort((x, y) => y.floor - x.floor || x.number - y.number);
      S.$('.h3d-list-title', list).textContent = t('h3d.listN', { n: l.length });
      listToggle.textContent = t('h3d.listBtn', { n: l.length });
      rows = l;
      listBox.replaceChildren(...(l.length ? l.map((a, k) => h('div', { class: 'h3d-item st-' + a.status, role: 'option', id: 'h3d-opt-' + k, 'aria-selected': String(!!sel && sel.id === a.id), onclick: () => choose(a, true) },
        h('span', { class: 'h3d-item-top' }, h('b', null, t('nr.cat.aptN', { n: a.number })), h('span', { class: 'h3d-item-st' }, h('i', { class: 'nr-dot ' + a.status }), t('status.' + a.status))),
        h('span', { class: 'h3d-item-meta' }, t('floor.n', { n: a.floor }) + ' · ' + S.fmt.rooms(a.rooms) + ' · ' + S.fmt.area(a.area)),
        h('span', { class: 'h3d-item-price' }, S.fmt.money(a.price, 'TJS'), usd(a.price) ? h('small', null, ' ≈ $' + S.fmt.n(usd(a.price))) : null))) : [h('p', { class: 'h3d-empty' }, t('list.none'))]));
      active = sel ? l.findIndex((a) => a.id === sel.id) : -1;
      if (active >= 0) listBox.setAttribute('aria-activedescendant', 'h3d-opt-' + active); else listBox.removeAttribute('aria-activedescendant');
    }
    listBox.addEventListener('keydown', (e) => {
      if (!rows.length) return;
      if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
        e.preventDefault();
        active = Math.max(0, Math.min(rows.length - 1, active + (e.key === 'ArrowDown' ? 1 : -1)));
        S.$$('.h3d-item', listBox).forEach((x, k) => x.classList.toggle('is-active', k === active));
        const item = S.$('#h3d-opt-' + active, listBox); listBox.setAttribute('aria-activedescendant', 'h3d-opt-' + active);
        if (item && item.scrollIntoView) item.scrollIntoView({ block: 'nearest' });
      } else if (e.key === 'Enter' && active >= 0) { e.preventDefault(); choose(rows[active], true); }
    });
    // Price in dollars: by the National Bank rate from the API, marked as an estimate; no rate — no dollars.
    const usd = (p) => (ctx.rate && ctx.rate.rate ? Math.round(p / ctx.rate.rate) : null);

    // ---------- the price card (right, bottom) ----------
    const card = h('section', { class: 'h3d-card', 'aria-live': 'polite', hidden: true });
    root.appendChild(card);
    let sel = null;
    function choose(a, fly) {
      sel = a;
      house.selectApartment(a.id, !!fly);
      ctx.onPick(a);
      drawCard(); drawList();
      if (fly && window.matchMedia('(max-width: 760px)').matches) toggleList(false);
    }
    function drawCard() {
      if (!sel) { card.hidden = true; return; }
      const a = sel, d = usd(a.price);
      card.hidden = false;
      const phone = ctx.phone;
      card.replaceChildren(
        h('button', { class: 'h3d-icon h3d-card-x', type: 'button', 'aria-label': t('common.close'), onclick: () => { sel = null; house.clearSelection(); drawCard(); drawList(); } }, '×'),
        h('div', { class: 'h3d-card-kicker' }, t('nr.cat.aptN', { n: a.number }) + ' · ' + t('floor.n', { n: a.floor })),
        h('div', { class: 'h3d-card-type' }, t('layouts.type' + Math.min(3, a.rooms)) + ' · ' + S.fmt.area(a.area)),
        h('div', { class: 'h3d-card-price' }, S.fmt.money(a.price, 'TJS')),
        d ? h('div', { class: 'h3d-card-usd' }, '≈ $' + S.fmt.n(d) + ' · ' + t('price.estimate')) : null,
        h('div', { class: 'h3d-card-st' }, h('i', { class: 'nr-dot ' + a.status }), t('status.' + a.status)),
        h('div', { class: 'h3d-card-acts' },
          a.status === 'free' ? h('button', { class: 'btn nr-btn nr-btn--primary', type: 'button', onclick: () => ctx.book(a) }, t('book.cta')) : h('p', { class: 'h3d-card-off' }, t('book.unavailable')),
          h('div', { class: 'h3d-card-row' },
            phone ? h('a', { class: 'h3d-btn', href: 'tel:+' + String(phone).replace(/\D/g, '') }, S.icon('phone'), t('h3d.call')) : null,
            h('button', { class: 'h3d-btn', type: 'button', 'data-act': 'inside', onclick: () => goInside() }, S.icon('eye'), t('h3d.inside')))));
    }
    house.on('select', (a) => { const apt = ctx.apts().find((x) => x.id === a.id); if (apt) { sel = apt; ctx.onPick(apt); drawCard(); drawList(); } });

    // ---------- inside the apartment and the walk ----------
    const furnBtn = h('button', { class: 'h3d-chip', type: 'button', 'aria-pressed': 'true', onclick: () => { const on = furnBtn.getAttribute('aria-pressed') !== 'true'; furnBtn.setAttribute('aria-pressed', String(on)); house.setFurniture(on); } }, t('h3d.furniture'));
    const walkBtn = h('button', { class: 'h3d-btn h3d-btn--accent', type: 'button', onclick: () => house.startWalk() }, t('h3d.walk'));
    const insideBack = h('button', { class: 'h3d-btn', type: 'button', onclick: () => house.leaveInside() }, '← ' + t('h3d.toHouse'));
    const walkBack = h('button', { class: 'h3d-btn h3d-btn--accent', type: 'button', onclick: () => house.leaveWalk() }, t('h3d.walkExit'));
    const insideNote = h('p', { class: 'h3d-note' });
    const insideBar = h('div', { class: 'h3d-inside', hidden: true }, h('div', { class: 'h3d-inside-acts' }, insideBack, furnBtn, walkBtn, walkBack), insideNote);
    root.appendChild(insideBar);
    const hintLine = h('p', { class: 'h3d-gesture', 'aria-live': 'polite' });
    root.appendChild(hintLine);
    function goInside() {
      if (!sel) return;
      const a = sel;
      ctx.loadInterior().then(() => {
        if (!S.plans || !house.enterInside(a.id, S.plans.layout(Math.min(3, a.rooms), a.area), { furniture: furnBtn.getAttribute('aria-pressed') === 'true' })) S.toast(t('h3d.insideFail'), 'err');
      }).catch(() => S.toast(t('h3d.insideFail'), 'err'));
    }
    house.on('mode', (m) => {
      root.dataset.mode = m;
      insideBar.hidden = m === 'house';
      walkBtn.hidden = m !== 'inside'; walkBack.hidden = m !== 'walk'; insideBack.hidden = m !== 'inside'; furnBtn.hidden = m === 'walk';
      house.removeLabels('room');
      if (m === 'house') { hintLine.textContent = ''; return; }
      insideNote.textContent = t('h3d.typical') + ' · ' + t('h3d.furnitureNote');
      hintLine.textContent = m === 'walk' ? t(coarse() ? 'h3d.walkHintTouch' : 'h3d.walkHint') : t('h3d.insideHint');
      // Room labels with their area (the layout scaled to the apartment's area); a click — stand there.
      const anchors = house.roomAnchors();
      house.insideRooms.forEach((r, k) => {
        const tag = h('button', { class: 'h3d-tag h3d-tag--room', type: 'button', onclick: () => house.roomFocus(k) }, h('b', null, t('plans.room.' + r.key)), ' ' + S.fmt.n(Math.round(r.area * 10) / 10) + ' ' + t('u.m2'));
        house.addLabel(tag, anchors[k], { group: 'room', prio: 5 });
      });
      if (m === 'walk') { const c = S.$('canvas', scene); if (c) c.focus({ preventScroll: true }); }
    });

    // ---------- the real surroundings (only with the building's coordinates) ----------
    const poiBar = h('div', { class: 'h3d-poi', role: 'group', 'aria-label': t('h3d.poiFilter'), hidden: true });
    const attribution = h('div', { class: 'h3d-attr', hidden: true }, h('a', { href: 'https://www.openstreetmap.org/copyright', target: '_blank', rel: 'noopener' }, t('h3d.osm')));
    root.append(poiBar, attribution);
    const poiOn = new Set(POI_CATS);
    let poiLabels = [];
    function loadContext() {
      if (b.latitude == null || b.longitude == null) return;
      house.setGeo(b.latitude, b.longitude);
      if (house.hasContext) { showPois(ctx.pois || []); return; }
      fetch('/data/context-' + encodeURIComponent(b.id) + '.json', { cache: 'no-cache' }).then((r) => (r.ok ? r.json() : null)).then((data) => {
        if (!data || !data.origin || !alive) return;
        // The file must be made for these coordinates (within 150 m), otherwise it is someone else's surroundings.
        if (S.House3D.distanceM(+b.latitude, +b.longitude, data.origin.lat, data.origin.lon) > 150) return;
        ctx.pois = house.setContext(data, { lat: +b.latitude, lon: +b.longitude });
        showPois(ctx.pois);
      }).catch(() => { /* no file — no surroundings */ });
    }
    function showPois(pois) {
      drawViews();
      house.removeLabels('poi');
      poiLabels = pois.filter((p) => p.dist <= 1500).sort((x, y) => x.dist - y.dist).map((p, k) => {
        const el = h('div', { class: 'h3d-tag h3d-tag--poi', 'data-cat': p.cat }, h('span', { class: 'h3d-poi-ic', 'aria-hidden': 'true' }, POI_ICON[p.cat] || '•'), h('b', null, p.name), h('small', null, distText(p.dist) + ' · ' + t('h3d.straight')));
        return { p, l: house.addLabel(el, [p.x, 8, p.z], { group: 'poi', prio: 3 - k * 0.01 }) };
      });
      const cats = POI_CATS.filter((c) => pois.some((p) => p.cat === c));
      poiBar.hidden = !cats.length; attribution.hidden = false;
      poiBar.replaceChildren(...cats.map((c) => h('button', { class: 'h3d-chip', type: 'button', 'aria-pressed': String(poiOn.has(c)), onclick: (e) => { if (poiOn.has(c)) poiOn.delete(c); else poiOn.add(c); e.currentTarget.setAttribute('aria-pressed', String(poiOn.has(c))); poiLabels.forEach(({ p, l }) => { l.on = poiOn.has(p.cat); }); } }, (POI_ICON[c] || '') + ' ' + t('h3d.poi.' + c))));
    }

    // ---------- first visit: the gestures ----------
    if (!store.get(HINT_KEY)) {
      const close = h('button', { class: 'btn nr-btn nr-btn--primary', type: 'button' }, t('h3d.hintOk'));
      const hint = h('div', { class: 'h3d-hint', role: 'note' }, h('b', null, t('h3d.hintTitle')), h('p', null, t(coarse() ? 'h3d.hintTouch' : 'h3d.hintMouse')), close);
      close.addEventListener('click', () => { store.set(HINT_KEY, '1'); hint.remove(); });
      root.appendChild(hint);
    }

    // ---------- keys ----------
    const onKey = (e) => {
      if (e.key !== 'Escape' || document.querySelector('.overlay')) return;
      e.preventDefault();
      if (house.mode === 'walk') house.leaveWalk();
      else if (house.mode === 'inside') house.leaveInside();
      else if (listOpen && window.matchMedia('(max-width: 760px)').matches) toggleList(false);
      else ctx.requestClose();
    };
    document.addEventListener('keydown', onKey);

    let alive = true;
    house.setFocusMode(true); house.setAutoRotate(false);
    const inset = () => house.setLabelInset(top.getBoundingClientRect().bottom - root.getBoundingClientRect().top);
    inset(); window.addEventListener('resize', inset);
    drawViews(); drawFilters(); drawList(); applyFilter();
    if (ctx.selected) { sel = ctx.selected; drawCard(); drawList(); }
    house.setView('overview', 0.9);
    setNow(true);
    loadContext();
    root.dataset.mode = 'house'; walkBack.hidden = true;
    setTimeout(() => back.focus({ preventScroll: true }), 50);
    window.__house3dMode = { root, choose: (id) => { const a = ctx.apts().find((x) => x.id === id); if (a) choose(a, true); } };

    return {
      el: root,
      refresh() { if (sel) sel = ctx.apts().find((x) => x.id === sel.id) || null; drawFilters(); drawList(); drawCard(); applyFilter(); },
      close() {
        if (!alive) return; alive = false;
        clearInterval(nowTimer); stopPlay();
        document.removeEventListener('keydown', onKey); window.removeEventListener('resize', inset); house.setLabelInset(0);
        if (house.mode !== 'house') { house.leaveInside(); }
        house.removeLabels('poi'); house.removeLabels('house'); house.removeLabels('room');
        house.setFilter(null); house.setFocusMode(false);
        house.on('select', ctx.onModelSelect || (() => {})); house.on('view', () => {}); house.on('mode', () => {});
        if (next && next.parentNode === home) home.insertBefore(ctx.box, next); else home.appendChild(ctx.box);
        root.remove(); document.documentElement.classList.remove('h3d-lock');
        delete window.__house3dMode;
        if (lastFocus && lastFocus.focus) lastFocus.focus({ preventScroll: true });
      },
    };
  }

  S.House3DMode = { open };
})();
