// A-01: the apartment type is named by its real number of rooms — the same in the catalog, the layouts and the 3D card
// (one rule, S.roomsKind). A small catalog from a mocked API with 1-, 2-, 3- and 4-room apartments.
const { test, expect } = require('@playwright/test');
const { mockCatalog } = require('./catalog-mock');

test('a 4-room apartment is «4-комнатная» in the catalog and the layouts, the filter has «4+»', async ({ page }) => {
  await mockCatalog(page);
  await page.goto('/apartments');
  const unit = page.locator('.nr-unit', { hasText: 'Кв. 204' }).first();
  await expect(unit).toContainText('4-комнатная');
  await expect(unit).not.toContainText('3-комнатная');
  await expect(page.locator('.nr-unit', { hasText: 'Кв. 203' }).first()).toContainText('3-комнатная');
  // The layouts: a card of its own for the four rooms, with the area of the real apartments.
  const card = page.locator('.nr-layout', { hasText: '4-комнатная' });
  await expect(card).toHaveCount(1);
  await expect(card).toContainText('119');
  await expect(page.locator('.nr-layout', { hasText: '3-комнатная' })).toContainText('92');
  // «4+» in the room filters (of the chosen floor and of the whole list), no «3+».
  await expect(page.locator('.nr-pill', { hasText: '4+' }).first()).toBeVisible();
  await expect(page.locator('.nr-pill', { hasText: '3+' })).toHaveCount(0);
});

test('the 3D card names the same type', async ({ page, isMobile }) => {
  test.setTimeout(120000);
  await mockCatalog(page);
  await page.goto('/apartments#3d');
  await page.waitForFunction(() => window.__house3d && document.querySelector('.h3d-full'), null, { timeout: 90000 });
  if (isMobile) await page.locator('.h3d-list-tab').click();
  await page.locator('.h3d-item', { hasText: '204' }).first().click();
  await expect(page.locator('.h3d-card-type')).toContainText('4-комнатная');
});
