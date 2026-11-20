/* The apartment inside, for the 3D model (modeli-3d-bino-script.js → enterInside): walls with their thickness, door and
   window openings, wooden floors, tiles in the bathroom, the balcony with its railing and an example of furniture made of
   simple boxes and cylinders (no outside models). The geometry comes from S.plans (planirovki-data.js) — the same layout
   the 2D plan is drawn from. Loaded by the page only when someone opens «Посмотреть внутри». */
(function () {
  const S = (window.S = window.S || {});
  const WALL_H = 2.7, SILL = 0.9, HEAD = 2.2, OUTER = 0.25, INNER = 0.12, E = 1e-4;

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

    // Floor slab and the floors of the rooms (click targets: userData.room).
    box(lay.w + 0.5, 0.24, lay.d + 0.5, M.slab, lay.w / 2, -0.13, lay.d / 2, inner, false).receiveShadow = true;
    const floors = [];
    const floorOf = (r, tex, k) => {
      const t = tex.clone(); t.needsUpdate = true; t.repeat.set(r.w / 1.4, r.h / 1.4); textures.push(t);
      const m = new T.Mesh(new T.PlaneGeometry(r.w, r.h), new T.MeshStandardMaterial({ map: t, roughness: tex === wood ? 0.55 : 0.3 }));
      m.rotation.x = -Math.PI / 2; m.position.set(r.x + r.w / 2, 0.01, r.y + r.h / 2); m.receiveShadow = true; m.userData.room = k;
      inner.add(m); floors.push(m);
    };
    lay.rooms.forEach((r) => floorOf(r, r.wet ? tiles : wood, r.part ? lay.rooms.findIndex((x) => x.key === r.key && !x.part) : r.i));
    const b = lay.balcony;
    floorOf({ x: b.x, y: b.y, w: b.w, h: b.h }, balconyTiles, lay.rooms.length);
    box(b.w + 0.2, 0.2, b.h + 0.1, M.slab, b.x + b.w / 2, -0.11, b.y + b.h / 2 - 0.05, inner, false);

    // Walls. On the facade (y = 0) the windows leave a sill and a lintel; the balcony door — only a lintel.
    const segs = S.plans.walls(lay);
    const collision = [];
    const facadeOpen = lay.windows.map((w) => [w.from, w.to, w.door]);
    function wallPiece(x1, y1, x2, y2, outer, y0, y1h) {
      const L = Math.hypot(x2 - x1, y2 - y1); if (L < 0.02) return;
      const th = outer ? OUTER : INNER;
      const horiz = Math.abs(y1 - y2) < E;
      box(horiz ? L + th : th, y1h - y0, horiz ? th : L + th, outer ? M.outer : M.wall, (x1 + x2) / 2, (y0 + y1h) / 2, (y1 + y2) / 2);
    }
    segs.forEach(([x1, y1, x2, y2, outer]) => {
      const facade = Math.abs(y1) < E && Math.abs(y2) < E;
      if (!facade) { wallPiece(x1, y1, x2, y2, outer, 0, WALL_H); collision.push([x1, y1, x2, y2]); return; }
      // Split the facade segment by the openings.
      const a = Math.min(x1, x2), c = Math.max(x1, x2);
      let cur = a;
      facadeOpen.filter(([f, t]) => t > a + E && f < c - E).sort((p, q) => p[0] - q[0]).forEach(([f, t, door]) => {
        const f2 = Math.max(a, f), t2 = Math.min(c, t);
        if (f2 > cur + E) { wallPiece(cur, 0, f2, 0, true, 0, WALL_H); collision.push([cur, 0, f2, 0]); }
        if (!door) { wallPiece(f2, 0, t2, 0, true, 0, SILL); collision.push([f2, 0, t2, 0]); }
        wallPiece(f2, 0, t2, 0, true, HEAD, WALL_H);
        // Glass and its frame.
        const gw = t2 - f2, gy0 = door ? 0.02 : SILL, gh = HEAD - gy0;
        const g = new T.Mesh(new T.BoxGeometry(gw, gh, 0.03), M.glass); g.position.set(f2 + gw / 2, gy0 + gh / 2, 0); g.renderOrder = 2; inner.add(g);
        box(gw, 0.06, 0.12, M.frame, f2 + gw / 2, gy0 + 0.03, 0); box(gw, 0.06, 0.12, M.frame, f2 + gw / 2, HEAD - 0.03, 0);
        box(0.06, gh, 0.12, M.frame, f2 + gw / 2, gy0 + gh / 2, 0, inner, false);
        if (door) collision.push(null); // the balcony door: passable
        cur = t2;
      });
      if (c > cur + E) { wallPiece(cur, 0, c, 0, true, 0, WALL_H); collision.push([cur, 0, c, 0]); }
    });
    // Lintels over the doors (an open passage has none).
    lay.doors.forEach((d) => {
      if (d.open) return;
      const outer = !!d.entrance;
      const th = outer ? OUTER : INNER;
      if (d.axis === 'x') box(d.to - d.from, WALL_H - 2.1, th, outer ? M.outer : M.wall, (d.from + d.to) / 2, (2.1 + WALL_H) / 2, d.at);
      else box(th, WALL_H - 2.1, d.to - d.from, M.wall, d.at, (2.1 + WALL_H) / 2, (d.from + d.to) / 2);
    });
    // The entrance door leaf, open to the hall.
    const ent = lay.doors.find((d) => d.entrance);
    if (ent) {
      const leaf = box(0.9, 2.05, 0.05, M.wood, 0, 1.03, 0);
      leaf.geometry.translate(0.45, 0, 0);
      leaf.position.set(ent.from, 1.03, ent.at - 0.05); leaf.rotation.y = -Math.PI * 0.42;
    }
    // Balcony railing: glass on three sides with a handrail; it is a wall for the walk.
    [[b.x, b.y, b.x + b.w, b.y], [b.x, b.y, b.x, 0], [b.x + b.w, b.y, b.x + b.w, 0]].forEach(([x1, y1, x2, y2]) => {
      const horiz = Math.abs(y1 - y2) < E, L = Math.hypot(x2 - x1, y2 - y1);
      const r = new T.Mesh(new T.BoxGeometry(horiz ? L : 0.04, 1.0, horiz ? 0.04 : L), M.rail); r.position.set((x1 + x2) / 2, 0.55, (y1 + y2) / 2); r.renderOrder = 2; inner.add(r);
      box(horiz ? L + 0.06 : 0.06, 0.05, horiz ? 0.06 : L + 0.06, M.frame, (x1 + x2) / 2, 1.07, (y1 + y2) / 2);
      collision.push([x1, y1, x2, y2]);
    });

    // Ceiling — only for the walk (seen from above the apartment is cut open).
    const ceiling = new T.Mesh(new T.PlaneGeometry(lay.w + 0.4, lay.d + 0.4), M.ceiling);
    ceiling.rotation.x = Math.PI / 2; ceiling.position.set(lay.w / 2, WALL_H, lay.d / 2); ceiling.visible = false; inner.add(ceiling);

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
      const W = { front: { len: r.w, blocked: [] }, back: { len: r.w, blocked: [] }, left: { len: r.h, blocked: [] }, right: { len: r.h, blocked: [] } };
      lay.doors.forEach((d) => {
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
      if (r.y < E) lay.windows.forEach((w) => { if (w.room === rIndex || (w.to > r.x && w.from < r.x + r.w)) W.front.blocked.push([w.from - r.x, w.to - r.x]); });
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
    lay.rooms.forEach((r) => {
      if (r.part) return;
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
    { const g = ITEMS.chairs(); g.position.set(b.x + b.w / 2, 0, b.y + b.h / 2); furn.add(g); }

    // Labels: one per room (parts of one space together) and the balcony, with the area of the scaled layout.
    const rooms = lay.rooms.filter((r) => !r.part).map((r) => ({ i: r.i, key: r.key, area: r.area, cx: r.x + r.w / 2, cy: r.y + r.h / 2 }));
    rooms.push({ i: lay.rooms.length, key: 'balcony', area: b.area, cx: b.x + b.w / 2, cy: b.y + b.h / 2 });
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
