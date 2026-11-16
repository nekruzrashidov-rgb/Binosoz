// The 3D model on the whole screen (/apartments#3d) with a small catalog from a mocked API: the site is static here.
// Three.js comes from cdnjs, the scene runs on SwiftShader; window.__house3d is the engine's test hook.
const { test, expect } = require('@playwright/test');
const fs = require('fs');
const path = require('path');

const B = 'b0000000-0000-4000-8000-000000000001';
const FLOORS = 6, PER = 4;
const ROOMS = [0, 1, 2, 3];
const apartments = (floor) => Array.from({ length: PER }, (_, k) => {
  const rooms = ROOMS[k], area = [30, 42, 65, 88][k];
  return { id: `a${floor}${k}-0000-4000-8000-000000000000`, number: floor * 100 + k + 1, rooms, area, pricePerM2: 10000, price: area * 10000, currency: 'TJS', status: (floor + k) % 3 ? 'Free' : 'Booked', planSlot: k };
});
const building = (o) => Object.assign({ id: B, name: 'Тестовый дом', nameRu: 'Тестовый дом', nameTg: 'Хонаи санҷишӣ', address: 'Душанбе', floorsCount: FLOORS, totalApartments: FLOORS * PER, freeApartments: 12 }, o || {});
const SAMPLE = path.join(__dirname, '..', 'fixtures', 'context-sample.json');

async function mockApi(page, o) {
  o = o || {};
  await page.route('**/api/v1/**', (route) => {
    const u = new URL(route.request().url());
    const p = u.pathname.replace(/^\/api\/v1/, '');
    const json = (body) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    if (p === '/buildings') return json({ items: [building(o.building)], totalCount: 1, page: 1, pageSize: 100 });
    if (p === '/buildings/' + B) return json(building(o.building));
    if (p === '/buildings/' + B + '/floors') return json({ items: Array.from({ length: FLOORS }, (_, i) => ({ id: 'f' + (i + 1), buildingId: B, number: i + 1, totalApartments: PER, freeApartments: 3 })), totalCount: FLOORS });
    const fm = p.match(/^\/floors\/f(\d+)\/apartments$/);
    if (fm) return json({ items: apartments(+fm[1]), totalCount: PER });
    if (p === '/rates/current') return json({ currency: 'USD', rateToTjs: 10.92, date: '2026-11-16', source: 'NBT' });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{"title":"Не найдено"}' });
  });
  await page.route('**/data/context-*.json', (route) => (o.context ? route.fulfill({ status: 200, contentType: 'application/json', body: fs.readFileSync(SAMPLE, 'utf8') }) : route.fulfill({ status: 404, body: '' })));
}

// The scene draws its frames; SwiftShader is slow, so the camera is checked by the goal of the flight, not by time.
async function openFull(page, o) {
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e)));
  page.on('console', (m) => { if (m.type() === 'error' && !/Failed to load resource|favicon/.test(m.text())) errors.push(m.text()); });
  await page.addInitScript(() => { try { localStorage.setItem('sitora.3dHint', '1'); } catch (e) { /* private mode */ } });
  await mockApi(page, o);
  await page.goto('/apartments#3d');
  await page.waitForFunction(() => window.__house3d && document.querySelector('.h3d-full'), null, { timeout: 90000 });
  return errors;
}
const state = (page) => page.evaluate(() => window.__house3d.state());
// On a phone the list is a sheet from below: open it first.
async function showList(page, isMobile) { if (isMobile) { await page.locator('.h3d-list-toggle').click(); await expect(page.locator('.h3d-list')).toBeVisible(); } }

