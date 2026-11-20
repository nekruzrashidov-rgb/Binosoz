/* Typical layouts of the apartments — one source for the 2D plan (drawFloorPlan, sahifai-asosi-script.js) and the
   apartment inside the 3D model (modeli-3d-kvartira-script.js).
   Every layout is drawn in metres for its nominal area: x to the right, y from the facade (y = 0, the windows) into the
   house; the balcony is a strip in front of the facade (y < 0). S.plans.layout(type, area) scales it so that the rooms and
   the balcony add up to the apartment's area from the API: the layout is typical and the sizes are approximate (the site
   says so next to it). Rooms are rectangles; doors join two rooms by the middle of their common wall ('open' — no wall
   between them); the entrance door is in the middle of the hall's wall on the stairwell side (y = d). */
(function () {
  const S = (window.S = window.S || {});

  // [key, x, y, w, h] in metres; keys are the i18n names plans.room.<key>.
  const L = {
    // Studio, 27.9 m²: the room with a kitchen nook, the hall and the bathroom.
    0: { w: 5.4, d: 4.6,
      rooms: [['living', 0, 0, 3.4, 3.0], ['kitchen', 3.4, 0, 2.0, 3.0], ['hall', 0, 3.0, 3.4, 1.6], ['bath', 3.4, 3.0, 2.0, 1.6]],
      doors: [[2, 0, 0.9], [0, 1, 'open'], [2, 3, 0.8]], entrance: 2, balcony: { room: 0, x: 0.4, w: 2.6, depth: 1.2 } },
    // One room, 42.3 m².
    1: { w: 7.2, d: 5.4,
      rooms: [['bedroom', 0, 0, 3.6, 3.6], ['kitchen', 3.6, 0, 3.6, 3.6], ['hall', 0, 3.6, 5.0, 1.8], ['bath', 5.0, 3.6, 2.2, 1.8]],
      doors: [[2, 0, 0.9], [2, 1, 0.9], [2, 3, 0.8]], entrance: 2, balcony: { room: 0, x: 0.4, w: 2.8, depth: 1.2 } },
    // Two rooms, 65 m².
    2: { w: 9.6, d: 6.4,
      rooms: [['living', 0, 0, 4.4, 4.4], ['kitchen', 4.4, 0, 2.4, 4.4], ['bedroom', 6.8, 0, 2.8, 4.4], ['bath', 0, 4.4, 3.4, 2.0], ['hall', 3.4, 4.4, 6.2, 2.0]],
      doors: [[4, 0, 0.8], [4, 1, 0.9], [4, 2, 0.9], [4, 3, 0.8]], entrance: 4, balcony: { room: 0, x: 0.6, w: 3.0, depth: 1.2 } },
    // Three rooms, 88.1 m²: the hall is two rectangles of one space ('open' between them).
    3: { w: 12.0, d: 7.0,
      rooms: [['living', 0, 0, 4.6, 4.4], ['bedroom', 4.6, 0, 3.6, 4.4], ['bedroom2', 8.2, 0, 3.8, 4.4], ['kitchen', 0, 4.4, 3.6, 2.6], ['hall', 3.6, 4.4, 5.2, 2.6], ['hall', 8.8, 4.4, 1.2, 2.6, true], ['bath', 10.0, 4.4, 2.0, 2.6]],
      doors: [[4, 0, 0.9], [4, 1, 0.9], [5, 2, 0.9], [4, 3, 0.9], [4, 5, 'open'], [5, 6, 0.8]], entrance: 4, balcony: { room: 0, x: 0.6, w: 3.4, depth: 1.2 } },
    // Four rooms, 115 m² (also «four and more»): three bedrooms on the facade, the hall is two rectangles of one space.
    4: { w: 14.6, d: 7.6,
      rooms: [['living', 0, 0, 5.0, 4.6], ['bedroom', 5.0, 0, 3.4, 4.6], ['bedroom2', 8.4, 0, 3.2, 4.6], ['bedroom3', 11.6, 0, 3.0, 4.6], ['kitchen', 0, 4.6, 3.8, 3.0], ['hall', 3.8, 4.6, 6.4, 3.0], ['hall', 10.2, 4.6, 2.6, 3.0, true], ['bath', 12.8, 4.6, 1.8, 3.0]],
      doors: [[5, 0, 0.9], [5, 1, 0.9], [5, 2, 0.9], [6, 3, 0.9], [5, 4, 0.9], [5, 6, 'open'], [6, 7, 0.8]], entrance: 5, balcony: { room: 0, x: 0.6, w: 3.4, depth: 1.2 } },
  };
  // Rooms without windows to the street.
  const INNER = { hall: 1, bath: 1, wc: 1 };
  const WET = { bath: 1, wc: 1 };

  const nominal = (p) => p.rooms.reduce((s, r) => s + r[3] * r[4], 0) + p.balcony.w * p.balcony.depth;

  // The common part of two room edges, or null: { axis: 'x' (a horizontal wall at y) | 'y' (a vertical wall at x), at, a, b }.
  function shared(r1, r2) {
    const e = 1e-6;
    const [x1, y1, w1, h1] = r1, [x2, y2, w2, h2] = r2;
    if (Math.abs(y1 + h1 - y2) < e || Math.abs(y2 + h2 - y1) < e) {
      const a = Math.max(x1, x2), b = Math.min(x1 + w1, x2 + w2);
      if (b - a > e) return { axis: 'x', at: Math.abs(y1 + h1 - y2) < e ? y2 : y1, a, b };
    }
    if (Math.abs(x1 + w1 - x2) < e || Math.abs(x2 + w2 - x1) < e) {
      const a = Math.max(y1, y2), b = Math.min(y1 + h1, y2 + h2);
      if (b - a > e) return { axis: 'y', at: Math.abs(x1 + w1 - x2) < e ? x2 : x1, a, b };
    }
    return null;
  }

  // The layout of a type (0 studio … 4 four rooms and more) scaled to the area (m²); without the area — the nominal one.
  function layout(type, area) {
    const p = L[Math.max(0, Math.min(4, type | 0))] || L[1];
    const nom = nominal(p);
    const k = area > 0 ? Math.sqrt(area / nom) : 1;
    const rooms = p.rooms.map(([key, x, y, w, h, part], i) => ({ i, key, x: x * k, y: y * k, w: w * k, h: h * k, area: w * h * k * k, part: !!part, wet: !!WET[key] }));
    // Parts of one space (the 3-room hall) show one label with the whole area.
    rooms.forEach((r) => { if (r.part) { const main = rooms.find((m) => m.key === r.key && !m.part); if (main) { main.area += r.area; r.area = 0; } } });
    const doors = [];
    p.doors.forEach(([i, j, wd]) => {
      const s = shared(p.rooms[i].slice(1), p.rooms[j].slice(1));
      if (!s) return;
      const len = (s.b - s.a) * k, mid = ((s.a + s.b) / 2) * k;
      const width = wd === 'open' ? len : Math.min(wd, len - 0.2);
      doors.push({ rooms: [i, j], axis: s.axis, at: s.at * k, from: mid - width / 2, to: mid + width / 2, open: wd === 'open' });
    });
    // The entrance: in the middle of the hall's wall on the stairwell side.
    const hall = rooms[p.entrance];
    doors.push({ rooms: [p.entrance, -1], axis: 'x', at: hall.y + hall.h, from: hall.x + hall.w / 2 - 0.45, to: hall.x + hall.w / 2 + 0.45, entrance: true });
    // Windows on the facade: one per room with a window, about half its width (1–1.8 m); the balcony room gets a glazed door.
    const b = p.balcony;
    const balcony = { key: 'balcony', x: b.x * k, y: -b.depth * k, w: b.w * k, h: b.depth * k, area: b.w * b.depth * k * k, room: b.room };
    const windows = [];
    rooms.forEach((r) => {
      if (r.y > 1e-6 || INNER[r.key]) return;
      if (r.i === b.room) {
        const bx0 = balcony.x, bx1 = balcony.x + balcony.w;
        windows.push({ room: r.i, from: bx0 + 0.25, to: bx0 + 1.05, door: true });
        if (bx1 - 0.2 - (bx0 + 1.3) > 0.8) windows.push({ room: r.i, from: bx0 + 1.3, to: Math.min(bx1 - 0.2, bx0 + 1.3 + 1.6) });
      } else {
        const ww = Math.max(1.0, Math.min(1.8, r.w * 0.5));
        windows.push({ room: r.i, from: r.x + (r.w - ww) / 2, to: r.x + (r.w + ww) / 2 });
      }
    });
    return { type: Math.max(0, Math.min(4, type | 0)), w: p.w * k, d: p.d * k, rooms, doors, windows, balcony, area: area > 0 ? area : nom, scale: k };
  }

  // Walls as segments [x1, y1, x2, y2, outer] with the door openings cut out (windows stay in the wall: the 3D model
  // draws a sill and a lintel there, the 2D plan a copper line). Used by both pictures and by the walk's collisions.
  function walls(lay) {
    const e = 1e-6, seen = {}, list = [];
    const add = (x1, y1, x2, y2) => {
      const key = [x1, y1, x2, y2].map((v) => v.toFixed(3)).join(',');
      if (seen[key]) return; seen[key] = 1;
      list.push([x1, y1, x2, y2]);
    };
    // Every room edge, split where it meets the edges of the neighbours, so a shared wall is one segment.
    const xs = new Set([0, lay.w]), ys = new Set([0, lay.d]);
    lay.rooms.forEach((r) => { xs.add(+r.x.toFixed(4)); xs.add(+(r.x + r.w).toFixed(4)); ys.add(+r.y.toFixed(4)); ys.add(+(r.y + r.h).toFixed(4)); });
    const X = [...xs].sort((a, b) => a - b), Y = [...ys].sort((a, b) => a - b);
    lay.rooms.forEach((r) => {
      const x0 = r.x, x1 = r.x + r.w, y0 = r.y, y1 = r.y + r.h;
      const cutsX = X.filter((v) => v > x0 + e && v < x1 - e), cutsY = Y.filter((v) => v > y0 + e && v < y1 - e);
      const hx = [x0, ...cutsX, x1], hy = [y0, ...cutsY, y1];
      for (let i = 0; i < hx.length - 1; i++) { add(hx[i], y0, hx[i + 1], y0); add(hx[i], y1, hx[i + 1], y1); }
      for (let i = 0; i < hy.length - 1; i++) { add(x0, hy[i], x0, hy[i + 1]); add(x1, hy[i], x1, hy[i + 1]); }
    });
    // Cut the doors out.
    let out = list;
    lay.doors.forEach((d) => {
      const next = [];
      out.forEach((s) => {
        const [x1, y1, x2, y2] = s;
        const horiz = Math.abs(y1 - y2) < e;
        if ((d.axis === 'x') !== horiz || Math.abs((horiz ? y1 : x1) - d.at) > e) { next.push(s); return; }
        const a = horiz ? x1 : y1, b = horiz ? x2 : y2;
        if (d.to <= a + e || d.from >= b - e) { next.push(s); return; }
        if (d.from > a + e) next.push(horiz ? [a, y1, d.from, y1] : [x1, a, x1, d.from]);
        if (d.to < b - e) next.push(horiz ? [d.to, y1, b, y1] : [x1, d.to, x1, b]);
      });
      out = next;
    });
    return out.map((s) => s.concat([s[1] < e && s[3] < e || s[1] > lay.d - e && s[3] > lay.d - e || s[0] < e && s[2] < e || s[0] > lay.w - e && s[2] > lay.w - e]));
  }

  S.plans = { layout, walls, types: [0, 1, 2, 3], nominalArea: (type) => Math.round(nominal(L[type] || L[1]) * 10) / 10 };
})();
