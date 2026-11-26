// Numbering of the blocks (docs/DECISIONS.md): homes 1…N in each block from the 3rd floor up, on a floor clockwise from the
// unit at the lift hall; the number goes with the block — «Блок A, кв. 45», short «A-45»; shops Н-…, parking spaces М-….
// The mock (complex-mock.js) numbers like the seed; the backend test BlockNumberingTests checks that their start slots match.
const { test, expect } = require('@playwright/test');
const { mockComplex, BLOCKS } = require('./complex-mock');

test.describe('numbering of the blocks', () => {
  test.setTimeout(120000);

  test('the first number of a floor is the unit nearest to the lift hall, the ring goes clockwise', async ({ page }) => {
    await mockComplex(page);
    await page.goto('/apartments');
    await page.waitForFunction(() => window.S && window.S.geom && window.S.geom.floor);
    const res = await page.evaluate((blocks) => blocks.map((b) => {
      const near = (fp, areas, depth) => {
        const f = window.S.geom.floor(fp, areas, { depth, lifts: 3 }); const c = f.core.center;
        const d = f.units.map((u) => Math.min(...u.poly.map((p) => Math.hypot(p.x - c.x, p.y - c.y))));
        // Winding of the unit centres around the core in plan coordinates (y down): +1 turn — clockwise on the plan.
        const cen = f.units.map((u) => u.poly.reduce((s, p) => ({ x: s.x + p.x / u.poly.length, y: s.y + p.y / u.poly.length }), { x: 0, y: 0 }));
        let turn = 0;
        for (let i = 0; i < cen.length; i++) {
          const a0 = Math.atan2(cen[i].y - c.y, cen[i].x - c.x); const a1 = Math.atan2(cen[(i + 1) % cen.length].y - c.y, cen[(i + 1) % cen.length].x - c.x);
          let da = a1 - a0; while (da > Math.PI) da -= 2 * Math.PI; while (da < -Math.PI) da += 2 * Math.PI; turn += da;
        }
        return { start: d.indexOf(Math.min(...d)), clockwise: Math.round(turn / (2 * Math.PI)) === 1 };
      };
      return { code: b.code, typ: near(b.footprint, b.areas, 7), top: near(b.top, [1, 1, 1, 1], b.depth) };
    }), BLOCKS);
    for (const r of res) {
      const b = BLOCKS.find((x) => x.code === r.code);
      expect(r.typ, r.code).toEqual({ start: b.start, clockwise: true });
      expect(r.top, r.code + ' top').toEqual({ start: b.topStart, clockwise: true });
    }
  });

  test('names carry the block: «Блок A, кв. 45», «A-45», shops Н-, parking М-; the old building keeps its numbers', async ({ page }) => {
    await page.goto('/apartments');
    await page.waitForFunction(() => window.S && window.S.unitName && window.S.t('unit.inBlock') !== 'unit.inBlock');
    const names = await page.evaluate(() => {
      const S = window.S;
      return [
        S.unitName({ number: 45, block: 'A', kind: 'residential' }), S.aptNum({ number: 45, block: 'A' }),
        S.unitName({ apartmentNumber: 3, blockCode: 'A', apartmentKind: 'Commercial' }), S.aptNum({ apartmentNumber: 12, blockCode: 'B', apartmentKind: 'Parking' }),
        S.unitName({ number: 153, block: 'A', kind: 'penthouse' }), S.unitName({ number: 1201, kind: 'residential' }), S.aptShort({ apartmentNumber: 1201 }),
      ];
    });
    expect(names).toEqual(['Блок A, кв. 45', 'A-45', 'Блок A, пом. Н-3', 'B-М-12', 'Блок A, пентхаус 153', 'Кв. 1201', '№ 1201']);
  });

  test('the list of the whole complex: numbers are unique (A-45 ≠ B-45), apartment 1 of A is on the 3rd floor', async ({ page }) => {
    await mockComplex(page);
    await page.goto('/apartments#3d');
    await page.waitForFunction(() => document.querySelector('.h3d-full .h3d-item'), null, { timeout: 90000 });
    const labels = await page.locator('.h3d-full .h3d-item .h3d-item-top b').allTextContents();
    expect(labels.length).toBeGreaterThan(20);
    expect(new Set(labels).size).toBe(labels.length);
    expect(labels.every((l) => /^[ABC]-\d+$/.test(l))).toBe(true);
    const first = page.locator('.h3d-full .h3d-item', { has: page.locator('.h3d-item-top b', { hasText: /^A-1$/ }) });
    if (await first.count()) await expect(first.first().locator('.h3d-item-meta')).toContainText('3 этаж');
  });
});
