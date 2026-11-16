// The real surroundings of a building for its 3D model: buildings with their outlines and heights, roads, parks and the
// places nearby (schools, kindergartens, parks, medicine, mosques, shops, stops) from OpenStreetMap, saved as a static file
// frontend/data/context-<buildingId>.json. The site never calls Overpass itself: it reads this file, and only for a building
// that has coordinates (PUT /buildings/{id}/location). Data © OpenStreetMap contributors, ODbL 1.0.
//
//   node frontend/tools/context/build.mjs --id <buildingId> --lat 38.5598 --lon 68.7870 [--radius 600] [--out file.json]
//   node frontend/tools/context/build.mjs --id <buildingId> --api https://binosoz.onrender.com   (coordinates from the API)
//
// Heights: the height tag in metres, else building:levels × 3 m, else 12 m. Distances to the places are not stored —
// the site counts them in a straight line from the building's coordinates.
import { writeFile, mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = Object.fromEntries(process.argv.slice(2).reduce((a, v, i, all) => (v.startsWith('--') ? a.concat([[v.slice(2), all[i + 1] && !all[i + 1].startsWith('--') ? all[i + 1] : 'true']]) : a), []));
const OVERPASS = args.overpass || 'https://overpass-api.de/api/interpreter';
const radius = Math.min(1500, Math.max(150, Number(args.radius || 600)));
const poiRadius = Math.max(radius, 1200);

async function coordinates() {
  if (args.lat && args.lon) return { lat: Number(args.lat), lon: Number(args.lon) };
  if (!args.api || !args.id) throw new Error('Укажите --lat и --lon или --api и --id');
  const r = await fetch(args.api.replace(/\/$/, '') + '/api/v1/buildings/' + encodeURIComponent(args.id));
  if (!r.ok) throw new Error('Дом не найден в API: ' + r.status);
  const b = await r.json();
  if (b.latitude == null || b.longitude == null) throw new Error('У дома в API нет координат — окружение не строится');
  return { lat: Number(b.latitude), lon: Number(b.longitude) };
}

async function overpass(query, retry = 2) {
  const r = await fetch(OVERPASS, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'User-Agent': 'binosoz-context-builder/1.0' },
    body: 'data=' + encodeURIComponent(query),
  });
  // Busy or slow server (429, 504): wait and try again.
  if ((r.status === 429 || r.status >= 500) && retry > 0) { await new Promise((ok) => setTimeout(ok, 8000)); return overpass(query, retry - 1); }
  if (!r.ok) throw new Error('Overpass ответил ' + r.status + ': ' + (await r.text()).slice(0, 200));
  return (await r.json()).elements || [];
}

const ROAD_W = { motorway: 14, trunk: 14, primary: 12, secondary: 10, tertiary: 8, unclassified: 6, residential: 6, living_street: 4, service: 4, pedestrian: 4 };
const r6 = (v) => Math.round(v * 1e6) / 1e6;
const pts = (geom) => (geom || []).map((g) => [r6(g.lat), r6(g.lon)]);
function inside([lat, lon], poly) {
  let c = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [ai, oi] = poly[i], [aj, oj] = poly[j];
    if ((oi > lon) !== (oj > lon) && lat < ((aj - ai) * (lon - oi)) / (oj - oi) + ai) c = !c;
  }
  return c;
}
function height(tags) {
  const h = parseFloat(String(tags.height || '').replace(',', '.'));
  if (h > 0 && h < 400) return Math.round(h * 10) / 10;
  const lv = parseFloat(tags['building:levels']);
  if (lv > 0 && lv < 120) return lv * 3;
  return 12;
}
function poiCat(t) {
  if (t.amenity === 'school') return 'school';
  if (t.amenity === 'kindergarten') return 'kindergarten';
  if (['hospital', 'clinic', 'doctors', 'pharmacy'].includes(t.amenity)) return 'medical';
  if (t.amenity === 'place_of_worship' && t.religion === 'muslim') return 'mosque';
  if (t.shop) return 'shop';
  if (t.highway === 'bus_stop' || t.public_transport === 'platform' || t.public_transport === 'stop_position') return 'transport';
  if (t.leisure === 'park') return 'park';
  return null;
}

const o = await coordinates();
const around = (r) => `(around:${r},${o.lat},${o.lon})`;
console.log(`Окружение для ${o.lat}, ${o.lon}, радиус ${radius} м…`);
const geo = await overpass(`[out:json][timeout:90];(
  way["building"]${around(radius)};
  way["highway"~"^(motorway|trunk|primary|secondary|tertiary|unclassified|residential|living_street|service|pedestrian)$"]${around(radius)};
  way["leisure"~"^(park|garden)$"]${around(radius)};
  way["landuse"~"^(grass|recreation_ground)$"]${around(radius)};
);out geom;`);
const places = await overpass(`[out:json][timeout:90];(
  nwr["amenity"~"^(school|kindergarten|hospital|clinic|doctors|pharmacy|place_of_worship)$"]${around(poiRadius)};
  nwr["shop"~"^(supermarket|convenience|mall|department_store)$"]${around(poiRadius)};
  node["highway"="bus_stop"]${around(poiRadius)};
  nwr["leisure"="park"]["name"]${around(poiRadius)};
);out center tags;`);

const buildings = [], roads = [], parks = [];
geo.forEach((e) => {
  const t = e.tags || {}, p = pts(e.geometry);
  if (p.length < 2) return;
  if (t.building) {
    if (p.length < 4 || inside([o.lat, o.lon], p)) return; // the building itself is the 3D model
    buildings.push({ h: height(t), pts: p.slice(0, -1) });
  } else if (t.highway) roads.push({ w: ROAD_W[t.highway] || 6, kind: t.highway, pts: p });
  else if (p.length >= 4) parks.push({ pts: p.slice(0, -1) });
});
const seen = new Set();
const pois = [];
places.forEach((e) => {
  const t = e.tags || {}, cat = poiCat(t);
  const name = t['name:ru'] || t.name || t['name:tg'];
  const lat = e.lat ?? e.center?.lat, lon = e.lon ?? e.center?.lon;
  if (!cat || !name || lat == null) return;
  const key = cat + '|' + name + '|' + Math.round(lat * 500) + '|' + Math.round(lon * 500);
  if (seen.has(key)) return; seen.add(key);
  pois.push({ cat, name: String(name).slice(0, 60), lat: r6(lat), lon: r6(lon) });
});

const out = resolve(args.out || resolve(dirname(fileURLToPath(import.meta.url)), '../../data/context-' + (args.id || 'sample') + '.json'));
const data = { origin: { lat: o.lat, lon: o.lon }, radius, generated: new Date().toISOString().slice(0, 10), source: 'OpenStreetMap (Overpass API)', license: 'ODbL 1.0', attribution: '© участники OpenStreetMap', buildings, roads, parks, pois };
await mkdir(dirname(out), { recursive: true });
await writeFile(out, JSON.stringify(data));
console.log(`Готово: ${out} — зданий ${buildings.length}, дорог ${roads.length}, зелени ${parks.length}, мест ${pois.length}`);
