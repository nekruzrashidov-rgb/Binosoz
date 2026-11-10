/* Gallery (/gallery): eight scenes of the complex drawn with Canvas 2D (no photos yet) and the four floor plans.
   Filters by category; a click opens a lightbox with a larger drawing, arrows and the keyboard (← → Esc). */
(function () {
  const S = (window.S = window.S || {});
  const h = (...a) => S.h(...a);
  const t = (...a) => S.t(...a);
  S.views = S.views || {};

  // Repeatable "random" numbers: the same windows light up on every redraw and in the lightbox.
  function rng(seed) { let x = seed >>> 0 || 1; return () => ((x = (x * 1664525 + 1013904223) >>> 0) / 4294967296); }
  const lin = (g, x0, y0, x1, y1, stops) => { const gr = g.createLinearGradient(x0, y0, x1, y1); stops.forEach(([o, c]) => gr.addColorStop(o, c)); return gr; };
  const GOLD = '#c9a227', GOLD2 = '#f2d27a';

  // Each scene draws into a W×H area; sizes are proportional, so the card and the lightbox show the same picture.
  const SCENES = {
    facade(g, W, H) {
      g.fillStyle = lin(g, 0, 0, 0, H, [[0, '#0b1a3d'], [0.6, '#3a2f5c'], [1, '#c98a4b']]); g.fillRect(0, 0, W, H);
      const r = rng(7);
      for (let i = 0; i < 40; i++) { g.fillStyle = 'rgba(255,255,255,' + (0.3 + r() * 0.6) + ')'; g.fillRect(r() * W, r() * H * 0.45, 1.5, 1.5); }
      g.fillStyle = '#141d38'; [[0.05, 0.62, 0.14], [0.78, 0.58, 0.18]].forEach(([x, y, w]) => g.fillRect(x * W, y * H, w * W, H));
      const bx = W * 0.32, bw = W * 0.36, top = H * 0.12;
      g.fillStyle = lin(g, bx, 0, bx + bw, 0, [[0, '#1b2750'], [0.5, '#24346a'], [1, '#141d3d']]); g.fillRect(bx, top, bw, H - top);
      g.fillStyle = GOLD; g.fillRect(bx - W * 0.01, top - H * 0.02, bw + W * 0.02, H * 0.02);
      const cols = 6, rows = 18, cw = bw / cols, rh = (H - top - H * 0.08) / rows;
      for (let c = 0; c < cols; c++) for (let k = 0; k < rows; k++) {
        g.fillStyle = r() > 0.45 ? 'rgba(242, 196, 106,' + (0.55 + r() * 0.45) + ')' : 'rgba(120, 150, 220, 0.18)';
        g.fillRect(bx + c * cw + cw * 0.22, top + H * 0.03 + k * rh + rh * 0.2, cw * 0.56, rh * 0.55);
      }
      g.fillStyle = '#0a1024'; g.fillRect(0, H * 0.94, W, H * 0.06);
      g.fillStyle = GOLD2; g.fillRect(bx + bw * 0.4, H * 0.88, bw * 0.2, H * 0.06);
    },
    lobby(g, W, H) {
      g.fillStyle = lin(g, 0, 0, 0, H, [[0, '#2a2418'], [1, '#14110b']]); g.fillRect(0, 0, W, H);
      // Marble floor in perspective.
      g.fillStyle = lin(g, 0, H * 0.62, 0, H, [[0, '#d9d2c3'], [1, '#8f8676']]);
      g.beginPath(); g.moveTo(W * 0.3, H * 0.62); g.lineTo(W * 0.7, H * 0.62); g.lineTo(W, H); g.lineTo(0, H); g.closePath(); g.fill();
      g.strokeStyle = 'rgba(90, 80, 60, 0.35)'; g.lineWidth = 1;
      for (let i = 0; i <= 8; i++) { g.beginPath(); g.moveTo(W * (0.3 + i * 0.05), H * 0.62); g.lineTo(W * i / 8, H); g.stroke(); }
      g.fillStyle = '#3a3122'; g.fillRect(W * 0.3, H * 0.18, W * 0.4, H * 0.44);
      g.fillStyle = 'rgba(242, 210, 122, 0.25)'; g.fillRect(W * 0.42, H * 0.3, W * 0.16, H * 0.32);
      // Columns.
      [0.1, 0.22, 0.78, 0.9].forEach((x, i) => {
        const w = W * (i === 0 || i === 3 ? 0.07 : 0.05);
        g.fillStyle = lin(g, W * x - w / 2, 0, W * x + w / 2, 0, [[0, '#bdb4a2'], [0.5, '#f1ece0'], [1, '#a39a88']]);
        g.fillRect(W * x - w / 2, H * 0.06, w, H * 0.86);
        g.fillStyle = GOLD; g.fillRect(W * x - w / 2 - 2, H * 0.06, w + 4, H * 0.025); g.fillRect(W * x - w / 2 - 2, H * 0.9, w + 4, H * 0.025);
      });
      // Chandelier with a glow.
      const glow = g.createRadialGradient(W / 2, H * 0.16, 2, W / 2, H * 0.16, H * 0.3); glow.addColorStop(0, 'rgba(255, 220, 140, 0.8)'); glow.addColorStop(1, 'rgba(255, 220, 140, 0)');
      g.fillStyle = glow; g.fillRect(0, 0, W, H * 0.5);
      g.strokeStyle = GOLD; g.lineWidth = 1.5; g.beginPath(); g.moveTo(W / 2, 0); g.lineTo(W / 2, H * 0.1); g.stroke();
      g.fillStyle = GOLD2; for (let i = -3; i <= 3; i++) { g.beginPath(); g.arc(W / 2 + i * W * 0.025, H * 0.14 + Math.abs(i) * -H * 0.008, H * 0.012, 0, Math.PI * 2); g.fill(); }
    },
    apartment(g, W, H) {
      g.fillStyle = '#e9e3d8'; g.fillRect(0, 0, W, H);
      g.fillStyle = '#d8cfbf'; g.beginPath(); g.moveTo(0, 0); g.lineTo(W * 0.18, H * 0.15); g.lineTo(W * 0.18, H * 0.7); g.lineTo(0, H); g.closePath(); g.fill();
      g.fillStyle = '#9b7a55'; g.beginPath(); g.moveTo(W * 0.18, H * 0.7); g.lineTo(W, H * 0.7); g.lineTo(W, H); g.lineTo(0, H); g.closePath(); g.fill();
      // Two big windows with the city at dusk.
      [[0.28, 0.42], [0.62, 0.3]].forEach(([x, w]) => {
        g.fillStyle = lin(g, 0, H * 0.15, 0, H * 0.6, [[0, '#4b6cb7'], [1, '#f2a65a']]); g.fillRect(W * x, H * 0.15, W * w, H * 0.45);
        g.fillStyle = '#2b3150'; for (let i = 0; i < 6; i++) g.fillRect(W * x + i * W * w / 6, H * (0.42 + (i % 3) * 0.04), W * w / 7, H * 0.2);
        g.strokeStyle = '#fff'; g.lineWidth = 3; g.strokeRect(W * x, H * 0.15, W * w, H * 0.45);
      });
      // Sofa, table and a lamp.
      g.fillStyle = '#34405e'; g.fillRect(W * 0.3, H * 0.6, W * 0.38, H * 0.12); g.fillRect(W * 0.3, H * 0.54, W * 0.38, H * 0.08);
      g.fillStyle = '#6b4f33'; g.fillRect(W * 0.4, H * 0.78, W * 0.18, H * 0.04);
      g.fillStyle = GOLD; g.fillRect(W * 0.78, H * 0.38, W * 0.006, H * 0.32); g.beginPath(); g.moveTo(W * 0.75, H * 0.38); g.lineTo(W * 0.81, H * 0.38); g.lineTo(W * 0.795, H * 0.31); g.lineTo(W * 0.765, H * 0.31); g.fill();
    },
    kitchen(g, W, H) {
      g.fillStyle = '#f1ede6'; g.fillRect(0, 0, W, H);
      g.fillStyle = '#c4b49a'; g.fillRect(0, H * 0.72, W, H * 0.28);
      // Upper and lower cabinets, a hood and an island.
      g.fillStyle = '#26314f'; g.fillRect(W * 0.05, H * 0.12, W * 0.5, H * 0.18);
      g.fillStyle = '#1c2540'; g.fillRect(W * 0.05, H * 0.46, W * 0.5, H * 0.26);
      g.fillStyle = '#e6e0d4'; g.fillRect(W * 0.05, H * 0.43, W * 0.5, H * 0.03);
      g.strokeStyle = GOLD; g.lineWidth = 1.5;
      for (let i = 1; i < 5; i++) { g.beginPath(); g.moveTo(W * (0.05 + i * 0.1), H * 0.12); g.lineTo(W * (0.05 + i * 0.1), H * 0.3); g.moveTo(W * (0.05 + i * 0.1), H * 0.46); g.lineTo(W * (0.05 + i * 0.1), H * 0.72); g.stroke(); }
      g.fillStyle = '#9aa3b8'; g.beginPath(); g.moveTo(W * 0.25, H * 0.12); g.lineTo(W * 0.35, H * 0.12); g.lineTo(W * 0.38, H * 0.28); g.lineTo(W * 0.22, H * 0.28); g.fill();
      g.fillStyle = '#f7f4ee'; g.fillRect(W * 0.2, H * 0.62, W * 0.4, H * 0.06); g.fillStyle = '#2a2f45'; g.fillRect(W * 0.22, H * 0.68, W * 0.36, H * 0.16);
      [0.27, 0.4, 0.53].forEach((x) => { g.fillStyle = GOLD; g.fillRect(W * x, H * 0.04, 1.5, H * 0.14); g.beginPath(); g.arc(W * x + 0.75, H * 0.2, H * 0.025, 0, Math.PI * 2); g.fill(); });
      // Living part: window and armchair.
      g.fillStyle = lin(g, 0, H * 0.1, 0, H * 0.62, [[0, '#7fb2e5'], [1, '#e8f1fb']]); g.fillRect(W * 0.66, H * 0.1, W * 0.28, H * 0.5);
      g.strokeStyle = '#fff'; g.lineWidth = 3; g.strokeRect(W * 0.66, H * 0.1, W * 0.28, H * 0.5);
      g.fillStyle = '#b5893f'; g.fillRect(W * 0.7, H * 0.62, W * 0.18, H * 0.12);
    },
    roof(g, W, H) {
      g.fillStyle = lin(g, 0, 0, 0, H, [[0, '#f4a259'], [0.45, '#f7c97e'], [1, '#8ec1e8']]); g.fillRect(0, 0, W, H);
      g.fillStyle = 'rgba(255, 240, 200, 0.9)'; g.beginPath(); g.arc(W * 0.8, H * 0.22, H * 0.07, 0, Math.PI * 2); g.fill();
      // Mountains around Dushanbe and the city below.
      g.fillStyle = '#7f8fb0'; g.beginPath(); g.moveTo(0, H * 0.5);
      [[0.1, 0.42], [0.22, 0.34], [0.38, 0.46], [0.55, 0.3], [0.7, 0.44], [0.85, 0.36], [1, 0.48]].forEach(([x, y]) => g.lineTo(W * x, H * y));
      g.lineTo(W, H * 0.6); g.lineTo(0, H * 0.6); g.fill();
      g.fillStyle = '#eef2fa'; [[0.22, 0.34], [0.55, 0.3], [0.85, 0.36]].forEach(([x, y]) => { g.beginPath(); g.moveTo(W * x, H * y); g.lineTo(W * (x - 0.03), H * (y + 0.05)); g.lineTo(W * (x + 0.03), H * (y + 0.05)); g.fill(); });
      const r = rng(21);
      for (let i = 0; i < 46; i++) {
        const x = r() * W, w = W * (0.02 + r() * 0.04), hh = H * (0.06 + r() * 0.2);
        g.fillStyle = r() > 0.5 ? '#4b5878' : '#5d6a8c'; g.fillRect(x, H * 0.82 - hh, w, hh + H * 0.2);
      }
      // Roof railing with gold top.
      g.fillStyle = 'rgba(10, 18, 40, 0.85)'; g.fillRect(0, H * 0.86, W, H * 0.14);
      g.fillStyle = GOLD; g.fillRect(0, H * 0.84, W, H * 0.015);
      for (let i = 0; i < 24; i++) g.fillRect(i * W / 24, H * 0.84, 1.5, H * 0.06);
    },
    parking(g, W, H) {
      g.fillStyle = '#3b3f48'; g.fillRect(0, 0, W, H);
      g.fillStyle = lin(g, 0, H * 0.45, 0, H, [[0, '#5a5f6a'], [1, '#2d3038']]);
      g.beginPath(); g.moveTo(W * 0.2, H * 0.45); g.lineTo(W * 0.8, H * 0.45); g.lineTo(W, H); g.lineTo(0, H); g.fill();
      g.fillStyle = '#4a4e58'; g.fillRect(0, 0, W, H * 0.08);
      // Ceiling lights in a row.
      for (let i = 0; i < 5; i++) {
        const x = W * (0.15 + i * 0.175), y = H * 0.12;
        const glow = g.createRadialGradient(x, y, 1, x, y, H * 0.18); glow.addColorStop(0, 'rgba(255, 250, 230, 0.6)'); glow.addColorStop(1, 'rgba(255, 250, 230, 0)');
        g.fillStyle = glow; g.fillRect(x - H * 0.2, 0, H * 0.4, H * 0.4);
        g.fillStyle = '#fffbe8'; g.fillRect(x - W * 0.04, y - 2, W * 0.08, 4);
      }
      // Columns and parking lines in yellow.
      g.fillStyle = '#6c707b'; [0.12, 0.88].forEach((x) => g.fillRect(W * x - W * 0.03, H * 0.08, W * 0.06, H * 0.6));
      g.fillStyle = '#e0b83c'; [0.12, 0.88].forEach((x) => g.fillRect(W * x - W * 0.03, H * 0.6, W * 0.06, H * 0.03));
      g.strokeStyle = '#f2d06b'; g.lineWidth = 2;
      for (let i = 0; i <= 6; i++) { g.beginPath(); g.moveTo(W * (0.2 + i * 0.1), H * 0.45); g.lineTo(W * (i / 6), H); g.stroke(); }
      g.fillStyle = '#c9cdd6'; g.font = '700 ' + Math.round(H * 0.07) + 'px Inter, sans-serif'; g.textAlign = 'center'; g.fillText('B1', W / 2, H * 0.36);
      // Two cars.
      [[0.32, '#9c2f2f'], [0.62, '#20304f']].forEach(([x, c]) => { g.fillStyle = c; g.fillRect(W * x, H * 0.66, W * 0.13, H * 0.12); g.fillRect(W * (x + 0.02), H * 0.6, W * 0.09, H * 0.07); g.fillStyle = '#111'; g.fillRect(W * (x + 0.01), H * 0.77, W * 0.025, H * 0.03); g.fillRect(W * (x + 0.095), H * 0.77, W * 0.025, H * 0.03); });
    },
    playground(g, W, H) {
      g.fillStyle = lin(g, 0, 0, 0, H * 0.6, [[0, '#8ec5f0'], [1, '#d9eefb']]); g.fillRect(0, 0, W, H);
      g.fillStyle = '#7cc46f'; g.fillRect(0, H * 0.6, W, H * 0.4);
      g.fillStyle = '#e8846b'; g.beginPath(); g.ellipse(W * 0.5, H * 0.8, W * 0.32, H * 0.1, 0, 0, Math.PI * 2); g.fill();
      // Trees.
      [[0.08, 0.5], [0.9, 0.48], [0.2, 0.55]].forEach(([x, y]) => { g.fillStyle = '#6b4a2b'; g.fillRect(W * x - 3, H * y, 6, H * 0.14); g.fillStyle = '#3f8f4a'; g.beginPath(); g.arc(W * x, H * y, H * 0.11, 0, Math.PI * 2); g.fill(); });
      // Slide, swing and a sandbox.
      g.fillStyle = '#f2c14e'; g.fillRect(W * 0.34, H * 0.48, W * 0.08, H * 0.26);
      g.fillStyle = '#4a90d9'; g.beginPath(); g.moveTo(W * 0.42, H * 0.48); g.lineTo(W * 0.56, H * 0.74); g.lineTo(W * 0.52, H * 0.76); g.lineTo(W * 0.4, H * 0.54); g.fill();
      g.strokeStyle = '#d0573f'; g.lineWidth = 4; g.beginPath(); g.moveTo(W * 0.62, H * 0.76); g.lineTo(W * 0.68, H * 0.44); g.lineTo(W * 0.8, H * 0.44); g.lineTo(W * 0.86, H * 0.76); g.stroke();
      g.strokeStyle = '#555'; g.lineWidth = 1.5; [0.71, 0.77].forEach((x) => { g.beginPath(); g.moveTo(W * x, H * 0.44); g.lineTo(W * x, H * 0.64); g.stroke(); });
      g.fillStyle = '#333'; g.fillRect(W * 0.7, H * 0.64, W * 0.08, H * 0.02);
      g.fillStyle = '#f1dd9a'; g.fillRect(W * 0.12, H * 0.74, W * 0.14, H * 0.08); g.strokeStyle = '#a5753f'; g.lineWidth = 3; g.strokeRect(W * 0.12, H * 0.74, W * 0.14, H * 0.08);
    },
    gym(g, W, H) {
      g.fillStyle = '#1a1f2b'; g.fillRect(0, 0, W, H);
      g.fillStyle = '#2b2f38'; g.fillRect(0, H * 0.7, W, H * 0.3);
      // Mirror wall with reflections.
      g.fillStyle = lin(g, 0, H * 0.1, W, H * 0.6, [[0, '#3c4a66'], [0.5, '#7d8fb3'], [1, '#3c4a66']]); g.fillRect(W * 0.05, H * 0.12, W * 0.9, H * 0.5);
      g.strokeStyle = 'rgba(255,255,255,0.25)'; g.lineWidth = 2; for (let i = 0; i < 4; i++) { g.beginPath(); g.moveTo(W * (0.15 + i * 0.22), H * 0.12); g.lineTo(W * (0.05 + i * 0.22), H * 0.62); g.stroke(); }
      g.strokeStyle = '#11151d'; g.lineWidth = 3; for (let i = 1; i < 4; i++) { g.beginPath(); g.moveTo(W * (0.05 + i * 0.225), H * 0.12); g.lineTo(W * (0.05 + i * 0.225), H * 0.62); g.stroke(); }
      // Lights.
      for (let i = 0; i < 4; i++) { g.fillStyle = '#fff7da'; g.fillRect(W * (0.12 + i * 0.22), H * 0.04, W * 0.12, H * 0.02); }
      // Treadmill, bench and dumbbell rack.
      g.fillStyle = '#0d0f14'; g.fillRect(W * 0.1, H * 0.66, W * 0.24, H * 0.06); g.fillStyle = '#444b59'; g.fillRect(W * 0.3, H * 0.48, W * 0.02, H * 0.2); g.fillRect(W * 0.27, H * 0.46, W * 0.07, H * 0.03);
      g.fillStyle = '#c0392b'; g.fillRect(W * 0.42, H * 0.64, W * 0.18, H * 0.04); g.fillStyle = '#555'; g.fillRect(W * 0.44, H * 0.68, W * 0.02, H * 0.06); g.fillRect(W * 0.56, H * 0.68, W * 0.02, H * 0.06);
      g.fillStyle = '#3a3f4b'; g.fillRect(W * 0.68, H * 0.58, W * 0.24, H * 0.14);
      g.fillStyle = GOLD; for (let i = 0; i < 6; i++) { g.beginPath(); g.arc(W * (0.71 + i * 0.037), H * 0.6, H * 0.022, 0, Math.PI * 2); g.fill(); g.beginPath(); g.arc(W * (0.71 + i * 0.037), H * 0.67, H * 0.022, 0, Math.PI * 2); g.fill(); }
    }
  };

  // Gallery items: [id, category, caption key, how to draw]. Plans reuse the catalog's floor plan drawing.
  const ITEMS = [
    ['facade', 'facade', 'galpg.s.facade'], ['lobby', 'interior', 'galpg.s.lobby'], ['apartment', 'interior', 'galpg.s.apartment'],
    ['kitchen', 'interior', 'galpg.s.kitchen'], ['roof', 'facade', 'galpg.s.roof'], ['parking', 'infra', 'galpg.s.parking'],
    ['playground', 'infra', 'galpg.s.playground'], ['gym', 'infra', 'galpg.s.gym'],
    ['plan0', 'plans', 'layouts.type0'], ['plan1', 'plans', 'layouts.type1'], ['plan2', 'plans', 'layouts.type2'], ['plan3', 'plans', 'layouts.type3']
  ];
  const FILTERS = [['all', 'galpg.f.all'], ['facade', 'galpg.f.facade'], ['interior', 'galpg.f.interior'], ['infra', 'galpg.f.infra'], ['plans', 'galpg.f.plans']];

  // Draws an item onto a canvas of W×H CSS pixels, sharp on high-density screens.
  function paint(canvas, id, W, H) {
    if (id.startsWith('plan')) { canvas.setAttribute('width', W); canvas.setAttribute('height', H); S.site.drawFloorPlan(canvas, +id.slice(4)); return; }
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = W * dpr; canvas.height = H * dpr;
    const g = canvas.getContext('2d'); if (!g) return;
    g.scale(dpr, dpr);
    SCENES[id](g, W, H);
    // Thin gold frame and a corner mark: "illustration", these are not photos.
    g.strokeStyle = 'rgba(201, 162, 39, 0.5)'; g.lineWidth = 2; g.strokeRect(1, 1, W - 2, H - 2);
    g.fillStyle = 'rgba(6, 13, 31, 0.55)'; g.fillRect(W - 92, H - 22, 88, 18);
    g.fillStyle = GOLD2; g.font = '600 10px Inter, sans-serif'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(t('galpg.illustration'), W - 48, H - 13);
  }

  S.views.gallery = function (root) {
    const site = S.site;
    let filter = 'all', lightbox = null;
    const page = h('div', { class: 'page-in lux gallery-page' });
    root.appendChild(page);

    const head = h('section', { class: 'lux-pagehead' }, h('div', { class: 'wrap' },
      h('nav', { class: 'crumbs', 'aria-label': t('cat.crumbs') }, h('a', { href: '/' }, t('nav.home')), h('span', { 'aria-hidden': 'true' }, '/'), h('span', { 'aria-current': 'page' }, t('nav.gallery'))),
      h('h1', null, site.accentFirst(t('galpg.title'))), h('p', null, t('galpg.sub'))));

    const bar = h('div', { class: 'gal-filters', role: 'group', 'aria-label': t('galpg.filters') });
    const grid = h('div', { class: 'galpg-grid' });
    const shown = () => ITEMS.filter((it) => filter === 'all' || it[1] === filter);

    function drawBar() {
      bar.replaceChildren(...FILTERS.map(([k, key]) => h('button', { type: 'button', class: 'gal-filter' + (filter === k ? ' on' : ''), 'aria-pressed': String(filter === k), onclick: () => { filter = k; drawBar(); drawGrid(); } }, t(key))));
    }
    function drawGrid() {
      const list = shown();
      grid.replaceChildren(...list.map(([id, , cap], i) => {
        const canvas = h('canvas', { role: 'img', 'aria-label': t(cap) });
        paint(canvas, id, 300, 200);
        return h('figure', { class: 'galpg-card' },
          h('button', { type: 'button', class: 'galpg-shot', 'aria-label': t('galpg.zoom') + ': ' + t(cap), onclick: () => open(i) }, canvas,
            h('span', { class: 'galpg-hover', 'aria-hidden': 'true' }, h('span', { class: 'galpg-zoom' }, '⤢'), t('galpg.zoom'))),
          h('figcaption', null, t(cap)));
      }));
    }

    // Lightbox over the page: the picture at 600×400 (scaled down on a phone), caption, arrows, Esc.
    function open(index) {
      close();
      let i = index;
      const list = shown();
      const canvas = h('canvas', { class: 'lb-canvas', role: 'img' });
      const cap = h('p', { class: 'lb-cap' });
      const count = h('span', { class: 'lb-count' });
      const draw = () => { const [id, , key] = list[i]; paint(canvas, id, 600, 400); canvas.setAttribute('aria-label', t(key)); cap.textContent = t(key); count.textContent = (i + 1) + ' / ' + list.length; };
      const step = (d) => { i = (i + d + list.length) % list.length; draw(); };
      const prev = h('button', { type: 'button', class: 'lb-nav prev', 'aria-label': t('galpg.prev'), onclick: () => step(-1) }, '‹');
      const next = h('button', { type: 'button', class: 'lb-nav next', 'aria-label': t('galpg.next'), onclick: () => step(1) }, '›');
      const x = h('button', { type: 'button', class: 'lb-close', 'aria-label': t('common.close'), onclick: close }, '×');
      lightbox = h('div', { class: 'lightbox', role: 'dialog', 'aria-modal': 'true', 'aria-label': t('nav.gallery'), onclick: (e) => { if (e.target === lightbox) close(); } },
        h('div', { class: 'lb-box' }, x, h('div', { class: 'lb-stage' }, prev, canvas, next), h('div', { class: 'lb-foot' }, cap, count)));
      lightbox.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); else if (e.key === 'ArrowLeft') step(-1); else if (e.key === 'ArrowRight') step(1); });
      document.body.appendChild(lightbox);
      document.body.classList.add('menu-open');
      draw();
      x.focus();
    }
    function close() {
      if (!lightbox) return;
      lightbox.remove(); lightbox = null;
      document.body.classList.remove('menu-open');
    }

    drawBar(); drawGrid();
    page.append(head, h('section', { class: 'section lux-sec section-reveal' }, h('div', { class: 'wrap' }, bar, grid, h('p', { class: 'gal-note' }, t('galpg.note')))), site.footer());
    return () => close();
  };
})();
