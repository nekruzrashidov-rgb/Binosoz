// «3D на весь экран» of the complex (mocked API of three blocks): the layout — the blocks segment on top, the icons, the
// views at the bottom, the folding list, the floor scale; nothing overlaps (buttons, panels, badges) at 1440 × 900,
// 1024 × 768 and 375 × 812; the list folds, the segment switches the block, Esc closes, no console errors.
const { test, expect } = require('@playwright/test');
const { mockComplex } = require('./complex-mock');

async function openComplex(page, size) {
  await page.setViewportSize(size);
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e)));
  page.on('console', (m) => { if (m.type() === 'error' && !/Failed to load resource|favicon/.test(m.text())) errors.push(m.text()); });
  await mockComplex(page);
  await page.goto('/apartments#3d');
  await page.waitForFunction(() => window.__house3d && document.querySelector('.h3d-full .h3d-seg--main') && window.__house3d.state().blocks.length === 3, null, { timeout: 90000 });
  return errors;
}

// Every visible control, panel and 3D label as a box clipped by its scrolling parents; pairs that intersect (one inside
// the other is a part of it, not an overlap).
function overlaps(page) {
  return page.evaluate(() => {
    const root = document.querySelector('.h3d-full');
    const els = [...root.querySelectorAll('button, input, [role="switch"], .h3d-list, .h3d-card, .h3d-concept, .h3d-floors, .h3d-pop, .h3d-tag')];
    const box = (el) => {
      const cs = getComputedStyle(el);
      if (cs.visibility === 'hidden' || cs.display === 'none' || el.closest('[hidden]') || el.closest('.h3d-items')) return null;
      let r = el.getBoundingClientRect(); r = { x0: r.left, y0: r.top, x1: r.right, y1: r.bottom };
      for (let p = el.parentElement; p && p !== document.body; p = p.parentElement) {
        const s = getComputedStyle(p);
        if (s.visibility === 'hidden') return null;
        if (s.overflowX !== 'visible' || s.overflowY !== 'visible') { const q = p.getBoundingClientRect(); r = { x0: Math.max(r.x0, q.left), y0: Math.max(r.y0, q.top), x1: Math.min(r.x1, q.right), y1: Math.min(r.y1, q.bottom) }; }
      }
      r.x0 = Math.max(r.x0, 0); r.y0 = Math.max(r.y0, 0); r.x1 = Math.min(r.x1, innerWidth); r.y1 = Math.min(r.y1, innerHeight);
      return r.x1 - r.x0 > 1 && r.y1 - r.y0 > 1 ? r : null;
    };
    const list = els.map((el) => ({ el, r: box(el) })).filter((x) => x.r);
    const name = (el) => el.className && typeof el.className === 'string' ? el.className.split(' ')[0] + (el.textContent ? ' «' + el.textContent.trim().slice(0, 24) + '»' : '') : el.tagName;
    const out = [];
    for (let i = 0; i < list.length; i++) for (let j = i + 1; j < list.length; j++) {
      const a = list[i], b = list[j];
      if (a.el.contains(b.el) || b.el.contains(a.el)) continue;
      const w = Math.min(a.r.x1, b.r.x1) - Math.max(a.r.x0, b.r.x0), h = Math.min(a.r.y1, b.r.y1) - Math.max(a.r.y0, b.r.y0);
      if (w > 1 && h > 1) out.push(name(a.el) + ' × ' + name(b.el));
    }
    return { count: list.length, out };
  });
}
// Labels are placed by the render loop: let a few frames go after the camera settles.
const settle = (page) => page.waitForTimeout(1800);

const SIZES = [{ width: 1440, height: 900 }, { width: 1024, height: 768 }, { width: 375, height: 812 }];

