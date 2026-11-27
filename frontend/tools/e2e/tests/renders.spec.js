// Stage 5: the pictures of the 3D model are of the whole complex and say «концепт»; the home page and «О проекте» name
// the complex with its number of blocks from the API («Rich Tower — 3 блока»); one building — no such line.
const { test, expect } = require('@playwright/test');
const { mockComplex } = require('./complex-mock');

for (const path of ['/', '/about']) {
  test(`${path}: «Rich Tower — 3 блока» from the API, the render says «концепт»`, async ({ page }) => {
    await mockComplex(page);
    await page.goto(path);
    await expect(page.locator('.nr-hero-blocks')).toHaveText('Rich Tower — 3 блока');
    await expect(page.locator('.nr-render-note').first()).toHaveText('Визуализация 3D-модели, концепт');
  });
}

test('without the API there is no blocks line (nothing invented)', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('.nr-render-note').first()).toHaveText('Визуализация 3D-модели, концепт');
  await expect(page.locator('.nr-hero-blocks')).toBeHidden();
});
