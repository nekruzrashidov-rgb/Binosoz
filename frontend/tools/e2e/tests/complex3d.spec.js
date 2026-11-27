// The 3D of the whole complex (/apartments#3d) with a mocked API of three blocks: the scene is built from the data —
// three blocks, the gaps between them, three lift shafts each, the playground and the pitch in the yard, the parking
// levels −1/−2; the block switch; a click on a window picks that window's apartment of that block.
const { test, expect } = require('@playwright/test');
const { mockComplex } = require('./complex-mock');

const state = (page) => page.evaluate(() => window.__house3d.state());

async function openComplex(page) {
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e)));
  page.on('console', (m) => { if (m.type() === 'error' && !/Failed to load resource|favicon/.test(m.text())) errors.push(m.text()); });
  await mockComplex(page);
  await page.goto('/apartments#3d');
  await page.waitForFunction(() => window.__house3d && document.querySelector('.h3d-full') && window.__house3d.state().blocks.length === 3, null, { timeout: 90000 });
  return errors;
}

test.describe('3D of the whole complex', () => {
  test.setTimeout(150000);

  test('three blocks from the data, gaps ≥ 20 m, 3 lifts each, the yard, the parking levels, the concept caption', async ({ page, isMobile }) => {
    const errors = await openComplex(page);
    const s = await state(page);
    expect(s.blocks.map((b) => b.code)).toEqual(['A', 'B', 'C']);
    expect(s.blocks.map((b) => b.floors)).toEqual([22, 20, 20]);
    expect(s.blocks.every((b) => b.windows > 100 && b.setback)).toBe(true);
    expect(s.gaps.length).toBe(2);
    for (const g of s.gaps) expect(g.d).toBeGreaterThanOrEqual(20);
    expect(s.elevators).toEqual([{ block: 'A', shafts: 3 }, { block: 'B', shafts: 3 }, { block: 'C', shafts: 3 }]);
    expect(s.amenities).toEqual(expect.arrayContaining(['playground', 'football_field']));
    expect(s.drawCalls).toBeLessThanOrEqual(300);
    await expect(page.locator('.h3d-concept')).toHaveText('Визуализация, концепт — не проектная документация');
    await expect(page.locator('.h3d-tag--block', { hasText: 'Блок B' })).toHaveCount(1);
    // The parking mode: the two levels with their spots.
    await page.locator('[data-mode="parking"]').click();
    await expect.poll(async () => (await state(page)).parking.on).toBe(true);
    const p = (await state(page)).parking;
    expect(p.levels).toEqual([-2, -1]);
    expect(p.spots).toBeGreaterThan(0);
    await page.locator('[data-mode="parking"]').click();
    await expect.poll(async () => (await state(page)).parking.on).toBe(false);
    // «Сверху»: the distance labels from the scene's data (on a phone the views are in the bottom sheet).
    if (isMobile) await page.locator('.h3d-list-tab').click();
    await page.locator('[data-view="top"]').click();
    await expect(page.locator('.h3d-tag--dim').first()).toContainText('по концепту');
    expect(errors).toEqual([]);
  });

  test('the block switch flies to the block; a click on 10 windows picks that window\'s apartment of its block', async ({ page }) => {
    const errors = await openComplex(page);
    const before = (await state(page)).blocks;
    await page.locator('.h3d-blocks [data-block="C"]').click();
    const c = before.find((b) => b.code === 'C').center;
    await expect.poll(async () => { const v = (await state(page)).view; return Math.hypot(v.tx - c[0], v.tz - c[1]); }, { timeout: 20000 }).toBeLessThan(8);
    await page.locator('.h3d-blocks [data-block="all"]').click();

    const picks = await page.evaluate(() => window.__house3d.windowPoints(10));
    expect(picks.length).toBe(10);
    const box = await page.locator('.house3d canvas').boundingBox();
    for (const w of picks) {
      await page.evaluate((id) => window.__house3d.lookAtApartment(id), w.id);
      await page.waitForTimeout(150);
      const pt = await page.evaluate((id) => window.__house3d.windowPoint(id), w.id);
      expect(pt && pt.visible, w.id).toBe(true);
      await page.mouse.click(box.x + pt.x, box.y + pt.y);
      await expect.poll(async () => (await state(page)).selected, { timeout: 5000 }).toBe(w.id);
      expect((await state(page)).selectedBlock).toBe(w.block);
    }
    expect(errors).toEqual([]);
  });


  // A chosen block is the person's camera: a later resize of the scene (the window, the list, a rotated phone) only fits the
  // frame and never sends the camera back to the whole complex (it used to: ResizeObserver reset it while lastInput was 0).
  test('after a block is chosen, a resize of the scene keeps the camera at that block', async ({ page }) => {
    const errors = await openComplex(page);
    const c = (await state(page)).blocks.find((b) => b.code === 'C').center;
    const toC = async () => { const v = (await state(page)).view; return Math.hypot(v.tx - c[0], v.tz - c[1]); };
    // No apartment chosen (as when the chosen one is not bound yet on a slow device): nothing else holds the camera.
    if (await page.locator('.h3d-card').isVisible()) await page.locator('.h3d-card-x').click();
    await expect.poll(async () => (await state(page)).selected).toBeNull();
    await page.locator('.h3d-blocks [data-block="C"]').click();
    await expect.poll(toC, { timeout: 20000 }).toBeLessThan(8);
    const size = page.viewportSize();
    for (const s of [{ width: size.width - 120, height: size.height - 60 }, size]) {
      await page.setViewportSize(s);
      // A few frames after the resize (the observer runs on the next frame).
      await page.waitForTimeout(2500);
      expect(await toC()).toBeLessThan(8);
    }
    expect((await state(page)).focusBlock).toBe('C');
    expect(errors).toEqual([]);
  });

  test('catalog: «Весь ЖК / A / B / C» over the model, the rail of the chosen block, the list by block, the block in the card', async ({ page }) => {
    await mockComplex(page);
    await page.goto('/apartments');
    await expect(page.locator('.nr-blocks [data-block="B"]')).toBeVisible();
    await expect(page.locator('.nr-concept-note')).toHaveText('Визуализация, концепт — не проектная документация');
    await page.locator('.nr-blocks [data-block="B"]').click();
    await expect(page.locator('.nr-floorpanel .nr-kicker')).toContainText('Блок B');
    // The rail of block B: 20 floors and the parking levels.
    await expect(page.locator('.nr-rail-btn')).toHaveCount(22);
    await page.locator('.nr-block-pills [data-block="C"]').click();
    const cards = page.locator('#apt-grid .nr-unit-card');
    await expect(cards.first()).toBeVisible();
    for (const c of (await cards.all()).slice(0, 5)) await expect(c).toContainText('Блок C');
  });
});