test.describe('3D on the whole screen', () => {
  test.setTimeout(120000);

  test('opens from the catalog button, the views move the camera, Escape and history close it', async ({ page }) => {
    const errors = await openFull(page);
    await expect(page.locator('.h3d-full')).toBeVisible();
    await expect(page.locator('.h3d-tag--height')).toContainText('6 этажей');
    await expect(page.locator('.h3d-tag--height')).toContainText('оценка');
    // No surroundings without coordinates: no «Окружение» view, no places, no OSM credit.
    await expect(page.locator('[data-view="area"]')).toHaveCount(0);
    await expect(page.locator('.h3d-attr')).toBeHidden();
    const before = await state(page);
    await page.locator('[data-view="top"]').click();
    await expect.poll(async () => (await state(page)).currentView).toBe('top');
    await expect.poll(async () => (await state(page)).view.pol, { timeout: 20000 }).toBeLessThan(0.4);
    expect((await state(page)).view.pol).not.toBeCloseTo(before.view.pol, 1);
    await page.locator('[data-view="street"]').click();
    await expect.poll(async () => (await state(page)).view.pol, { timeout: 20000 }).toBeGreaterThan(1.3);
    await page.keyboard.press('Escape');
    await expect(page.locator('.h3d-full')).toHaveCount(0);
    expect(page.url()).not.toContain('#3d');
    // Back in the catalog: the button opens it again, the browser's Back closes it.
    await page.locator('.nr-open3d').click();
    await expect(page.locator('.h3d-full')).toBeVisible();
    expect(page.url()).toContain('#3d');
    await page.goBack();
    await expect(page.locator('.h3d-full')).toHaveCount(0);
    expect(errors).toEqual([]);
  });

  test('the time slider changes the light by the real sun; the presets are points of it', async ({ page }) => {
    await openFull(page);
    const slider = page.locator('.h3d-time input[type=range]');
    await slider.fill('13');
    const day = await state(page);
    expect(day.sun.el).toBeGreaterThan(15);
    await expect(page.locator('.h3d-clock')).toHaveText('13:00');
    await expect(page.locator('.h3d-time [aria-pressed="true"]')).toHaveCount(0);
    await slider.fill('21.5');
    const night = await state(page);
    expect(night.sun.el).toBeLessThan(-10);
    expect(night.sunIntensity).toBeLessThan(day.sunIntensity);
    await page.locator('.h3d-seg button[data-k="dusk"]').click();
    const dusk = await state(page);
    expect(dusk.sun.el).toBeGreaterThan(1); expect(dusk.sun.el).toBeLessThan(6);
    await page.locator('.h3d-chip', { hasText: 'Сейчас' }).click();
    await expect(page.locator('.h3d-chip', { hasText: 'Сейчас' })).toHaveAttribute('aria-pressed', 'true');
  });

  test('a chosen apartment opens the card with TJS and ≈ $; inside and the walk switch on and off', async ({ page, isMobile }) => {
    // On CI the scene runs on SwiftShader (no GPU): building the apartment takes far longer than on a real
    // machine (on the site — under a second), so this test gets more time instead of failing on speed.
    test.slow();
    const errors = await openFull(page);
    await showList(page, isMobile);
    const item = page.locator('.h3d-item').first();
    await item.click();
    const card = page.locator('.h3d-card');
    await expect(card).toBeVisible();
    await expect(card.locator('.h3d-card-price')).toContainText('TJS');
    await expect(card.locator('.h3d-card-usd')).toContainText('≈ $');
    await expect(card.locator('.h3d-card-usd')).toContainText('оценочно по курсу');
    await expect.poll(async () => (await state(page)).selected).not.toBeNull();
    await card.locator('[data-act="inside"]').click();
    await expect.poll(async () => (await state(page)).mode, { timeout: 90000 }).toBe('inside');
    await expect(page.locator('.h3d-note')).toContainText('Планировка типовая');
    await expect(page.locator('.h3d-tag--room').first()).toBeAttached();
    await page.locator('.h3d-chip', { hasText: 'С мебелью' }).click();
    await expect(page.locator('.h3d-chip', { hasText: 'С мебелью' })).toHaveAttribute('aria-pressed', 'false');
    await page.locator('.h3d-btn', { hasText: 'Прогулка' }).click();
    await expect.poll(async () => (await state(page)).mode).toBe('walk');
    // Walking forward never goes through a wall: the camera stays inside the apartment.
    const w0 = (await state(page)).walk;
    await page.keyboard.down('KeyW'); await page.waitForTimeout(2500); await page.keyboard.up('KeyW');
    const w1 = (await state(page)).walk;
    expect(Math.hypot(w1.x - w0.x, w1.z - w0.z)).toBeLessThan(6);
    await page.keyboard.press('Escape');
    await expect.poll(async () => (await state(page)).mode).toBe('inside');
    await page.keyboard.press('Escape');
    await expect.poll(async () => (await state(page)).mode).toBe('house');
    await expect(page.locator('.h3d-full')).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('the catalog filters dim the windows and narrow the list', async ({ page, isMobile }) => {
    await openFull(page);
    await showList(page, isMobile);
    const all = await page.locator('.h3d-item').count();
    await page.locator('.h3d-pills .h3d-chip', { hasText: 'Студия' }).click();
    const studios = await page.locator('.h3d-item').count();
    expect(studios).toBeLessThan(all);
    await expect(page.locator('.h3d-item-meta').first()).toContainText('Студия');
  });

  test('with coordinates: OSM surroundings, places with «по прямой», the credit', async ({ page }) => {
    const sample = JSON.parse(fs.readFileSync(SAMPLE, 'utf8'));
    const errors = await openFull(page, { context: true, building: { latitude: sample.origin.lat, longitude: sample.origin.lon, floorHeight: 3.2 } });
    await expect.poll(async () => (await state(page)).context, { timeout: 20000 }).toBe(true);
    await expect(page.locator('.h3d-attr')).toContainText('OpenStreetMap');
    await expect(page.locator('[data-view="area"]')).toHaveCount(1);
    await expect(page.locator('.h3d-tag--poi').first()).toContainText('по прямой');
    await expect(page.locator('.h3d-tag--height')).not.toContainText('оценка');
    // On a phone the filter of places gives way to the price card; it is there all the same.
    await expect(page.locator('.h3d-poi .h3d-chip').first()).toBeAttached();
    expect(errors).toEqual([]);
  });

  test('no horizontal scroll at 375 px and the panels are readable (AA)', async ({ page, isMobile }) => {
    test.skip(!isMobile, 'phone only');
    await openFull(page);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow).toBeLessThanOrEqual(0);
    await page.locator('.h3d-list-toggle').click();
    await expect(page.locator('.h3d-list')).toBeVisible();
    // Text contrast of the panels over the scene: text colour against the panel colour blended over the darkest scene.
    const worst = await page.evaluate(() => {
      const parse = (c) => (c.match(/[\d.]+/g) || []).map(Number);
      const lum = ([r, g, b]) => { const f = (v) => { v /= 255; return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
      const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
      let min = 99, what = '';
      document.querySelectorAll('.h3d-full :is(.h3d-list, .h3d-top, .h3d-views, .h3d-card) :is(b, span, small, button, label, output, p)').forEach((el) => {
        if (!el.offsetParent || !el.textContent.trim()) return;
        let bg = null;
        for (let e = el; e; e = e.parentElement) { const c = getComputedStyle(e).backgroundColor; const v = parse(c); if (v.length >= 3 && (v.length === 3 || v[3] > 0.5)) { bg = v; break; } }
        if (!bg) return;
        const r = ratio(parse(getComputedStyle(el).color), bg.slice(0, 3));
        if (r < min) { min = r; what = el.className + ' ' + el.textContent.trim().slice(0, 20); }
      });
      return { min, what };
    });
    expect(worst.min, worst.what).toBeGreaterThanOrEqual(4.5);
  });
});
