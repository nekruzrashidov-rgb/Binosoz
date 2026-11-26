/* Geometry of the complex's blocks (metres, x to the right, y down the plan; the same as PlanGeometry.cs on the server):
   the block outline (Footprint) with rounded corners, its place on the site (SitePosition), and the floor plan — the units of
   a floor laid along the facade in a band of a given depth, the corridor and the lift core inside (S.geom.floor). The 2D plan
   of the catalog and the 3D model read the same result, so a window of the model and a room of the plan are one apartment.
   A point is [x, y] or [x, y, r]: r — the radius of the rounded corner at that vertex. */
(function () {
  const S = (window.S = window.S || {});
  const ARC = 6; // segments of a rounded corner (PlanGeometry.ArcSegments)

  const pt = (p) => ({ x: +p[0], y: +p[1], r: p.length > 2 ? +p[2] : 0 });
  const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
  const unit = (x, y) => { const l = Math.hypot(x, y); return l < 1e-12 ? { x: 0, y: 0 } : { x: x / l, y: y / l }; };

  function signedArea(ring) {
    let s = 0;
    for (let i = 0; i < ring.length; i++) { const a = ring[i], b = ring[(i + 1) % ring.length]; s += a.x * b.y - b.x * a.y; }
    return s / 2;
  }
  const area = (ring) => Math.abs(signedArea(ring));

  // The rounded corner of vertex i: the arc points (an empty list for a sharp corner).
  function arcAt(ring, i) {
    const n = ring.length, cur = ring[i];
    if (!(cur.r > 0)) return null;
    const prev = ring[(i - 1 + n) % n], next = ring[(i + 1) % n];
    const u = unit(prev.x - cur.x, prev.y - cur.y), v = unit(next.x - cur.x, next.y - cur.y);
    const angle = Math.acos(Math.max(-1, Math.min(1, u.x * v.x + u.y * v.y)));
    if (angle < 1e-3 || Math.PI - angle < 1e-3) return null;
    const tanHalf = Math.tan(angle / 2);
    const t = Math.min(cur.r / tanHalf, Math.min(dist(prev, cur), dist(cur, next)) * 0.45), rr = t * tanHalf;
    const a = { x: cur.x + u.x * t, y: cur.y + u.y * t }, b = { x: cur.x + v.x * t, y: cur.y + v.y * t };
    const bis = unit(u.x + v.x, u.y + v.y), d = rr / Math.sin(angle / 2);
    const c = { x: cur.x + bis.x * d, y: cur.y + bis.y * d };
    const a0 = Math.atan2(a.y - c.y, a.x - c.x);
    let sweep = Math.atan2(b.y - c.y, b.x - c.x) - a0;
    while (sweep > Math.PI) sweep -= 2 * Math.PI;
    while (sweep < -Math.PI) sweep += 2 * Math.PI;
    const pts = [];
    for (let k = 0; k <= ARC; k++) { const g = a0 + sweep * k / ARC; pts.push({ x: c.x + rr * Math.cos(g), y: c.y + rr * Math.sin(g) }); }
    return { pts, t };
  }
  // The outline with its rounded corners as plain points.
  function expand(raw) {
    const ring = raw.map(pt), out = [];
    ring.forEach((p, i) => { const a = arcAt(ring, i); if (a) out.push(...a.pts); else out.push({ x: p.x, y: p.y }); });
    return out;
  }
  // A point of the site: the block's outline turned by rotationDeg (clockwise on the plan) and moved to (x, y).
  function place(ring, pos) {
    if (!pos) return ring.map((p) => ({ x: p.x, y: p.y }));
    const a = (pos.rotationDeg || 0) * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
    return ring.map((p) => ({ x: (pos.x || 0) + p.x * c - p.y * s, y: (pos.y || 0) + p.x * s + p.y * c }));
  }
  function bounds(ring) {
    const xs = ring.map((p) => p.x), ys = ring.map((p) => p.y);
    return { x0: Math.min(...xs), y0: Math.min(...ys), x1: Math.max(...xs), y1: Math.max(...ys) };
  }
  function contains(ring, p) {
    let inside = false;
    for (let i = 0, j = ring.length - 1; i < ring.length; j = i++) {
      const a = ring[i], b = ring[j];
      if ((a.y > p.y) !== (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
    }
    return inside;
  }
  function segDist(p, a, b) {
    const dx = b.x - a.x, dy = b.y - a.y, l2 = dx * dx + dy * dy;
    const t = l2 < 1e-12 ? 0 : Math.max(0, Math.min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / l2));
    return Math.hypot(p.x - a.x - dx * t, p.y - a.y - dy * t);
  }
  const ringDist = (ring, p) => Math.min(...ring.map((a, i) => segDist(p, a, ring[(i + 1) % ring.length])));
  // The smallest distance between two outlines (0 — they touch, cross or one is inside the other).
  function gap(a, b) {
    if (contains(a, b[0]) || contains(b, a[0])) return 0;
    let best = Infinity;
    a.forEach((p) => { best = Math.min(best, ringDist(b, p)); });
    b.forEach((p) => { best = Math.min(best, ringDist(a, p)); });
    return best;
  }
  // The two closest points of two outlines (for the dimension line on «Сверху»).
  function closest(a, b) {
    let best = { d: Infinity };
    const test = (ring, p, swap) => ring.forEach((q, i) => {
      const r = ring[(i + 1) % ring.length], dx = r.x - q.x, dy = r.y - q.y, l2 = dx * dx + dy * dy;
      const t = l2 < 1e-12 ? 0 : Math.max(0, Math.min(1, ((p.x - q.x) * dx + (p.y - q.y) * dy) / l2));
      const c = { x: q.x + dx * t, y: q.y + dy * t }, d = Math.hypot(p.x - c.x, p.y - c.y);
      if (d < best.d) best = swap ? { d, a: c, b: p } : { d, a: p, b: c };
    });
    a.forEach((p) => test(b, p, false));
    b.forEach((p) => test(a, p, true));
    return best;
  }

  // The outline moved inside by «depth» (mitred corners). An edge that the move turns around (a short edge of a bay or of a
  // rounded corner) is dropped and its neighbours meet — the inner outline stays simple. Returns, for every vertex of the
  // outline, its point on the inner one (several vertices can share one inner point).
  function offsetInward(ring, depth) {
    const n = ring.length, cw = signedArea(ring) > 0 ? 1 : -1;
    // Inward normal of edge i (from i to i+1): for a clockwise ring on the screen axes — to the right of the direction.
    const lineOf = (i) => {
      const a = ring[i], b = ring[(i + 1) % n], d = unit(b.x - a.x, b.y - a.y);
      const nx = -d.y * cw, ny = d.x * cw;
      return { p: { x: a.x + nx * depth, y: a.y + ny * depth }, d };
    };
    const meet = (l1, l2) => {
      const den = l1.d.x * l2.d.y - l1.d.y * l2.d.x;
      if (Math.abs(den) < 1e-9) return { x: l2.p.x, y: l2.p.y };
      const t = ((l2.p.x - l1.p.x) * l2.d.y - (l2.p.y - l1.p.y) * l2.d.x) / den;
      return { x: l1.p.x + l1.d.x * t, y: l1.p.y + l1.d.y * t };
    };
    // Edges still alive; vertex i of the outline belongs to the inner point between the alive edges around it.
    let alive = ring.map((_, i) => i);
    const lines = ring.map((_, i) => lineOf(i));
    for (let guard = 0; guard < n; guard++) {
      const m = alive.length;
      if (m < 3) break;
      const pts = alive.map((e, k) => meet(lines[alive[(k - 1 + m) % m]], lines[e]));
      let dropped = -1;
      for (let k = 0; k < m && dropped < 0; k++) {
        const a = pts[k], b = pts[(k + 1) % m], d = lines[alive[k]].d;
        if ((b.x - a.x) * d.x + (b.y - a.y) * d.y < 1e-6) dropped = k;
      }
      if (dropped < 0) break;
      alive = alive.filter((_, k) => k !== dropped);
    }
    const m = alive.length;
    const innerPts = alive.map((e, k) => meet(lines[alive[(k - 1 + m) % m]], lines[e]));
    // Outline vertex i → the inner point of the first alive edge starting at or after i.
    const at = new Array(n);
    for (let i = 0; i < n; i++) {
      let k = -1;
      for (let s = 0; s < n && k < 0; s++) { const e = (i + s) % n; const idx = alive.indexOf(e); if (idx >= 0) k = idx; }
      at[i] = innerPts[k];
    }
    return { inner: innerPts, at };
  }

  // The band between the outline and its inner outline, cut into units along the facade.
  // shares: the areas (or weights) of the units in plan order, starting at vertex 0 of the outline, clockwise.
  // Returns every unit's polygon (with the rounded corners on its facade), its facade (the outline's pieces with their
  // direction) and the inner outline (the corridor and the lift core).
  function band(raw, depth, shares) {
    const ring = raw.map(pt), n = ring.length;
    const off = offsetInward(ring, depth);
    const O = (i, t) => { const a = ring[i], b = ring[(i + 1) % n]; return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t }; };
    const I = (i, t) => { const a = off.at[i], b = off.at[(i + 1) % n]; return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t }; };
    const quad = (i, t0, t1) => area([O(i, t0), O(i, t1), I(i, t1), I(i, t0)]);
    const edgeArea = ring.map((_, i) => quad(i, 0, 1));
    const total = edgeArea.reduce((s, v) => s + v, 0);
    // The position along the band (edge + fraction) where the band's area from the start reaches «target».
    function posAt(target) {
      let acc = 0;
      for (let i = 0; i < n; i++) {
        if (acc + edgeArea[i] >= target - 1e-9 || i === n - 1) {
          let lo = 0, hi = 1;
          for (let it = 0; it < 40; it++) { const mid = (lo + hi) / 2; if (acc + quad(i, 0, mid) < target) lo = mid; else hi = mid; }
          return { i, t: (lo + hi) / 2 };
        }
        acc += edgeArea[i];
      }
      return { i: n - 1, t: 1 };
    }
    const sum = shares.reduce((s, v) => s + v, 0) || 1;
    const cuts = [{ i: 0, t: 0 }];
    let acc = 0;
    shares.forEach((v, k) => { acc += v; cuts.push(k === shares.length - 1 ? { i: n, t: 0 } : posAt(total * acc / sum)); });
    const arcs = ring.map((_, i) => arcAt(ring, i));
    const units = shares.map((_, k) => {
      const a = cuts[k], b = cuts[k + 1];
      const outer = [O(a.i % n, a.t)], facade = [];
      let from = O(a.i % n, a.t);
      // Outline vertices strictly inside the unit (vertex v starts edge v); a rounded corner brings its arc.
      const lastV = b.t > 1e-9 ? b.i : b.i - 1;
      for (let v = a.i + 1; v <= lastV; v++) {
        const vi = v % n, arc = arcs[vi];
        if (arc) { facade.push({ a: from, b: arc.pts[0] }); for (let q = 0; q < arc.pts.length; q++) { outer.push(arc.pts[q]); if (q) facade.push({ a: arc.pts[q - 1], b: arc.pts[q], arc: true }); } from = arc.pts[arc.pts.length - 1]; }
        else { outer.push({ x: ring[vi].x, y: ring[vi].y }); facade.push({ a: from, b: ring[vi] }); from = ring[vi]; }
      }
      const end = b.i >= n ? O(0, 0) : O(b.i, b.t);
      outer.push(end); facade.push({ a: from, b: end });
      const inner = [b.i >= n ? I(n - 1, 1) : I(b.i, b.t)];
      for (let v = lastV; v > a.i; v--) inner.push(off.at[v % n]);
      inner.push(I(a.i % n, a.t));
      // Equal neighbours (a dropped edge) are one point.
      const poly = outer.concat(inner).filter((p, q, arr) => q === 0 || dist(p, arr[q - 1]) > 1e-6);
      return { poly, facade: facade.filter((s) => dist(s.a, s.b) > 0.05), area: area(poly) };
    });
    return { units, inner: off.inner, total, outlineArea: area(expand(raw)) };
  }

  // The point of a polygon farthest from its edges (a grid search; enough for placing the lift core).
  function pole(ring) {
    const b = bounds(ring); let best = { d: -1, x: (b.x0 + b.x1) / 2, y: (b.y0 + b.y1) / 2 };
    for (let k = 0; k < 2; k++) {
      const step = Math.max(b.x1 - b.x0, b.y1 - b.y0) / (k ? 60 : 30);
      const cx = best.x, cy = best.y, span = k ? step * 8 : Infinity;
      for (let x = Math.max(b.x0, cx - span); x <= Math.min(b.x1, cx + span); x += step) {
        for (let y = Math.max(b.y0, cy - span); y <= Math.min(b.y1, cy + span); y += step) {
          const p = { x, y };
          if (!contains(ring, p)) continue;
          const d = ringDist(ring, p);
          if (d > best.d) best = { d, x, y };
        }
      }
    }
    return best;
  }

  // The floor of a block: units in the band along the facade, the corridor (the inner outline) and the lift core — three
  // lifts in a row and the stairs — at the point of the corridor farthest from its walls, turned along its longest edge.
  // o: { depth (m, 8 by default), lifts (3) }.
  function floor(raw, shares, o) {
    o = o || {};
    const r = band(raw, o.depth || 8, shares);
    const c = pole(r.inner);
    // Direction of the core: along the inner outline's longest edge.
    let ang = 0, len = 0;
    r.inner.forEach((p, i) => { const q = r.inner[(i + 1) % r.inner.length], l = dist(p, q); if (l > len) { len = l; ang = Math.atan2(q.y - p.y, q.x - p.x); } });
    const lifts = o.lifts == null ? 3 : o.lifts, shaft = 2.2, stair = 3.0;
    const w = Math.max(4, lifts * shaft + stair + 0.6), d = 2.6;
    const ca = Math.cos(ang), sa = Math.sin(ang);
    // Moved along its own axis until all four corners are in the corridor (when the corridor allows it).
    const inside = (cx, cy) => [[-w / 2, -d / 2], [w / 2, -d / 2], [w / 2, d / 2], [-w / 2, d / 2]].every(([lx, ly]) => contains(r.inner, { x: cx + lx * ca - ly * sa, y: cy + lx * sa + ly * ca }));
    for (let k = 0; k <= 40 && !inside(c.x, c.y); k++) {
      const s = (k % 2 ? 1 : -1) * Math.ceil(k / 2) * 0.5;
      if (inside(c.x + s * ca, c.y + s * sa)) { c.x += s * ca; c.y += s * sa; break; }
    }
    const at = (lx, ly) => ({ x: c.x + lx * ca - ly * sa, y: c.y + lx * sa + ly * ca });
    const rect = (x0, y0, x1, y1) => [at(x0, y0), at(x1, y0), at(x1, y1), at(x0, y1)];
    const core = { poly: rect(-w / 2, -d / 2, w / 2, d / 2), center: c, angle: ang,
      lifts: Array.from({ length: lifts }, (_, k) => rect(-w / 2 + 0.3 + k * shaft, -d / 2 + 0.2, -w / 2 + 0.3 + (k + 1) * shaft - 0.15, d / 2 - 0.2)),
      stair: rect(w / 2 - stair - 0.3, -d / 2 + 0.2, w / 2 - 0.3, d / 2 - 0.2) };
    return { units: r.units, corridor: r.inner, core, total: r.total, outlineArea: r.outlineArea, innerArea: area(r.inner) };
  }

  // Parking spots of a level: rows of 2.5 × 5.3 m along the longest axis of the outline, a 6 m driveway between pairs of rows;
  // only spots wholly inside the outline (and not on the lift core) count. Returns the first «count» spots.
  function parking(raw, count, o) {
    o = o || {};
    const ring = expand(raw), b = bounds(ring), wide = b.x1 - b.x0 >= b.y1 - b.y0;
    const W = 2.5, L = 5.3, lane = 6, spots = [];
    const ok = (cx, cy, hw, hh) => [[-hw, -hh], [hw, -hh], [hw, hh], [-hw, hh]].every(([dx, dy]) => contains(ring, { x: cx + dx, y: cy + dy }) && ringDist(ring, { x: cx + dx, y: cy + dy }) > 0.4)
      && !(o.avoid && contains(o.avoid, { x: cx, y: cy }));
    const a0 = wide ? b.y0 : b.x0, a1 = wide ? b.y1 : b.x1, s0 = wide ? b.x0 : b.y0, s1 = wide ? b.x1 : b.y1;
    for (let row = a0 + 0.6 + L / 2, k = 0; row < a1 - L / 2 && spots.length < count; k++) {
      for (let s = s0 + 0.6 + W / 2; s < s1 - W / 2 && spots.length < count; s += W) {
        const cx = wide ? s : row, cy = wide ? row : s;
        if (ok(cx, cy, wide ? W / 2 : L / 2, wide ? L / 2 : W / 2)) spots.push({ x: cx, y: cy, w: wide ? W : L, h: wide ? L : W });
      }
      row += k % 2 ? L + lane : L;
    }
    return spots;
  }

  // The outline of floor n of a block: the setback (Setbacks, «from floor») that applies to it, otherwise the footprint.
  function footprintOf(b, n) {
    let fp = b.footprint, from = -Infinity;
    (b.setbacks || []).forEach((s) => { if (n >= s.fromFloor && s.fromFloor > from && s.footprint && s.footprint.length >= 3) { fp = s.footprint; from = s.fromFloor; } });
    return fp;
  }

  S.geom = { pt, area, signedArea, expand, place, bounds, contains, gap, closest, offsetInward, band, floor, parking, pole, footprintOf, ringDist };
})();
