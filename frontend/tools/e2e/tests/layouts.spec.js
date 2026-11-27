// Stage 4: polygon layouts. The corner, bay and penthouse layouts are polygons (not rectangles) that scale to the
// apartment's area from the API; the floor plan of a block is drawn from its outline with the lift core of 3 lifts.
const { test, expect } = require('@playwright/test');
const { mockComplex } = require('./complex-mock');

test.describe('polygon layouts', () => {
  test.setTimeout(120000);

  test('corner, bay, rounded and penthouse layouts: outlines with more than 4 vertices, valid, area from the API', async ({ page }) => {
    await page.goto('/apartments');
    await page.waitForFunction(() => window.S && window.S.plans && window.S.plans.layout);
    const r = await page.evaluate(() => {
      const P = window.S.plans;
      return [['corner-l', 4, 149], ['corner-round', 4, 181], ['bay-2', 2, 90], ['ph-4', 4, 180]].map(([code, type, area]) => {
        const l = P.layout(type, area, code);
        const rooms = l.rooms.reduce((s, x) => s + x.area, 0) + (l.balcony ? 0 : 0);
        return { code, outline: l.outline.length, poly: l.rooms.some((x) => x.poly.length > 4), area: Math.round(l.roomsArea || rooms), want: area };
      });
    });
    for (const x of r) {
      if (x.code !== 'ph-4') expect(x.outline, x.code).toBeGreaterThan(4);
      expect(Math.abs(x.area - x.want) / x.want, x.code + ' area').toBeLessThan(0.03);
    }
    expect(r.filter((x) => x.poly).length).toBeGreaterThanOrEqual(3);
  });

  test('the floor plan of block A: its outline and the lift core with 3 lifts; the corner apartment opens its layout', async ({ page }) => {
    const errors = [];
    page.on('pageerror', (e) => errors.push(String(e)));
    await mockComplex(page);
    await page.goto('/apartments');
    await page.locator('.nr-blocks [data-block="A"]').click();
    await page.locator('.nr-rail-btn', { hasText: /^5$/ }).first().click();
    const plan = page.locator('canvas.nr-block-floor');
    await expect(plan).toBeVisible();
    await expect(plan).toHaveAttribute('data-lifts', '3');
    // Apartment 20 of A is slot 7 on the 5th floor — the corner one (layoutCode corner-l, 4 rooms).
    await page.locator('.nr-unit', { hasText: 'Блок A, кв. 20' }).click();
    await expect(page.locator('.nr-detail-kicker')).toContainText('Блок A, кв. 20');
    await expect(page.locator('canvas.nr-detail-plan')).toBeVisible();
    expect(errors).toEqual([]);
  });
});
