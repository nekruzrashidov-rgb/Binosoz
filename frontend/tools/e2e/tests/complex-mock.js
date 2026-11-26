// A mocked API of the complex «Rich Tower» for the e2e tests: the three blocks of the seed (outlines, places, setbacks),
// floors −2…N with parking spots, shops, apartments and penthouses, the site plan with the playground and the pitch.
// Not a test file itself (no .spec).
const BLOCKS = [
  { code: 'A', floors: 22, sort: 1, footprint: [[10, 0], [38, 0], [38, 18], [19, 18], [19, 34], [0, 34], [0, 0, 6]], pos: { x: -89, y: -50, rotationDeg: 0 },
    top: [[10, 2.5], [38, 2.5], [38, 18], [19, 18], [19, 34], [2.5, 34], [2.5, 2.5, 5]], depth: 6.5, areas: [54, 158, 72, 90, 158, 54, 72, 149], rooms: [0, 4, 1, 2, 4, 0, 1, 3] },
  { code: 'B', floors: 20, sort: 2, footprint: [[24, 0], [31, 0], [32.5, -1.5], [37.5, -1.5], [39, 0], [46, 0], [50, 4], [50, 16], [46, 20], [39, 20], [37.5, 21.5], [32.5, 21.5], [31, 20], [19, 20], [17.5, 21.5], [12.5, 21.5], [11, 20], [4, 20], [0, 16], [0, 4], [4, 0], [11, 0], [12.5, -1.5], [17.5, -1.5], [19, 0]],
    pos: { x: -25, y: -60, rotationDeg: 0 }, top: [[24, 0], [43, 0], [47, 4], [47, 16], [43, 20], [7, 20], [3, 16], [3, 4], [7, 0]], depth: 8, areas: [52, 81, 170, 90, 52, 81, 170, 90], rooms: [0, 1, 4, 2, 0, 1, 4, 2] },
  { code: 'C', floors: 20, sort: 3, footprint: [[21, 0], [42, 0, 8], [42, 28], [27, 28], [27, 15], [15, 15], [15, 28], [0, 28], [0, 0, 8]], pos: { x: 81, y: -55, rotationDeg: 90 },
    top: [[21, 1.5], [40.5, 1.5, 7], [40.5, 28], [27, 28], [27, 15], [15, 15], [15, 28], [1.5, 28], [1.5, 1.5, 7]], depth: 6.5, areas: [67, 181, 98, 122, 122, 98, 181, 67], rooms: [0, 4, 1, 2, 2, 1, 4, 0] },
];
const AMENITIES = ['underground_parking', 'closed_yard', 'playground', 'football_field', 'elevators', 'landscaping', 'commercial'];
const SITE = { boundary: [[-100, -75], [105, -75], [105, 22], [-100, 22]], zones: [
  { kind: 'football_field', polygon: [[-6, -30], [36, -30], [36, -8], [-6, -8]] },
  { kind: 'playground', polygon: [[-42, -32], [-24, -32], [-24, -18], [-42, -18]] },
  { kind: 'lawn', polygon: [[-46, -36], [48, -36], [48, 6], [-46, 6]] },
  { kind: 'parking_ramp', polygon: [[-12, 14], [2, 14], [2, 22], [-12, 22]] }] };
const id = (b, f, k) => `apt-${b}-${f}-${k}`;
const bid = (b) => `b0000000-0000-4000-8000-00000000000${b.toLowerCase().charCodeAt(0) - 87}`;
function units(b, f) {
  const st = (k) => ((f * 7 + k * 3) % 5 === 0 ? 'Sold' : (f + k) % 11 === 0 ? 'Booked' : 'Free');
  const u = (k, kind, rooms, area, price) => ({ id: id(b.code, f, k), number: Math.abs(f) * 100 + k + 1, rooms, area, pricePerM2: Math.round(price / area), price, currency: 'USD', status: st(k), planSlot: k, kind });
  if (f < 0) return Array.from({ length: 6 }, (_, k) => u(k, 'Parking', 0, 13.25, 20000));
  if (f <= 2) return Array.from({ length: 6 }, (_, k) => u(k, 'Commercial', 0, 120, 180000));
  if (f === b.floors) return Array.from({ length: 4 }, (_, k) => Object.assign(u(k, 'Penthouse', 4, 180, 252000), { terraceArea: 40 }));
  return b.areas.map((a, k) => u(k, 'Residential', b.rooms[k], a, Math.round(a * 1000 / 100) * 100));
}
const floorsOf = (b) => [-2, -1].concat(Array.from({ length: b.floors }, (_, i) => i + 1));
const building = (b) => ({ id: bid(b.code), name: 'Блок ' + b.code, nameRu: 'Блок ' + b.code, nameTg: 'Блоки ' + b.code, address: 'Душанбе', floorsCount: b.floors,
  totalApartments: (b.floors - 3) * 8 + 4, freeApartments: 50, blockCode: b.code, sortOrder: b.sort, footprint: b.footprint, sitePosition: b.pos,
  setbacks: [{ fromFloor: b.floors, footprint: b.top, planDepth: b.depth }], amenities: AMENITIES, elevatorsCount: 3, priceCurrency: 'USD', isPublished: true,
  description: 'Концепт: форма, расположение и планировки блока — визуализация до проекта застройщика, не проектная документация.' });

async function mockComplex(page) {
  await page.addInitScript(() => { try { localStorage.setItem('sitora.3dHint', '1'); } catch (e) { /* private mode */ } });
  await page.route('**/api/v1/**', (route) => {
    const p = new URL(route.request().url()).pathname.replace(/^\/api\/v1/, '');
    const json = (body) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    if (p === '/buildings') return json({ items: BLOCKS.map(building), totalCount: 3, page: 1, pageSize: 100 });
    const one = BLOCKS.find((b) => p === '/buildings/' + bid(b.code));
    if (one) return json(building(one));
    const fl = BLOCKS.find((b) => p === '/buildings/' + bid(b.code) + '/floors');
    if (fl) return json({ items: floorsOf(fl).map((n) => ({ id: 'f' + fl.code + (n < 0 ? 'm' + -n : n), buildingId: bid(fl.code), number: n, isParking: n < 0, isCommercial: n > 0 && n <= 2, totalApartments: units(fl, n).length, freeApartments: 3 })), totalCount: fl.floors + 2 });
    const fm = p.match(/^\/floors\/f([ABC])(m?)(\d+)\/apartments$/);
    if (fm) { const b = BLOCKS.find((x) => x.code === fm[1]); const items = units(b, (fm[2] ? -1 : 1) * +fm[3]); return json({ items, totalCount: items.length, page: 1, pageSize: 100 }); }
    if (p === '/public/brand') return json({ organizationId: '01a20000-0000-7000-8000-000000000001', name: 'Rich Tower', slug: 'nekruz', buildings: BLOCKS.map(building), sitePlan: SITE });
    if (p === '/rates/current') return json({ currency: 'USD', rateToTjs: 10.92, date: '2026-11-26', source: 'NBT' });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{"title":"Не найдено"}' });
  });
}

module.exports = { mockComplex, BLOCKS };
