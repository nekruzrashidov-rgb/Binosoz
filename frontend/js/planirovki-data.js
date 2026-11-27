/* Layouts of the apartments — one source for the 2D plan (drawFloorPlan, sahifai-asosi-script.js), the 3D apartment
   (modeli-3d-kvartira-script.js) and the CRM's preview of an imported layout.
   A layout is in metres: x to the right, y from the facade into the house. Rooms are polygons [[x, y], …] (L-shaped halls,
   slanted walls, bays, a living room along a rounded facade); walls have a thickness — outer 0.4 m, between rooms 0.12 m,
   between apartments 0.2 m; doors and windows lie on the wall segments; wet rooms (bath, wc, kitchen) by the risers.
   The area of a room is counted by its polygon (the shoelace formula). The balcony counts ×0.3, the loggia ×0.5 (the terrace
   of a penthouse is not in the area). A concept layout is scaled to the apartment's area from the API; a real layout of the
   developer (CRM → «Планировки») is not scaled: if its rooms differ from the API's area by more than 3 %, the plan shows no
   m² and says «уточняйте у менеджера».
   Format of a layout (also of the import): { code, name, outline: [[x, y], …], facade: [edge index, …] (edges of the outline
   with windows), rooms: [[key, [[x, y], …]], …], doors: [[roomA, roomB, width | 'open'], …], entrance: room index,
   balcony: { kind: 'balcony' | 'loggia' | 'terrace', room, poly: [[x, y], …] } | null, rooms on the outline's non-facade
   edges border the neighbours (party walls) or the corridor (the entrance side). */
