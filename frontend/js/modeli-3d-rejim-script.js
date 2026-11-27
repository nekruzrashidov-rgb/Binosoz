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

    // ---------- the layout (≥ 1024 px): top left — «← Каталог», the kicker and the title of what is shown; top centre — the one
    // main control «Весь ЖК · A · B · C»; top right — icons 36 px (time of day, parking, cut-away, full screen, close);
    // bottom centre — the camera views; right — the floor scale of the chosen block; left — the folding list of apartments.
    // On a phone (≤ 768 px) the list is a bottom sheet with the views inside. ----------
    const blocks = house.blocks.filter((x) => x.code);
    const complex = blocks.length > 1;
    let cur = null; // the chosen block (letter) or null — the whole complex
    const blockOf = (code) => blocks.find((x) => x.code === code) || null;
    const inBlock = (a) => !cur || a.block === cur;

    const back = h('button', { class: 'h3d-backlink', type: 'button', onclick: () => ctx.requestClose() }, h('span', { 'aria-hidden': 'true' }, '←'), h('span', { class: 'h3d-backlink-t' }, t('h3d.catalog')));
    const headTitle = h('b', { class: 'h3d-head-title' });
    const head = h('div', { class: 'h3d-head' }, back, h('div', { class: 'h3d-head-text' }, h('span', { class: 'h3d-head-kicker', 'data-brand': '' }, S.brandName()), headTitle));
    function drawTitle() {
      const b0 = blockOf(cur);
      headTitle.textContent = b0 ? t('blk.block', { c: b0.code }) + ' · ' + S.fmt.n(b0.floors) + ' ' + S.plural(b0.floors, 'nr.floors') : complex ? t('blk.all') : (b.name || S.brandName());
    }

    // Segments: one button pressed, ←/→ move along (roving focus), Home/End — the ends.
    function segment(cls, label, attr, items, onPick) {
      const box = h('div', { class: 'h3d-seg ' + cls, role: 'group', 'aria-label': label });
      box.addEventListener('keydown', (e) => {
        const bs = S.$$('button', box); const i = bs.indexOf(document.activeElement);
        if (i < 0) return;
        const j = e.key === 'ArrowRight' ? Math.min(bs.length - 1, i + 1) : e.key === 'ArrowLeft' ? Math.max(0, i - 1) : e.key === 'Home' ? 0 : e.key === 'End' ? bs.length - 1 : -1;
        if (j < 0) return;
        e.preventDefault(); bs[j].focus(); bs[j].click();
      });
      box.draw = (sel) => {
        const had = box.contains(document.activeElement);
        box.replaceChildren(...items().map(([v, l, aria]) => h('button', { type: 'button', [attr]: v == null ? 'all' : v, 'aria-pressed': String(sel === v), tabindex: sel === v ? '0' : '-1', 'aria-label': aria || null, onclick: () => onPick(v) }, l)));
        // Redrawn under the keyboard: the focus stays on the pressed button.
        if (had) { const p = S.$('[aria-pressed="true"]', box); if (p) p.focus({ preventScroll: true }); }
      };
      return box;
    }
    const blockSeg = segment('h3d-seg--main h3d-blocks', t('blk.switch'), 'data-block', () => [[null, t('blk.all')]].concat(blocks.map((x) => [x.code, x.code, t('blk.block', { c: x.code })])), (v) => pickBlock(v));

    // ---------- the icons: time of day (a card with the slider), parking, cut-away, full screen, close ----------
    const tool = (icon, label, onclick, extra) => h('button', Object.assign({ class: 'h3d-tool', type: 'button', title: label, 'aria-label': label, onclick }, extra || {}), S.icon(icon));
    const timeBtn = tool('sun', t('h3d.timeSlider'), () => togglePop(), { 'aria-expanded': 'false', 'aria-haspopup': 'dialog' });
    const hasParking = complex && (ctx.units ? ctx.units() : []).some((a) => a.kind === 'parking');
    const parkBtn = hasParking ? tool('car', t('blk.parking'), () => { house.setParking(!house.parking); parkBtn.setAttribute('aria-pressed', String(house.parking)); }, { 'aria-pressed': 'false', 'data-mode': 'parking' }) : null;
    const xrayBtn = complex ? tool('layers', t('blk.xray'), () => { house.setXray(!house.state().xray); xrayBtn.setAttribute('aria-pressed', String(house.state().xray)); }, { 'aria-pressed': 'false', 'data-mode': 'xray' }) : null;
    const fsOk = !!(document.fullscreenEnabled && root.requestFullscreen);
    const fsBtn = fsOk ? tool('expand', t('h3d.fullscreen'), () => { if (document.fullscreenElement) document.exitFullscreen().catch(() => {}); else root.requestFullscreen().catch(() => {}); }) : null;
    const onFs = () => { if (!fsBtn) return; const on = document.fullscreenElement === root; fsBtn.setAttribute('aria-label', t(on ? 'h3d.fullscreenExit' : 'h3d.fullscreen')); fsBtn.title = fsBtn.getAttribute('aria-label'); };
    document.addEventListener('fullscreenchange', onFs);
    const closeBtn = tool('x', t('common.close'), () => ctx.requestClose(), { 'data-act': 'close' });
    const tools = h('div', { class: 'h3d-tools', role: 'toolbar', 'aria-label': t('h3d.tools') }, timeBtn, parkBtn, xrayBtn, fsBtn, closeBtn);

    const slider = h('input', { type: 'range', min: 5, max: 22, step: 0.25, value: 12, 'aria-label': t('h3d.timeSlider'), class: 'h3d-range' });
    const clock = h('output', { class: 'h3d-clock', 'aria-live': 'off' }, '12:00');
    const play = h('button', { class: 'h3d-pop-btn', type: 'button', 'aria-label': t('h3d.play'), title: t('h3d.play') }, '▶');
    const nowBtn = h('button', { class: 'h3d-pop-btn', type: 'button', 'aria-pressed': 'true', title: t('h3d.nowHint') }, t('h3d.now'));
    const presetSeg = h('div', { class: 'h3d-seg h3d-seg--sm', role: 'group', 'aria-label': t('hero.time') }, ['day', 'dusk', 'night'].map((k) => h('button', { type: 'button', 'data-k': k, onclick: () => preset(k) }, t('time.' + k))));
    const pop = h('div', { class: 'h3d-pop', role: 'dialog', 'aria-label': t('h3d.timeSlider'), hidden: true },
      h('div', { class: 'h3d-pop-row' }, h('span', { 'aria-hidden': 'true', class: 'h3d-sym' }, '☀'), slider, h('span', { 'aria-hidden': 'true', class: 'h3d-sym' }, '☾'), clock),
      h('div', { class: 'h3d-pop-row' }, nowBtn, play, presetSeg));
    function togglePop(v) {
      const on = v == null ? pop.hidden : v;
      pop.hidden = !on; timeBtn.setAttribute('aria-expanded', String(on));
      if (on) slider.focus({ preventScroll: true });
    }
    const onDocDown = (e) => { if (!pop.hidden && !pop.contains(e.target) && !timeBtn.contains(e.target)) togglePop(false); };
    document.addEventListener('pointerdown', onDocDown, true);

    const top = h('div', { class: 'h3d-top' }, head, complex ? blockSeg : h('span', { class: 'h3d-top-gap' }), tools);
    root.append(top, pop);

    let nowTimer = 0;
    function showClock(x) { clock.textContent = hhmm(x); slider.value = String(Math.max(5, Math.min(22, x))); }
    function setNow(on) {
      nowBtn.setAttribute('aria-pressed', String(on));
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

    // ---------- labels: the height — only over the chosen block (one house without blocks: over the house) ----------
    const fh = b.floorHeight ? Number(b.floorHeight) : null;
    const heightText = (floors) => S.fmt.n(floors) + ' ' + S.plural(floors, 'nr.floors') + ' · ≈ ' + S.fmt.n(Math.round(floors * (fh || 3.0))) + ' ' + t('h3d.m') + (fh ? '' : ' · ' + t('h3d.estimate'));
    if (!complex) {
      const floors = b.floorsCount || house.floors;
      house.addLabel(h('div', { class: 'h3d-tag h3d-tag--height' }, h('b', null, heightText(floors))), house.anchors().top, { group: 'house', prio: 10 });
    }
    // The block badges («Блок A») are the engine's; the chosen one also says its height.
    function drawBadges() {
      S.$$('.h3d-tag--block', root).forEach((el) => {
        const b0 = blockOf(el.dataset.block); if (!b0) return;
        el.replaceChildren(h('b', null, b0.name || t('blk.block', { c: b0.code })), cur === b0.code ? h('small', null, ' · ' + heightText(b0.floors)) : null);
        el.classList.toggle('is-current', cur === b0.code);
      });
      house.labelsChanged();
    }

    // ---------- camera views (bottom centre; on a phone — in the sheet) ----------
    const viewSeg = segment('h3d-seg--views h3d-views', t('h3d.views'), 'data-view', () => house.views.filter((v) => v !== 'area' || house.hasContext).map((v) => [v, t('h3d.view.' + v)]), (v) => house.setView(v));
    house.on('view', (v) => { viewSeg.draw(v); });
    root.appendChild(viewSeg);

    // ---------- the floor scale of the chosen block (right) ----------
    const floorsBox = h('nav', { class: 'h3d-floors', 'aria-label': t('h3d.floorScale'), hidden: true });
    root.appendChild(floorsBox);
    let floorSel = null;
    function drawFloors() {
      floorsBox.hidden = !cur;
      if (!cur) { floorsBox.replaceChildren(); return; }
      const mine = ctx.apts().filter((a) => a.block === cur);
      const nums = [...new Set(mine.map((a) => a.floor))].sort((x, y) => y - x);
      floorsBox.replaceChildren(...nums.map((n) => {
        const free = mine.some((a) => a.floor === n && a.status === 'free');
        return h('button', { type: 'button', class: 'h3d-floor', 'aria-pressed': String(floorSel === n), 'aria-label': t('floor.n', { n }) + (free ? ' · ' + t('status.free') : ''), onclick: () => pickFloor(n) },
          h('span', null, String(n)), free ? h('i', { class: 'h3d-floor-dot', 'aria-hidden': 'true' }) : null);
      }));
    }
    function pickFloor(n) {
      floorSel = floorSel === n ? null : n;
      if (floorSel != null) house.selectFloor(floorSel, cur);
      drawFloors(); drawList();
    }

    function pickBlock(code) {
      cur = code; floorSel = null;
      house.focusBlock(code);
      blockSeg.draw(code); drawTitle(); drawBadges(); drawFloors(); drawList();
      // The list opens with a block and folds for the whole complex (on a phone it stays as the user left it).
      if (!phone()) toggleList(!!code);
    }

    // ---------- the list of apartments: a folding panel 340 px on the left (a bottom sheet on a phone) ----------
    const phone = () => window.matchMedia('(max-width: 768px)').matches;
    const listBox = h('div', { class: 'h3d-items', role: 'listbox', 'aria-label': t('h3d.list'), tabindex: '0' });
    const filters = h('div', { class: 'h3d-filters' });
    const listTitle = h('b', { class: 'h3d-list-title' });
    const sheetSum = h('span', { class: 'h3d-sheet-sum' });
    // The tab on the edge (desktop) and the handle of the sheet (phone) — one button.
    const listTab = h('button', { class: 'h3d-list-tab', type: 'button', 'aria-expanded': 'false', onclick: () => toggleList() }, h('span', { class: 'h3d-sheet-grip', 'aria-hidden': 'true' }), h('span', { class: 'h3d-list-tab-t' }), sheetSum);
    const listFold = h('button', { class: 'h3d-tool h3d-list-fold', type: 'button', title: t('h3d.listFold'), 'aria-label': t('h3d.listFold'), onclick: () => toggleList(false) }, S.icon('x'));
    const sheetViews = h('div', { class: 'h3d-sheet-views' });
    const list = h('aside', { class: 'h3d-list', 'aria-label': t('h3d.list') }, h('div', { class: 'h3d-list-head' }, listTitle, listFold), sheetViews, filters, listBox);
    root.append(list, listTab);
    let listOpen = false;
    function toggleList(v) {
      listOpen = v == null ? !listOpen : v;
      root.classList.toggle('list-open', listOpen);
      listTab.setAttribute('aria-expanded', String(listOpen));
    }
    // The views go into the sheet on a phone, under the scene on a wider screen.
    function place() { const want = phone() ? sheetViews : root; if (viewSeg.parentNode !== want) want.appendChild(viewSeg); }
    const match = (a) => ctx.matches(a);
    const cur$ = () => ((ctx.apts()[0] || {}).currency);
    function drawFilters() {
      const opts = ctx.roomOptions(ctx.apts());
      const pills = h('div', { class: 'h3d-pills', role: 'group', 'aria-label': t('filter.rooms') }, opts.map(([v, l]) => h('button', { type: 'button', class: 'h3d-chip', 'aria-pressed': String(st.rooms === v), onclick: () => { st.rooms = v; changed(); } }, l)));
      const sw = h('button', { type: 'button', class: 'h3d-switch', role: 'switch', 'aria-checked': String(!!st.onlyFree), onclick: () => { st.onlyFree = !st.onlyFree; changed(); } }, h('span', { class: 'h3d-switch-track', 'aria-hidden': 'true' }, h('i')), t('filter.onlyFree'));
      const all = ctx.apts();
      const lo = all.length ? Math.floor(Math.min(...all.map((a) => a.price)) / 10000) * 10000 : 0;
      const out = h('b', null, S.fmt.money(st.priceCap, cur$()));
      const price = h('input', { type: 'range', class: 'h3d-range', min: lo, max: st.maxPrice, step: 10000, value: st.priceCap, 'aria-label': t('filter.priceTo') });
      price.addEventListener('input', () => { st.priceCap = +price.value; out.textContent = S.fmt.money(st.priceCap, cur$()); drawList(); applyFilter(); });
      price.addEventListener('change', () => ctx.onFilter());
      filters.replaceChildren(pills,
        h('div', { class: 'h3d-price' }, h('span', null, t('filter.priceTo')), out), price,
        h('div', { class: 'h3d-price-ends', 'aria-hidden': 'true' }, h('span', null, S.fmt.money(lo, cur$())), h('span', null, S.fmt.money(st.maxPrice, cur$()))),
        sw);
    }
    function changed() { drawFilters(); drawList(); applyFilter(); ctx.onFilter(); }
    function applyFilter() { house.setFilter(match); }
    let rows = [], active = -1;
    function drawList() {
      const mine = ctx.apts().filter(inBlock);
      const l = mine.filter((a) => match(a) && (floorSel == null || a.floor === floorSel))
        .sort((x, y) => (x.block || '').localeCompare(y.block || '') || y.floor - x.floor || x.number - y.number);
      listTitle.textContent = t('h3d.listN', { n: l.length });
      S.$('.h3d-list-tab-t', listTab).textContent = t('h3d.listBtn', { n: l.length });
      const prices = l.map((a) => a.price).filter((p) => p > 0);
      sheetSum.textContent = prices.length ? t('home.chip.from', { p: S.fmt.money(Math.min(...prices), cur$()) }) : '';
      rows = l;
      listBox.replaceChildren(...(l.length ? l.map((a, k) => {
        const eq = eqOf(a);
        const item = h('div', { class: 'h3d-item st-' + a.status, role: 'option', id: 'h3d-opt-' + k, 'aria-selected': String(!!sel && sel.id === a.id), 'aria-label': S.unitName(a), onclick: () => choose(a, true) },
          h('span', { class: 'h3d-item-top' }, h('b', { class: 'h3d-item-no' }, a.block ? S.aptNum(a) : t('nr.cat.aptN', { n: a.number })),
            a.kind === 'penthouse' ? h('span', { class: 'chip warn h3d-item-ph' }, t('kind.penthouseOne')) : null,
            h('span', { class: 'h3d-item-st' }, h('i', { class: 'nr-dot ' + a.status }), t('status.' + a.status))),
          h('span', { class: 'h3d-item-meta' }, [t('floor.n', { n: a.floor }), S.fmt.rooms(a.rooms), S.fmt.area(a.area)].join(' · ')),
          h('span', { class: 'h3d-item-price' }, S.fmt.money(a.price, a.currency), eq ? h('small', null, eq) : null));
        item.addEventListener('pointerenter', () => house.hoverApartment(a.id));
        item.addEventListener('pointerleave', () => house.hoverApartment(null));
        return item;
      }) : [h('p', { class: 'h3d-empty' }, t('list.none'))]));
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
        house.hoverApartment(rows[active].id);
      } else if (e.key === 'Enter' && active >= 0) { e.preventDefault(); choose(rows[active], true); }
    });
    listBox.addEventListener('blur', () => house.hoverApartment(null));
    // Price in dollars: by the National Bank rate from the API, marked as an estimate; no rate — no dollars.
    const eqOf = (a) => S.priceEq(a.price, a.currency, ctx.rate);
    const conceptNote = h('p', { class: 'h3d-concept' }, t('viz.concept'));
    root.appendChild(conceptNote);

    // ---------- the price card (right, bottom) ----------
    const card = h('section', { class: 'h3d-card', 'aria-live': 'polite', hidden: true });
    root.appendChild(card);
    let sel = null;
    function choose(a, fly) {
      sel = a;
      house.selectApartment(a.id, !!fly);
      ctx.onPick(a);
      drawCard(); drawList();
      if (fly && phone()) toggleList(false);
    }
    function drawCard() {
      if (!sel) { card.hidden = true; return; }
      const a = sel, d = eqOf(a);
      card.hidden = false;
      const phone = ctx.phone;
      card.replaceChildren(
        h('button', { class: 'h3d-icon h3d-card-x', type: 'button', 'aria-label': t('common.close'), onclick: () => { sel = null; house.clearSelection(); drawCard(); drawList(); } }, '×'),
        h('div', { class: 'h3d-card-kicker' }, S.unitName(a) + ' · ' + S.floorName({ number: a.floor, parking: a.floor < 0 })),
        h('div', { class: 'h3d-card-type' }, S.roomsKind.label(a.rooms) + ' · ' + S.fmt.area(a.area)),
        h('div', { class: 'h3d-card-price' }, S.fmt.money(a.price, a.currency)),
        d ? h('div', { class: 'h3d-card-usd' }, d + ' · ' + t('price.estimate')) : null,
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
        if (!S.plans || !house.enterInside(a.id, S.plans.layout(S.roomsKind.group(a.rooms), a.area, a.layoutCode), { furniture: furnBtn.getAttribute('aria-pressed') === 'true' })) S.toast(t('h3d.insideFail'), 'err');
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
      viewSeg.draw(house.currentView);
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

    // ---------- keys: Esc closes the time card, then the list, then the 3D ----------
    const onKey = (e) => {
      if (e.key !== 'Escape' || document.querySelector('.overlay')) return;
      e.preventDefault();
      if (!pop.hidden) { togglePop(false); timeBtn.focus({ preventScroll: true }); }
      else if (house.mode === 'walk') house.leaveWalk();
      else if (house.mode === 'inside') house.leaveInside();
      else if (listOpen && (complex || phone())) { toggleList(false); listTab.focus({ preventScroll: true }); }
      else ctx.requestClose();
    };
    document.addEventListener('keydown', onKey);

    let alive = true;
    house.setFocusMode(true); house.setAutoRotate(false);
    // The glass panels (a blur over the canvas) only on a fast GPU: two seconds at ≥ 30 fps on full quality; a step down of
    // the quality takes it away again (styles.css, .h3d-glass).
    let fast = 0;
    house.on('fps', (f, q) => { fast = q >= 2 && f >= 30 ? fast + 1 : 0; if (fast >= 2) root.classList.add('h3d-glass'); if (q < 2) root.classList.remove('h3d-glass'); });
    // Labels stay out of the controls: the engine skips (or moves) a label over any of these boxes.
    const avoid = [top, pop, viewSeg, floorsBox, list, listTab, card, conceptNote, poiBar, attribution, insideBar];
    house.setLabelAvoid(() => {
      const o = root.getBoundingClientRect();
      return avoid.filter((x) => x.isConnected && !x.hidden && x.offsetParent !== null).map((x) => { const r = x.getBoundingClientRect(); return { x0: r.left - o.left, y0: r.top - o.top, x1: r.right - o.left, y1: r.bottom - o.top }; }).filter((r) => r.x1 > r.x0 && r.y1 > r.y0);
    });
    const onResize = () => place();
    window.addEventListener('resize', onResize);
    place();
    drawTitle(); blockSeg.draw(null); viewSeg.draw(house.currentView); drawFilters(); drawList(); applyFilter(); drawBadges();
    if (ctx.selected) { sel = ctx.selected; drawCard(); drawList(); }
    // One house without blocks: the list is open on a wide screen, as before; the complex starts folded.
    if (!complex && !phone()) toggleList(true);
    house.setView('overview', 0.9);
    setNow(true);
    loadContext();
    root.dataset.mode = 'house'; walkBack.hidden = true;
    setTimeout(() => back.focus({ preventScroll: true }), 50);
    window.__house3dMode = { root, choose: (id) => { const a = ctx.apts().find((x) => x.id === id); if (a) choose(a, true); }, pickBlock };

    return {
      el: root,
      refresh() { if (sel) sel = ctx.apts().find((x) => x.id === sel.id) || null; drawFilters(); drawList(); drawFloors(); drawCard(); applyFilter(); },
      close() {
        if (!alive) return; alive = false;
        clearInterval(nowTimer); stopPlay();
        document.removeEventListener('keydown', onKey); document.removeEventListener('pointerdown', onDocDown, true); document.removeEventListener('fullscreenchange', onFs);
        window.removeEventListener('resize', onResize); house.setLabelAvoid(null); house.hoverApartment(null);
        if (document.fullscreenElement === root) document.exitFullscreen().catch(() => {});
        if (house.mode !== 'house') { house.leaveInside(); }
        house.removeLabels('poi'); house.removeLabels('house'); house.removeLabels('room');
        cur = null; drawBadges(); house.focusBlock(null);
        if (house.parking) house.setParking(false);
        if (house.state().xray) house.setXray(false);
        house.setFilter(null); house.setFocusMode(false);
        house.on('select', ctx.onModelSelect || (() => {})); house.on('view', () => {}); house.on('mode', () => {}); house.on('fps', () => {});
        if (next && next.parentNode === home) home.insertBefore(ctx.box, next); else home.appendChild(ctx.box);
        root.remove(); document.documentElement.classList.remove('h3d-lock');
        delete window.__house3dMode;
        if (lastFocus && lastFocus.focus) lastFocus.focus({ preventScroll: true });
      },
    };
  }

  S.House3DMode = { open };
})();
