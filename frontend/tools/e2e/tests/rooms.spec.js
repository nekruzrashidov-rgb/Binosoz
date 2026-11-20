// A-01: the apartment type is named by its real number of rooms — the same in the catalog, the layouts and the 3D card
// (one rule, S.roomsKind). A small catalog from a mocked API with 1-, 2-, 3- and 4-room apartments.
const { test, expect } = require('@playwright/test');

const B = 'b0000000-0000-4000-8000-000000000004';
const ROOMS = [1, 2, 3, 4], AREA = [42.5, 68, 92, 118.5];
const apartments = (floor) => ROOMS.map((rooms, k) => ({ id: `c${floor}${k}-0000-4000-8000-000000000000`, number: floor * 100 + k + 1, rooms, area: AREA[k], pricePerM2: 6500, price: Math.round(AREA[k] * 6500), currency: 'TJS', status: 'Free', planSlot: k }));
const building = { id: B, name: 'Тестовый дом', nameRu: 'Тестовый дом', nameTg: 'Хонаи санҷишӣ', address: 'Душанбе', floorsCount: 2, totalApartments: 8, freeApartments: 8 };

async function mockApi(page) {
  await page.addInitScript(() => { try { localStorage.setItem('sitora.3dHint', '1'); } catch (e) { /* private mode */ } });
  await page.route('**/api/v1/**', (route) => {
    const p = new URL(route.request().url()).pathname.replace(/^\/api\/v1/, '');
    const json = (body) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    if (p === '/buildings') return json({ items: [building], totalCount: 1, page: 1, pageSize: 100 });
    if (p === '/buildings/' + B) return json(building);
    if (p === '/buildings/' + B + '/floors') return json({ items: [1, 2].map((n) => ({ id: 'f' + n, buildingId: B, number: n, totalApartments: 4, freeApartments: 4 })), totalCount: 2 });
    const fm = p.match(/^\/floors\/f(\d+)\/apartments$/);
    if (fm) return json({ items: apartments(+fm[1]), totalCount: 4 });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{"title":"Не найдено"}' });
  });
}

test('a 4-room apartment is «4-комнатная» in the catalog and the layouts, the filter has «4+»', async ({ page }) => {
  await mockApi(page);
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
  await mockApi(page);
  await page.goto('/apartments#3d');
  await page.waitForFunction(() => window.__house3d && document.querySelector('.h3d-full'), null, { timeout: 90000 });
  if (isMobile) await page.locator('.h3d-list-toggle').click();
  await page.locator('.h3d-item', { hasText: '204' }).first().click();
  await expect(page.locator('.h3d-card-type')).toContainText('4-комнатная');
});
