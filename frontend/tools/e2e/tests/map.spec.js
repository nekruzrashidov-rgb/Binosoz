// The Google map (feat(map)): the address and the point only from the API; no point and no address — no map; until the
// click nothing is loaded from Google (a card with the address and the links), after it — the Maps Embed iframe; the choice
// is remembered. CSP lets the iframe in (frame-src https://www.google.com) — no console errors.
const { test, expect } = require('@playwright/test');
const path = require('path');

// The site's CSP from frontend/vercel.json on the page itself (the static test server sends no headers).
const CSP = JSON.parse(require('fs').readFileSync(path.join(__dirname, '../../../vercel.json'), 'utf8')).headers
  .flatMap((x) => x.headers).find((x) => x.key === 'Content-Security-Policy').value;
async function withCsp(page, url) {
  // CSP violations are collected by the page itself (securitypolicyviolation) and must stay empty.
  await page.addInitScript(() => { window.__csp = []; document.addEventListener('securitypolicyviolation', (e) => window.__csp.push(e.violatedDirective + ' ' + e.blockedURI)); });
  await page.route(url, async (route) => {
    const r = await route.fetch();
    return route.fulfill({ response: r, headers: Object.assign({}, r.headers(), { 'content-security-policy': CSP }) });
  });
}

const ADDRESS = 'г. Душанбе, ул. Немата Карабаева, 22';
async function mockBrand(page, brand) {
  await page.route('**/api/v1/**', (route) => {
    const p = new URL(route.request().url()).pathname.replace(/^\/api\/v1/, '');
    if (p === '/public/brand') return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(Object.assign({ organizationId: 'o1', name: 'Rich Tower', slug: 'nekruz', buildings: [] }, brand)) });
    return route.fulfill({ status: 404, contentType: 'application/json', body: '{"title":"Не найдено"}' });
  });
  // The Google frame itself is not loaded in the tests (no network): an empty page instead.
  await page.route('https://www.google.com/**', (route) => route.fulfill({ status: 200, contentType: 'text/html', body: '<!doctype html><title>map</title>' }));
}
const consoleErrors = (page) => {
  const errors = [];
  page.on('console', (m) => { if (m.type() === 'error' && !/Failed to load resource|favicon/.test(m.text())) errors.push(m.text()); });
  page.on('pageerror', (e) => errors.push(String(e)));
  return errors;
};

test('no address and no point — no map, no footer link', async ({ page }) => {
  await mockBrand(page, {});
  await page.goto('/contact');
  await expect(page.locator('.nr-ct')).toBeVisible();
  await expect(page.locator('.nr-map')).toBeHidden();
  await expect(page.locator('.nr-map-sec')).toBeHidden();
  await expect(page.locator('.foot-map')).toBeHidden();
});

test('/contact: the card until the click, links with the point, then the Google iframe; remembered', async ({ page, isMobile }) => {
  const errors = consoleErrors(page);
  await withCsp(page, '**/contact');
  await mockBrand(page, { address: ADDRESS, latitude: 38.5751, longitude: 68.7811, mapsEmbedKey: 'k-test' });
  await page.goto('/contact');
  const map = page.locator('.nr-map');
  await expect(map).toBeVisible();
  await expect(map.locator('.nr-map-addr')).toHaveText(ADDRESS);
  await expect(map.locator('iframe')).toHaveCount(0);
  await expect(map.locator('.nr-map-note')).toHaveText('Карта загружается с серверов Google');
  await expect(map.getByRole('link', { name: 'Открыть в Google Картах' })).toHaveAttribute('href', 'https://www.google.com/maps/search/?api=1&query=38.5751%2C68.7811');
  await expect(map.getByRole('link', { name: 'Маршрут' })).toHaveAttribute('href', 'https://www.google.com/maps/dir/?api=1&destination=38.5751%2C68.7811');
  await expect(map.getByRole('link', { name: 'Маршрут' })).toHaveAttribute('rel', 'noopener');
  const show = map.getByRole('button', { name: 'Показать карту' });
  const box = await show.boundingBox();
  expect(box.height).toBeGreaterThanOrEqual(44);
  if (isMobile) {
    const w = await map.boundingBox();
    expect(w.width).toBeGreaterThanOrEqual(page.viewportSize().width - 48);
  }
  await show.click();
  const frame = map.locator('iframe');
  await expect(frame).toHaveAttribute('src', /^https:\/\/www\.google\.com\/maps\/embed\/v1\/place\?key=k-test&q=38\.5751%2C68\.7811&zoom=16&language=ru$/);
  await expect(frame).toHaveAttribute('loading', 'lazy');
  await expect(frame).toHaveAttribute('referrerpolicy', 'no-referrer-when-downgrade');
  await expect(frame).toHaveAttribute('title', 'Карта Google: ' + ADDRESS);
  await page.waitForTimeout(500);
  expect(await page.evaluate(() => window.__csp)).toEqual([]);
  // Remembered: the next visit shows the map at once.
  await page.reload();
  await expect(page.locator('.nr-map iframe')).toHaveCount(1);
  await expect(page.locator('.foot-map')).toHaveAttribute('href', /maps\/search\/\?api=1&query=38\.5751/);
  expect(errors).toEqual([]);
});

test('without the key: only the links, no iframe and no «Показать карту»', async ({ page }) => {
  await mockBrand(page, { address: ADDRESS, latitude: 38.5751, longitude: 68.7811 });
  await page.goto('/contact');
  await expect(page.locator('.nr-map')).toBeVisible();
  await expect(page.locator('.nr-map-show')).toHaveCount(0);
  await expect(page.locator('.nr-map iframe')).toHaveCount(0);
  await expect(page.locator('.nr-map').getByRole('link', { name: 'Открыть в Google Картах' })).toBeVisible();
});

test('the home page: «Как добраться» before «Остались вопросы?»', async ({ page }) => {
  await mockBrand(page, { address: ADDRESS, latitude: 38.5751, longitude: 68.7811, mapsEmbedKey: 'k-test' });
  await page.goto('/');
  const sec = page.locator('#map.nr-map-sec');
  await expect(sec).toBeVisible();
  await expect(sec.locator('.nr-h2')).toHaveText('Как добраться');
  const order = await page.evaluate(() => { const m = document.querySelector('#map'); const c = document.querySelector('#contact'); return !!(m && c && (m.compareDocumentPosition(c) & Node.DOCUMENT_POSITION_FOLLOWING)); });
  expect(order).toBe(true);
});

test('/privacy has the section «Карта Google»', async ({ page }) => {
  await page.goto('/privacy');
  await expect(page.getByRole('heading', { name: 'Карта Google' })).toBeVisible();
});
