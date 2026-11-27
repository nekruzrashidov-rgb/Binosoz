// Pictures of the site's 3D model (not photos of a real building): node render.mjs [http://localhost:5500]
// Needs the static server: node frontend/tools/serve-web.mjs 5500. Writes frontend/img/renders/<name>-800|1600|2400.webp.
// Playwright: from this folder (npm ci) or, if not installed, from ../e2e.
import { createRequire } from 'node:module';
import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
let pw;
try { pw = require('playwright'); } catch { pw = require('../e2e/node_modules/playwright'); }
const BASE = process.argv[2] || 'http://localhost:5500';
const OUT = fileURLToPath(new URL('../../img/renders/', import.meta.url));
const SIZES = [800, 1600, 2400];

// The whole complex «Rich Tower» from the blocks of the seed (the same data as the e2e mock: outlines, places, setbacks,
// the yard) — written next to render.html; every apartment free (a render shows the concept, not sales).
const { BLOCKS, SITE, units, floorsOf, building } = require('../e2e/tests/complex-mock.js');
const complex = {
  complex: { site: SITE, blocks: BLOCKS.map((b) => { const x = building(b); return { id: x.id, code: b.code, name: x.name, footprint: b.footprint, position: b.pos, setbacks: x.setbacks, floors: b.floors, elevators: 3, amenities: x.amenities }; }) },
  units: BLOCKS.flatMap((b) => floorsOf(b).flatMap((n) => units(b, n).map((a) => ({ id: a.id, block: b.code, floor: n, planSlot: a.planSlot, number: a.number, status: 'free', rooms: a.rooms, area: a.area, price: a.price, kind: a.kind.toLowerCase() })))),
};
await writeFile(fileURLToPath(new URL('./complex.json', import.meta.url)), JSON.stringify(complex));

// name, time of day, camera view of the complex, aspect (width / height)
const SHOTS = [
  ['tower-dusk-wide', { time: 'dusk', complex: 1, distK: 1.35, tyK: 0.8 }, 16 / 9],
  ['tower-dusk-tall', { time: 'dusk', complex: 1, view: 'yard' }, 4 / 5],
  ['tower-day-tall', { time: 'day', complex: 1, view: 'street' }, 4 / 5],
  ['tower-night-tall', { time: 'night', complex: 1, view: 'overview' }, 4 / 5],
];
const ONLY = process.env.ONLY ? process.env.ONLY.split(',') : null;

const browser = await pw.chromium.launch({ executablePath: process.env.CHROME || undefined, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
await mkdir(OUT, { recursive: true });
for (const [name, cam, aspect] of SHOTS.filter((x) => !ONLY || ONLY.includes(x[0]))) {
  const W = 2400, H = Math.round(W / aspect);
  const page = await browser.newPage({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });
  page.on('pageerror', (e) => console.error(name, e.message));
  const qs = new URLSearchParams(Object.entries(cam).map(([k, v]) => [k, String(v)])).toString();
  await page.goto(BASE + '/tools/renders/render.html?' + qs);
  await page.waitForFunction(() => window.__ready === true, null, { timeout: 900000 });
  const png = await page.screenshot({ type: 'png', timeout: 300000 }); // software WebGL at 2400 px is slow
  // PNG → WebP in three widths, in the browser itself (no image libraries).
  const webps = await page.evaluate(async ({ b64, sizes }) => {
    const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
    return sizes.map((w) => { const c = document.createElement('canvas'); c.width = w; c.height = Math.round(img.height * w / img.width);
      const x = c.getContext('2d'); x.imageSmoothingQuality = 'high'; x.drawImage(img, 0, 0, c.width, c.height); return c.toDataURL('image/webp', 0.82).split(',')[1]; });
  }, { b64: png.toString('base64'), sizes: SIZES });
  for (let i = 0; i < SIZES.length; i++) await writeFile(OUT + name + '-' + SIZES[i] + '.webp', Buffer.from(webps[i], 'base64'));
  console.log(name, W + '×' + H);
  await page.close();
}
await browser.close();
