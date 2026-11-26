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

  S.blockForms = { shape, draw };
})();
