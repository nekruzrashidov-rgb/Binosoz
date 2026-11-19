// Notifications in the cabinet («Настройки») with a mocked API: «Получать уведомления в Telegram» gives a t.me link that the
// person opens with their own click, «Я нажал «Старт»» shows the linked state, the checkboxes save at once (PUT),
// without a bot on the server — an honest «not available yet» instead of the button.
const { test, expect } = require('@playwright/test');

const USER = { id: 'u1', email: 'buyer@example.test', fullName: 'Покупатель Тестов', roles: ['Customer'], role: 'Customer' };
const DEAL = { dealId: 'd1', buildingId: 'b1', buildingName: 'Дом 1', apartmentNumber: 12, rooms: 2, stage: 'Payment', paidPercent: 40, myReview: null };

async function mockApi(page, o) {
  const s = { settings: { telegram: true, email: true, events: { bookings: true, deal: true, payments: true, documents: true, chat: true }, telegramLinked: false, telegramAvailable: o.bot !== false, emailAvailable: true, language: 'ru' }, puts: [], links: 0 };
  await page.route('**/health/live', (route) => route.fulfill({ status: 200, contentType: 'text/plain', body: 'Healthy' }));
  await page.route('**/api/v1/**', (route) => {
    const req = route.request();
    const p = new URL(req.url()).pathname.replace(/^\/api\/v1/, '');
    const json = (status, body) => route.fulfill({ status, contentType: status >= 400 ? 'application/problem+json' : 'application/json', body: JSON.stringify(body) });
    if (p === '/auth/me') return json(200, USER);
    if (p === '/auth/sessions') return json(200, [{ id: 's1', device: 'Chrome · Windows', startedAt: '2026-11-01T08:00:00Z', lastActiveAt: '2026-11-19T08:00:00Z', expiresAt: '2026-12-19T08:00:00Z', current: true }]);
    if (p === '/cabinet/overview') return json(200, { hasDeals: true, customerName: USER.fullName, deals: [DEAL], contacts: {} });
    if (p.startsWith('/deals/d1/messages/unread')) return json(200, { unread: 0 });
    if (p === '/profile/notifications' && req.method() === 'GET') return json(200, s.settings);
    if (p === '/profile/notifications' && req.method() === 'PUT') { const b = req.postDataJSON(); s.puts.push(b); Object.assign(s.settings, { telegram: b.telegram, email: b.email, events: b.events }); return json(200, s.settings); }
    if (p === '/profile/telegram/link') { s.links++; return json(200, { url: 'https://t.me/sitora_test_bot?start=tok123', expiresAt: new Date(Date.now() + 9e5).toISOString() }); }
    if (p === '/profile/telegram' && req.method() === 'DELETE') { s.settings.telegramLinked = false; return route.fulfill({ status: 204 }); }
    return json(404, { title: 'Не найдено' });
  });
  return s;
}

async function openSettings(page) {
  await page.addInitScript((u) => { if (!sessionStorage.getItem('seeded')) { localStorage.setItem('sitora.auth', JSON.stringify({ accessToken: 'a', refreshToken: 'r', user: u })); sessionStorage.setItem('seeded', '1'); } }, USER);
  await page.goto('/cabinet');
  await page.getByRole('tab', { name: /Настройки/ }).click();
  await expect(page.locator('.cab-notify h2')).toHaveText('Уведомления');
}

test('Telegram: a one-time t.me link opened by the person, then «linked» and «Отключить»', async ({ page }) => {
  const s = await mockApi(page, {});
  await openSettings(page);
  const card = page.locator('.cab-notify');
  // Not linked yet: the Telegram checkbox is off and disabled.
  await expect(card.locator('#ntf-telegram')).toBeDisabled();
  await card.locator('[data-tg-link]').click();
  const open = card.locator('[data-tg-open]');
  await expect(open).toHaveAttribute('href', 'https://t.me/sitora_test_bot?start=tok123');
  await expect(open).toHaveAttribute('target', '_blank');
  await expect(card).toContainText('15 минут');
  expect(s.links).toBe(1);

  s.settings.telegramLinked = true;
  await card.getByRole('button', { name: 'Я нажал «Старт»' }).click();
  await expect(card.locator('[data-tg-linked]')).toContainText('Telegram подключён');
  await expect(card.locator('#ntf-telegram')).toBeEnabled();
  await expect(card.locator('#ntf-telegram')).toBeChecked();

  await card.getByRole('button', { name: 'Отключить' }).click();
  await expect(card.locator('[data-tg-link]')).toBeVisible();
});

test('checkboxes save at once: e-mail off, deal stages off', async ({ page }) => {
  const s = await mockApi(page, {});
  await openSettings(page);
  const card = page.locator('.cab-notify');
  await card.locator('label[for="ntf-email"]').click();
  await expect.poll(() => s.puts.length).toBe(1);
  expect(s.puts[0].email).toBe(false);
  await card.locator('label[for="ntf-ev-deal"]').click();
  await expect.poll(() => s.puts.length).toBe(2);
  expect(s.puts[1].events).toEqual({ payments: true, bookings: true, deal: false, documents: true, chat: true });
});

test('no bot on the server: an honest «not available yet», no link button', async ({ page }) => {
  await mockApi(page, { bot: false });
  await openSettings(page);
  const card = page.locator('.cab-notify');
  await expect(card).toContainText('застройщик ещё не подключил бота');
  await expect(card.locator('[data-tg-link]')).toHaveCount(0);
});