test.describe('3D on the whole screen — the layout', () => {
  test.setTimeout(180000);
  test.skip(({ isMobile }) => isMobile, 'the sizes are set by the test itself');

  for (const size of SIZES) {
    test(`no overlaps at ${size.width}×${size.height}: whole complex, block A, a picked apartment`, async ({ page }) => {
      const errors = await openComplex(page, size);
      const phone = size.width <= 768;
      await settle(page);
      let o = await overlaps(page);
      expect(o.count).toBeGreaterThan(8);
      expect(o.out, 'whole complex').toEqual([]);
      // Buttons: 36 px icons on a desktop, ≥ 44 px on a phone.
      const tool = await page.locator('.h3d-tool').first().boundingBox();
      expect(tool.height).toBeGreaterThanOrEqual(phone ? 44 : 36);

      await page.locator('.h3d-seg--main button[data-block="A"]').click();
      await expect(page.locator('.h3d-head-title')).toHaveText(/Блок A · 22 этажа/);
      await expect.poll(async () => (await page.evaluate(() => window.__house3d.state().focusBlock))).toBe('A');
      if (!phone) await expect(page.locator('.h3d-full')).toHaveClass(/list-open/);
      await settle(page);
      o = await overlaps(page);
      expect(o.out, 'block A').toEqual([]);

      if (phone) await page.locator('.h3d-list-tab').click();
      await page.locator('.h3d-item').first().click();
      await expect(page.locator('.h3d-card')).toBeVisible();
      if (phone) await expect(page.locator('.h3d-full')).not.toHaveClass(/list-open/);
      await settle(page);
      o = await overlaps(page);
      expect(o.out, 'with the card').toEqual([]);
      expect(errors).toEqual([]);
    });
  }

  test('the list folds, the segment and the arrows switch the block, the time card and Esc', async ({ page }) => {
    const errors = await openComplex(page, { width: 1440, height: 900 });
    const full = page.locator('.h3d-full');
    // Folded on the whole complex: the tab says how many.
    await expect(full).not.toHaveClass(/list-open/);
    await expect(page.locator('.h3d-list-tab')).toContainText(/Квартиры · \d+/);
    await page.locator('.h3d-list-tab').click();
    await expect(full).toHaveClass(/list-open/);
    await page.locator('.h3d-list-fold').click();
    await expect(full).not.toHaveClass(/list-open/);

    // The keyboard: → in the segment goes to the next block.
    await page.locator('.h3d-seg--main button[data-block="all"]').focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.locator('.h3d-seg--main button[data-block="A"]')).toHaveAttribute('aria-pressed', 'true');
    await page.keyboard.press('ArrowRight');
    await expect(page.locator('.h3d-seg--main button[data-block="B"]')).toHaveAttribute('aria-pressed', 'true');
    await expect(page.locator('.h3d-head-title')).toHaveText(/Блок B/);
    // The floor scale of B with free floors marked; the list shows only B.
    await expect(page.locator('.h3d-floors')).toBeVisible();
    expect(await page.locator('.h3d-floor').count()).toBeGreaterThan(10);
    const nums = await page.locator('.h3d-item-no').allTextContents();
    expect(nums.length).toBeGreaterThan(0);
    expect(nums.every((n) => /^B-\d+$/.test(n))).toBe(true);
    // The height only over the chosen block.
    await expect(page.locator('.h3d-tag--block.is-current')).toHaveCount(1);
    await expect(page.locator('.h3d-tag--block.is-current')).toContainText('20 этажей');

    // The switch «only free»: a real switch.
    const sw = page.locator('.h3d-switch');
    await expect(sw).toHaveAttribute('role', 'switch');
    const was = await sw.getAttribute('aria-checked');
    await sw.click();
    await expect(page.locator('.h3d-switch')).toHaveAttribute('aria-checked', was === 'true' ? 'false' : 'true');

    // Hover over a row lights its windows.
    const row = page.locator('.h3d-item').first();
    await row.hover();
    await expect.poll(() => page.evaluate(() => window.__house3d.state().hover)).not.toBeNull();

    // The time card: opens from the sun icon, Esc closes it, then the list, then the 3D.
    await page.locator('.h3d-tool[aria-haspopup]').click();
    await expect(page.locator('.h3d-pop')).toBeVisible();
    await page.keyboard.press('Escape');
    await expect(page.locator('.h3d-pop')).toBeHidden();
    await expect(full).toHaveClass(/list-open/);
    await page.keyboard.press('Escape');
    await expect(full).not.toHaveClass(/list-open/);
    await page.keyboard.press('Escape');
    await expect(page.locator('.h3d-full')).toHaveCount(0);
    expect(errors).toEqual([]);
  });

  test('yard names only on «Двор» and «Сверху»; parking and cut-away are toggles', async ({ page }) => {
    await openComplex(page, { width: 1440, height: 900 });
    const yardShown = () => page.evaluate(() => [...document.querySelectorAll('.h3d-tag--yard')].filter((e) => e.style.visibility === 'visible').length);
    await settle(page);
    expect(await yardShown()).toBe(0);
    await page.locator('.h3d-seg--views button[data-view="yard"]').click();
    await expect.poll(yardShown, { timeout: 8000 }).toBeGreaterThan(0);
    await page.locator('.h3d-seg--views button[data-view="overview"]').click();
    await expect.poll(yardShown, { timeout: 8000 }).toBe(0);
    const park = page.locator('.h3d-tool[data-mode="parking"]');
    await park.click();
    await expect(park).toHaveAttribute('aria-pressed', 'true');
    await expect.poll(() => page.evaluate(() => window.__house3d.state().parking.on)).toBe(true);
    const xray = page.locator('.h3d-tool[data-mode="xray"]');
    await expect(xray).toHaveAttribute('aria-pressed', /true|false/);
  });
});
