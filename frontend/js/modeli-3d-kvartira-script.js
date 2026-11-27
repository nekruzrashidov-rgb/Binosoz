/* The apartment inside, for the 3D model (modeli-3d-bino-script.js → enterInside): walls with their thickness, door and
   window openings, wooden floors, tiles in the bathroom, the balcony with its railing and an example of furniture made of
   simple boxes and cylinders (no outside models). The geometry comes from S.plans (planirovki-data.js) — the same layout
   the 2D plan is drawn from: rooms are polygons, walls are segments of any direction with their thickness (outer, between
   apartments, between rooms). Loaded by the page only when someone opens «Посмотреть внутри». */
(function () {
  const S = (window.S = window.S || {});
  const WALL_H = 2.7, SILL = 0.9, HEAD = 2.2, E = 1e-4;
  // Wall thickness by kind (S.plans.THICK: outer 0.4, party 0.2, inner 0.12), a little thinner in the model for the walk.
  const TH = (kind) => (S.plans.THICK[kind] || 0.12) * (kind === 'outer' ? 0.7 : 1);

  function canvasTex(T, w, h, draw) {
    const c = document.createElement('canvas'); c.width = w; c.height = h;
    draw(c.getContext('2d'), w, h);
    const t = new T.CanvasTexture(c); t.encoding = T.sRGBEncoding; t.wrapS = t.wrapT = T.RepeatWrapping; t.anisotropy = 4;
    return t;
  }
  function woodTex(T) {
    return canvasTex(T, 256, 256, (g, w, h) => {
      g.fillStyle = '#b48a5e'; g.fillRect(0, 0, w, h);
      for (let row = 0; row < 8; row++) {
        const y = row * 32, off = (row % 2) * 96;
        for (let x = -off; x < w; x += 192) {
          const v = 150 + ((row * 37 + x) % 40);
          g.fillStyle = 'rgb(' + (v + 30) + ',' + (v - 2) + ',' + (v - 40) + ')'; g.fillRect(x + 1, y + 1, 190, 30);
          g.strokeStyle = 'rgba(90,60,30,0.18)';
          for (let k = 0; k < 5; k++) { g.beginPath(); g.moveTo(x + 4, y + 6 + k * 5); g.bezierCurveTo(x + 60, y + 3 + k * 5, x + 120, y + 10 + k * 5, x + 186, y + 6 + k * 5); g.stroke(); }
        }
      }
    });
  }
  function tileTex(T, base, line) {
    return canvasTex(T, 128, 128, (g, w, h) => {
      g.fillStyle = base; g.fillRect(0, 0, w, h);
      g.strokeStyle = line; g.lineWidth = 3;
      for (let i = 0; i <= w; i += 64) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i, h); g.stroke(); g.beginPath(); g.moveTo(0, i); g.lineTo(w, i); g.stroke(); }
    });
  }

  // Free place along one wall of a room: blocked spans (doors, windows, furniture already there) are avoided.
  function spotOnWall(len, wallLen, blocked) {
    if (len > wallLen - 0.1) return null;
    const ok = (c) => blocked.every(([a, b]) => c + len / 2 <= a - 0.12 || c - len / 2 >= b + 0.12);
    const mid = wallLen / 2;
    if (ok(mid)) return mid;
    for (let d = 0.1; d < wallLen / 2; d += 0.1) {
      if (mid - d - len / 2 >= 0.05 && ok(mid - d)) return mid - d;
      if (mid + d + len / 2 <= wallLen - 0.05 && ok(mid + d)) return mid + d;
    }
    return null;
  }

  function build(T, lay, o) {
    o = o || {};
    const lin = o.lin || ((h) => new T.Color(h).convertSRGBToLinear());
    const mat = (c, extra) => new T.MeshStandardMaterial(Object.assign({ color: lin(c), roughness: 0.8 }, extra || {}));
    const M = {
      wall: mat(0xf1ece3, { roughness: 0.92 }), outer: mat(0xe2dccf, { roughness: 0.9 }), slab: mat(0xbfb7a8),
      glass: new T.MeshStandardMaterial({ color: lin(0x9cc4e4), roughness: 0.05, metalness: 0.2, transparent: true, opacity: 0.28, depthWrite: false }),
      frame: mat(0x34383f, { roughness: 0.4, metalness: 0.6 }), rail: new T.MeshStandardMaterial({ color: lin(0x7fa6c8), roughness: 0.05, transparent: true, opacity: 0.3, depthWrite: false }),
      fabric: mat(0x8d877c, { roughness: 1 }), fabric2: mat(0x5d6f80, { roughness: 1 }), cushion: mat(0xd8cfc0, { roughness: 1 }),
      wood: mat(0x8a5c38, { roughness: 0.6 }), woodLight: mat(0xc9a77c, { roughness: 0.6 }), white: mat(0xf4f2ee, { roughness: 0.35 }),
      metal: mat(0xb9bdc3, { roughness: 0.3, metalness: 0.7 }), dark: mat(0x2a2b30, { roughness: 0.5 }), rug: mat(0xa8774e, { roughness: 1 }),
      ceiling: new T.MeshStandardMaterial({ color: lin(0xf7f5f0), roughness: 0.95, side: T.DoubleSide }),
    };
    const wood = woodTex(T), tiles = tileTex(T, '#dfe3e4', '#b9c0c2'), balconyTiles = tileTex(T, '#c9c2b6', '#a59d90');
    const textures = [wood, tiles, balconyTiles];
    const group = new T.Group();
    const inner = new T.Group(); inner.position.x = -lay.w / 2; group.add(inner);
    const box = (w, h, d, m, x, y, z, parent, shadow) => {
      const b = new T.Mesh(new T.BoxGeometry(w, h, d), m); b.position.set(x, y, z);
      if (shadow !== false) { b.castShadow = true; b.receiveShadow = true; }
      (parent || inner).add(b); return b;
    };

    // A polygon of the plan as a horizontal surface at height y (plan y → world z).
    const flat = (poly, y) => { const g = new T.ShapeGeometry(new T.Shape(poly.map((p) => new T.Vector2(p.x, -p.y)))); g.rotateX(-Math.PI / 2); g.translate(0, y, 0); return g; };
    // Floor slab and the floors of the rooms (click targets: userData.room).
    { const slab = new T.Mesh(flat(lay.outline, -0.02), M.slab); slab.receiveShadow = true; inner.add(slab); }
    const floors = [];
    const floorOf = (poly, tex, k) => {
      const bb = poly.reduce((a, p) => ({ x0: Math.min(a.x0, p.x), y0: Math.min(a.y0, p.y), x1: Math.max(a.x1, p.x), y1: Math.max(a.y1, p.y) }), { x0: 1e9, y0: 1e9, x1: -1e9, y1: -1e9 });
      const t = tex.clone(); t.needsUpdate = true; t.repeat.set(1 / 1.4, 1 / 1.4); textures.push(t);
      const m = new T.Mesh(flat(poly, 0.01), new T.MeshStandardMaterial({ map: t, roughness: tex === wood ? 0.55 : 0.3 }));
      m.receiveShadow = true; m.userData.room = k; void bb;
      inner.add(m); floors.push(m);
    };
    lay.rooms.forEach((r) => floorOf(r.poly, r.wet ? tiles : wood, r.i));
    const b = lay.balcony;
    if (b) { floorOf(b.poly, balconyTiles, lay.rooms.length); const bs = new T.Mesh(flat(b.poly, -0.03), M.slab); inner.add(bs); }

    // Walls: segments of any direction (S.plans.walls) with the thickness of their kind; a wall on the facade is split by
    // its windows — a sill and a lintel (the balcony door — only a lintel), glass and a frame in the opening.
    const segs = S.plans.walls(lay);
    const collision = [];
    function wallPiece(x1, y1, x2, y2, kind, y0, y1h) {
      const L = Math.hypot(x2 - x1, y2 - y1); if (L < 0.02) return;
      const th = TH(kind);
      const m = new T.Mesh(new T.BoxGeometry(L + th, y1h - y0, th), kind === 'outer' ? M.outer : M.wall);
      m.position.set((x1 + x2) / 2, (y0 + y1h) / 2, (y1 + y2) / 2); m.rotation.y = -Math.atan2(y2 - y1, x2 - x1);
      m.castShadow = true; m.receiveShadow = true; inner.add(m);
    }
    const along = (x1, y1, x2, y2, p) => { const L = Math.hypot(x2 - x1, y2 - y1); return ((p.x - x1) * (x2 - x1) + (p.y - y1) * (y2 - y1)) / L; };
    const off = (x1, y1, x2, y2, p) => { const L = Math.hypot(x2 - x1, y2 - y1); return Math.abs((p.x - x1) * (y2 - y1) - (p.y - y1) * (x2 - x1)) / L; };
    segs.forEach(([x1, y1, x2, y2, , kind]) => {
      const L = Math.hypot(x2 - x1, y2 - y1), ux = (x2 - x1) / L, uy = (y2 - y1) / L;
      const P2 = (t) => [x1 + ux * t, y1 + uy * t];
      // The windows on this wall: their span along it.
      const opens = kind !== 'outer' ? [] : lay.windows.filter((w) => off(x1, y1, x2, y2, w.a) < 0.02 && off(x1, y1, x2, y2, w.b) < 0.02)
        .map((w) => { const a = along(x1, y1, x2, y2, w.a), c = along(x1, y1, x2, y2, w.b); return [Math.max(0, Math.min(a, c)), Math.min(L, Math.max(a, c)), w.door]; })
        .filter(([f, t]) => t - f > 0.1).sort((p, q) => p[0] - q[0]);
      if (!opens.length) { wallPiece(x1, y1, x2, y2, kind, 0, WALL_H); collision.push([x1, y1, x2, y2]); return; }
      let cur = 0;
      opens.forEach(([f, t, door]) => {
        if (f > cur + E) { const [a1, b1] = P2(cur), [a2, b2] = P2(f); wallPiece(a1, b1, a2, b2, kind, 0, WALL_H); collision.push([a1, b1, a2, b2]); }
        const [p1, q1] = P2(f), [p2, q2] = P2(t);
        if (!door) { wallPiece(p1, q1, p2, q2, kind, 0, SILL); collision.push([p1, q1, p2, q2]); }
        wallPiece(p1, q1, p2, q2, kind, HEAD, WALL_H);
        const gw = t - f, gy0 = door ? 0.02 : SILL, gh = HEAD - gy0, ry = -Math.atan2(uy, ux), mx = (p1 + p2) / 2, mz = (q1 + q2) / 2;
        const gl = new T.Mesh(new T.BoxGeometry(gw, gh, 0.03), M.glass); gl.position.set(mx, gy0 + gh / 2, mz); gl.rotation.y = ry; gl.renderOrder = 2; inner.add(gl);
        [[gy0 + 0.03], [HEAD - 0.03]].forEach(([y]) => { const fr = new T.Mesh(new T.BoxGeometry(gw, 0.06, 0.12), M.frame); fr.position.set(mx, y, mz); fr.rotation.y = ry; inner.add(fr); });
        cur = t;
      });
      if (L > cur + E) { const [a1, b1] = P2(cur); wallPiece(a1, b1, x2, y2, kind, 0, WALL_H); collision.push([a1, b1, x2, y2]); }
    });
    // Lintels over the doors (an open passage has none).
    lay.doors.forEach((d) => {
      if (d.open) return;
      const L = Math.hypot(d.b.x - d.a.x, d.b.y - d.a.y);
      const m = new T.Mesh(new T.BoxGeometry(L, WALL_H - 2.1, TH(d.entrance ? 'party' : 'inner')), d.entrance ? M.outer : M.wall);
      m.position.set((d.a.x + d.b.x) / 2, (2.1 + WALL_H) / 2, (d.a.y + d.b.y) / 2); m.rotation.y = -Math.atan2(d.b.y - d.a.y, d.b.x - d.a.x); inner.add(m);
    });
    // The entrance door leaf, open into the hall.
    const ent = lay.doors.find((d) => d.entrance);
    if (ent) {
      const leaf = box(0.9, 2.05, 0.05, M.wood, 0, 1.03, 0);
      leaf.geometry.translate(0.45, 0, 0);
      leaf.position.set(ent.a.x, 1.03, ent.a.y); leaf.rotation.y = -Math.atan2(ent.b.y - ent.a.y, ent.b.x - ent.a.x) - Math.PI * 0.42;
    }
    // Balcony, loggia or terrace railing: glass on its free edges (not on the facade) with a handrail; a wall for the walk.
    if (b) {
      const onOutline = (p, q) => lay.outline.some((u, i) => { const v = lay.outline[(i + 1) % lay.outline.length]; return off(u.x, u.y, v.x, v.y, p) < 0.02 && off(u.x, u.y, v.x, v.y, q) < 0.02; });
      b.poly.forEach((p, i) => {
        const q = b.poly[(i + 1) % b.poly.length]; if (onOutline(p, q)) return;
        const L = Math.hypot(q.x - p.x, q.y - p.y), ry = -Math.atan2(q.y - p.y, q.x - p.x);
        const r = new T.Mesh(new T.BoxGeometry(L, 1.0, 0.04), M.rail); r.position.set((p.x + q.x) / 2, 0.55, (p.y + q.y) / 2); r.rotation.y = ry; r.renderOrder = 2; inner.add(r);
        const hr = new T.Mesh(new T.BoxGeometry(L + 0.06, 0.05, 0.06), M.frame); hr.position.set((p.x + q.x) / 2, 1.07, (p.y + q.y) / 2); hr.rotation.y = ry; inner.add(hr);
        collision.push([p.x, p.y, q.x, q.y]);
      });
    }

    // Ceiling — only for the walk (seen from above the apartment is cut open).
    const ceiling = new T.Mesh(flat(lay.outline, WALL_H), M.ceiling); ceiling.visible = false; inner.add(ceiling);

    // ---------- furniture: an example arrangement ----------
    const furn = new T.Group(); inner.add(furn);
    const fbox = (p, w, h, d, m, x, y, z) => box(w, h, d, m, x, y, z, p);
    const cyl = (p, rt, rb, h, m, x, y, z) => { const c = new T.Mesh(new T.CylinderGeometry(rt, rb, h, 14), m); c.position.set(x, y, z); c.castShadow = true; p.add(c); return c; };
    // Items are built facing +z with their back at z = 0 and their middle at x = 0.
    const ITEMS = {
      sofa(len) { const g = new T.Group(); fbox(g, len, 0.42, 0.9, M.fabric, 0, 0.21, 0.45); fbox(g, len, 0.5, 0.22, M.fabric, 0, 0.62, 0.11); fbox(g, 0.2, 0.62, 0.9, M.fabric, -len / 2 + 0.1, 0.31, 0.45); fbox(g, 0.2, 0.62, 0.9, M.fabric, len / 2 - 0.1, 0.31, 0.45); for (let k = 0; k < 2; k++) fbox(g, len / 2 - 0.25, 0.12, 0.6, M.cushion, (k ? 1 : -1) * (len / 4 - 0.05), 0.48, 0.52); return g; },
      tv(len) { const g = new T.Group(); fbox(g, len, 0.45, 0.42, M.woodLight, 0, 0.225, 0.21); fbox(g, Math.min(1.3, len - 0.2), 0.75, 0.05, M.dark, 0, 0.9, 0.1); return g; },
      bed(len) { const g = new T.Group(); const w = Math.min(1.6, len - 1.0); fbox(g, w + 0.1, 0.32, 2.05, M.wood, 0, 0.16, 1.02); fbox(g, w, 0.22, 1.95, M.white, 0, 0.42, 1.0); fbox(g, w, 0.06, 1.3, M.fabric2, 0, 0.56, 1.3); fbox(g, w + 0.1, 0.9, 0.08, M.wood, 0, 0.6, 0.04); for (let k = 0; k < 2; k++) { fbox(g, w / 2 - 0.1, 0.14, 0.36, M.cushion, (k ? 1 : -1) * w / 4, 0.6, 0.3); fbox(g, 0.42, 0.45, 0.4, M.woodLight, (k ? 1 : -1) * (w / 2 + 0.3), 0.225, 0.22); } return g; },
      wardrobe(len) { const g = new T.Group(); fbox(g, len, 2.2, 0.6, M.woodLight, 0, 1.1, 0.3); fbox(g, 0.02, 2.1, 0.01, M.dark, 0, 1.1, 0.605); return g; },
      kitchen(len) { const g = new T.Group(); const run = len - 0.7; fbox(g, run, 0.86, 0.6, M.white, -0.35, 0.43, 0.3); fbox(g, run, 0.04, 0.62, M.dark, -0.35, 0.88, 0.31); fbox(g, run, 0.7, 0.35, M.white, -0.35, 1.85, 0.18); fbox(g, 0.6, 0.02, 0.45, M.metal, -0.35 + run / 4, 0.905, 0.3); fbox(g, 0.66, 1.9, 0.65, M.metal, len / 2 - 0.33, 0.95, 0.33); return g; },
      table(n) { const g = new T.Group(); fbox(g, 0.9, 0.04, 0.9, M.woodLight, 0, 0.74, 0); [[-0.38, -0.38], [0.38, -0.38], [-0.38, 0.38], [0.38, 0.38]].forEach(([x, z]) => fbox(g, 0.05, 0.72, 0.05, M.wood, x, 0.36, z)); [[0, -0.65, 0], [0, 0.65, Math.PI], [-0.65, 0, Math.PI / 2], [0.65, 0, -Math.PI / 2]].slice(0, n).forEach(([x, z, r]) => { const c = new T.Group(); fbox(c, 0.42, 0.04, 0.42, M.wood, 0, 0.45, 0); fbox(c, 0.42, 0.45, 0.04, M.wood, 0, 0.7, -0.2); [[-0.18, -0.18], [0.18, -0.18], [-0.18, 0.18], [0.18, 0.18]].forEach(([a, bb]) => fbox(c, 0.03, 0.45, 0.03, M.dark, a, 0.22, bb)); c.position.set(x, 0, z); c.rotation.y = r; g.add(c); }); return g; },
      coffee() { const g = new T.Group(); fbox(g, 1.6, 0.01, 1.1, M.rug, 0, 0.012, 0); fbox(g, 0.9, 0.05, 0.5, M.woodLight, 0, 0.4, 0); fbox(g, 0.8, 0.38, 0.4, M.wood, 0, 0.19, 0); return g; },
      tub(len) { const g = new T.Group(); fbox(g, len, 0.55, 0.72, M.white, 0, 0.275, 0.36); fbox(g, len - 0.14, 0.04, 0.58, M.fabric2, 0, 0.52, 0.36); return g; },
      sink() { const g = new T.Group(); fbox(g, 0.6, 0.12, 0.45, M.white, 0, 0.82, 0.22); fbox(g, 0.45, 0.6, 0.02, M.metal, 0, 1.35, 0.02); return g; },
      toilet() { const g = new T.Group(); fbox(g, 0.38, 0.4, 0.16, M.white, 0, 0.6, 0.08); cyl(g, 0.19, 0.16, 0.4, M.white, 0, 0.2, 0.42); fbox(g, 0.36, 0.03, 0.44, M.white, 0, 0.42, 0.4); return g; },
      shoe(len) { const g = new T.Group(); fbox(g, len, 1.0, 0.4, M.woodLight, 0, 0.5, 0.2); fbox(g, len * 0.6, 0.7, 0.02, M.metal, 0, 1.6, 0.02); return g; },
      chairs() { const g = new T.Group(); fbox(g, 0.5, 0.04, 0.5, M.woodLight, 0, 0.6, 0); cyl(g, 0.04, 0.04, 0.6, M.metal, 0, 0.3, 0); [-0.6, 0.6].forEach((x) => { fbox(g, 0.45, 0.06, 0.45, M.fabric, x, 0.42, 0); fbox(g, 0.45, 0.45, 0.06, M.fabric, x, 0.68, x < 0 ? -0.2 : -0.2); }); return g; },
    };
    // Openings of every wall of a room, in its own coordinates.
    function roomWalls(r, rIndex) {
      // r — the room's inner rectangle { x, y, w, h }; only the axis-aligned doors and windows block its walls.
      const W = { front: { len: r.w, blocked: [] }, back: { len: r.w, blocked: [] }, left: { len: r.h, blocked: [] }, right: { len: r.h, blocked: [] } };
      lay.doors.forEach((d) => {
        if (!d.axis) return;
        if (d.axis === 'x') {
          if (d.to <= r.x || d.from >= r.x + r.w) return;
          if (Math.abs(d.at - r.y) < E) W.front.blocked.push([d.from - r.x, d.to - r.x]);
          if (Math.abs(d.at - (r.y + r.h)) < E) W.back.blocked.push([d.from - r.x, d.to - r.x]);
        } else {
          if (d.to <= r.y || d.from >= r.y + r.h) return;
          if (Math.abs(d.at - r.x) < E) W.left.blocked.push([d.from - r.y, d.to - r.y]);
          if (Math.abs(d.at - (r.x + r.w)) < E) W.right.blocked.push([d.from - r.y, d.to - r.y]);
        }
      });
      lay.windows.forEach((w) => {
        if (w.axis === 'x' && w.to > r.x && w.from < r.x + r.w) {
          if (Math.abs(w.at - r.y) < 0.3) W.front.blocked.push([w.from - r.x, w.to - r.x]);
          if (Math.abs(w.at - (r.y + r.h)) < 0.3) W.back.blocked.push([w.from - r.x, w.to - r.x]);
        } else if (w.axis === 'y' && w.to > r.y && w.from < r.y + r.h) {
          if (Math.abs(w.at - r.x) < 0.3) W.left.blocked.push([w.from - r.y, w.to - r.y]);
          if (Math.abs(w.at - (r.x + r.w)) < 0.3) W.right.blocked.push([w.from - r.y, w.to - r.y]);
        }
      });
      void rIndex;
      return W;
    }
    // Put an item along the first wall with room for it.
    function place(r, W, item, len, depth, prefer) {
      for (const side of prefer) {
        const w = W[side];
        const c = spotOnWall(len, w.len, w.blocked);
        if (c == null) continue;
        const sideDepth = side === 'front' || side === 'back' ? r.h : r.w;
        if (depth > sideDepth - 0.6) continue;
        w.blocked.push([c - len / 2, c + len / 2]);
        const g = ITEMS[item](len);
        if (side === 'back') { g.position.set(r.x + c, 0, r.y + r.h - 0.07); g.rotation.y = Math.PI; }
        else if (side === 'front') { g.position.set(r.x + c, 0, r.y + 0.14); }
        else if (side === 'left') { g.position.set(r.x + 0.07, 0, r.y + c); g.rotation.y = Math.PI / 2; }
        else { g.position.set(r.x + r.w - 0.07, 0, r.y + c); g.rotation.y = -Math.PI / 2; }
        furn.add(g);
        return { side, c, g };
      }
      return null;
    }
    const centre = (r, item, arg, dx, dz) => { const g = ITEMS[item](arg); g.position.set(r.x + r.w / 2 + (dx || 0), 0, r.y + r.h / 2 + (dz || 0)); furn.add(g); return g; };
    lay.rooms.forEach((room) => {
      // The furniture stands in the largest rectangle inside the room (a polygon room keeps its corners free).
      const r = Object.assign({ key: room.key, i: room.i }, S.plans.innerRect ? S.plans.innerRect(room.poly) : room);
      const W = roomWalls(r, r.i);
      const fitLen = (max, wall) => Math.min(max, Math.max(r.w, r.h) - 0.6);
      if (r.key === 'living') {
        const s = place(r, W, 'sofa', fitLen(2.2), 0.95, ['back', 'left', 'right']);
        place(r, W, 'tv', Math.min(1.6, fitLen(1.6)), 0.45, s && s.side === 'back' ? ['left', 'right', 'front'] : ['right', 'left', 'back']);
        if (r.w > 2.8 && r.h > 2.8) centre(r, 'coffee', 0, 0, s && s.side === 'back' ? 0.25 : 0);
        // A studio: the kitchen corner is a room of its own in the layout.
      } else if (r.key === 'bedroom' || r.key === 'bedroom2' || r.key === 'bedroom3') {
        place(r, W, 'bed', Math.min(2.6, fitLen(2.6)), 2.1, ['back', 'left', 'right']);
        place(r, W, 'wardrobe', Math.min(1.6, fitLen(1.6)), 0.6, ['left', 'right', 'back', 'front']);
      } else if (r.key === 'kitchen') {
        place(r, W, 'kitchen', Math.min(3.0, fitLen(3.0)), 0.65, ['back', 'right', 'left']);
        if (r.w > 2.4 && r.h > 2.4) centre(r, 'table', r.w > 3 && r.h > 3 ? 4 : 2, 0, -0.2);
      } else if (r.key === 'bath') {
        place(r, W, 'tub', Math.min(1.7, Math.max(r.w, r.h) - 0.3), 0.72, ['back', 'left', 'right', 'front']);
        place(r, W, 'sink', 0.6, 0.45, ['left', 'right', 'back', 'front']);
        place(r, W, 'toilet', 0.42, 0.7, ['right', 'left', 'back', 'front']);
      } else if (r.key === 'wc') {
        place(r, W, 'toilet', 0.42, 0.7, ['back', 'left', 'right']);
      } else if (r.key === 'hall') {
        place(r, W, 'shoe', Math.min(1.4, fitLen(1.4)), 0.4, ['left', 'right', 'back', 'front']);
      }
    });
    if (b && b.kind !== 'terrace') { const g = ITEMS.chairs(); g.position.set(b.x + b.w / 2, 0, b.y + b.h / 2); furn.add(g); }

    // Labels: one per room (parts of one space together) and the balcony, with the area of the scaled layout.
    const rooms = lay.rooms.map((r) => ({ i: r.i, key: r.key, area: r.area, cx: r.label.x, cy: r.label.y }));
    if (b) rooms.push({ i: lay.rooms.length, key: b.kind === 'terrace' ? 'terrace' : b.kind === 'loggia' ? 'loggia' : 'balcony', area: b.area, cx: b.x + b.w / 2, cy: b.y + b.h / 2 });
    // A floor tells which label (index in «rooms») it belongs to.
    floors.forEach((f) => { f.userData.room = rooms.findIndex((r) => r.i === f.userData.room); });
    return {
      group, inner, floors, rooms,
      walls: collision.filter(Boolean),
      setCeiling(v) { ceiling.visible = !!v; },
      setFurniture(v) { furn.visible = !!v; },
      textures,
    };
  }

  S.House3D = Object.assign(S.House3D || {}, { interior: { build } });
})();
