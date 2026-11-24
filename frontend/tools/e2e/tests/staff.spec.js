// CRM and platform with a mocked API.
// R-06: a session whose access token has expired (a page opened after 15 minutes) is refreshed before /auth/me — no 401
// in the network and no errors in the console on /crm and /platform.
// R-05 / C-13: on a 375 px phone the floating ☰ and ✦ buttons do not cover the last table row or the last form field.
// K-02: the name in the top bar opens a menu with «Профиль и настройки», «Устройства» and «Выйти».
const { test, expect } = require('@playwright/test');

const OWNER = { id: 'u1', email: 'owner@example.test', fullName: 'Владелец Тестов', roles: ['Owner'], role: 'Owner', organizationId: 'o1', organizationName: 'Тест' };
const PLATFORM = { id: 'u2', email: 'super@example.test', fullName: 'Платформа Тестов', roles: ['SuperAdmin'], role: 'SuperAdmin' };

// A JWT-shaped token (the server signs real ones; the site only reads exp).
const jwt = (exp) => 'h.' + Buffer.from(JSON.stringify({ exp })).toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_') + '.s';
const LEADS = Array.from({ length: 14 }, (_, i) => ({ id: 'l' + i, name: 'Заявка ' + (i + 1), phone: '+99290000000' + i, status: 'New', source: 'Website', createdAt: '2026-11-20T08:00:00Z' }));

async function mockApi(page, user) {
  const s = { unauthorized: [], refreshes: 0 };
  await page.route('**/health/live', (route) => route.fulfill({ status: 200, contentType: 'application/json', body: '{"status":"Healthy"}' }));
  await page.route('**/api/v1/**', (route) => {
    const req = route.request();
    const p = new URL(req.url()).pathname.replace(/^\/api\/v1/, '');
    const json = (status, body) => route.fulfill({ status, contentType: status >= 400 ? 'application/problem+json' : 'application/json', body: JSON.stringify(body) });
    const bearer = (req.headers().authorization || '').replace('Bearer ', '');
    if (p === '/auth/refresh') { s.refreshes++; return json(200, { accessToken: jwt(Math.floor(Date.now() / 1000) + 900), refreshToken: 'r2', user }); }
    // Only a token that is not expired is accepted, like the real API.
    let exp = 0; try { exp = JSON.parse(Buffer.from(bearer.split('.')[1], 'base64').toString()).exp; } catch (e) { exp = 0; }
    if (exp * 1000 < Date.now()) { s.unauthorized.push(p); return json(401, { title: 'Требуется вход', status: 401 }); }
    if (p === '/auth/me') return json(200, user);
    if (p === '/auth/sessions') return json(200, [{ id: 's1', device: 'Chrome · Windows', startedAt: '2026-11-01T08:00:00Z', lastActiveAt: '2026-11-24T08:00:00Z', expiresAt: '2026-12-24T08:00:00Z', current: true }]);
    if (p === '/leads') return json(200, { items: LEADS, page: 1, pageSize: 100, totalCount: LEADS.length });
    // One building, so the CRM opens its sections and not the first-run wizard.
    if (p === '/buildings') return json(200, { items: [{ id: 'b1', nameRu: 'Дом 1', name: 'Дом 1', floorsCount: 5, organizationId: 'o1' }], page: 1, pageSize: 100, totalCount: 1 });
    if (p.startsWith('/notifications/unread')) return json(200, { count: 0 });
    if (p === '/platform/settings') return json(200, { maxOrganizations: null, demoMode: false, version: 'abc1234', contactEmail: null, organizations: 1, builtAt: '2026-11-24T10:00:00Z' });
    if (/count|summary|stats|dashboard|overview|status|tariff|brand|settings/.test(p)) return json(200, {});
    return json(200, []);
  });
  await page.addInitScript((u) => {
    if (sessionStorage.getItem('seeded')) return;
    // The access token expired a minute ago; the refresh token is alive.
    const p = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) - 60 })).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
    localStorage.setItem('sitora.auth', JSON.stringify({ accessToken: 'h.' + p + '.s', refreshToken: 'r1', user: Object.assign({ name: u.fullName }, u) }));
    sessionStorage.setItem('seeded', '1');
  }, user);
  return s;
}

// /crm opens on «Заявки»: the dashboard needs a full analytics mock, and the check is about the page load and the token.
for (const [path, user] of [['/crm#leads', OWNER], ['/platform', PLATFORM]]) {
  test(`${path}: an expired access token is refreshed first — no 401 and no console errors (R-06)`, async ({ page }) => {
    const errors = [];
    page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('pageerror', (e) => errors.push(e.message));
    const s = await mockApi(page, user);
    await page.goto(path);
    await expect(page.locator('.userchip')).toBeVisible();
    await page.waitForLoadState('networkidle');
    expect(s.refreshes).toBe(1);
    expect(s.unauthorized).toEqual([]);
    expect(errors).toEqual([]);
  });
}

test('the name in the top bar opens a menu: profile and settings, devices, sign out (K-02)', async ({ page }) => {
  await mockApi(page, OWNER);
  await page.goto('/crm');
  const chip = page.locator('button.userchip');
  await chip.click();
  await expect(chip).toHaveAttribute('aria-expanded', 'true');
  const menu = page.locator('.user-menu');
  await expect(menu.getByRole('menuitem')).toHaveText(['Профиль и настройки', 'Устройства', 'Выйти']);
  await menu.getByRole('menuitem', { name: 'Устройства' }).click();
  await expect(page.locator('.dev-row.current')).toBeVisible();
  await page.keyboard.press('Escape');
  await chip.click();
  await page.keyboard.press('Escape');
  await expect(menu).toBeHidden();
});

test.describe('375 px phone', () => {
  test.use({ viewport: { width: 375, height: 812 } });

  test('the floating buttons do not cover the last table row (R-05 / C-13)', async ({ page }) => {
    await mockApi(page, OWNER);
    await page.goto('/crm#leads');
    await expectNotCovered(page, page.locator('.main table tbody tr').last(), 'the last row');
  });

  test('the floating buttons do not cover the last form field (R-05 / C-13)', async ({ page }) => {
    await mockApi(page, OWNER);
    await page.goto('/crm#settings');
    await expectNotCovered(page, page.locator('.main form .inp').last(), 'the last field');
  });
});

// Scrolled to the very bottom (where ☰ and ✦ sit over the content), the element must not intersect either button.
async function expectNotCovered(page, target, what) {
  await expect(target).toBeVisible();
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await page.waitForTimeout(400);
  const box = await target.boundingBox();
  for (const sel of ['.mob-sidebar-toggle', '.ai-fab']) {
    const btn = page.locator(sel);
    if (!(await btn.count()) || !(await btn.isVisible())) continue;
    const b = await btn.boundingBox();
    const overlap = !(b.x + b.width <= box.x || box.x + box.width <= b.x || b.y + b.height <= box.y || box.y + box.height <= b.y);
    expect(overlap, `${sel} covers ${what}`).toBe(false);
  }
}
