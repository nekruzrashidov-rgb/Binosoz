// «Запомнить меня» with a mocked API: after signing in and reloading — the cabinet at once (an expired access token is
// refreshed quietly), the e-mail is remembered for the next sign-in («Войти как …», only the code is left), «Выйти»
// shows the sign-in form and forgets the e-mail, «Устройства» lists the sign-ins and «Выйти на всех устройствах».
const { test, expect } = require('@playwright/test');

const EMAIL = 'buyer@example.test';
const USER = { id: 'u1', email: EMAIL, fullName: 'Покупатель Тестов', roles: ['Customer'], role: 'Customer' };
const DEAL = { dealId: 'd1', buildingId: 'b1', buildingName: 'Дом 1', apartmentNumber: 12, rooms: 2, stage: 'Payment', paidPercent: 40, myReview: null };

async function mockApi(page) {
  const s = { access: 0, refreshes: 0, logouts: 0, revokedAll: 0, refreshBodies: [] };
  const tokens = () => { s.access++; return { accessToken: 'access-' + s.access, accessTokenExpiresAt: new Date(Date.now() + 9e5).toISOString(), refreshToken: 'refresh-' + s.access, refreshTokenExpiresAt: new Date(Date.now() + 30 * 864e5).toISOString(), user: USER }; };
  await page.route('**/health/live', (route) => route.fulfill({ status: 200, contentType: 'text/plain', body: 'Healthy' }));
  await page.route('**/api/v1/**', (route) => {
    const req = route.request();
    const p = new URL(req.url()).pathname.replace(/^\/api\/v1/, '');
    const json = (status, body) => route.fulfill({ status, contentType: status >= 400 ? 'application/problem+json' : 'application/json', body: JSON.stringify(body) });
    const bearer = (req.headers().authorization || '').replace('Bearer ', '');
    // Only the newest access token is alive: an older one (a reload after 15 minutes) gets 401 and must be refreshed.
    const alive = bearer === 'access-' + s.access && !s.expired;
    if (p === '/auth/methods') return json(200, { pin: true, email: true, sms: false, demo: false, telegram: false });
    if (p === '/auth/customer/login' && req.method() === 'POST') return json(200, tokens());
    if (p === '/auth/refresh') { s.refreshes++; s.refreshBodies.push(req.postData() || ''); s.expired = false; return s.revokedAll ? json(401, { detail: 'Сессия истекла' }) : json(200, tokens()); }
    if (p === '/auth/logout') { s.logouts++; return route.fulfill({ status: 204 }); }
    if (p === '/auth/sessions/revoke-all') { s.revokedAll++; return json(200, { revoked: 2 }); }
    if (!alive) return json(401, { title: 'Unauthorized' });
    if (p === '/auth/me') return json(200, USER);
    if (p === '/auth/sessions') return json(200, [
      { id: 's1', device: 'Chrome · Windows', startedAt: '2026-11-01T08:00:00Z', lastActiveAt: '2026-11-19T08:00:00Z', expiresAt: '2026-12-19T08:00:00Z', current: true },
      { id: 's2', device: 'Safari · iPhone', startedAt: '2026-10-20T08:00:00Z', lastActiveAt: '2026-11-10T08:00:00Z', expiresAt: '2026-12-10T08:00:00Z', current: false }]);
    if (p === '/cabinet/overview') return json(200, { hasDeals: true, customerName: USER.fullName, deals: [DEAL], contacts: {} });
    if (p.startsWith('/deals/d1/messages/unread')) return json(200, { unread: 0 });
    if (p === '/notifications/preferences') return json(200, { email: true, telegram: true, emailAvailable: true, telegramLinked: false, telegramAvailable: false, events: {} });
    return json(404, { title: 'Не найдено' });
  });
  return s;
}

async function signIn(page) {
  await page.goto('/login');
  await page.locator('input[name=email]').fill(EMAIL);
  await page.locator('.pin-cell[data-pin="pin"]').first().click();
  await page.keyboard.type('482915');
  await page.getByRole('button', { name: 'Войти', exact: true }).click();
  await expect(page).toHaveURL(/\/cabinet/);
  await expect(page.locator('.cab-welcome-name')).toBeVisible();
}

test('after sign-in and a reload — the cabinet at once, an expired access token is refreshed quietly', async ({ page }) => {
  const s = await mockApi(page);
  await signIn(page);
  s.expired = true;
  await page.reload();
  await expect(page.locator('.cab-welcome-name')).toBeVisible();
  await expect(page.locator('.login-card')).toHaveCount(0);
  expect(s.refreshes).toBe(1);
  // /login with a live session goes straight to the cabinet.
  await page.goto('/login');
  await expect(page).toHaveURL(/\/cabinet/);
  await expect(page.locator('.cab-welcome-name')).toBeVisible();
});

test('«Выйти»: the sign-in form again, the refresh token revoked, the e-mail forgotten', async ({ page }) => {
  const s = await mockApi(page);
  await signIn(page);
  expect(await page.evaluate(() => localStorage.getItem('sitora.lastEmail'))).toBe(EMAIL);
  await page.getByRole('button', { name: 'Выйти', exact: true }).first().click();
  await expect(page).not.toHaveURL(/\/cabinet/);
  await page.goto('/cabinet');
  await expect(page.locator('.login-card input[name=email]')).toBeVisible();
  await expect(page.locator('[data-known]')).toHaveCount(0);
  expect(s.logouts).toBe(1);
  expect(await page.evaluate(() => localStorage.getItem('sitora.lastEmail'))).toBeNull();
});

test('a finished session: «Войти как …» remembers the e-mail, only the code is left; «Сменить» shows the field', async ({ page }) => {
  await mockApi(page);
  await page.goto('/login');
  await page.evaluate((e) => localStorage.setItem('sitora.lastEmail', e), EMAIL);
  await page.reload();
  const known = page.locator('[data-known]');
  await expect(known).toContainText(EMAIL);
  await expect(page.locator('input[name=email]')).toHaveAttribute('type', 'hidden');
  await expect(page.locator('.pin-cell[data-pin="pin"]').first()).toBeFocused();
  await known.getByRole('button').click();
  await expect(page.locator('input[name=email]')).toHaveAttribute('type', 'email');
  expect(await page.evaluate(() => localStorage.getItem('sitora.lastEmail'))).toBeNull();
});

test('«Устройства»: this device is marked, «Выйти на всех устройствах» signs out', async ({ page }) => {
  const s = await mockApi(page);
  await signIn(page);
  await page.getByRole('tab', { name: /Настройки/ }).click();
  const devices = page.locator('.cab-devices');
  await expect(devices.locator('.dev-row')).toHaveCount(2);
  await expect(devices.locator('.dev-row.current')).toContainText('Это устройство');
  await expect(devices.locator('.dev-row.current')).toContainText('Chrome · Windows');
  await devices.locator('[data-logout-all]').click();
  await page.getByRole('dialog').getByRole('button', { name: 'Выйти на всех устройствах' }).click();
  await expect(page).toHaveURL(/\/login/);
  expect(s.revokedAll).toBe(1);
  await expect(page.locator('input[name=email]')).toHaveAttribute('type', 'email');
});
