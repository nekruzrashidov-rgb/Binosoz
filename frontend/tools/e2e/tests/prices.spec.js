// Prices by floor in USD (a mocked API): the catalogue card shows the price per m² of its floor in dollars and, next to the
// apartment's price, the estimate in somoni by the NBT rate, signed «оценочно по курсу».
const { test, expect } = require('@playwright/test');

const B = 'b0000000-0000-4000-8000-0000000000a1';
const RATE = 10.92;
// Floor 6 — 1 200 $/m², floor 19 — 900 $/m² (the default rule); areas chosen so that price / area is exact.
const apt = (floor, k, area, ppm) => ({ id: `d${floor}${k}-0000-4000-8000-000000000000`, number: floor * 100 + k + 1, rooms: 2, area, pricePerM2: ppm,
  price: area * ppm, currency: 'USD', status: 'Free', planSlot: k });
const FLOORS = { 6: [apt(6, 0, 50, 1200), apt(6, 1, 75, 1200)], 19: [apt(19, 0, 50, 900), apt(19, 1, 80, 900)] };
const building = { id: B, name: 'Блок A', nameRu: 'Блок A', nameTg: 'Блоки A', address: 'Душанбе', floorsCount: 19, totalApartments: 4, freeApartments: 4, priceCurrency: 'USD' };

async function mock(page) {
  await page.addInitScript(() => { try { localStorage.setItem('sitora.3dHint', '1'); } catch (e) { /* private mode */ } });
  await page.route('**/api/v1/**', (route) => {
    const p = new URL(route.request().url()).pathname.replace(/^\/api\/v1/, '');
    const json = (body) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    if (p === '/buildings') return json({ items: [building], totalCount: 1, page: 1, pageSize: 100 });
    if (p === '/buildings/' + B) return json(building);
    if (p === '/buildings/' + B + '/floors') return json({ items: [6, 19].map((n) => ({ id: 'f' + n, buildingId: B, number: n, totalApartments: 2, freeApartments: 2 })), totalCount: 2 });
    const fm = p.match(/^\/floors\/f(\d+)\/apartments$/);
    if (fm) return json({ items: FLOORS[fm[1]], totalCount: 2 });
    if (p === '/rates/current') return json({ currency: 'USD', rateToTjs: RATE, date: '2026-11-26', source: 'NBT' });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{"title":"Не найдено"}' });
  });
}

test('price per m² by floor in USD: $1 200 on floor 6, $900 on floor 19, ≈ TJS by the NBT rate', async ({ page }) => {
  await mock(page);
  await page.goto('/apartments');
  const card = (n) => page.locator('.nr-unit-card', { hasText: 'Кв. ' + n });
  await expect(page.locator('.nr-unit-card').first()).toBeVisible();
  await expect(card(601)).toContainText('$1 200 / м²');
  await expect(card(1901)).toContainText('$900 / м²');
  await expect(card(601)).toContainText('$60 000');
  // The chosen apartment's panel: the dollar price and the somoni estimate.
  await card(601).click();
  const detail = page.locator('.nr-detail');
  await expect(detail.locator('.nr-detail-price')).toHaveText('$60 000');
  await expect(detail.locator('.nr-detail-note')).toContainText('655 200 TJS по курсу НБТ');
  await expect(detail.locator('.nr-detail-note')).toContainText('оценочно по курсу');
});
