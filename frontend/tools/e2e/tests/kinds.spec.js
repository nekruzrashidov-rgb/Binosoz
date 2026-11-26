// Shops on floors 1–2 and parking on −1/−2 (a mocked API): the list shows apartments by default, «Коммерция» and «Паркинг»
// show only their own premises; the floor rail has the parking level; the floor panel says how many lifts the block has.
const { test, expect } = require('@playwright/test');

const B = 'b0000000-0000-4000-8000-0000000000b1';
const unit = (floor, k, kind, area, price) => ({ id: `e${floor < 0 ? 'm' + -floor : floor}${k}-0000-4000-8000-000000000000`, number: Math.abs(floor) * 100 + k + 1,
  rooms: kind === 'Residential' ? 2 : 0, area, pricePerM2: Math.round(price / area), price, currency: 'USD', status: 'Free', planSlot: k, kind });
const FLOORS = {
  '-1': [unit(-1, 0, 'Parking', 13.25, 20000), unit(-1, 1, 'Parking', 13.25, 20000)],
  1: [unit(1, 0, 'Commercial', 120, 180000), unit(1, 1, 'Commercial', 80, 120000)],
  3: [unit(3, 0, 'Residential', 60, 72000), unit(3, 1, 'Residential', 70, 84000)],
  4: [Object.assign(unit(4, 0, 'Penthouse', 180, 252000), { rooms: 4, terraceArea: 40 }), Object.assign(unit(4, 1, 'Penthouse', 200, 280000), { rooms: 4, terraceArea: 44 })],
};
const building = { id: B, name: 'Блок A', nameRu: 'Блок A', nameTg: 'Блоки A', address: 'Душанбе', floorsCount: 4, totalApartments: 4, freeApartments: 4,
  elevatorsCount: 3, commercialUnits: 2, parkingSpots: 2 };
const floors = [{ id: 'fm1', number: -1, isParking: true }, { id: 'f1', number: 1, isCommercial: true }, { id: 'f3', number: 3 }, { id: 'f4', number: 4 }];

test('apartments by default, «Коммерция» and «Паркинг» lists, the parking level on the rail, lifts in the panel', async ({ page }) => {
  await page.addInitScript(() => { try { localStorage.setItem('sitora.3dHint', '1'); } catch (e) { /* private mode */ } });
  await page.route('**/api/v1/**', (route) => {
    const p = new URL(route.request().url()).pathname.replace(/^\/api\/v1/, '');
    const json = (body) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    if (p === '/buildings') return json({ items: [building], totalCount: 1, page: 1, pageSize: 100 });
    if (p === '/buildings/' + B) return json(building);
    if (p === '/buildings/' + B + '/floors') return json({ items: floors.map((f) => Object.assign({ buildingId: B, totalApartments: 2, freeApartments: 2 }, f)), totalCount: 4 });
    const fm = p.match(/^\/floors\/f(m?)(\d+)\/apartments$/);
    if (fm) return json({ items: FLOORS[(fm[1] ? '-' : '') + fm[2]], totalCount: 2 });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{"title":"Не найдено"}' });
  });
  await page.goto('/apartments');
  const grid = page.locator('#apt-grid');
  await expect(grid.locator('.nr-unit-card')).toHaveCount(4);
  await expect(grid).not.toContainText('Помещение');
  // «Пентхаусы» — only the top floor, with the badge and the terrace.
  await page.locator('.nr-kinds [data-kind="penthouse"]').click();
  await expect(grid.locator('.nr-unit-card')).toHaveCount(2);
  for (const c of await grid.locator('.nr-unit-card').all()) { await expect(c).toContainText('Пентхаус'); await expect(c).toContainText('4 этаж'); }
  await grid.locator('.nr-unit-card').first().click();
  await expect(page.locator('.nr-detail')).toContainText('Терраса');
  await expect(page.locator('.nr-detail')).toContainText('3,6 м (оценка)');
  await page.locator('.nr-kinds [data-kind="commercial"]').click();
  await expect(grid.locator('.nr-unit-card')).toHaveCount(2);
  await expect(grid.locator('.nr-unit-card').first()).toContainText('Помещение');
  await page.locator('.nr-kinds [data-kind="parking"]').click();
  await expect(grid.locator('.nr-unit-card')).toHaveCount(2);
  await expect(grid.locator('.nr-unit-card').first()).toContainText('Машино-место');
  await expect(grid.locator('.nr-unit-card').first()).toContainText('$20 000');
  // The rail: −1 (parking), 1К (shops) and floor 3; the parking panel lists the spaces.
  await page.locator('.nr-rail-btn', { hasText: '−1' }).click();
  await expect(page.locator('.nr-panel-floor')).toHaveText('Паркинг −1');
  await expect(page.locator('.nr-floorpanel')).toContainText('Лифтов: 3');
  await expect(page.locator('.nr-floorpanel .nr-unit').first()).toContainText('Машино-место');
});