(function () {
  const S = (window.S = window.S || {});
  const E = 1e-6;
  const THICK = { outer: 0.4, inner: 0.12, party: 0.2 };
  const COEF = { balcony: 0.3, loggia: 0.5, terrace: 0 };
  const rect = (x, y, w, h) => [[x, y], [x + w, y], [x + w, y + h], [x, y + h]];
  const R = (key, x, y, w, h) => [key, rect(x, y, w, h)];

  // The typical layouts (concept): a rectangle w × d with the facade on y = 0, rooms as rectangles, and the corner/bay ones
  // with polygon rooms.
  const TYPICAL = {
    // Studio, 27.9 m²: the room with a kitchen nook, the hall and the bathroom.
    0: { w: 5.4, d: 4.6, rooms: [R('living', 0, 0, 3.4, 3.0), R('kitchen', 3.4, 0, 2.0, 3.0), R('hall', 0, 3.0, 3.4, 1.6), R('bath', 3.4, 3.0, 2.0, 1.6)],
      doors: [[2, 0, 0.9], [0, 1, 'open'], [2, 3, 0.8]], entrance: 2, balcony: { kind: 'balcony', room: 0, poly: rect(0.4, -1.2, 2.6, 1.2) } },
    1: { w: 7.2, d: 5.4, rooms: [R('bedroom', 0, 0, 3.6, 3.6), R('kitchen', 3.6, 0, 3.6, 3.6), R('hall', 0, 3.6, 5.0, 1.8), R('bath', 5.0, 3.6, 2.2, 1.8)],
      doors: [[2, 0, 0.9], [2, 1, 0.9], [2, 3, 0.8]], entrance: 2, balcony: { kind: 'balcony', room: 0, poly: rect(0.4, -1.2, 2.8, 1.2) } },
    2: { w: 9.6, d: 6.4, rooms: [R('living', 0, 0, 4.4, 4.4), R('kitchen', 4.4, 0, 2.4, 4.4), R('bedroom', 6.8, 0, 2.8, 4.4), R('bath', 0, 4.4, 3.4, 2.0), R('hall', 3.4, 4.4, 6.2, 2.0)],
      doors: [[4, 0, 0.8], [4, 1, 0.9], [4, 2, 0.9], [4, 3, 0.8]], entrance: 4, balcony: { kind: 'balcony', room: 0, poly: rect(0.6, -1.2, 3.0, 1.2) } },
    // Three rooms, 88 m²: the hall is L-shaped (one polygon).
    3: { w: 12.0, d: 7.0, rooms: [R('living', 0, 0, 4.6, 4.4), R('bedroom', 4.6, 0, 3.6, 4.4), R('bedroom2', 8.2, 0, 3.8, 4.4), R('kitchen', 0, 4.4, 3.6, 2.6),
      ['hall', [[3.6, 4.4], [10.0, 4.4], [10.0, 5.6], [8.8, 5.6], [8.8, 7.0], [3.6, 7.0]]], R('bath', 10.0, 4.4, 2.0, 2.6), R('wc', 8.8, 5.6, 1.2, 1.4)],
      doors: [[4, 0, 0.9], [4, 1, 0.9], [4, 2, 0.9], [4, 3, 0.9], [4, 5, 0.8], [4, 6, 0.7]], entrance: 4, balcony: { kind: 'loggia', room: 0, poly: rect(0.6, -1.4, 3.4, 1.4) } },
    4: { w: 14.6, d: 7.6, rooms: [R('living', 0, 0, 5.0, 4.6), R('bedroom', 5.0, 0, 3.4, 4.6), R('bedroom2', 8.4, 0, 3.2, 4.6), R('bedroom3', 11.6, 0, 3.0, 4.6), R('kitchen', 0, 4.6, 3.8, 3.0),
      ['hall', [[3.8, 4.6], [12.8, 4.6], [12.8, 7.6], [10.2, 7.6], [10.2, 6.2], [3.8, 6.2]]], R('wc', 3.8, 6.2, 1.8, 1.4), R('bath', 12.8, 4.6, 1.8, 3.0), ['dressing', rect(5.6, 6.2, 4.6, 1.4)]],
      doors: [[5, 0, 0.9], [5, 1, 0.9], [5, 2, 0.9], [5, 3, 0.9], [5, 4, 0.9], [5, 6, 0.7], [5, 7, 0.8], [5, 8, 0.8]], entrance: 5, balcony: { kind: 'loggia', room: 0, poly: rect(0.6, -1.4, 3.4, 1.4) } },
  };

  // The corner and bay layouts under the blocks' shapes (concept). Their outline is not a rectangle: the facade runs along
  // two sides (the corner of A), a slanted cut (the ends of B), a rounded corner (C) or a bay (B); the penthouse has its
  // panoramic living-kitchen on the corner, the master bedroom with its own dressing and bath, the terrace along the setback.
  const arc = (cx, cy, r, a0, a1, n) => Array.from({ length: n + 1 }, (_, k) => { const a = a0 + (a1 - a0) * k / n; return [+(cx + r * Math.cos(a)).toFixed(3), +(cy + r * Math.sin(a)).toFixed(3)]; });
  const CORNER = {
    // The corner of block A (L): the L-shaped living room wraps the corner with windows on two facades; 4 rooms, ~140 m².
    'corner-l': {
      outline: [[0, 0], [13, 0], [13, 7.4], [7.4, 7.4], [7.4, 13], [0, 13]], facade: [0, 5],
      rooms: [['living', [[0, 0], [7.4, 0], [7.4, 4.6], [4.6, 4.6], [4.6, 7.4], [0, 7.4]]], R('bedroom', 7.4, 0, 5.6, 4.6), R('bedroom2', 0, 7.4, 4.6, 5.6),
        R('kitchen', 4.6, 4.6, 2.8, 2.8), R('hall', 7.4, 4.6, 3.2, 2.8), R('bath', 10.6, 4.6, 2.4, 2.8), R('bedroom3', 4.6, 7.4, 2.8, 5.6)],
      doors: [[4, 1, 0.9], [4, 3, 'open'], [4, 5, 0.8], [3, 0, 'open'], [0, 2, 0.9], [3, 6, 0.9]], entrance: 4,
      balcony: { kind: 'loggia', room: 0, poly: rect(0.4, -1.4, 5.6, 1.4) } },
    // The rounded corner of block C: the living room along the curve of the facade, a slanted wall to the hall; 4 rooms.
    'corner-round': {
      outline: [[0, 0], [8, 0]].concat(arc(8, 6, 6, -Math.PI / 2, 0, 6).slice(1)).concat([[14, 14], [0, 14]]), facade: [0, 1, 2, 3, 4, 5, 6, 7],
      rooms: [['living', [[3, 0], [8, 0]].concat(arc(8, 6, 6, -Math.PI / 2, 0, 6).slice(1)).concat([[14, 8], [8, 8], [3, 6]])], R('kitchen', 0, 0, 3, 6),
        R('bedroom', 8, 8, 6, 6), R('bedroom2', 0, 9, 4, 3), R('bedroom3', 4, 10, 4, 4), ['hall', [[0, 6], [3, 6], [8, 8], [8, 10], [4, 10], [4, 9], [0, 9]]], R('bath', 0, 12, 4, 2)],
      doors: [[5, 0, 'open'], [5, 1, 0.9], [5, 2, 0.9], [5, 3, 0.9], [5, 4, 0.9], [3, 6, 0.8]], entrance: 5, balcony: null },
    // The bay of block B: two rooms, the living room with the bay window (a trapezoid out of the facade), ~90 m².
    'bay-2': {
      outline: [[0, 0], [3.5, 0], [4.5, -1.5], [8.5, -1.5], [9.5, 0], [12, 0], [12, 7], [0, 7]], facade: [0, 1, 2, 3, 4],
      rooms: [['living', [[3.5, 0], [4.5, -1.5], [8.5, -1.5], [9.5, 0], [9.5, 4.6], [3.5, 4.6]]], R('bedroom', 9.5, 0, 2.5, 4.6), R('kitchen', 0, 0, 3.5, 4.6),
        R('hall', 3.5, 4.6, 6, 2.4), R('bath', 9.5, 4.6, 2.5, 2.4), R('wc', 0, 4.6, 3.5, 2.4)],
      doors: [[3, 0, 0.9], [0, 1, 0.9], [0, 2, 'open'], [3, 4, 0.8], [3, 5, 0.7]], entrance: 3, balcony: null },
    // The penthouse (the top floor of every block): the panoramic living-kitchen on the corner, three bedrooms — the master
    // one with its own dressing and bath — and the terrace along the setback (not in the area).
    'ph-4': {
      outline: rect(0, 0, 16, 12), facade: [0, 1],
      rooms: [R('living', 0, 0, 9, 6.5), R('kitchen', 9, 0, 3, 4), R('bedroom', 12, 0, 4, 6.5), R('dressing', 12, 6.5, 4, 2.2), R('bath', 12, 8.7, 4, 3.3),
        R('bedroom2', 0, 6.5, 4.2, 5.5), R('bedroom3', 4.2, 8, 4, 4), ['hall', [[4.2, 6.5], [12, 6.5], [12, 12], [8.2, 12], [8.2, 8], [4.2, 8]]], R('wc', 9, 4, 3, 2.5)],
      doors: [[7, 0, 'open'], [0, 1, 'open'], [7, 3, 0.8], [3, 2, 0.8], [3, 4, 0.8], [7, 5, 0.9], [7, 6, 0.9], [7, 8, 0.7]], entrance: 7,
      balcony: { kind: 'terrace', room: 0, poly: rect(0, -3, 16, 3) } },
  };

  // Real layouts of the developer (CRM → «Планировки», GET /public/{org}/layouts): code → layout; they are not scaled.
  const REAL = {};

  const INNER = { hall: 1, bath: 1, wc: 1, dressing: 1 };
  const WET = { bath: 1, wc: 1, kitchen: 1 };
  const P = (p) => ({ x: +p[0], y: +p[1] });
  function area(poly) { let s = 0; for (let i = 0; i < poly.length; i++) { const a = poly[i], b = poly[(i + 1) % poly.length]; s += a.x * b.y - b.x * a.y; } return Math.abs(s) / 2; }
  function bbox(poly) { const xs = poly.map((p) => p.x), ys = poly.map((p) => p.y); return { x: Math.min(...xs), y: Math.min(...ys), w: Math.max(...xs) - Math.min(...xs), h: Math.max(...ys) - Math.min(...ys) }; }
  const contains = (poly, p) => { let c = false; for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) { const a = poly[i], b = poly[j]; if ((a.y > p.y) !== (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) c = !c; } return c; };
  // The point to write a room's name: the middle of its bounding box when inside, otherwise the inside point nearest it.
  function labelPoint(poly) {
    const b = bbox(poly), c = { x: b.x + b.w / 2, y: b.y + b.h / 2 };
    if (contains(poly, c)) return c;
    let best = null;
    for (let i = 1; i < 12; i++) for (let j = 1; j < 12; j++) { const p = { x: b.x + b.w * i / 12, y: b.y + b.h * j / 12 }; if (contains(poly, p)) { const d = Math.hypot(p.x - c.x, p.y - c.y); if (!best || d < best.d) best = { d, p }; } }
    return best ? best.p : c;
  }
  // The biggest axis-aligned rectangle inside a room (approximate, for the furniture of the 3D apartment).
  function innerRect(poly) {
    const b = bbox(poly);
    if (Math.abs(area(poly) - b.w * b.h) < 0.05) return b;
    let best = { x: b.x, y: b.y, w: 0.5, h: 0.5 };
    const N = 10;
    for (let i = 0; i < N; i++) for (let j = 0; j < N; j++) for (let k = i + 1; k <= N; k++) for (let l = j + 1; l <= N; l++) {
      const x0 = b.x + b.w * i / N, y0 = b.y + b.h * j / N, x1 = b.x + b.w * k / N, y1 = b.y + b.h * l / N;
      if ((x1 - x0) * (y1 - y0) <= best.w * best.h) continue;
      const pts = [[x0, y0], [x1, y0], [x1, y1], [x0, y1], [(x0 + x1) / 2, (y0 + y1) / 2]].map(([x, y]) => ({ x: x + (x < (x0 + x1) / 2 ? 0.02 : -0.02), y: y + (y < (y0 + y1) / 2 ? 0.02 : -0.02) }));
      if (pts.every((p) => contains(poly, p))) best = { x: x0, y: y0, w: x1 - x0, h: y1 - y0 };
    }
    return best;
  }

  // The common part of two segments on one line, or null: { a, b } (points).
  function overlap(p1, p2, q1, q2) {
    const dx = p2.x - p1.x, dy = p2.y - p1.y, L = Math.hypot(dx, dy); if (L < E) return null;
    const ux = dx / L, uy = dy / L;
    const off = (q) => Math.abs((q.x - p1.x) * uy - (q.y - p1.y) * ux);
    if (off(q1) > 1e-3 || off(q2) > 1e-3) return null;
    const t = (q) => (q.x - p1.x) * ux + (q.y - p1.y) * uy;
    const a = Math.max(0, Math.min(t(q1), t(q2))), b = Math.min(L, Math.max(t(q1), t(q2)));
    if (b - a < 0.05) return null;
    return { a: { x: p1.x + ux * a, y: p1.y + uy * a }, b: { x: p1.x + ux * b, y: p1.y + uy * b } };
  }
  const edges = (poly) => poly.map((p, i) => [p, poly[(i + 1) % poly.length]]);

  // A layout normalised to polygons: the typical rectangles get their outline (the rectangle, the facade on y = 0).
  function normalised(src) {
    const o = src.outline ? src : Object.assign({}, src, { outline: rect(0, 0, src.w, src.d), facade: [0] });
    return o;
  }
  // The layout of an apartment: by its code (a real one, a corner one), otherwise the typical one by the rooms; scaled to the
  // area (concept only). Returns rooms with polygons and their areas, doors and windows as segments, the walls, the check.
  function layout(type, area0, code) {
    const real = code && REAL[code];
    const src = normalised(real || (code && CORNER[code]) || TYPICAL[Math.max(0, Math.min(4, type | 0))] || TYPICAL[1]);
    const outline0 = src.outline.map(P);
    const rooms0 = src.rooms.map(([key, poly]) => ({ key, poly: poly.map(P) }));
    const bal0 = src.balcony ? Object.assign({}, src.balcony, { poly: src.balcony.poly.map(P) }) : null;
    const nominal = rooms0.reduce((s, r) => s + area(r.poly), 0) + (bal0 ? area(bal0.poly) * (COEF[bal0.kind] || 0) : 0);
    const k = !real && area0 > 0 ? Math.sqrt(area0 / nominal) : 1;
    const sc = (p) => ({ x: p.x * k, y: p.y * k });
    const outline = outline0.map(sc);
    const rooms = rooms0.map((r, i) => { const poly = r.poly.map(sc); const b = bbox(poly); return Object.assign({ i, key: r.key, poly, area: area(poly), wet: !!WET[r.key], label: labelPoint(poly) }, b); });
    const balcony = bal0 ? Object.assign({ key: bal0.kind === 'terrace' ? 'terrace' : 'balcony', kind: bal0.kind, room: bal0.room, poly: bal0.poly.map(sc) }, bbox(bal0.poly.map(sc))) : null;
    if (balcony) balcony.area = area(balcony.poly);
    // Facade edges of the outline.
    const facadeEdges = (src.facade || [0]).map((i) => [outline[i], outline[(i + 1) % outline.length]]);
    const onFacade = (a, b) => facadeEdges.some(([p, q]) => overlap(p, q, a, b));
    // Doors: in the middle of the common wall of two rooms; the entrance — in the middle of the entrance room's longest
    // wall that is on the outline and not on the facade (the corridor side).
    const doors = [];
    (src.doors || []).forEach(([i, j, wd]) => {
      const A = rooms[i], B = rooms[j]; if (!A || !B) return;
      let best = null;
      edges(A.poly).forEach(([p, q]) => edges(B.poly).forEach(([u, v]) => { const o = overlap(p, q, u, v); if (o) { const L = Math.hypot(o.b.x - o.a.x, o.b.y - o.a.y); if (!best || L > best.L) best = { o, L }; } }));
      if (!best) return;
      const L = best.L, width = wd === 'open' ? L : Math.min(wd, L - 0.2), m = { x: (best.o.a.x + best.o.b.x) / 2, y: (best.o.a.y + best.o.b.y) / 2 };
      const ux = (best.o.b.x - best.o.a.x) / L, uy = (best.o.b.y - best.o.a.y) / L;
      doors.push(seg({ rooms: [i, j], a: { x: m.x - ux * width / 2, y: m.y - uy * width / 2 }, b: { x: m.x + ux * width / 2, y: m.y + uy * width / 2 }, open: wd === 'open' }));
    });
    const ent = rooms[src.entrance] || rooms[0];
    let eb = null;
    edges(ent.poly).forEach(([p, q]) => {
      if (onFacade(p, q)) return;
      const onOutline = edges(outline).some(([u, v]) => overlap(u, v, p, q));
      const L = Math.hypot(q.x - p.x, q.y - p.y);
      // The corridor side: deeper in the house (the largest y) wins among the outline edges.
      const score = (onOutline ? 1000 : 0) + (p.y + q.y) / 2 * 10 + L;
      if (!eb || score > eb.score) eb = { p, q, L, score };
    });
    if (eb) { const m = { x: (eb.p.x + eb.q.x) / 2, y: (eb.p.y + eb.q.y) / 2 }, ux = (eb.q.x - eb.p.x) / eb.L, uy = (eb.q.y - eb.p.y) / eb.L; doors.push(seg({ rooms: [src.entrance, -1], a: { x: m.x - ux * 0.45, y: m.y - uy * 0.45 }, b: { x: m.x + ux * 0.45, y: m.y + uy * 0.45 }, entrance: true })); }
    // Windows: on every room edge that lies on a facade edge (not the inner rooms), about half of it (1–1.8 m) per 3 m;
    // the room with the balcony gets a glazed door to it.
    const windows = [];
    rooms.forEach((r) => {
      if (INNER[r.key]) return;
      edges(r.poly).forEach(([p, q]) => {
        facadeEdges.forEach(([u, v]) => {
          const o = overlap(u, v, p, q); if (!o) return;
          const L = Math.hypot(o.b.x - o.a.x, o.b.y - o.a.y), ux = (o.b.x - o.a.x) / L, uy = (o.b.y - o.a.y) / L;
          const n = Math.max(1, Math.floor(L / 3.2)), ww = Math.max(0.8, Math.min(1.8, L / n * 0.55));
          for (let s = 0; s < n; s++) {
            const c = (s + 0.5) * L / n;
            const door = balcony && balcony.room === r.i && s === 0 && touches(balcony.poly, o);
            windows.push(seg({ room: r.i, a: { x: o.a.x + ux * (c - ww / 2), y: o.a.y + uy * (c - ww / 2) }, b: { x: o.a.x + ux * (c + ww / 2), y: o.a.y + uy * (c + ww / 2) }, door }));
          }
        });
      });
    });
    const roomsArea = rooms.reduce((s, r) => s + r.area, 0) + (balcony ? balcony.area * (COEF[balcony.kind] || 0) : 0);
    const exact = !(area0 > 0) || Math.abs(roomsArea - area0) / area0 <= 0.03;
    const bb = bbox(outline.concat(balcony ? balcony.poly : []));
    const lay = { type: Math.max(0, Math.min(4, type | 0)), code: code && (REAL[code] || CORNER[code]) ? code : null, real: !!real, outline, facade: facadeEdges, rooms, doors, windows, balcony,
      area: area0 > 0 ? area0 : roomsArea, roomsArea, exact, scale: k, x0: bb.x, y0: bb.y, w: bbox(outline).w, d: bbox(outline).h };
    // Compatibility with the rectangular callers: the facade is y = 0 for the typical ones.
    return lay;
  }
  function seg(o) {
    const dx = o.b.x - o.a.x, dy = o.b.y - o.a.y;
    // Axis-aligned segments also carry the old fields (axis, at, from, to).
    if (Math.abs(dy) < E) Object.assign(o, { axis: 'x', at: o.a.y, from: Math.min(o.a.x, o.b.x), to: Math.max(o.a.x, o.b.x) });
    else if (Math.abs(dx) < E) Object.assign(o, { axis: 'y', at: o.a.x, from: Math.min(o.a.y, o.b.y), to: Math.max(o.a.y, o.b.y) });
    return o;
  }
  const touches = (poly, o) => edges(poly.map((p) => p)).some(([p, q]) => overlap(p, q, o.a, o.b));

  // Walls as segments [x1, y1, x2, y2, outer, kind] with the door openings cut out (windows stay in the wall: the 3D model
  // draws a sill and a lintel there, the 2D plan a copper line). kind: outer (on the outline's facade), party (on the
  // outline, to the neighbours or the corridor), inner (between rooms). Used by both pictures and by the walk's collisions.
  function walls(lay) {
    const seen = {}, list = [];
    const key = (a, b) => { const k1 = a.x.toFixed(2) + ',' + a.y.toFixed(2), k2 = b.x.toFixed(2) + ',' + b.y.toFixed(2); return k1 < k2 ? k1 + '|' + k2 : k2 + '|' + k1; };
    const outlineEdges = edges(lay.outline);
    // Room edges split at the vertices of the other rooms lying on them, so a shared wall is one segment.
    const verts = lay.rooms.flatMap((r) => r.poly);
    lay.rooms.forEach((r) => edges(r.poly).forEach(([p, q]) => {
      const L = Math.hypot(q.x - p.x, q.y - p.y); if (L < E) return;
      const ux = (q.x - p.x) / L, uy = (q.y - p.y) / L;
      const cuts = [0, L].concat(verts.map((v) => ({ t: (v.x - p.x) * ux + (v.y - p.y) * uy, off: Math.abs((v.x - p.x) * uy - (v.y - p.y) * ux) }))
        .filter((c) => c.off < 1e-3 && c.t > E && c.t < L - E).map((c) => c.t)).sort((a, b) => a - b);
      for (let i = 0; i + 1 < cuts.length; i++) {
        if (cuts[i + 1] - cuts[i] < 0.02) continue;
        const a = { x: p.x + ux * cuts[i], y: p.y + uy * cuts[i] }, b = { x: p.x + ux * cuts[i + 1], y: p.y + uy * cuts[i + 1] };
        const kk = key(a, b); if (seen[kk]) continue; seen[kk] = 1;
        const facade = lay.facade.some(([u, v]) => overlap(u, v, a, b));
        const onOutline = outlineEdges.some(([u, v]) => overlap(u, v, a, b));
        list.push({ a, b, kind: facade ? 'outer' : onOutline ? 'party' : 'inner' });
      }
    }));
    // Cut the doors out.
    let out = list;
    lay.doors.forEach((d) => {
      const next = [];
      out.forEach((s) => {
        const o = overlap(s.a, s.b, d.a, d.b);
        if (!o) { next.push(s); return; }
        const L = Math.hypot(s.b.x - s.a.x, s.b.y - s.a.y), ux = (s.b.x - s.a.x) / L, uy = (s.b.y - s.a.y) / L;
        const t = (p) => (p.x - s.a.x) * ux + (p.y - s.a.y) * uy, t0 = Math.min(t(o.a), t(o.b)), t1 = Math.max(t(o.a), t(o.b));
        if (t0 > 0.02) next.push({ a: s.a, b: { x: s.a.x + ux * t0, y: s.a.y + uy * t0 }, kind: s.kind });
        if (t1 < L - 0.02) next.push({ a: { x: s.a.x + ux * t1, y: s.a.y + uy * t1 }, b: s.b, kind: s.kind });
      });
      out = next;
    });
    return out.map((s) => [s.a.x, s.a.y, s.b.x, s.b.y, s.kind !== 'inner', s.kind]);
  }

  // A layout from the import (CRM → «Планировки»): the same format; checked here before it is saved (the server checks too).
  function validate(src) {
    const errs = [];
    if (!src || typeof src !== 'object') return ['format'];
    const okPoly = (p) => Array.isArray(p) && p.length >= 3 && p.length <= 64 && p.every((q) => Array.isArray(q) && q.length >= 2 && q.every(Number.isFinite));
    if (!okPoly(src.outline)) errs.push('outline');
    if (!Array.isArray(src.rooms) || !src.rooms.length || !src.rooms.every((r) => Array.isArray(r) && typeof r[0] === 'string' && okPoly(r[1]))) errs.push('rooms');
    if (!errs.length && src.rooms.some((r) => area(r[1].map(P)) < 0.5)) errs.push('roomArea');
    if (src.balcony && !okPoly(src.balcony.poly)) errs.push('balcony');
    return errs;
  }

  S.plans = {
    layout, walls, validate, area: (poly) => area(poly.map(P)), THICK, COEF, innerRect,
    types: [0, 1, 2, 3], corners: Object.keys(CORNER),
    nominalArea: (type) => Math.round(layout(type, 0).roomsArea * 10) / 10,
    // Real layouts from the API: [{ code, layout }]; a layout with a real code is drawn without «концепт».
    setReal(list) { Object.keys(REAL).forEach((k) => delete REAL[k]); (list || []).forEach((x) => { if (x && x.code && x.layout && !validate(x.layout).length) REAL[x.code] = x.layout; }); },
    isReal: (code) => !!(code && REAL[code]),
  };
})();
