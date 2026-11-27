/* CRM → «Каталог» → a block of the complex: «Форма и место на участке». The block's letter and order, its outline in
   metres as JSON ([[x, y], …], a point may carry the corner radius: [x, y, r]), the setbacks of the upper floors, and the
   place on the common site (x, y, turn). A 2D preview on a canvas shows the block among the other blocks of the
   organization, its area and the gap to the nearest one; the server checks it all again (PUT /buildings/{id}/shape) and
   answers 400 with the reason. Colours come from the page tokens (light and dark theme). */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a), t = (k, p) => S.t(k, p);

  function parse(text) {
    const s = String(text || '').trim();
    if (!s) return { value: null };
    try { return { value: JSON.parse(s) }; } catch (e) { return { error: true }; }
  }

  // The site with every block placed; this one highlighted, its setbacks dashed; a scale bar of 10 m.
  function draw(canvas, me, others) {
    const W = 520, H = 320, dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = W * dpr; canvas.height = H * dpr; canvas.style.width = '100%'; canvas.style.maxWidth = W + 'px';
    const g = canvas.getContext('2d'); if (!g || !S.geom) return null;
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    const css = getComputedStyle(canvas.isConnected ? canvas : document.documentElement);
    const tok = (n, f) => (css.getPropertyValue(n) || '').trim() || f;
    const paper = tok('--surface', '#fbf8f2'), ink = tok('--ink', '#1b1815'), muted = tok('--muted', '#6b6258'), accent = tok('--accent-text', '#8a4e2a');
    g.fillStyle = paper; g.fillRect(0, 0, W, H);
    const rings = [];
    const placeOf = (b) => (b.footprint && b.footprint.length >= 3 ? S.geom.place(S.geom.expand(b.footprint), b.sitePosition || { x: 0, y: 0, rotationDeg: 0 }) : null);
    others.forEach((b) => { const r = placeOf(b); if (r) rings.push({ r, b, me: false }); });
    const mine = me.footprint ? S.geom.place(S.geom.expand(me.footprint), me.sitePosition) : null;
    if (mine) rings.push({ r: mine, b: me, me: true });
    if (!rings.length) { g.fillStyle = muted; g.font = '13px Manrope, system-ui, sans-serif'; g.fillText(t('blk.noShape'), 16, 28); return null; }
    const all = rings.flatMap((x) => x.r), bb = S.geom.bounds(all), pad = 24;
    const k = Math.min((W - pad * 2) / Math.max(1, bb.x1 - bb.x0), (H - pad * 2) / Math.max(1, bb.y1 - bb.y0));
    const X = (x) => pad + (x - bb.x0) * k, Y = (y) => pad + (y - bb.y0) * k;
    const path = (ring) => { g.beginPath(); ring.forEach((p, i) => (i ? g.lineTo(X(p.x), Y(p.y)) : g.moveTo(X(p.x), Y(p.y)))); g.closePath(); };
    rings.forEach(({ r, b, me: isMe }) => {
      path(r);
      g.globalAlpha = isMe ? 0.22 : 0.14; g.fillStyle = isMe ? accent : muted; g.fill(); g.globalAlpha = 1;
      g.lineWidth = isMe ? 2 : 1; g.strokeStyle = isMe ? accent : muted; g.stroke();
      const c = r.reduce((a, p) => ({ x: a.x + p.x / r.length, y: a.y + p.y / r.length }), { x: 0, y: 0 });
      g.fillStyle = isMe ? accent : muted; g.font = '700 14px Manrope, system-ui, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle';
      g.fillText(b.blockCode || '·', X(c.x), Y(c.y));
    });
    (me.setbacks || []).forEach((s) => {
      if (!s.footprint || s.footprint.length < 3) return;
      path(S.geom.place(S.geom.expand(s.footprint), me.sitePosition));
      g.setLineDash([5, 4]); g.strokeStyle = ink; g.lineWidth = 1; g.stroke(); g.setLineDash([]);
    });
    g.fillStyle = ink; g.fillRect(pad, H - 12, 10 * k, 2);
    g.font = '11px Manrope, system-ui, sans-serif'; g.textAlign = 'left'; g.fillText('10 ' + t('u.m'), pad + 10 * k + 6, H - 11);
    if (!mine) return null;
    const near = others.map((b) => placeOf(b)).filter(Boolean).map((r) => S.geom.gap(mine, r));
    return { area: S.geom.area(mine), gap: near.length ? Math.min(...near) : null };
  }

  function shape(b, buildings) {
    const others = (buildings || []).filter((x) => x.id !== b.id && x.footprint);
    const code = S.input('blockCode', { value: b.blockCode || '', maxlength: 4, placeholder: 'A' });
    const order = S.input('sortOrder', { type: 'number', min: 0, max: 1000, value: b.sortOrder || 0 });
    const pos = b.sitePosition || { x: 0, y: 0, rotationDeg: 0 };
    const num = (name, v, step) => S.input(name, { type: 'number', step: step || 'any', inputmode: 'decimal', value: v == null ? '' : v });
    const px = num('x', pos.x), py = num('y', pos.y), rot = num('rotationDeg', pos.rotationDeg, '1');
    const fp = h('textarea', { class: 'inp mono', name: 'footprint', id: 'blk-fp', rows: 4, spellcheck: 'false' }); fp.value = b.footprint ? JSON.stringify(b.footprint) : '';
    const sb = h('textarea', { class: 'inp mono', name: 'setbacks', id: 'blk-sb', rows: 3, spellcheck: 'false' }); sb.value = b.setbacks && b.setbacks.length ? JSON.stringify(b.setbacks) : '';
    const canvas = h('canvas', { class: 'blk-preview', role: 'img', 'aria-label': t('blk.preview'), width: 520, height: 320 });
    const facts = h('p', { class: 'hint blk-facts', 'aria-live': 'polite' });
    const btn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
    const form = h('form', { class: 'card stack blk-form', novalidate: true },
      h('h3', null, t('blk.title')), h('p', { class: 'muted' }, t('blk.hint')),
      h('div', { class: 'grid g2' }, S.field(t('blk.code'), code, t('blk.codeHint')), S.field(t('blk.order'), order)),
      S.field(t('blk.footprint'), fp, t('blk.footprintHint')),
      S.field(t('blk.setbacks'), sb, t('blk.setbacksHint')),
      h('div', { class: 'grid g3' }, S.field(t('blk.x'), px), S.field(t('blk.y'), py), S.field(t('blk.rot'), rot)),
      canvas, facts, h('p', { class: 'hint' }, t('viz.concept')), h('div', null, btn));
    const val = (el) => (el.value.trim() === '' ? 0 : Number(el.value.replace(',', '.')));
    function current() {
      const f = parse(fp.value), s = parse(sb.value);
      return { f, s, body: { blockCode: code.value.trim() || null, sortOrder: Math.round(val(order)), footprint: f.value || null, sitePosition: { x: val(px), y: val(py), rotationDeg: val(rot) }, setbacks: s.value || null } };
    }
    function preview() {
      const c = current();
      const ok = !c.f.error && !c.s.error && (!c.f.value || (Array.isArray(c.f.value) && c.f.value.length >= 3 && c.f.value.every((p) => Array.isArray(p) && p.length >= 2)));
      const r = ok ? draw(canvas, Object.assign({}, b, c.body, { setbacks: Array.isArray(c.body.setbacks) ? c.body.setbacks : [] }), others) : null;
      facts.textContent = !ok ? t('blk.jsonErr') : r ? t('blk.facts', { a: S.fmt.n(r.area), n: c.f.value.length }) + (r.gap != null ? ' · ' + t('blk.gap', { d: (Math.round(r.gap * 10) / 10).toLocaleString('ru-RU') }) : '') : '';
    }
    [fp, sb, px, py, rot, code].forEach((el) => el.addEventListener('input', preview));
    form.addEventListener('submit', async (e) => {
      e.preventDefault(); S.showErrors(form, null);
      const c = current();
      if (c.f.error || c.s.error) { S.showErrors(form, { message: t('blk.jsonErr'), errors: { [c.f.error ? 'footprint' : 'setbacks']: [t('blk.jsonErr')] } }); return; }
      await S.busy(btn, async () => {
        try {
          const r = await S.api.saveBuildingShape(b.id, c.body);
          Object.assign(b, { blockCode: r.blockCode, sortOrder: r.sortOrder, footprint: r.footprint, sitePosition: r.sitePosition, setbacks: r.setbacks });
          S.toast(t('common.saved'), 'ok');
        } catch (er) { S.showErrors(form, er); }
      });
    });
    requestAnimationFrame(preview);
    return form;
  }

  // ---------- prices by floor ----------
  // The price per m² of floor n by the rule (the same as PriceRules.PricePerM2 on the server): a step with «до» changes evenly
  // by floor, rounded to 1; below the first step — its price, above the last — its last price; the top floor — penthouses.
  function ppmOf(steps, n, floors, penthouse) {
    if (n === floors && penthouse > 0) return penthouse;
    const list = (steps || []).filter((s) => s.fromFloor > 0 && s.toFloor >= s.fromFloor && s.pricePerM2 > 0).slice().sort((a, b) => a.fromFloor - b.fromFloor);
    if (!list.length) return null;
    const s = list.find((x) => n >= x.fromFloor && n <= x.toFloor);
    if (!s) return n < list[0].fromFloor ? list[0].pricePerM2 : (list[list.length - 1].pricePerM2To || list[list.length - 1].pricePerM2);
    if (!(s.pricePerM2To > 0) || s.toFloor === s.fromFloor) return s.pricePerM2;
    return Math.round(s.pricePerM2 + (s.pricePerM2To - s.pricePerM2) * (n - s.fromFloor) / (s.toFloor - s.fromFloor));
  }

  // A bar per floor: the price per m², the top floor (penthouses) in the accent colour.
  function chart(canvas, steps, floors, penthouse, cur) {
    const W = 520, H = 180, dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = W * dpr; canvas.height = H * dpr; canvas.style.width = '100%'; canvas.style.maxWidth = W + 'px';
    const g = canvas.getContext('2d'); if (!g) return;
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    const css = getComputedStyle(canvas.isConnected ? canvas : document.documentElement);
    const tok = (n, f) => (css.getPropertyValue(n) || '').trim() || f;
    g.fillStyle = tok('--surface', '#fbf8f2'); g.fillRect(0, 0, W, H);
    const vals = Array.from({ length: floors }, (_, i) => ppmOf(steps, i + 1, floors, penthouse));
    const max = Math.max(1, ...vals.filter(Boolean)), bw = (W - 40) / floors;
    g.font = '10px Manrope, system-ui, sans-serif'; g.textAlign = 'center';
    vals.forEach((v, i) => {
      if (!v) return;
      const hh = (H - 40) * v / max, x = 30 + i * bw;
      g.fillStyle = i === floors - 1 ? tok('--accent-text', '#8a4e2a') : tok('--muted', '#6b6258');
      g.fillRect(x + 1, H - 20 - hh, Math.max(1, bw - 2), hh);
      if (floors <= 30 || i % 2 === 0) { g.fillStyle = tok('--ink', '#1b1815'); g.fillText(String(i + 1), x + bw / 2, H - 6); }
    });
    g.textAlign = 'left'; g.fillStyle = tok('--ink', '#1b1815'); g.fillText(S.fmt.money(max, cur) + ' / ' + t('u.m2'), 30, 12);
  }

  function prices(b, onApplied) {
    const rows = h('tbody');
    const cur = S.select('currency', [['USD', 'USD'], ['TJS', 'TJS']], b.priceCurrency || 'USD');
    const pent = S.input('penthousePricePerM2', { type: 'number', min: 0, step: '1', inputmode: 'decimal', value: b.penthousePricePerM2 == null ? '' : b.penthousePricePerM2 });
    const canvas = h('canvas', { class: 'blk-price-chart', role: 'img', 'aria-label': t('blkp.chart'), width: 520, height: 180 });
    const n = (v) => S.input('n', { type: 'number', min: 0, step: '1', inputmode: 'decimal', value: v == null ? '' : v, 'aria-label': ' ' });
    function addRow(s) {
      const from = n(s && s.fromFloor), to = n(s && s.toFloor), p = n(s && s.pricePerM2), pTo = n(s && s.pricePerM2To);
      from.setAttribute('aria-label', t('blkp.from')); to.setAttribute('aria-label', t('blkp.to')); p.setAttribute('aria-label', t('blkp.ppm')); pTo.setAttribute('aria-label', t('blkp.ppmTo'));
      const tr = h('tr', null, h('td', null, from), h('td', null, to), h('td', null, p), h('td', null, pTo),
        h('td', null, h('button', { class: 'btn ghost sm', type: 'button', 'aria-label': t('blkp.remove'), onclick: () => { tr.remove(); redraw(); } }, '×')));
      [from, to, p, pTo].forEach((el) => el.addEventListener('input', redraw));
      rows.append(tr);
    }
    const num = (el) => (el.value.trim() === '' ? null : Number(el.value.replace(',', '.')));
    const read = () => [...rows.children].map((tr) => { const i = tr.querySelectorAll('input'); return { fromFloor: num(i[0]), toFloor: num(i[1]), pricePerM2: num(i[2]), pricePerM2To: num(i[3]) }; })
      .filter((s) => s.fromFloor != null && s.toFloor != null && s.pricePerM2 != null);
    function redraw() { chart(canvas, read(), b.floorsCount || 1, num(pent), cur.value); }
    (b.priceSteps || []).forEach(addRow);
    if (!rows.children.length) addRow(null);
    pent.addEventListener('input', redraw); cur.addEventListener('change', redraw);
    const save = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
    const apply = h('button', { class: 'btn ghost', type: 'button' }, t('blkp.apply'));
    const form = h('form', { class: 'card stack blk-prices', novalidate: true },
      h('h3', null, t('blkp.title')), h('p', { class: 'muted' }, t('blkp.hint')),
      h('div', { class: 'tbl-wrap' }, h('table', { class: 'tbl' }, h('thead', null, h('tr', null, h('th', null, t('blkp.from')), h('th', null, t('blkp.to')), h('th', null, t('blkp.ppm')), h('th', null, t('blkp.ppmTo')), h('th'))), rows)),
      h('div', null, h('button', { class: 'btn ghost sm', type: 'button', onclick: () => { addRow(null); } }, t('blkp.add'))),
      h('div', { class: 'grid g2' }, S.field(t('blkp.penthouse'), pent, t('blkp.penthouseHint')), S.field(t('blkp.currency'), cur)),
      canvas, h('div', { class: 'row' }, save, apply));
    form.addEventListener('submit', async (e) => {
      e.preventDefault(); S.showErrors(form, null);
      await S.busy(save, async () => {
        try { const r = await S.api.savePriceRule(b.id, { currency: cur.value, steps: read(), penthousePricePerM2: num(pent) }); Object.assign(b, { priceSteps: r.priceSteps, penthousePricePerM2: r.penthousePricePerM2, priceCurrency: r.priceCurrency }); S.toast(t('common.saved'), 'ok'); }
        catch (er) { S.showErrors(form, er); }
      });
    });
    apply.addEventListener('click', () => {
      const md = S.modal({ title: t('blkp.apply'), body: h('p', null, t('blkp.applyText')), actions: [
        h('button', { class: 'btn ghost', type: 'button', onclick: () => md.close() }, t('common.cancel')),
        h('button', { class: 'btn', type: 'button', onclick: (ev) => S.busy(ev.currentTarget, async () => {
          try { const r = await S.api.applyPriceRule(b.id); md.close(); S.toast(t('blkp.applied', { u: r.updated, s: r.skipped + r.unchanged }), 'ok'); if (onApplied) onApplied(); }
          catch (er) { md.close(); S.err(er); }
        }) }, t('blkp.applyGo'))] });
    });
    requestAnimationFrame(redraw);
    return form;
  }

  // ---------- the developer's layouts (JSON) ----------
  function layouts() {
    const list = h('div', { class: 'stack' });
    const code = S.input('code', { maxlength: 32, placeholder: 'a-corner-4' });
    const name = S.input('name', { maxlength: 200 });
    const json = h('textarea', { class: 'inp mono', name: 'layout', id: 'lay-json', rows: 8, spellcheck: 'false' });
    const canvas = h('canvas', { class: 'lay-preview', role: 'img', 'aria-label': t('lay.preview'), width: 360, height: 240 });
    const facts = h('p', { class: 'hint', 'aria-live': 'polite' });
    const btn = h('button', { class: 'btn', type: 'submit' }, t('common.save'));
    const form = h('form', { class: 'card stack lay-form', novalidate: true }, h('h3', null, t('lay.title')), h('p', { class: 'muted' }, t('lay.hint')),
      h('div', { class: 'grid g2' }, S.field(t('lay.code'), code, t('lay.codeHint')), S.field(t('lay.name'), name)), S.field(t('lay.json'), json, t('lay.jsonHint')),
      canvas, facts, h('div', null, btn), list);
    function parsed() { try { return JSON.parse(json.value); } catch (e) { return null; } }
    function preview() {
      const v = parsed();
      const errs = v ? S.plans.validate(v) : ['json'];
      if (errs.length) { facts.textContent = t('lay.bad'); const g = canvas.getContext('2d'); if (g) g.clearRect(0, 0, canvas.width, canvas.height); return false; }
      const tmp = 'preview-' + Date.now();
      S.plans.setReal([{ code: tmp, layout: v }]);
      const lay = S.site && S.site.drawFloorPlan ? S.site.drawFloorPlan(canvas, 0, 0, tmp, { areas: true }) : null;
      S.plans.setReal(lastList.map((x) => ({ code: x.code, layout: x.layout })));
      facts.textContent = lay ? t('lay.facts', { a: S.fmt.n(Math.round(lay.roomsArea * 10) / 10), n: lay.rooms.length }) : '';
      return true;
    }
    json.addEventListener('input', preview);
    let lastList = [];
    async function load() {
      try { lastList = await S.api.layouts(); } catch (e) { lastList = []; }
      S.plans.setReal(lastList.map((x) => ({ code: x.code, layout: x.layout })));
      list.replaceChildren(...(lastList.length ? lastList.map((x) => h('div', { class: 'row between lay-item' }, h('span', null, h('b', null, x.code), ' · ', x.name),
        h('span', { class: 'row' }, h('button', { class: 'btn ghost sm', type: 'button', onclick: () => { code.value = x.code; name.value = x.name; json.value = JSON.stringify(x.layout, null, 1); preview(); } }, t('common.edit')),
          h('button', { class: 'btn ghost sm', type: 'button', onclick: (e) => S.busy(e.currentTarget, async () => { try { await S.api.deleteLayout(x.code); S.toast(t('lay.deleted'), 'ok'); load(); } catch (er) { S.err(er); } }) }, t('lay.delete'))))) : [h('p', { class: 'hint' }, t('lay.none'))]));
    }
    form.addEventListener('submit', async (e) => {
      e.preventDefault(); S.showErrors(form, null);
      const v = parsed();
      if (!v || !preview()) { S.showErrors(form, { message: t('lay.bad'), errors: { layout: [t('lay.bad')] } }); return; }
      await S.busy(btn, async () => { try { await S.api.saveLayout(code.value.trim(), { name: name.value.trim() || null, layout: v }); S.toast(t('common.saved'), 'ok'); load(); } catch (er) { S.showErrors(form, er); } });
    });
    load();
    return form;
  }

  S.blockForms = { shape, draw, prices, ppmOf, layouts };
})();
