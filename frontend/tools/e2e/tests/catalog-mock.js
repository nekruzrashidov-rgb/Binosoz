// A small catalog from a mocked API for the e2e tests (the site is static here): two floors with 1-, 2-, 3- and
// 4-room apartments, all free. Not a test file itself (no .spec).
const B = 'b0000000-0000-4000-8000-000000000004';
const ROOMS = [1, 2, 3, 4], AREA = [42.5, 68, 92, 118.5];
const apartments = (floor) => ROOMS.map((rooms, k) => ({ id: `c${floor}${k}-0000-4000-8000-000000000000`, number: floor * 100 + k + 1, rooms, area: AREA[k], pricePerM2: 6500, price: Math.round(AREA[k] * 6500), currency: 'TJS', status: 'Free', planSlot: k }));
const building = { id: B, name: 'Тестовый дом', nameRu: 'Тестовый дом', nameTg: 'Хонаи санҷишӣ', address: 'Душанбе', floorsCount: 2, totalApartments: 8, freeApartments: 8 };

// requests — a list the mock fills with every non-GET request (the tests check that nothing was sent).
async function mockCatalog(page, requests) {
  await page.addInitScript(() => { try { localStorage.setItem('sitora.3dHint', '1'); } catch (e) { /* private mode */ } });
  await page.route('**/api/v1/**', (route) => {
    const req = route.request();
    if (req.method() !== 'GET' && requests) requests.push(req.method() + ' ' + req.url());
    const p = new URL(req.url()).pathname.replace(/^\/api\/v1/, '');
    const json = (body) => route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });
    if (p === '/buildings') return json({ items: [building], totalCount: 1, page: 1, pageSize: 100 });
    if (p === '/buildings/' + B) return json(building);
    if (p === '/buildings/' + B + '/floors') return json({ items: [1, 2].map((n) => ({ id: 'f' + n, buildingId: B, number: n, totalApartments: 4, freeApartments: 4 })), totalCount: 2 });
    const fm = p.match(/^\/floors\/f(\d+)\/apartments$/);
    if (fm) return json({ items: apartments(+fm[1]), totalCount: 4 });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{"title":"Не найдено"}' });
  });
}

module.exports = { mockCatalog };
